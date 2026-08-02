#nullable enable

using System;
using System.IO;
#if UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd.Editor;
using Mmd.Editor.Timeline;
using Mmd.Native;
using Mmd.Timeline;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed class MmdVmdImportTimelinePerformanceTests
    {
        private const string GateEnvironmentVariable = "MMD_VMD_PERF_GATE";
        private const string NativeHandleLifetimeStressGateEnvironmentVariable =
            "MMD_NATIVE_HANDLE_LIFETIME_STRESS_GATE";
        private const string TempDirectory = "Assets/__MmdVmdImportTimelinePerformanceTests";
        private const string TempPmxPath = TempDirectory + "/test_1bone_cube.pmx";
        private const string TempVmdPath = TempDirectory + "/generated-vmd-timeline.vmd";
        private const int MinimumMeasurementCount = 20;
        private const int DefaultMeasurementCount = MinimumMeasurementCount;
        private const int DefaultGeneratedBoneKeyframeCount = 300_000;
        private const int GeneratedFrameSpan = 12_000;
        private const double P95BudgetMilliseconds = 100.0;
        private const int NativeHandleLifetimeStressIterationCount = 8;
        private const int StressGeneratedBoneKeyframeCount = 16;
        private const int StressGeneratedFrameSpan = 24;
        private const int RetainedHandleAllowance = 4;
        private const long RetainedManagedMemoryAllowanceBytes = 4L * 1024L * 1024L;

        [TearDown]
        public void TearDown()
        {
            CleanupTemporaryAssets();
        }

        private static void CleanupTemporaryAssets()
        {
            AssetDatabase.DeleteAsset(TempDirectory);
            AssetDatabase.Refresh();
        }

        [Test]
        [Category("Performance")]
        public void GeneratedVmdImportTimelineFirstEvaluateHasP95Under100Milliseconds()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable(GateEnvironmentVariable),
                    "1",
                    StringComparison.Ordinal))
            {
                Assert.Ignore("Set MMD_VMD_PERF_GATE=1 to run the VMD import/Timeline performance gate.");
            }

#if !UNITY_EDITOR_WIN
            Assert.Ignore("The distributed VMD import/Timeline performance gate is Windows Editor only.");
