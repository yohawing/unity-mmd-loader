#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Mmd.Native;
using Mmd.Parser;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    public static partial class MmdPerformanceBaselineCli
    {
        public static void RunFromCommandLine()
        {
            MmdPerformanceBaselineOptions options = CreateOptions();
            string outputPath = GetArgument("-out", DefaultOutputPath());
            MmdPerformanceBaselineReport report;
            try
            {
                report = Run(options);
            }
            catch (Exception exception)
            {
                report = CreateReport(options, MmdPerformanceStatus.Error, exception.GetType().Name + ": " + exception.Message);
            }

            WriteReport(report, outputPath);
            EditorApplication.Exit(report.status == MmdPerformanceStatus.Pass ? 0 : report.status == MmdPerformanceStatus.Skip ? 2 : 1);
        }

        public static MmdPerformanceBaselineReport Run(MmdPerformanceBaselineOptions options)
        {
            MmdPerformanceBaseline.ValidateOptions(options);
            MmdPerformanceBaselineReport report = CreateReport(options, MmdPerformanceStatus.Pass, string.Empty);
            string pmxPath = Path.GetFullPath(options.pmxPath);
            string vmdPath = Path.GetFullPath(options.vmdPath);
            string physicsPath = Path.GetFullPath(options.physicsPmxPath);
            if (!File.Exists(pmxPath) || !File.Exists(vmdPath) || !File.Exists(physicsPath))
            {
                report.status = MmdPerformanceStatus.Skip;
                report.skipReason = "A tracked PMX/VMD or physics fixture is missing.";
                AddSkippedRequiredPhases(report, report.skipReason);
                return report;
            }

            report.fixtureSha256 = Sha256File(pmxPath);
            report.vmdFixtureSha256 = Sha256File(vmdPath);
            report.physicsFixtureSha256 = Sha256File(physicsPath);
            byte[] pmxBytes = File.ReadAllBytes(pmxPath);
            byte[] vmdBytes = File.ReadAllBytes(vmdPath);
            var parser = new NativeMmdParser();
            MmdModelDefinition model;
            MmdMotionDefinition motion;
            try
            {
                report.phases.Add(MeasureRepeated("pmx-load", options, () => File.ReadAllBytes(pmxPath)));
                MmdModelDefinition? measuredModel = null;
                report.phases.Add(MeasureRepeated("pmx-parse", options, () =>
                {
                    measuredModel = parser.LoadModel(pmxBytes);
                }));
                model = measuredModel ?? throw new InvalidOperationException("PMX parse produced no model.");
                MmdMotionDefinition? measuredMotion = null;
                report.phases.Add(MeasureRepeated("vmd-load-parse", options, () =>
                {
                    measuredMotion = parser.LoadMotion(vmdBytes);
                }));
                motion = measuredMotion ?? throw new InvalidOperationException("VMD parse produced no motion.");
                MmdModelValidator.ThrowIfInvalid(model);
                MmdMotionValidator.ThrowIfInvalid(motion);
            }
            catch (Exception exception)
            {
                bool unavailable = IsNativeCapabilityUnavailable(exception);
                report.status = unavailable ? MmdPerformanceStatus.Skip : MmdPerformanceStatus.Error;
                report.skipReason = (unavailable ? "mmd-anim parser/backend unavailable: " : "Tracked fixture parse/validation failed: ") + exception.Message;
                AddSkippedRequiredPhases(report, report.skipReason);
                return report;
            }

            try
            {
                report.phases.Add(MeasureUnityAssetBuild(model, pmxPath, options));
            }
            catch (Exception exception)
            {
                report.phases.Add(MmdPerformanceBaseline.ErrorPhase("unity-asset-build", exception.Message));
                report.status = MmdPerformanceStatus.Error;
                report.skipReason = "Unity asset build failed: " + exception.Message;
            }
            try
            {
                MeasureNativeEvaluateCopy(report, options, pmxBytes, vmdBytes);
            }
            catch (Exception exception)
            {
                bool unavailable = IsNativeCapabilityUnavailable(exception);
                report.phases.Add(unavailable
                    ? MmdPerformanceBaseline.SkipPhase("native-evaluate-copy", exception.Message)
                    : MmdPerformanceBaseline.ErrorPhase("native-evaluate-copy", exception.Message));
                report.status = unavailable ? MmdPerformanceStatus.Skip : MmdPerformanceStatus.Error;
                report.skipReason = (unavailable ? "Native evaluate/copy unavailable: " : "Native evaluate/copy failed: ") + exception.Message;
            }

            MmdUnityModelInstance? applyInstance = null;
            try
            {
                applyInstance = MmdUnityModelFactory.CreateSkinnedModel(model, pmxPath);
                using var session = new MmdRuntimeSession(model, motion, pmxPath, vmdPath);
                int totalFrames = options.warmupFrames + options.measurementFrames;
                var frames = new List<MmdEvaluatedFrame>(totalFrames);
                for (int i = 0; i < totalFrames; i++)
                    frames.Add(session.EvaluateNativeFrame(i, MmdPlaybackTime.ToTime(i, options.frameRate)));
                for (int i = 0; i < options.warmupFrames; i++)
                    MmdUnityFrameApplier.ApplyFrame(applyInstance, frames[i % frames.Count]);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var samples = new List<double>(options.measurementFrames);
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < options.measurementFrames; i++)
                {
                    long start = Stopwatch.GetTimestamp();
                    MmdUnityFrameApplier.ApplyFrame(applyInstance, frames[options.warmupFrames + i]);
                    samples.Add(ElapsedMs(start));
                }
                report.phases.Add(MmdPerformanceBaseline.BuildPhase(
                    "unity-pose-morph-apply",
                    samples,
                    GC.GetAllocatedBytesForCurrentThread() - allocatedBefore,
                    options.measurementFrames,
                    "MmdUnityFrameApplier only; evaluated frames are prepared before measurement."));
            }
            catch (Exception exception)
            {
                report.phases.Add(MmdPerformanceBaseline.ErrorPhase("unity-pose-morph-apply", exception.Message));
                report.status = MmdPerformanceStatus.Error;
                report.skipReason = "Unity pose/morph apply failed: " + exception.Message;
            }
            finally
            {
                DestroyInstance(applyInstance);
            }

            MeasureLivePhysics(report, options, physicsPath, vmdBytes);
            if (report.status != MmdPerformanceStatus.Error && report.phases.Exists(phase => phase.status == MmdPerformanceStatus.Skip))
            {
                report.status = MmdPerformanceStatus.Skip;
                if (string.IsNullOrWhiteSpace(report.skipReason))
                    report.skipReason = "One or more required performance phases were skipped.";
            }

            if (!string.IsNullOrWhiteSpace(options.baselinePath) && report.status == MmdPerformanceStatus.Pass)
            {
                try
                {
                    MmdPerformanceBaselineReport baseline = LoadBaseline(options.baselinePath);
                    report.comparison = MmdPerformanceBaselineComparer.Compare(baseline, report);
                    if (!report.comparison.passed)
                    {
                        report.status = MmdPerformanceStatus.Fail;
                        report.skipReason = report.comparison.reason;
                    }
                }
                catch (Exception exception)
                {
                    report.status = MmdPerformanceStatus.Error;
                    report.skipReason = "Baseline report could not be loaded: " + exception.Message;
                }
            }

            return report;
        }

        private static void MeasureNativeEvaluateCopy(
            MmdPerformanceBaselineReport report,
            MmdPerformanceBaselineOptions options,
            byte[] pmxBytes,
            byte[] vmdBytes)
        {
            using MmdRuntimeFfiPlaybackSession session = MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes);
            report.backend = "mmd-runtime-ffi";
            float[] worldMatrices = new float[session.WorldMatrixFloatCount];
            float[] morphWeights = new float[session.MorphWeightCount];
            byte[] ikEnabled = new byte[session.IkEnabledCount];
            for (int i = 0; i < options.warmupFrames; i++)
                session.EvaluateAndCopy(i, worldMatrices, morphWeights, ikEnabled);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var samples = new List<double>(options.measurementFrames);
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            ulong checksum = MmdPerformanceBaseline.ChecksumSeed;
            for (int i = 0; i < options.measurementFrames; i++)
            {
                int frame = options.warmupFrames + i;
                long start = Stopwatch.GetTimestamp();
                session.EvaluateAndCopy(frame, worldMatrices, morphWeights, ikEnabled);
                double elapsed = ElapsedMs(start);
                samples.Add(elapsed);
                MmdPerformanceBaseline.MixChecksum(ref checksum, frame, worldMatrices, morphWeights, ikEnabled);
            }

            report.deterministicResultChecksum = MmdPerformanceBaseline.FinishChecksum(checksum);
            report.phases.Add(MmdPerformanceBaseline.BuildPhase(
                "native-evaluate-copy",
                samples,
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore,
                options.measurementFrames,
                "MmdRuntimeFfiPlaybackSession with preallocated world/morph/IK buffers."));
        }

        private static void AddSkippedRequiredPhases(MmdPerformanceBaselineReport report, string reason)
        {
            foreach (string name in MmdPerformanceBaseline.RequiredPhaseNames)
                if (!report.phases.Exists(phase => phase.name == name))
                    report.phases.Add(MmdPerformanceBaseline.SkipPhase(name, reason));
        }

        private static MmdPerformancePhaseReport MeasureRepeated(
            string name,
            MmdPerformanceBaselineOptions options,
            Action action)
        {
            for (int i = 0; i < options.warmupFrames; i++)
                action();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var samples = new List<double>(options.measurementFrames);
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < options.measurementFrames; i++)
            {
                long start = Stopwatch.GetTimestamp();
                action();
                samples.Add(ElapsedMs(start));
            }

            return MmdPerformanceBaseline.BuildPhase(
                name,
                samples,
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore,
                options.measurementFrames);
        }

        private static MmdPerformancePhaseReport MeasureUnityAssetBuild(
            MmdModelDefinition model,
            string pmxPath,
            MmdPerformanceBaselineOptions options)
        {
            for (int i = 0; i < options.warmupFrames; i++)
            {
                MmdUnityModelInstance instance = MmdUnityModelFactory.CreateSkinnedModel(model, pmxPath);
                DestroyInstance(instance);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var samples = new List<double>(options.measurementFrames);
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < options.measurementFrames; i++)
            {
                long start = Stopwatch.GetTimestamp();
                MmdUnityModelInstance instance = MmdUnityModelFactory.CreateSkinnedModel(model, pmxPath);
                samples.Add(ElapsedMs(start));
                DestroyInstance(instance);
            }

            return MmdPerformanceBaseline.BuildPhase(
                "unity-asset-build",
                samples,
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore,
                options.measurementFrames);
        }
    }
}
