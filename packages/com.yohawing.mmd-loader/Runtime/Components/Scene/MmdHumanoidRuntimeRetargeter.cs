#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public enum MmdHumanoidRuntimeRetargetGate
    {
        Ready = 0,
        ComponentDisabled = 1,
        MissingHumanAnimator = 2,
        AnimatorNotDriven = 3,
        PlaybackControllerDriving = 4,
        MissingBindings = 5
    }

    [DisallowMultipleComponent]
    public sealed class MmdHumanoidRuntimeRetargeter : MonoBehaviour
    {
        [SerializeField] private Transform? proxyRoot;
        [SerializeField] private List<MmdHumanoidRetargetBinding> entries = new();
        [SerializeField] private List<MmdHumanoidAppendTransformBinding> appendEntries = new();

        public Transform? ProxyRoot => proxyRoot;

        public IReadOnlyList<MmdHumanoidRetargetBinding> Entries => entries;

        public IReadOnlyList<MmdHumanoidAppendTransformBinding> AppendEntries => appendEntries;

        public MmdHumanoidRuntimeRetargetGate LastGate { get; private set; } =
            MmdHumanoidRuntimeRetargetGate.MissingBindings;

        public MmdHumanoidRetargeterResult? LastResult { get; private set; }

        private int lastTimelineDriveFrameCount = int.MinValue / 2;

        public void Configure(
            Transform? proxyRoot,
            IReadOnlyList<MmdHumanoidRetargetBinding>? bindings,
            IReadOnlyList<MmdHumanoidAppendTransformBinding>? appendBindings)
        {
            this.proxyRoot = proxyRoot;
            entries = bindings != null
                ? new List<MmdHumanoidRetargetBinding>(bindings)
                : new List<MmdHumanoidRetargetBinding>();
            appendEntries = appendBindings != null
                ? new List<MmdHumanoidAppendTransformBinding>(appendBindings)
                : new List<MmdHumanoidAppendTransformBinding>();
            LastGate = EvaluateRetargetGate(requireAnimatorDriver: true);
            LastResult = null;
        }

        public MmdHumanoidRetargeterResult ApplyRetargetNow()
        {
            MmdHumanoidRuntimeRetargetGate gate = EvaluateRetargetGate(requireAnimatorDriver: true);
            LastGate = gate;
            if (gate != MmdHumanoidRuntimeRetargetGate.Ready)
            {
                LastResult = CreateNoOpResult(gate);
                return LastResult;
            }

            // RetargetPose applies cached bind-rotation deltas while preserving the
            // identity-bind direct-copy behavior used by older bindings and tests.
            LastResult = MmdHumanoidRetargeter.RetargetPose(entries);
            MmdHumanoidAppendTransformApplier.Apply(appendEntries);
            return LastResult;
        }

        internal MmdHumanoidRetargeterResult ApplyRetargetFromTimeline()
        {
            lastTimelineDriveFrameCount = Time.frameCount;
            MmdHumanoidRuntimeRetargetGate gate = EvaluateRetargetGate(requireAnimatorDriver: false);
            LastGate = gate;
            if (gate != MmdHumanoidRuntimeRetargetGate.Ready)
            {
                LastResult = CreateNoOpResult(gate);
                return LastResult;
            }

            LastResult = MmdHumanoidRetargeter.RetargetPose(entries);
            MmdHumanoidAppendTransformApplier.Apply(appendEntries);
            return LastResult;
        }

        internal MmdHumanoidRuntimeRetargetGate EvaluateRetargetGate(bool requireAnimatorDriver)
        {
            if (!isActiveAndEnabled)
            {
                return MmdHumanoidRuntimeRetargetGate.ComponentDisabled;
            }

            Animator? animator = GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return MmdHumanoidRuntimeRetargetGate.MissingHumanAnimator;
            }

            if (requireAnimatorDriver &&
                animator.runtimeAnimatorController == null &&
                !animator.hasBoundPlayables)
            {
                return MmdHumanoidRuntimeRetargetGate.AnimatorNotDriven;
            }

            MmdUnityPlaybackController? playbackController = GetComponent<MmdUnityPlaybackController>();
            if (IsPlaybackControllerDriving(playbackController))
            {
                return MmdHumanoidRuntimeRetargetGate.PlaybackControllerDriving;
            }

            if (proxyRoot == null || entries == null || entries.Count == 0)
            {
                return MmdHumanoidRuntimeRetargetGate.MissingBindings;
            }

            return MmdHumanoidRuntimeRetargetGate.Ready;
        }

        private void LateUpdate()
        {
            if (ShouldSuppressLateUpdateAfterTimelineDrive(lastTimelineDriveFrameCount, Time.frameCount))
            {
                return;
            }

            ApplyRetargetNow();
        }

        internal static bool ShouldSuppressLateUpdateAfterTimelineDrive(
            int lastTimelineDriveFrameCount,
            int currentFrameCount)
        {
            return MmdUnityPlaybackController.ShouldSuppressSelfTick(
                lastTimelineDriveFrameCount,
                currentFrameCount);
        }

        private static bool IsPlaybackControllerDriving(MmdUnityPlaybackController? playbackController)
        {
            if (playbackController == null)
            {
                return false;
            }

            return playbackController.IsConfigured
                   || playbackController.IsPlaying
                   || MmdUnityPlaybackController.ShouldSuppressSelfTick(
                       playbackController.LastTimelineDriveFrameCount,
                       Time.frameCount);
        }

        private static MmdHumanoidRetargeterResult CreateNoOpResult(MmdHumanoidRuntimeRetargetGate gate)
        {
            return new MmdHumanoidRetargeterResult(
                0,
                0,
                new[] { "runtime-retarget: no-op gate=" + gate });
        }
    }
}
