#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Mmd.Editor
{
    /// <summary>
    /// Applies Unity's normal-map import semantics before a project texture is imported
    /// when a neighboring MME effect explicitly names it as a normal map.
    /// </summary>
    public sealed class MmdMmeNormalMapTexturePostprocessor : AssetPostprocessor
    {
        private static readonly Regex ExplicitNormalMapDefineRegex = new(
            @"^\s*#define\s+(?:TEXTURE_NORMALMAP|NORMAL_MAP_FILE)\s+""([^""]+)""\s*(?://.*)?$",
            RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly HashSet<string> PendingExplicitNormalMaps = new(
            StringComparer.OrdinalIgnoreCase);

        public void OnPreprocessTexture()
        {
            if (assetImporter is not TextureImporter importer ||
                (!PendingExplicitNormalMaps.Contains(assetPath) && !IsExplicitNormalMapAsset(assetPath)))
            {
                return;
            }

            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var normalMapsToReimport = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectReferencedNormalMaps(importedAssets, normalMapsToReimport);
            CollectReferencedNormalMaps(movedAssets, normalMapsToReimport);
            foreach (string normalMapAssetPath in normalMapsToReimport)
            {
                if (AssetImporter.GetAtPath(normalMapAssetPath) is TextureImporter importer &&
                    importer.textureType == TextureImporterType.NormalMap &&
                    !importer.sRGBTexture)
                {
                    continue;
                }

                PendingExplicitNormalMaps.Add(normalMapAssetPath);
                try
                {
                    AssetDatabase.ImportAsset(
                        normalMapAssetPath,
                        ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }
                finally
                {
                    PendingExplicitNormalMaps.Remove(normalMapAssetPath);
                }
            }
        }

        private static void CollectReferencedNormalMaps(
            string[] assetPaths,
            HashSet<string> destination)
        {
            foreach (string assetPath in assetPaths)
            {
                if (!string.Equals(Path.GetExtension(assetPath), ".fx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (string normalMapAssetPath in GetExplicitNormalMapAssetPaths(assetPath))
                {
                    destination.Add(normalMapAssetPath);
                }
            }
        }

        internal static IReadOnlyList<string> GetExplicitNormalMapAssetPaths(string fxAssetPath)
        {
            string normalizedFxAssetPath = fxAssetPath.Replace('\\', '/');
            if (!normalizedFxAssetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<string>();
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            string fxAbsolutePath = Path.GetFullPath(Path.Combine(
                projectRoot,
                normalizedFxAssetPath.Replace('/', Path.DirectorySeparatorChar)));
            string? fxDirectory = Path.GetDirectoryName(fxAbsolutePath);
            if (string.IsNullOrWhiteSpace(fxDirectory) || !File.Exists(fxAbsolutePath))
            {
                return Array.Empty<string>();
            }

            var result = new List<string>();
            foreach (string candidatePath in ReadExplicitNormalMapPaths(fxDirectory, fxAbsolutePath))
            {
                if (!candidatePath.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(candidatePath))
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(projectRoot, candidatePath).Replace('\\', '/');
                if (!result.Exists(path => string.Equals(path, relativePath, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(relativePath);
                }
            }

            return result;
        }

        internal static bool IsExplicitNormalMapAsset(string projectAssetPath)
        {
            if (string.IsNullOrWhiteSpace(projectAssetPath))
            {
                return false;
            }

            string normalizedAssetPath = projectAssetPath.Replace('\\', '/');
            if (!normalizedAssetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string textureAbsolutePath = Path.GetFullPath(Path.Combine(
                projectRoot,
                normalizedAssetPath.Replace('/', Path.DirectorySeparatorChar)));
            string? textureDirectory = Path.GetDirectoryName(textureAbsolutePath);
            if (string.IsNullOrWhiteSpace(textureDirectory) || !Directory.Exists(textureDirectory))
            {
                return false;
            }

            string[] fxFiles;
            try
            {
                fxFiles = Directory.GetFiles(textureDirectory, "*.fx", SearchOption.TopDirectoryOnly);
                Array.Sort(fxFiles, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }

            for (int i = 0; i < fxFiles.Length; i++)
            {
                if (ReferencesNormalMap(textureDirectory, fxFiles[i], textureAbsolutePath))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ReferencesNormalMap(
            string fxDirectory,
            string fxPath,
            string textureAbsolutePath)
        {
            foreach (string candidatePath in ReadExplicitNormalMapPaths(fxDirectory, fxPath))
            {
                if (string.Equals(candidatePath, textureAbsolutePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> ReadExplicitNormalMapPaths(string fxDirectory, string fxPath)
        {
            string content;
            try
            {
                content = ReadTextBestEffort(fxPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                yield break;
            }

            foreach (Match match in ExplicitNormalMapDefineRegex.Matches(content))
            {
                string reference = match.Groups[1].Value.Trim();
                if (reference.Length == 0)
                {
                    continue;
                }

                string candidatePath;
                try
                {
                    candidatePath = Path.IsPathRooted(reference)
                        ? Path.GetFullPath(reference)
                        : Path.GetFullPath(Path.Combine(
                            fxDirectory,
                            reference.Replace('/', Path.DirectorySeparatorChar)));
                }
                catch (ArgumentException)
                {
                    continue;
                }

                yield return candidatePath;
            }
        }

        private static string ReadTextBestEffort(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            try
            {
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                    .GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(932).GetString(bytes);
            }
        }
    }
}
