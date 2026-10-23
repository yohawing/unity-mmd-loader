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
        private MmdHumanoidPhysicsBindingFailureReason humanoidPhysicsBindingFailureReason;

        private bool StepHumanoidRetargetLivePhysicsIfNeeded(
            MmdHumanoidRetargeterResult result,
            float[] localPositionOffsets,
            float[] localRotations,
            float[] localScales,
            float[] morphWeights,
            byte[] ikEnabled)
        {
            bool sameFrame = lastHumanoidLivePhysicsFrameCount == Time.frameCount;
            if (sameFrame &&
                humanoidPhysicsBinding?.NativeHumanoidHostPoseEnabled == true)
            {
                if (hasLastHumanoidNativeHostPoseInputFingerprint &&
                    humanoidHostPoseInputFingerprint == lastHumanoidNativeHostPoseInputFingerprint)
                {
                    return humanoidPhysicsBinding.ReapplyNativeHumanoidHostPoseWorldMatrices();
                }
            }

            if (physicsMode != MmdPhysicsMode.Live ||
                !Application.isPlaying ||
                result.CopiedBoneCount <= 0 ||
                LastHumanoidRetargetGate != MmdHumanoidRetargetGate.Ready ||
                IsVmdDriving ||
                isApplyingPlaybackPose ||
                ShouldSuppressSelfTick(lastTimelineDriveFrameCount, Time.frameCount) ||
                lastVmdLivePhysicsFrameCount == Time.frameCount)
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

            bool switchedDriveSource = PrepareLivePhysicsDriveSource(
                LivePhysicsDriveSource.HumanoidRetarget,
                physicsBinding);
            bool resetOnFirstStep = (humanoidTimelineEvaluationActive &&
                                     !humanoidTimelineEvaluationPlaying) ||
                                    sameFrame || switchedDriveSource;
            // Keep the native sequence frame stable for same-Unity-frame pose changes. A source
            // switch can reset the index to -1 in that frame, so still allocate the first zero
            // sequence frame for that seed.
            if (!sameFrame || humanoidLivePhysicsStepIndex < 0)
            {
                humanoidLivePhysicsStepIndex++;
            }
            physicsBinding.StepLivePhysicsFromHostPose(
                humanoidLivePhysicsStepIndex,
                resetOnFirstStep ? 0.0f :
                    humanoidTimelinePhysicsDeltaTime ?? Time.deltaTime,
                resetOnFirstStep,
                localPositionOffsets,
                localRotations,
                localScales,
                morphWeights,
                ikEnabled);
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
                humanoidPhysicsBindingFailureReason =
                    MmdHumanoidPhysicsBindingFailureReason.MissingComponent;
            }
            catch (InvalidOperationException)
            {
                humanoidPhysicsBindingFailureReason =
                    MmdHumanoidPhysicsBindingFailureReason.InvalidOperation;
            }
        }

        private void ResetHumanoidPhysicsBindingFailureReason()
        {
            humanoidPhysicsBindingFailureReason = MmdHumanoidPhysicsBindingFailureReason.None;
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

            if (humanoidHostPoseDrivenBones == null || humanoidHostPoseGoalBones == null)
            {
                return false;
            }

            if (physicsBinding.TryEnableNativeHumanoidHostPose(
                humanoidHostPoseDrivenBones,
                humanoidHostPoseGoalBones))
            {
                return true;
            }

            humanoidHostPoseFailureReason = physicsBinding.LastNativeHumanoidHostPoseFailureReason;

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
