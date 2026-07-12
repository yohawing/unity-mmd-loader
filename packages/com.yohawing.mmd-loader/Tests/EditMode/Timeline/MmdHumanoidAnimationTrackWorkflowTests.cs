#nullable enable

using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd.Editor.Timeline;
using Mmd.Timeline;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    // Slice 3: authoring wiring for the single MMD Humanoid Track. The workflow must bind the
    // controller, assign the AnimationClip, and auto-resolve the proxy Animator (on the controller's
    // GameObject) into the clip's ExposedReference — so the user adds ONE track + ONE clip and the
    // model drives, with no second track or manual Animator reference.
    public sealed class MmdHumanoidAnimationTrackWorkflowTests
    {
        [Test]
        public void CreateHumanoidAnimationTrackAndClipWiresControllerAndProxyAnimator()
        {
            GameObject? root = null;
            GameObject? directorObject = null;
            TimelineAsset? timeline = null;
            AnimationClip? muscleClip = null;
            try
            {
                root = new GameObject("workflow-root");
                Animator animator = root.AddComponent<Animator>();
                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();

                muscleClip = new AnimationClip { frameRate = 30.0f };
                muscleClip.SetCurve(
                    string.Empty,
                    typeof(Animator),
                    ResolveSpineMuscleName(),
                    AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.5f));
                Assert.That(muscleClip.humanMotion, Is.True, "muscle-curve clip should be recognized as Humanoid motion");

                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                directorObject = new GameObject("workflow-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.playableAsset = timeline;

                MmdHumanoidAnimationTrack track =
                    MmdTimelineAssetWorkflow.CreateHumanoidAnimationTrack(timeline, director, controller);
                Assert.That(director.GetGenericBinding(track), Is.SameAs(controller),
                    "the humanoid track must be bound to the controller");

                TimelineClip clip =
                    MmdTimelineAssetWorkflow.CreateHumanoidAnimationClip(track, muscleClip, controller, director);
                var clipAsset = (MmdHumanoidAnimationClip)clip.asset;

                Assert.That(clipAsset.clip, Is.SameAs(muscleClip), "the AnimationClip must be assigned to the clip asset");
                Assert.That(clip.duration, Is.EqualTo((double)muscleClip.length).Within(0.01),
                    "the clip duration should match the AnimationClip length");
            }
            finally
            {
                if (directorObject != null)
                {
                    Object.DestroyImmediate(directorObject);
                }

                if (timeline != null)
                {
                    Object.DestroyImmediate(timeline);
                }

                if (muscleClip != null)
                {
                    Object.DestroyImmediate(muscleClip);
                }

                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void CreateHumanoidAnimationClipRejectsNonHumanoidClip()
        {
            GameObject? root = null;
            GameObject? directorObject = null;
            TimelineAsset? timeline = null;
            AnimationClip? genericClip = null;
            try
            {
                root = new GameObject("workflow-reject-root");
                root.AddComponent<Animator>();
                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();

                genericClip = new AnimationClip { frameRate = 30.0f };
                genericClip.SetCurve("Bone", typeof(Transform), "localPosition.x",
                    AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 1.0f));
                Assert.That(genericClip.humanMotion, Is.False, "a transform-curve clip must not be Humanoid motion");

                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                directorObject = new GameObject("workflow-reject-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.playableAsset = timeline;

                MmdHumanoidAnimationTrack track =
                    MmdTimelineAssetWorkflow.CreateHumanoidAnimationTrack(timeline, director, controller);

                Assert.Throws<InvalidOperationException>(
                    () => MmdTimelineAssetWorkflow.CreateHumanoidAnimationClip(track, genericClip, controller, director),
                    "a Generic/transform AnimationClip must be rejected (it cannot retarget onto the proxy avatar)");
            }
            finally
            {
                if (directorObject != null)
                {
                    Object.DestroyImmediate(directorObject);
                }

                if (timeline != null)
                {
                    Object.DestroyImmediate(timeline);
                }

                if (genericClip != null)
                {
                    Object.DestroyImmediate(genericClip);
                }

                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void HumanoidAnimationClipAutoResolvesProxyAnimatorFromTrackBinding()
        {
            GameObject? root = null;
            GameObject? directorObject = null;
            TimelineAsset? timeline = null;
            AnimationClip? animationClip = null;
            PlayableGraph graph = default;
            try
            {
                root = new GameObject("workflow-auto-proxy-root");
                Animator animator = root.AddComponent<Animator>();
                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();

                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdHumanoidAnimationTrack track =
                    timeline.CreateTrack<MmdHumanoidAnimationTrack>(null, "MMD Humanoid");
                TimelineClip clip = track.CreateClip<MmdHumanoidAnimationClip>();
                var clipAsset = (MmdHumanoidAnimationClip)clip.asset;
                animationClip = new AnimationClip();
                clipAsset.clip = animationClip;
                directorObject = new GameObject("workflow-auto-proxy-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timeline;
                director.SetGenericBinding(track, controller);

                graph = PlayableGraph.Create();
                Playable playable = clipAsset.CreatePlayable(graph, directorObject);
                Assert.That(playable.IsValid(), Is.True,
                    "clip must derive the proxy Animator from its track-bound playback controller instead of returning Playable.Null");
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                if (directorObject != null)
                {
                    Object.DestroyImmediate(directorObject);
                }

                if (timeline != null)
                {
                    Object.DestroyImmediate(timeline);
                }

                if (animationClip != null)
                {
                    Object.DestroyImmediate(animationClip);
                }

                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void HumanoidAnimationClipHasNoIndependentProxyAnimatorSourceOfTruth()
        {
            Assert.That(
                typeof(MmdHumanoidAnimationClip).GetField("proxyAnimator"),
                Is.Null,
                "Humanoid clips must derive the Animator exclusively from the track-bound playback controller.");
        }

        [Test]
        public void HumanoidAnimationClipPreservesRootMotionDuringEditModeTimelineEvaluation()
        {
            GameObject? root = null;
            GameObject? directorObject = null;
            TimelineAsset? timeline = null;
            AnimationClip? muscleClip = null;
            try
            {
                root = new GameObject("workflow-edit-rootmotion-root");
                Animator animator = root.AddComponent<Animator>();
                animator.applyRootMotion = true;
                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();

                muscleClip = new AnimationClip { frameRate = 30.0f };
                muscleClip.SetCurve(
                    string.Empty,
                    typeof(Animator),
                    ResolveSpineMuscleName(),
                    AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.5f));
                Assert.That(muscleClip.humanMotion, Is.True, "muscle-curve clip should be recognized as Humanoid motion");

                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdHumanoidAnimationTrack track =
                    timeline.CreateTrack<MmdHumanoidAnimationTrack>(null, "MMD Humanoid");
                TimelineClip clip = track.CreateClip<MmdHumanoidAnimationClip>();
                var clipAsset = (MmdHumanoidAnimationClip)clip.asset;
                clipAsset.clip = muscleClip;
                TimelineClip secondClip = track.CreateClip<MmdHumanoidAnimationClip>();
                secondClip.start = 1.0;
                var secondClipAsset = (MmdHumanoidAnimationClip)secondClip.asset;
                secondClipAsset.clip = muscleClip;

                directorObject = new GameObject("workflow-edit-rootmotion-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.playableAsset = timeline;
                director.SetGenericBinding(track, controller);

                director.time = 0.0;
                director.Evaluate();

                Assert.That(animator.applyRootMotion, Is.True,
                    "Edit Mode Timeline evaluation must preserve the Animator's serialized root-motion setting.");

                director.Stop();

                Assert.That(animator.applyRootMotion, Is.True,
                    "Edit Mode Timeline evaluation should restore the user's serialized applyRootMotion setting after the graph stops.");
            }
            finally
            {
                if (directorObject != null)
                {
                    Object.DestroyImmediate(directorObject);
                }

                if (timeline != null)
                {
                    Object.DestroyImmediate(timeline);
                }

                if (muscleClip != null)
                {
                    Object.DestroyImmediate(muscleClip);
                }

                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static string ResolveSpineMuscleName()
        {
            for (int dof = 0; dof < 3; dof++)
            {
                int muscle = HumanTrait.MuscleFromBone((int)HumanBodyBones.Spine, dof);
                if (muscle >= 0)
                {
                    return HumanTrait.MuscleName[muscle];
                }
            }

            return string.Empty;
        }
    }
}
