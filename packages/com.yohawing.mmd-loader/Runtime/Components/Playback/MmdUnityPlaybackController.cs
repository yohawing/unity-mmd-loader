#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Mmd;
using Mmd.Parser;
using Mmd.Physics;

namespace Mmd.UnityIntegration
{
    public enum MmdHumanoidRetargetGate
    {
        Ready = 0,
        ComponentDisabled = 1,
        MissingHumanAnimator = 2,
        AnimatorNotDriven = 3,
        PlaybackControllerDriving = 4,
        MissingBindings = 5
    }

    internal enum LivePhysicsDriveSource
    {
        None = 0,
        VmdForward = 1,
        HumanoidRetarget = 2
    }

    public sealed class MmdUnityPlaybackController : MonoBehaviour
    {
        private MmdUnityPlaybackBinding? binding;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private int initialFrame;
        [SerializeField] private float frameRate = 30.0f;
        [SerializeField] private MmdPhysicsMode physicsMode = MmdPhysicsMode.Live;
        [SerializeField] private MmdPmxAsset? modelAsset;
        [SerializeField] private MmdVmdAsset? motionAsset;
        [SerializeField] private string lastFastRuntimeReason = string.Empty;
        [SerializeField] private Transform? proxyRoot;
        [SerializeField] private List<MmdHumanoidRetargetBinding> humanoidRetargetEntries = new();
        [SerializeField] private List<MmdHumanoidAppendTransformBinding> humanoidAppendEntries = new();
        private float playbackFrame;

        // Unity frame on which a Timeline/PlayableDirector last evaluated this controller. When a
        // Timeline drives playback it is the single source of time; the controller must NOT also
        // self-advance via Update().Tick() (double-driving Live physics with two diverging clocks
        // whips the 揺れもの between frames and destabilizes scrub/seek). Sentinel keeps standalone
        // (non-Timeline) playback self-driving.
        private int lastTimelineDriveFrameCount = int.MinValue / 2;

        private int lastHumanoidRetargetTimelineDriveFrameCount = int.MinValue / 2;
        private int lastVmdLivePhysicsFrameCount = int.MinValue / 2;
        private int humanoidLivePhysicsStepIndex = -1;
        private int lastHumanoidLivePhysicsFrameCount = int.MinValue / 2;
        private LivePhysicsDriveSource livePhysicsDriveSource = LivePhysicsDriveSource.None;
        private bool isApplyingPlaybackPose;

        private bool _userExplicitLive;

        public bool IsConfigured => binding != null;

        public bool IsPlaying { get; private set; }

        public int CurrentFrame { get; private set; }

        public float FrameRate => frameRate;

        public MmdPhysicsMode PhysicsMode => physicsMode;

        public bool PlayOnStart => playOnStart;

        public bool IsFastRuntimeEnabled => binding?.IsFastRuntimeEnabled ?? false;

        public string LastFastRuntimeReason => lastFastRuntimeReason;

        public int ConfigurationRevision { get; private set; }

        // SOURCE-OF-TRUTH: normal asset authoring keeps PMX/VMD source references on the controller.
        // RuntimeImporterComponent remains the raw-path owner.
        public bool HasModelSource => ModelAssetSource != null || !string.IsNullOrWhiteSpace(ModelSourceId);

        public MmdPmxAsset? ModelAssetSource => modelAsset;

        public MmdVmdAsset? MotionAssetSource => motionAsset;

        public string ModelSourceId => ResolveProviderModelSourceId();

        public string MotionSourceId => binding != null
            ? binding.MotionId
            : ResolveProviderMotionSourceId();

        public GameObject? ConfiguredInstanceRoot => binding?.Instance.Root;

        public MmdPlaybackSnapshot? LastSnapshot { get; private set; }

        public MmdEditableRigLayerDiagnostics? LastEditableRigDiagnostics { get; private set; }

        public MmdLivePhysicsFrameDiagnostics? LastLivePhysicsDiagnostics => binding?.LastLivePhysicsDiagnostics;

        public Transform? HumanoidProxyRoot => proxyRoot;

        public IReadOnlyList<MmdHumanoidRetargetBinding> HumanoidRetargetEntries => humanoidRetargetEntries;

