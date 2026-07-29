#nullable enable

using System;
using System.Collections.Generic;
using Mmd.Motion;
using Mmd.Native;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Pose;
using Mmd.Rendering;
using Mmd.Tracing;

namespace Mmd
{
    public sealed class MmdRuntimeSession : IDisposable
    {
        private readonly MmdModelDefinition model;
        private readonly MmdMotionDefinition motion;
        private readonly string modelId;
        private readonly string motionId;
        private readonly byte[]? nativeModelSourceIdentity;
        private readonly byte[]? nativeMotionSourceIdentity;
        private readonly int nativeModelSourceLength;
        private readonly int nativeMotionSourceLength;
        private readonly ulong nativeModelSourceFingerprint;
        private readonly ulong nativeMotionSourceFingerprint;
        private MmdTopologyPlan? topologyPlan;
        private MmdRuntimeFfiPlaybackSession? nativePlaybackSession;
        private float[]? nativeWorldMatrices;
        private float[]? nativeMorphWeights;
        private byte[]? nativeIkEnabled;
        private bool disposed;

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
            nativeModelSourceIdentity = model.sourceBytes;
            nativeMotionSourceIdentity = motion.sourceBytes;
            nativeModelSourceLength = nativeModelSourceIdentity?.Length ?? 0;
            nativeMotionSourceLength = nativeMotionSourceIdentity?.Length ?? 0;
            nativeModelSourceFingerprint = ComputeSourceFingerprint(nativeModelSourceIdentity);
            nativeMotionSourceFingerprint = ComputeSourceFingerprint(nativeMotionSourceIdentity);
        }

        public int MotionMaxFrame => motion.maxFrame;

        ~MmdRuntimeSession()
        {
            Dispose(disposing: false);
        }

        internal byte[]? MotionSourceBytes => motion.sourceBytes;

        internal MmdTopologyPlan TopologyPlan
        {
            get
            {
                ThrowIfDisposed();
                return topologyPlan ??= MmdTopologyPlan.CreateFromValidatedModel(model);
            }
        }

        public MmdTrace EvaluateTrace(int frame, float time, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            ThrowIfDisposed();
            return MmdRuntimeTraceEvaluator.EvaluatePhaseOneTrace(model, motion, frame, time, modelId, motionId, physicsBackend, ikSolver);
        }

        public MmdTrace EvaluateTraceFrames(IReadOnlyList<int> frames, float frameRate, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            ThrowIfDisposed();
            return MmdRuntimeTraceEvaluator.EvaluatePhaseOneTraceFrames(model, motion, frames, frameRate, modelId, motionId, physicsBackend, ikSolver);
        }

        public MmdPlaybackSnapshot BuildSnapshot(int frame, float time, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            ThrowIfDisposed();
            return MmdPlaybackSnapshotBuilder.BuildPhaseOneSnapshot(model, motion, frame, time, modelId, motionId, physicsBackend, ikSolver);
        }

        public MmdPlaybackSnapshot BuildSnapshotAtTime(float time, float frameRate, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            MmdPlaybackTimeMapping mapping = DescribePlaybackTime(time, frameRate);
            return BuildSnapshot(mapping.frame, time, physicsBackend, ikSolver);
        }

        public MmdEvaluatedFrame EvaluateFrame(int frame, float time, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            ThrowIfDisposed();
            return ikSolver != null
                ? MmdRuntimeFrameEvaluator.EvaluateValidatedPhaseOnePlaybackFrame(model, motion, frame, time, TopologyPlan, physicsBackend, ikSolver)
                : EvaluateNativeFrame(frame, time);
        }

        internal MmdEvaluatedFrame EvaluateBeforePhysicsFrame(int frame, float time, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            ThrowIfDisposed();
            return ikSolver != null
                ? MmdRuntimeFrameEvaluator.EvaluateValidatedBeforePhysicsPlaybackFrame(model, motion, frame, time, TopologyPlan, physicsBackend, ikSolver)
                : EvaluateNativeFrame(frame, time);
        }

        public MmdEvaluatedFrame EvaluateFrameAtTime(float time, float frameRate, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            MmdPlaybackTimeMapping mapping = DescribePlaybackTime(time, frameRate);
            return EvaluateFrame(mapping.frame, time, physicsBackend, ikSolver);
        }

