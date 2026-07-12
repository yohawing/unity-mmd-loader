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
        private LivePhysicsDriveSource livePhysicsDriveSource = LivePhysicsDriveSource.None;

        private void StepHumanoidRetargetLivePhysicsIfNeeded(MmdHumanoidRetargeterResult result)
        {
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
                return;
            }

            EnsureHumanoidPhysicsBinding();

            MmdUnityPlaybackBinding? physicsBinding = humanoidPhysicsBinding;
            if (physicsBinding == null)
            {
                return;
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

            physicsBinding.ResetLivePhysics();
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