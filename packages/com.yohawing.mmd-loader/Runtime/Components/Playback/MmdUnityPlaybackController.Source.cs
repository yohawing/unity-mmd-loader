#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using Mmd;
using Mmd.Native;
using Mmd.Parser;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackController
    {
        public void ConfigureModelAsset(MmdPmxAsset pmxAsset)
        {
            if (pmxAsset == null)
            {
                throw new ArgumentNullException(nameof(pmxAsset));
            }

            if (!ReferenceEquals(modelAsset, pmxAsset))
            {
                DisposeHumanoidPhysicsBinding();
                DisposeHumanoidHostPoseSession();
                ResetHumanoidHostPoseFailureLatch();
            }

            modelAsset = pmxAsset;
        }

        public void ConfigureMotionAsset(MmdVmdAsset vmdAsset)
        {
            if (vmdAsset == null)
            {
                throw new ArgumentNullException(nameof(vmdAsset));
            }

            motionAsset = vmdAsset;
        }

        public void ConfigureFromPlaybackSource(MmdPmxAsset pmxAsset, MmdVmdAsset vmdAsset, MmdPlaybackConfig config)
        {
            if (pmxAsset == null)
            {
                throw new ArgumentNullException(nameof(pmxAsset));
            }

            if (vmdAsset == null)
            {
                throw new ArgumentNullException(nameof(vmdAsset));
            }

            config.Validate();
            ConfigureModelAsset(pmxAsset);
            ConfigureMotionAsset(vmdAsset);
            MmdMotionDefinition motion = vmdAsset.CreateNativeClipMotionHeader();
            MmdMotionValidator.ThrowIfInvalid(motion);
            if (TryConfigureReboundAssetBinding(
                pmxAsset,
                vmdAsset,
                motion,
                reboundBinding => Configure(reboundBinding, config),
                timelineEvaluation: false,
                applyStartFrame: null))
            {
                return;
            }

            throw CreateMissingSceneModelException(ResolveAssetSourceId(pmxAsset), timelineEvaluation: false);
        }

        public void ConfigureFromRuntimeImporterPaths(
            string pmxPath,
            string vmdPath,
            MmdPlaybackConfig config)
        {
            ConfigureFromRuntimeImporterPathsCore(pmxPath, vmdPath, config, timelineEvaluation: false);
        }

        internal void ConfigureFromRuntimeImporterPathsForTimeline(
            string pmxPath,
            string vmdPath,
            MmdPlaybackConfig config,
            MmdTimelineSetupTimingSummary? setupTiming = null)
        {
            ConfigureFromRuntimeImporterPathsCore(
                pmxPath,
                vmdPath,
                config,
                timelineEvaluation: true,
                setupTiming);
        }

        private void ConfigureFromRuntimeImporterPathsCore(
            string pmxPath,
            string vmdPath,
            MmdPlaybackConfig config,
            bool timelineEvaluation,
            MmdTimelineSetupTimingSummary? setupTiming = null)
        {
            if (string.IsNullOrWhiteSpace(pmxPath))
            {
                throw new ArgumentException("PMX path is required.", nameof(pmxPath));
            }

            if (string.IsNullOrWhiteSpace(vmdPath))
            {
                throw new ArgumentException("VMD path is required.", nameof(vmdPath));
            }

            config.Validate();
            string resolvedPmxPath = Path.GetFullPath(pmxPath);
            string resolvedVmdPath = Path.GetFullPath(vmdPath);
            if (!File.Exists(resolvedPmxPath))
            {
                throw new FileNotFoundException("Runtime importer PMX file was not found.", resolvedPmxPath);
            }

            if (!File.Exists(resolvedVmdPath))
            {
                throw new FileNotFoundException("Runtime importer VMD file was not found.", resolvedVmdPath);
            }

            long phaseStart = Stopwatch.GetTimestamp();
            byte[] pmxBytes = File.ReadAllBytes(resolvedPmxPath);
            byte[] vmdBytes = File.ReadAllBytes(resolvedVmdPath);
            if (setupTiming != null)
            {
                setupTiming.sourceCopyMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            var parser = new NativeMmdParser();
            phaseStart = Stopwatch.GetTimestamp();
            MmdModelDefinition model = parser.LoadModel(pmxBytes);
            MmdModelValidator.ThrowIfInvalid(model);
            if (setupTiming != null)
            {
                setupTiming.pmxParseMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            phaseStart = Stopwatch.GetTimestamp();
            MmdVmdParseSummary summary = MmdVmdNativeSummaryAdapter.Read(vmdBytes);
            MmdMotionDefinition motion = MmdVmdAsset.CreateNativeClipMotionHeader(vmdBytes, summary);
            MmdMotionValidator.ThrowIfInvalid(motion);
            if (setupTiming != null)
            {
                setupTiming.motionHeaderMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            if (!HasExistingSceneSkinnedMeshRenderer())
            {
                throw CreateMissingSceneModelException(resolvedPmxPath, timelineEvaluation);
            }

            phaseStart = Stopwatch.GetTimestamp();
            MmdUnityModelFactory.ValidateExistingSkinnedModelCompatibility(gameObject, model);
            if (setupTiming != null)
            {
                setupTiming.compatibilityValidationMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            TryConfigureNativeFirst(
                motion,
                pmxBytes,
                vmdBytes,
                nativeMotion => MmdUnityPlaybackBinding.CreateSkinnedFromExistingSceneModel(
                    gameObject,
                    model,
                    nativeMotion,
                    resolvedPmxPath,
                    resolvedVmdPath,
                    resolvedPmxPath),
                candidate =>
                {
                    if (setupTiming == null)
                    {
                        Configure(candidate, config);
                    }
                    else
                    {
                        Configure(candidate, config.FrameRate, config.PlayOnStart);
                    }
                },
                timelineEvaluation,
                setupTiming: setupTiming);
            if (setupTiming != null)
            {
                phaseStart = Stopwatch.GetTimestamp();
                ApplyFrame(config.InitialFrame);
                setupTiming.initialSeedMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
        }

        public void ConfigureMotionFromProviderModelSource(MmdVmdAsset vmdAsset, MmdPlaybackConfig config)
        {
            config.Validate();
            ConfigureMotionFromProviderModelSource(
                vmdAsset,
                config.FrameRate,
                config.InitialFrame,
                config.PlayOnStart);
        }

        public void ConfigureMotionFromProviderModelSource(
            MmdVmdAsset vmdAsset,
            float playbackFrameRate,
            int startFrame = 0,
            bool playOnStart = true)
        {
            ConfigureMotionFromProviderModelSourceCore(
                vmdAsset,
                playbackFrameRate,
                startFrame,
                playOnStart,
                timelineEvaluation: false);
        }

        internal void ConfigureMotionFromProviderModelSourceForTimeline(
            MmdVmdAsset vmdAsset,
            float playbackFrameRate,
            int startFrame = 0,
            bool playOnStart = false,
            MmdTimelineSetupTimingSummary? setupTiming = null)
        {
            ConfigureMotionFromProviderModelSourceCore(
                vmdAsset,
                playbackFrameRate,
                startFrame,
                playOnStart,
                timelineEvaluation: true,
                setupTiming);
        }

        private void ConfigureMotionFromProviderModelSourceCore(
            MmdVmdAsset vmdAsset,
            float playbackFrameRate,
            int startFrame,
            bool playOnStart,
            bool timelineEvaluation,
            MmdTimelineSetupTimingSummary? setupTiming = null)
        {
            if (vmdAsset == null)
            {
                throw new ArgumentNullException(nameof(vmdAsset));
            }

            if (startFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startFrame), "Initial frame must not be negative.");
            }

            ConfigureMotionAsset(vmdAsset);
            // Imported VMD playback uses source-backed native evaluation for both normal playback
            // and Timeline. Native-unavailable evaluation is explicitly unsupported.
            long phaseStart = Stopwatch.GetTimestamp();
            MmdMotionDefinition motion = vmdAsset.CreateNativeClipMotionHeader();
            MmdMotionValidator.ThrowIfInvalid(motion);
            if (setupTiming != null)
            {
                setupTiming.motionHeaderMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }

            // Model source from controller asset source or RuntimeImporterComponent raw path.
            MmdPmxAsset? providerPmxAsset = ModelAssetSource;

            MmdRuntimeImporterComponent? runtimeImporter = GetComponent<MmdRuntimeImporterComponent>();
            string? providerPmxPath = runtimeImporter != null && !string.IsNullOrWhiteSpace(runtimeImporter.ModelPath)
                ? runtimeImporter.ModelPath
                : null;

            if (providerPmxAsset == null && string.IsNullOrWhiteSpace(providerPmxPath))
            {
                throw new InvalidOperationException("A provider-owned PMX model source is required before configuring VMD playback.");
            }

            if (providerPmxAsset != null)
            {
                if (TryConfigureReboundAssetBinding(
                    providerPmxAsset,
                    vmdAsset,
                    motion,
                    reboundBinding => Configure(reboundBinding, playbackFrameRate, playOnStart),
                    timelineEvaluation,
                    applyStartFrame: startFrame,
                    setupTiming))
                {
                    return;
                }

                throw CreateMissingSceneModelException(
                    ResolveAssetSourceId(providerPmxAsset),
                    timelineEvaluation);
            }

            // providerPmxPath (runtime importer model path) + vmdAsset case
            string resolvedPmxPath = Path.GetFullPath(providerPmxPath!);
            if (!File.Exists(resolvedPmxPath))
            {
                throw new FileNotFoundException("Provider PMX file was not found.", resolvedPmxPath);
            }

            var parser = new NativeMmdParser();
            phaseStart = Stopwatch.GetTimestamp();
            MmdModelDefinition model = parser.LoadModel(File.ReadAllBytes(resolvedPmxPath));
            MmdModelValidator.ThrowIfInvalid(model);
            if (setupTiming != null)
            {
                setupTiming.pmxParseMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }

            if (!HasExistingSceneSkinnedMeshRenderer())
            {
                throw CreateMissingSceneModelException(resolvedPmxPath, timelineEvaluation);
            }

            phaseStart = Stopwatch.GetTimestamp();
            MmdUnityModelFactory.ValidateExistingSkinnedModelCompatibility(gameObject, model);
            if (setupTiming != null)
            {
                setupTiming.compatibilityValidationMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            phaseStart = Stopwatch.GetTimestamp();
            byte[] pathPmxBytes = File.ReadAllBytes(resolvedPmxPath);
            byte[] pathVmdBytes = vmdAsset.GetBytesCopy();
            if (setupTiming != null)
            {
                setupTiming.sourceCopyMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            TryConfigureNativeFirst(
                motion,
                pathPmxBytes,
                pathVmdBytes,
                nativeMotion => MmdUnityPlaybackBinding.CreateSkinnedFromExistingSceneModel(
                    gameObject,
                    model,
                    nativeMotion,
                    resolvedPmxPath,
                    string.IsNullOrWhiteSpace(vmdAsset.SourceId) ? vmdAsset.name : vmdAsset.SourceId,
                    resolvedPmxPath),
                candidate => Configure(candidate, playbackFrameRate, playOnStart),
                timelineEvaluation,
                setupTiming: setupTiming);

            phaseStart = Stopwatch.GetTimestamp();
            ApplyFrame(startFrame);
            if (setupTiming != null)
            {
                setupTiming.initialSeedMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
        }

        public bool IsConfiguredForMotionAsset(MmdVmdAsset motion)
        {
            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            return binding != null && string.Equals(
                MotionSourceId,
                ResolveAssetSourceId(motion),
                StringComparison.Ordinal);
        }

        public void ConfigureFromAssets(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            float playbackFrameRate,
            int startFrame = 0,
            bool playOnStart = true)
        {
            if (startFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startFrame), "Initial frame must not be negative.");
            }

            ConfigureModelAsset(pmxAsset);
            ConfigureMotionAsset(vmdAsset);
            MmdMotionDefinition motion = vmdAsset.CreateNativeClipMotionHeader();
            MmdMotionValidator.ThrowIfInvalid(motion);

            if (TryConfigureReboundAssetBinding(
                pmxAsset,
                vmdAsset,
                motion,
                reboundBinding => Configure(reboundBinding, playbackFrameRate, playOnStart),
                timelineEvaluation: false,
                applyStartFrame: startFrame))
            {
                return;
            }

            throw CreateMissingSceneModelException(ResolveAssetSourceId(pmxAsset), timelineEvaluation: false);
        }

        public bool ConfigureFromPlaybackSourceIfAvailable()
        {
            return ConfigureFromPlaybackSourceIfAvailableCore(timelineEvaluation: false);
        }

        internal bool ConfigureFromPlaybackSourceIfAvailableForTimeline(
            MmdTimelineSetupTimingSummary? setupTiming = null)
        {
            return ConfigureFromPlaybackSourceIfAvailableCore(timelineEvaluation: true, setupTiming);
        }

        private bool ConfigureFromPlaybackSourceIfAvailableCore(
            bool timelineEvaluation,
            MmdTimelineSetupTimingSummary? setupTiming = null)
        {
            MmdRuntimeImporterComponent? runtimeImporter = GetComponent<MmdRuntimeImporterComponent>();
            if (runtimeImporter != null && (timelineEvaluation
                ? runtimeImporter.TryConfigureControllerForTimeline(this, setupTiming)
                : runtimeImporter.TryConfigureController(this)))
            {
                return true;
            }

            // Asset source path: controller fields are primary.
            // Enables auto-config on PlayMode Start / domain reload for authored scenes.
            MmdPmxAsset? modelAsset = ModelAssetSource;
            MmdVmdAsset? motionAsset = MotionAssetSource;
            if (modelAsset != null && motionAsset != null)
            {
                if (timelineEvaluation)
                {
                    ConfigureMotionFromProviderModelSourceForTimeline(
                        motionAsset,
                        frameRate,
                        initialFrame,
                        playOnStart,
                        setupTiming);
                }
                else
                {
                    ConfigureFromAssets(
                        modelAsset,
                        motionAsset,
                        frameRate,
                        initialFrame,
                        playOnStart);
                }
                return true;
            }

            return false;
        }

        private bool HasExistingSceneSkinnedMeshRenderer()
        {
            return GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true) != null;
        }

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

        private bool TryConfigureReboundAssetBinding(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            MmdMotionDefinition motion,
            Action<MmdUnityPlaybackBinding> configure,
            bool timelineEvaluation,
            int? applyStartFrame,
            MmdTimelineSetupTimingSummary? setupTiming = null)
        {
            if (!HasExistingSceneSkinnedMeshRenderer())
            {
                return false;
            }

            var parser = new NativeMmdParser();
            long phaseStart = Stopwatch.GetTimestamp();
            MmdModelDefinition model = pmxAsset.LoadModel(parser);
            MmdModelValidator.ThrowIfInvalid(model);
            if (setupTiming != null)
            {
                setupTiming.pmxParseMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            phaseStart = Stopwatch.GetTimestamp();
            MmdUnityModelFactory.ValidateExistingSkinnedModelCompatibility(gameObject, model);
            if (setupTiming != null)
            {
                setupTiming.compatibilityValidationMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            phaseStart = Stopwatch.GetTimestamp();
            byte[] pmxBytes = pmxAsset.GetBytesCopy();
            byte[] vmdBytes = motion.sourceBytes
                ?? throw new InvalidOperationException("Native VMD motion source bytes are required.");
            if (setupTiming != null)
            {
                setupTiming.sourceCopyMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            phaseStart = Stopwatch.GetTimestamp();
            vmdAsset.TryGetOrCreateNativeVmdContext(
                out MmdRuntimeFfiVmdContext? sharedVmdContext,
                out string sharedVmdContextFailure);
            if (setupTiming != null)
            {
                setupTiming.sharedVmdContextMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            TryConfigureNativeFirst(
                motion,
                pmxBytes,
                vmdBytes,
                nativeMotion => MmdUnityPlaybackBinding.CreateSkinnedFromExistingSceneModel(
                    gameObject,
                    pmxAsset,
                    model,
                    vmdAsset,
                    nativeMotion),
                configure,
                timelineEvaluation,
                sharedVmdContext,
                sharedVmdContextFailure,
                setupTiming);

            if (applyStartFrame.HasValue)
            {
                phaseStart = Stopwatch.GetTimestamp();
                ApplyFrame(applyStartFrame.Value);
                if (setupTiming != null)
                {
                    setupTiming.initialSeedMs += TimelineSetupElapsedMilliseconds(phaseStart);
                }
            }

            return true;
        }

        private void TryConfigureNativeFirst(
            MmdMotionDefinition nativeMotion,
            byte[] pmxBytes,
            byte[] vmdBytes,
            Func<MmdMotionDefinition, MmdUnityPlaybackBinding> createBinding,
            Action<MmdUnityPlaybackBinding> configure,
            bool timelineEvaluation,
            MmdRuntimeFfiVmdContext? sharedVmdContext = null,
            string? sharedVmdContextFailure = null,
            MmdTimelineSetupTimingSummary? setupTiming = null)
        {
            long phaseStart = Stopwatch.GetTimestamp();
            bool nativeAvailable = TryCheckNativeRuntimeAvailability(
                pmxBytes,
                vmdBytes,
                out string nativeRuntimeFailure);
            if (setupTiming != null)
            {
                setupTiming.nativeAvailabilityMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            if (nativeAvailable)
            {
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
                    nativeBinding = createBinding(nativeMotion);
                    if (setupTiming != null)
                    {
                        setupTiming.sceneBindingMs += TimelineSetupElapsedMilliseconds(phaseStart);
                    }
                    phaseStart = Stopwatch.GetTimestamp();
                    if (TryEnableNativeRuntime(
                            nativeBinding,
                            pmxBytes,
                            vmdBytes,
                            sharedVmdContext,
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
                    if (nativeBinding != null)
                    {
                        ClearFailedBindingReference(nativeBinding);
                    }
                    nativeBinding?.Dispose();
                }
            }

            string finalNativeRuntimeFailure = ComposeNativeRuntimeFailure(
                sharedVmdContextFailure,
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

        private static string ResolveAssetSourceId(MmdVmdAsset asset)
        {
            return string.IsNullOrWhiteSpace(asset.SourceId) ? asset.name : asset.SourceId;
        }

        private static string ResolveAssetSourceId(MmdPmxAsset asset)
        {
            return string.IsNullOrWhiteSpace(asset.SourceId) ? asset.name : asset.SourceId;
        }

        private string ResolveProviderModelSourceId()
        {
            MmdPmxAsset? sourceAsset = ModelAssetSource;
            if (sourceAsset != null)
            {
                return ResolveAssetSourceId(sourceAsset);
            }

            MmdRuntimeImporterComponent? importer = GetComponent<MmdRuntimeImporterComponent>();
            return importer != null ? importer.ModelPath : string.Empty;
        }

        private string ResolveProviderMotionSourceId()
        {
            MmdVmdAsset? motionAsset = MotionAssetSource;
            if (motionAsset != null)
            {
                return ResolveAssetSourceId(motionAsset);
            }

            MmdRuntimeImporterComponent? importer = GetComponent<MmdRuntimeImporterComponent>();
            return importer != null ? importer.MotionPath : string.Empty;
        }
    }
}
