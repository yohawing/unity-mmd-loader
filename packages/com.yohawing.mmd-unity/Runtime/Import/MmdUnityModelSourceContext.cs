#nullable enable

using System;
using System.IO;

namespace Yohawing.MmdUnity.UnityIntegration
{
    public sealed class MmdUnityModelSourceContext
    {
        private MmdUnityModelSourceContext(string sourcePath, string sourceDirectory)
        {
            SourcePath = sourcePath;
            SourceDirectory = sourceDirectory;
        }

        public string SourcePath { get; }

        public string SourceDirectory { get; }

        public static MmdUnityModelSourceContext? FromOptionalPath(string? sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return null;
            }

            string fullPath = Path.GetFullPath(sourcePath);
            string? sourceDirectory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(sourceDirectory))
            {
                throw new ArgumentException("MMD model source path must include a directory.", nameof(sourcePath));
            }

            return new MmdUnityModelSourceContext(fullPath, sourceDirectory);
        }
    }
}
