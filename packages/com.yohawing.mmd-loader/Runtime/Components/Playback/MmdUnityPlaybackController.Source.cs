#nullable enable

using System;
using System.IO;
using Mmd;
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
            MmdMotionDefinition motion = vmdAsset.LoadMotion();
            MmdMotionValidator.ThrowIfInvalid(motion);
            if (TryConfigureReboundAssetBinding(
                pmxAsset,
                vmdAsset,
                motion,
                reboundBinding => Configure(reboundBinding, config),
                requireNativeClip: false,
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
            ConfigureFromRuntimeImporterPathsCore(pmxPath, vmdPath, config, requireNativeClip: false);
        }

        internal void ConfigureFromRuntimeImporterPathsForTimeline(
            string pmxPath,
            string vmdPath,
            MmdPlaybackConfig config)
        {
            ConfigureFromRuntimeImporterPathsCore(pmxPath, vmdPath, config, requireNativeClip: true);
        }

        private void ConfigureFromRuntimeImporterPathsCore(
            string pmxPath,
            string vmdPath,
            MmdPlaybackConfig config,
            bool requireNativeClip)
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
            MmdMotionDefinition motion = parser.LoadMotion(vmdBytes);
            MmdMotionValidator.ThrowIfInvalid(motion);
            if (!HasExistingSceneSkinnedMeshRenderer())
            {
                throw CreateMissingSceneModelException(resolvedPmxPath, requireNativeClip);
            }

            MmdUnityModelFactory.ValidateExistingSkinnedModelCompatibility(gameObject, model);
            ReleaseCurrentBindingBeforeSceneRebind();
            MmdUnityPlaybackBinding runtimeImporterBinding =
                MmdUnityPlaybackBinding.CreateSkinnedFromExistingSceneModel(
                    gameObject,
                    model,
                    motion,
                    resolvedPmxPath,
                    resolvedVmdPath,
                    resolvedPmxPath);

            Configure(runtimeImporterBinding, config);
            TryEnableFastRuntimeFromBytesForDefaultPlayback(pmxBytes, vmdBytes);
            if (requireNativeClip)
            {
                ThrowIfTimelineNativeClipUnavailable();
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
                requireNativeClip: false);
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
                requireNativeClip: true);
        }

        private void ConfigureMotionFromProviderModelSourceCore(
            MmdVmdAsset vmdAsset,
            float playbackFrameRate,
            int startFrame,
            bool playOnStart,
            bool requireNativeClip)
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
            MmdMotionDefinition motion = requireNativeClip
                ? vmdAsset.CreateNativeClipMotionHeader()
                : vmdAsset.LoadMotion();
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
                    requireNativeClip,
                    applyStartFrame: startFrame))
                {
                    return;
                }

                throw CreateMissingSceneModelException(
                    ResolveAssetSourceId(providerPmxAsset),
                    requireNativeClip);
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
                throw CreateMissingSceneModelException(resolvedPmxPath, requireNativeClip);
            }

            MmdUnityModelFactory.ValidateExistingSkinnedModelCompatibility(gameObject, model);
            ReleaseCurrentBindingBeforeSceneRebind();
            MmdUnityPlaybackBinding pathBinding =
                MmdUnityPlaybackBinding.CreateSkinnedFromExistingSceneModel(
                    gameObject,
                    model,
                    motion,
                    resolvedPmxPath,
                    string.IsNullOrWhiteSpace(vmdAsset.SourceId) ? vmdAsset.name : vmdAsset.SourceId,
                    resolvedPmxPath);

            Configure(pathBinding, playbackFrameRate, playOnStart);
            TryEnableFastRuntimeFromBytesForDefaultPlayback(
                File.ReadAllBytes(resolvedPmxPath),
                vmdAsset.GetBytesCopy());
            if (requireNativeClip)
            {
                ThrowIfTimelineNativeClipUnavailable();
            }

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
            var parser = new NativeMmdParser();
            MmdMotionDefinition motion = vmdAsset.LoadMotion(parser);
            MmdMotionValidator.ThrowIfInvalid(motion);

            if (TryConfigureReboundAssetBinding(
                pmxAsset,
                vmdAsset,
                motion,
                reboundBinding => Configure(reboundBinding, playbackFrameRate, playOnStart),
                requireNativeClip: false,
                applyStartFrame: startFrame))
            {
                return;
            }

            throw CreateMissingSceneModelException(ResolveAssetSourceId(pmxAsset), timelineEvaluation: false);
        }

        public bool ConfigureFromPlaybackSourceIfAvailable()
        {
            return ConfigureFromPlaybackSourceIfAvailableCore(requireNativeClip: false);
        }

        internal bool ConfigureFromPlaybackSourceIfAvailableForTimeline()
        {
            return ConfigureFromPlaybackSourceIfAvailableCore(requireNativeClip: true);
        }

        private bool ConfigureFromPlaybackSourceIfAvailableCore(bool requireNativeClip)
        {
            MmdRuntimeImporterComponent? runtimeImporter = GetComponent<MmdRuntimeImporterComponent>();
            if (runtimeImporter != null && (requireNativeClip
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
                if (requireNativeClip)
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

        private bool TryCreateExistingSceneBinding(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            MmdMotionDefinition motion,
            out MmdUnityPlaybackBinding binding)
        {
            if (!HasExistingSceneSkinnedMeshRenderer())
            {
                binding = null!;
                return false;
            }

            var parser = new NativeMmdParser();
            MmdModelDefinition model = pmxAsset.LoadModel(parser);
            MmdUnityModelFactory.ValidateExistingSkinnedModelCompatibility(gameObject, model);
            ReleaseCurrentBindingBeforeSceneRebind();
            binding = MmdUnityPlaybackBinding.CreateSkinnedFromExistingSceneModel(
                gameObject,
                pmxAsset,
                vmdAsset,
                motion);
            return true;
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
            bool requireNativeClip,
            int? applyStartFrame)
        {
            if (!TryCreateExistingSceneBinding(pmxAsset, vmdAsset, motion, out MmdUnityPlaybackBinding reboundBinding))
            {
                return false;
            }

            configure(reboundBinding);
            TryEnableFastRuntimeFromAssetBytesForDefaultPlayback(pmxAsset, vmdAsset);
            if (requireNativeClip)
            {
                ThrowIfTimelineNativeClipUnavailable();
            }

            if (applyStartFrame.HasValue)
            {
                ApplyFrame(applyStartFrame.Value);
            }

            return true;
        }

        private void ThrowIfTimelineNativeClipUnavailable()
        {
            if (IsFastRuntimeEnabled)
            {
                return;
            }

            string reason = string.IsNullOrWhiteSpace(lastFastRuntimeReason)
                ? "unknown fast runtime failure"
                : lastFastRuntimeReason;
            binding?.Dispose();
            binding = null;
            LastSnapshot = null;
            IsPlaying = false;
            throw new InvalidOperationException(
                "Timeline evaluation requires mmd-anim native clip playback for VMD asset evaluation. " +
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