#endif
            int measurementCount = ReadPositiveIntEnvironmentVariable(
                "MMD_VMD_TIMELINE_PERF_ITERATIONS",
                DefaultMeasurementCount);
            Assert.That(
                measurementCount,
                Is.GreaterThanOrEqualTo(MinimumMeasurementCount),
                "MMD_VMD_TIMELINE_PERF_ITERATIONS must provide at least 20 samples for a meaningful p95.");
            int generatedKeyframeCount = ReadPositiveIntEnvironmentVariable(
                "MMD_VMD_TIMELINE_PERF_KEYFRAMES",
                DefaultGeneratedBoneKeyframeCount);

            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            byte[] vmdBytes = MmdTestFixtures.CreateDenseVmdBytes(
                "generated-vmd-timeline",
                "全ての親",
                generatedKeyframeCount,
                GeneratedFrameSpan);
            MmdVmdParseSummary summary = MmdVmdNativeSummaryAdapter.Read(vmdBytes);
            Assert.That(summary.BoneKeyframeCount, Is.EqualTo(generatedKeyframeCount));

            PrepareTemporaryAssets(vmdBytes);

            double coldImportMilliseconds = MmdPerformanceTestKit.Measure(
                () => AssetDatabase.ImportAsset(TempVmdPath, ImportAssetOptions.ForceUpdate),
                out long coldImportGcAllocatedBytes);
            MmdVmdAsset vmdAsset = LoadImportedVmdAsset();
            string assetGuid = AssetDatabase.AssetPathToGUID(TempVmdPath);
            Assert.That(assetGuid, Is.Not.Null.And.Not.Empty, "The imported VMD must have a stable AssetDatabase GUID.");
            Assert.That(vmdAsset.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.Passed));
            Assert.That(vmdAsset.ByteLength, Is.EqualTo(vmdBytes.Length));
            Assert.That(vmdAsset.BoneKeyframeCount, Is.EqualTo(summary.BoneKeyframeCount));
            Assert.That(vmdAsset.MaxFrame, Is.EqualTo(summary.MaxFrame));
            Assert.That(
                MmdPerformanceTestKit.Sha256(vmdAsset.GetBytesCopy()),
                Is.EqualTo(MmdPerformanceTestKit.Sha256(vmdBytes)));

            var warmImportSamples = new double[measurementCount];
            var warmImportGcAllocatedBytes = new long[measurementCount];
            for (int i = 0; i < warmImportSamples.Length; i++)
            {
                warmImportSamples[i] = MmdPerformanceTestKit.Measure(
                    () => AssetDatabase.ImportAsset(TempVmdPath, ImportAssetOptions.ForceUpdate),
                    out warmImportGcAllocatedBytes[i]);
            }

            vmdAsset = LoadImportedVmdAsset();
            Assert.That(AssetDatabase.AssetPathToGUID(TempVmdPath), Is.EqualTo(assetGuid));

            var firstEvaluateSamples = new double[measurementCount];
            var firstEvaluateGcAllocatedBytes = new long[measurementCount];
            MmdPmxAsset pmxAsset = LoadImportedPmxAsset();
            for (int i = 0; i < firstEvaluateSamples.Length; i++)
            {
                using TimelineEvaluationFixture evaluation = CreateTimelineEvaluationFixture(pmxAsset, vmdAsset);
                evaluation.Director.time = 0.0;
                firstEvaluateSamples[i] = MmdPerformanceTestKit.Measure(
                    evaluation.Director.Evaluate,
                    out firstEvaluateGcAllocatedBytes[i]);
                Assert.That(evaluation.Controller.IsConfigured, Is.True);
                Assert.That(evaluation.Controller.LastSnapshot, Is.Not.Null);
            }

            var steadyGraphRebuildSamples = new double[measurementCount];
            var steadyGraphRebuildGcAllocatedBytes = new long[measurementCount];
            var steadyEvaluateSamples = new double[measurementCount];
            var steadyEvaluateGcAllocatedBytes = new long[measurementCount];
            using (TimelineEvaluationFixture steady = CreateTimelineEvaluationFixture(pmxAsset, vmdAsset))
            {
                steady.Director.time = 0.0;
                steady.Director.Evaluate();
                Assert.That(steady.Controller.IsConfigured, Is.True);
                Assert.That(steady.Controller.LastSnapshot, Is.Not.Null);

                for (int i = 0; i < measurementCount; i++)
                {
                    steadyGraphRebuildSamples[i] = MmdPerformanceTestKit.Measure(
                        steady.Director.RebuildGraph,
                        out steadyGraphRebuildGcAllocatedBytes[i]);
                    steadyEvaluateSamples[i] = MmdPerformanceTestKit.Measure(
                        steady.Director.Evaluate,
                        out steadyEvaluateGcAllocatedBytes[i]);
                }
            }

            uint nativeAbiVersion = MmdRuntimeFfiMethods.ValidateAbiVersion();
            string vmdSha256 = MmdPerformanceTestKit.Sha256(vmdBytes);
            string serializedVmdSha256 = MmdPerformanceTestKit.Sha256(vmdAsset.GetBytesCopy());
            string identity = string.Format(
                "VMD import Timeline gate identity: package={0}@{1}, unity={2}, nativeLibrary={3}, nativeAbi={4}, " +
                "expectedAbi={5}, nativeDllSha256={6}, pmxFixture={7}, pmxBytes={8}, pmxSha256={9}, " +
                "vmdFixture={10}, keys={11}, frameSpan={12}, maxFrame={13}, vmdBytes={14}, vmdSha256={15}, " +
                "serializedBytes={16}, serializedVmdSha256={17}, assetPath={18}, assetGuid={19}, " +
                "assetSourceId={20}, assetSourcePath={21}",
                MmdRuntimeInfo.PackageName,
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(MmdVmdImportTimelinePerformanceTests).Assembly)?.version ?? "<unavailable>",
                Application.unityVersion,
                MmdRuntimeFfiMethods.LibraryName,
                nativeAbiVersion,
                MmdRuntimeFfiMethods.ExpectedAbiVersion,
                MmdPerformanceTestKit.ReadNativeDllSha256(),
                "test_1bone_cube.pmx",
                pmxBytes.Length,
                MmdPerformanceTestKit.Sha256(pmxBytes),
                "generated-vmd-timeline",
                summary.BoneKeyframeCount,
                GeneratedFrameSpan,
                summary.MaxFrame,
                vmdBytes.Length,
                vmdSha256,
                vmdAsset.ByteLength,
                serializedVmdSha256,
                TempVmdPath,
                assetGuid,
                vmdAsset.SourceId,
                vmdAsset.SourcePath);
            string coldImportMeasurement = MmdPerformanceTestKit.FormatMeasurement(
                "cold-AssetDatabase-import",
                new[] { coldImportMilliseconds },
                new[] { coldImportGcAllocatedBytes },
                "VMD import Timeline gate");
            string warmImportMeasurement = MmdPerformanceTestKit.FormatMeasurement(
                "warm-AssetDatabase-reimport", warmImportSamples, warmImportGcAllocatedBytes,
                "VMD import Timeline gate");
            string firstEvaluateMeasurement = MmdPerformanceTestKit.FormatMeasurement(
                "first-Timeline-Evaluate", firstEvaluateSamples, firstEvaluateGcAllocatedBytes,
                "VMD import Timeline gate");
            string steadyGraphMeasurement = MmdPerformanceTestKit.FormatMeasurement(
                "steady-Timeline-graph-rebuild", steadyGraphRebuildSamples, steadyGraphRebuildGcAllocatedBytes,
                "VMD import Timeline gate");
            string steadyEvaluateMeasurement = MmdPerformanceTestKit.FormatMeasurement(
                "steady-Timeline-Evaluate", steadyEvaluateSamples, steadyEvaluateGcAllocatedBytes,
                "VMD import Timeline gate");

            TestContext.Progress.WriteLine(identity);
            TestContext.Progress.WriteLine(coldImportMeasurement);
            TestContext.Progress.WriteLine(warmImportMeasurement);
            TestContext.Progress.WriteLine(firstEvaluateMeasurement);
            TestContext.Progress.WriteLine(steadyGraphMeasurement);
            TestContext.Progress.WriteLine(steadyEvaluateMeasurement);
            UnityEngine.Debug.Log(identity + Environment.NewLine + coldImportMeasurement + Environment.NewLine +
                warmImportMeasurement + Environment.NewLine + firstEvaluateMeasurement + Environment.NewLine +
                steadyGraphMeasurement + Environment.NewLine + steadyEvaluateMeasurement);

            Assert.That(
                MmdPerformanceTestKit.Percentile(warmImportSamples, 0.95),
                Is.LessThanOrEqualTo(P95BudgetMilliseconds),
                "Warm VMD AssetDatabase reimport exceeded the 100ms p95 budget.");
            Assert.That(
                MmdPerformanceTestKit.Percentile(firstEvaluateSamples, 0.95),
                Is.LessThanOrEqualTo(P95BudgetMilliseconds),
                "First Timeline Evaluate exceeded the 100ms p95 budget.");
            Assert.That(
                MmdPerformanceTestKit.Percentile(steadyGraphRebuildSamples, 0.95),
                Is.LessThanOrEqualTo(P95BudgetMilliseconds),
                "Steady Timeline graph rebuild exceeded the 100ms p95 budget.");
            Assert.That(
                MmdPerformanceTestKit.Percentile(steadyEvaluateSamples, 0.95),
                Is.LessThanOrEqualTo(P95BudgetMilliseconds),
                "Steady Timeline Evaluate exceeded the 100ms p95 budget.");
        }

        [Test]
        [Category("Stress")]
        public void GeneratedVmdImportTimelineHandleLifetimeStressStaysWithinCleanupAllowance()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable(NativeHandleLifetimeStressGateEnvironmentVariable),
                    "1",
                    StringComparison.Ordinal))
            {
                Assert.Ignore(
                    "Set " + NativeHandleLifetimeStressGateEnvironmentVariable +
                    "=1 to run the VMD import/Timeline native handle lifetime stress gate.");
            }

