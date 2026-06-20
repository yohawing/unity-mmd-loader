#nullable enable

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Mmd.Timeline
{
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
