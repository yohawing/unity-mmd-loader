#nullable enable

using System;
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

        public MmdHumanoidRetargeterResult ApplyHumanoidRetargetNow()
        {
            MmdHumanoidRetargetGate gate = EvaluateHumanoidRetargetGate(requireAnimatorDriver: true);
            LastHumanoidRetargetGate = gate;
            if (gate != MmdHumanoidRetargetGate.Ready)
            {
                LastHumanoidRetargetResult = CreateHumanoidRetargetNoOpResult(gate);
                return LastHumanoidRetargetResult;
            }

            LastHumanoidRetargetResult = MmdHumanoidRetargeter.RetargetPose(humanoidRetargetEntries);
            MmdHumanoidAppendTransformApplier.Apply(humanoidAppendEntries);
            return LastHumanoidRetargetResult;
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

            LastHumanoidRetargetResult = MmdHumanoidRetargeter.RetargetPose(humanoidRetargetEntries);
            MmdHumanoidAppendTransformApplier.Apply(humanoidAppendEntries);
            StepHumanoidRetargetLivePhysicsIfNeeded(LastHumanoidRetargetResult);
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

        /// <summary>
        /// Apply time for Timeline random-access evaluation, temporarily suppressing Live physics
        /// on the binding without modifying the controller's serialized <see cref="physicsMode"/> field.
        /// Preserves the binding's physics mode after evaluation without the frame-reset side effects
        /// of <see cref="SetPhysicsMode(MmdPhysicsMode)"/> followed by <see cref="ApplyFrame"/>.
        /// </summary>
        internal MmdPlaybackSnapshot ApplyTimelineTime(float sourceTime, float frameRate)
        {
            if (binding == null)
            {
                throw new InvalidOperationException("Playback controller must be configured before applying timeline time.");
            }

            MmdPlaybackTime.ValidateFrameRate(frameRate);
            lastTimelineDriveFrameCount = Time.frameCount;
            int frame = MmdPlaybackTime.ToFrame(sourceTime, frameRate);

            // Save the binding's current physics mode; we will restore it after ApplyTime.
            MmdPhysicsMode originalBindingMode = binding.PhysicsMode;

            // Temporarily suppress Live on the binding so ApplyTime (random-access) works.
            // We go directly to the binding to avoid changing the controller's serialized physicsMode.
            if (originalBindingMode == MmdPhysicsMode.Live)
            {
                binding.SetPhysicsMode(MmdPhysicsMode.Off);
            }

            try
            {
                return ApplyPlaybackPose(() =>
                {
                    playbackFrame = frame;
                    CurrentFrame = frame;
                    LastSnapshot = binding.ApplyTime(sourceTime, frameRate);
                    ApplyEditableRigLayer("post-native-apply-time");
                    return LastSnapshot;
                });
            }
            finally
            {
                // Restore the binding's original physics mode.
                // We use binding.SetPhysicsMode directly rather than going through the controller's
                // ApplyPhysicsModeToBinding, which would reset playbackFrame/CurrentFrame/LastSnapshot
                // when switching back to Live.
                if (originalBindingMode == MmdPhysicsMode.Live)
                {
                    binding.SetPhysicsMode(MmdPhysicsMode.Live);
                }

                ResetLivePhysicsDriveSource();
                // controller.physicsMode is intentionally NOT modified — preserves serialized/Inspector value.
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

            playbackFrame = frame;
            CurrentFrame = frame;
            return ApplyPlaybackPose(() =>
            {
                PrepareLivePhysicsDriveSource(LivePhysicsDriveSource.VmdForward);
                LastSnapshot = binding.ApplyLivePhysicsForwardFrame(frame, frameRate);
                lastVmdLivePhysicsFrameCount = Time.frameCount;
                ApplyEditableRigLayer("post-physics-live-frame");
                return LastSnapshot;
            });
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
