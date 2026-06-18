#nullable enable

using UnityEngine.Timeline;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    /// <summary>
    /// A Timeline track that drives a global MMD camera. It binds to a
    /// <see cref="MmdSceneEnvironmentBinding"/> proxy (NOT a model), keeping camera motion on its own
    /// lane separate from the per-model <see cref="MmdVmdTimelineTrack"/>.
    /// </summary>
    [TrackClipType(typeof(MmdVmdCameraClip))]
    [TrackBindingType(typeof(MmdSceneEnvironmentBinding))]
    public sealed class MmdVmdCameraTrack : TrackAsset
    {
    }
}