        public IReadOnlyList<MmdHumanoidAppendTransformBinding> HumanoidAppendEntries => humanoidAppendEntries;

        public MmdHumanoidRetargetGate LastHumanoidRetargetGate { get; private set; } =
            MmdHumanoidRetargetGate.MissingBindings;

        public MmdHumanoidRetargeterResult? LastHumanoidRetargetResult { get; private set; }

        public void ConfigureModelAsset(MmdPmxAsset pmxAsset)
        {
            if (pmxAsset == null)
            {
                throw new ArgumentNullException(nameof(pmxAsset));
            }

            modelAsset = pmxAsset;
        }

        public void ConfigureHumanoidRetarget(
            Transform? proxyRoot,
            IReadOnlyList<MmdHumanoidRetargetBinding>? entries,
            IReadOnlyList<MmdHumanoidAppendTransformBinding>? appendEntries)
        {
            this.proxyRoot = proxyRoot;
            humanoidRetargetEntries = entries != null
                ? new List<MmdHumanoidRetargetBinding>(entries)
                : new List<MmdHumanoidRetargetBinding>();
            humanoidAppendEntries = appendEntries != null
                ? new List<MmdHumanoidAppendTransformBinding>(appendEntries)
                : new List<MmdHumanoidAppendTransformBinding>();
            LastHumanoidRetargetGate = EvaluateHumanoidRetargetGate(requireAnimatorDriver: true);
            LastHumanoidRetargetResult = null;
        }

        public MmdHumanoidRetargeterResult ApplyHumanoidRetargetNow()
        {
            MmdHumanoidRetargetGate gate = EvaluateHumanoidRetargetGate(requireAnimatorDriver: true);
            LastHumanoidRetargetGate = gate;
            if (gate != MmdHumanoidRetargetGate.Ready)
            {
                LastHumanoidRetargetResult = CreateHumanoidRetargetNoOpResult(gate);
                return LastHumanoidRetargetResult;
            }

            LastHumanoidRetargetResult = MmdHumanoidRetargeter.RetargetPose(humanoidRetargetEntries);
            MmdHumanoidAppendTransformApplier.Apply(humanoidAppendEntries);
            return LastHumanoidRetargetResult;
        }

        internal MmdHumanoidRetargeterResult ApplyHumanoidRetargetFromTimeline()
        {
            lastHumanoidRetargetTimelineDriveFrameCount = Time.frameCount;
            MmdHumanoidRetargetGate gate = EvaluateHumanoidRetargetGate(requireAnimatorDriver: false);
            LastHumanoidRetargetGate = gate;
            if (gate != MmdHumanoidRetargetGate.Ready)
            {
                LastHumanoidRetargetResult = CreateHumanoidRetargetNoOpResult(gate);
                return LastHumanoidRetargetResult;
            }

            LastHumanoidRetargetResult = MmdHumanoidRetargeter.RetargetPose(humanoidRetargetEntries);
            MmdHumanoidAppendTransformApplier.Apply(humanoidAppendEntries);
            return LastHumanoidRetargetResult;
        }

        internal MmdHumanoidRetargetGate EvaluateHumanoidRetargetGate(bool requireAnimatorDriver)
        {
            if (!isActiveAndEnabled)
            {
                return MmdHumanoidRetargetGate.ComponentDisabled;
            }

            Animator? animator = GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return MmdHumanoidRetargetGate.MissingHumanAnimator;
            }

            if (requireAnimatorDriver &&
                animator.runtimeAnimatorController == null &&
                !animator.hasBoundPlayables)
            {
                return MmdHumanoidRetargetGate.AnimatorNotDriven;
            }

            if (IsVmdDriving)
            {
                return MmdHumanoidRetargetGate.PlaybackControllerDriving;
            }

            if (proxyRoot == null || humanoidRetargetEntries == null || humanoidRetargetEntries.Count == 0)
            {
                return MmdHumanoidRetargetGate.MissingBindings;
            }

            return MmdHumanoidRetargetGate.Ready;
        }

