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
            if (TryCreateExistingSceneBinding(pmxAsset, vmdAsset, motion, out MmdUnityPlaybackBinding reboundBinding))
            {
                Configure(reboundBinding, config);
                TryEnableFastRuntimeFromAssetBytesForDefaultPlayback(pmxAsset, vmdAsset);
                return;
            }

            MmdUnityPlaybackBinding fallbackBinding = MmdUnityPlaybackBinding.CreateSkinned(pmxAsset, vmdAsset);
            AttachRestoredRuntimeInstance(fallbackBinding.Instance);
            Configure(fallbackBinding, config);
            TryEnableFastRuntimeFromAssetBytesForDefaultPlayback(pmxAsset, vmdAsset);
            HidePreviewRenderersIfFallbackVisible(fallbackBinding.Instance, ResolveAssetSourceId(pmxAsset));
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
            MmdUnityModelInstance? existingInstance = null;
            bool reboundExisting = false;
            try
            {
                existingInstance = MmdUnityModelFactory.CreateExistingSkinnedModelInstance(
                    gameObject,
                    model,
                    resolvedPmxPath);
                reboundExisting = true;
            }
            catch (MissingComponentException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            MmdUnityPlaybackBinding runtimeImporterBinding;
            if (reboundExisting && existingInstance != null)
            {
                MmdVmdAsset runtimeMotionAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
                runtimeMotionAsset.Initialize(vmdBytes, Path.GetFileName(resolvedVmdPath), resolvedVmdPath);

                try
                {
                    runtimeImporterBinding = MmdUnityPlaybackBinding.CreateSkinned(
                        existingInstance,
                        model,
                        runtimeMotionAsset,
                        motion,
                        resolvedPmxPath,
                        resolvedPmxPath);
                }
                finally
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(runtimeMotionAsset);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(runtimeMotionAsset);
                    }
                }
            }
            else
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
                if (TryCreateExistingSceneBinding(providerPmxAsset, vmdAsset, motion, out MmdUnityPlaybackBinding reboundBinding))
                {
                    Configure(reboundBinding, playbackFrameRate, playOnStart);
                    TryEnableFastRuntimeFromAssetBytesForDefaultPlayback(providerPmxAsset, vmdAsset);
                    if (!allowRuntimeFallback)
                    {
                        ThrowIfTimelineNativeClipUnavailable();
                    }

                    ApplyFrame(startFrame);
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

                MmdUnityPlaybackBinding fallbackBinding = MmdUnityPlaybackBinding.CreateSkinned(providerPmxAsset, vmdAsset);
                AttachRestoredRuntimeInstance(fallbackBinding.Instance);
                Configure(fallbackBinding, playbackFrameRate, playOnStart);
                TryEnableFastRuntimeFromAssetBytesForDefaultPlayback(providerPmxAsset, vmdAsset);
                HidePreviewRenderersIfFallbackVisible(fallbackBinding.Instance, ResolveAssetSourceId(providerPmxAsset));
                ApplyFrame(startFrame);
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

            MmdUnityModelInstance? existingInstance = null;
            bool reboundExisting = false;
            try
            {
                existingInstance = MmdUnityModelFactory.CreateExistingSkinnedModelInstance(
                    gameObject, model, resolvedPmxPath);
                reboundExisting = true;
            }
            catch (MissingComponentException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            MmdUnityPlaybackBinding pathBinding;
            if (reboundExisting && existingInstance != null)
            {
                pathBinding = MmdUnityPlaybackBinding.CreateSkinned(
                    existingInstance,
                    model,
                    vmdAsset,
                    motion,
                    resolvedPmxPath,
                    resolvedPmxPath);
            }
            else
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

            if (TryCreateExistingSceneBinding(pmxAsset, vmdAsset, motion, out MmdUnityPlaybackBinding reboundBinding))
            {
                Configure(reboundBinding, playbackFrameRate, playOnStart);
                TryEnableFastRuntimeFromAssetBytesForDefaultPlayback(pmxAsset, vmdAsset);
                if (!allowRuntimeFallback)
                {
                    ThrowIfTimelineNativeClipUnavailable();
                }

                ApplyFrame(startFrame);
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
            MmdUnityPlaybackBinding assetBinding = MmdUnityPlaybackBinding.CreateSkinned(pmxAsset, vmdAsset);
            AttachRestoredRuntimeInstance(assetBinding.Instance);
            Configure(assetBinding, playbackFrameRate, playOnStart);
            TryEnableFastRuntimeFromAssetBytesForDefaultPlayback(pmxAsset, vmdAsset);
            HidePreviewRenderersIfFallbackVisible(assetBinding.Instance, ResolveAssetSourceId(pmxAsset));
            ApplyFrame(startFrame);
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
        }

        private bool TryCreateExistingSceneBinding(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            MmdMotionDefinition motion,
            out MmdUnityPlaybackBinding binding)
        {
            try
            {
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
            LastEditableRigDiagnostics = null;
            IsPlaying = false;
            throw new InvalidOperationException(
                "Timeline evaluation requires mmd-anim native clip playback for VMD asset evaluation. " +
                "Fast runtime unavailable: " + reason);
        }

        private void HidePreviewRenderersIfFallbackVisible(MmdUnityModelInstance fallbackInstance, string reason)
        {
            int runtimeRendererCount = CountEnabledRenderers(fallbackInstance.Root);
            Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            if (runtimeRendererCount <= 0)
            {
                Debug.LogWarning(
                    "MMD restore kept preview renderers enabled because the fallback runtime has no enabled renderers. reason=" + reason,
                    this);
                return;
            }

            int hiddenCount = 0;
            Transform fallbackRoot = fallbackInstance.Root.transform;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer.transform.IsChildOf(fallbackRoot))
                {
                    continue;
                }

                if (renderer.enabled)
                {
                    hiddenCount++;
                }

                renderer.enabled = false;
            }

            Debug.Log("MMD restore used fallback runtime. preview-hidden=" + (hiddenCount > 0) + " hidden-count=" + hiddenCount + " runtime-renderer-count=" + runtimeRendererCount + " reason=" + reason, this);
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
