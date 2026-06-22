#nullable enable

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Mmd.Timeline
{
    [Obsolete(
        "Deprecated: use MmdHumanoidAnimationClip on an MmdHumanoidAnimationTrack instead. " +
        "Kept functional for existing timelines; see docs/design/humanoid-retarget-ux-alternatives.md.")]
    [Serializable]
    public sealed class MmdHumanoidRetargetClip : PlayableAsset, ITimelineClipAsset
    {
        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<MmdHumanoidRetargetBehaviour>.Create(graph);
        }
    }
}
