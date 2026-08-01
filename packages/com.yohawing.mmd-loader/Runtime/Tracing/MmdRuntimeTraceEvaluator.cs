#nullable enable

using System.Collections.Generic;
using System;
using System.Linq;
using Mmd.Motion;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Tracing;

namespace Mmd
{
    public static class MmdRuntimeTraceEvaluator
    {
        public static MmdTrace EvaluatePhaseOneTrace(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            float time,
            string modelId,
            string motionId,
            IMmdPhysicsBackend? physicsBackend = null,
            IMmdIkSolver? ikSolver = null)
        {
            ValidateInputs(model, motion, modelId, motionId);
            if (frame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Frame index must not be negative.");
            }

            if (time < 0.0f || float.IsNaN(time) || float.IsInfinity(time))
            {
                throw new ArgumentOutOfRangeException(nameof(time), "Time must be a non-negative finite value.");
            }

            ThrowIfNativeBackedTraceUnsupported(model, motion);
            physicsBackend ??= new NullMmdPhysicsBackend();
            physicsBackend.Reset();
            MmdTrace trace = MmdActualTraceDumper.CreateTrace(modelId, motionId);
            AppendPhaseOneFrame(trace, model, motion, frame, time, physicsBackend, ikSolver);
            return trace;
        }

        public static MmdTrace EvaluatePhaseOneTraceFrames(
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

            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            if (frames.Count == 0)
            {
                throw new ArgumentException("At least one frame is required.", nameof(frames));
            }

            if (frameRate <= 0.0f || float.IsNaN(frameRate) || float.IsInfinity(frameRate))
            {
                throw new ArgumentOutOfRangeException(nameof(frameRate), "Frame rate must be a finite positive value.");
            }

            ValidateInputs(model, motion, modelId, motionId);
            ThrowIfNativeBackedTraceUnsupported(model, motion);
            physicsBackend ??= new NullMmdPhysicsBackend();
            physicsBackend.Reset();

            MmdTrace trace = MmdActualTraceDumper.CreateTrace(modelId, motionId);
            var seenFrames = new HashSet<int>(frames.Count);

            foreach (int frame in frames.OrderBy(value => value))
            {
                if (frame < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(frames), "Frame indices must not be negative.");
                }

                if (!seenFrames.Add(frame))
                {
                    throw new ArgumentException("Frame indices must be unique.", nameof(frames));
                }

                AppendPhaseOneFrame(trace, model, motion, frame, frame / frameRate, physicsBackend, ikSolver);
            }

            return trace;
        }

        private static void ValidateInputs(
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
        }

        private static void ThrowIfNativeBackedTraceUnsupported(
            MmdModelDefinition model,
            MmdMotionDefinition motion)
        {
            if (MmdRuntimeFramePipeline.IsSourceLess(model, motion))
            {
                return;
            }

            throw new NotSupportedException(
                "Native-backed VMD trace checkpoints are unsupported: source-backed or mixed-source " +
                "model/motion inputs cannot use the managed checkpoint sequence. " +
                "mmd-anim exposes final pose/batch evaluation but not the managed " +
                "motion/append/IK/morph checkpoint sequence; source-less definitions are required.");
        }

        private static void AppendPhaseOneFrame(
            MmdTrace trace,
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            int frame,
            float time,
            IMmdPhysicsBackend physicsBackend,
            IMmdIkSolver? ikSolver)
        {
            MmdRuntimeFrameEvaluation evaluation = MmdRuntimeFramePipeline.EvaluateWithOptions(
                model, motion, frame, physicsBackend, ikSolver, captureCheckpoints: true);
            trace.frames.Add(MmdTraceBuilder.BuildMotionSamplingFrame(model, evaluation.SampledMotion, evaluation.SampledWorldMatrices, frame, time));

            trace.frames.Add(MmdTraceBuilder.BuildAppendTransformFrame(model, evaluation.AppendedMotion, evaluation.AppendedWorldMatrices, frame, time));

            trace.frames.Add(MmdTraceBuilder.BuildIkFrame(model, evaluation.IkMotion, evaluation.IkWorldMatrices, frame, time));

            trace.frames.Add(MmdTraceBuilder.BuildMorphEvaluationFrame(model, evaluation.IkMotion, evaluation.IkWorldMatrices, frame, time));

            trace.frames.Add(MmdTraceBuilder.BuildFinalWorldFrame(model, evaluation.FinalMotion, evaluation.WorldMatrices, frame, time));
        }
    }
}
