#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using Mmd.Motion;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Pose;

namespace Mmd
{
    internal sealed class MmdRuntimeFrameEvaluation
    {
        public MmdSampledMotion SampledMotion { get; init; } = new();
        public Dictionary<int, float[]> SampledWorldMatrices { get; init; } = new();
        public MmdSampledMotion AppendedMotion { get; init; } = new();
        public Dictionary<int, float[]> AppendedWorldMatrices { get; init; } = new();
        public MmdSampledMotion IkMotion { get; init; } = new();
        public Dictionary<int, float[]> IkWorldMatrices { get; init; } = new();
        public MmdSampledMotion FinalMotion { get; init; } = new();
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
            MmdSampledMotion appendedMotion = ApplyBeforePhysicsAppendTransforms(model, boneMorphedMotion, ikSolver);
            RecordTiming(timing, started, ticks => timing!.AppendTransformTicks = ticks);

            started = Stopwatch.GetTimestamp();
            Dictionary<int, float[]> appendedWorldMatrices = MmdPoseEvaluator.EvaluateWorldMatrices(model, appendedMotion);
            RecordTiming(timing, started, ticks => timing!.AppendedWorldTicks = ticks);

            started = Stopwatch.GetTimestamp();
            MmdSampledMotion ikMotion = ikSolver is MmdIkSolver mmdIkSolver
                ? mmdIkSolver.Solve(model, boneMorphedMotion, appendedMotion, MmdBoneEvaluationPass.BeforePhysics)
                : ikSolver.Solve(model, appendedMotion);
            RecordTiming(timing, started, ticks => timing!.IkSolveTicks = ticks);

            started = Stopwatch.GetTimestamp();
            Dictionary<int, float[]> ikWorldMatrices = MmdPoseEvaluator.EvaluateWorldMatrices(model, ikMotion);
            RecordTiming(timing, started, ticks => timing!.IkWorldTicks = ticks);

            started = Stopwatch.GetTimestamp();
            physicsBackend.Step(frame, deltaTime: 0.0f);
            RecordTiming(timing, started, ticks => timing!.PhysicsStepTicks = ticks);

            MmdSampledMotion finalMotion = ikMotion;
            Dictionary<int, float[]> finalWorldMatrices = ikWorldMatrices;
            if (model.HasDeformAfterPhysicsBones)
            {
                started = Stopwatch.GetTimestamp();
                MmdSampledMotion afterAppendMotion = MmdAppendTransformEvaluator.ApplyAppendTransforms(
                    model,
                    ikMotion,
                    MmdBoneEvaluationPass.AfterPhysics);
                RecordTiming(timing, started, ticks => timing!.AppendTransformTicks += ticks);

                started = Stopwatch.GetTimestamp();
                MmdSampledMotion afterIkMotion = ikSolver is MmdIkSolver afterPassIkSolver
                    ? afterPassIkSolver.Solve(model, ikMotion, afterAppendMotion, MmdBoneEvaluationPass.AfterPhysics)
                    : ikSolver.Solve(model, afterAppendMotion);
                finalMotion = MergeAfterPhysicsMotion(model, ikMotion, afterIkMotion);
                RecordTiming(timing, started, ticks => timing!.IkSolveTicks += ticks);

                started = Stopwatch.GetTimestamp();
                finalWorldMatrices = MmdPoseEvaluator.EvaluateWorldMatrices(model, finalMotion);
                RecordTiming(timing, started, ticks => timing!.IkWorldTicks += ticks);
            }

            return new MmdRuntimeFrameEvaluation
            {
                SampledMotion = sampledMotion,
                SampledWorldMatrices = sampledWorldMatrices,
                AppendedMotion = appendedMotion,
                AppendedWorldMatrices = appendedWorldMatrices,
                IkMotion = ikMotion,
                IkWorldMatrices = ikWorldMatrices,
                FinalMotion = finalMotion,
                WorldMatrices = finalWorldMatrices,
                Timing = timing
            };
        }

        private static MmdSampledMotion ApplyBeforePhysicsAppendTransforms(
            MmdModelDefinition model,
            MmdSampledMotion boneMorphedMotion,
            IMmdIkSolver ikSolver)
        {
            if (ikSolver is MmdIkSolver appendAwareIkSolver)
            {
                return appendAwareIkSolver.ApplyAppendTransforms(model, boneMorphedMotion, MmdBoneEvaluationPass.BeforePhysics);
            }

            if (!model.HasDeformAfterPhysicsBones && ikSolver is IMmdAppendTransformProvider appendTransformProvider)
            {
                return appendTransformProvider.ApplyAppendTransforms(model, boneMorphedMotion);
            }

            return MmdAppendTransformEvaluator.ApplyAppendTransforms(
                model,
                boneMorphedMotion,
                MmdBoneEvaluationPass.BeforePhysics);
        }

        private static MmdSampledMotion MergeAfterPhysicsMotion(
            MmdModelDefinition model,
            MmdSampledMotion beforePhysicsMotion,
            MmdSampledMotion afterPhysicsMotion)
        {
            var result = CopyMotion(beforePhysicsMotion);
            IReadOnlyList<MmdBoneDefinition> bones = model.bones != null
                ? model.bones
                : System.Array.Empty<MmdBoneDefinition>();
            for (int i = 0; i < bones.Count; i++)
            {
                MmdBoneDefinition bone = bones[i];
                if (!bone.deformAfterPhysics)
                {
                    continue;
                }

                if (afterPhysicsMotion.Bones.TryGetValue(bone.name, out MmdBonePoseSample pose))
                {
                    result.Bones[bone.name] = pose;
                }
            }

            return result;
        }

        private static MmdSampledMotion CopyMotion(MmdSampledMotion source)
        {
            var result = new MmdSampledMotion();
            foreach (KeyValuePair<string, MmdBonePoseSample> bone in source.Bones)
            {
                result.Bones[bone.Key] = bone.Value;
            }

            foreach (KeyValuePair<string, float> morph in source.Morphs)
            {
                result.Morphs[morph.Key] = morph.Value;
            }

            foreach (KeyValuePair<string, bool> ikState in source.IkStates)
            {
                result.IkStates[ikState.Key] = ikState.Value;
            }

            return result;
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
