#nullable enable

using System;
using System.Collections.Generic;
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

        public IReadOnlyList<MmdCameraKeyframeDefinition>? CameraKeyframes { get; set; }
            = Array.Empty<MmdCameraKeyframeDefinition>();

        public byte[]? MotionBytes { get; set; }

        public IReadOnlyList<MmdLightKeyframeDefinition>? LightKeyframes { get; set; }
            = Array.Empty<MmdLightKeyframeDefinition>();

        public IReadOnlyList<MmdSelfShadowKeyframeDefinition>? SelfShadowKeyframes { get; set; }
            = Array.Empty<MmdSelfShadowKeyframeDefinition>();

        public string MotionSourceId { get; set; } = string.Empty;

        public float FrameRate { get; set; } = 30.0f;

        public float StartOffsetSeconds { get; set; }

        public MmdVmdTimelineLoopPolicy LoopPolicy { get; set; }

        public float MinFieldOfView { get; set; } = MmdCameraStateToUnity.DefaultMinFieldOfView;

        public float ImportScale { get; set; } = MmdPmxAsset.DefaultImportScale;

        public MmdSceneCameraApplyStatus LastApplyStatus { get; private set; }

        public string NativeCameraTrackDiagnostic { get; private set; } = string.Empty;

        public string NativeLightTrackDiagnostic { get; private set; } = string.Empty;

        public string NativeSelfShadowTrackDiagnostic { get; private set; } = string.Empty;

        // Imported assets carry these counts from the binary summary. A zero count means that a
        // native track is legitimately absent; null means that the source has not been summarized,
        // so a native null handle must remain diagnosable as a creation failure.
        public int? ExpectedCameraKeyframeCount { get; set; }

        public int? ExpectedLightKeyframeCount { get; set; }

        public int? ExpectedSelfShadowKeyframeCount { get; set; }

        private NativeVmdCameraTrackSampler? nativeCameraSampler;
        private byte[]? nativeCameraSamplerSource;
        private bool nativeCameraSamplerUnavailable;
        private NativeVmdLightTrackSampler? nativeLightSampler;
        private byte[]? nativeLightSamplerSource;
        private bool nativeLightSamplerUnavailable;
        private NativeVmdSelfShadowTrackSampler? nativeSelfShadowSampler;
        private byte[]? nativeSelfShadowSamplerSource;
        private bool nativeSelfShadowSamplerUnavailable;
        private bool nativeSelfShadowTrackAbsent;

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
            DisposeNativeCameraSampler();
            DisposeNativeLightSampler();
            DisposeNativeSelfShadowSampler();
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

            if (TrySampleCamera(frame, out MmdCameraState cameraState))
            {
                LastApplyStatus = target.ApplyCameraState(cameraState, MinFieldOfView, ImportScale);
            }
            else
            {
                LastApplyStatus = MmdSceneCameraApplyStatus.NotApplied;
            }

            if (TrySampleLight(frame, out MmdLightState lightState))
            {
                target.ApplyLightState(lightState);
            }

            if (TrySampleNativeSelfShadow(frame, out MmdSelfShadowState selfShadowState))
            {
                target.ApplySelfShadowState(selfShadowState);
            }
            else if (MotionBytes == null || MotionBytes.Length == 0)
            {
                target.TryEvaluateSelfShadowAtFrame(SelfShadowKeyframes, frame, out _);
            }

            return LastApplyStatus;
        }

        private bool TrySampleCamera(float frame, out MmdCameraState state)
        {
            if (TrySampleNativeCamera(frame, out state))
            {
                return true;
            }

            state = MmdCameraState.Default;
            return false;
        }

        private bool TrySampleNativeCamera(float frame, out MmdCameraState state)
        {
            state = MmdCameraState.Default;
            byte[]? motionBytes = MotionBytes;
            if (motionBytes == null || motionBytes.Length == 0)
            {
                DisposeNativeCameraSampler();
                NativeCameraTrackDiagnostic = EmptyCameraTrackDiagnostic;
                return false;
            }

            if (!ReferenceEquals(nativeCameraSamplerSource, motionBytes))
            {
                DisposeNativeCameraSampler();
                nativeCameraSamplerSource = motionBytes;
                nativeCameraSamplerUnavailable = false;
                if (!NativeVmdCameraTrackSampler.TryCreate(
                        motionBytes,
                        out nativeCameraSampler,
                        out string failureReason))
                {
                    nativeCameraSamplerUnavailable = true;
                    NativeCameraTrackDiagnostic = IsExpectedTrackAbsent(ExpectedCameraKeyframeCount, failureReason)
                        ? string.Empty
                        : TrackUnavailable("camera", failureReason);
                    return false;
                }

                NativeCameraTrackDiagnostic = string.Empty;
            }

            if (nativeCameraSamplerUnavailable)
            {
                return false;
            }

            if (nativeCameraSampler != null && nativeCameraSampler.TrySample(frame, out state))
            {
                NativeCameraTrackDiagnostic = string.Empty;
                return true;
            }

            NativeCameraTrackDiagnostic = TrackUnavailable(
                "camera",
                nativeCameraSampler?.LastFailureReason ?? "native sampler is unavailable");
            return false;
        }

        private bool TrySampleLight(float frame, out MmdLightState state)
        {
            if (TrySampleNativeLight(frame, out state))
            {
                return true;
            }

            state = MmdLightState.Default;
            return false;
        }

        private bool TrySampleNativeLight(float frame, out MmdLightState state)
        {
            state = MmdLightState.Default;
            byte[]? motionBytes = MotionBytes;
            if (motionBytes == null || motionBytes.Length == 0)
            {
                DisposeNativeLightSampler();
                NativeLightTrackDiagnostic = EmptyLightTrackDiagnostic;
                return false;
            }

            if (!ReferenceEquals(nativeLightSamplerSource, motionBytes))
            {
                DisposeNativeLightSampler();
                nativeLightSamplerSource = motionBytes;
                nativeLightSamplerUnavailable = false;
                if (!NativeVmdLightTrackSampler.TryCreate(
                        motionBytes,
                        out nativeLightSampler,
                        out string failureReason))
                {
                    nativeLightSamplerUnavailable = true;
                    NativeLightTrackDiagnostic = IsExpectedTrackAbsent(ExpectedLightKeyframeCount, failureReason)
                        ? string.Empty
                        : TrackUnavailable("light", failureReason);
                    return false;
                }

                NativeLightTrackDiagnostic = string.Empty;
            }

            if (nativeLightSamplerUnavailable)
            {
                return false;
            }

            if (nativeLightSampler != null && nativeLightSampler.TrySample(frame, out state))
            {
                NativeLightTrackDiagnostic = string.Empty;
                return true;
            }

            NativeLightTrackDiagnostic = TrackUnavailable(
                "light",
                nativeLightSampler?.LastFailureReason ?? "native sampler is unavailable");
            return false;
        }

        private bool TrySampleNativeSelfShadow(float frame, out MmdSelfShadowState state)
        {
            state = MmdSelfShadowState.Default;
            byte[]? motionBytes = MotionBytes;
            if (motionBytes == null || motionBytes.Length == 0)
            {
                DisposeNativeSelfShadowSampler();
                NativeSelfShadowTrackDiagnostic = EmptySelfShadowTrackDiagnostic;
                return false;
            }

            if (!ReferenceEquals(nativeSelfShadowSamplerSource, motionBytes))
            {
                DisposeNativeSelfShadowSampler();
                nativeSelfShadowSamplerSource = motionBytes;
                nativeSelfShadowSamplerUnavailable = false;
                if (!NativeVmdSelfShadowTrackSampler.TryCreate(
                        motionBytes,
                        out nativeSelfShadowSampler,
                        out string failureReason))
                {
                    nativeSelfShadowSamplerUnavailable = true;
                    nativeSelfShadowTrackAbsent = IsExpectedTrackAbsent(ExpectedSelfShadowKeyframeCount, failureReason);
                    NativeSelfShadowTrackDiagnostic = nativeSelfShadowTrackAbsent
                        ? string.Empty
                        : TrackUnavailable("self-shadow", failureReason);
                    return false;
                }

                NativeSelfShadowTrackDiagnostic = string.Empty;
            }

            if (nativeSelfShadowSamplerUnavailable)
            {
                return false;
            }

            if (nativeSelfShadowSampler != null && nativeSelfShadowSampler.TrySample(frame, out state))
            {
                NativeSelfShadowTrackDiagnostic = string.Empty;
                return true;
            }

            NativeSelfShadowTrackDiagnostic = TrackUnavailable(
                "self-shadow",
                nativeSelfShadowSampler?.LastFailureReason ?? "native sampler is unavailable");
            return false;
        }

        private static string TrackUnavailable(string track, string reason)
        {
            return "VMD native " + track + " track unavailable: " + reason;
        }

        private static bool IsExpectedTrackAbsent(int? expectedKeyframeCount, string failureReason)
        {
            return expectedKeyframeCount == 0 &&
                string.Equals(failureReason, "native track creation returned null", StringComparison.Ordinal);
        }

        private void DisposeNativeCameraSampler()
        {
            nativeCameraSampler?.Dispose();
            nativeCameraSampler = null;
            nativeCameraSamplerSource = null;
            nativeCameraSamplerUnavailable = false;
            NativeCameraTrackDiagnostic = string.Empty;
        }

        private void DisposeNativeLightSampler()
        {
            nativeLightSampler?.Dispose();
            nativeLightSampler = null;
            nativeLightSamplerSource = null;
            nativeLightSamplerUnavailable = false;
            NativeLightTrackDiagnostic = string.Empty;
        }

        private void DisposeNativeSelfShadowSampler()
        {
            nativeSelfShadowSampler?.Dispose();
            nativeSelfShadowSampler = null;
            nativeSelfShadowSamplerSource = null;
            nativeSelfShadowSamplerUnavailable = false;
            nativeSelfShadowTrackAbsent = false;
            NativeSelfShadowTrackDiagnostic = string.Empty;
        }
    }
}