        public void ConfigureMotionAsset(MmdVmdAsset vmdAsset)
        {
            if (vmdAsset == null)
            {
                throw new ArgumentNullException(nameof(vmdAsset));
            }

            motionAsset = vmdAsset;
        }

        public void Configure(MmdUnityPlaybackBinding playbackBinding, MmdPlaybackConfig config)
        {
            config.Validate();
            Configure(playbackBinding, config.FrameRate, config.PlayOnStart);
            ApplyFrame(config.InitialFrame);
        }

        public void Configure(MmdUnityPlaybackBinding playbackBinding, float playbackFrameRate, bool playOnStart = true)
        {
            if (playbackBinding == null)
            {
                throw new ArgumentNullException(nameof(playbackBinding));
            }

            ValidateFrameRate(playbackFrameRate);
            if (binding != null && !ReferenceEquals(binding, playbackBinding))
            {
                binding.Dispose();
            }

            binding = playbackBinding;
            frameRate = playbackFrameRate;
            playbackFrame = 0.0f;
            CurrentFrame = 0;
            LastSnapshot = null;
            LastEditableRigDiagnostics = null;
            IsPlaying = false;
            this.playOnStart = playOnStart;
            binding.SetPhysicsMode(physicsMode);
            ResetLivePhysicsDriveSource();
            ConfigurationRevision++;
        }

        public void SetPlayOnStart(bool value)
        {
            playOnStart = value;
        }

        public void SetPhysicsMode(MmdPhysicsMode mode)
        {
            ApplyPhysicsMode(mode, markUserExplicit: true);
        }

        internal bool IsUserExplicitLive => physicsMode == MmdPhysicsMode.Live && _userExplicitLive;

