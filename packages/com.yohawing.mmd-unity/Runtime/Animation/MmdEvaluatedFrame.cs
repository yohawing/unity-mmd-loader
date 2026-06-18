#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Yohawing.MmdUnity.Motion;
using Yohawing.MmdUnity.Parser;
using Yohawing.MmdUnity.Physics;
using Yohawing.MmdUnity.Rendering;

namespace Yohawing.MmdUnity
{
    [Serializable]
    public sealed class MmdEvaluatedFrame
    {
        public int frame;
        public float time;
        public List<MmdEvaluatedBonePose> bones = new();
        public List<MmdEvaluatedMorphWeight> morphs = new();
        public List<MmdMaterialDescriptor> materials = new();
    }

    [Serializable]
    public sealed class MmdEvaluatedBonePose
    {
        public int index;
        public string name = string.Empty;
        public float[] localPosition = Array.Empty<float>();
        public float[] localRotation = Array.Empty<float>();
        public float[] localScale = Array.Empty<float>();
        public float[] worldMatrix = Array.Empty<float>();
    }

    [Serializable]
    public sealed class MmdEvaluatedMorphWeight
    {
        public string name = string.Empty;
        public float weight;
    }

    [Serializable]
    public sealed class MmdRuntimeFrameTimingSummary
    {
        public int measuredFrames;
        public double motionSamplingMs;
        public double sampledWorldMs;
        public double boneMorphMs;
        public double appendTransformMs;
        public double appendedWorldMs;
        public double ikSolveMs;
        public double ikWorldMs;
        public double physicsStepMs;
        public double averageMotionSamplingMs;
        public double averageSampledWorldMs;
        public double averageBoneMorphMs;
        public double averageAppendTransformMs;
        public double averageAppendedWorldMs;
        public double averageIkSolveMs;
        public double averageIkWorldMs;
        public double averagePhysicsStepMs;
    }

    public static class MmdRuntimeFrameEvaluator
    {
        public static MmdEvaluatedFrame EvaluatePhaseOneFrame(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            float time,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            return EvaluatePhaseOneFrame(model, motion, frame, time, includeMaterials: true, physicsBackend, ikSolver);
        }

        public static MmdEvaluatedFrame EvaluatePhaseOnePlaybackFrame(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            float time,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            return EvaluatePhaseOneFrame(model, motion, frame, time, includeMaterials: false, physicsBackend, ikSolver);
        }

        internal static MmdEvaluatedFrame EvaluateValidatedPhaseOnePlaybackFrame(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            float time,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            ValidateFrame(frame);
            ValidateTime(time);
            physicsBackend ??= new NullMmdPhysicsBackend();
            physicsBackend.Reset();

            MmdRuntimeFrameEvaluation evaluation = MmdRuntimeFramePipeline.Evaluate(model, motion, frame, physicsBackend, ikSolver);
            return BuildFrame(model, frame, time, evaluation, includeMaterials: false);
        }

        private static MmdEvaluatedFrame EvaluatePhaseOneFrame(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            float time,
            bool includeMaterials,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            ValidateInputs(model, motion);
            ValidateFrame(frame);
            ValidateTime(time);
            physicsBackend ??= new NullMmdPhysicsBackend();
            physicsBackend.Reset();

            MmdRuntimeFrameEvaluation evaluation = MmdRuntimeFramePipeline.Evaluate(model, motion, frame, physicsBackend, ikSolver);
            return BuildFrame(model, frame, time, evaluation, includeMaterials);
        }

        public static IReadOnlyList<MmdEvaluatedFrame> EvaluatePhaseOneFrames(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            IReadOnlyList<int> frames,
            float frameRate,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            ValidateInputs(model, motion);
            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            if (frames.Count == 0)
            {
                throw new ArgumentException("At least one frame is required.", nameof(frames));
            }

            MmdPlaybackTime.ValidateFrameRate(frameRate);

            physicsBackend ??= new NullMmdPhysicsBackend();
            physicsBackend.Reset();

            var evaluatedFrames = new List<MmdEvaluatedFrame>(frames.Count);
            var seenFrames = new HashSet<int>(frames.Count);
            foreach (int frame in frames.OrderBy(value => value))
            {
                ValidateFrame(frame);
                if (!seenFrames.Add(frame))
                {
                    throw new ArgumentException("Frame indices must be unique.", nameof(frames));
                }

                MmdRuntimeFrameEvaluation evaluation = MmdRuntimeFramePipeline.Evaluate(model, motion, frame, physicsBackend, ikSolver);
                evaluatedFrames.Add(BuildFrame(model, frame, MmdPlaybackTime.ToTime(frame, frameRate), evaluation, includeMaterials: true));
            }

            return evaluatedFrames;
        }

