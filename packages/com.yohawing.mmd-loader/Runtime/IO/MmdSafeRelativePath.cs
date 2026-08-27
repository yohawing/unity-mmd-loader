#nullable enable

using System;
using System.IO;
using System.Security;

namespace Mmd.IO
{
    internal enum MmdSafeRelativePathFailure
    {
        None,
        Invalid,
        OutsideRoot,
        ReparsePoint
    }

    internal static class MmdSafeRelativePath
    {
        internal static bool TryResolve(
            string rootDirectory,
            string relativeReference,
            out string resolvedPath,
            out MmdSafeRelativePathFailure failure,
            bool allowRoot = false)
        {
            resolvedPath = string.Empty;
            failure = MmdSafeRelativePathFailure.None;

            if (string.IsNullOrWhiteSpace(rootDirectory) || relativeReference == null)
            {
                failure = MmdSafeRelativePathFailure.Invalid;
                return false;
            }

            string fullRoot;
            try
            {
                fullRoot = Path.GetFullPath(rootDirectory);
                resolvedPath = Path.GetFullPath(Path.Combine(
                    fullRoot,
                    relativeReference
                        .Replace('\\', Path.DirectorySeparatorChar)
                        .Replace('/', Path.DirectorySeparatorChar)));
            }
            catch (Exception ex) when (IsPathFailure(ex))
            {
                resolvedPath = string.Empty;
                failure = MmdSafeRelativePathFailure.Invalid;
                return false;
            }

            if (!IsUnderRoot(fullRoot, resolvedPath, allowRoot))
            {
                resolvedPath = string.Empty;
                failure = MmdSafeRelativePathFailure.OutsideRoot;
                return false;
            }

            if (ContainsExistingReparsePoint(fullRoot, resolvedPath))
            {
                resolvedPath = string.Empty;
                failure = MmdSafeRelativePathFailure.ReparsePoint;
                return false;
            }

            return true;
        }

        private static bool IsUnderRoot(string rootDirectory, string candidatePath, bool allowRoot)
        {
            string normalizedRoot = rootDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            string normalizedCandidate = candidatePath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (allowRoot && string.Equals(normalizedCandidate, normalizedRoot, comparison))
            {
                return true;
            }

            string rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;
            return normalizedCandidate.StartsWith(rootPrefix, comparison);
        }

        private static bool ContainsExistingReparsePoint(string rootDirectory, string candidatePath)
        {
            string relativePath;
            try
            {
                relativePath = Path.GetRelativePath(rootDirectory, candidatePath);
            }
            catch (Exception ex) when (IsPathFailure(ex))
            {
                return true;
            }

            string current = rootDirectory;
            foreach (string component in relativePath.Split(
                         new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                if (component == ".")
                {
                    continue;
                }

                try
                {
                    current = Path.Combine(current, component);
                    FileAttributes attributes = File.GetAttributes(current);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return true;
                    }
                }
                catch (FileNotFoundException)
                {
                    // A missing leaf is allowed; the caller classifies it as missing.
                    return false;
                }
                catch (DirectoryNotFoundException)
                {
                    // A missing leaf is allowed; the caller classifies it as missing.
                    return false;
                }
                catch (Exception ex) when (IsPathFailure(ex))
                {
                    // Existing-but-unreadable path components fail closed.
                    return true;
                }
            }

            return false;
        }

        private static bool IsPathFailure(Exception exception)
        {
            return exception is ArgumentException
                || exception is IOException
                || exception is NotSupportedException
                || exception is SecurityException
                || exception is UnauthorizedAccessException;
        }
    }
}
