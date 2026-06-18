#nullable enable

using UnityEngine.Timeline;
using Yohawing.MmdUnity.UnityIntegration;

namespace Yohawing.MmdUnity.Timeline
{
    [TrackClipType(typeof(MmdVmdTimelineClip))]
    [TrackBindingType(typeof(MmdUnityPlaybackController))]
    public sealed class MmdVmdTimelineTrack : TrackAsset
    {
    }
}
