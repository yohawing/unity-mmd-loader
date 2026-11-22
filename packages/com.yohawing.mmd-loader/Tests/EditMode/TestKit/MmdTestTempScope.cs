#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mmd.Tests
{
    internal sealed class MmdTestTempScope : IDisposable
    {
        public string Path { get; }

        public MmdTestTempScope()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "yohawing-mmd-unity-tests",
                System.IO.Path.GetRandomFileName());
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                try
                {
                    Directory.Delete(Path, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup in tests
                }
            }
        }

        /// <summary>
        /// Deletes a project folder that a test created under <c>Assets/</c>: removes it from the
        /// AssetDatabase, then deletes whatever is left on disk (clearing read-only attributes)
        /// and its .meta file. Unlike <see cref="Dispose"/>, failures propagate so a leaked asset
        /// folder fails the test instead of polluting the next one.
        /// </summary>
        internal static void DeleteAssetFolder(string projectRelativeAssetPath, bool refreshAssetDatabase = true)
        {
            AssetDatabase.DeleteAsset(projectRelativeAssetPath);
            string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
            string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                projectRoot,
                projectRelativeAssetPath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
            if (Directory.Exists(fullPath))
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(fullPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(entry, FileAttributes.Normal);
                    }
                    catch (Exception)
                    {
                        // Ignore files that disappear while deleting.
                    }
                }

                Directory.Delete(fullPath, recursive: true);
            }

            string metaPath = fullPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.SetAttributes(metaPath, FileAttributes.Normal);
                File.Delete(metaPath);
            }

            if (refreshAssetDatabase)
            {
                AssetDatabase.Refresh();
            }
        }
    }
}
