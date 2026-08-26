#nullable enable

using System;
using System.Collections.Generic;
using Mmd;
using Mmd.Physics;
using UnityEngine;

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

    internal enum MmdHumanoidPhysicsBindingFailureReason
    {
        None = 0,
        MissingComponent = 1,
        InvalidOperation = 2
    }

    [Serializable]
    public sealed class MmdTimelineSetupTimingSummary
    {
        public double totalMs;
        public double motionHeaderMs;
        public double pmxParseMs;
        public double compatibilityValidationMs;
        public double sourceAcquireMs;
        public double sharedVmdContextMs;
        public double nativeAvailabilityMs;
        public double releasePreviousBindingMs;
        public double sceneBindingMs;
        public double nativeSessionMs;
        public double controllerConfigureMs;
        public double initialSeedMs;
        public double livePhysicsPrewarmMs;
        public bool configured;
        public bool pmxSourceBufferBorrowed;
        public bool pmxParseCacheHit;
        public bool livePhysicsPrewarmed;
        public bool livePhysicsWorldReused;
        public bool succeeded;
    }

    [DisallowMultipleComponent]
    public sealed partial class MmdUnityPlaybackController : MonoBehaviour
    {
        private const string HumanoidPhysicsOffIkCapNotSupportedMessage =
            "A positive IK iteration cap is not supported for Humanoid retargeting while Physics Mode is Off. " +
            "Use the compatibility default 0 or Physics Mode Live.";
        private const string HumanoidPhysicsBindingMissingComponentReason = "MissingComponent";
        private const string HumanoidPhysicsBindingInvalidOperationReason = "InvalidOperation";
        private const string HumanoidPhysicsBindingUnknownReason = "Unknown";

        private MmdUnityPlaybackBinding? binding;
        private MmdUnityPlaybackBinding? humanoidPhysicsBinding;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private int initialFrame;
        [SerializeField] private float frameRate = 30.0f;
        [SerializeField] private MmdPhysicsMode physicsMode = MmdPhysicsMode.Live;
        [SerializeField] private int livePhysicsBodyDiagnosticsSampleInterval;
        [SerializeField] private int ikMaxIterationsCap;
        [SerializeField] private MmdPmxAsset? modelAsset;
        [SerializeField] private MmdVmdAsset? motionAsset;
        [SerializeField] private string lastFastRuntimeReason = string.Empty;
        [SerializeField] private Transform? proxyRoot;
        [SerializeField] private List<MmdHumanoidRetargetBinding> humanoidRetargetEntries = new();
        [SerializeField] private List<MmdHumanoidAppendTransformBinding> humanoidAppendEntries = new();
        private float playbackFrame;
        private bool isApplyingPlaybackPose;
        private bool pendingSeekReseed;


        public bool IsConfigured => binding != null;

        public bool IsPlaying { get; private set; }

        public int CurrentFrame { get; private set; }

        public float FrameRate => frameRate;

        public MmdPhysicsMode PhysicsMode => physicsMode;

        public int MotionMaxFrame => binding?.MotionMaxFrame ?? 0;

        public bool PlayOnStart => playOnStart;

        public bool IsFastRuntimeEnabled => binding?.IsFastRuntimeEnabled ?? false;

        public string LastFastRuntimeReason => lastFastRuntimeReason;

        public int ConfigurationRevision { get; private set; }

        internal int TimelinePoseEvaluationCount { get; private set; }

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

        internal MmdUnityModelInstance? ConfiguredPlaybackInstance => binding?.PlaybackInstance;

        public MmdPlaybackSnapshot? LastSnapshot { get; private set; }

        public MmdTimelineSetupTimingSummary? LastTimelineSetupTiming { get; internal set; }

        public MmdLivePhysicsFrameDiagnostics? LastLivePhysicsDiagnostics =>
            binding?.LastLivePhysicsDiagnostics ?? humanoidPhysicsBinding?.LastLivePhysicsDiagnostics;

        public int LivePhysicsBodyDiagnosticsSampleInterval
        {
            get => livePhysicsBodyDiagnosticsSampleInterval;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Live physics body diagnostics sample interval must be non-negative.");
                }

                livePhysicsBodyDiagnosticsSampleInterval = value;
                if (binding != null)
                {
                    binding.LivePhysicsBodyDiagnosticsSampleInterval = value;
                }

                if (humanoidPhysicsBinding != null)
                {
                    humanoidPhysicsBinding.LivePhysicsBodyDiagnosticsSampleInterval = value;
                }
            }
        }

        public Transform? HumanoidProxyRoot => proxyRoot;

        public IReadOnlyList<MmdHumanoidRetargetBinding> HumanoidRetargetEntries => humanoidRetargetEntries;

        public IReadOnlyList<MmdHumanoidAppendTransformBinding> HumanoidAppendEntries => humanoidAppendEntries;

        public MmdHumanoidRetargetGate LastHumanoidRetargetGate { get; private set; } =
            MmdHumanoidRetargetGate.MissingBindings;

        public MmdHumanoidRetargeterResult? LastHumanoidRetargetResult { get; private set; }

        /// <summary>
        /// Stable diagnostic name for the last model-only Humanoid physics binding failure.
        /// An empty value means no binding failure has been recorded since the last source or
        /// retarget configuration change.
        /// </summary>
        public string HumanoidPhysicsBindingFailureReason =>
            humanoidPhysicsBindingFailureReason switch
            {
                MmdHumanoidPhysicsBindingFailureReason.None => string.Empty,
                MmdHumanoidPhysicsBindingFailureReason.MissingComponent =>
                    HumanoidPhysicsBindingMissingComponentReason,
                MmdHumanoidPhysicsBindingFailureReason.InvalidOperation =>
                    HumanoidPhysicsBindingInvalidOperationReason,
                _ => HumanoidPhysicsBindingUnknownReason
            };

        public void ConfigureHumanoidRetarget(
            Transform? proxyRoot,
            IReadOnlyList<MmdHumanoidRetargetBinding>? entries,
            IReadOnlyList<MmdHumanoidAppendTransformBinding>? appendEntries)
        {
            ThrowIfMultiCharacterPoolOwnsController(nameof(ConfigureHumanoidRetarget));
            if (physicsMode == MmdPhysicsMode.Off && ikMaxIterationsCap > 0 &&
                proxyRoot != null && entries != null && entries.Count > 0)
            {
                throw new NotSupportedException(
                    HumanoidPhysicsOffIkCapNotSupportedMessage);
            }

            this.proxyRoot = proxyRoot;
            humanoidRetargetEntries = entries != null
                ? new List<MmdHumanoidRetargetBinding>(entries)
                : new List<MmdHumanoidRetargetBinding>();
            humanoidAppendEntries = appendEntries != null
                ? new List<MmdHumanoidAppendTransformBinding>(appendEntries)
                : new List<MmdHumanoidAppendTransformBinding>();
            LastHumanoidRetargetGate = EvaluateHumanoidRetargetGate(requireAnimatorDriver: true);
            LastHumanoidRetargetResult = null;
            DisposeHumanoidPhysicsBinding();
            DisposeHumanoidHostPoseSession();
            ResetHumanoidHostPoseFailureLatch();
            ResetHumanoidPhysicsBindingFailureReason();
        }

        public void Configure(MmdUnityPlaybackBinding playbackBinding, MmdPlaybackConfig config)
        {
            ThrowIfMultiCharacterPoolOwnsController(nameof(Configure));
            config.Validate();
            Configure(playbackBinding, config.FrameRate, config.PlayOnStart);
            ApplyFrame(config.InitialFrame);
        }

        public void Configure(MmdUnityPlaybackBinding playbackBinding, float playbackFrameRate, bool playOnStart = true)
        {
            ThrowIfMultiCharacterPoolOwnsController(nameof(Configure));
            if (playbackBinding == null)
            {
                throw new ArgumentNullException(nameof(playbackBinding));
            }

            MmdPlaybackTime.ValidateFrameRate(playbackFrameRate);
            ValidatePhysicsModeForSerialization();
            DisposeHumanoidHostPoseSession();
            if (binding != null && !ReferenceEquals(binding, playbackBinding))
            {
                binding.Dispose();
            }

            DisposeHumanoidPhysicsBinding();
            ResetHumanoidHostPoseFailureLatch();
            ResetHumanoidPhysicsBindingFailureReason();
            binding = playbackBinding;
            frameRate = playbackFrameRate;
            playbackFrame = 0.0f;
            CurrentFrame = 0;
            LastSnapshot = null;
            IsPlaying = false;
            this.playOnStart = playOnStart;
            binding.LivePhysicsBodyDiagnosticsSampleInterval = livePhysicsBodyDiagnosticsSampleInterval;
            binding.IkMaxIterationsCap = ikMaxIterationsCap;
            binding.SetPhysicsMode(physicsMode);
            ResetLivePhysicsDriveSource();
            TimelinePoseEvaluationCount = 0;
            timelinePreparationSeedPending = false;
            timelinePreparationSeedWasLive = false;
            ConfigurationRevision++;
        }

        public void SetPlayOnStart(bool value)
        {
            ThrowIfMultiCharacterPoolOwnsController(nameof(SetPlayOnStart));
            playOnStart = value;
        }

        public void SetPhysicsMode(MmdPhysicsMode mode)
        {
            ThrowIfMultiCharacterPoolOwnsController(nameof(SetPhysicsMode));
            ApplyPhysicsMode(mode);
        }

        /// <summary>
        /// Optional per-chain IK iteration ceiling. Zero preserves each PMX chain's authored
        /// iteration count. Positive values are an advanced override for supported evaluation paths;
        /// unsupported native physics combinations fail closed instead of silently ignoring the cap.
        /// </summary>
        public int IkMaxIterationsCap
        {
            get => ikMaxIterationsCap;
            set
            {
                ThrowIfMultiCharacterPoolOwnsController(nameof(IkMaxIterationsCap));
                ValidateIkMaxIterationsCap(value);
                ValidateHumanoidPhysicsOffIkCap(value);
                ikMaxIterationsCap = value;
                PropagateIkMaxIterationsCap();
            }
        }

        private void ApplyPhysicsMode(MmdPhysicsMode mode)
        {
            ValidatePhysicsMode(mode);
            if (mode == MmdPhysicsMode.Off)
            {
                ValidateHumanoidPhysicsOffIkCap(ikMaxIterationsCap, mode);
            }
            MmdPhysicsMode previousMode = physicsMode;
            if (mode != MmdPhysicsMode.Off)
            {
                DisposeHumanoidHostPoseSession();
            }
            else if (previousMode != MmdPhysicsMode.Off)
            {
                ResetHumanoidHostPoseFailureLatch();
            }

            if (binding != null)
            {
                ApplyPhysicsModeToBinding(mode);
            }
            else
            {
                ValidatePhysicsMode(mode);
            }

            physicsMode = mode;
            if (humanoidPhysicsBinding != null)
            {
                if (mode == MmdPhysicsMode.Live)
                {
                    humanoidPhysicsBinding.SetPhysicsMode(MmdPhysicsMode.Live);
                }
                else
                {
                    DisposeHumanoidPhysicsBinding();
                }
            }
        }

        public MmdPlaybackSnapshot ApplyFrame(int frame)
        {
            ThrowIfMultiCharacterPoolOwnsController(nameof(ApplyFrame));
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

        public MmdPlaybackSnapshot SeekFrame(int frame)
        {
            ThrowIfMultiCharacterPoolOwnsController(nameof(SeekFrame));
            if (binding == null)
            {
                throw new InvalidOperationException("Playback controller must be configured before seeking.");
            }

            if (frame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Frame index must not be negative.");
            }

            MmdPhysicsMode originalBindingMode = binding.PhysicsMode;
            if (originalBindingMode == MmdPhysicsMode.Live)
            {
                binding.SetPhysicsMode(MmdPhysicsMode.Off);
            }

            try
            {
                playbackFrame = frame;
                CurrentFrame = frame;
                return ApplyPlaybackPose(() =>
                {
                    LastSnapshot = binding.ApplyFrame(CurrentFrame, frameRate);
                    return LastSnapshot;
                });
            }
            finally
            {
                if (originalBindingMode == MmdPhysicsMode.Live)
                {
                    binding.SetPhysicsMode(MmdPhysicsMode.Live);
                    pendingSeekReseed = true;
                }

                ResetLivePhysicsDriveSource();
            }
        }

        public MmdPlaybackSnapshot ApplyTime(float time)
        {
            ThrowIfMultiCharacterPoolOwnsController(nameof(ApplyTime));
            return ApplyTime(time, frameRate);
        }

        public MmdPlaybackSnapshot ApplyTime(float time, float playbackFrameRate)
        {
            ThrowIfMultiCharacterPoolOwnsController(nameof(ApplyTime));
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
                return LastSnapshot;
            });
        }

        public void Play()
        {
            ThrowIfMultiCharacterPoolOwnsController(nameof(Play));
            EnsureConfigured();
            IsPlaying = true;
        }

        public void Pause()
        {
            ThrowIfMultiCharacterPoolOwnsController(nameof(Pause));
            IsPlaying = false;
        }

        public void Stop()
        {
            ThrowIfMultiCharacterPoolOwnsController(nameof(Stop));
            IsPlaying = false;
            pendingSeekReseed = false;
            if (binding != null)
            {
                binding.ResetLivePhysics();
                ApplyFrame(0);
                return;
            }

            playbackFrame = 0.0f;
            CurrentFrame = 0;
            LastSnapshot = null;
        }

        public void Tick(float deltaTime)
        {
            ThrowIfMultiCharacterPoolOwnsController(nameof(Tick));
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
            if (IsMultiCharacterClaimed)
            {
                return;
            }

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
            if (IsMultiCharacterClaimed)
            {
                return;
            }

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

            ApplyHumanoidRetargetNow();
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

        internal bool IsVmdDriving => binding != null
            && (IsPlaying
                || isApplyingPlaybackPose
                || ShouldSuppressSelfTick(lastTimelineDriveFrameCount, Time.frameCount));

        private void OnValidate()
        {
            livePhysicsBodyDiagnosticsSampleInterval = Math.Max(0, livePhysicsBodyDiagnosticsSampleInterval);
            ikMaxIterationsCap = Math.Max(0, ikMaxIterationsCap);
            if (IsHumanoidPhysicsOffIkCapUnsupported(ikMaxIterationsCap))
            {
                Debug.LogError(
                    HumanoidPhysicsOffIkCapNotSupportedMessage + " Reverting the serialized cap to 0.",
                    this);
                ikMaxIterationsCap = 0;
            }

            if (binding != null)
            {
                binding.LivePhysicsBodyDiagnosticsSampleInterval = livePhysicsBodyDiagnosticsSampleInterval;
                binding.IkMaxIterationsCap = ikMaxIterationsCap;
            }

            if (humanoidPhysicsBinding != null)
            {
                humanoidPhysicsBinding.LivePhysicsBodyDiagnosticsSampleInterval = livePhysicsBodyDiagnosticsSampleInterval;
                humanoidPhysicsBinding.IkMaxIterationsCap = ikMaxIterationsCap;
            }

            if (binding == null)
            {
                ValidatePhysicsModeForSerialization();
                if (physicsMode == MmdPhysicsMode.Off)
                {
                    ResetHumanoidHostPoseFailureLatch();
                }
                else
                {
                    DisposeHumanoidHostPoseSession();
                }

                if (humanoidPhysicsBinding != null)
                {
                    if (physicsMode == MmdPhysicsMode.Live)
                    {
                        humanoidPhysicsBinding.SetPhysicsMode(MmdPhysicsMode.Live);
                    }
                    else
                    {
                        DisposeHumanoidPhysicsBinding();
                    }
                }

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
            MmdMultiCharacterPlaybackGroup.NotifyControllerUnavailable(this);
            if (multiCharacterGroup != null)
            {
                ReleaseMultiCharacterGroup(multiCharacterGroup);
            }

            ReleasePlaybackResources();
        }

        internal void ReleasePlaybackResources()
        {
            DisposeHumanoidPhysicsBinding();
            DisposeHumanoidHostPoseSession();
            binding?.Dispose();
            binding = null;
            LastSnapshot = null;
            IsPlaying = false;
        }

        private void OnDisable()
        {
            MmdMultiCharacterPlaybackGroup.NotifyControllerUnavailable(this);
            DisposeHumanoidPhysicsBinding();
            DisposeHumanoidHostPoseSession();
        }

        private void OnEnable()
        {
            if (modelAsset != null)
            {
                _ = modelAsset.BeginSynchronousPlaybackPreload(
                    MmdUnityPlaybackBinding.ResolveMaterialPreset(modelAsset));
            }
        }

        private void EnsureConfigured()
        {
            if (binding == null)
            {
                throw new InvalidOperationException("Playback controller must be configured before playback starts.");
            }
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

        private static void ValidateIkMaxIterationsCap(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "IK maximum iterations cap must be non-negative; zero preserves authored PMX iteration counts.");
            }
        }

        private void ValidateHumanoidPhysicsOffIkCap(int value)
        {
            ValidateHumanoidPhysicsOffIkCap(value, physicsMode);
        }

        private void ValidateHumanoidPhysicsOffIkCap(int value, MmdPhysicsMode mode)
        {
            if (IsHumanoidPhysicsOffIkCapUnsupported(value, mode))
            {
                throw new NotSupportedException(HumanoidPhysicsOffIkCapNotSupportedMessage);
            }
        }

        private bool IsHumanoidPhysicsOffIkCapUnsupported(int value)
        {
            return IsHumanoidPhysicsOffIkCapUnsupported(value, physicsMode);
        }

        private bool IsHumanoidPhysicsOffIkCapUnsupported(int value, MmdPhysicsMode mode)
        {
            return value > 0 && mode == MmdPhysicsMode.Off &&
                   proxyRoot != null && humanoidRetargetEntries != null && humanoidRetargetEntries.Count > 0;
        }

        private void PropagateIkMaxIterationsCap()
        {
            if (binding != null)
            {
                binding.IkMaxIterationsCap = ikMaxIterationsCap;
            }

            if (humanoidPhysicsBinding != null)
            {
                humanoidPhysicsBinding.IkMaxIterationsCap = ikMaxIterationsCap;
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

            ValidatePhysicsMode(physicsMode);
            if (physicsMode == MmdPhysicsMode.Off)
            {
                ResetHumanoidHostPoseFailureLatch();
            }
            else
            {
                DisposeHumanoidHostPoseSession();
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
            pendingSeekReseed = false;
            if (mode == MmdPhysicsMode.Live)
            {
                playbackFrame = 0.0f;
                CurrentFrame = 0;
                ApplyPlaybackPose(() =>
                {
                    PrepareLivePhysicsDriveSource(LivePhysicsDriveSource.VmdForward);
                    LastSnapshot = binding.ApplyFrame(0, frameRate);
                    lastVmdLivePhysicsFrameCount = Time.frameCount;
                    return LastSnapshot;
                });
            }
        }

        private MmdPlaybackSnapshot ApplyCurrentFrame()
        {
            if (binding == null)
            {
                throw new InvalidOperationException("Playback controller must be configured before applying frames.");
            }

            if (binding.PhysicsMode == MmdPhysicsMode.Live && pendingSeekReseed)
            {
                pendingSeekReseed = false;
                PrepareLivePhysicsDriveSource(LivePhysicsDriveSource.VmdForward);
                LastSnapshot = binding.ApplyLivePhysicsForwardFrame(CurrentFrame, frameRate);
                lastVmdLivePhysicsFrameCount = Time.frameCount;
                return LastSnapshot;
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

            return LastSnapshot;
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
    }
}