        private void ApplyPhysicsMode(MmdPhysicsMode mode, bool markUserExplicit)
        {
            if (binding != null)
            {
                ApplyPhysicsModeToBinding(mode);
            }
            else
            {
                ValidatePhysicsMode(mode);
            }

            physicsMode = mode;
            if (mode == MmdPhysicsMode.Live)
            {
                _userExplicitLive = markUserExplicit;
            }
            else
            {
                _userExplicitLive = false;
            }
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
            bool allowRuntimeFallback = true)
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
                    resolvedPmxPath);
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

        public bool TryEnableFastRuntime(byte[] pmxBytes, byte[] vmdBytes, out string reason)
        {
            if (binding == null)
            {
                reason = "Playback controller must be configured before enabling fast runtime.";
                return false;
            }

            return binding.TryEnableFastRuntime(pmxBytes, vmdBytes, out reason);
        }

        public bool TryEnableFastRuntimeFromConfiguredSource(out string reason)
        {
            if (binding == null)
            {
                reason = "Playback controller must be configured before enabling fast runtime.";
                return false;
            }

            try
            {
                // Split: model/motion from controller assets; raw paths from importer.
                MmdPmxAsset? modelAsset = ModelAssetSource;
                MmdVmdAsset? motionAsset = MotionAssetSource;
                if (modelAsset != null && motionAsset != null)
                {
                    return binding.TryEnableFastRuntime(
                        modelAsset.GetBytesCopy(),
                        motionAsset.GetBytesCopy(),
                        out reason);
                }

                MmdRuntimeImporterComponent? importer = GetComponent<MmdRuntimeImporterComponent>();
                if (importer != null &&
                    !string.IsNullOrWhiteSpace(importer.ModelPath) &&
                    !string.IsNullOrWhiteSpace(importer.MotionPath))
                {
                    string pmxFull = Path.GetFullPath(importer.ModelPath);
                    string vmdFull = Path.GetFullPath(importer.MotionPath);
                    if (!File.Exists(pmxFull))
                    {
                        reason = "Configured PMX file was not found: " + pmxFull;
                        return false;
                    }
                    if (!File.Exists(vmdFull))
                    {
                        reason = "Configured VMD file was not found: " + vmdFull;
                        return false;
                    }

                    return binding.TryEnableFastRuntime(
                        File.ReadAllBytes(pmxFull),
                        File.ReadAllBytes(vmdFull),
                        out reason);
                }

                reason = "A complete provider-owned PMX/VMD source (controller asset source, or MmdRuntimeImporterComponent with both paths) is required before enabling fast runtime from source.";
                return false;
            }
            catch (Exception ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public void DisableFastRuntime()
        {
            binding?.DisableFastRuntime();
            lastFastRuntimeReason = string.Empty;
        }

        public MmdPlaybackSnapshot ApplyFrame(int frame)
        {
            if (binding == null)
            {
                throw new InvalidOperationException("Playback controller must be configured before applying frames.");
            }

            if (frame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Frame index must not be negative.");
            }

            playbackFrame = frame;
            CurrentFrame = frame;
            return ApplyPlaybackPose(() => ApplyCurrentFrame());
        }

        public MmdPlaybackSnapshot ApplyTime(float time)
        {
            return ApplyTime(time, frameRate);
        }

        public MmdPlaybackSnapshot ApplyTime(float time, float playbackFrameRate)
        {
            if (binding == null)
            {
                throw new InvalidOperationException("Playback controller must be configured before applying time.");
            }

            MmdPlaybackTime.ValidateFrameRate(playbackFrameRate);
            int frame = MmdPlaybackTime.ToFrame(time, playbackFrameRate);
            playbackFrame = frame;
            CurrentFrame = frame;
            return ApplyPlaybackPose(() =>
            {
                LastSnapshot = binding.ApplyTime(time, playbackFrameRate);
                ApplyEditableRigLayer("post-native-apply-time");
                return LastSnapshot;
            });
        }

        public void Play()
        {
            EnsureConfigured();
            IsPlaying = true;
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void Stop()
        {
            IsPlaying = false;
            if (binding != null)
            {
                binding.ResetLivePhysics();
                ApplyFrame(0);
                return;
            }

            playbackFrame = 0.0f;
            CurrentFrame = 0;
            LastSnapshot = null;
            LastEditableRigDiagnostics = null;
        }

        public void Tick(float deltaTime)
        {
            if (!IsPlaying)
            {
                return;
            }

            if (deltaTime < 0.0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be a non-negative finite value.");
            }

            playbackFrame += deltaTime * frameRate;
            CurrentFrame = MmdPlaybackTime.ToFrame(playbackFrame / frameRate, frameRate);
            ApplyPlaybackPose(() => ApplyCurrentFrame());
        }

        private void Update()
        {
            SyncSerializedPhysicsModeToBinding();
            if (ShouldSuppressSelfTick(lastTimelineDriveFrameCount, Time.frameCount))
            {
                // A Timeline/PlayableDirector evaluated this controller on this or the previous frame.
                // It owns the playback clock; self-Tick here would double-drive Live physics and the
                // two diverging clocks destabilize the simulation on scrub/seek. Let the Timeline drive.
                return;
            }

            Tick(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (!HasHumanoidRetargetInputsForLateUpdate())
            {
                return;
            }

            if (ShouldSuppressHumanoidRetargetLateUpdateAfterTimelineDrive(
                    lastHumanoidRetargetTimelineDriveFrameCount,
                    Time.frameCount))
            {
                return;
            }

            MmdHumanoidRetargeterResult result = ApplyHumanoidRetargetNow();
            StepHumanoidRetargetLivePhysicsIfNeeded(result);
        }

        private bool HasHumanoidRetargetInputsForLateUpdate()
        {
            if (proxyRoot == null || humanoidRetargetEntries == null || humanoidRetargetEntries.Count == 0)
            {
                return false;
            }

            Animator? animator = GetComponent<Animator>();
            return animator != null && animator.avatar != null && animator.avatar.isHuman;
        }

        internal int LastTimelineDriveFrameCount => lastTimelineDriveFrameCount;

        internal bool IsVmdDriving => binding != null
            && (IsPlaying
                || isApplyingPlaybackPose
                || ShouldSuppressSelfTick(lastTimelineDriveFrameCount, Time.frameCount));

        // Update() runs in the Update phase, before the PlayableDirector evaluates in PreLateUpdate,
        // so a controller driven by a Timeline sees the previous frame's drive (delta == 1) on the
        // current frame's Update. Suppress for delta 0 and 1; resume self-Tick once the Timeline stops
        // driving (delta >= 2) or when it never drove (sentinel -> large delta).
        internal static bool ShouldSuppressSelfTick(int lastTimelineDriveFrameCount, int currentFrameCount)
        {
            return currentFrameCount >= lastTimelineDriveFrameCount
                && (currentFrameCount - lastTimelineDriveFrameCount) <= 1;
        }

        internal static bool ShouldSuppressHumanoidRetargetLateUpdateAfterTimelineDrive(
            int lastHumanoidRetargetTimelineDriveFrameCount,
            int currentFrameCount)
        {
            return ShouldSuppressSelfTick(lastHumanoidRetargetTimelineDriveFrameCount, currentFrameCount);
        }

        private void OnValidate()
        {
            if (binding == null)
            {
                ValidatePhysicsModeForSerialization();
                return;
            }

            try
            {
                SyncSerializedPhysicsModeToBinding();
            }
            catch (Exception ex)
            {
                Debug.LogError("MMD playback physics mode could not be applied:" + Environment.NewLine + ex, this);
            }
        }

        public void StartPlaybackIfRequested()
        {
            if (playOnStart && binding != null)
            {
                Play();
            }
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

        private void Start()
        {
            try
            {
                ConfigureFromPlaybackSourceIfAvailable();
                StartPlaybackIfRequested();
            }
            catch (Exception ex)
            {
                Debug.LogError("MMD playback could not start in Play Mode:" + Environment.NewLine + ex, this);
            }
        }

        private void OnDestroy()
        {
            binding?.Dispose();
            binding = null;
            LastSnapshot = null;
            LastEditableRigDiagnostics = null;
            IsPlaying = false;
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

        private void TryEnableFastRuntimeFromAssetBytesForDefaultPlayback(MmdPmxAsset pmxAsset, MmdVmdAsset vmdAsset)
        {
            TryEnableFastRuntimeFromBytesForDefaultPlayback(pmxAsset.GetBytesCopy(), vmdAsset.GetBytesCopy());
        }

        private void TryEnableFastRuntimeFromBytesForDefaultPlayback(byte[] pmxBytes, byte[] vmdBytes)
        {
            if (binding == null)
            {
                lastFastRuntimeReason = string.Empty;
                return;
            }

            if (TryEnableFastRuntime(pmxBytes, vmdBytes, out string reason))
            {
                lastFastRuntimeReason = string.Empty;
                return;
            }

            lastFastRuntimeReason = reason;
            Debug.LogWarning("MMD fast runtime unavailable; managed playback remains active: " + reason, this);
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

        private void EnsureConfigured()
        {
            if (binding == null)
            {
                throw new InvalidOperationException("Playback controller must be configured before playback starts.");
            }
        }

        private static void ValidateFrameRate(float playbackFrameRate)
        {
            MmdPlaybackTime.ValidateFrameRate(playbackFrameRate);
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

        private static void ValidatePhysicsMode(MmdPhysicsMode mode)
        {
            switch (mode)
            {
                case MmdPhysicsMode.Off:
                case MmdPhysicsMode.Live:
                    return;
                case MmdPhysicsMode.Cache:
                    throw new NotSupportedException("Physics Cache is not implemented yet.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown physics mode.");
            }
        }

        private void ValidatePhysicsModeForSerialization()
        {
            try
            {
                ValidatePhysicsMode(physicsMode);
            }
            catch (Exception ex)
            {
                Debug.LogError("Invalid MMD playback physics mode; reverting to Off:" + Environment.NewLine + ex, this);
                physicsMode = MmdPhysicsMode.Off;
            }
        }

        private void SyncSerializedPhysicsModeToBinding()
        {
            if (binding == null || binding.PhysicsMode == physicsMode)
            {
                return;
            }

            ApplyPhysicsModeToBinding(physicsMode);
        }

        private void ApplyPhysicsModeToBinding(MmdPhysicsMode mode)
        {
            if (binding == null)
            {
                ValidatePhysicsMode(mode);
                return;
            }

            binding.SetPhysicsMode(mode);
            ResetLivePhysicsDriveSource();
            if (mode == MmdPhysicsMode.Live)
            {
                playbackFrame = 0.0f;
                CurrentFrame = 0;
                ApplyPlaybackPose(() =>
                {
                    PrepareLivePhysicsDriveSource(LivePhysicsDriveSource.VmdForward);
                    LastSnapshot = binding.ApplyFrame(0, frameRate);
                    lastVmdLivePhysicsFrameCount = Time.frameCount;
                    ApplyEditableRigLayer("post-physics-live-frame");
                    return LastSnapshot;
                });
            }
        }

        /// <summary>
        /// Apply time for Timeline random-access evaluation, temporarily suppressing Live physics
        /// on the binding without modifying the controller's serialized <see cref="physicsMode"/> field.
        /// Preserves the binding's physics mode after evaluation without the frame-reset side effects
        /// of <see cref="SetPhysicsMode(MmdPhysicsMode)"/> followed by <see cref="ApplyFrame"/>.
        /// </summary>
        internal MmdPlaybackSnapshot ApplyTimelineTime(float sourceTime, float frameRate)
        {
            if (binding == null)
            {
                throw new InvalidOperationException("Playback controller must be configured before applying timeline time.");
            }

            MmdPlaybackTime.ValidateFrameRate(frameRate);
            lastTimelineDriveFrameCount = Time.frameCount;
            int frame = MmdPlaybackTime.ToFrame(sourceTime, frameRate);

            // Save the binding's current physics mode; we will restore it after ApplyTime.
            MmdPhysicsMode originalBindingMode = binding.PhysicsMode;

            // Temporarily suppress Live on the binding so ApplyTime (random-access) works.
            // We go directly to the binding to avoid changing the controller's serialized physicsMode.
            if (originalBindingMode == MmdPhysicsMode.Live)
            {
                binding.SetPhysicsMode(MmdPhysicsMode.Off);
            }

            try
            {
                return ApplyPlaybackPose(() =>
                {
                    playbackFrame = frame;
                    CurrentFrame = frame;
                    LastSnapshot = binding.ApplyTime(sourceTime, frameRate);
                    ApplyEditableRigLayer("post-native-apply-time");
                    return LastSnapshot;
                });
            }
            finally
            {
                // Restore the binding's original physics mode.
                // We use binding.SetPhysicsMode directly rather than going through the controller's
                // ApplyPhysicsModeToBinding, which would reset playbackFrame/CurrentFrame/LastSnapshot
                // when switching back to Live.
                if (originalBindingMode == MmdPhysicsMode.Live)
                {
                    binding.SetPhysicsMode(MmdPhysicsMode.Live);
                }

                ResetLivePhysicsDriveSource();
                // controller.physicsMode is intentionally NOT modified — preserves serialized/Inspector value.
            }
        }

        /// <summary>
        /// Apply time for Timeline forward (real-time) playback while Live physics is enabled.
        /// Steps the Live physics simulation forward instead of suppressing it (the random-access
        /// <see cref="ApplyTimelineTime"/> path). Used only when the PlayableDirector is playing
        /// forward in Play Mode — not editor preview, not scrubbing/seeking. The first stepped frame
        /// seeds physics from the current animated pose, so resuming play after a scrub (which routes
        /// through <see cref="ApplyTimelineTime"/> and resets Live physics) restarts the simulation.
        /// </summary>
        internal MmdPlaybackSnapshot ApplyTimelineLivePhysicsForward(float sourceTime, float frameRate)
        {
            if (binding == null)
            {
                throw new InvalidOperationException("Playback controller must be configured before applying timeline time.");
            }

            MmdPlaybackTime.ValidateFrameRate(frameRate);
            lastTimelineDriveFrameCount = Time.frameCount;
            int frame = MmdPlaybackTime.ToFrame(sourceTime, frameRate);

            // The animation-only scrub path (ApplyTimelineTime) leaves the binding in Live but with a
            // reset simulation; guard against a binding that was left in Off by re-enabling Live.
            if (binding.PhysicsMode != MmdPhysicsMode.Live)
            {
                binding.SetPhysicsMode(MmdPhysicsMode.Live);
            }

            playbackFrame = frame;
            CurrentFrame = frame;
            return ApplyPlaybackPose(() =>
            {
                PrepareLivePhysicsDriveSource(LivePhysicsDriveSource.VmdForward);
                LastSnapshot = binding.ApplyLivePhysicsForwardFrame(frame, frameRate);
                lastVmdLivePhysicsFrameCount = Time.frameCount;
                ApplyEditableRigLayer("post-physics-live-frame");
                return LastSnapshot;
            });
        }

        private MmdPlaybackSnapshot ApplyCurrentFrame()
        {
            if (binding == null)
            {
                throw new InvalidOperationException("Playback controller must be configured before applying frames.");
            }

            if (binding.PhysicsMode == MmdPhysicsMode.Live)
            {
                PrepareLivePhysicsDriveSource(LivePhysicsDriveSource.VmdForward);
            }

            LastSnapshot = binding.ApplyFrame(CurrentFrame, frameRate);
            if (binding.PhysicsMode == MmdPhysicsMode.Live)
            {
                lastVmdLivePhysicsFrameCount = Time.frameCount;
            }

            ApplyEditableRigLayer(binding.PhysicsMode == MmdPhysicsMode.Live ? "post-physics-live-frame" : "post-native-apply-frame");
            return LastSnapshot;
        }

        private void StepHumanoidRetargetLivePhysicsIfNeeded(MmdHumanoidRetargeterResult result)
        {
            EnsureHumanoidPhysicsBinding();

            if (binding == null ||
                physicsMode != MmdPhysicsMode.Live ||
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

            if (binding.PhysicsMode != MmdPhysicsMode.Live)
            {
                binding.SetPhysicsMode(MmdPhysicsMode.Live);
                ResetLivePhysicsDriveSource();
            }

            bool resetOnFirstStep = PrepareLivePhysicsDriveSource(LivePhysicsDriveSource.HumanoidRetarget);
            humanoidLivePhysicsStepIndex++;
            binding.StepLivePhysicsFromCurrentPose(
                humanoidLivePhysicsStepIndex,
                resetOnFirstStep ? 0.0f : Time.deltaTime,
                resetOnFirstStep);
            lastHumanoidLivePhysicsFrameCount = Time.frameCount;
        }

        private void EnsureHumanoidPhysicsBinding()
        {
            if (binding != null ||
                physicsMode != MmdPhysicsMode.Live ||
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
                binding = MmdUnityPlaybackBinding.CreateSkinnedForModelOnlyPhysicsFromExistingSceneModel(
                    gameObject,
                    modelAsset,
                    "humanoid-physics");
                binding.SetPhysicsMode(MmdPhysicsMode.Live);
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

            if (livePhysicsDriveSource == source)
            {
                return false;
            }

            binding.ResetLivePhysics();
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

        private MmdPlaybackSnapshot ApplyPlaybackPose(Func<MmdPlaybackSnapshot> apply)
        {
            isApplyingPlaybackPose = true;
            try
            {
                return apply();
            }
            finally
            {
                isApplyingPlaybackPose = false;
            }
        }

        private void ApplyEditableRigLayer(string executionStage)
        {
            if (binding == null)
            {
                LastEditableRigDiagnostics = MmdEditableRigLayerDiagnostics.NotFound(executionStage);
                return;
            }

            MmdEditableRigLayer? layer = ResolveEditableRigLayer();
            LastEditableRigDiagnostics = layer == null
                ? MmdEditableRigLayerDiagnostics.NotFound(executionStage)
                : layer.ApplyAfterRuntimePose(binding.Instance, executionStage);
        }

        private MmdEditableRigLayer? ResolveEditableRigLayer()
        {
            MmdEditableRigLayer? layer = GetComponent<MmdEditableRigLayer>();
            if (layer != null || binding == null || binding.Instance.Root == gameObject)
            {
                return layer;
            }

            return binding.Instance.Root.GetComponent<MmdEditableRigLayer>();
        }

        private static MmdHumanoidRetargeterResult CreateHumanoidRetargetNoOpResult(MmdHumanoidRetargetGate gate)
        {
            return new MmdHumanoidRetargeterResult(
                0,
                0,
                new[] { "humanoid-retarget: no-op gate=" + gate });
        }
    }
}
