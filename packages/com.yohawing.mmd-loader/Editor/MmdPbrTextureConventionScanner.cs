#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Mmd.Parser;
using Mmd.UnityIntegration;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Mmd.Editor
{
    public static class MmdPbrTextureConventionScanner
    {
        private static readonly string[] TextureExtensions = { ".png", ".tga", ".jpg", ".jpeg" };
        private static readonly string[] NormalSuffixes = { "_normal", "_Normal", "_normalmap", "_NormalMap", "_n", "_N", "_nm", "_NM" };
        private static readonly string[] MetallicSuffixes = { "_metallic", "_Metallic", "_metalness", "_Metalness", "_metal", "_Metal", "_m", "_M" };
        private static readonly string[] RoughnessSuffixes = { "_roughness", "_Roughness", "_rough", "_Rough", "_r", "_R" };
        private static readonly string[] OcclusionSuffixes = { "_ao", "_AO", "_occlusion", "_Occlusion", "_ambientocclusion", "_AmbientOcclusion" };

        public static MmdMaterialOverrideEntry[] BuildMaterialOverrides(
            MmdModelDefinition? model,
            string pmxAssetPath)
        {
            if (model?.materials == null ||
                model.materials.Count == 0 ||
                string.IsNullOrWhiteSpace(pmxAssetPath))
            {
                return Array.Empty<MmdMaterialOverrideEntry>();
            }

            var entries = new List<MmdMaterialOverrideEntry>();
            for (int i = 0; i < model.materials.Count; i++)
            {
                MmdMaterialDefinition? material = model.materials[i];
                if (material == null)
                {
                    continue;
                }

                Texture2D? normalMap = TryResolveRoleTexture(pmxAssetPath, material, NormalSuffixes);
                Texture2D? metallicMap = TryResolveRoleTexture(pmxAssetPath, material, MetallicSuffixes);
                Texture2D? roughnessMap = TryResolveRoleTexture(pmxAssetPath, material, RoughnessSuffixes);
                Texture2D? occlusionMap = TryResolveRoleTexture(pmxAssetPath, material, OcclusionSuffixes);
                if (normalMap == null && metallicMap == null && roughnessMap == null && occlusionMap == null)
                {
                    continue;
                }

                var entry = new MmdMaterialOverrideEntry
                {
                    enabled = true,
                    materialIndex = material.index,
                    materialName = material.name ?? string.Empty,
                    matchMode = MmdMaterialOverrideMatchMode.IndexThenName,
                    sourceKind = MmdMaterialOverrideSourceKind.TextureScan,
                    sourcePath = FirstAssetPath(normalMap, metallicMap, roughnessMap, occlusionMap),
                    effectType = "pbr-texture-scan"
                };

                if (normalMap != null)
                {
                    entry.hasNormalMap = true;
                    entry.normalMap = normalMap;
                }

                if (metallicMap != null)
                {
                    entry.hasMetallicMap = true;
                    entry.metallicMap = metallicMap;
                    entry.metallicMapIncludesSmoothness = true;
                    if (roughnessMap == null)
                    {
                        entry.hasSmoothness = true;
                        entry.smoothness = 0.5f;
                    }
                }

                if (roughnessMap != null)
                {
                    entry.hasRoughnessMap = true;
                    entry.roughnessMap = roughnessMap;
                    if (TryEstimateSmoothnessFromRoughnessMap(roughnessMap, out float smoothness))
                    {
                        entry.hasSmoothness = true;
                        entry.smoothness = smoothness;
                    }
                }

                if (occlusionMap != null)
                {
                    entry.hasOcclusionMap = true;
                    entry.occlusionMap = occlusionMap;
                    entry.hasOcclusionStrength = true;
                    entry.occlusionStrength = 1.0f;
                }

                entries.Add(entry);
            }

            return entries.ToArray();
        }

        internal static void ApplyScannedMaterialOverrides(
            AssetImportContext ctx,
            MmdModelDefinition model,
            string pmxAssetPath,
            Material[] materials)
        {
            foreach (string candidateAssetPath in EnumerateConventionAssetPathCandidates(model, pmxAssetPath))
            {
                ctx.DependsOnSourceAsset(candidateAssetPath);
            }

            MmdMaterialOverrideEntry[] entries = BuildMaterialOverrides(model, pmxAssetPath);
            if (entries.Length == 0)
            {
                return;
            }

            var overrideAsset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
            try
            {
                overrideAsset.entries = entries;
                foreach (MmdMaterialOverrideEntry entry in entries)
                {
                    RegisterDependency(ctx, entry.normalMap);
                    RegisterDependency(ctx, entry.metallicMap);
                    RegisterDependency(ctx, entry.roughnessMap);
                    RegisterDependency(ctx, entry.occlusionMap);
                }

                Mmd.UnityIntegration.MmdMaterialOverrideApplier.Apply(overrideAsset, materials);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overrideAsset);
            }
        }

        internal static string[] EnumerateConventionAssetPathCandidates(
            MmdModelDefinition? model,
            string pmxAssetPath)
        {
            if (model?.materials == null ||
                model.materials.Count == 0 ||
                string.IsNullOrWhiteSpace(pmxAssetPath))
            {
                return Array.Empty<string>();
            }

            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < model.materials.Count; i++)
            {
                MmdMaterialDefinition? material = model.materials[i];
                if (material == null)
                {
                    continue;
                }

                foreach ((string directory, string basename) in EnumerateCandidateBases(material))
                {
                    AddRoleCandidates(pmxAssetPath, directory, basename, NormalSuffixes, paths, seen);
                    AddRoleCandidates(pmxAssetPath, directory, basename, MetallicSuffixes, paths, seen);
                    AddRoleCandidates(pmxAssetPath, directory, basename, RoughnessSuffixes, paths, seen);
                    AddRoleCandidates(pmxAssetPath, directory, basename, OcclusionSuffixes, paths, seen);
                }
            }

            return paths.ToArray();
        }

        private static Texture2D? TryResolveRoleTexture(
            string pmxAssetPath,
            MmdMaterialDefinition material,
            IReadOnlyList<string> suffixes)
        {
            foreach ((string directory, string basename) in EnumerateCandidateBases(material))
            {
                foreach (string suffix in suffixes)
                {
                    foreach (string extension in TextureExtensions)
                    {
                        string relativeReference = BuildRelativeReference(directory, basename, suffix, extension);
                        if (MmdAssetPathUtility.TryResolveProjectRelativeAssetPath(
                                pmxAssetPath,
                                relativeReference,
                                out string candidateAssetPath))
                        {
                            Texture2D? texture = AssetDatabase.LoadAssetAtPath<Texture2D>(candidateAssetPath);
                            if (texture != null)
                            {
                                return texture;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static void AddRoleCandidates(
            string pmxAssetPath,
            string directory,
            string basename,
            IReadOnlyList<string> suffixes,
            List<string> paths,
            HashSet<string> seen)
        {
            foreach (string suffix in suffixes)
            {
                foreach (string extension in TextureExtensions)
                {
                    string relativeReference = BuildRelativeReference(directory, basename, suffix, extension);
                    if (MmdAssetPathUtility.TryResolveProjectRelativeAssetPathCandidate(
                            pmxAssetPath,
                            relativeReference,
                            out string candidateAssetPath) &&
                        seen.Add(candidateAssetPath))
                    {
                        paths.Add(candidateAssetPath);
                    }
                }
            }
        }

        private static string BuildRelativeReference(string directory, string basename, string suffix, string extension)
        {
            return string.IsNullOrEmpty(directory)
                ? basename + suffix + extension
                : directory + "/" + basename + suffix + extension;
        }

        private static bool TryEstimateSmoothnessFromRoughnessMap(Texture2D roughnessMap, out float smoothness)
        {
            smoothness = default;
            TexturePixelData? data = TryReadTexturePixels(roughnessMap);
            if (data == null || data.Value.Pixels.Length == 0)
            {
                return false;
            }

            Color32[] pixels = data.Value.Pixels;
            double roughnessSum = 0.0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                roughnessSum += (pixel.r + pixel.g + pixel.b) / (3.0 * 255.0);
            }

            smoothness = Mathf.Clamp01(1.0f - (float)(roughnessSum / pixels.Length));
            return true;
        }

        private static TexturePixelData? TryReadTexturePixels(Texture2D texture)
        {
            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
                if (File.Exists(absolutePath))
                {
                    Texture2D? decoded = null;
                    try
                    {
                        decoded = MmdRuntimeTextureResolver.DecodeTextureBytes(
                            File.ReadAllBytes(absolutePath),
                            Path.GetExtension(absolutePath),
                            Path.GetFileNameWithoutExtension(absolutePath));
                        return decoded != null
                            ? new TexturePixelData(decoded.GetPixels32(), decoded.width, decoded.height)
                            : null;
                    }
                    catch (IOException)
                    {
                        return null;
                    }
                    finally
                    {
                        if (decoded != null)
                        {
                            UnityEngine.Object.DestroyImmediate(decoded);
                        }
                    }
                }
            }

            try
            {
                return new TexturePixelData(texture.GetPixels32(), texture.width, texture.height);
            }
            catch (Exception ex) when (ex is UnityException || ex is ArgumentException || ex is NotSupportedException)
            {
                return null;
            }
        }

        private readonly struct TexturePixelData
        {
            public TexturePixelData(Color32[] pixels, int width, int height)
            {
                Pixels = pixels;
                Width = width;
                Height = height;
            }

            public Color32[] Pixels { get; }

            public int Width { get; }

            public int Height { get; }
        }

        private static IEnumerable<(string Directory, string Basename)> EnumerateCandidateBases(MmdMaterialDefinition material)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string directory, string basename) in EnumerateOne(material.texture))
            {
                string key = directory + "/" + basename;
                if (seen.Add(key))
                {
                    yield return (directory, basename);
                }
            }

            foreach ((string directory, string basename) in EnumerateOne(material.name))
            {
                string key = directory + "/" + basename;
                if (seen.Add(key))
                {
                    yield return (directory, basename);
                }
            }

            static IEnumerable<(string Directory, string Basename)> EnumerateOne(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    yield break;
                }

                string normalized = value.Replace('\\', '/');
                string directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/') ?? string.Empty;
                string basename = Path.GetFileNameWithoutExtension(normalized);
                if (string.IsNullOrWhiteSpace(basename))
                {
                    yield break;
                }

                yield return (directory, basename);
            }
        }

        private static string FirstAssetPath(params Texture2D?[] textures)
        {
            foreach (Texture2D? texture in textures)
            {
                if (texture == null)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(texture);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }

        private static void RegisterDependency(AssetImportContext ctx, Texture2D? texture)
        {
            if (texture == null)
            {
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(assetPath))
            {
                ctx.DependsOnSourceAsset(assetPath);
            }
        }
    }
}
