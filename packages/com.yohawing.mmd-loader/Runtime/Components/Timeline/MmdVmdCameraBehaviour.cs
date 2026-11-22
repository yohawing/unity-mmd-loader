#nullable enable

using System;
using UnityEngine.Playables;
using Mmd;
using Mmd.Motion;
using Mmd.Parser;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    /// <summary>
    /// Drives a Scene Camera (through the bound <see cref="MmdSceneEnvironmentBinding"/> proxy) from a
    /// VMD camera track. Each frame it samples the camera keyframes at the local Timeline time,
    /// converts to a Unity pose, and applies it. Stateless / random-access safe: the same time always
    /// produces the same pose, so Timeline scrubbing and re-evaluation are well-defined.
    /// </summary>
    [Serializable]
    public sealed class MmdVmdCameraBehaviour : PlayableBehaviour
    {
        private const string EmptyCameraTrackDiagnostic =
            "VMD native camera track unavailable: source bytes are empty";
        private const string EmptyLightTrackDiagnostic =
            "VMD native light track unavailable: source bytes are empty";
        private const string EmptySelfShadowTrackDiagnostic =
            "VMD native self-shadow track unavailable: source bytes are empty";

        public MmdSceneEnvironmentBinding? Binding { get; set; }

        public byte[]? MotionBytes { get; set; }

        public string MotionSourceId { get; set; } = string.Empty;

        public float FrameRate { get; set; } = 30.0f;

        public float StartOffsetSeconds { get; set; }

        public MmdVmdTimelineLoopPolicy LoopPolicy { get; set; }

        public float MinFieldOfView { get; set; } = MmdCameraStateToUnity.DefaultMinFieldOfView;

        public float ImportScale { get; set; } = MmdPmxAsset.DefaultImportScale;

        public MmdSceneCameraApplyStatus LastApplyStatus { get; private set; }

        public string NativeCameraTrackDiagnostic => OwnedOrNull(cameraTrack)?.Diagnostic ?? string.Empty;

        public string NativeLightTrackDiagnostic => OwnedOrNull(lightTrack)?.Diagnostic ?? string.Empty;

        public string NativeSelfShadowTrackDiagnostic => OwnedOrNull(selfShadowTrack)?.Diagnostic ?? string.Empty;

        // Imported assets carry these counts from the binary summary. A zero count means that a
        // native track is legitimately absent; null means that the source has not been summarized,
        // so a native null handle must remain diagnosable as a creation failure.
        public int? ExpectedCameraKeyframeCount { get; set; }

        public int? ExpectedLightKeyframeCount { get; set; }

        public int? ExpectedSelfShadowKeyframeCount { get; set; }

        // Native track slots are owned per behaviour instance. ScriptPlayable.Create(graph, template)
        // copies the template with a shallow PlayableBehaviour.Clone(), so a copied reference must
        // never be used: every access checks the slot owner and replaces a slot it does not own.
        private NativeTrackSlot<NativeVmdCameraTrackSampler, MmdCameraState>? cameraTrack;
        private NativeTrackSlot<NativeVmdLightTrackSampler, MmdLightState>? lightTrack;
        private NativeTrackSlot<NativeVmdSelfShadowTrackSampler, MmdSelfShadowState>? selfShadowTrack;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (info.effectiveWeight <= 0.0f)
            {
                return;
            }

            MmdSceneEnvironmentBinding? target = playerData as MmdSceneEnvironmentBinding ?? Binding;
            if (target == null)
            {
                return;
            }

            EvaluateAtLocalTime(target, playable.GetTime());
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            // A slot copied from a template belongs to the template; leave it alone.
            OwnedOrNull(cameraTrack)?.Dispose();
            OwnedOrNull(lightTrack)?.Dispose();
            OwnedOrNull(selfShadowTrack)?.Dispose();
        }

        /// <summary>
        /// Samples and applies the camera at the given Timeline local time. Returns the proxy's apply
        /// status; when the track has no camera keyframes it is a no-op (<see cref="MmdSceneCameraApplyStatus.NotApplied"/>).
        /// </summary>
        public MmdSceneCameraApplyStatus EvaluateAtLocalTime(MmdSceneEnvironmentBinding? target, double localTime)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (double.IsNaN(localTime) || double.IsInfinity(localTime) || localTime < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(localTime), "Timeline local time must be a non-negative finite value.");
            }

            MmdPlaybackTime.ValidateFrameRate(FrameRate);
            MmdPlaybackTime.ValidateTime(StartOffsetSeconds);

            double sourceTime = localTime + StartOffsetSeconds;
            if (sourceTime > float.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(localTime), "Timeline local time is too large for camera evaluation.");
            }

            float frame = (float)(sourceTime * FrameRate);

            if (OwnedCameraTrack().TrySample(MotionBytes, ExpectedCameraKeyframeCount, frame, out MmdCameraState cameraState))
            {
                LastApplyStatus = target.ApplyCameraState(cameraState, MinFieldOfView, ImportScale);
            }
            else
            {
                LastApplyStatus = MmdSceneCameraApplyStatus.NotApplied;
            }

            if (OwnedLightTrack().TrySample(MotionBytes, ExpectedLightKeyframeCount, frame, out MmdLightState lightState))
            {
                target.ApplyLightState(lightState);
            }

            if (OwnedSelfShadowTrack().TrySample(
                    MotionBytes,
                    ExpectedSelfShadowKeyframeCount,
                    frame,
                    out MmdSelfShadowState selfShadowState))
            {
                target.ApplySelfShadowState(selfShadowState);
            }

            return LastApplyStatus;
        }

        private NativeTrackSlot<NativeVmdCameraTrackSampler, MmdCameraState> OwnedCameraTrack()
        {
            return OwnedOrNull(cameraTrack) ?? (cameraTrack =
                new NativeTrackSlot<NativeVmdCameraTrackSampler, MmdCameraState>(
                    this,
                    "camera",
                    EmptyCameraTrackDiagnostic,
                    MmdCameraState.Default,
                    NativeVmdCameraTrackSampler.TryCreate));
        }

        private NativeTrackSlot<NativeVmdLightTrackSampler, MmdLightState> OwnedLightTrack()
        {
            return OwnedOrNull(lightTrack) ?? (lightTrack =
                new NativeTrackSlot<NativeVmdLightTrackSampler, MmdLightState>(
                    this,
                    "light",
                    EmptyLightTrackDiagnostic,
                    MmdLightState.Default,
                    NativeVmdLightTrackSampler.TryCreate));
        }

        private NativeTrackSlot<NativeVmdSelfShadowTrackSampler, MmdSelfShadowState> OwnedSelfShadowTrack()
        {
            return OwnedOrNull(selfShadowTrack) ?? (selfShadowTrack =
                new NativeTrackSlot<NativeVmdSelfShadowTrackSampler, MmdSelfShadowState>(
                    this,
                    "self-shadow",
                    EmptySelfShadowTrackDiagnostic,
                    MmdSelfShadowState.Default,
                    NativeVmdSelfShadowTrackSampler.TryCreate));
        }

        private TSlot? OwnedOrNull<TSlot>(TSlot? slot)
            where TSlot : class, INativeTrackSlot
        {
            return slot != null && ReferenceEquals(slot.Owner, this) ? slot : null;
        }

        private interface INativeTrackSlot
        {
            object Owner { get; }
        }

        private delegate bool TryCreateSampler<TSampler>(
            byte[] vmdBytes,
            out TSampler? sampler,
            out string failureReason,
            out bool trackAbsent)
            where TSampler : class;

        /// <summary>
        /// Owns one native scene-track sampler (camera, light or self-shadow). The sampler is created
        /// once per source byte array, and a creation failure is latched for that array so that every
        /// frame does not retry native creation. Replacing the array recreates it.
        /// </summary>
        private sealed class NativeTrackSlot<TSampler, TState> : INativeTrackSlot
            where TSampler : NativeVmdTrackSampler<TState>
        {
            private readonly string trackName;
            private readonly string emptySourceDiagnostic;
            private readonly TState defaultState;
            private readonly TryCreateSampler<TSampler> tryCreate;
            private TSampler? sampler;
            private byte[]? samplerSource;
            private bool samplerUnavailable;

            public NativeTrackSlot(
                object owner,
                string trackName,
                string emptySourceDiagnostic,
                TState defaultState,
                TryCreateSampler<TSampler> tryCreate)
            {
                Owner = owner;
                this.trackName = trackName;
                this.emptySourceDiagnostic = emptySourceDiagnostic;
                this.defaultState = defaultState;
                this.tryCreate = tryCreate;
            }

            public object Owner { get; }

            public string Diagnostic { get; private set; } = string.Empty;

            public bool TrySample(byte[]? motionBytes, int? expectedKeyframeCount, float frame, out TState state)
            {
                state = defaultState;
                if (motionBytes == null || motionBytes.Length == 0)
                {
                    Dispose();
                    Diagnostic = emptySourceDiagnostic;
                    return false;
                }

                if (!ReferenceEquals(samplerSource, motionBytes))
                {
                    Dispose();
                    samplerSource = motionBytes;
                    if (!tryCreate(motionBytes, out sampler, out string failureReason, out bool trackAbsent))
                    {
                        samplerUnavailable = true;
                        // A zero summary count means the VMD legitimately has no records for this
                        // track; without a summary a null native track stays a diagnosable failure.
                        Diagnostic = trackAbsent && expectedKeyframeCount == 0
                            ? string.Empty
                            : TrackUnavailable(failureReason);
                        return false;
                    }

                    Diagnostic = string.Empty;
                }

                if (samplerUnavailable)
                {
                    return false;
                }

                if (sampler != null && sampler.TrySample(frame, out state))
                {
                    Diagnostic = string.Empty;
                    return true;
                }

                Diagnostic = TrackUnavailable(sampler?.LastFailureReason ?? "native sampler is unavailable");
                return false;
            }

            public void Dispose()
            {
                sampler?.Dispose();
                sampler = null;
                samplerSource = null;
                samplerUnavailable = false;
                Diagnostic = string.Empty;
            }

            private string TrackUnavailable(string reason)
            {
                return "VMD native " + trackName + " track unavailable: " + reason;
            }
        }
    }
}
