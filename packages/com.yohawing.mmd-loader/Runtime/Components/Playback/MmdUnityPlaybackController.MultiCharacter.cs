#nullable enable

using System;
using System.Collections.Generic;
using Mmd;
using Mmd.Parser;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackController
    {
        private enum PlaybackWorkerKind
        {
            None = 0,
            PhysicsOff = 1,
            Live = 2
        }

        // Timeline and standalone playback share one controller-owned slot. Keeping a single
        // session here is important: switching from Timeline to Update must either reuse the
        // exact Off worker or explicitly replace the Live worker, never leave two native sessions
        // racing over the same binding.
        private MmdMultiCharacterWorkerPool? playbackWorkerPool;
        private PlaybackWorkerKind playbackWorkerKind;
        private int playbackWorkerPoolConfigurationRevision = -1;
        private long playbackWorkerPoolFastRuntimeSourceRevision = -1;
        private int automaticWorkerDriveFrameCount = int.MinValue / 2;
        private bool standaloneWorkerFaulted;

        internal bool HasMultiCharacterHumanoidInputs =>
            proxyRoot != null && humanoidRetargetEntries != null && humanoidRetargetEntries.Count > 0;

        internal void ReleaseAutomaticWorkerForSynchronousPlayback()
        {
            ReleasePlaybackWorkerPool();
        }

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

            return TryGetOrCreatePlaybackWorkerPool(
                PlaybackWorkerKind.PhysicsOff,
                allowFastRuntimeEnable: false,
                out pool,
                out reason);
        }

        internal void ReleaseTimelineWorkerPool()
        {
            ReleasePlaybackWorkerPool();
        }

        internal long TimelineWorkerFastRuntimeSourceRevision =>
            playbackWorkerPoolFastRuntimeSourceRevision;

        internal bool IsTimelineWorkerPool(MmdMultiCharacterWorkerPool expectedPool)
        {
            return playbackWorkerKind == PlaybackWorkerKind.PhysicsOff &&
                ReferenceEquals(playbackWorkerPool, expectedPool);
        }

        internal bool TryGetOrCreateStandaloneWorkerPool(
            out MmdMultiCharacterWorkerPool pool,
            out string reason)
        {
            pool = null!;
            if (standaloneWorkerFaulted)
            {
                reason = "Standalone worker is faulted; change the source/configuration or re-enable the controller to retry.";
                return false;
            }

            if (!TryValidateStandaloneWorkerConfiguration(out reason))
            {
                return false;
            }

            return TryGetOrCreatePlaybackWorkerPool(
                PhysicsMode == Mmd.Physics.MmdPhysicsMode.Live
                    ? PlaybackWorkerKind.Live
                    : PlaybackWorkerKind.PhysicsOff,
                allowFastRuntimeEnable: true,
                out pool,
                out reason);
        }

        internal void ReleaseStandaloneWorkerPool()
        {
            ReleasePlaybackWorkerPool();
        }

        internal void ReleasePlaybackWorkerPoolForTimelineSync()
        {
            // Timeline animation-only evaluation in Physics Off is compatible with the same
            // prepared native session. Preserve it so playback can return to standalone without
            // rebuilding a worker. Live Timeline evaluation mutates the binding directly and must
            // invalidate the worker slot first.
            if (PhysicsMode == Mmd.Physics.MmdPhysicsMode.Live)
            {
                ReleasePlaybackWorkerPool();
            }
        }

        private void ReleasePlaybackWorkerPool()
        {
            MmdMultiCharacterWorkerPool? pool = playbackWorkerPool;
            playbackWorkerPool = null;
            playbackWorkerKind = PlaybackWorkerKind.None;
            playbackWorkerPoolConfigurationRevision = -1;
            playbackWorkerPoolFastRuntimeSourceRevision = -1;
            if (pool == null)
            {
                return;
            }

            try
            {
                pool.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogError("Standalone worker cleanup failed: " + exception, this);
            }
        }

        private bool TryGetOrCreatePlaybackWorkerPool(
            PlaybackWorkerKind requestedKind,
            bool allowFastRuntimeEnable,
            out MmdMultiCharacterWorkerPool pool,
            out string reason)
        {
            pool = null!;
            // The worker is controller-owned and all source/configuration mutation entrypoints
            // release it. Reuse the matching live slot before acquiring provider bytes so the
            // steady-state playback path does not clone and hash the full PMX/VMD every frame.
            if (playbackWorkerPool != null &&
                playbackWorkerKind == requestedKind &&
                playbackWorkerPoolConfigurationRevision == ConfigurationRevision &&
                binding != null &&
                binding.IsFastRuntimeEnabled &&
                playbackWorkerPoolFastRuntimeSourceRevision == binding.FastRuntimeSourceRevision)
            {
                pool = playbackWorkerPool;
                reason = string.Empty;
                return true;
            }

            if (!TryGetMultiCharacterSource(out byte[] pmxBytes, out byte[] vmdBytes, out reason))
            {
                return false;
            }

            if (!IsFastRuntimeEnabled && allowFastRuntimeEnable)
            {
                try
                {
                    if (binding == null || !binding.TryEnableFastRuntime(pmxBytes, vmdBytes, out reason))
                    {
                        if (string.IsNullOrEmpty(reason))
                        {
                            reason = "Native fast runtime could not be enabled for automatic worker playback.";
                        }

                        return false;
                    }
                }
                catch (Exception exception)
                {
                    reason = exception.GetType().Name + ": " + exception.Message;
                    return false;
                }
            }

            if (!IsFastRuntimeEnabled)
            {
                reason = "Native fast runtime is not enabled for worker playback.";
                return false;
            }

            string sourceReason = string.Empty;
            if (binding == null || !binding.TryMatchFastRuntimeSources(pmxBytes, vmdBytes, out sourceReason))
            {
                reason = string.IsNullOrEmpty(sourceReason)
                    ? "Worker source does not match the active fast-runtime binding source."
                    : "Worker source does not match the active fast-runtime binding source: " + sourceReason;
                return false;
            }

            long sourceRevision = binding.FastRuntimeSourceRevision;
            ReleasePlaybackWorkerPool();
            try
            {
                MmdMultiCharacterWorkerPool.IEvaluator evaluator;
                if (requestedKind == PlaybackWorkerKind.Live)
                {
                    if (MultiCharacterModelDefinition == null || IkMaxIterationsCap != 0 ||
                        !TryPrepareMultiCharacterLivePhysicsWorker(out reason))
                    {
                        reason = string.IsNullOrEmpty(reason)
                            ? "Live worker requires a managed model and IK iteration cap 0."
                            : reason;
                        return false;
                    }

                    evaluator = new MmdNativeLivePhysicsMultiCharacterWorker(
                        pmxBytes,
                        vmdBytes,
                        MultiCharacterModelDefinition,
                        ikMaxIterationsCap: 0,
                        ikCompatibilityProfile: ikCompatibilityProfile);
                }
                else
                {
                    evaluator = new MmdNativeMultiCharacterWorker(
                        pmxBytes,
                        vmdBytes,
                        (uint)IkMaxIterationsCap,
                        ikCompatibilityProfile);
                }

                playbackWorkerPool = new MmdMultiCharacterWorkerPool(new[] { evaluator });
                playbackWorkerKind = requestedKind;
                playbackWorkerPoolConfigurationRevision = ConfigurationRevision;
                playbackWorkerPoolFastRuntimeSourceRevision = sourceRevision;
                pool = playbackWorkerPool;
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = exception.GetType().Name + ": " + exception.Message;
                ReleasePlaybackWorkerPool();
                return false;
            }
        }

        internal bool TryPrepareStandaloneWorkerEvaluation(
            float deltaTime,
            out MmdMultiCharacterWorkerPool pool,
            out MmdMultiCharacterWorkerRequest request,
            out MmdMultiCharacterClockState previousClock,
            out string reason)
        {
            pool = null!;
            request = default;
            previousClock = default;
            reason = string.Empty;
            if (!IsPlaying ||
                MmdUnityPlaybackController.ShouldSuppressSelfTick(
                    lastTimelineDriveFrameCount,
                    UnityEngine.Time.frameCount))
            {
                return false;
            }

            if (!TryGetOrCreateStandaloneWorkerPool(out pool, out reason))
            {
                return false;
            }

            try
            {
                previousClock = AdvanceMultiCharacterClock(deltaTime);
                // Keep the accumulated fractional position through apply. The worker evaluates
                // the integer frame, while sourceTime owns the standalone playback clock; reducing
                // it back to CurrentFrame would stall 30 fps playback on a 60 Hz PlayerLoop.
                float time = playbackFrame / frameRate;
                request = new MmdMultiCharacterWorkerRequest(CurrentFrame, time, frameRate);
                return true;
            }
            catch (Exception exception)
            {
                RestoreMultiCharacterClock(previousClock);
                reason = exception.GetType().Name + ": " + exception.Message;
                ReleaseStandaloneWorkerPool();
                return false;
            }
        }

        internal bool TryValidateStandaloneWorkerApply(
            MmdMultiCharacterWorkerRequest request,
            MmdMultiCharacterWorkerPool expectedPool,
            MmdMultiCharacterWorkerResult result,
            out string reason)
        {
            if (!isActiveAndEnabled || standaloneWorkerFaulted)
            {
                reason = "Standalone worker result target is disabled or faulted.";
                return false;
            }

            if (ConfigurationRevision != playbackWorkerPoolConfigurationRevision)
            {
                reason = "Standalone worker result belongs to an older controller configuration.";
                return false;
            }

            if (!ReferenceEquals(playbackWorkerPool, expectedPool))
            {
                reason = "Standalone worker result belongs to an older worker session.";
                return false;
            }

            if (binding == null || binding.FastRuntimeSourceRevision != playbackWorkerPoolFastRuntimeSourceRevision)
            {
                reason = "Standalone worker result belongs to an older fast-runtime source revision.";
                return false;
            }

            if (request.Frame != CurrentFrame || request.FrameRate != frameRate)
            {
                reason = "Standalone worker result does not match the current controller clock.";
                return false;
            }

            return TryValidateMultiCharacterPreparedApply(result, out reason);
        }

        internal void HandleStandaloneWorkerFailure(
            MmdMultiCharacterClockState previousClock,
            string reason)
        {
            RestoreMultiCharacterClock(previousClock);
            HandleStandaloneWorkerPreparationFailure(reason);
        }

        internal void HandleStandaloneWorkerPreparationFailure(string reason)
        {
            standaloneWorkerFaulted = true;
            ReleaseStandaloneWorkerPool();
            Debug.LogWarning(
                "Standalone native worker playback faulted and was disabled for this controller: " + reason,
                this);
        }

        internal void MarkStandaloneWorkerDriven()
        {
            automaticWorkerDriveFrameCount = UnityEngine.Time.frameCount;
        }

        internal bool WasStandaloneWorkerDrivenThisFrame =>
            automaticWorkerDriveFrameCount == UnityEngine.Time.frameCount;

        internal bool IsStandaloneWorkerFaulted => standaloneWorkerFaulted;

        internal MmdModelDefinition? MultiCharacterModelDefinition => binding?.ManagedModelDefinition;

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

        internal bool TryGetMultiCharacterSource(
            out byte[] pmxBytes,
            out byte[] vmdBytes,
            out string reason)
        {
            pmxBytes = Array.Empty<byte>();
            vmdBytes = Array.Empty<byte>();
            if (!IsConfigured)
            {
                reason = "Playback controller must be configured before automatic worker playback.";
                return false;
            }

            if (PhysicsMode != Mmd.Physics.MmdPhysicsMode.Off && PhysicsMode != Mmd.Physics.MmdPhysicsMode.Live)
            {
                reason = "Automatic worker playback requires Physics Mode Off or Live.";
                return false;
            }

            MmdPmxAsset? model = ModelAssetSource;
            MmdVmdAsset? motion = MotionAssetSource;
            if (model == null || motion == null)
            {
                reason = "Automatic worker playback requires controller-owned PMX and VMD asset sources.";
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

        internal bool TryPrepareMultiCharacterLivePhysicsWorker(out string reason)
        {
            if (PhysicsMode != Mmd.Physics.MmdPhysicsMode.Live || binding == null ||
                binding.NativeHumanoidHostPoseEnabled)
            {
                reason = "Automatic Live worker playback requires a configured non-Humanoid Live binding.";
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
            long expectedFastRuntimeSourceRevision,
            MmdMultiCharacterWorkerPool expectedPool,
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

            if (!IsTimelineWorkerPool(expectedPool))
            {
                reason = "Timeline worker result belongs to an older worker session.";
                return false;
            }

            if (TimelineWorkerFastRuntimeSourceRevision != expectedFastRuntimeSourceRevision)
            {
                reason = "Timeline worker result belongs to an older fast-runtime source revision.";
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
            if (!TryValidateStandaloneWorkerConfiguration(out reason))
            {
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

        private bool TryValidateStandaloneWorkerConfiguration(out string reason)
        {
            if (HasMultiCharacterHumanoidInputs)
            {
                reason = "Humanoid retarget input is not supported by automatic worker playback.";
                return false;
            }

            if (!IsConfigured)
            {
                reason = "Playback controller is not configured.";
                return false;
            }

            if (PhysicsMode != Mmd.Physics.MmdPhysicsMode.Off &&
                PhysicsMode != Mmd.Physics.MmdPhysicsMode.Live)
            {
                reason = "Automatic worker playback requires Physics Mode Off or Live.";
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
