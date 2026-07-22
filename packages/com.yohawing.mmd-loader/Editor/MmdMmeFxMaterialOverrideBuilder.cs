#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Mmd.Mme;
using Mmd.Rendering;
using Mmd.UnityIntegration;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Mmd.Editor
{
    public static class MmdMmeFxMaterialOverrideBuilder
    {
        private const string DotEmdExtension = ".emd";

        /// <summary>
        /// Scans the MME files next to a PMX source and applies the resulting
        /// material overrides to the importer-owned descriptor and materials.
        /// The temporary override asset is intentionally not persisted; source
        /// files and resolvable texture references are tracked as importer
        /// dependencies instead.
        /// </summary>
        internal static void ApplyScannedMaterialOverrides(
            AssetImportContext ctx,
            string modelSourcePath,
            MmdRenderingDescriptor? renderingDescriptor,
            Material[]? materials)
        {
            if (ctx == null ||
                string.IsNullOrWhiteSpace(modelSourcePath) ||
                renderingDescriptor?.materials == null ||
                renderingDescriptor.materials.Count == 0 ||
                materials == null ||
                materials.Length == 0)
            {
                return;
            }

            IReadOnlyList<MmeFxEffectDescriptor> effectDescriptors =
                MmeFxScanner.ScanFromModelPath(modelSourcePath, renderingDescriptor.materials.Count);
            RegisterImportDependencies(ctx, modelSourcePath, effectDescriptors);

            MmdMaterialOverrideEntry[] entries =
                BuildMaterialOverrides(renderingDescriptor.materials, effectDescriptors);
            if (entries.Length == 0)
            {
                return;
            }

            var overrideAsset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
            try
            {
                overrideAsset.entries = entries;
                MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(overrideAsset, renderingDescriptor);
                MmdMaterialOverrideApplier.Apply(overrideAsset, materials);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overrideAsset);
            }
        }

        public static MmdMaterialOverrideEntry[] BuildMaterialOverrides(
            IReadOnlyList<MmdMaterialDescriptor>? materialDescriptors,
            IReadOnlyList<MmeFxEffectDescriptor>? effectDescriptors)
        {
            if (materialDescriptors == null || materialDescriptors.Count == 0 ||
                effectDescriptors == null || effectDescriptors.Count == 0)
            {
                return Array.Empty<MmdMaterialOverrideEntry>();
            }

            var entries = new List<MmdMaterialOverrideEntry>();
            var assignedMaterialIndices = new HashSet<int>();
            AppendMaterialOverrides(materialDescriptors, effectDescriptors, requireMaterialIndex: true, assignedMaterialIndices, entries);
            AppendMaterialOverrides(materialDescriptors, effectDescriptors, requireMaterialIndex: false, assignedMaterialIndices, entries);

            return entries.ToArray();
        }

        private static void AppendMaterialOverrides(
            IReadOnlyList<MmdMaterialDescriptor> materialDescriptors,
            IReadOnlyList<MmeFxEffectDescriptor> effectDescriptors,
            bool requireMaterialIndex,
            HashSet<int> assignedMaterialIndices,
            List<MmdMaterialOverrideEntry> entries)
        {
            for (int i = 0; i < effectDescriptors.Count; i++)
            {
                MmeFxEffectDescriptor? effectDescriptor = effectDescriptors[i];
                if (effectDescriptor == null || IsEmdDescriptor(effectDescriptor))
                {
                    continue;
                }

                bool hasMaterialIndex = effectDescriptor.materialIndex >= 0;
                if (hasMaterialIndex != requireMaterialIndex)
                {
                    continue;
                }

                MmdMaterialDescriptor? matchedMaterial = TryFindMaterial(materialDescriptors, effectDescriptor);
                if (matchedMaterial == null)
                {
                    continue;
                }

                if (!assignedMaterialIndices.Add(matchedMaterial.materialIndex))
                {
                    continue;
                }

                var entry = new MmdMaterialOverrideEntry
                {
                    enabled = true,
                    materialIndex = matchedMaterial.materialIndex,
                    materialName = matchedMaterial.name,
                    matchMode = MmdMaterialOverrideMatchMode.IndexThenName,
                    sourceKind = MmdMaterialOverrideSourceKind.MmeFx,
                    sourcePath = effectDescriptor.sourcePath ?? string.Empty,
                    effectType = effectDescriptor.effectType ?? string.Empty
                };

                Texture2D? normalMap = ResolveNormalMap(effectDescriptor);
                if (normalMap != null)
                {
                    entry.hasNormalMap = true;
                    entry.normalMap = normalMap;
                }

                if (TryGetFiniteScalar(effectDescriptor.metalness, out float metallic))
                {
                    entry.hasMetallic = true;
                    entry.metallic = metallic;
                }

                if (TryGetFiniteScalar(effectDescriptor.smoothness, out float smoothness))
                {
                    entry.hasSmoothness = true;
                    entry.smoothness = smoothness;
                }

                entries.Add(entry);
            }
        }

        private static MmdMaterialDescriptor? TryFindMaterial(
            IReadOnlyList<MmdMaterialDescriptor> materialDescriptors,
            MmeFxEffectDescriptor effectDescriptor)
        {
            if (effectDescriptor.materialIndex >= 0)
            {
                return TryFindMaterialByIndex(materialDescriptors, effectDescriptor.materialIndex);
            }

            string materialName = Path.GetFileNameWithoutExtension(effectDescriptor.sourcePath ?? string.Empty);
            return TryFindMaterialByName(materialDescriptors, materialName);
        }

        private static MmdMaterialDescriptor? TryFindMaterialByIndex(
            IReadOnlyList<MmdMaterialDescriptor> materialDescriptors,
            int materialIndex)
        {
            for (int i = 0; i < materialDescriptors.Count; i++)
            {
                MmdMaterialDescriptor? materialDescriptor = materialDescriptors[i];
                if (materialDescriptor != null && materialDescriptor.materialIndex == materialIndex)
                {
                    return materialDescriptor;
                }
            }

            return null;
        }

        private static MmdMaterialDescriptor? TryFindMaterialByName(
            IReadOnlyList<MmdMaterialDescriptor> materialDescriptors,
            string materialName)
        {
            for (int i = 0; i < materialDescriptors.Count; i++)
            {
                MmdMaterialDescriptor? materialDescriptor = materialDescriptors[i];
                if (materialDescriptor != null &&
                    string.Equals(materialDescriptor.name, materialName, StringComparison.Ordinal))
                {
                    return materialDescriptor;
                }
            }

            return null;
        }

        private static bool IsEmdDescriptor(MmeFxEffectDescriptor descriptor)
        {
            return string.Equals(Path.GetExtension(descriptor.sourcePath), DotEmdExtension, StringComparison.OrdinalIgnoreCase);
        }

        private static Texture2D? ResolveNormalMap(MmeFxEffectDescriptor descriptor)
        {
            Texture2D? normalMap = descriptor.useNormalMap
                ? TryResolveTexture(descriptor.sourcePath, descriptor.normalMapTexture)
                : null;
            return normalMap ?? TryResolveTexture(descriptor.sourcePath, descriptor.normalMapFile);
        }

        private static Texture2D? TryResolveTexture(string sourcePath, string? relativeReference)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(relativeReference))
            {
                return null;
            }

            if (!MmdAssetPathUtility.TryResolveProjectRelativeAssetPath(
                    sourcePath,
                    relativeReference,
                    out string candidateAssetPath))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(candidateAssetPath);
        }

        private static bool TryGetFiniteScalar(float? value, out float scalar)
        {
            scalar = default;
            if (!value.HasValue || float.IsNaN(value.Value) || float.IsInfinity(value.Value))
            {
                return false;
            }

            scalar = value.Value;
            return true;
        }

        private static void RegisterImportDependencies(
            AssetImportContext ctx,
            string modelSourcePath,
            IReadOnlyList<MmeFxEffectDescriptor> effectDescriptors)
        {
            RegisterDependency(ctx, Path.ChangeExtension(modelSourcePath, DotEmdExtension));
            for (int i = 0; i < effectDescriptors.Count; i++)
            {
                MmeFxEffectDescriptor? descriptor = effectDescriptors[i];
                if (descriptor == null)
                {
                    continue;
                }

                RegisterDependency(ctx, descriptor.sourcePath);
                RegisterReferencedTexture(ctx, descriptor.sourcePath, descriptor.normalMapTexture);
                RegisterReferencedTexture(ctx, descriptor.sourcePath, descriptor.normalMapFile);
                RegisterReferencedTexture(ctx, descriptor.sourcePath, descriptor.thresholdTexture);
                RegisterReferencedTexture(ctx, descriptor.sourcePath, descriptor.albedoMapTexture);
                RegisterReferencedTexture(ctx, descriptor.sourcePath, descriptor.smoothnessMapFile);
                RegisterReferencedTexture(ctx, descriptor.sourcePath, descriptor.metalnessMapFile);
            }
        }

        private static void RegisterReferencedTexture(
            AssetImportContext ctx,
            string sourcePath,
            string? relativeReference)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(relativeReference) ||
                !MmdAssetPathUtility.TryResolveProjectRelativeAssetPath(
                    sourcePath,
                    relativeReference,
                    out string candidateAssetPath))
            {
                return;
            }

            RegisterDependency(ctx, candidateAssetPath);
        }

        private static void RegisterDependency(AssetImportContext ctx, string? path)
        {
            if (!TryGetProjectRelativeAssetPath(path, out string assetPath))
            {
                return;
            }

            ctx.DependsOnSourceAsset(assetPath);
        }

        private static bool TryGetProjectRelativeAssetPath(
            string? path,
            out string assetPath)
        {
            assetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string candidateAbsolutePath = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(projectRoot, path));
            string relativePath = Path.GetRelativePath(projectRoot, candidateAbsolutePath).Replace('\\', '/');
            if (!relativePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(candidateAbsolutePath))
            {
                return false;
            }

            assetPath = relativePath;
            return true;
        }
    }
}
