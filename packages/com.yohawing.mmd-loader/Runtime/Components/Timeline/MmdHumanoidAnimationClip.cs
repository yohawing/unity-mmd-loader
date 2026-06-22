#nullable enable

using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Mmd.Timeline
{
    [Serializable]
    public sealed class MmdHumanoidAnimationClip : PlayableAsset, ITimelineClipAsset
    {
        public AnimationClip? clip;

        public ExposedReference<Animator> proxyAnimator;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            if (clip == null)
            {
                return Playable.Null;
            }

            var resolver = owner.GetComponent<PlayableDirector>() as IExposedPropertyTable;
            Animator? animator = resolver != null ? proxyAnimator.Resolve(resolver) : null;
            if (animator == null)
            {
                return Playable.Null;
            }

            animator.applyRootMotion = true;

            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, clip);
            AnimationPlayableOutput output =
                AnimationPlayableOutput.Create(graph, "MmdHumanoidProxyAnim", animator);
            output.SetSourcePlayable(clipPlayable);

            return clipPlayable;
        }
    }
}
