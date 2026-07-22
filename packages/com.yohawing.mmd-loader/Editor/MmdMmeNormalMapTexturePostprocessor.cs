#nullable enable

using System;
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

        public void OnPreprocessTexture()
        {
            if (assetImporter is not TextureImporter importer ||
                !IsExplicitNormalMapAsset(assetPath))
            {
                return;
            }

            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
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
            string content;
            try
            {
                content = ReadTextBestEffort(fxPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }

            MatchCollection matches = ExplicitNormalMapDefineRegex.Matches(content);
            foreach (Match match in matches)
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

                if (string.Equals(candidatePath, textureAbsolutePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
