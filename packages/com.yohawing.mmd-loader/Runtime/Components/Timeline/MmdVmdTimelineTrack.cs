#nullable enable

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    [TrackClipType(typeof(MmdVmdTimelineClip))]
    [TrackBindingType(typeof(MmdUnityPlaybackController))]
    public sealed class MmdVmdTimelineTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<MmdVmdTimelineMixerBehaviour>.Create(graph, inputCount);
        }
    }
}
