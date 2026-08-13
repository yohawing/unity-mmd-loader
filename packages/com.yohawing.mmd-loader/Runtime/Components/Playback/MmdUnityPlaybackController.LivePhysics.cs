#nullable enable

using System;
using Mmd.Physics;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackController
    {
        private int lastVmdLivePhysicsFrameCount = int.MinValue / 2;
        private int humanoidLivePhysicsStepIndex = -1;
        private int lastHumanoidLivePhysicsFrameCount = int.MinValue / 2;
        private uint lastHumanoidNativeHostPoseInputFingerprint;
        private bool hasLastHumanoidNativeHostPoseInputFingerprint;
        private LivePhysicsDriveSource livePhysicsDriveSource = LivePhysicsDriveSource.None;

        private bool StepHumanoidRetargetLivePhysicsIfNeeded(MmdHumanoidRetargeterResult result)
        {
            if (lastHumanoidLivePhysicsFrameCount == Time.frameCount &&
                humanoidPhysicsBinding?.NativeHumanoidHostPoseEnabled == true)
            {
                if (!hasLastHumanoidNativeHostPoseInputFingerprint ||
                    humanoidHostPoseInputFingerprint != lastHumanoidNativeHostPoseInputFingerprint)
                {
                    return false;
                }

                return humanoidPhysicsBinding.ReapplyNativeHumanoidHostPoseWorldMatrices();
            }

            if (physicsMode != MmdPhysicsMode.Live ||
                !Application.isPlaying ||
                result.CopiedBoneCount <= 0 ||
                LastHumanoidRetargetGate != MmdHumanoidRetargetGate.Ready ||
                IsVmdDriving ||
                isApplyingPlaybackPose ||
                ShouldSuppressSelfTick(lastTimelineDriveFrameCount, Time.frameCount) ||
                lastVmdLivePhysicsFrameCount == Time.frameCount ||
                lastHumanoidLivePhysicsFrameCount == Time.frameCount)
            {
                return false;
            }

            EnsureHumanoidPhysicsBinding();

            MmdUnityPlaybackBinding? physicsBinding = humanoidPhysicsBinding;
            if (physicsBinding == null)
            {
                return false;
            }

            if (physicsBinding.PhysicsMode != MmdPhysicsMode.Live)
            {
                physicsBinding.SetPhysicsMode(MmdPhysicsMode.Live);
                ResetLivePhysicsDriveSource();
            }

            bool resetOnFirstStep = PrepareLivePhysicsDriveSource(
                LivePhysicsDriveSource.HumanoidRetarget,
                physicsBinding);
            humanoidLivePhysicsStepIndex++;
            physicsBinding.StepLivePhysicsFromCurrentPose(
                humanoidLivePhysicsStepIndex,
                resetOnFirstStep ? 0.0f : Time.deltaTime,
                resetOnFirstStep);
            lastHumanoidLivePhysicsFrameCount = Time.frameCount;
            lastHumanoidNativeHostPoseInputFingerprint = humanoidHostPoseInputFingerprint;
            hasLastHumanoidNativeHostPoseInputFingerprint = true;
            return true;
        }

        private void EnsureHumanoidPhysicsBinding()
        {
            if (binding != null ||
                humanoidPhysicsBinding != null ||
                physicsMode != MmdPhysicsMode.Live ||
                !Application.isPlaying ||
                proxyRoot == null ||
                humanoidRetargetEntries == null ||
                humanoidRetargetEntries.Count == 0 ||
                modelAsset == null ||
                LastHumanoidRetargetGate != MmdHumanoidRetargetGate.Ready ||
                IsVmdDriving ||
                isApplyingPlaybackPose)
            {
                return;
            }

            try
            {
                humanoidPhysicsBinding = MmdUnityPlaybackBinding.CreateSkinnedForModelOnlyPhysicsFromExistingSceneModel(
                    gameObject,
                    modelAsset,
                    "humanoid-physics");
                humanoidPhysicsBinding.LivePhysicsBodyDiagnosticsSampleInterval = livePhysicsBodyDiagnosticsSampleInterval;
                humanoidPhysicsBinding.IkMaxIterationsCap = ikMaxIterationsCap;
                humanoidPhysicsBinding.SetPhysicsMode(MmdPhysicsMode.Live);
                ResetLivePhysicsDriveSource();
                ConfigurationRevision++;
            }
            catch (MissingComponentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private bool TryPrepareHumanoidNativeLivePhysicsBinding()
        {
            EnsureHumanoidPhysicsBinding();
            MmdUnityPlaybackBinding? physicsBinding = humanoidPhysicsBinding;
            if (physicsBinding == null)
            {
                return false;
            }

            if (physicsBinding.PhysicsMode != MmdPhysicsMode.Live)
            {
                physicsBinding.SetPhysicsMode(MmdPhysicsMode.Live);
                ResetLivePhysicsDriveSource();
            }

            if (physicsBinding.TryEnableNativeHumanoidHostPose())
            {
                return true;
            }

            // Native setup must succeed before the managed append path is skipped. Preserve the
            // existing managed append Oracle when the backend is unavailable or incompatible.
            DisposeHumanoidPhysicsBinding();
            DisposeHumanoidHostPoseSession();
            humanoidHostPoseFailureLatched = true;
            humanoidHostPoseFailureModelAsset = modelAsset;
            return false;
        }

        private bool PrepareLivePhysicsDriveSource(LivePhysicsDriveSource source)
        {
            if (binding == null)
            {
                return false;
            }

            return PrepareLivePhysicsDriveSource(source, binding);
        }

        private bool PrepareLivePhysicsDriveSource(
            LivePhysicsDriveSource source,
            MmdUnityPlaybackBinding physicsBinding)
        {
            if (physicsBinding == null)
            {
                return false;
            }

            if (livePhysicsDriveSource == source)
            {
                return false;
            }

            physicsBinding.ResetLivePhysicsForDriveSource();
            livePhysicsDriveSource = source;
            if (source == LivePhysicsDriveSource.HumanoidRetarget)
            {
                humanoidLivePhysicsStepIndex = -1;
            }

            return true;
        }

        private void ResetLivePhysicsDriveSource()
        {
            livePhysicsDriveSource = LivePhysicsDriveSource.None;
            humanoidLivePhysicsStepIndex = -1;
            lastHumanoidLivePhysicsFrameCount = int.MinValue / 2;
            hasLastHumanoidNativeHostPoseInputFingerprint = false;
            lastVmdLivePhysicsFrameCount = int.MinValue / 2;
        }

        private void DisposeHumanoidPhysicsBinding()
        {
            if (humanoidPhysicsBinding == null)
            {
                return;
            }

            humanoidPhysicsBinding.Dispose();
            humanoidPhysicsBinding = null;
            ResetLivePhysicsDriveSource();
        }
    }
}
