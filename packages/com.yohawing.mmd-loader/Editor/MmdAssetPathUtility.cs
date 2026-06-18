#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.IO;
using UnityEngine;

namespace Mmd.Editor
{
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
            if (Path.IsPathRooted(relativeReference))
            {
                candidatePath = Path.GetFullPath(relativeReference);
            }
            else
            {
                string ownerDirectoryFullPath = Path.GetFullPath(Path.Combine(projectRoot, ownerDirectory));
                candidatePath = Path.GetFullPath(Path.Combine(ownerDirectoryFullPath, relativeReference));

                // Traversal guard: after GetFullPath normalization (resolves ..), ensure candidate
                // cannot escape the owner PMX asset directory. String prefix after normalization
                // is sufficient because .. that escapes will land outside the owner tree.
                string ownerDirWithSep = ownerDirectoryFullPath;
                char sep = Path.DirectorySeparatorChar;
                char alt = Path.AltDirectorySeparatorChar;
                if (!ownerDirWithSep.EndsWith(sep) && !ownerDirWithSep.EndsWith(alt))
                {
                    ownerDirWithSep += sep;
                }
                bool underOwner = candidatePath.StartsWith(ownerDirWithSep, System.StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(candidatePath, ownerDirectoryFullPath, System.StringComparison.OrdinalIgnoreCase);
                if (!underOwner)
                {
                    return false;
                }
            }

            if (!candidatePath.StartsWith(projectRoot, System.StringComparison.OrdinalIgnoreCase))
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
    }
}
