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

        private NativeVmdCameraTrackSampler? nativeCameraSampler;
        private byte[]? nativeCameraSamplerSource;
        private bool nativeCameraSamplerUnavailable;
        private NativeVmdLightTrackSampler? nativeLightSampler;
        private byte[]? nativeLightSamplerSource;
        private bool nativeLightSamplerUnavailable;

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

            target.TryEvaluateSelfShadowAtFrame(SelfShadowKeyframes, frame, out _);

            return LastApplyStatus;
        }

        private bool TrySampleCamera(float frame, out MmdCameraState state)
        {
            if (TrySampleNativeCamera(frame, out state))
            {
                return true;
            }

            if (CameraKeyframes != null && CameraKeyframes.Count > 0)
            {
                state = VmdCameraSampler.Sample(CameraKeyframes, frame);
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
                return false;
            }

            if (!ReferenceEquals(nativeCameraSamplerSource, motionBytes))
            {
                DisposeNativeCameraSampler();
                nativeCameraSamplerSource = motionBytes;
                nativeCameraSamplerUnavailable = false;
                if (!NativeVmdCameraTrackSampler.TryCreate(motionBytes, out nativeCameraSampler))
                {
                    nativeCameraSamplerUnavailable = true;
                    return false;
                }
            }

            if (nativeCameraSamplerUnavailable)
            {
                return false;
            }

            return nativeCameraSampler != null && nativeCameraSampler.TrySample(frame, out state);
        }

        private bool TrySampleLight(float frame, out MmdLightState state)
        {
            if (TrySampleNativeLight(frame, out state))
            {
                return true;
            }

            if (LightKeyframes != null && LightKeyframes.Count > 0)
            {
                state = VmdLightSampler.Sample(LightKeyframes, frame);
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
                return false;
            }

            if (!ReferenceEquals(nativeLightSamplerSource, motionBytes))
            {
                DisposeNativeLightSampler();
                nativeLightSamplerSource = motionBytes;
                nativeLightSamplerUnavailable = false;
                if (!NativeVmdLightTrackSampler.TryCreate(motionBytes, out nativeLightSampler))
                {
                    nativeLightSamplerUnavailable = true;
                    return false;
                }
            }

            if (nativeLightSamplerUnavailable)
            {
                return false;
            }

            return nativeLightSampler != null && nativeLightSampler.TrySample(frame, out state);
        }

        private void DisposeNativeCameraSampler()
        {
            nativeCameraSampler?.Dispose();
            nativeCameraSampler = null;
            nativeCameraSamplerSource = null;
            nativeCameraSamplerUnavailable = false;
        }

        private void DisposeNativeLightSampler()
        {
            nativeLightSampler?.Dispose();
            nativeLightSampler = null;
            nativeLightSamplerSource = null;
            nativeLightSamplerUnavailable = false;
        }
    }
}
