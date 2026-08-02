#nullable enable

using System;
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
            MmdPlaybackConfig config)
        {
            ConfigureFromRuntimeImporterPathsCore(pmxPath, vmdPath, config, timelineEvaluation: true);
        }

        private void ConfigureFromRuntimeImporterPathsCore(
            string pmxPath,
            string vmdPath,
            MmdPlaybackConfig config,
            bool timelineEvaluation)
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

            byte[] pmxBytes = File.ReadAllBytes(resolvedPmxPath);
            byte[] vmdBytes = File.ReadAllBytes(resolvedVmdPath);
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(pmxBytes);
            MmdModelValidator.ThrowIfInvalid(model);
            MmdVmdParseSummary summary = MmdVmdBinarySummaryReader.Read(vmdBytes);
            MmdMotionDefinition motion = MmdVmdAsset.CreateNativeClipMotionHeader(vmdBytes, summary);
            MmdMotionValidator.ThrowIfInvalid(motion);
            if (!HasExistingSceneSkinnedMeshRenderer())
            {
                throw CreateMissingSceneModelException(resolvedPmxPath, timelineEvaluation);
            }

            MmdUnityModelFactory.ValidateExistingSkinnedModelCompatibility(gameObject, model);
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
                candidate => Configure(candidate, config),
                timelineEvaluation);
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
            bool playOnStart = false)
        {
            ConfigureMotionFromProviderModelSourceCore(
                vmdAsset,
                playbackFrameRate,
                startFrame,
                playOnStart,
                timelineEvaluation: true);
        }

        private void ConfigureMotionFromProviderModelSourceCore(
            MmdVmdAsset vmdAsset,
            float playbackFrameRate,
            int startFrame,
            bool playOnStart,
            bool timelineEvaluation)
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
            MmdMotionDefinition motion = vmdAsset.CreateNativeClipMotionHeader();
            MmdMotionValidator.ThrowIfInvalid(motion);

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
                    applyStartFrame: startFrame))
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
            MmdModelDefinition model = parser.LoadModel(File.ReadAllBytes(resolvedPmxPath));
            MmdModelValidator.ThrowIfInvalid(model);

            if (!HasExistingSceneSkinnedMeshRenderer())
            {
                throw CreateMissingSceneModelException(resolvedPmxPath, timelineEvaluation);
            }

            MmdUnityModelFactory.ValidateExistingSkinnedModelCompatibility(gameObject, model);
            byte[] pathPmxBytes = File.ReadAllBytes(resolvedPmxPath);
            TryConfigureNativeFirst(
                motion,
                pathPmxBytes,
                vmdAsset.GetBytesCopy(),
                nativeMotion => MmdUnityPlaybackBinding.CreateSkinnedFromExistingSceneModel(
                    gameObject,
                    model,
                    nativeMotion,
                    resolvedPmxPath,
                    string.IsNullOrWhiteSpace(vmdAsset.SourceId) ? vmdAsset.name : vmdAsset.SourceId,
                    resolvedPmxPath),
                candidate => Configure(candidate, playbackFrameRate, playOnStart),
                timelineEvaluation);

            ApplyFrame(startFrame);
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

        internal bool ConfigureFromPlaybackSourceIfAvailableForTimeline()
        {
            return ConfigureFromPlaybackSourceIfAvailableCore(timelineEvaluation: true);
        }

        private bool ConfigureFromPlaybackSourceIfAvailableCore(bool timelineEvaluation)
        {
            MmdRuntimeImporterComponent? runtimeImporter = GetComponent<MmdRuntimeImporterComponent>();
            if (runtimeImporter != null && (timelineEvaluation
                ? runtimeImporter.TryConfigureControllerForTimeline(this)
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
                        playOnStart);
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
            int? applyStartFrame)
        {
            if (!HasExistingSceneSkinnedMeshRenderer())
            {
                return false;
            }

            var parser = new NativeMmdParser();
            MmdModelDefinition model = pmxAsset.LoadModel(parser);
            MmdModelValidator.ThrowIfInvalid(model);
            MmdUnityModelFactory.ValidateExistingSkinnedModelCompatibility(gameObject, model);
            byte[] pmxBytes = pmxAsset.GetBytesCopy();
            byte[] vmdBytes = motion.sourceBytes
                ?? throw new InvalidOperationException("Native VMD motion source bytes are required.");
            vmdAsset.TryGetOrCreateNativeVmdContext(
                out MmdRuntimeFfiVmdContext? sharedVmdContext,
                out string sharedVmdContextFailure);
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
                sharedVmdContextFailure);

            if (applyStartFrame.HasValue)
            {
                ApplyFrame(applyStartFrame.Value);
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
            string? sharedVmdContextFailure = null)
        {
            bool nativeAvailable = TryCheckNativeRuntimeAvailability(
                pmxBytes,
                vmdBytes,
                out string nativeRuntimeFailure);
            if (nativeAvailable)
            {
                ReleaseCurrentBindingBeforeSceneRebind();
                MmdUnityPlaybackBinding? nativeBinding = null;
                try
                {
                    nativeBinding = createBinding(nativeMotion);
                    if (TryEnableNativeRuntime(
                            nativeBinding,
                            pmxBytes,
                            vmdBytes,
                            sharedVmdContext,
                            out nativeRuntimeFailure))
                    {
                        lastFastRuntimeReason = string.Empty;
                        configure(nativeBinding);
                        nativeBinding = null;
                        return;
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
            ReleaseCurrentBindingBeforeSceneRebind();
            throw CreateNativeClipPlaybackUnavailableException(finalNativeRuntimeFailure, timelineEvaluation);
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