#if !UNITY_EDITOR_WIN
            Assert.Fail("The VMD import/Timeline native handle lifetime stress gate is Windows Editor only.");
#else
            try
            {
                _ = MmdRuntimeNativeBoundary.Invoke(
                    "VMD import/Timeline native handle lifetime stress",
                    MmdRuntimeFfiMethods.ValidateAbiVersion);
            }
            catch (MmdRuntimeNativeUnavailableException exception)
            {
                Assert.Fail("Packaged native runtime is unavailable: " + exception.Message);
            }
            catch (MmdRuntimeUnsupportedException exception)
            {
                Assert.Fail("Packaged native runtime ABI is unsupported: " + exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                Assert.Fail("Packaged native runtime ABI is unsupported: " + exception.Message);
            }

            byte[] vmdBytes = MmdTestFixtures.CreateDenseVmdBytes(
                "generated-vmd-handle-lifetime-stress",
                "全ての親",
                StressGeneratedBoneKeyframeCount,
                StressGeneratedFrameSpan);
            MmdVmdParseSummary summary = MmdVmdNativeSummaryAdapter.Read(vmdBytes);
            Assert.That(summary.BoneKeyframeCount, Is.EqualTo(StressGeneratedBoneKeyframeCount));

            NativeHandleLifetimeStressReport report = RunNativeHandleLifetimeStress(vmdBytes);
            TestContext.Progress.WriteLine(
                "VMD import/Timeline native handle lifetime stress: iterations={0}, fixtureBytes={1}, " +
                "fixtureBoneKeys={2}, fixtureMaxFrame={3}, retainedHandleAllowance={4}, " +
                "retainedManagedMemoryAllowanceBytes={5} (machine-sensitive; kept conservative).",
                NativeHandleLifetimeStressIterationCount,
                vmdBytes.Length,
                summary.BoneKeyframeCount,
                summary.MaxFrame,
                RetainedHandleAllowance,
                RetainedManagedMemoryAllowanceBytes);
            TestContext.Progress.WriteLine(FormatStressMetrics("baseline", report.Baseline));
            TestContext.Progress.WriteLine(
                "VMD import/Timeline native handle lifetime stress peak-after-cleanup: " +
                "gcAllocatedBytesPerIteration={0}, gcRetainedBytes={1}, handleCount={2}",
                FormatManagedGcAllocation(report.PeakManagedGcAllocatedBytesPerIteration),
                report.PeakManagedRetainedBytes,
                report.PeakHandleCount);
            TestContext.Progress.WriteLine(FormatStressMetrics("final-after-teardown", report.Final));
            UnityEngine.Debug.Log(
                FormatStressMetrics("baseline", report.Baseline) + Environment.NewLine +
                string.Format(
                    "VMD import/Timeline native handle lifetime stress peak-after-cleanup: " +
                    "gcAllocatedBytesPerIteration={0}, gcRetainedBytes={1}, handleCount={2}",
                    FormatManagedGcAllocation(report.PeakManagedGcAllocatedBytesPerIteration),
                    report.PeakManagedRetainedBytes,
                    report.PeakHandleCount) + Environment.NewLine +
                FormatStressMetrics("final-after-teardown", report.Final));

            Assert.That(
                report.Final.HandleCount,
                Is.LessThanOrEqualTo(report.Baseline.HandleCount + RetainedHandleAllowance),
                "Process.HandleCount retained beyond the conservative allowance after AssetDatabase and " +
                "Timeline teardown. Baseline=" + report.Baseline.HandleCount +
                ", final=" + report.Final.HandleCount + ". HandleCount is machine-sensitive; inspect the " +
                "per-iteration peak before changing this gate.");
            Assert.That(
                report.Final.ManagedRetainedBytes,
                Is.LessThanOrEqualTo(report.Baseline.ManagedRetainedBytes + RetainedManagedMemoryAllowanceBytes),
                "Managed memory retained beyond the conservative allowance after full GC/finalizers. " +
                "Baseline=" + report.Baseline.ManagedRetainedBytes +
                ", final=" + report.Final.ManagedRetainedBytes +
                ". Unity editor caches can be machine-sensitive; inspect the diagnostic metrics before " +
                "changing this gate.");
#endif
        }

