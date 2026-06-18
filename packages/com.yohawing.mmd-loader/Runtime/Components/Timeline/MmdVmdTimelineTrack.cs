#nullable enable

using UnityEngine.Timeline;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    [TrackClipType(typeof(MmdVmdTimelineClip))]
    [TrackBindingType(typeof(MmdUnityPlaybackController))]
    public sealed class MmdVmdTimelineTrack : TrackAsset
    {
    }
}
