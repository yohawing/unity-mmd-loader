#nullable enable

using System;
using System.Collections.Generic;
using Mmd;
using Mmd.Parser;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackController
    {
        private MmdMultiCharacterPlaybackGroup? multiCharacterGroup;
        private bool multiCharacterClaimed;
        private MmdMultiCharacterWorkerPool? timelineWorkerPool;
        private int timelineWorkerPoolConfigurationRevision = -1;
        private long timelineWorkerPoolFastRuntimeSourceRevision = -1;

        internal bool IsMultiCharacterClaimed => multiCharacterClaimed;

        internal bool HasMultiCharacterHumanoidInputs =>
            proxyRoot != null && humanoidRetargetEntries != null && humanoidRetargetEntries.Count > 0;

        internal MmdMultiCharacterPlaybackGroup? MultiCharacterGroup => multiCharacterGroup;

        internal bool TryGetOrCreateTimelineWorkerPool(
            out MmdMultiCharacterWorkerPool pool,
            out string reason)
        {
            pool = null!;
            if (PhysicsMode != Mmd.Physics.MmdPhysicsMode.Off)
            {
                reason = "Timeline worker playback requires Physics Mode Off.";
                return false;
            }

            if (!TryValidateMultiCharacterState(out reason))
            {
                return false;
            }

            if (timelineWorkerPool != null &&
                timelineWorkerPoolConfigurationRevision == ConfigurationRevision &&
                binding != null &&
                timelineWorkerPoolFastRuntimeSourceRevision == binding.FastRuntimeSourceRevision)
            {
                pool = timelineWorkerPool;
                return true;
            }

            ReleaseTimelineWorkerPool();
            if (!TryGetMultiCharacterSource(out byte[] pmxBytes, out byte[] vmdBytes, out reason))
            {
                return false;
            }

            string sourceReason = string.Empty;
            if (binding == null || !binding.TryMatchFastRuntimeSources(pmxBytes, vmdBytes, out sourceReason))
            {
                reason = string.IsNullOrEmpty(sourceReason)
                    ? "Timeline worker source does not match the active fast-runtime binding source."
                    : "Timeline worker source does not match the active fast-runtime binding source: " + sourceReason;
                return false;
            }

            try
            {
                var evaluators = new List<MmdMultiCharacterWorkerPool.IEvaluator>(1)
                {
                    new MmdNativeMultiCharacterWorker(
                        pmxBytes,
                        vmdBytes,
                        (uint)ikMaxIterationsCap)
                };
                timelineWorkerPool = new MmdMultiCharacterWorkerPool(evaluators);
                timelineWorkerPoolConfigurationRevision = ConfigurationRevision;
                timelineWorkerPoolFastRuntimeSourceRevision = binding.FastRuntimeSourceRevision;
                pool = timelineWorkerPool;
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = exception.GetType().Name + ": " + exception.Message;
                ReleaseTimelineWorkerPool();
                return false;
            }
        }

        internal void ReleaseTimelineWorkerPool()
        {
            MmdMultiCharacterWorkerPool? pool = timelineWorkerPool;
            timelineWorkerPool = null;
            timelineWorkerPoolConfigurationRevision = -1;
            timelineWorkerPoolFastRuntimeSourceRevision = -1;
            pool?.Dispose();
        }

        internal MmdModelDefinition? MultiCharacterModelDefinition => binding?.ManagedModelDefinition;

        internal bool TryClaimMultiCharacterGroup(
            MmdMultiCharacterPlaybackGroup group,
            out string reason)
        {
            if (multiCharacterGroup != null && multiCharacterGroup != group)
            {
                reason = "Playback controller is already claimed by another group.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        internal void AssignMultiCharacterGroup(MmdMultiCharacterPlaybackGroup group)
        {
            multiCharacterGroup = group ?? throw new ArgumentNullException(nameof(group));
            multiCharacterClaimed = true;
        }

        internal void ReleaseMultiCharacterGroup(MmdMultiCharacterPlaybackGroup group)
        {
            if (multiCharacterGroup != group)
            {
                return;
            }

            multiCharacterClaimed = false;
            multiCharacterGroup = null;
        }

        internal readonly struct MmdMultiCharacterClockState
        {
            internal MmdMultiCharacterClockState(float playbackFrame, int currentFrame)
            {
                this.playbackFrame = playbackFrame;
                this.currentFrame = currentFrame;
            }

            internal readonly float playbackFrame;
            internal readonly int currentFrame;
        }

        internal void ThrowIfMultiCharacterPoolOwnsController(string operation)
        {
            if (multiCharacterClaimed && multiCharacterGroup?.HasWorkerPool == true &&
                operation != nameof(Play) && operation != nameof(Pause))
            {
                throw new InvalidOperationException(
                    operation + " is not supported while a multi-character playback group owns this controller.");
            }
        }

        internal MmdMultiCharacterClockState AdvanceMultiCharacterClock(float deltaTime)
        {
            if (deltaTime < 0.0f || !float.IsFinite(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            MmdPlaybackTime.ValidateFrameRate(frameRate);

            var previous = new MmdMultiCharacterClockState(playbackFrame, CurrentFrame);
            playbackFrame += deltaTime * frameRate;
            // playbackFrame is already expressed in frame-position units. Converting it to
            // seconds and back introduces enough float error at 30 fps to skip a frame after
            // roughly two seconds of multi-character playback.
            MmdPlaybackTime.ValidateTime(playbackFrame);
            CurrentFrame = (int)MathF.Floor(playbackFrame);
            return previous;
        }

        internal void RestoreMultiCharacterClock(MmdMultiCharacterClockState state)
        {
            playbackFrame = state.playbackFrame;
            CurrentFrame = state.currentFrame;
        }

        internal bool IsMultiCharacterTimelineDriven =>
            MmdUnityPlaybackController.ShouldSuppressSelfTick(
                lastTimelineDriveFrameCount,
                UnityEngine.Time.frameCount);

        internal bool TryGetMultiCharacterSource(
            out byte[] pmxBytes,
            out byte[] vmdBytes,
            out string reason)
        {
            pmxBytes = Array.Empty<byte>();
            vmdBytes = Array.Empty<byte>();
            if (!IsConfigured)
            {
                reason = "Playback controller must be configured before joining a multi-character group.";
                return false;
            }

            if (PhysicsMode != Mmd.Physics.MmdPhysicsMode.Off && PhysicsMode != Mmd.Physics.MmdPhysicsMode.Live)
            {
                reason = "Multi-character playback requires Physics Mode Off or Live.";
                return false;
            }

            if (!IsFastRuntimeEnabled)
            {
                reason = "Multi-character playback requires an enabled native fast runtime binding.";
                return false;
            }

            MmdPmxAsset? model = ModelAssetSource;
            MmdVmdAsset? motion = MotionAssetSource;
            if (model == null || motion == null)
            {
                reason = "Multi-character playback requires controller-owned PMX and VMD asset sources.";
                return false;
            }

            try
            {
                pmxBytes = model.GetBytesCopy();
                vmdBytes = motion.GetBytesCopy();
                if (pmxBytes.Length == 0 || vmdBytes.Length == 0)
                {
                    reason = "Configured PMX/VMD asset source bytes are empty.";
                    pmxBytes = Array.Empty<byte>();
                    vmdBytes = Array.Empty<byte>();
                    return false;
                }

                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                pmxBytes = Array.Empty<byte>();
                vmdBytes = Array.Empty<byte>();
                reason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Recreates the managed binding's native session from the exact bytes that will be
        /// copied into the worker-owned evaluator. This closes the provenance gap between the
        /// configured asset source and the public arbitrary-byte fast-runtime entry point.
        /// </summary>
        internal bool TryPrepareMultiCharacterSource(
            byte[] pmxBytes,
            byte[] vmdBytes,
            out string reason)
        {
            if (!TryValidateMultiCharacterState(out reason))
            {
                return false;
            }

            if (binding == null)
            {
                reason = "Playback controller must be configured before preparing multi-character playback.";
                return false;
            }

            try
            {
                return binding.TryEnableFastRuntime(pmxBytes, vmdBytes, out reason);
            }
            catch (Exception exception)
            {
                reason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        internal bool TryPrepareMultiCharacterLivePhysicsWorker(out string reason)
        {
            if (PhysicsMode != Mmd.Physics.MmdPhysicsMode.Live || binding == null ||
                binding.NativeHumanoidHostPoseEnabled)
            {
                reason = "Multi-character Live physics requires a configured non-Humanoid Live binding.";
                return false;
            }

            binding.ResetLivePhysicsForDriveSource();
            reason = string.Empty;
            return true;
        }

        internal bool TryValidateMultiCharacterPreparedApply(
            MmdMultiCharacterWorkerResult result,
            out string reason)
        {
            if (result == null)
            {
                reason = "Prepared multi-character worker result is missing.";
                return false;
            }

            if (!TryValidateMultiCharacterState(out reason))
            {
                return false;
            }

            if (PhysicsMode == Mmd.Physics.MmdPhysicsMode.Live)
            {
                return binding!.TryValidatePreparedMultiCharacterLiveFrame(CurrentFrame, frameRate, result, out reason);
            }

            if (!binding!.TryValidatePreparedFastFrame(result.WorldMatrices, result.MorphWeights, out reason))
            {
                return false;
            }

            reason = string.Empty;
            return true;
        }

        internal bool TryValidateTimelineWorkerApply(
            int frame,
            float sourceTime,
            float frameRate,
            int expectedConfigurationRevision,
            MmdMultiCharacterWorkerResult result,
            out string reason)
        {
            if (!isActiveAndEnabled)
            {
                reason = "Timeline worker result target is disabled.";
                return false;
            }

            if (ConfigurationRevision != expectedConfigurationRevision)
            {
                reason = "Timeline worker result belongs to an older controller configuration.";
                return false;
            }

            if (PhysicsMode != Mmd.Physics.MmdPhysicsMode.Off)
            {
                reason = "Timeline worker result requires Physics Mode Off.";
                return false;
            }

            try
            {
                MmdPlaybackTime.ValidateFrame(frame);
                MmdPlaybackTime.ValidateTime(sourceTime);
                MmdPlaybackTime.ValidateFrameRate(frameRate);
            }
            catch (Exception exception)
            {
                reason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }

            return TryValidateMultiCharacterPreparedApply(result, out reason);
        }

        internal bool TryValidateMultiCharacterState(out string reason)
        {
            if (HasMultiCharacterHumanoidInputs)
            {
                reason = "Humanoid retarget input is not supported by multi-character playback.";
                return false;
            }

            if (!IsConfigured)
            {
                reason = "Playback controller is not configured.";
                return false;
            }

            if (PhysicsMode != Mmd.Physics.MmdPhysicsMode.Off && PhysicsMode != Mmd.Physics.MmdPhysicsMode.Live)
            {
                reason = "Multi-character playback requires Physics Mode Off or Live.";
                return false;
            }

            if (!IsFastRuntimeEnabled)
            {
                reason = "Native fast runtime is no longer enabled on the controller.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        internal MmdPlaybackSnapshot ApplyPreparedMultiCharacterFrame(
            int frame,
            float frameRate,
            MmdMultiCharacterWorkerResult result)
        {
            return ApplyPreparedMultiCharacterFrame(
                frame,
                MmdPlaybackTime.ToTime(frame, frameRate),
                frameRate,
                result);
        }

        internal MmdPlaybackSnapshot ApplyPreparedMultiCharacterFrame(
            int frame,
            float sourceTime,
            float frameRate,
            MmdMultiCharacterWorkerResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (binding == null)
            {
                throw new InvalidOperationException(
                    "Playback controller must be configured before applying a prepared frame.");
            }

            MmdPlaybackTime.ValidateFrame(frame);
            MmdPlaybackTime.ValidateTime(sourceTime);
            MmdPlaybackTime.ValidateFrameRate(frameRate);
            playbackFrame = sourceTime * frameRate;
            CurrentFrame = frame;
            bool poseWillBeApplied = PhysicsMode != Mmd.Physics.MmdPhysicsMode.Live ||
                !binding.CanReuseLivePhysicsSeed(frame);
            return ApplyPlaybackPose(() =>
            {
                LastSnapshot = PhysicsMode == Mmd.Physics.MmdPhysicsMode.Live
                    ? binding.ApplyPreparedMultiCharacterLiveFrame(frame, frameRate, result)
                    : binding.ApplyPreparedFastFrame(
                        frame,
                        frameRate,
                        sourceTime,
                        result.WorldMatrices,
                        result.MorphWeights);
                return LastSnapshot;
            }, poseWillBeApplied);
        }
    }
}
