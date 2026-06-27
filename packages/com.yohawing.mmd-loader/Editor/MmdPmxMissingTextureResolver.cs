#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mmd.Editor
{
    internal static class MmdPmxMissingTextureResolver
    {
        internal static MmdPmxMissingTextureResolveResult ResolveFirstMissingTextureReference(
            MmdPmxAsset asset,
            string searchRoot)
        {
            if (asset == null)
            {
                return MmdPmxMissingTextureResolveResult.Failed("PMX asset is required.");
            }

            return ResolveFirstMissingTextureReference(
                AssetDatabase.GetAssetPath(asset),
                asset.MissingProjectTextureReferenceSample,
                searchRoot,
                reimportOwner: true);
        }

        internal static MmdPmxMissingTextureResolveResult ResolveFirstMissingTextureReference(
            string pmxAssetPath,
            string missingReferenceSample,
            string searchRoot,
            bool reimportOwner)
        {
            if (!TryParseMissingReferenceSample(missingReferenceSample, out string reference))
            {
                return MmdPmxMissingTextureResolveResult.Failed("Missing texture sample is empty or unsupported.");
            }

            if (!MmdAssetPathUtility.TryResolveProjectRelativeAssetPathCandidate(
                pmxAssetPath,
                reference,
                out string targetAssetPath))
            {
                return MmdPmxMissingTextureResolveResult.Failed("Missing texture reference cannot be resolved under the PMX asset directory.");
            }

            if (string.IsNullOrWhiteSpace(searchRoot) || !Directory.Exists(searchRoot))
            {
                return MmdPmxMissingTextureResolveResult.Failed("Search root does not exist.");
            }

            string fileName = Path.GetFileName(reference);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return MmdPmxMissingTextureResolveResult.Failed("Missing texture reference does not contain a file name.");
            }

            if (!TryFindTextureCandidate(searchRoot, reference, fileName, out string sourcePath, out string searchFailure))
            {
                return MmdPmxMissingTextureResolveResult.Failed(searchFailure);
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string targetFullPath = Path.GetFullPath(Path.Combine(projectRoot, targetAssetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath) ?? projectRoot);
            if (string.Equals(Path.GetFullPath(sourcePath), targetFullPath, StringComparison.OrdinalIgnoreCase) &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(targetAssetPath) == null)
            {
                return MmdPmxMissingTextureResolveResult.Failed("Matching file is already at the PMX texture target path but cannot be imported as a Unity Texture2D.");
            }

            bool targetExisted = File.Exists(targetFullPath);
            byte[]? previousTargetBytes = targetExisted ? File.ReadAllBytes(targetFullPath) : null;
            bool copied = false;
            if (!targetExisted || AssetDatabase.LoadAssetAtPath<Texture2D>(targetAssetPath) == null)
            {
                File.Copy(sourcePath, targetFullPath, overwrite: true);
                copied = true;
            }

            AssetDatabase.ImportAsset(targetAssetPath, ImportAssetOptions.ForceUpdate);
            Texture2D? importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(targetAssetPath);
            if (importedTexture == null)
            {
                if (copied && !targetExisted)
                {
                    AssetDatabase.DeleteAsset(targetAssetPath);
                }
                else if (copied && previousTargetBytes != null)
                {
                    File.WriteAllBytes(targetFullPath, previousTargetBytes);
                }

                return MmdPmxMissingTextureResolveResult.Failed("Copied file could not be imported as a Unity Texture2D.");
            }

            if (reimportOwner && !string.IsNullOrWhiteSpace(pmxAssetPath))
            {
                AssetDatabase.ImportAsset(pmxAssetPath, ImportAssetOptions.ForceUpdate);
            }

            return MmdPmxMissingTextureResolveResult.Resolved(reference, sourcePath!, targetAssetPath);
        }

        internal static bool TryParseMissingReferenceSample(string missingReferenceSample, out string reference)
        {
            reference = string.Empty;
            if (string.IsNullOrWhiteSpace(missingReferenceSample))
            {
                return false;
            }

            int separator = missingReferenceSample.IndexOf(':');
            if (separator < 0 || separator >= missingReferenceSample.Length - 1)
            {
                return false;
            }

            reference = missingReferenceSample.Substring(separator + 1).Trim();
            return !string.IsNullOrWhiteSpace(reference) && !Path.IsPathRooted(reference);
        }

        private static bool TryFindTextureCandidate(
            string searchRoot,
            string reference,
            string fileName,
            out string sourcePath,
            out string failure)
        {
            sourcePath = string.Empty;
            failure = string.Empty;
            string normalizedReference = reference.Replace('\\', '/').TrimStart('/');
            var suffixMatches = new List<string>();
            var pending = new System.Collections.Generic.Stack<string>();
            pending.Push(searchRoot);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                foreach (string path in EnumerateFilesSafely(current, fileName))
                {
                    string relative = Path.GetRelativePath(searchRoot, path).Replace('\\', '/');
                    if (MatchesReferenceSuffix(relative, normalizedReference))
                    {
                        suffixMatches.Add(path);
                    }
                }

                foreach (string directory in EnumerateDirectoriesSafely(current))
                {
                    pending.Push(directory);
                }
            }

            if (suffixMatches.Count == 1)
            {
                sourcePath = suffixMatches[0];
                return true;
            }

            if (suffixMatches.Count > 1)
            {
                failure = "Multiple matching texture files were found for the same PMX relative reference.";
                return false;
            }

            failure = "No matching texture file preserving the PMX relative reference was found under the search root.";
            return false;
        }

        private static bool MatchesReferenceSuffix(string relativePath, string normalizedReference)
        {
            return string.Equals(relativePath, normalizedReference, StringComparison.OrdinalIgnoreCase)
                || relativePath.EndsWith("/" + normalizedReference, StringComparison.OrdinalIgnoreCase);
        }

        private static string[] EnumerateFilesSafely(string directory, string fileName)
        {
            try
            {
                return Directory.GetFiles(directory, fileName, SearchOption.TopDirectoryOnly);
            }
            catch (IOException)
            {
                return Array.Empty<string>();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        private static string[] EnumerateDirectoriesSafely(string directory)
        {
            try
            {
                string[] directories = Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly);
                return Array.FindAll(directories, IsSearchableDirectory);
            }
            catch (IOException)
            {
                return Array.Empty<string>();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        private static bool IsSearchableDirectory(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    internal readonly struct MmdPmxMissingTextureResolveResult
    {
        private MmdPmxMissingTextureResolveResult(
            bool success,
            string message,
            string reference,
            string sourcePath,
            string targetAssetPath)
        {
            Success = success;
            Message = message;
            Reference = reference;
            SourcePath = sourcePath;
            TargetAssetPath = targetAssetPath;
        }

        public bool Success { get; }

        public string Message { get; }

        public string Reference { get; }

        public string SourcePath { get; }

        public string TargetAssetPath { get; }

        public static MmdPmxMissingTextureResolveResult Resolved(
            string reference,
            string sourcePath,
            string targetAssetPath)
        {
            return new MmdPmxMissingTextureResolveResult(
                true,
                "Resolved missing texture reference.",
                reference,
                sourcePath,
                targetAssetPath);
        }

        public static MmdPmxMissingTextureResolveResult Failed(string message)
        {
            return new MmdPmxMissingTextureResolveResult(
                false,
                message,
                string.Empty,
                string.Empty,
                string.Empty);
        }
    }
}
