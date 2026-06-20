#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd.Parser;
using Mmd.Timeline;
using Mmd.UnityIntegration;
using UnityObject = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed class MmdHumanoidRetargetTimelineTests
    {
        private static readonly HumanBodyBones[] TestedBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Head,
            HumanBodyBones.LeftHand
        };

        [Test]
        public void ApplyRetargetFromTimelineCopiesProxyRotationsWithoutAnimatorController()
        {
            RetargetFixture fixture = CreateFixture();
            try
            {
                Vector3[] nativePositionsBefore = CapturePositions(fixture.NativeBones);
                SetProxyRotations(fixture.ProxyRig);

                MmdHumanoidRetargeterResult result = fixture.Retargeter.ApplyRetargetFromTimeline();

                Assert.That(fixture.Retargeter.LastGate, Is.EqualTo(MmdHumanoidRuntimeRetargetGate.Ready));
                Assert.That(result.CopiedBoneCount, Is.EqualTo(TestedBones.Length));
                Assert.That(result.SkippedBoneCount, Is.EqualTo(0));

                for (int i = 0; i < TestedBones.Length; i++)
                {
                    HumanBodyBones humanBone = TestedBones[i];
                    int nativeIndex = fixture.IndexByHumanBone[humanBone];
                    Transform proxy = fixture.ProxyRig.BoneMap[humanBone];
                    Transform native = fixture.NativeBones[nativeIndex];

                    Assert.That(Quaternion.Angle(native.localRotation, proxy.localRotation),
                        Is.LessThan(0.001f), humanBone + " native rotation should match proxy");
                    Assert.That(native.localPosition, Is.EqualTo(nativePositionsBefore[nativeIndex]),
                        humanBone + " native localPosition must remain unchanged");
                }
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void GatherPropertiesRegistersNativeLocalRotationsOnly()
        {
            RetargetFixture fixture = CreateFixture();
            TimelineAsset? timelineAsset = null;
            GameObject? directorObject = null;
            try
            {
                var nativeWithNullEntry = new List<MmdHumanoidRetargetBinding>(fixture.Retargeter.Entries)
                {
                    new MmdHumanoidRetargetBinding(
                        HumanBodyBones.RightHand,
                        100,
                        fixture.ProxyRig.BoneMap[HumanBodyBones.LeftHand],
                        null)
                };
                fixture.Retargeter.Configure(
                    fixture.ProxyRig.ProxyRoot!.transform,
                    nativeWithNullEntry,
                    System.Array.Empty<MmdHumanoidAppendTransformBinding>());

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdHumanoidRetargetTrack track =
                    timelineAsset.CreateTrack<MmdHumanoidRetargetTrack>(null, "MMD Humanoid Retarget");
                track.CreateClip<MmdHumanoidRetargetClip>();

                directorObject = new GameObject("humanoid-retarget-gather-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timelineAsset;
                director.SetGenericBinding(track, fixture.Retargeter);

                var collector = new SpyPropertyCollector();
                track.GatherProperties(director, collector);

                string[] expectedRotationProperties =
                {
                    "m_LocalRotation.x",
                    "m_LocalRotation.y",
                    "m_LocalRotation.z",
                    "m_LocalRotation.w"
                };
                IReadOnlyList<SpyPropertyCollector.Entry> rotationEntries = collector.Entries;
                Assert.That(rotationEntries, Has.Count.EqualTo(TestedBones.Length * expectedRotationProperties.Length));
                foreach (HumanBodyBones humanBone in TestedBones)
                {
                    Transform native = fixture.NativeBones[fixture.IndexByHumanBone[humanBone]];
                    foreach (string propertyName in expectedRotationProperties)
                    {
                        Assert.That(rotationEntries.Count(entry =>
                                entry.GameObject == native.gameObject &&
                                entry.ComponentType == typeof(Transform) &&
                                entry.PropertyName == propertyName),
                            Is.EqualTo(1),
                            humanBone + " should register " + propertyName);
                    }
                }

                Assert.That(rotationEntries.Any(entry => entry.PropertyName.Contains("m_LocalPosition")),
                    Is.False);
                Assert.That(rotationEntries.Any(entry => entry.PropertyName.Contains("m_LocalScale")),
                    Is.False);
            }
            finally
            {
                if (directorObject != null)
                {
                    UnityObject.DestroyImmediate(directorObject);
                }

                if (timelineAsset != null)
                {
                    UnityObject.DestroyImmediate(timelineAsset);
                }

                fixture.Destroy();
            }
        }

        [Test]
        public void GatherPropertiesRegistersTranslationTargetLocalPositionWhenBindingRequestsIt()
        {
            RetargetFixture fixture = CreateFixture();
            TimelineAsset? timelineAsset = null;
            GameObject? directorObject = null;
            GameObject? centerObject = null;
            try
            {
                HumanBodyBones hipsBone = HumanBodyBones.Hips;
                Transform proxyHips = fixture.ProxyRig.BoneMap[hipsBone];
                Transform nativeHips = fixture.NativeBones[fixture.IndexByHumanBone[hipsBone]];
                centerObject = new GameObject("native-center");
                Transform center = centerObject.transform;
                center.SetParent(fixture.Root.transform, worldPositionStays: false);

                var entries = new List<MmdHumanoidRetargetBinding>();
                foreach (MmdHumanoidRetargetBinding entry in fixture.Retargeter.Entries)
                {
                    if (entry.HumanBone == hipsBone)
                    {
                        entries.Add(new MmdHumanoidRetargetBinding(
                            hipsBone,
                            entry.MmdBoneIndex,
                            proxyHips,
                            nativeHips,
                            copyLocalPosition: true,
                            translationTargetTransform: center,
                            translationTargetMmdBoneIndex: 100,
                            proxyBindLocalPosition: proxyHips.localPosition,
                            translationTargetBindLocalPosition: center.localPosition));
                    }
                    else
                    {
                        entries.Add(entry);
                    }
                }

                fixture.Retargeter.Configure(
                    fixture.ProxyRig.ProxyRoot!.transform,
                    entries,
                    System.Array.Empty<MmdHumanoidAppendTransformBinding>());

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdHumanoidRetargetTrack track =
                    timelineAsset.CreateTrack<MmdHumanoidRetargetTrack>(null, "MMD Humanoid Retarget");
                track.CreateClip<MmdHumanoidRetargetClip>();

                directorObject = new GameObject("humanoid-retarget-gather-translation-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timelineAsset;
                director.SetGenericBinding(track, fixture.Retargeter);

                var collector = new SpyPropertyCollector();
                track.GatherProperties(director, collector);

                string[] expectedPositionProperties =
                {
                    "m_LocalPosition.x",
                    "m_LocalPosition.y",
                    "m_LocalPosition.z"
                };

                foreach (string propertyName in expectedPositionProperties)
                {
                    Assert.That(collector.Entries.Count(entry =>
                            entry.GameObject == center.gameObject &&
                            entry.ComponentType == typeof(Transform) &&
                            entry.PropertyName == propertyName),
                        Is.EqualTo(1),
                        "translation target should register " + propertyName);
                }

                Assert.That(collector.Entries.Any(entry =>
                        entry.GameObject == nativeHips.gameObject &&
                        entry.PropertyName.Contains("m_LocalPosition")),
                    Is.False,
                    "rotation target should not receive localPosition registration unless it is the translation target");
            }
            finally
            {
                if (directorObject != null)
                {
                    UnityObject.DestroyImmediate(directorObject);
                }

                if (timelineAsset != null)
                {
                    UnityObject.DestroyImmediate(timelineAsset);
                }

                if (centerObject != null)
                {
                    UnityObject.DestroyImmediate(centerObject);
                }

                fixture.Destroy();
            }
        }

        [Test]
        public void GatherPropertiesRegistersAppendTargetLocalRotationAndPosition()
        {
            RetargetFixture fixture = CreateFixture();
            TimelineAsset? timelineAsset = null;
            GameObject? directorObject = null;
            GameObject? appendParentObject = null;
            GameObject? appendTargetObject = null;
            try
            {
                appendParentObject = new GameObject("append-parent");
                appendTargetObject = new GameObject("append-target");
                Transform appendParent = appendParentObject.transform;
                Transform appendTarget = appendTargetObject.transform;

                var appendEntries = new[]
                {
                    new MmdHumanoidAppendTransformBinding(
                        appendTarget,
                        targetMmdBoneIndex: 101,
                        appendParent,
                        appendParentMmdBoneIndex: 100,
                        appendRatio: 0.75f,
                        appendRotation: true,
                        appendTranslation: true,
                        appendLocal: false,
                        appendTarget.localRotation,
                        appendTarget.localPosition,
                        appendParent.localRotation,
                        appendParent.localPosition,
                        evaluationOrder: 101)
                };
                fixture.Retargeter.Configure(
                    fixture.ProxyRig.ProxyRoot!.transform,
                    fixture.Retargeter.Entries,
                    appendEntries);

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdHumanoidRetargetTrack track =
                    timelineAsset.CreateTrack<MmdHumanoidRetargetTrack>(null, "MMD Humanoid Retarget");
                track.CreateClip<MmdHumanoidRetargetClip>();

                directorObject = new GameObject("humanoid-retarget-gather-append-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timelineAsset;
                director.SetGenericBinding(track, fixture.Retargeter);

                var collector = new SpyPropertyCollector();
                track.GatherProperties(director, collector);

                string[] expectedProperties =
                {
                    "m_LocalRotation.x",
                    "m_LocalRotation.y",
                    "m_LocalRotation.z",
                    "m_LocalRotation.w",
                    "m_LocalPosition.x",
                    "m_LocalPosition.y",
                    "m_LocalPosition.z"
                };

                foreach (string propertyName in expectedProperties)
                {
                    Assert.That(collector.Entries.Count(entry =>
                            entry.GameObject == appendTarget.gameObject &&
                            entry.ComponentType == typeof(Transform) &&
                            entry.PropertyName == propertyName),
                        Is.EqualTo(1),
                        "append target should register " + propertyName);
                }
            }
            finally
            {
                if (directorObject != null)
                {
                    UnityObject.DestroyImmediate(directorObject);
                }

                if (timelineAsset != null)
                {
                    UnityObject.DestroyImmediate(timelineAsset);
                }

                if (appendParentObject != null)
                {
                    UnityObject.DestroyImmediate(appendParentObject);
                }

                if (appendTargetObject != null)
                {
                    UnityObject.DestroyImmediate(appendTargetObject);
                }

                fixture.Destroy();
            }
        }

        [Test]
        public void ShouldSuppressLateUpdateAfterTimelineDriveMatchesPlaybackControllerWindow()
        {
            Assert.That(MmdHumanoidRuntimeRetargeter.ShouldSuppressLateUpdateAfterTimelineDrive(10, 10),
                Is.True);
            Assert.That(MmdHumanoidRuntimeRetargeter.ShouldSuppressLateUpdateAfterTimelineDrive(10, 11),
                Is.True);
            Assert.That(MmdHumanoidRuntimeRetargeter.ShouldSuppressLateUpdateAfterTimelineDrive(10, 12),
                Is.False);
            Assert.That(MmdHumanoidRuntimeRetargeter.ShouldSuppressLateUpdateAfterTimelineDrive(10, 9),
                Is.False);
        }

        private static RetargetFixture CreateFixture()
        {
            MmdModelDefinition model = CreateHumanoidMappingModelWithOrigins();
            MmdHumanoidProxyRigResult proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.That(proxyRig.ProxyRoot, Is.Not.Null);
            MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyRig);
            Assert.That(avatarResult.IsValidHumanAvatar, Is.True, string.Join("\n", avatarResult.Diagnostics));

            var root = new GameObject("TimelineRetargeterRoot");
            proxyRig.ProxyRoot!.transform.SetParent(root.transform, worldPositionStays: false);
            proxyRig.ProxyRoot.SetActive(true);

            var animator = root.AddComponent<Animator>();
            animator.avatar = avatarResult.Avatar;
            Assert.That(animator.runtimeAnimatorController, Is.Null,
                "Timeline retarget fixture must verify the no-controller gate relaxation.");

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
            retargeter.Configure(proxyRig.ProxyRoot.transform, entries, System.Array.Empty<MmdHumanoidAppendTransformBinding>());

            return new RetargetFixture(
                root,
                proxyRig,
                nativeBones,
                indexByHumanBone,
                retargeter,
                avatarResult.Avatar);
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

        private static Vector3[] CapturePositions(IReadOnlyList<Transform> bones)
        {
            var values = new Vector3[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                values[i] = bones[i].localPosition;
            }

            return values;
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
                Avatar? avatar)
            {
                Root = root;
                ProxyRig = proxyRig;
                NativeBones = nativeBones;
                IndexByHumanBone = indexByHumanBone;
                Retargeter = retargeter;
                Avatar = avatar;
            }

            public GameObject Root { get; }
            public MmdHumanoidProxyRigResult ProxyRig { get; }
            public Transform[] NativeBones { get; }
            public IReadOnlyDictionary<HumanBodyBones, int> IndexByHumanBone { get; }
            public MmdHumanoidRuntimeRetargeter Retargeter { get; }
            public Avatar? Avatar { get; }

            public void Destroy()
            {
                if (Root != null)
                {
                    UnityObject.DestroyImmediate(Root);
                }

                if (Avatar != null)
                {
                    UnityObject.DestroyImmediate(Avatar);
                }
            }
        }

        private sealed class SpyPropertyCollector : IPropertyCollector
        {
            private readonly List<Entry> entries = new();
            private readonly Stack<GameObject> activeGameObjects = new();

            public IReadOnlyList<Entry> Entries => entries;

            public void PushActiveGameObject(GameObject gameObject)
            {
                activeGameObjects.Push(gameObject);
            }

            public void PopActiveGameObject()
            {
                activeGameObjects.Pop();
            }

            public void AddFromClip(AnimationClip clip)
            {
            }

            public void AddFromClips(IEnumerable<AnimationClip> clips)
            {
            }

            public void AddFromName<T>(string name) where T : Component
            {
                entries.Add(new Entry(activeGameObjects.Peek(), typeof(T), name));
            }

            public void AddFromName(string name)
            {
                entries.Add(new Entry(activeGameObjects.Peek(), null, name));
            }

            public void AddFromClip(GameObject obj, AnimationClip clip)
            {
            }

            public void AddFromClips(GameObject obj, IEnumerable<AnimationClip> clips)
            {
            }

            public void AddFromName<T>(GameObject obj, string name) where T : Component
            {
                entries.Add(new Entry(obj, typeof(T), name));
            }

            public void AddFromName(GameObject obj, string name)
            {
                entries.Add(new Entry(obj, null, name));
            }

            public void AddFromName(Component component, string name)
            {
                entries.Add(new Entry(component.gameObject, component.GetType(), name));
            }

            public void AddFromComponent(GameObject obj, Component component)
            {
            }

            public void AddObjectProperties(UnityObject obj, AnimationClip clip)
            {
            }

            public readonly struct Entry
            {
                public Entry(GameObject gameObject, Type? componentType, string propertyName)
                {
                    GameObject = gameObject;
                    ComponentType = componentType;
                    PropertyName = propertyName;
                }

                public GameObject GameObject { get; }
                public Type? ComponentType { get; }
                public string PropertyName { get; }
            }
        }
    }
}
