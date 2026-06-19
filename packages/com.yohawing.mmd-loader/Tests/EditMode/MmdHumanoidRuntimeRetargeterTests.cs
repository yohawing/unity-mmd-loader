#nullable enable

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using Mmd.Parser;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdHumanoidRuntimeRetargeterTests
    {
        private static readonly HumanBodyBones[] TestedBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Head,
            HumanBodyBones.LeftHand
        };

        [Test]
        public void ApplyRetargetNowCopiesProxyRotationsAndPreservesNativePositionsWhenAnimatorIsDriven()
        {
            RetargetFixture fixture = CreateFixture(controllerAssigned: true);
            try
            {
                Quaternion[] nativeBeforeRotations = CaptureRotations(fixture.NativeBones);
                Vector3[] nativeBeforePositions = CapturePositions(fixture.NativeBones);
                SetProxyRotations(fixture.ProxyRig);

                MmdHumanoidRetargeterResult result = fixture.Retargeter.ApplyRetargetNow();

                Assert.That(fixture.Retargeter.LastGate, Is.EqualTo(MmdHumanoidRuntimeRetargetGate.Ready));
                Assert.That(result.CopiedBoneCount, Is.EqualTo(TestedBones.Length));
                Assert.That(result.SkippedBoneCount, Is.EqualTo(0));

                for (int i = 0; i < TestedBones.Length; i++)
                {
                    HumanBodyBones humanBone = TestedBones[i];
                    int nativeIndex = fixture.IndexByHumanBone[humanBone];
                    Transform proxy = fixture.ProxyRig.BoneMap[humanBone];
                    Transform native = fixture.NativeBones[nativeIndex];

                    Assert.That(Quaternion.Angle(native.localRotation, nativeBeforeRotations[nativeIndex]),
                        Is.GreaterThan(0.1f), humanBone + " native rotation should change");
                    Assert.That(Quaternion.Angle(native.localRotation, proxy.localRotation),
                        Is.LessThan(0.001f), humanBone + " native rotation should match proxy");
                    Assert.That(native.localPosition, Is.EqualTo(nativeBeforePositions[nativeIndex]),
                        humanBone + " native localPosition must remain unchanged");
                }
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void ApplyRetargetNowSkipsWithoutAnimatorControllerOrPlayableDriver()
        {
            RetargetFixture fixture = CreateFixture(controllerAssigned: false);
            try
            {
                Quaternion[] before = CaptureRotations(fixture.NativeBones);
                SetProxyRotations(fixture.ProxyRig);

                MmdHumanoidRetargeterResult result = fixture.Retargeter.ApplyRetargetNow();

                Assert.That(fixture.Retargeter.LastGate, Is.EqualTo(MmdHumanoidRuntimeRetargetGate.AnimatorNotDriven));
                Assert.That(result.CopiedBoneCount, Is.EqualTo(0));
                AssertNativeRotationsUnchanged(fixture.NativeBones, before);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void ApplyRetargetNowSkipsWhenPlaybackControllerIsTimelineDriven()
        {
            RetargetFixture fixture = CreateFixture(controllerAssigned: true);
            try
            {
                fixture.Root.AddComponent<MmdUnityPlaybackController>();
                FieldInfo? field = typeof(MmdUnityPlaybackController).GetField(
                    "lastTimelineDriveFrameCount",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                field!.SetValue(fixture.Root.GetComponent<MmdUnityPlaybackController>(), Time.frameCount);

                Quaternion[] before = CaptureRotations(fixture.NativeBones);
                SetProxyRotations(fixture.ProxyRig);

                MmdHumanoidRetargeterResult result = fixture.Retargeter.ApplyRetargetNow();

                Assert.That(fixture.Retargeter.LastGate, Is.EqualTo(MmdHumanoidRuntimeRetargetGate.PlaybackControllerDriving));
                Assert.That(result.CopiedBoneCount, Is.EqualTo(0));
                AssertNativeRotationsUnchanged(fixture.NativeBones, before);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        private static RetargetFixture CreateFixture(bool controllerAssigned)
        {
            MmdModelDefinition model = CreateHumanoidMappingModelWithOrigins();
            MmdHumanoidProxyRigResult proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.That(proxyRig.ProxyRoot, Is.Not.Null);
            MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyRig);
            Assert.That(avatarResult.IsValidHumanAvatar, Is.True, string.Join("\n", avatarResult.Diagnostics));

            var root = new GameObject("RuntimeRetargeterRoot");
            proxyRig.ProxyRoot!.transform.SetParent(root.transform, worldPositionStays: false);
            proxyRig.ProxyRoot.SetActive(true);

            var animator = root.AddComponent<Animator>();
            animator.avatar = avatarResult.Avatar;
            RuntimeAnimatorController? controller = null;
            if (controllerAssigned)
            {
                var animatorController = new AnimatorController();
                animatorController.AddLayer("Base");
                controller = animatorController;
                animator.runtimeAnimatorController = controller;
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null,
                    "Driven retarget fixture must install a valid RuntimeAnimatorController.");
            }

            Transform[] nativeBones = CreateNativeBones(root, model.bones.Count);
            var entries = new List<MmdHumanoidRetargetBinding>();
            var indexByHumanBone = new Dictionary<HumanBodyBones, int>();
            foreach (HumanBodyBones humanBone in TestedBones)
            {
                MmdHumanoidBoneMappingMatch match = FindMatch(proxyRig, humanBone);
                entries.Add(new MmdHumanoidRetargetBinding(
                    humanBone,
                    match.MmdBoneIndex,
                    proxyRig.BoneMap[humanBone],
                    nativeBones[match.MmdBoneIndex]));
                indexByHumanBone[humanBone] = match.MmdBoneIndex;
            }

            MmdHumanoidRuntimeRetargeter retargeter = root.AddComponent<MmdHumanoidRuntimeRetargeter>();
            retargeter.Configure(proxyRig.ProxyRoot.transform, entries);

            return new RetargetFixture(
                root,
                proxyRig,
                nativeBones,
                indexByHumanBone,
                retargeter,
                avatarResult.Avatar,
                controller);
        }

        private static Transform[] CreateNativeBones(GameObject root, int count)
        {
            var bones = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                var bone = new GameObject("native-" + i);
                bone.transform.SetParent(root.transform, worldPositionStays: false);
                bone.transform.localPosition = new Vector3(10f + i, 20f + i, 30f + i);
                bone.transform.localRotation = Quaternion.identity;
                bones[i] = bone.transform;
            }

            return bones;
        }

        private static void SetProxyRotations(MmdHumanoidProxyRigResult proxyRig)
        {
            for (int i = 0; i < TestedBones.Length; i++)
            {
                proxyRig.BoneMap[TestedBones[i]].localRotation =
                    Quaternion.Euler(8f + i * 7f, 12f + i * 5f, 16f + i * 3f);
            }
        }

        private static Quaternion[] CaptureRotations(IReadOnlyList<Transform> bones)
        {
            var values = new Quaternion[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                values[i] = bones[i].localRotation;
            }

            return values;
        }

        private static Vector3[] CapturePositions(IReadOnlyList<Transform> bones)
        {
            var values = new Vector3[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                values[i] = bones[i].localPosition;
            }

            return values;
        }

        private static void AssertNativeRotationsUnchanged(IReadOnlyList<Transform> nativeBones, IReadOnlyList<Quaternion> before)
        {
            for (int i = 0; i < nativeBones.Count; i++)
            {
                Assert.That(Quaternion.Angle(nativeBones[i].localRotation, before[i]), Is.LessThan(0.001f),
                    "native bone " + i + " must not be written when retarget gate is closed");
            }
        }

        private static MmdHumanoidBoneMappingMatch FindMatch(
            MmdHumanoidProxyRigResult proxyRig,
            HumanBodyBones humanBone)
        {
            foreach (MmdHumanoidBoneMappingMatch match in proxyRig.Matches)
            {
                if (match.HumanBone == humanBone)
                {
                    return match;
                }
            }

            throw new AssertionException("Missing proxy match for " + humanBone);
        }

        private static MmdModelDefinition CreateHumanoidMappingModelWithOrigins()
        {
            var model = new MmdModelDefinition();
            AddBone(model, 0, "下半身", -1, new[] { 0f, 90f, 0f });
            AddBone(model, 1, "上半身", 0, new[] { 0f, 115f, 0f });
            AddBone(model, 2, "首", 1, new[] { 0f, 150f, 0f });
            AddBone(model, 3, "頭", 2, new[] { 0f, 165f, 0f });
            AddBone(model, 4, "左足", 0, new[] { 8f, 85f, 0f });
            AddBone(model, 5, "左ひざ", 4, new[] { 8f, 45f, 0f });
            AddBone(model, 6, "左足首", 5, new[] { 8f, 5f, 3f });
            AddBone(model, 7, "右足", 0, new[] { -8f, 85f, 0f });
            AddBone(model, 8, "右ひざ", 7, new[] { -8f, 45f, 0f });
            AddBone(model, 9, "右足首", 8, new[] { -8f, 5f, 3f });
            AddBone(model, 10, "左腕", 1, new[] { 25f, 135f, 0f });
            AddBone(model, 11, "左ひじ", 10, new[] { 50f, 135f, 0f });
            AddBone(model, 12, "左手首", 11, new[] { 70f, 135f, 0f });
            AddBone(model, 13, "右腕", 1, new[] { -25f, 135f, 0f });
            AddBone(model, 14, "右ひじ", 13, new[] { -50f, 135f, 0f });
            AddBone(model, 15, "右手首", 14, new[] { -70f, 135f, 0f });
            return model;
        }

        private static void AddBone(MmdModelDefinition model, int index, string name, int parentIndex, float[] origin)
        {
            model.bones.Add(new MmdBoneDefinition
            {
                index = index,
                name = name,
                parentIndex = parentIndex,
                origin = origin
            });
        }

        private sealed class RetargetFixture
        {
            public RetargetFixture(
                GameObject root,
                MmdHumanoidProxyRigResult proxyRig,
                Transform[] nativeBones,
                IReadOnlyDictionary<HumanBodyBones, int> indexByHumanBone,
                MmdHumanoidRuntimeRetargeter retargeter,
                Avatar? avatar,
                RuntimeAnimatorController? controller)
            {
                Root = root;
                ProxyRig = proxyRig;
                NativeBones = nativeBones;
                IndexByHumanBone = indexByHumanBone;
                Retargeter = retargeter;
                Avatar = avatar;
                Controller = controller;
            }

            public GameObject Root { get; }
            public MmdHumanoidProxyRigResult ProxyRig { get; }
            public Transform[] NativeBones { get; }
            public IReadOnlyDictionary<HumanBodyBones, int> IndexByHumanBone { get; }
            public MmdHumanoidRuntimeRetargeter Retargeter { get; }
            public Avatar? Avatar { get; }
            public RuntimeAnimatorController? Controller { get; }

            public void Destroy()
            {
                if (Root != null)
                {
                    Object.DestroyImmediate(Root);
                }

                if (Avatar != null)
                {
                    Object.DestroyImmediate(Avatar);
                }

                if (Controller != null)
                {
                    Object.DestroyImmediate(Controller);
                }
            }
        }
    }
}
