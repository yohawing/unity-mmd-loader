#nullable enable

using System;
using System.IO;

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
    }
}