#if UNITY_EDITOR_WIN
        private readonly struct StressMetrics
        {
            public readonly long ManagedGcAllocatedBytes;
            public readonly long ManagedRetainedBytes;
            public readonly int HandleCount;

            public StressMetrics(
                long managedGcAllocatedBytes,
                long managedRetainedBytes,
                int handleCount)
            {
                ManagedGcAllocatedBytes = managedGcAllocatedBytes;
                ManagedRetainedBytes = managedRetainedBytes;
                HandleCount = handleCount;
            }
        }

        private readonly struct NativeHandleLifetimeStressReport
        {
            public readonly StressMetrics Baseline;
            public readonly StressMetrics Final;
            public readonly long PeakManagedGcAllocatedBytesPerIteration;
            public readonly long PeakManagedRetainedBytes;
            public readonly int PeakHandleCount;

            public NativeHandleLifetimeStressReport(
                StressMetrics baseline,
                StressMetrics final,
                long peakManagedGcAllocatedBytesPerIteration,
                long peakManagedRetainedBytes,
                int peakHandleCount)
            {
                Baseline = baseline;
                Final = final;
                PeakManagedGcAllocatedBytesPerIteration = peakManagedGcAllocatedBytesPerIteration;
                PeakManagedRetainedBytes = peakManagedRetainedBytes;
                PeakHandleCount = peakHandleCount;
            }
        }

        private NativeHandleLifetimeStressReport RunNativeHandleLifetimeStress(byte[] vmdBytes)
        {
            MmdPmxAsset? pmxAsset = null;
            MmdVmdAsset? vmdAsset = null;
            StressMetrics baseline = default;
            long peakManagedGcAllocatedBytesPerIteration = 0L;
            long peakManagedRetainedBytes = 0L;
            int peakHandleCount = 0;
            try
            {
                PrepareTemporaryAssets(vmdBytes);
                AssetDatabase.ImportAsset(TempVmdPath, ImportAssetOptions.ForceUpdate);
                pmxAsset = LoadImportedPmxAsset();
                vmdAsset = LoadImportedVmdAsset();
                string assetGuid = AssetDatabase.AssetPathToGUID(TempVmdPath);
                Assert.That(assetGuid, Is.Not.Null.And.Not.Empty);

                using (TimelineEvaluationFixture warmup = CreateTimelineEvaluationFixture(pmxAsset, vmdAsset))
                {
                    warmup.Director.time = 0.0;
                    warmup.Director.RebuildGraph();
                    warmup.Director.Evaluate();
                    Assert.That(warmup.Controller.IsConfigured, Is.True);
                    Assert.That(warmup.Controller.LastSnapshot, Is.Not.Null);
                }

                ForceFullGarbageCollection();
                baseline = CaptureStressMetrics();
                peakManagedRetainedBytes = baseline.ManagedRetainedBytes;
                peakHandleCount = baseline.HandleCount;

                for (int iteration = 0; iteration < NativeHandleLifetimeStressIterationCount; iteration++)
                {
                    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                    AssetDatabase.ImportAsset(TempVmdPath, ImportAssetOptions.ForceUpdate);
                    vmdAsset = LoadImportedVmdAsset();
                    Assert.That(
                        AssetDatabase.AssetPathToGUID(TempVmdPath),
                        Is.EqualTo(assetGuid),
                        "The temporary VMD AssetDatabase GUID changed during reimport iteration " + iteration + ".");

                    using (TimelineEvaluationFixture evaluation =
                           CreateTimelineEvaluationFixture(pmxAsset, vmdAsset))
                    {
                        evaluation.Director.time = 0.0;
                        evaluation.Director.RebuildGraph();
                        evaluation.Director.Evaluate();
                        Assert.That(
                            evaluation.Controller.IsConfigured,
                            Is.True,
                            "Timeline controller was not configured at iteration " + iteration + ".");
                        Assert.That(
                            evaluation.Controller.LastSnapshot,
                            Is.Not.Null,
                            "Timeline controller did not produce a snapshot at iteration " + iteration + ".");
                    }

                    ForceFullGarbageCollection();
                    long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
                    long iterationManagedGcAllocatedBytes = Math.Max(0L, allocatedAfter - allocatedBefore);
                    StressMetrics current = CaptureStressMetrics();
                    peakManagedGcAllocatedBytesPerIteration = Math.Max(
                        peakManagedGcAllocatedBytesPerIteration,
                        iterationManagedGcAllocatedBytes);
                    peakManagedRetainedBytes = Math.Max(peakManagedRetainedBytes, current.ManagedRetainedBytes);
                    peakHandleCount = Math.Max(peakHandleCount, current.HandleCount);

                    TestContext.Progress.WriteLine(
                        "VMD import/Timeline native handle lifetime stress iteration {0}: " +
                        "gcAllocatedBytes={1}, gcRetainedBytes={2}, handleCount={3}",
                        iteration,
                        FormatManagedGcAllocation(iterationManagedGcAllocatedBytes),
                        current.ManagedRetainedBytes,
                        current.HandleCount);
                }

                pmxAsset = null;
                vmdAsset = null;
            }
            finally
            {
                pmxAsset = null;
                vmdAsset = null;
                CleanupTemporaryAssets();
            }

            ForceFullGarbageCollection();
            return new NativeHandleLifetimeStressReport(
                baseline,
                CaptureStressMetrics(),
                peakManagedGcAllocatedBytesPerIteration,
                peakManagedRetainedBytes,
                peakHandleCount);
        }

        private static void ForceFullGarbageCollection()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        }

        private static StressMetrics CaptureStressMetrics()
        {
            return new StressMetrics(
                GC.GetAllocatedBytesForCurrentThread(),
                GC.GetTotalMemory(forceFullCollection: true),
                ReadProcessHandleCount());
        }

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessHandleCount(
            IntPtr hProcess,
            out uint pdwHandleCount);

        private static int ReadProcessHandleCount()
        {
            IntPtr processHandle = GetCurrentProcess();
            if (processHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                Assert.Fail(
                    "GetCurrentProcess returned NULL for the opt-in stress gate. Win32Error=" + error + ".");
            }

            if (!GetProcessHandleCount(processHandle, out uint handleCount))
            {
                int error = Marshal.GetLastWin32Error();
                Assert.Fail(
                    "GetProcessHandleCount failed for the current process. Win32Error=" + error + ".");
            }

            if (handleCount == 0)
            {
                Assert.Fail(
                    "GetProcessHandleCount returned zero for the current Windows process; refusing to treat " +
                    "zero as meaningful OS handle telemetry.");
            }

            return checked((int)handleCount);
        }

        private static string FormatManagedGcAllocation(long allocatedBytes)
        {
            return allocatedBytes == 0L
                ? "0 (unavailable_or_zero_observed)"
                : allocatedBytes.ToString();
        }

        private static string FormatStressMetrics(string label, StressMetrics metrics)
        {
            return string.Format(
                "VMD import/Timeline native handle lifetime stress {0}: " +
                "gcAllocatedBytes={1}, gcRetainedBytes={2}, handleCount={3}",
                label,
                FormatManagedGcAllocation(metrics.ManagedGcAllocatedBytes),
                metrics.ManagedRetainedBytes,
                metrics.HandleCount);
        }
