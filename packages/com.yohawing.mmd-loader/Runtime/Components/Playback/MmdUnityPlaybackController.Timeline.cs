#nullable enable

using System;
using Mmd.Native;
using Mmd.Physics;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackController
    {
        // Unity frame on which a Timeline/PlayableDirector last evaluated this controller. When a
        // Timeline drives playback it is the single source of time; the controller must NOT also
        // self-advance via Update().Tick() (double-driving Live physics with two diverging clocks
        // whips the 揺れもの between frames and destabilizes scrub/seek). Sentinel keeps standalone
        // (non-Timeline) playback self-driving.
        private int lastTimelineDriveFrameCount = int.MinValue / 2;

        private int lastHumanoidRetargetTimelineDriveFrameCount = int.MinValue / 2;
        private bool timelinePreparationSeedPending;
        private bool timelinePreparationSeedWasLive;
        private double lastHumanoidTimelineSourceTime;
        private bool hasHumanoidTimelineClock;
        private bool lastHumanoidTimelinePlaying;
        private bool humanoidTimelineEvaluationActive;
        private bool humanoidTimelineEvaluationPlaying;
        private float? humanoidTimelinePhysicsDeltaTime;

        internal void PrepareTimelineSeed(float sourceTime, float frameRate, bool runLivePhysics)
        {
            // A Timeline Off evaluation can reuse the same controller slot as standalone Off
            // playback. Live setup is a different evaluator/session and must replace any slot
            // before mutating the binding synchronously.
            if (runLivePhysics || physicsMode == MmdPhysicsMode.Live)
            {
                ReleasePlaybackWorkerPoolForTimelineSync();
            }
            timelinePreparationSeedPending = false;
            bool seedLivePhysics = runLivePhysics && physicsMode == MmdPhysicsMode.Live;
            if (seedLivePhysics)
            {
                ApplyTimelineLivePhysicsForward(sourceTime, frameRate);
            }
            else
            {
                ApplyTimelineTime(sourceTime, frameRate);
            }

            // Setup owns this evaluation. The first clip sample may reuse it without counting a
            // second pose evaluation, but only while its evaluation mode and Live state still match.
            TimelinePoseEvaluationCount = 0;
            timelinePreparationSeedPending = LastSnapshot != null;
            timelinePreparationSeedWasLive = seedLivePhysics;
        }

        public MmdHumanoidRetargeterResult ApplyHumanoidRetargetNow()
        {
            ReleaseAutomaticWorkerForSynchronousPlayback();
            MmdHumanoidRetargetGate gate = EvaluateHumanoidRetargetGate(requireAnimatorDriver: true);
            LastHumanoidRetargetGate = gate;
            if (gate != MmdHumanoidRetargetGate.Ready)
            {
                LastHumanoidRetargetResult = CreateHumanoidRetargetNoOpResult(gate);
                return LastHumanoidRetargetResult;
            }

            return ApplyHumanoidRetargetCore(copyLocalPositions: true);
        }

        internal MmdHumanoidRetargeterResult ApplyHumanoidRetargetFromTimeline()
        {
            lastHumanoidRetargetTimelineDriveFrameCount = Time.frameCount;
            MmdHumanoidRetargetGate gate = EvaluateHumanoidRetargetGate(requireAnimatorDriver: false);
            LastHumanoidRetargetGate = gate;
            if (gate != MmdHumanoidRetargetGate.Ready)
            {
                LastHumanoidRetargetResult = CreateHumanoidRetargetNoOpResult(gate);
                return LastHumanoidRetargetResult;
            }

            // RootT is already consumed by the custom Timeline root-motion driver. Copying the
            // proxy Hips position into the native center during the same evaluation would apply
            // that translation twice. Keep the legacy position-copy path when no driver is active.
            bool copyLocalPositions =
                !(GetComponent<MmdHumanoidRootMotionDriver>()?.IsTimelineEvaluationActive ?? false);
            return ApplyHumanoidRetargetCore(copyLocalPositions);
        }

        internal MmdHumanoidRetargeterResult ApplyHumanoidRetargetFromTimeline(
            double sourceTime,
            bool timelinePlaying,
            bool seekOccurred,
            double timelineDeltaTime)
        {
            double observedSourceDelta = hasHumanoidTimelineClock
                ? sourceTime - lastHumanoidTimelineSourceTime
                : 0.0;
            bool sourceTimeAdvancing = observedSourceDelta > 1.0e-7;
            UpdateHumanoidTimelineClock(sourceTime, timelinePlaying || sourceTimeAdvancing, seekOccurred, timelineDeltaTime);
            bool previousEvaluationActive = humanoidTimelineEvaluationActive;
            bool previousEvaluationPlaying = humanoidTimelineEvaluationPlaying;
            float? previousPhysicsDeltaTime = humanoidTimelinePhysicsDeltaTime;
            humanoidTimelineEvaluationActive = true;
            // Manual Timeline evaluation can report Paused while its source time advances. Treat
            // that forward sample as playback so each sample steps Live physics instead of
            // reseeding the solver merely because effectivePlayState is paused.
            humanoidTimelineEvaluationPlaying = timelinePlaying || sourceTimeAdvancing;
            humanoidTimelinePhysicsDeltaTime = sourceTimeAdvancing &&
                                               observedSourceDelta <= float.MaxValue
                ? (float)observedSourceDelta
                : 0.0f;
            try
            {
                return ApplyHumanoidRetargetFromTimeline();
            }
            finally
            {
                humanoidTimelineEvaluationActive = previousEvaluationActive;
                humanoidTimelineEvaluationPlaying = previousEvaluationPlaying;
                humanoidTimelinePhysicsDeltaTime = previousPhysicsDeltaTime;
            }
        }

        internal void NotifyHumanoidTimelinePaused(double sourceTime)
        {
            UpdateHumanoidTimelineClock(
                sourceTime,
                timelinePlaying: false,
                seekOccurred: true,
                timelineDeltaTime: 0.0);
        }

        private void UpdateHumanoidTimelineClock(
            double sourceTime,
            bool timelinePlaying,
            bool seekOccurred,
            double timelineDeltaTime)
        {
            if (double.IsNaN(sourceTime) || double.IsInfinity(sourceTime) || sourceTime < 0.0)
            {
                return;
            }

            bool discontinuity = false;
            if (hasHumanoidTimelineClock)
            {
                double observedDelta = sourceTime - lastHumanoidTimelineSourceTime;
                double expectedDelta = !double.IsNaN(timelineDeltaTime) &&
                                       !double.IsInfinity(timelineDeltaTime) &&
                                       timelineDeltaTime > 1.0e-7
                    ? timelineDeltaTime
                    : 1.0 / (frameRate > 0.0f && float.IsFinite(frameRate) ? frameRate : 30.0f);
                discontinuity = observedDelta < -1.0e-7 ||
                    (seekOccurred && observedDelta >= 0.0 &&
                     observedDelta > expectedDelta * 2.0 + 1.0e-4);
                if (lastHumanoidTimelinePlaying && !timelinePlaying)
                {
                    discontinuity = true;
                }
            }

            if (discontinuity && physicsMode == MmdPhysicsMode.Live)
            {
                humanoidPhysicsBinding?.ResetLivePhysicsForDriveSource();
                ResetLivePhysicsDriveSource();
            }

            lastHumanoidTimelineSourceTime = sourceTime;
            hasHumanoidTimelineClock = true;
            lastHumanoidTimelinePlaying = timelinePlaying;
        }

        private MmdHumanoidRetargeterResult ApplyHumanoidRetargetCore(bool copyLocalPositions)
        {
            MmdHumanoidRetargeterResult? nativeResult = null;
            if (physicsMode == MmdPhysicsMode.Off &&
                TryApplyHumanoidNativeHostPose(copyLocalPositions, out nativeResult))
            {
                LastHumanoidRetargetResult = nativeResult!;
                return LastHumanoidRetargetResult;
            }

            bool nativePoseApplied = false;
            bool nativeLivePrepared = physicsMode == MmdPhysicsMode.Live &&
                                      TryPrepareHumanoidNativeLivePhysics(
                                          copyLocalPositions,
                                          out nativeResult);
            if (nativeLivePrepared && nativeResult != null)
            {
                if (!Application.isPlaying)
                {
                    nativePoseApplied = true;
                }
                else
                {
                    try
                    {
                        nativePoseApplied = StepHumanoidRetargetLivePhysicsIfNeeded(
                            nativeResult,
                            humanoidHostPosePositions!,
                            humanoidHostPoseRotations!,
                            humanoidHostPoseScales!,
                            humanoidHostPoseMorphs!,
                            humanoidHostPoseIk!);
                    }
                    catch (Exception exception) when (IsNativeHostPoseFailure(exception))
                    {
                        DisposeHumanoidPhysicsBinding();
                        RecordHumanoidHostPoseFailure(exception);
                    }
                }
                if (nativePoseApplied)
                {
                    LastHumanoidRetargetResult = nativeResult;
                    return LastHumanoidRetargetResult;
                }
            }

            if (!nativePoseApplied)
            {
                RestoreHumanoidHostPoseInputIfAvailable();
            }

            LastHumanoidRetargetResult = MmdHumanoidRetargeter.RetargetPose(
                humanoidRetargetEntries,
                copyLocalPositions);
            MmdHumanoidAppendTransformApplier.Apply(humanoidAppendEntries);

            return LastHumanoidRetargetResult;
        }

        internal MmdHumanoidRetargetGate EvaluateHumanoidRetargetGate(bool requireAnimatorDriver)
        {
            if (!isActiveAndEnabled)
            {
                return MmdHumanoidRetargetGate.ComponentDisabled;
            }

            Animator? animator = GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return MmdHumanoidRetargetGate.MissingHumanAnimator;
            }

            if (requireAnimatorDriver &&
                animator.runtimeAnimatorController == null &&
                !animator.hasBoundPlayables)
            {
                return MmdHumanoidRetargetGate.AnimatorNotDriven;
            }

            if (IsVmdDriving)
            {
                return MmdHumanoidRetargetGate.PlaybackControllerDriving;
            }

            if (proxyRoot == null || humanoidRetargetEntries == null || humanoidRetargetEntries.Count == 0)
            {
                return MmdHumanoidRetargetGate.MissingBindings;
            }

            return MmdHumanoidRetargetGate.Ready;
        }

        internal int LastTimelineDriveFrameCount => lastTimelineDriveFrameCount;

        internal void MarkTimelineDriveForWorker()
        {
            lastTimelineDriveFrameCount = Time.frameCount;
        }

        // Update() runs in the Update phase, before the PlayableDirector evaluates in PreLateUpdate,
        // so a controller driven by a Timeline sees the previous frame's drive (delta == 1) on the
        // current frame's Update. Suppress for delta 0 and 1; resume self-Tick once the Timeline stops
        // driving (delta >= 2) or when it never drove (sentinel -> large delta).
        internal static bool ShouldSuppressSelfTick(int lastTimelineDriveFrameCount, int currentFrameCount)
        {
            return currentFrameCount >= lastTimelineDriveFrameCount
                && (currentFrameCount - lastTimelineDriveFrameCount) <= 1;
        }

        internal static bool ShouldSuppressHumanoidRetargetLateUpdateAfterTimelineDrive(
            int lastHumanoidRetargetTimelineDriveFrameCount,
            int currentFrameCount)
        {
            return ShouldSuppressSelfTick(lastHumanoidRetargetTimelineDriveFrameCount, currentFrameCount);
        }

        internal bool PrewarmTimelineLivePhysics()
        {
            if (binding == null)
            {
                return false;
            }

            try
            {
                return binding.PrewarmLivePhysicsBackend();
            }
            catch (Exception exception) when (
                exception is MmdPhysicsBackendException ||
                exception is MmdRuntimeNativeUnavailableException ||
                exception is DllNotFoundException ||
                exception is EntryPointNotFoundException ||
                exception is BadImageFormatException ||
                exception is InvalidOperationException ||
                exception is ArgumentException)
            {
                // Prewarm is opportunistic. Preserve the previous behaviour outside active clips;
                // the normal Live evaluation path remains authoritative for reporting failures.
                return false;
            }
        }

        /// <summary>
        /// Apply time for Timeline random-access evaluation without stepping Live physics.
        /// The native physics backend is soft-reset and retained so preview/scrubbing does not rebuild
        /// the Bullet world when forward playback resumes.
        /// </summary>
        internal MmdPlaybackSnapshot ApplyTimelineTime(float sourceTime, float frameRate)
        {
            ReleasePlaybackWorkerPoolForTimelineSync();
            if (binding == null)
            {
                throw new InvalidOperationException("Playback controller must be configured before applying timeline time.");
            }

            MmdPlaybackTime.ValidateFrameRate(frameRate);
            lastTimelineDriveFrameCount = Time.frameCount;
            int frame = MmdPlaybackTime.ToFrame(sourceTime, frameRate);
            if (TryReuseTimelinePreparationSeed(
                sourceTime,
                runLivePhysics: false,
                seedStateValid: true,
                out MmdPlaybackSnapshot? cachedSnapshot))
            {
                return cachedSnapshot;
            }

            try
            {
                return ApplyPlaybackPose(() =>
                {
                    playbackFrame = frame;
                    CurrentFrame = frame;
                    LastSnapshot = binding.ApplyTimelinePreviewTime(sourceTime, frameRate);
                    TimelinePoseEvaluationCount++;
                    return LastSnapshot;
                });
            }
            finally
            {
                ResetLivePhysicsDriveSource();
            }
        }

        /// <summary>
        /// Apply time for Timeline forward (real-time) playback while Live physics is enabled.
        /// Steps the Live physics simulation forward instead of suppressing it (the random-access
        /// <see cref="ApplyTimelineTime"/> path). Used only when the PlayableDirector is playing
        /// forward in Play Mode — not editor preview, not scrubbing/seeking. The first stepped frame
        /// seeds physics from the current animated pose, so resuming play after a scrub (which routes
        /// through <see cref="ApplyTimelineTime"/> and resets Live physics) restarts the simulation.
        /// </summary>
        internal MmdPlaybackSnapshot ApplyTimelineLivePhysicsForward(float sourceTime, float frameRate)
        {
            ReleasePlaybackWorkerPool();
            if (binding == null)
            {
                throw new InvalidOperationException("Playback controller must be configured before applying timeline time.");
            }

            MmdPlaybackTime.ValidateFrameRate(frameRate);
            lastTimelineDriveFrameCount = Time.frameCount;
            int frame = MmdPlaybackTime.ToFrame(sourceTime, frameRate);
            // The animation-only scrub path (ApplyTimelineTime) leaves the binding in Live but with a
            // reset simulation; guard against a binding that was left in Off by re-enabling Live.
            if (binding.PhysicsMode != MmdPhysicsMode.Live)
            {
                binding.SetPhysicsMode(MmdPhysicsMode.Live);
            }

            if (TryReuseTimelinePreparationSeed(
                sourceTime,
                runLivePhysics: true,
                seedStateValid: binding.CanReuseLivePhysicsSeed(frame),
                out MmdPlaybackSnapshot? cachedSnapshot))
            {
                return cachedSnapshot;
            }

            playbackFrame = frame;
            CurrentFrame = frame;
            bool poseWillBeApplied = !binding.CanReuseLivePhysicsSeed(frame);
            return ApplyPlaybackPose(() =>
            {
                PrepareLivePhysicsDriveSource(LivePhysicsDriveSource.VmdForward);
                LastSnapshot = binding.ApplyLivePhysicsForwardFrame(frame, frameRate);
                TimelinePoseEvaluationCount++;
                lastVmdLivePhysicsFrameCount = Time.frameCount;
                return LastSnapshot;
            }, poseWillBeApplied);
        }

        private bool TryReuseTimelinePreparationSeed(
            float sourceTime,
            bool runLivePhysics,
            bool seedStateValid,
            out MmdPlaybackSnapshot snapshot)
        {
            if (!timelinePreparationSeedPending)
            {
                snapshot = null!;
                return false;
            }

            timelinePreparationSeedPending = false;
            MmdPlaybackSnapshot? candidate = LastSnapshot;
            if (timelinePreparationSeedWasLive == runLivePhysics &&
                seedStateValid &&
                candidate != null &&
                Math.Abs(candidate.frame.time - sourceTime) <= 1e-7f)
            {
                playbackFrame = MmdPlaybackTime.ToFrame(sourceTime, frameRate);
                CurrentFrame = (int)playbackFrame;
                snapshot = candidate;
                return true;
            }

            snapshot = null!;
            return false;
        }

        private static MmdHumanoidRetargeterResult CreateHumanoidRetargetNoOpResult(MmdHumanoidRetargetGate gate)
        {
            return new MmdHumanoidRetargeterResult(
                0,
                0,
                new[] { "humanoid-retarget: no-op gate=" + gate });
        }
    }
}
