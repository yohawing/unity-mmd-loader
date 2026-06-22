#nullable enable

using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd.Parser;
using Mmd.Timeline;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    // Slice 2 verification for Alternative C: when scrubbing the single MmdHumanoidAnimationTrack
    // in EDIT mode (the authoring experience), AnimationMode must drive the track's manually-created
    // AnimationPlayableOutput so the proxy avatar poses from the muscle clip, and the track's
    // ProcessFrame must retarget that posed proxy onto the native MMD bone.
    //
    // Marked [Explicit] on purpose: AnimationMode in a live Editor session is known to leave
    // "Access version should be odd" noise that can pollute later tests in the same run, so this
    // is run on demand (filter) rather than in the default suite. The permanent runtime regression
    // is the PlayMode test HumanoidAnimationTrackSingleTrackMuscleClipDrivesProxyAndRetargetsAndStepsPhysics.
    public sealed class MmdHumanoidAnimationTrackEditScrubTests
    {
        [Test]
        [Explicit("Uses UnityEditor.AnimationMode; run on demand to avoid AnimationMode session noise polluting later tests.")]
        public void EditScrubPosesProxyAndRetargetsNativeViaAnimationMode()
        {
            GameObject? root = null;
            Avatar? avatar = null;
            TimelineAsset? timeline = null;
            AnimationClip? muscleClip = null;
            GameObject? directorObject = null;
            MmdHumanoidProxyRigResult? proxyRig = null;
            bool animationModeStarted = false;
            try
            {
                root = new GameObject("edit-scrub-root");
                var nativeBoneObject = new GameObject("NativeSpine");
                nativeBoneObject.transform.SetParent(root.transform, worldPositionStays: false);

                proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(CreateHumanoidMappingModel());
                Assert.That(proxyRig.ProxyRoot, Is.Not.Null);
                proxyRig.ProxyRoot!.transform.SetParent(root.transform, worldPositionStays: false);
                proxyRig.ProxyRoot.SetActive(true);
                MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyRig);
                Assert.That(avatarResult.IsValidHumanAvatar, Is.True, string.Join("\n", avatarResult.Diagnostics));
                avatar = avatarResult.Avatar;

                Animator animator = root.AddComponent<Animator>();
                animator.avatar = avatar;

                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();

                Transform proxySpine = proxyRig.BoneMap[HumanBodyBones.Spine];
                Transform nativeSpine = nativeBoneObject.transform;
                Quaternion proxySpineBind = proxySpine.localRotation;
                Quaternion nativeSpineBind = nativeSpine.localRotation;
                controller.ConfigureHumanoidRetarget(
                    proxyRig.ProxyRoot.transform,
                    new[]
                    {
                        new MmdHumanoidRetargetBinding(
                            HumanBodyBones.Spine,
                            0,
                            proxySpine,
                            nativeSpine,
                            proxySpineBind,
                            nativeSpineBind)
                    },
                    Array.Empty<MmdHumanoidAppendTransformBinding>());

                string spineMuscleName = ResolveSpineMuscleName();
                Assert.That(spineMuscleName, Is.Not.Null.And.Not.Empty);
                muscleClip = new AnimationClip { frameRate = 30.0f };
                muscleClip.SetCurve(
                    string.Empty,
                    typeof(Animator),
                    spineMuscleName,
                    AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.9f));

                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdHumanoidAnimationTrack track =
                    timeline.CreateTrack<MmdHumanoidAnimationTrack>(null, "MMD Humanoid");
                TimelineClip clip = track.CreateClip<MmdHumanoidAnimationClip>();
                clip.start = 0.0;
                clip.duration = 1.0;
                var clipAsset = (MmdHumanoidAnimationClip)clip.asset;
                clipAsset.clip = muscleClip;
                clipAsset.proxyAnimator.exposedName = "editScrubProxyAnimator_" + Guid.NewGuid().ToString("N");

                directorObject = new GameObject("edit-scrub-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.playableAsset = timeline;
                director.SetGenericBinding(track, controller);
                director.SetReferenceValue(clipAsset.proxyAnimator.exposedName, animator);

                AnimationMode.StartAnimationMode();
                animationModeStarted = true;
                director.RebuildGraph();

                // Scrub to 0.5s and re-evaluate at the same time to settle the accepted 1-eval lag.
                director.time = 0.5;
                director.Evaluate();
                director.time = 0.5;
                director.Evaluate();

                // (a) AnimationMode drives the single track's AnimationPlayableOutput -> proxy posed
                float proxyAngle = Quaternion.Angle(proxySpineBind, proxySpine.localRotation);
                Assert.That(proxyAngle, Is.GreaterThan(2.0f),
                    "AnimationMode edit-scrub should pose the proxy spine via the single track (angle " + proxyAngle + ")");

                // (b) the track's ProcessFrame retargeted the posed proxy onto the native bone
                Assert.That(controller.LastHumanoidRetargetGate, Is.EqualTo(MmdHumanoidRetargetGate.Ready));
                float nativeAngle = Quaternion.Angle(nativeSpineBind, nativeSpine.localRotation);
                Assert.That(nativeAngle, Is.EqualTo(proxyAngle).Within(0.5f),
                    "native bone should track the proxy spine delta after settle (proxy " + proxyAngle +
                    " native " + nativeAngle + ")");

                // (c) the documented 1-eval lag exists on a fresh time jump but converges on re-eval
                director.time = 0.2;
                director.Evaluate();
                director.time = 0.2;
                director.Evaluate();
                float proxySettled = Quaternion.Angle(proxySpineBind, proxySpine.localRotation);
                float nativeSettled = Quaternion.Angle(nativeSpineBind, nativeSpine.localRotation);
                Assert.That(Mathf.Abs(proxySettled - nativeSettled), Is.LessThan(0.5f),
                    "native should converge to the proxy on same-time re-evaluation");
            }
            finally
            {
                if (animationModeStarted)
                {
                    AnimationMode.StopAnimationMode();
                }

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

                if (avatar != null)
                {
                    Object.DestroyImmediate(avatar);
                }

                if (proxyRig?.ProxyRoot != null)
                {
                    Object.DestroyImmediate(proxyRig.ProxyRoot);
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

        private static MmdModelDefinition CreateHumanoidMappingModel()
        {
            var model = new MmdModelDefinition();
            AddHumanoidBone(model, 0, "下半身", -1, new[] { 0f, 90f, 0f });
            AddHumanoidBone(model, 1, "上半身", 0, new[] { 0f, 115f, 0f });
            AddHumanoidBone(model, 2, "首", 1, new[] { 0f, 150f, 0f });
            AddHumanoidBone(model, 3, "頭", 2, new[] { 0f, 165f, 0f });
            AddHumanoidBone(model, 4, "左足", 0, new[] { 8f, 85f, 0f });
            AddHumanoidBone(model, 5, "左ひざ", 4, new[] { 8f, 45f, 0f });
            AddHumanoidBone(model, 6, "左足首", 5, new[] { 8f, 5f, 3f });
            AddHumanoidBone(model, 7, "右足", 0, new[] { -8f, 85f, 0f });
            AddHumanoidBone(model, 8, "右ひざ", 7, new[] { -8f, 45f, 0f });
            AddHumanoidBone(model, 9, "右足首", 8, new[] { -8f, 5f, 3f });
            AddHumanoidBone(model, 10, "左腕", 1, new[] { 25f, 135f, 0f });
            AddHumanoidBone(model, 11, "左ひじ", 10, new[] { 50f, 135f, 0f });
            AddHumanoidBone(model, 12, "左手首", 11, new[] { 70f, 135f, 0f });
            AddHumanoidBone(model, 13, "右腕", 1, new[] { -25f, 135f, 0f });
            AddHumanoidBone(model, 14, "右ひじ", 13, new[] { -50f, 135f, 0f });
            AddHumanoidBone(model, 15, "右手首", 14, new[] { -70f, 135f, 0f });
            return model;
        }

        private static void AddHumanoidBone(MmdModelDefinition model, int index, string name, int parentIndex, float[] origin)
        {
            model.bones.Add(new MmdBoneDefinition
            {
                index = index,
                name = name,
                parentIndex = parentIndex,
                origin = origin
            });
        }
    }
}
