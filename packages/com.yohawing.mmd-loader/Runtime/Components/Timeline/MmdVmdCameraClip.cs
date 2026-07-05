#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd;
using Mmd.Parser;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    [Serializable]
    public sealed class MmdVmdCameraClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private ExposedReference<MmdSceneEnvironmentBinding> binding;
        [SerializeField] private MmdVmdAsset? motionAsset;
        [SerializeField] private string motionSourceId = string.Empty;
        [SerializeField] private float frameRate = 30.0f;
        [SerializeField] private float startOffsetSeconds;
        [SerializeField] private MmdVmdTimelineLoopPolicy loopPolicy = MmdVmdTimelineLoopPolicy.None;
        [SerializeField] private float minFieldOfView = MmdCameraStateToUnity.DefaultMinFieldOfView;
        [SerializeField] private float importScale = MmdPmxAsset.DefaultImportScale;

        public ClipCaps clipCaps => ClipCaps.None;

        public ExposedReference<MmdSceneEnvironmentBinding> Binding
        {
            get => binding;
            set => binding = value;
        }

        public MmdVmdAsset? MotionAsset
        {
            get => motionAsset;
            set => motionAsset = value;
        }

        // Parity with MmdVmdTimelineClip: an informational source id (defaults from the asset).
        public string MotionSourceId
        {
            get => motionSourceId;
            set => motionSourceId = value ?? string.Empty;
        }

        public float FrameRate
        {
            get => frameRate;
            set
            {
                MmdPlaybackTime.ValidateFrameRate(value);
                frameRate = value;
            }
        }

        public float StartOffsetSeconds
        {
            get => startOffsetSeconds;
            set
            {
                MmdPlaybackTime.ValidateTime(value);
                startOffsetSeconds = value;
            }
        }

        // Parity with MmdVmdTimelineClip. Only None is supported today.
        public MmdVmdTimelineLoopPolicy LoopPolicy
        {
            get => loopPolicy;
            set => loopPolicy = value;
        }

        public float MinFieldOfView
        {
            get => minFieldOfView;
            set => minFieldOfView = value;
        }

        public float ImportScale
        {
            get => NormalizeImportScale(importScale);
            set => importScale = value;
        }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            MmdPlaybackTime.ValidateFrameRate(frameRate);
            MmdPlaybackTime.ValidateTime(startOffsetSeconds);
            ScriptPlayable<MmdVmdCameraBehaviour> playable = ScriptPlayable<MmdVmdCameraBehaviour>.Create(graph);
            MmdVmdCameraBehaviour behaviour = playable.GetBehaviour();
            behaviour.Binding = binding.Resolve(graph.GetResolver());
            behaviour.MotionSourceId = string.IsNullOrWhiteSpace(motionSourceId) && motionAsset != null
                ? (string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId)
                : motionSourceId;
            behaviour.FrameRate = frameRate;
            behaviour.StartOffsetSeconds = startOffsetSeconds;
            behaviour.LoopPolicy = loopPolicy;
            behaviour.MinFieldOfView = minFieldOfView;
            behaviour.ImportScale = ImportScale;
            behaviour.MotionBytes = motionAsset != null && motionAsset.ByteLength > 0
                ? motionAsset.GetBytesCopy()
                : null;
            (
                IReadOnlyList<MmdCameraKeyframeDefinition> cam,
                IReadOnlyList<MmdLightKeyframeDefinition> lit,
                IReadOnlyList<MmdSelfShadowKeyframeDefinition> shd) = LoadSceneKeyframes(motionAsset);
            behaviour.CameraKeyframes = cam;
            behaviour.LightKeyframes = lit;
            behaviour.SelfShadowKeyframes = shd;
            return playable;
        }

        private static float NormalizeImportScale(float value)
        {
            return float.IsFinite(value) && value > 0.0f ? value : MmdPmxAsset.DefaultImportScale;
        }

        private static (
            IReadOnlyList<MmdCameraKeyframeDefinition> camera,
            IReadOnlyList<MmdLightKeyframeDefinition> light,
            IReadOnlyList<MmdSelfShadowKeyframeDefinition> selfShadow) LoadSceneKeyframes(
            MmdVmdAsset? asset)
        {
            if (asset == null || asset.ByteLength <= 0)
            {
                return (
                    Array.Empty<MmdCameraKeyframeDefinition>(),
                    Array.Empty<MmdLightKeyframeDefinition>(),
                    Array.Empty<MmdSelfShadowKeyframeDefinition>());
            }

            try
            {
                MmdMotionDefinition motion = asset.LoadMotion();
                return (motion.cameraKeyframes, motion.lightKeyframes, motion.selfShadowKeyframes);
            }
            catch (Exception ex)
            {
                // Fail soft: a broken camera VMD, a missing native parser, or an IO error must not
                // abort the whole Timeline graph build (which also carries the model lane). The
                // camera/light lane degrades to a no-op and the failure is surfaced as a warning. The broad
                // catch is intentional at this graph-build boundary — there is no safe partial state.
                Debug.LogWarning(
                    $"MmdVmdCameraClip: failed to load camera/light/self-shadow keyframes from '{asset.name}'; the camera/light track is a no-op. {ex.Message}");
                return (
                    Array.Empty<MmdCameraKeyframeDefinition>(),
                    Array.Empty<MmdLightKeyframeDefinition>(),
                    Array.Empty<MmdSelfShadowKeyframeDefinition>());
            }
        }
    }
}
