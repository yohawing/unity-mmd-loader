#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mmd.Editor
{
    /// <summary>
    /// Copies importer-owned PMX material sub-assets into independent project
    /// Material assets. This deliberately does not copy textures; a cloned
    /// Material retains the texture object references owned by the source.
    /// </summary>
    internal static class MmdPmxMaterialExtractor
    {
        public readonly struct Result
        {
            public Result(bool success, string message, IReadOnlyList<string> createdAssetPaths)
            {
                Success = success;
                Message = message;
                CreatedAssetPaths = createdAssetPaths;
            }

            public bool Success { get; }
            public string Message { get; }
            public IReadOnlyList<string> CreatedAssetPaths { get; }
        }

        /// <summary>
        /// Extracts to a sibling <c>Materials</c> folder beside the PMX asset.
        /// The folder is created only when needed and remains user-owned after
        /// extraction, including a no-op or failed extraction.
        /// </summary>
        public static Result TryExtractToSiblingMaterialsFolder(
            string importerAssetPath,
            Material[]? importedMaterials,
            Material[]? existingRemaps,
            out Material[] updatedRemaps)
        {
            updatedRemaps = Array.Empty<Material>();
            string normalizedImporterPath = (importerAssetPath ?? string.Empty).Trim().Replace('\\', '/');
            if (!normalizedImporterPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(Path.GetFileName(normalizedImporterPath)))
            {
                return Failure("The PMX importer asset path is invalid.");
            }

            string? parentFolder = Path.GetDirectoryName(normalizedImporterPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(parentFolder) || !AssetDatabase.IsValidFolder(parentFolder))
            {
                return Failure("The PMX importer parent folder is unavailable.");
            }

            string parent = parentFolder!;
            string materialsFolder = parent.TrimEnd('/') + "/Materials";
            if (!AssetDatabase.IsValidFolder(materialsFolder))
            {
                string folderGuid = AssetDatabase.CreateFolder(parent, "Materials");
                if (string.IsNullOrEmpty(folderGuid) || !AssetDatabase.IsValidFolder(materialsFolder))
                {
                    return Failure($"Could not create sibling Materials folder: {materialsFolder}");
                }
            }

            return TryExtract(
                importedMaterials,
                existingRemaps,
                materialsFolder,
                out updatedRemaps);
        }

        /// <summary>
        /// Extracts every still-embedded/unmapped slot and returns the complete
        /// slot-aligned remap array. Existing non-null entries are never changed.
        /// No serialized importer state is modified until the caller commits the
        /// returned array, so a failed extraction is remap-transaction safe.
        /// </summary>
        public static Result TryExtract(
            Material[]? importedMaterials,
            Material[]? existingRemaps,
            string destinationFolderAssetPath,
            out Material[] updatedRemaps)
        {
            updatedRemaps = Array.Empty<Material>();
            if (importedMaterials == null)
            {
                return Failure("Imported PMX materials are unavailable.");
            }

            if (!TryNormalizeDestinationFolder(destinationFolderAssetPath, out string folder, out string folderError))
            {
                return Failure(folderError);
            }

            int materialCount = importedMaterials.Length;
            Material[] remaps = new Material[materialCount];
            if (existingRemaps != null)
            {
                int copyCount = Math.Min(existingRemaps.Length, materialCount);
                for (int i = 0; i < copyCount; i++)
                {
                    remaps[i] = existingRemaps[i];
                }
            }

            List<string> createdPaths = new List<string>();
            try
            {
                for (int slot = 0; slot < materialCount; slot++)
                {
                    // Partial extraction is intentional: an existing remap owns
                    // this slot and must not be copied or replaced.
                    if (remaps[slot] != null)
                    {
                        continue;
                    }

                    Material source = importedMaterials[slot];
                    if (source == null)
                    {
                        throw new InvalidOperationException(
                            $"PMX material slot {slot.ToString(System.Globalization.CultureInfo.InvariantCulture)} is null.");
                    }

                    string path = AllocateAssetPath(folder, source.name, createdPaths);
                    Material extracted = new Material(source)
                    {
                        // Keep the PMX material name in the asset. The filename
                        // is the deterministic collision-safe identity.
                        name = source.name
                    };
                    try
                    {
                        AssetDatabase.CreateAsset(extracted, path);
                        if (!AssetDatabase.Contains(extracted))
                        {
                            throw new InvalidOperationException(
                                $"Material asset was not registered at {path}.");
                        }
                    }
                    catch
                    {
                        // CreateAsset can fail before registering the clone.
                        // Destroy only an unregistered object; registered assets
                        // remain available for the outer transaction rollback.
                        if (!AssetDatabase.Contains(extracted))
                        {
                            UnityEngine.Object.DestroyImmediate(extracted);
                        }

                        throw;
                    }

                    createdPaths.Add(path);
                    remaps[slot] = extracted;
                }

                if (createdPaths.Count > 0)
                {
                    AssetDatabase.SaveAssets();
                }

                updatedRemaps = remaps;
                string message = createdPaths.Count == 0
                    ? "All PMX material slots already have remaps."
                    : $"Extracted {createdPaths.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} material asset(s).";
                return new Result(true, message, createdPaths.ToArray());
            }
            catch (Exception exception)
            {
                // Roll back only assets created by this invocation. Existing
                // project assets and remap entries are untouched.
                for (int i = createdPaths.Count - 1; i >= 0; i--)
                {
                    AssetDatabase.DeleteAsset(createdPaths[i]);
                }

                AssetDatabase.Refresh();
                updatedRemaps = Array.Empty<Material>();
                return Failure($"Material extraction failed: {exception.Message}");
            }
        }

        private static Result Failure(string message)
        {
            return new Result(false, message, Array.Empty<string>());
        }

        private static bool TryNormalizeDestinationFolder(
            string destinationFolderAssetPath,
            out string normalized,
            out string error)
        {
            normalized = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(destinationFolderAssetPath))
            {
                error = "Choose a project folder under Assets.";
                return false;
            }

            string candidate = destinationFolderAssetPath.Trim().Replace('\\', '/');
            if (Path.IsPathRooted(candidate))
            {
                candidate = FileUtil.GetProjectRelativePath(candidate).Replace('\\', '/');
            }

            if (!candidate.Equals("Assets", StringComparison.Ordinal) &&
                !candidate.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = "Material extraction is limited to a project folder under Assets.";
                return false;
            }

            if (!AssetDatabase.IsValidFolder(candidate))
            {
                error = $"The selected folder does not exist in the project: {candidate}";
                return false;
            }

            normalized = candidate.TrimEnd('/');
            return true;
        }

        private static string AllocateAssetPath(
            string folder,
            string? sourceName,
            IReadOnlyCollection<string> createdPaths)
        {
            string safeName = SanitizeFileName(sourceName);
            string candidate = folder + "/" + safeName + ".mat";
            int suffix = 1;
            while (ContainsPath(createdPaths, candidate) || AssetPathExists(candidate))
            {
                candidate = folder + "/" + safeName + "_" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".mat";
                suffix++;
            }

            return candidate;
        }

        private static bool ContainsPath(IReadOnlyCollection<string> paths, string candidate)
        {
            foreach (string path in paths)
            {
                if (string.Equals(path, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AssetPathExists(string assetPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                return true;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return false;
            }

            string fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(fullPath) || Directory.Exists(fullPath);
        }

        private static string SanitizeFileName(string? sourceName)
        {
            string value = sourceName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                value = "Material";
            }
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char current = chars[i];
                if (current == '/' || current == '\\' || Array.IndexOf(invalid, current) >= 0)
                {
                    chars[i] = '_';
                }
            }

            value = new string(chars).Trim().TrimEnd('.');
            if (string.IsNullOrEmpty(value))
            {
                return "Material";
            }

            return IsReservedWindowsDeviceName(value) ? "_" + value : value;
        }

        private static bool IsReservedWindowsDeviceName(string value)
        {
            int dotIndex = value.IndexOf('.');
            string basename = (dotIndex >= 0 ? value.Substring(0, dotIndex) : value)
                .TrimEnd(' ', '.');
            if (basename.Length == 0)
            {
                return false;
            }

            string upper = basename.ToUpperInvariant();
            if (upper == "CON" || upper == "PRN" || upper == "AUX" || upper == "NUL")
            {
                return true;
            }

            if (upper.Length != 4 ||
                (upper.StartsWith("COM", StringComparison.Ordinal) &&
                 (upper[3] < '1' || upper[3] > '9')) ||
                (upper.StartsWith("LPT", StringComparison.Ordinal) &&
                 (upper[3] < '1' || upper[3] > '9')))
            {
                return false;
            }

            return upper.StartsWith("COM", StringComparison.Ordinal) ||
                upper.StartsWith("LPT", StringComparison.Ordinal);
        }
    }
}
