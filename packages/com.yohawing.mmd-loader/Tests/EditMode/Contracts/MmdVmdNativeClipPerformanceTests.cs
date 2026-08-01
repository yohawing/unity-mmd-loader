#nullable enable

using System;
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
                    MmdPerformanceTestKit.ReadPackageVersion(),
                    MmdRuntimeFfiMethods.LibraryName,
                    nativeAbiVersion,
                    MmdRuntimeFfiMethods.ExpectedAbiVersion,
                    MmdPerformanceTestKit.ReadNativeDllSha256(),
                    "test_1bone_cube.pmx",
                    pmxBytes.Length,
                    MmdPerformanceTestKit.Sha256(pmxBytes),
                    "generated-vmd-benchmark",
                    summary.BoneKeyframeCount,
                    vmdBytes.Length,
                    MmdPerformanceTestKit.Sha256(vmdBytes));
                string coldMeasurement = MmdPerformanceTestKit.FormatMeasurement(
                    "cold",
                    new[] { coldMilliseconds },
                    new[] { coldGcAllocatedBytes },
                    "VMD native clip gate");
                string warmMeasurement = MmdPerformanceTestKit.FormatMeasurement(
                    "warm", warmSamples, warmGcAllocatedBytes, "VMD native clip gate");
                string steadyMeasurement = MmdPerformanceTestKit.FormatMeasurement(
                    "steady", steadySamples, steadyGcAllocatedBytes, "VMD native clip gate");
                TestContext.Progress.WriteLine(identity);
                TestContext.Progress.WriteLine(coldMeasurement);
                TestContext.Progress.WriteLine(warmMeasurement);
                TestContext.Progress.WriteLine(steadyMeasurement);
                UnityEngine.Debug.Log(identity + Environment.NewLine + coldMeasurement + Environment.NewLine +
                    warmMeasurement + Environment.NewLine + steadyMeasurement);

                Assert.That(
                    MmdPerformanceTestKit.Percentile(steadySamples, 0.95),
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
            IntPtr clip = IntPtr.Zero;
            try
            {
                double elapsed = MmdPerformanceTestKit.Measure(
                    () => clip = MmdRuntimeFfiMethods.ClipCreateFromVmdBytesForModel(
                        model,
                        vmdBytes,
                        new IntPtr(vmdBytes.Length)),
                    out gcAllocatedBytes);
                Assert.That(clip, Is.Not.EqualTo(IntPtr.Zero), "Native VMD clip creation returned null.");
                return elapsed;
            }
            finally
            {
                if (clip != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ClipFree(clip);
                }
            }
        }

    }
}
