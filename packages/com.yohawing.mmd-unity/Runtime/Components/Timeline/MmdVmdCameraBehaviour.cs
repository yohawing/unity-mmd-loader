#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Playables;
using Yohawing.MmdUnity;
using Yohawing.MmdUnity.Motion;
using Yohawing.MmdUnity.Parser;
using Yohawing.MmdUnity.UnityIntegration;

namespace Yohawing.MmdUnity.Timeline
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

        public IReadOnlyList<MmdCameraKeyframeDefinition> CameraKeyframes { get; set; }
            = Array.Empty<MmdCameraKeyframeDefinition>();

        public IReadOnlyList<MmdLightKeyframeDefinition> LightKeyframes { get; set; }
            = Array.Empty<MmdLightKeyframeDefinition>();

        public string MotionSourceId { get; set; } = string.Empty;

        public float FrameRate { get; set; } = 30.0f;

        public float StartOffsetSeconds { get; set; }

        public MmdVmdTimelineLoopPolicy LoopPolicy { get; set; }

        public float MinFieldOfView { get; set; } = MmdCameraStateToUnity.DefaultMinFieldOfView;

        public MmdSceneCameraApplyStatus LastApplyStatus { get; private set; }

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

        /// <summary>
        /// Samples and applies the camera at the given Timeline local time. Returns the proxy's apply
        /// status; when the track has no camera keyframes it is a no-op (<see cref="MmdSceneCameraApplyStatus.NotApplied"/>).
        /// </summary>
        public MmdSceneCameraApplyStatus EvaluateAtLocalTime(MmdSceneEnvironmentBinding target, double localTime)
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

            if (CameraKeyframes != null && CameraKeyframes.Count > 0)
            {
                MmdCameraState cs = VmdCameraSampler.Sample(CameraKeyframes, frame);
                LastApplyStatus = target.ApplyCameraState(cs, MinFieldOfView);
            }
            else
            {
                LastApplyStatus = MmdSceneCameraApplyStatus.NotApplied;
            }

            if (LightKeyframes != null && LightKeyframes.Count > 0)
            {
                MmdLightState ls = VmdLightSampler.Sample(LightKeyframes, frame);
                target.ApplyLightState(ls);
            }

            return LastApplyStatus;
        }
    }
}
