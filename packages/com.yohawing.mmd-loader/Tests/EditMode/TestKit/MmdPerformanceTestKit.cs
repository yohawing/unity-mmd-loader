#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;

namespace Mmd.Tests
{
    internal static class MmdPerformanceTestKit
    {
        internal static double Measure(Action action, out long gcAllocatedBytes)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            Stopwatch stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            gcAllocatedBytes = Math.Max(0L, allocatedAfter - allocatedBefore);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        internal static string FormatMeasurement(
            string label,
            double[] samples,
            long[] gcAllocatedBytes,
            string prefix)
        {
            Assert.That(samples, Is.Not.Null.And.Not.Empty, label + " must have samples.");
            Assert.That(gcAllocatedBytes, Has.Length.EqualTo(samples.Length), label + " GC sample count.");
            Array.Sort(samples);
            Array.Sort(gcAllocatedBytes);
            return string.Format(
                "{0} {1}: samples={2}, p50={3:F2}ms, p95={4:F2}ms, max={5:F2}ms, " +
                "gcAllocatedBytes(p50={6}, p95={7}, max={8})",
                prefix,
                label,
                samples.Length,
                Percentile(samples, 0.50),
                Percentile(samples, 0.95),
                samples[samples.Length - 1],
                Percentile(gcAllocatedBytes, 0.50),
                Percentile(gcAllocatedBytes, 0.95),
                gcAllocatedBytes[gcAllocatedBytes.Length - 1]);
        }

        internal static double Percentile(double[] sortedSamples, double percentile)
        {
            int index = Math.Max(0, (int)Math.Ceiling(sortedSamples.Length * percentile) - 1);
            return sortedSamples[Math.Min(sortedSamples.Length - 1, index)];
        }

        internal static long Percentile(long[] sortedSamples, double percentile)
        {
            int index = Math.Max(0, (int)Math.Ceiling(sortedSamples.Length * percentile) - 1);
            return sortedSamples[Math.Min(sortedSamples.Length - 1, index)];
        }

        internal static string ReadPackageVersion()
        {
            return UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(MmdTestFixtures).Assembly)?.version ?? "<unavailable>";
        }

        internal static string ReadNativeDllSha256()
        {
            string nativeDllPath = Path.Combine(
                MmdTestFixtures.PackageRoot,
                "Runtime",
                "Plugins",
                "x86_64",
                "mmd_runtime_ffi.dll");
            if (!File.Exists(nativeDllPath))
            {
                return "<unavailable>";
            }

            using FileStream stream = File.OpenRead(nativeDllPath);
            using SHA256 sha256 = SHA256.Create();
            return ToSha256Hex(sha256.ComputeHash(stream));
        }

        internal static string Sha256(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            return ToSha256Hex(sha256.ComputeHash(bytes));
        }

        private static string ToSha256Hex(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
