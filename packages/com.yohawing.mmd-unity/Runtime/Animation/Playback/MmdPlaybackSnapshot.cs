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
    public sealed class MmdPlaybackSnapshot
    {
        public string model = string.Empty;
        public string motion = string.Empty;
        public MmdEvaluatedFrame frame = new();
        public MmdRenderingDescriptor rendering = new();
    }

    public static class MmdPlaybackSnapshotBuilder
    {
        public static MmdPlaybackSnapshot BuildPhaseOneSnapshot(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            float time,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            return BuildPhaseOneSnapshot(model, motion, frame, time, string.Empty, string.Empty, physicsBackend, ikSolver);
        }

        public static MmdPlaybackSnapshot BuildPhaseOneSnapshot(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            float time,
            string modelId,
            string motionId,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            return new MmdPlaybackSnapshot
            {
                model = modelId ?? throw new ArgumentNullException(nameof(modelId)),
                motion = motionId ?? throw new ArgumentNullException(nameof(motionId)),
                frame = MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrame(model, motion, frame, time, physicsBackend, ikSolver),
                rendering = MmdRenderingDescriptorBuilder.Build(model)
            };
        }

        public static IReadOnlyList<MmdPlaybackSnapshot> BuildPhaseOneSnapshots(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            IReadOnlyList<int> frames,
            float frameRate,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            return BuildPhaseOneSnapshots(model, motion, frames, frameRate, string.Empty, string.Empty, physicsBackend, ikSolver);
        }

        public static IReadOnlyList<MmdPlaybackSnapshot> BuildPhaseOneSnapshots(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            IReadOnlyList<int> frames,
            float frameRate,
            string modelId,
            string motionId,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            if (modelId == null)
            {
                throw new ArgumentNullException(nameof(modelId));
            }

            if (motionId == null)
            {
                throw new ArgumentNullException(nameof(motionId));
            }

            IReadOnlyList<MmdEvaluatedFrame> evaluatedFrames = MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrames(
                model,
                motion,
                frames,
                frameRate,
                physicsBackend,
                ikSolver);

            return evaluatedFrames
                .Select(evaluatedFrame => new MmdPlaybackSnapshot
                {
                    model = modelId,
                    motion = motionId,
                    frame = evaluatedFrame,
                    rendering = MmdRenderingDescriptorBuilder.Build(model)
                })
                .ToList();
        }
    }
}
