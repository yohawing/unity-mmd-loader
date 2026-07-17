#nullable enable

using System;
using System.IO;
using Mmd;
using Mmd.Parser;
using Mmd.Rendering;
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

            ConfigureFallbackAssetBinding(
                pmxAsset,
                vmdAsset,
                fallbackBinding => Configure(fallbackBinding, config),
                applyStartFrame: null);
        }

        public void ConfigureFromRuntimeImporterPaths(
            string pmxPath,
            string vmdPath,
            MmdPlaybackConfig config,
            bool allowRuntimeFallback = true,
            MmdMaterialPreset materialPreset = MmdMaterialPreset.MmdToon)
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
            MmdUnityPlaybackBinding? runtimeImporterBinding = null;
            bool reboundExisting = false;
            try
            {
                MmdUnityModelFactory.ValidateExistingSkinnedModelCompatibility(gameObject, model);
                ReleaseCurrentBindingBeforeSceneRebind();
                runtimeImporterBinding = MmdUnityPlaybackBinding.CreateSkinnedFromExistingSceneModel(
                    gameObject,
                    model,
                    motion,
                    resolvedPmxPath,
                    resolvedVmdPath,
                    resolvedPmxPath);
                reboundExisting = true;
            }
            catch (MissingComponentException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            if (!reboundExisting || runtimeImporterBinding == null)
            {
                if (!allowRuntimeFallback)
                {
                    throw new InvalidOperationException(
                        "Timeline evaluation requires an existing scene PMX model with a SkinnedMeshRenderer " +
                        "to bind motion. The runtime importer path (" + resolvedPmxPath +
                        ") could not find a matching SkinnedMeshRenderer in the scene.");
                }

                runtimeImporterBinding = MmdUnityPlaybackBinding.CreateSkinned(
                    model,
                    motion,
                    resolvedPmxPath,
                    resolvedVmdPath,
                    resolvedPmxPath,
                    materialPreset);
                AttachRestoredRuntimeInstance(runtimeImporterBinding.Instance);
            }

            Configure(runtimeImporterBinding, config);
            TryEnableFastRuntimeFromBytesForDefaultPlayback(pmxBytes, vmdBytes);
            if (!reboundExisting)
            {
                HidePreviewRenderersIfFallbackVisible(runtimeImporterBinding.Instance, resolvedPmxPath);
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
            bool playOnStart = true,
            bool allowRuntimeFallback = true)
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
            MmdMotionDefinition motion = allowRuntimeFallback
                ? vmdAsset.LoadMotion()
                : vmdAsset.CreateNativeClipMotionHeader();
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
                    requireNativeClip: !allowRuntimeFallback,
                    applyStartFrame: startFrame))
                {
                    return;
                }

                if (!allowRuntimeFallback)
                {
                    throw new InvalidOperationException(
                        "Timeline evaluation requires an existing scene PMX model with a SkinnedMeshRenderer " +
                        "to bind motion. Provide a scene GameObject with a controller PMX source and a " +
                        "SkinnedMeshRenderer matching the provider model source (" +
                        ResolveAssetSourceId(providerPmxAsset) + ").");
                }

                ConfigureFallbackAssetBinding(
                    providerPmxAsset,
                    vmdAsset,
                    fallbackBinding => Configure(fallbackBinding, playbackFrameRate, playOnStart),
                    applyStartFrame: startFrame);
                return;
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

            MmdUnityPlaybackBinding? pathBinding = null;
            bool reboundExisting = false;
            try
            {
                MmdUnityModelFactory.ValidateExistingSkinnedModelCompatibility(gameObject, model);
                ReleaseCurrentBindingBeforeSceneRebind();
                pathBinding = MmdUnityPlaybackBinding.CreateSkinnedFromExistingSceneModel(
                    gameObject,
                    model,
                    motion,
                    resolvedPmxPath,
                    string.IsNullOrWhiteSpace(vmdAsset.SourceId) ? vmdAsset.name : vmdAsset.SourceId,
                    resolvedPmxPath);
                reboundExisting = true;
            }
            catch (MissingComponentException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            if (!reboundExisting || pathBinding == null)
            {
                if (!allowRuntimeFallback)
                {
                    throw new InvalidOperationException(
                        "Timeline evaluation requires an existing scene PMX model with a SkinnedMeshRenderer " +
                        "to bind motion. The runtime importer path (" + resolvedPmxPath +
                        ") could not find a matching SkinnedMeshRenderer in the scene.");
                }

                pathBinding = MmdUnityPlaybackBinding.CreateSkinned(
                    model,
                    motion,
                    resolvedPmxPath,
                    string.IsNullOrWhiteSpace(vmdAsset.SourceId) ? vmdAsset.name : vmdAsset.SourceId,
                    resolvedPmxPath);
                AttachRestoredRuntimeInstance(pathBinding.Instance);
            }

            Configure(pathBinding, playbackFrameRate, playOnStart);
            TryEnableFastRuntimeFromBytesForDefaultPlayback(
                File.ReadAllBytes(resolvedPmxPath),
                vmdAsset.GetBytesCopy());
            if (!allowRuntimeFallback)
            {
                ThrowIfTimelineNativeClipUnavailable();
            }

            ApplyFrame(startFrame);
            if (!reboundExisting)
            {
                HidePreviewRenderersIfFallbackVisible(pathBinding.Instance, resolvedPmxPath);
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
            bool playOnStart = true,
            bool allowRuntimeFallback = true)
        {
            if (startFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startFrame), "Initial frame must not be negative.");
            }

            ConfigureModelAsset(pmxAsset);
            ConfigureMotionAsset(vmdAsset);
            var parser = new NativeMmdParser();
            MmdMotionDefinition motion = allowRuntimeFallback
                ? vmdAsset.LoadMotion(parser)
                : vmdAsset.CreateNativeClipMotionHeader();
            MmdMotionValidator.ThrowIfInvalid(motion);

            if (TryConfigureReboundAssetBinding(
                pmxAsset,
                vmdAsset,
                motion,
                reboundBinding => Configure(reboundBinding, playbackFrameRate, playOnStart),
                requireNativeClip: !allowRuntimeFallback,
                applyStartFrame: startFrame))
            {
                return;
            }

            if (!allowRuntimeFallback)
            {
                throw new InvalidOperationException(
                    "Timeline evaluation requires an existing scene PMX model with a SkinnedMeshRenderer " +
                    "to bind motion. Provide a scene GameObject with a controller PMX source and a " +
                    "SkinnedMeshRenderer matching the provider model source (" +
                    ResolveAssetSourceId(pmxAsset) + ").");
            }

            // Fallback to runtime generation (explicit, consistent with ConfigureFromPlaybackSource / ConfigureMotionFromProviderModelSource).
            ConfigureFallbackAssetBinding(
                pmxAsset,
                vmdAsset,
                assetBinding => Configure(assetBinding, playbackFrameRate, playOnStart),
                applyStartFrame: startFrame);
        }

        public bool ConfigureFromPlaybackSourceIfAvailable(bool allowRuntimeFallback = true)
        {
            MmdRuntimeImporterComponent? runtimeImporter = GetComponent<MmdRuntimeImporterComponent>();
            if (runtimeImporter != null && runtimeImporter.TryConfigureController(this, allowRuntimeFallback))
            {
                return true;
            }

            // Asset source path: controller fields are primary.
            // Enables auto-config on PlayMode Start / domain reload for authored scenes.
            MmdPmxAsset? modelAsset = ModelAssetSource;
            MmdVmdAsset? motionAsset = MotionAssetSource;
            if (modelAsset != null && motionAsset != null)
            {
                ConfigureFromAssets(
                    modelAsset,
                    motionAsset,
                    frameRate,
                    initialFrame,
                    playOnStart,
                    allowRuntimeFallback);
                return true;
            }

            return false;
        }

        private void AttachRestoredRuntimeInstance(MmdUnityModelInstance instance)
        {
            instance.Root.name = gameObject.name + " Runtime";
            instance.Root.transform.SetParent(transform.parent, worldPositionStays: false);
            instance.Root.transform.localPosition = transform.localPosition;
            instance.Root.transform.localRotation = transform.localRotation;
            instance.Root.transform.localScale = transform.localScale;
            MmdTransientRuntimeInstanceMarker marker =
                instance.Root.GetComponent<MmdTransientRuntimeInstanceMarker>() ??
                instance.Root.AddComponent<MmdTransientRuntimeInstanceMarker>();
            marker.Initialize(this, instance);
        }

        private bool TryCreateExistingSceneBinding(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            MmdMotionDefinition motion,
            out MmdUnityPlaybackBinding binding)
        {
            try
            {
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
            catch (MissingComponentException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            binding = null!;
            return false;
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

        private void ConfigureFallbackAssetBinding(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            Action<MmdUnityPlaybackBinding> configure,
            int? applyStartFrame)
        {
            MmdUnityPlaybackBinding fallbackBinding = MmdUnityPlaybackBinding.CreateSkinned(pmxAsset, vmdAsset);
            AttachRestoredRuntimeInstance(fallbackBinding.Instance);
            configure(fallbackBinding);
            TryEnableFastRuntimeFromAssetBytesForDefaultPlayback(pmxAsset, vmdAsset);
            if (applyStartFrame.HasValue)
            {
                ApplyFrame(applyStartFrame.Value);
            }

            HidePreviewRenderersIfFallbackVisible(fallbackBinding.Instance, ResolveAssetSourceId(pmxAsset));
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

        private void HidePreviewRenderersIfFallbackVisible(MmdUnityModelInstance fallbackInstance, string reason)
        {
            int runtimeRendererCount = CountEnabledRenderers(fallbackInstance.Root);
            if (runtimeRendererCount <= 0)
            {
                Debug.LogWarning(
                    "MMD restore kept preview renderers enabled because the fallback runtime has no enabled renderers. reason=" + reason,
                    this);
                return;
            }

            Transform? modelRoot = transform.Find("Model");
            Renderer? previewRenderer = modelRoot != null
                ? modelRoot.GetComponent<SkinnedMeshRenderer>()
                : null;
            if (previewRenderer == null && modelRoot != null)
            {
                previewRenderer = modelRoot.GetComponent<MeshRenderer>();
            }
            if (previewRenderer == null)
            {
                Debug.LogWarning(
                    "MMD restore kept Scene renderers enabled because no canonical package Model renderer was found. reason=" + reason,
                    this);
                return;
            }

            MmdTransientRuntimeInstanceMarker marker =
                fallbackInstance.Root.GetComponent<MmdTransientRuntimeInstanceMarker>() ??
                throw new InvalidOperationException("Fallback runtime instance is missing its ownership marker.");
            bool wasEnabled = previewRenderer.enabled;
            marker.CaptureAndDisableBorrowedRenderer(previewRenderer);
            Debug.Log("MMD restore used fallback runtime. preview-hidden=" + wasEnabled + " hidden-count=" + (wasEnabled ? 1 : 0) + " runtime-renderer-count=" + runtimeRendererCount + " reason=" + reason, this);
        }

        private static int CountEnabledRenderers(GameObject root)
        {
            if (root == null)
            {
                return 0;
            }

            int count = 0;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.enabled)
                {
                    count++;
                }
            }

            return count;
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
