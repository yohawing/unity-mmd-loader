#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.IO;
using Mmd.IO;
using UnityEngine;

namespace Mmd.Editor
{
    internal enum MmdOutputPathError
    {
        None,
        Empty,
        Rooted,
        NotUnderAssets,
        WrongExtension,
        EmptyOrDotSegment,
        EscapesAssets
    }

    internal static class MmdAssetPathUtility
    {
        public static string ResolveAssetSourcePath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(assetPath))
            {
                return Path.GetFullPath(assetPath);
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string candidate = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            return File.Exists(candidate) ? candidate : string.Empty;
        }

        public static bool TryResolveProjectRelativeAssetPath(
            string ownerAssetPath,
            string relativeReference,
            [NotNullWhen(true)] out string resolvedAssetPath)
        {
            return TryResolveProjectRelativeAssetPath(
                ownerAssetPath,
                relativeReference,
                requireExistingFile: true,
                out resolvedAssetPath);
        }

        public static bool TryResolveProjectRelativeAssetPathCandidate(
            string ownerAssetPath,
            string relativeReference,
            [NotNullWhen(true)] out string resolvedAssetPath)
        {
            return TryResolveProjectRelativeAssetPath(
                ownerAssetPath,
                relativeReference,
                requireExistingFile: false,
                out resolvedAssetPath);
        }

        internal static string GetDefaultAnimationOutputPath(
            string? pmxSourceId,
            string? vmdSourceId)
        {
            return "Assets/" + GetSourceStem(pmxSourceId, "PMX") + "_"
                   + GetSourceStem(vmdSourceId, "VMD") + ".anim";
        }

        internal static string NormalizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "asset";
            }

            return value.Replace('/', '_')
                .Replace('\\', '_')
                .Replace(':', '_')
                .Replace('.', '_');
        }

        internal static bool TryValidateProjectRelativeOutputPath(
            string outputPath,
            string requiredExtension,
            out string normalizedOutputPath,
            out MmdOutputPathError error)
        {
            normalizedOutputPath = string.Empty;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                error = MmdOutputPathError.Empty;
                return false;
            }

            if (IsPathRootedAcrossPlatforms(outputPath))
            {
                error = MmdOutputPathError.Rooted;
                return false;
            }

            string normalized = outputPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", System.StringComparison.Ordinal))
            {
                error = MmdOutputPathError.NotUnderAssets;
                return false;
            }

            if (!normalized.EndsWith(requiredExtension, System.StringComparison.OrdinalIgnoreCase))
            {
                error = MmdOutputPathError.WrongExtension;
                return false;
            }

            foreach (string segment in normalized.Split('/'))
            {
                if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..")
                {
                    error = MmdOutputPathError.EmptyOrDotSegment;
                    return false;
                }
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string assetsRoot = Path.GetFullPath(Application.dataPath);
            string assetsRootWithSeparator = assetsRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullOutputPath = Path.GetFullPath(Path.Combine(projectRoot, normalized));
            if (!fullOutputPath.StartsWith(assetsRootWithSeparator, System.StringComparison.OrdinalIgnoreCase))
            {
                error = MmdOutputPathError.EscapesAssets;
                return false;
            }

            normalizedOutputPath = normalized;
            error = MmdOutputPathError.None;
            return true;
        }

        private static bool IsPathRootedAcrossPlatforms(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return true;
            }

            // Unity asset paths are exchanged as strings, so a Windows-authored rooted path
            // must remain rooted even when the package tests run on a POSIX host.
            if (path.Length > 0 && (path[0] == '\\' || path[0] == '/'))
            {
                return true;
            }

            return path.Length >= 2 &&
                path[1] == ':' &&
                ((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z'));
        }

        private static bool TryResolveProjectRelativeAssetPath(
            string ownerAssetPath,
            string relativeReference,
            bool requireExistingFile,
            [NotNullWhen(true)] out string resolvedAssetPath)
        {
            resolvedAssetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(ownerAssetPath) || string.IsNullOrWhiteSpace(relativeReference))
            {
                return false;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string ownerDirectory = Path.GetDirectoryName(ownerAssetPath)?.Replace('\\', '/') ?? string.Empty;
            string candidatePath;
            bool validatedAgainstProjectRoot = false;
            if (Path.IsPathRooted(relativeReference))
            {
                if (!MmdSafeRelativePath.TryResolve(
                        projectRoot,
                        relativeReference,
                        out candidatePath,
                        out _,
                        allowRoot: true))
                {
                    return false;
                }
                validatedAgainstProjectRoot = true;
            }
            else
            {
                string ownerDirectoryFullPath = Path.GetFullPath(Path.Combine(projectRoot, ownerDirectory));
                if (!MmdSafeRelativePath.TryResolve(
                        ownerDirectoryFullPath,
                        relativeReference,
                        out candidatePath,
                        out _,
                        allowRoot: true))
                {
                    return false;
                }
            }

            if (!validatedAgainstProjectRoot && !MmdSafeRelativePath.TryResolve(
                    projectRoot,
                    candidatePath,
                    out _,
                    out _,
                    allowRoot: true))
            {
                return false;
            }

            if (requireExistingFile && !File.Exists(candidatePath))
            {
                return false;
            }

            resolvedAssetPath = Path.GetRelativePath(projectRoot, candidatePath).Replace('\\', '/');
            return resolvedAssetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSourceStem(string? sourceId, string fallback)
        {
            string fileName = string.IsNullOrWhiteSpace(sourceId)
                ? fallback
                : Path.GetFileNameWithoutExtension(sourceId!.Replace('\\', '/')) ?? fallback;
            return NormalizeIdentifier(fileName);
        }
    }
}
