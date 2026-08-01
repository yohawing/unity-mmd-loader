#nullable enable

using System;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using NUnit.Framework;
using Mmd.Native;

namespace Mmd.Tests
{
    public sealed class MmdVmdNativeClipPerformanceTests
    {
        private const int WarmupCount = 5;
        private const int MeasurementCount = 20;
        private const int GeneratedBoneKeyframeCount = 300_000;
        private const int GeneratedFrameSpan = 12_000;
        private const double P95BudgetMilliseconds = 100.0;

        [Test]
        public void BinarySummaryRejectsTruncatedVmdWithoutBuildingMotionDtos()
        {
            byte[] bytes = MmdTestFixtures.CreateDenseVmdBytes(
                "generated-vmd-summary",
                "全ての親",
                3,
                30);
            Array.Resize(ref bytes, bytes.Length - 1);

            Assert.Throws<InvalidDataException>(() => MmdVmdBinarySummaryReader.Read(bytes));
        }

        [Test]
        [Category("Performance")]
        public void GeneratedDenseVmdNativeClipBuildHasP95Under100Milliseconds()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("MMD_VMD_PERF_GATE"),
                    "1",
                    StringComparison.Ordinal))
            {
                Assert.Ignore("Set MMD_VMD_PERF_GATE=1 to run the native VMD clip performance gate.");
            }

#if !UNITY_EDITOR_WIN
            Assert.Ignore("The distributed mmd-runtime VMD clip gate is Windows Editor only.");