#endif

        private static void PrepareTemporaryAssets(byte[] vmdBytes)
        {
            AssetDatabase.DeleteAsset(TempDirectory);
            AssetDatabase.Refresh();
            string tempDirectory = Path.Combine(MmdTestFixtures.ProjectRoot, TempDirectory.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(tempDirectory);

            string pmxSource = MmdTestFixtures.FixtureAssetPath("test_1bone_cube.pmx");
            File.Copy(
                pmxSource,
                Path.Combine(MmdTestFixtures.ProjectRoot, TempPmxPath),
                overwrite: true);
            AssetDatabase.ImportAsset(TempPmxPath, ImportAssetOptions.ForceUpdate);
            File.WriteAllBytes(Path.Combine(MmdTestFixtures.ProjectRoot, TempVmdPath), vmdBytes);
        }

        private static MmdPmxAsset LoadImportedPmxAsset()
        {
            MmdPmxAsset? asset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(asset, Is.Not.Null, "The temporary PMX fixture was not imported.");
            return asset!;
        }

        private static MmdVmdAsset LoadImportedVmdAsset()
        {
            MmdVmdAsset? asset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            Assert.That(asset, Is.Not.Null, "The generated VMD was not imported as MmdVmdAsset.");
            return asset!;
        }

        private static TimelineEvaluationFixture CreateTimelineEvaluationFixture(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset)
        {
            MmdUnityModelInstance instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(
                pmxAsset,
                Vector3.zero,
                parent: null);
            GameObject? directorObject = null;
            TimelineAsset? timelineAsset = null;
            try
            {
                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                directorObject = new GameObject("mmd-vmd-import-timeline-perf-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                MmdVmdTimelineTrack track = MmdTimelineAssetWorkflow.CreateVmdTrack(
                    timelineAsset,
                    director,
                    controller);
                MmdTimelineAssetWorkflow.CreateVmdClip(
                    track,
                    vmdAsset,
                    controller,
                    frameRate: 30.0f,
                    director: director);
                return new TimelineEvaluationFixture(instance, controller, timelineAsset, directorObject, director);
            }
            catch
            {
                if (directorObject != null)
                {
                    Object.DestroyImmediate(directorObject);
                }

                if (timelineAsset != null)
                {
                    Object.DestroyImmediate(timelineAsset);
                }

                DestroySceneInstance(instance);
                throw;
            }
        }

        private static int ReadPositiveIntEnvironmentVariable(string name, int defaultValue)
        {
            string? raw = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            if (!int.TryParse(raw, out int value) || value <= 0)
            {
                Assert.Fail("Environment variable " + name + " must be a positive integer; actual=" + raw);
            }

            return value;
        }

        private static void DestroySceneInstance(MmdUnityModelInstance? instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.Root != null && !AssetDatabase.Contains(instance.Root))
            {
                Object.DestroyImmediate(instance.Root);
            }

            if (instance.Mesh != null && !AssetDatabase.Contains(instance.Mesh))
            {
                Object.DestroyImmediate(instance.Mesh);
            }

            foreach (Material material in instance.Materials)
            {
                if (material != null && !AssetDatabase.Contains(material))
                {
                    Object.DestroyImmediate(material);
                }
            }

            foreach (Texture2D texture in instance.OwnedTextures)
            {
                if (texture != null && !AssetDatabase.Contains(texture))
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        private sealed class TimelineEvaluationFixture : IDisposable
        {
            private readonly TimelineAsset timelineAsset;
            private readonly GameObject directorObject;
            public MmdUnityModelInstance Instance { get; }
            public MmdUnityPlaybackController Controller { get; }
            public PlayableDirector Director { get; }

            public TimelineEvaluationFixture(MmdUnityModelInstance instance,
                MmdUnityPlaybackController controller, TimelineAsset timelineAsset,
                GameObject directorObject, PlayableDirector director)
            {
                Instance = instance;
                Controller = controller;
                this.timelineAsset = timelineAsset;
                this.directorObject = directorObject;
                Director = director;
            }

            public void Dispose()
            {
                Director.Stop();
                Director.playableAsset = null;
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(timelineAsset);
                DestroySceneInstance(Instance);
            }
        }
    }
}
