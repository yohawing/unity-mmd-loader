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
                report.phases.Add(MeasureSingle("pmx-load", () => File.ReadAllBytes(pmxPath).Length));
                long parseStart = Stopwatch.GetTimestamp();
                model = parser.LoadModel(pmxBytes);
                report.phases.Add(MmdPerformanceBaseline.BuildPhase("pmx-parse", new[] { ElapsedMs(parseStart) }, 0, 1));
                parseStart = Stopwatch.GetTimestamp();
                motion = parser.LoadMotion(vmdBytes);
                report.phases.Add(MmdPerformanceBaseline.BuildPhase("vmd-load-parse", new[] { ElapsedMs(parseStart) }, 0, 1));
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

            MmdUnityModelInstance? builtInstance = null;
            try
            {
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long buildStart = Stopwatch.GetTimestamp();
                builtInstance = MmdUnityModelFactory.CreateSkinnedModel(model, pmxPath);
                report.phases.Add(MmdPerformanceBaseline.BuildPhase(
                    "unity-asset-build",
                    new[] { ElapsedMs(buildStart) },
                    GC.GetAllocatedBytesForCurrentThread() - allocatedBefore,
                    1));
            }
            catch (Exception exception)
            {
                report.phases.Add(MmdPerformanceBaseline.ErrorPhase("unity-asset-build", exception.Message));
                report.status = MmdPerformanceStatus.Error;
                report.skipReason = "Unity asset build failed: " + exception.Message;
            }
            finally
            {
                DestroyInstance(builtInstance);
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
                var session = new MmdRuntimeSession(model, motion, pmxPath, vmdPath);
                var frames = new List<MmdEvaluatedFrame>(options.measurementFrames);
                for (int i = 0; i < options.measurementFrames; i++)
                    frames.Add(session.EvaluateFrame(i, MmdPlaybackTime.ToTime(i, options.frameRate)));
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
                    MmdUnityFrameApplier.ApplyFrame(applyInstance, frames[i]);
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
                long start = Stopwatch.GetTimestamp();
                session.EvaluateAndCopy(i, worldMatrices, morphWeights, ikEnabled);
                double elapsed = ElapsedMs(start);
                samples.Add(elapsed);
                MmdPerformanceBaseline.MixChecksum(ref checksum, i, worldMatrices, morphWeights, ikEnabled);
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
            report.phases.Add(MmdPerformanceBaseline.UnavailablePhase("live-physics-evaluate", reason));
            report.phases.Add(MmdPerformanceBaseline.UnavailablePhase("live-physics-sync", reason));
            report.phases.Add(MmdPerformanceBaseline.UnavailablePhase("live-physics-step", reason));
            report.phases.Add(MmdPerformanceBaseline.UnavailablePhase("live-physics-apply", reason));
        }

        private static MmdPerformancePhaseReport MeasureSingle(string name, Func<int> action)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            action();
            return MmdPerformanceBaseline.BuildPhase(name, new[] { ElapsedMs(start) }, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore, 1);
        }
    }
}
