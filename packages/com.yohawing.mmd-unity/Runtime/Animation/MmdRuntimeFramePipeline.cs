#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using Yohawing.MmdUnity.Motion;
using Yohawing.MmdUnity.Parser;
using Yohawing.MmdUnity.Physics;
using Yohawing.MmdUnity.Pose;

namespace Yohawing.MmdUnity
{
    internal sealed class MmdRuntimeFrameEvaluation
    {
        public MmdSampledMotion SampledMotion { get; init; } = new();
        public Dictionary<int, float[]> SampledWorldMatrices { get; init; } = new();
        public MmdSampledMotion AppendedMotion { get; init; } = new();
        public Dictionary<int, float[]> AppendedWorldMatrices { get; init; } = new();
        public MmdSampledMotion IkMotion { get; init; } = new();
        public Dictionary<int, float[]> IkWorldMatrices { get; init; } = new();
        public Dictionary<int, float[]> WorldMatrices { get; init; } = new();
        public MmdRuntimeFrameTiming? Timing { get; init; }
    }

    internal sealed class MmdRuntimeFrameTiming
    {
        public long MotionSamplingTicks { get; set; }
        public long SampledWorldTicks { get; set; }
        public long BoneMorphTicks { get; set; }
        public long AppendTransformTicks { get; set; }
        public long AppendedWorldTicks { get; set; }
        public long IkSolveTicks { get; set; }
        public long IkWorldTicks { get; set; }
        public long PhysicsStepTicks { get; set; }
    }

    internal static class MmdRuntimeFramePipeline
    {
        public static MmdRuntimeFrameEvaluation Evaluate(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            IMmdPhysicsBackend physicsBackend,
            IMmdIkSolver? ikSolver = null,
            bool collectTiming = false)
        {
            ikSolver ??= new MmdIkSolver();
            MmdRuntimeFrameTiming? timing = collectTiming ? new MmdRuntimeFrameTiming() : null;
            long started;

            started = Stopwatch.GetTimestamp();
            MmdSampledMotion sampledMotion = VmdMotionSampler.Sample(motion, frame);
            RecordTiming(timing, started, ticks => timing!.MotionSamplingTicks = ticks);

            started = Stopwatch.GetTimestamp();
            Dictionary<int, float[]> sampledWorldMatrices = MmdPoseEvaluator.EvaluateWorldMatrices(model, sampledMotion);
            RecordTiming(timing, started, ticks => timing!.SampledWorldTicks = ticks);

            started = Stopwatch.GetTimestamp();
            MmdSampledMotion boneMorphedMotion = MmdBoneMorphEvaluator.ApplyBoneMorphs(model, sampledMotion);
            RecordTiming(timing, started, ticks => timing!.BoneMorphTicks = ticks);

            started = Stopwatch.GetTimestamp();
            MmdSampledMotion appendedMotion = ikSolver is IMmdAppendTransformProvider appendTransformProvider
                ? appendTransformProvider.ApplyAppendTransforms(model, boneMorphedMotion)
                : MmdAppendTransformEvaluator.ApplyAppendTransforms(model, boneMorphedMotion);
            RecordTiming(timing, started, ticks => timing!.AppendTransformTicks = ticks);

            started = Stopwatch.GetTimestamp();
            Dictionary<int, float[]> appendedWorldMatrices = MmdPoseEvaluator.EvaluateWorldMatrices(model, appendedMotion);
            RecordTiming(timing, started, ticks => timing!.AppendedWorldTicks = ticks);

            started = Stopwatch.GetTimestamp();
            MmdSampledMotion ikMotion = ikSolver is MmdIkSolver mmdIkSolver
                ? mmdIkSolver.Solve(model, boneMorphedMotion, appendedMotion)
                : ikSolver.Solve(model, appendedMotion);
            RecordTiming(timing, started, ticks => timing!.IkSolveTicks = ticks);

            started = Stopwatch.GetTimestamp();
            Dictionary<int, float[]> ikWorldMatrices = MmdPoseEvaluator.EvaluateWorldMatrices(model, ikMotion);
            RecordTiming(timing, started, ticks => timing!.IkWorldTicks = ticks);

            started = Stopwatch.GetTimestamp();
            physicsBackend.Step(frame, deltaTime: 0.0f);
            RecordTiming(timing, started, ticks => timing!.PhysicsStepTicks = ticks);

            return new MmdRuntimeFrameEvaluation
            {
                SampledMotion = sampledMotion,
                SampledWorldMatrices = sampledWorldMatrices,
                AppendedMotion = appendedMotion,
                AppendedWorldMatrices = appendedWorldMatrices,
                IkMotion = ikMotion,
                IkWorldMatrices = ikWorldMatrices,
                WorldMatrices = ikWorldMatrices,
                Timing = timing
            };
        }

        private static void RecordTiming(MmdRuntimeFrameTiming? timing, long started, System.Action<long> assign)
        {
            if (timing == null)
            {
                return;
            }

            assign(Stopwatch.GetTimestamp() - started);
        }
    }
}
