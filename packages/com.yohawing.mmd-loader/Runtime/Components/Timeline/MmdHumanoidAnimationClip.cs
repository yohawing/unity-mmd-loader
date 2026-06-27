#nullable enable

using System;
using System.Collections.Generic;
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

            PlayableDirector? director = owner.GetComponent<PlayableDirector>();
            var resolver = director as IExposedPropertyTable;
            Animator? animator = resolver != null ? proxyAnimator.Resolve(resolver) : null;
            if (animator == null)
            {
                return Playable.Null;
            }

            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, clip);
            ScriptPlayable<MmdHumanoidAnimationRootMotionGuardBehaviour> guardPlayable =
                ScriptPlayable<MmdHumanoidAnimationRootMotionGuardBehaviour>.Create(graph, 1);
            guardPlayable.GetBehaviour().Initialize(animator, Application.isPlaying, director);
            graph.Connect(clipPlayable, 0, guardPlayable, 0);
            guardPlayable.SetInputWeight(0, 1.0f);
            guardPlayable.SetPropagateSetTime(true);
            ScriptPlayableOutput guardOutput =
                ScriptPlayableOutput.Create(graph, "MmdHumanoidRootMotionGuard");
            guardOutput.SetSourcePlayable(guardPlayable);

            AnimationPlayableOutput output =
                AnimationPlayableOutput.Create(graph, "MmdHumanoidProxyAnim", animator);
            output.SetSourcePlayable(clipPlayable);
            output.SetWeight(1.0f);

            return guardPlayable;
        }
    }

    internal sealed class MmdHumanoidAnimationRootMotionGuardBehaviour : PlayableBehaviour
    {
        private sealed class GuardState
        {
            public GuardState(Animator animator)
            {
                Animator = animator;
                OriginalApplyRootMotion = animator.applyRootMotion;
            }

            public Animator Animator { get; }

            public bool OriginalApplyRootMotion { get; }

            public int ReferenceCount { get; set; }
        }

        private static readonly Dictionary<Animator, GuardState> GuardStates = new Dictionary<Animator, GuardState>();

        private Animator? guardedAnimator;
        private PlayableDirector? guardedDirector;
        private bool initialized;

        public void Initialize(
            Animator animator,
            bool applyRootMotionDuringEvaluation,
            PlayableDirector? director)
        {
            guardedAnimator = animator;
            guardedDirector = director;
            initialized = true;
            if (guardedDirector != null)
            {
                guardedDirector.stopped += OnDirectorStopped;
            }

            if (!GuardStates.TryGetValue(animator, out GuardState state))
            {
                state = new GuardState(animator);
                GuardStates.Add(animator, state);
            }

            state.ReferenceCount++;
            animator.applyRootMotion = applyRootMotionDuringEvaluation;
        }

        public override void OnGraphStop(Playable playable)
        {
            Restore();
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            Restore();
        }

        private void OnDirectorStopped(PlayableDirector stoppedDirector)
        {
            if (stoppedDirector == guardedDirector)
            {
                Restore();
            }
        }

        private void Restore()
        {
            if (guardedDirector != null)
            {
                guardedDirector.stopped -= OnDirectorStopped;
                guardedDirector = null;
            }

            if (!initialized)
            {
                return;
            }

            initialized = false;
            if (guardedAnimator == null || !GuardStates.TryGetValue(guardedAnimator, out GuardState state))
            {
                return;
            }

            state.ReferenceCount--;
            if (state.ReferenceCount > 0)
            {
                return;
            }

            GuardStates.Remove(guardedAnimator);
            if (state.Animator != null)
            {
                state.Animator.applyRootMotion = state.OriginalApplyRootMotion;
            }

            guardedAnimator = null;
        }
    }
}
