#nullable enable

using System;
using System.Collections.Generic;
using Mmd.Motion;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Rendering;
using Mmd.Tracing;

namespace Mmd
{
    public sealed class MmdRuntimeSession
    {
        private readonly MmdModelDefinition model;
        private readonly MmdMotionDefinition motion;
        private readonly string modelId;
        private readonly string motionId;

        public MmdRuntimeSession(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            string modelId,
            string motionId)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new ArgumentException("Model identifier is required.", nameof(modelId));
            }

            if (string.IsNullOrWhiteSpace(motionId))
            {
                throw new ArgumentException("Motion identifier is required.", nameof(motionId));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            MmdMotionValidator.ThrowIfInvalid(motion);
            this.model = model;
            this.motion = motion;
            this.modelId = modelId;
            this.motionId = motionId;
        }

        public MmdTrace EvaluateTrace(int frame, float time, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            return MmdRuntimeTraceEvaluator.EvaluatePhaseOneTrace(model, motion, frame, time, modelId, motionId, physicsBackend, ikSolver);
        }

        public MmdTrace EvaluateTraceFrames(IReadOnlyList<int> frames, float frameRate, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            return MmdRuntimeTraceEvaluator.EvaluatePhaseOneTraceFrames(model, motion, frames, frameRate, modelId, motionId, physicsBackend, ikSolver);
        }

        public MmdPlaybackSnapshot BuildSnapshot(int frame, float time, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            return MmdPlaybackSnapshotBuilder.BuildPhaseOneSnapshot(model, motion, frame, time, modelId, motionId, physicsBackend, ikSolver);
        }

        public MmdPlaybackSnapshot BuildSnapshotAtTime(float time, float frameRate, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            MmdPlaybackTimeMapping mapping = DescribePlaybackTime(time, frameRate);
            return BuildSnapshot(mapping.frame, time, physicsBackend, ikSolver);
        }

        public MmdEvaluatedFrame EvaluateFrame(int frame, float time, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            return MmdRuntimeFrameEvaluator.EvaluateValidatedPhaseOnePlaybackFrame(model, motion, frame, time, physicsBackend, ikSolver);
        }

        public MmdEvaluatedFrame EvaluateFrameAtTime(float time, float frameRate, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            MmdPlaybackTimeMapping mapping = DescribePlaybackTime(time, frameRate);
            return EvaluateFrame(mapping.frame, time, physicsBackend, ikSolver);
        }

        public MmdPlaybackSnapshot BuildSnapshotFromEvaluatedFrame(MmdEvaluatedFrame frame, MmdRenderingDescriptor rendering)
        {
            return new MmdPlaybackSnapshot
            {
                model = modelId,
                motion = motionId,
                frame = frame ?? throw new ArgumentNullException(nameof(frame)),
                rendering = rendering ?? throw new ArgumentNullException(nameof(rendering))
            };
        }

        public MmdPlaybackTimeMapping DescribePlaybackTime(float time, float frameRate)
        {
            return MmdPlaybackTime.Map(time, frameRate, motion.maxFrame);
        }

        public MmdPlaybackSnapshotSummary BuildSnapshotSummary(int frame, float time, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            return MmdPlaybackSnapshotDiagnostics.Summarize(BuildSnapshot(frame, time, physicsBackend, ikSolver));
        }

        public IReadOnlyList<MmdPlaybackSnapshot> BuildSnapshots(IReadOnlyList<int> frames, float frameRate, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            return MmdPlaybackSnapshotBuilder.BuildPhaseOneSnapshots(model, motion, frames, frameRate, modelId, motionId, physicsBackend, ikSolver);
        }

        public MmdPlaybackSnapshotSequenceSummary BuildSnapshotSequenceSummary(IReadOnlyList<int> frames, float frameRate, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            return MmdPlaybackSnapshotDiagnostics.SummarizeSequence(BuildSnapshots(frames, frameRate, physicsBackend, ikSolver));
        }

        public MmdAnimationBakeSummary BuildTransformBakeSummary(
            int startFrame,
            int endFrame,
            float frameRate,
            string outputPath = "Assets/MmdUnity/BakedAnimations/animation-bake-plan.anim")
        {
            return MmdAnimationBakePlanner.BuildTransformBakeSummary(this, startFrame, endFrame, frameRate, outputPath);
        }
    }
}