#endif
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            byte[] vmdBytes = MmdTestFixtures.CreateDenseVmdBytes(
                "generated-vmd-benchmark",
                "全ての親",
                GeneratedBoneKeyframeCount,
                GeneratedFrameSpan);

            MmdVmdParseSummary summary = MmdVmdBinarySummaryReader.Read(vmdBytes);
            Assert.That(summary.BoneKeyframeCount,
                Is.EqualTo(GeneratedBoneKeyframeCount));

            uint nativeAbiVersion = MmdRuntimeFfiMethods.ValidateAbiVersion();
            IntPtr model = MmdRuntimeFfiMethods.ModelCreateFromPmxBytes(
                pmxBytes,
                new IntPtr(pmxBytes.Length));
            Assert.That(model, Is.Not.EqualTo(IntPtr.Zero), "Native PMX model creation returned null.");
            try
            {
                Assert.That(MmdRuntimeFfiMethods.ModelBoneCount(model).ToInt64(), Is.EqualTo(1));
                double coldMilliseconds = MeasureClipCreate(model, vmdBytes, out long coldGcAllocatedBytes);

                var warmSamples = new double[WarmupCount];
                var warmGcAllocatedBytes = new long[WarmupCount];
                for (int i = 0; i < warmSamples.Length; i++)
                {
                    warmSamples[i] = MeasureClipCreate(model, vmdBytes, out warmGcAllocatedBytes[i]);
                }

                var steadySamples = new double[MeasurementCount];
                var steadyGcAllocatedBytes = new long[MeasurementCount];
                for (int i = 0; i < steadySamples.Length; i++)
                {
                    steadySamples[i] = MeasureClipCreate(model, vmdBytes, out steadyGcAllocatedBytes[i]);
                }

                string identity = string.Format(
                    "VMD native clip gate identity: package={0}@{1}, nativeLibrary={2}, nativeAbi={3}, expectedAbi={4}, nativeDllSha256={5}, " +
                    "pmxFixture={6}, pmxBytes={7}, pmxSha256={8}, vmdFixture={9}, keys={10}, vmdBytes={11}, vmdSha256={12}",
                    MmdRuntimeInfo.PackageName,
                    ReadPackageVersion(),
                    MmdRuntimeFfiMethods.LibraryName,
                    nativeAbiVersion,
                    MmdRuntimeFfiMethods.ExpectedAbiVersion,
                    ReadNativeDllSha256(),
                    "test_1bone_cube.pmx",
                    pmxBytes.Length,
                    Sha256(pmxBytes),
                    "generated-vmd-benchmark",
                    summary.BoneKeyframeCount,
                    vmdBytes.Length,
                    Sha256(vmdBytes));
                string coldMeasurement = FormatMeasurement(
                    "cold",
                    new[] { coldMilliseconds },
                    new[] { coldGcAllocatedBytes });
                string warmMeasurement = FormatMeasurement("warm", warmSamples, warmGcAllocatedBytes);
                string steadyMeasurement = FormatMeasurement("steady", steadySamples, steadyGcAllocatedBytes);
                TestContext.Progress.WriteLine(identity);
                TestContext.Progress.WriteLine(coldMeasurement);
                TestContext.Progress.WriteLine(warmMeasurement);
                TestContext.Progress.WriteLine(steadyMeasurement);
                UnityEngine.Debug.Log(identity + Environment.NewLine + coldMeasurement + Environment.NewLine +
                    warmMeasurement + Environment.NewLine + steadyMeasurement);

                Assert.That(
                    Percentile(steadySamples, 0.95),
                    Is.LessThanOrEqualTo(P95BudgetMilliseconds),
                    "Native VMD clip creation exceeded the 100ms p95 budget.");
            }
            finally
            {
                MmdRuntimeFfiMethods.ModelFree(model);
            }
        }

        private static double MeasureClipCreate(IntPtr model, byte[] vmdBytes, out long gcAllocatedBytes)
        {
            Stopwatch stopwatch = new Stopwatch();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Start();
            IntPtr clip = MmdRuntimeFfiMethods.ClipCreateFromVmdBytesForModel(
                model,
                vmdBytes,
                new IntPtr(vmdBytes.Length));
            stopwatch.Stop();
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                Assert.That(clip, Is.Not.EqualTo(IntPtr.Zero), "Native VMD clip creation returned null.");
            }
            finally
            {
                if (clip != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ClipFree(clip);
                }
            }

            gcAllocatedBytes = Math.Max(0L, allocatedAfter - allocatedBefore);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static string FormatMeasurement(string label, double[] samples, long[] gcAllocatedBytes)
        {
            Array.Sort(samples);
            Array.Sort(gcAllocatedBytes);
            return string.Format(
                "VMD native clip gate {0}: samples={1}, p50={2:F2}ms, p95={3:F2}ms, max={4:F2}ms, " +
                "gcAllocatedBytes(p50={5}, p95={6}, max={7})",
                label,
                samples.Length,
                Percentile(samples, 0.50),
                Percentile(samples, 0.95),
                samples[samples.Length - 1],
                Percentile(gcAllocatedBytes, 0.50),
                Percentile(gcAllocatedBytes, 0.95),
                gcAllocatedBytes[gcAllocatedBytes.Length - 1]);
        }

        private static double Percentile(double[] sortedSamples, double percentile)
        {
            int index = Math.Max(0, (int)Math.Ceiling(sortedSamples.Length * percentile) - 1);
            return sortedSamples[Math.Min(sortedSamples.Length - 1, index)];
        }

        private static long Percentile(long[] sortedSamples, double percentile)
        {
            int index = Math.Max(0, (int)Math.Ceiling(sortedSamples.Length * percentile) - 1);
            return sortedSamples[Math.Min(sortedSamples.Length - 1, index)];
        }

        private static string ReadPackageVersion()
        {
            string packageJsonPath = Path.Combine(MmdTestFixtures.PackageRoot, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                return "<unavailable>";
            }

            try
            {
                PackageMetadata? metadata = UnityEngine.JsonUtility.FromJson<PackageMetadata>(
                    File.ReadAllText(packageJsonPath));
                return metadata != null && !string.IsNullOrWhiteSpace(metadata.version)
                    ? metadata.version
                    : "<unavailable>";
            }
            catch (Exception)
            {
                return "<unavailable>";
            }
        }

        private static string ReadNativeDllSha256()
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

        private static string Sha256(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            return ToSha256Hex(sha256.ComputeHash(bytes));
        }

        private static string ToSha256Hex(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        [Serializable]
        private sealed class PackageMetadata
        {
            public string version = string.Empty;
        }
    }
}
