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

        public Transform? ProxyRoot => proxyRoot;

        public IReadOnlyList<MmdHumanoidRetargetBinding> Entries => entries;

        public MmdHumanoidRuntimeRetargetGate LastGate { get; private set; } =
            MmdHumanoidRuntimeRetargetGate.MissingBindings;

        public MmdHumanoidRetargeterResult? LastResult { get; private set; }

        public void Configure(
            Transform? proxyRoot,
            IReadOnlyList<MmdHumanoidRetargetBinding>? bindings)
        {
            this.proxyRoot = proxyRoot;
            entries = bindings != null
                ? new List<MmdHumanoidRetargetBinding>(bindings)
                : new List<MmdHumanoidRetargetBinding>();
            LastGate = EvaluateRetargetGate();
            LastResult = null;
        }

        public MmdHumanoidRetargeterResult ApplyRetargetNow()
        {
            MmdHumanoidRuntimeRetargetGate gate = EvaluateRetargetGate();
            LastGate = gate;
            if (gate != MmdHumanoidRuntimeRetargetGate.Ready)
            {
                LastResult = CreateNoOpResult(gate);
                return LastResult;
            }

            // Slice 2 intentionally performs direct rotation copy. Future slices can extend
            // MmdHumanoidRetargetBinding with cached proxy rest/native bind rotations and apply:
            // native.localRotation = nativeBind * inverse(proxyRest) * proxyCurrent.
            LastResult = MmdHumanoidRetargeter.RetargetPose(entries);
            return LastResult;
        }

        internal MmdHumanoidRuntimeRetargetGate EvaluateRetargetGate()
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

            if (animator.runtimeAnimatorController == null && !animator.hasBoundPlayables)
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
            ApplyRetargetNow();
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
