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

    [DisallowMultipleComponent]
    public sealed partial class MmdUnityPlaybackController : MonoBehaviour
    {
        private MmdUnityPlaybackBinding? binding;
        private MmdUnityPlaybackBinding? humanoidPhysicsBinding;
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
        private bool isApplyingPlaybackPose;
        private bool pendingSeekReseed;

        private bool _userExplicitLive;

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

        public MmdLivePhysicsFrameDiagnostics? LastLivePhysicsDiagnostics =>
            binding?.LastLivePhysicsDiagnostics ?? humanoidPhysicsBinding?.LastLivePhysicsDiagnostics;

        internal bool HasHumanoidPhysicsBinding => humanoidPhysicsBinding != null;

        public Transform? HumanoidProxyRoot => proxyRoot;

        public IReadOnlyList<MmdHumanoidRetargetBinding> HumanoidRetargetEntries => humanoidRetargetEntries;

        public IReadOnlyList<MmdHumanoidAppendTransformBinding> HumanoidAppendEntries => humanoidAppendEntries;

        public MmdHumanoidRetargetGate LastHumanoidRetargetGate { get; private set; } =
            MmdHumanoidRetargetGate.MissingBindings;

        public MmdHumanoidRetargeterResult? LastHumanoidRetargetResult { get; private set; }

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
            DisposeHumanoidPhysicsBinding();
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

            DisposeHumanoidPhysicsBinding();
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
                    ApplyEditableRigLayer("post-seek-frame");
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

        internal bool IsVmdDriving => binding != null
            && (IsPlaying
                || isApplyingPlaybackPose
                || ShouldSuppressSelfTick(lastTimelineDriveFrameCount, Time.frameCount));

        private void OnValidate()
        {
            if (binding == null)
            {
                ValidatePhysicsModeForSerialization();
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
            DisposeHumanoidPhysicsBinding();
            binding?.Dispose();
            binding = null;
            LastSnapshot = null;
            LastEditableRigDiagnostics = null;
            IsPlaying = false;
        }

        internal void PrepareForAssemblyReload()
        {
            DisposeHumanoidPhysicsBinding();
            binding?.Dispose();
            binding = null;
            LastSnapshot = null;
            LastEditableRigDiagnostics = null;
            IsPlaying = false;
        }

        private void OnDisable()
        {
            DisposeHumanoidPhysicsBinding();
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
                    ApplyEditableRigLayer("post-physics-live-frame");
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
                ApplyEditableRigLayer("post-physics-live-frame");
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

            ApplyEditableRigLayer(binding.PhysicsMode == MmdPhysicsMode.Live ? "post-physics-live-frame" : "post-native-apply-frame");
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
