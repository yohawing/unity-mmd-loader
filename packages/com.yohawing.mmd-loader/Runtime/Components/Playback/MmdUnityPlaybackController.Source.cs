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
                ResetHumanoidPhysicsBindingFailureReason();
            }

            modelAsset = pmxAsset;
            _ = pmxAsset.BeginSynchronousPlaybackPreload(
                MmdUnityPlaybackBinding.ResolveMaterialPreset(pmxAsset));
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
            ConfigureFromAssetSourceCore(
                pmxAsset,
                vmdAsset,
                reboundBinding => Configure(reboundBinding, config),
                timelineEvaluation: false,
                applyStartFrame: null);
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

            MmdPmxRuntimeParseCache.Result cachedPmx = MmdPmxRuntimeParseCache.Load(resolvedPmxPath);
            byte[] pmxBytes = cachedPmx.Bytes;
            long phaseStart = Stopwatch.GetTimestamp();
            byte[] vmdBytes = File.ReadAllBytes(resolvedVmdPath);
            if (setupTiming != null)
            {
                setupTiming.sourceAcquireMs += cachedPmx.SourceAcquireMs;
                setupTiming.sourceAcquireMs += TimelineSetupElapsedMilliseconds(phaseStart);
                setupTiming.pmxParseMs += cachedPmx.ParseMs;
                setupTiming.pmxParseCacheHit = cachedPmx.CacheHit;
            }
            MmdModelDefinition model = cachedPmx.Model;
            phaseStart = Stopwatch.GetTimestamp();
            MmdVmdParseSummary summary = MmdVmdNativeSummaryAdapter.Read(vmdBytes);
            MmdMotionDefinition motion = MmdVmdAsset.CreateNativeClipMotionHeader(vmdBytes, summary);
            MmdMotionValidator.ThrowIfInvalid(motion);
            if (setupTiming != null)
            {
                setupTiming.motionHeaderMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            if (!TryValidateExistingSceneModelCompatibility(model, setupTiming))
            {
                throw CreateMissingSceneModelException(resolvedPmxPath, timelineEvaluation);
            }

            ConfigureNativePlaybackSetup(
                new MmdNativePlaybackSetup(motion, pmxBytes, vmdBytes),
                nativeMotion => MmdUnityPlaybackBinding.CreateSkinnedFromExistingSceneModel(
                    gameObject,
                    model,
                    nativeMotion,
                    resolvedPmxPath,
                    resolvedVmdPath,
                    resolvedPmxPath,
                    playbackDescriptor: null,
                    sourcesAlreadyValidated: true),
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
                applyStartFrame: null,
                setupTiming: setupTiming);
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
                    applyStartFrame: timelineEvaluation ? null : startFrame,
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

            MmdPmxRuntimeParseCache.Result cachedPmx = MmdPmxRuntimeParseCache.Load(resolvedPmxPath);
            MmdModelDefinition model = cachedPmx.Model;
            if (setupTiming != null)
            {
                setupTiming.sourceAcquireMs += cachedPmx.SourceAcquireMs;
                setupTiming.pmxParseMs += cachedPmx.ParseMs;
                setupTiming.pmxParseCacheHit = cachedPmx.CacheHit;
            }

            if (!TryValidateExistingSceneModelCompatibility(model, setupTiming))
            {
                throw CreateMissingSceneModelException(resolvedPmxPath, timelineEvaluation);
            }

            phaseStart = Stopwatch.GetTimestamp();
            byte[] pathPmxBytes = cachedPmx.Bytes;
            byte[] pathVmdBytes = vmdAsset.GetBytesCopy();
            if (setupTiming != null)
            {
                setupTiming.sourceAcquireMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            phaseStart = Stopwatch.GetTimestamp();
            vmdAsset.TryGetOrCreateNativeVmdContext(
                out MmdRuntimeFfiVmdContext? sharedVmdContext,
                out string sharedVmdContextFailure);
            if (setupTiming != null)
            {
                setupTiming.sharedVmdContextMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            ConfigureNativePlaybackSetup(
                new MmdNativePlaybackSetup(
                    motion,
                    pathPmxBytes,
                    pathVmdBytes,
                    sharedVmdContext,
                    sharedVmdContextFailure),
                nativeMotion => MmdUnityPlaybackBinding.CreateSkinnedFromExistingSceneModel(
                    gameObject,
                    model,
                    nativeMotion,
                    resolvedPmxPath,
                    string.IsNullOrWhiteSpace(vmdAsset.SourceId) ? vmdAsset.name : vmdAsset.SourceId,
                    resolvedPmxPath,
                    playbackDescriptor: null,
                    sourcesAlreadyValidated: true),
                candidate => Configure(candidate, playbackFrameRate, playOnStart),
                timelineEvaluation,
                applyStartFrame: timelineEvaluation ? null : startFrame,
                setupTiming: setupTiming);
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

            ConfigureFromAssetSourceCore(
                pmxAsset,
                vmdAsset,
                reboundBinding => Configure(reboundBinding, playbackFrameRate, playOnStart),
                timelineEvaluation: false,
                applyStartFrame: startFrame);
        }

        private void ConfigureFromAssetSourceCore(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            Action<MmdUnityPlaybackBinding> configure,
            bool timelineEvaluation,
            int? applyStartFrame)
        {
            ConfigureModelAsset(pmxAsset);
            ConfigureMotionAsset(vmdAsset);
            MmdMotionDefinition motion = vmdAsset.CreateNativeClipMotionHeader();
            MmdMotionValidator.ThrowIfInvalid(motion);

            if (TryConfigureReboundAssetBinding(
                pmxAsset,
                vmdAsset,
                motion,
                configure,
                timelineEvaluation,
                applyStartFrame))
            {
                return;
            }

            throw CreateMissingSceneModelException(
                ResolveAssetSourceId(pmxAsset),
                timelineEvaluation);
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

            long phaseStart = Stopwatch.GetTimestamp();
            MmdModelDefinition model = pmxAsset.LoadValidatedModelForSynchronousPlayback(
                new NativeMmdParser(),
                out bool pmxParseCacheHit);
            if (setupTiming != null)
            {
                if (!pmxParseCacheHit)
                {
                    setupTiming.pmxParseMs += TimelineSetupElapsedMilliseconds(phaseStart);
                }
                setupTiming.pmxParseCacheHit = pmxParseCacheHit;
            }
            ValidateExistingSceneModelCompatibility(model, setupTiming);
            phaseStart = Stopwatch.GetTimestamp();
            byte[] pmxBytes = pmxAsset.GetBytesForSynchronousRuntimeSetup();
            byte[] vmdBytes = motion.sourceBytes
                ?? throw new InvalidOperationException("Native VMD motion source bytes are required.");
            if (setupTiming != null)
            {
                setupTiming.sourceAcquireMs += TimelineSetupElapsedMilliseconds(phaseStart);
                setupTiming.pmxSourceBufferBorrowed = true;
            }
            phaseStart = Stopwatch.GetTimestamp();
            vmdAsset.TryGetOrCreateNativeVmdContext(
                out MmdRuntimeFfiVmdContext? sharedVmdContext,
                out string sharedVmdContextFailure);
            if (setupTiming != null)
            {
                setupTiming.sharedVmdContextMs += TimelineSetupElapsedMilliseconds(phaseStart);
            }
            ConfigureNativePlaybackSetup(
                new MmdNativePlaybackSetup(
                    motion,
                    pmxBytes,
                    vmdBytes,
                    sharedVmdContext,
                    sharedVmdContextFailure),
                nativeMotion => MmdUnityPlaybackBinding.CreateSkinnedFromExistingSceneModel(
                    gameObject,
                    pmxAsset,
                    model,
                    vmdAsset,
                    nativeMotion,
                    sourcesAlreadyValidated: true),
                configure,
                timelineEvaluation,
                applyStartFrame,
                setupTiming);

            return true;
        }

        private void ConfigureNativePlaybackSetup(
            MmdNativePlaybackSetup setup,
            Func<MmdMotionDefinition, MmdUnityPlaybackBinding> createBinding,
            Action<MmdUnityPlaybackBinding> configure,
            bool timelineEvaluation,
            int? applyStartFrame,
            MmdTimelineSetupTimingSummary? setupTiming = null)
        {
            TryConfigureNativeFirst(
                setup,
                createBinding,
                configure,
                timelineEvaluation,
                setupTiming);

            if (applyStartFrame.HasValue)
            {
                ApplyReboundStartFrameWithRollback(applyStartFrame.Value, setupTiming);
            }
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
