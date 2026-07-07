#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Mmd.Mme;
using Mmd.Rendering;
using UnityEditor;
using UnityEngine;

namespace Mmd.Editor
{
    public static class MmdMmeFxMaterialOverrideBuilder
    {
        private const string DotEmdExtension = ".emd";

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
            for (int i = 0; i < effectDescriptors.Count; i++)
            {
                MmeFxEffectDescriptor? effectDescriptor = effectDescriptors[i];
                if (effectDescriptor == null || IsEmdDescriptor(effectDescriptor))
                {
                    continue;
                }

                string materialName = Path.GetFileNameWithoutExtension(effectDescriptor.sourcePath ?? string.Empty);
                MmdMaterialDescriptor? matchedMaterial = TryFindMaterialByName(materialDescriptors, materialName);
                if (matchedMaterial == null)
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

            return entries.ToArray();
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
    }
}