        public static MmdRuntimeFrameTimingSummary MeasurePhaseOneFramePipeline(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            IReadOnlyList<int> frames,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            ValidateInputs(model, motion);
            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            if (frames.Count == 0)
            {
                throw new ArgumentException("At least one frame is required.", nameof(frames));
            }

            physicsBackend ??= new NullMmdPhysicsBackend();
            physicsBackend.Reset();

            long motionSamplingTicks = 0;
            long sampledWorldTicks = 0;
            long boneMorphTicks = 0;
            long appendTransformTicks = 0;
            long appendedWorldTicks = 0;
            long ikSolveTicks = 0;
            long ikWorldTicks = 0;
            long physicsStepTicks = 0;
            var seenFrames = new HashSet<int>(frames.Count);
            foreach (int frame in frames.OrderBy(value => value))
            {
                ValidateFrame(frame);
                if (!seenFrames.Add(frame))
                {
                    throw new ArgumentException("Frame indices must be unique.", nameof(frames));
                }

                MmdRuntimeFrameEvaluation evaluation = MmdRuntimeFramePipeline.Evaluate(
                    model,
                    motion,
                    frame,
                    physicsBackend,
                    ikSolver,
                    collectTiming: true);
                MmdRuntimeFrameTiming timing = evaluation.Timing
                    ?? throw new InvalidOperationException("Phase timing was not collected.");
                motionSamplingTicks += timing.MotionSamplingTicks;
                sampledWorldTicks += timing.SampledWorldTicks;
                boneMorphTicks += timing.BoneMorphTicks;
                appendTransformTicks += timing.AppendTransformTicks;
                appendedWorldTicks += timing.AppendedWorldTicks;
                ikSolveTicks += timing.IkSolveTicks;
                ikWorldTicks += timing.IkWorldTicks;
                physicsStepTicks += timing.PhysicsStepTicks;
            }

            int measuredFrames = frames.Count;
            return new MmdRuntimeFrameTimingSummary
            {
                measuredFrames = measuredFrames,
                motionSamplingMs = TicksToMilliseconds(motionSamplingTicks),
                sampledWorldMs = TicksToMilliseconds(sampledWorldTicks),
                boneMorphMs = TicksToMilliseconds(boneMorphTicks),
                appendTransformMs = TicksToMilliseconds(appendTransformTicks),
                appendedWorldMs = TicksToMilliseconds(appendedWorldTicks),
                ikSolveMs = TicksToMilliseconds(ikSolveTicks),
                ikWorldMs = TicksToMilliseconds(ikWorldTicks),
                physicsStepMs = TicksToMilliseconds(physicsStepTicks),
                averageMotionSamplingMs = TicksToMilliseconds(motionSamplingTicks) / measuredFrames,
                averageSampledWorldMs = TicksToMilliseconds(sampledWorldTicks) / measuredFrames,
                averageBoneMorphMs = TicksToMilliseconds(boneMorphTicks) / measuredFrames,
                averageAppendTransformMs = TicksToMilliseconds(appendTransformTicks) / measuredFrames,
                averageAppendedWorldMs = TicksToMilliseconds(appendedWorldTicks) / measuredFrames,
                averageIkSolveMs = TicksToMilliseconds(ikSolveTicks) / measuredFrames,
                averageIkWorldMs = TicksToMilliseconds(ikWorldTicks) / measuredFrames,
                averagePhysicsStepMs = TicksToMilliseconds(physicsStepTicks) / measuredFrames
            };
        }

        private static void ValidateInputs(MmdModelDefinition model, MmdMotionDefinition motion)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            MmdMotionValidator.ThrowIfInvalid(motion);
        }

        private static void ValidateFrame(int frame)
        {
            MmdPlaybackTime.ValidateFrame(frame);
        }

        private static void ValidateTime(float time)
        {
            MmdPlaybackTime.ValidateTime(time);
        }

        private static MmdEvaluatedFrame BuildFrame(
            MmdModelDefinition model,
            int frame,
            float time,
            MmdRuntimeFrameEvaluation evaluation,
            bool includeMaterials)
        {
            var orderedBones = new List<MmdBoneDefinition>(model.bones);
            orderedBones.Sort((left, right) => left.index.CompareTo(right.index));

            var bones = new List<MmdEvaluatedBonePose>(orderedBones.Count);
            foreach (MmdBoneDefinition bone in orderedBones)
            {
                MmdBonePoseSample pose = evaluation.IkMotion.Bones.TryGetValue(bone.name, out MmdBonePoseSample sample)
                    ? sample
                    : MmdBonePoseSample.Identity;
                bones.Add(new MmdEvaluatedBonePose
                {
                    index = bone.index,
                    name = string.IsNullOrWhiteSpace(bone.name) ? bone.index.ToString() : bone.name,
                    localPosition = pose.Translation,
                    localRotation = pose.Rotation,
                    localScale = new[] { 1.0f, 1.0f, 1.0f },
                    worldMatrix = evaluation.WorldMatrices[bone.index]
                });
            }

            var orderedMorphs = new List<KeyValuePair<string, float>>(evaluation.IkMotion.Morphs);
            orderedMorphs.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));

            var morphs = new List<MmdEvaluatedMorphWeight>(orderedMorphs.Count);
            foreach (KeyValuePair<string, float> pair in orderedMorphs)
            {
                morphs.Add(new MmdEvaluatedMorphWeight { name = pair.Key, weight = pair.Value });
            }

            return new MmdEvaluatedFrame
            {
                frame = frame,
                time = time,
                bones = bones,
                morphs = morphs,
                materials = includeMaterials ? MmdMaterialDescriptorBuilder.Build(model).ToList() : new List<MmdMaterialDescriptor>()
            };
        }

        private static double TicksToMilliseconds(long ticks)
        {
            return ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }
    }
}
