#nullable enable

using System;
using System.IO;
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
        private const string TempDirectory = "Assets/__MmdVmdImportTimelinePerformanceTests";
        private const string TempPmxPath = TempDirectory + "/test_1bone_cube.pmx";
        private const string TempVmdPath = TempDirectory + "/generated-vmd-timeline.vmd";
        private const int MinimumMeasurementCount = 20;
        private const int DefaultMeasurementCount = MinimumMeasurementCount;
        private const int DefaultGeneratedBoneKeyframeCount = 300_000;
        private const int GeneratedFrameSpan = 12_000;
        private const double P95BudgetMilliseconds = 100.0;

        [TearDown]
        public void TearDown()
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
            MmdVmdParseSummary summary = MmdVmdBinarySummaryReader.Read(vmdBytes);
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