        public MmdPlaybackSnapshot BuildSnapshotFromEvaluatedFrame(MmdEvaluatedFrame frame, MmdRenderingDescriptor rendering)
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            return MmdPlaybackTime.Map(time, frameRate, motion.maxFrame);
        }

        public MmdPlaybackSnapshotSummary BuildSnapshotSummary(int frame, float time, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            return MmdPlaybackSnapshotDiagnostics.Summarize(BuildSnapshot(frame, time, physicsBackend, ikSolver));
        }

        public IReadOnlyList<MmdPlaybackSnapshot> BuildSnapshots(IReadOnlyList<int> frames, float frameRate, IMmdPhysicsBackend? physicsBackend = null, IMmdIkSolver? ikSolver = null)
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            return MmdAnimationBakePlanner.BuildTransformBakeSummary(this, startFrame, endFrame, frameRate, outputPath);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                nativePlaybackSession?.Dispose();
            }
            finally
            {
                nativePlaybackSession = null;
                nativeWorldMatrices = null;
                nativeMorphWeights = null;
                nativeIkEnabled = null;
            }
        }

        private MmdEvaluatedFrame EvaluateNativeFrame(int frame, float time)
        {
            MmdPlaybackTime.ValidateFrame(frame);
            MmdPlaybackTime.ValidateTime(time);
            EnsureNativeSourcesUnchanged();
            EnsureNativePlaybackSession();

            nativePlaybackSession!.EvaluateAndCopy(
                frame,
                nativeWorldMatrices!,
                nativeMorphWeights!,
                nativeIkEnabled!);
            return MmdRuntimeFrameEvaluator.BuildFrameFromNative(
                model,
                frame,
                time,
                nativeWorldMatrices!,
                nativeMorphWeights!,
                includeMaterials: false);
        }

        private void EnsureNativePlaybackSession()
        {
            if (nativePlaybackSession != null)
            {
                return;
            }

            byte[] modelSourceIdentity = nativeModelSourceIdentity
                ?? throw new InvalidOperationException("Model source bytes are required for native runtime evaluation.");
            byte[] motionSourceIdentity = nativeMotionSourceIdentity
                ?? throw new InvalidOperationException("Motion source bytes are required for native runtime evaluation.");
            if (ComputeSourceFingerprint(nativeModelSourceIdentity) != nativeModelSourceFingerprint
                || ComputeSourceFingerprint(nativeMotionSourceIdentity) != nativeMotionSourceFingerprint)
            {
                throw new InvalidOperationException("Native runtime source bytes changed before session compilation.");
            }

            byte[] modelSource = (byte[])modelSourceIdentity.Clone();
            byte[] motionSource = (byte[])motionSourceIdentity.Clone();
            MmdRuntimeFfiPlaybackSession created = MmdRuntimeFfiPlaybackSession.Create(modelSource, motionSource);
            try
            {
                nativeWorldMatrices = new float[created.WorldMatrixFloatCount];
                nativeMorphWeights = new float[created.MorphWeightCount];
                nativeIkEnabled = new byte[created.IkEnabledCount];
                nativePlaybackSession = created;
            }
            catch
            {
                created.Dispose();
                nativeWorldMatrices = null;
                nativeMorphWeights = null;
                nativeIkEnabled = null;
                throw;
            }
        }

        private void EnsureNativeSourcesUnchanged()
        {
            if (!ReferenceEquals(model.sourceBytes, nativeModelSourceIdentity)
                || !ReferenceEquals(motion.sourceBytes, nativeMotionSourceIdentity)
                || (model.sourceBytes?.Length ?? 0) != nativeModelSourceLength
                || (motion.sourceBytes?.Length ?? 0) != nativeMotionSourceLength
                || ComputeSourceFingerprint(nativeModelSourceIdentity) != nativeModelSourceFingerprint
                || ComputeSourceFingerprint(nativeMotionSourceIdentity) != nativeMotionSourceFingerprint)
            {
                throw new InvalidOperationException("Native runtime session source identity changed after construction.");
            }
        }

        private static ulong ComputeSourceFingerprint(byte[]? source)
        {
            if (source == null)
            {
                return 0;
            }

            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            foreach (byte value in source)
            {
                hash = unchecked((hash ^ value) * prime);
            }

            return hash;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeSession));
            }
        }

    }
}
