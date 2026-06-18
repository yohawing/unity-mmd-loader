#nullable enable

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    [Serializable]
    public sealed class MmdVmdTimelineClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private ExposedReference<MmdUnityPlaybackController> controller;
        [SerializeField] private MmdVmdAsset? motionAsset;
        [SerializeField] private string modelSourceId = string.Empty;
        [SerializeField] private string motionSourceId = string.Empty;
        [SerializeField] private float frameRate = 30.0f;
        [SerializeField] private float startOffsetSeconds;
        [SerializeField] private MmdVmdTimelineLoopPolicy loopPolicy = MmdVmdTimelineLoopPolicy.None;

        public ClipCaps clipCaps => ClipCaps.None;

        public ExposedReference<MmdUnityPlaybackController> Controller
        {
            get => controller;
            set => controller = value;
        }

        public MmdVmdAsset? MotionAsset
        {
            get => motionAsset;
            set => motionAsset = value;
        }

        public string ModelSourceId
        {
            get => modelSourceId;
            set => modelSourceId = value ?? string.Empty;
        }

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

        public MmdVmdTimelineLoopPolicy LoopPolicy
        {
            get => loopPolicy;
            set => loopPolicy = value;
        }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            MmdPlaybackTime.ValidateFrameRate(frameRate);
            MmdPlaybackTime.ValidateTime(startOffsetSeconds);
            ScriptPlayable<MmdVmdTimelineBehaviour> playable = ScriptPlayable<MmdVmdTimelineBehaviour>.Create(graph);
            MmdVmdTimelineBehaviour behaviour = playable.GetBehaviour();
            behaviour.Controller = controller.Resolve(graph.GetResolver());
            behaviour.MotionAsset = motionAsset;
            behaviour.ModelSourceId = modelSourceId;
            behaviour.MotionSourceId = string.IsNullOrWhiteSpace(motionSourceId) && motionAsset != null
                ? (string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId)
                : motionSourceId;
            behaviour.FrameRate = frameRate;
            behaviour.StartOffsetSeconds = startOffsetSeconds;
            behaviour.LoopPolicy = loopPolicy;
            return playable;
        }
    }
}
