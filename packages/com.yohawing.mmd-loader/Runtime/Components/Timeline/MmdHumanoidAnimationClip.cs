#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    [Serializable]
    public sealed class MmdHumanoidAnimationClip : PlayableAsset, ITimelineClipAsset
    {
        public AnimationClip? clip;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            if (clip == null)
            {
                return Playable.Null;
            }

            PlayableDirector? director = owner.GetComponent<PlayableDirector>();
            Animator? animator = director != null ? ResolveProxyAnimator(director) : null;
            if (animator == null)
            {
                return Playable.Null;
            }

            if (!Application.isPlaying)
            {
                ScriptPlayable<MmdHumanoidEditModeSampleBehaviour> editModePlayable =
                    ScriptPlayable<MmdHumanoidEditModeSampleBehaviour>.Create(graph, 0);
                editModePlayable.GetBehaviour().Initialize(animator, clip);
                return editModePlayable;
            }

            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, clip);
            ScriptPlayable<MmdHumanoidAnimationRootMotionGuardBehaviour> guardPlayable =
                ScriptPlayable<MmdHumanoidAnimationRootMotionGuardBehaviour>.Create(graph, 1);
            guardPlayable.GetBehaviour().Initialize(animator, animator.applyRootMotion, director);
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

        private Animator? ResolveProxyAnimator(PlayableDirector director)
        {
            if (director == null)
            {
                return null;
            }

            if (director.playableAsset is not TimelineAsset timelineAsset)
            {
                return null;
            }

            foreach (TrackAsset track in timelineAsset.GetOutputTracks())
            {
                bool ownsThisClip = false;
                foreach (TimelineClip timelineClip in track.GetClips())
                {
                    if (ReferenceEquals(timelineClip.asset, this))
                    {
                        ownsThisClip = true;
                        break;
                    }
                }

                if (!ownsThisClip)
                {
                    continue;
                }

                MmdUnityPlaybackController? controller =
                    director.GetGenericBinding(track) as MmdUnityPlaybackController;
                return controller != null ? controller.GetComponent<Animator>() : null;
            }

            return null;
        }
    }

    internal sealed class MmdHumanoidEditModeSampleBehaviour : PlayableBehaviour
    {
        private Animator? animator;
        private AnimationClip? clip;
        private Vector3 baselinePosition;
        private Quaternion baselineRotation = Quaternion.identity;
        private bool initialized;
#if UNITY_EDITOR
        private bool ownsAnimationMode;
#endif

        public void Initialize(Animator targetAnimator, AnimationClip animationClip)
        {
            animator = targetAnimator;
            clip = animationClip;
            baselinePosition = targetAnimator.transform.position;
            baselineRotation = targetAnimator.transform.rotation;
            initialized = true;
        }

        public override void PrepareFrame(Playable playable, FrameData info)
        {
            if (animator == null || clip == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!UnityEditor.AnimationMode.InAnimationMode())
            {
                UnityEditor.AnimationMode.StartAnimationMode();
                ownsAnimationMode = true;
            }
            UnityEditor.AnimationMode.SampleAnimationClip(
                animator.gameObject,
                clip,
                (float)playable.GetTime());
#else
            clip.SampleAnimation(animator.gameObject, (float)playable.GetTime());
#endif
        }

        public override void OnGraphStop(Playable playable)
        {
            RestoreBaseline();
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            RestoreBaseline();
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            RestoreBaseline();
        }

        private void RestoreBaseline()
        {
            if (!initialized || animator == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (ownsAnimationMode && UnityEditor.AnimationMode.InAnimationMode())
            {
                UnityEditor.AnimationMode.StopAnimationMode();
            }
            ownsAnimationMode = false;
#endif
            initialized = false;
            animator.transform.SetPositionAndRotation(baselinePosition, baselineRotation);
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
