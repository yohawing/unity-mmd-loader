#nullable enable

using System;
using System.Diagnostics;
using Mmd;
using Mmd.Parser;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackController
    {
        private void ReleaseCurrentBindingBeforeSceneRebind()
        {
            DisposeHumanoidPhysicsBinding();
            if (binding == null)
            {
                return;
            }

            binding.Dispose();
            binding = null;
            LastSnapshot = null;
            IsPlaying = false;
            ResetLivePhysicsDriveSource();
        }

        private void ApplyReboundStartFrameWithRollback(
            int startFrame,
            MmdTimelineSetupTimingSummary? setupTiming)
        {
            long phaseStart = Stopwatch.GetTimestamp();
            try
            {
                ApplyFrame(startFrame);
                if (setupTiming != null)
                {
                    setupTiming.initialSeedMs += TimelineSetupElapsedMilliseconds(phaseStart);
                }
            }
            catch
            {
                // TryConfigureNativeFirst has already committed the candidate to the controller
                // by this point. Keep post-seed failures on the same cleanup boundary as setup
                // failures so scene mutation and native ownership do not outlive the exception.
                ReleaseCurrentBindingBeforeSceneRebind();
                throw;
            }
        }

        private void TryConfigureNativeFirst(
            MmdNativePlaybackSetup setup,
            Func<MmdMotionDefinition, MmdUnityPlaybackBinding> createBinding,
            Action<MmdUnityPlaybackBinding> configure,
            bool timelineEvaluation,
            MmdTimelineSetupTimingSummary? setupTiming = null)
        {
            long phaseStart = Stopwatch.GetTimestamp();
            bool nativeAvailable = TryCheckNativeRuntimeAvailability(
                setup.PmxBytes,
                setup.VmdBytes,
                out string nativeRuntimeFailure);
            if (setupTiming != null)
            {
                setupTiming.nativeAvailabilityMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            if (nativeAvailable)
            {
                MmdTimelineLivePhysicsTransfer? livePhysicsTransfer = timelineEvaluation
                    ? binding?.DetachTimelineLivePhysicsBackend()
                    : null;
                phaseStart = Stopwatch.GetTimestamp();
                ReleaseCurrentBindingBeforeSceneRebind();
                if (setupTiming != null)
                {
                    setupTiming.releasePreviousBindingMs += TimelineSetupElapsedMilliseconds(phaseStart);
                }
                MmdUnityPlaybackBinding? nativeBinding = null;
                try
                {
                    phaseStart = Stopwatch.GetTimestamp();
                    nativeBinding = createBinding(setup.Motion);
                    if (setupTiming != null)
                    {
                        setupTiming.sceneBindingMs += TimelineSetupElapsedMilliseconds(phaseStart);
                    }
                    phaseStart = Stopwatch.GetTimestamp();
                    if (TryEnableNativeRuntime(
                            nativeBinding,
                            setup.PmxBytes,
                            setup.VmdBytes,
                            setup.SharedVmdContext,
                            out nativeRuntimeFailure))
                    {
                        if (setupTiming != null)
                        {
                            setupTiming.nativeSessionMs += TimelineSetupElapsedMilliseconds(phaseStart);
                        }
                        lastFastRuntimeReason = string.Empty;
                        phaseStart = Stopwatch.GetTimestamp();
                        configure(nativeBinding);
                        if (setupTiming != null)
                        {
                            setupTiming.controllerConfigureMs += TimelineSetupElapsedMilliseconds(phaseStart);
                        }
                        if (livePhysicsTransfer != null)
                        {
                            try
                            {
                                bool reused = nativeBinding.TryAttachTimelineLivePhysicsBackend(livePhysicsTransfer);
                                if (setupTiming != null)
                                {
                                    setupTiming.livePhysicsWorldReused = reused;
                                }
                            }
                            finally
                            {
                                livePhysicsTransfer.Dispose();
                                livePhysicsTransfer = null;
                            }
                        }
                        nativeBinding = null;
                        return;
                    }
                    if (setupTiming != null)
                    {
                        setupTiming.nativeSessionMs += TimelineSetupElapsedMilliseconds(phaseStart);
                    }
                }
                finally
                {
                    livePhysicsTransfer?.Dispose();
                    if (nativeBinding != null)
                    {
                        ClearFailedBindingReference(nativeBinding);
                    }
                    nativeBinding?.Dispose();
                }
            }

            string finalNativeRuntimeFailure = ComposeNativeRuntimeFailure(
                setup.SharedVmdContextFailure,
                nativeRuntimeFailure);
            lastFastRuntimeReason = finalNativeRuntimeFailure;
            phaseStart = Stopwatch.GetTimestamp();
            ReleaseCurrentBindingBeforeSceneRebind();
            if (setupTiming != null)
            {
                setupTiming.releasePreviousBindingMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            throw CreateNativeClipPlaybackUnavailableException(finalNativeRuntimeFailure, timelineEvaluation);
        }

        private static double TimelineSetupElapsedMilliseconds(long startTimestamp)
        {
            return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
        }

        private static string ComposeNativeRuntimeFailure(
            string? sharedVmdContextFailure,
            string nativeRuntimeFailure)
        {
            if (string.IsNullOrWhiteSpace(sharedVmdContextFailure))
            {
                return nativeRuntimeFailure;
            }

            return "Shared VMD context setup failed: " + sharedVmdContextFailure +
                   "; standalone native setup failed: " + nativeRuntimeFailure;
        }

        private void ClearFailedBindingReference(MmdUnityPlaybackBinding failedBinding)
        {
            if (!ReferenceEquals(binding, failedBinding))
            {
                return;
            }

            binding = null;
            LastSnapshot = null;
            IsPlaying = false;
            ResetLivePhysicsDriveSource();
        }

        private static InvalidOperationException CreateNativeClipPlaybackUnavailableException(
            string reason,
            bool timelineEvaluation)
        {
            string prefix = timelineEvaluation ? "Timeline evaluation" : "Normal playback";
            return new InvalidOperationException(
                prefix + " requires mmd-anim native clip playback for VMD asset evaluation. " +
                "Fast runtime unavailable: " + reason);
        }

        private static InvalidOperationException CreateMissingSceneModelException(
            string sourceId,
            bool timelineEvaluation)
        {
            string prefix = timelineEvaluation ? "Timeline evaluation" : "MMD playback";
            return new InvalidOperationException(
                prefix + " requires an existing scene PMX model with a SkinnedMeshRenderer to bind motion. " +
                "No matching SkinnedMeshRenderer was found for provider model source (" + sourceId + ").");
        }

        private bool TryValidateExistingSceneModelCompatibility(
            MmdModelDefinition model,
            MmdTimelineSetupTimingSummary? setupTiming)
        {
            if (!HasExistingSceneSkinnedMeshRenderer())
            {
                return false;
            }

            ValidateExistingSceneModelCompatibility(model, setupTiming);
            return true;
        }

        private void ValidateExistingSceneModelCompatibility(
            MmdModelDefinition model,
            MmdTimelineSetupTimingSummary? setupTiming)
        {
            long phaseStart = Stopwatch.GetTimestamp();
            MmdUnityModelFactory.ValidateExistingSkinnedModelCompatibility(gameObject, model);
            if (setupTiming != null)
            {
                setupTiming.compatibilityValidationMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
        }
    }
}
