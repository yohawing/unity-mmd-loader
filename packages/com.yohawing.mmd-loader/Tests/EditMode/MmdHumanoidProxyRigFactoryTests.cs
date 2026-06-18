#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using NUnit.Framework;
using UnityEngine;
using Mmd.Parser;

namespace Mmd.Tests
{
    public sealed class MmdHumanoidProxyRigFactoryTests
    {
        [Test]
        public void ProxyRigFactoryCreatesHiddenRootWithMappedTransforms()
        {
            // Arrange: a model with full standard MMD bone names (16 required + 3 optional).
            MmdModelDefinition model = CreateHumanoidMappingModel(
                "下半身",
                "上半身",
                "首",
                "頭",
                "左足",
                "左ひざ",
                "左足首",
                "右足",
                "右ひざ",
                "右足首",
                "左腕",
                "左ひじ",
                "左手首",
                "右腕",
                "右ひじ",
                "右手首",
                "上半身2",
                "左肩",
                "右肩");

            // Act
            MmdHumanoidProxyRigResult result = MmdHumanoidProxyRigFactory.CreateProxyRig(model);

            // Assert: root is created and hidden
            Assert.That(result.ProxyRoot, Is.Not.Null, "proxy root must be created");
            Assert.That(result.ProxyRoot!.hideFlags, Is.EqualTo(HideFlags.HideInHierarchy),
                "root must have HideInHierarchy flag");
            Assert.That(result.ProxyRoot.activeSelf, Is.False,
                "root must be inactive");

            // Assert: major HumanBodyBones transforms exist
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.Hips), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.Spine), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.Neck), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.Head), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftUpperLeg), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftLowerLeg), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftFoot), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.RightUpperLeg), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.RightLowerLeg), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.RightFoot), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftUpperArm), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftLowerArm), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftHand), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.RightUpperArm), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.RightLowerArm), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.RightHand), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.Chest), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftShoulder), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.RightShoulder), Is.True);

            // Assert: total 19 mapped (16 required + 3 optional)
            Assert.That(result.BoneMap.Count, Is.EqualTo(19));

            // Assert: readiness is Ready
            Assert.That(result.Readiness, Is.EqualTo(MmdHumanoidSetupAsset.ReadyReadiness));

            // Assert: each transform has HideInHierarchy and is named after HumanBodyBones
            foreach (Transform t in result.BoneMap.Values)
            {
                Assert.That(t.hideFlags, Is.EqualTo(HideFlags.HideInHierarchy),
                    "each proxy bone Transform must be hidden");
                Assert.That(t.name, Does.Not.Contain("_"), "name should be HumanBodyBones enum name");
            }

            // Cleanup
            Object.DestroyImmediate(result.ProxyRoot);
        }

        [Test]
        public void ProxyRigFactoryCreatesShoulderToeEyeTransformsAsOptionalMappedBones()
        {
            // Arrange: full required bones plus shoulders, toes, and eyes.
            MmdModelDefinition model = CreateHumanoidMappingModel(
                "下半身",
                "上半身",
                "首",
                "頭",
                "左足",
                "左ひざ",
                "左足首",
                "右足",
                "右ひざ",
                "右足首",
                "左腕",
                "左ひじ",
                "左手首",
                "右腕",
                "右ひじ",
                "右手首",
                "上半身2",
                "左肩",
                "右肩",
                "左つま先",
                "右つま先",
                "左目",
                "右目");

            // Act
            MmdHumanoidProxyRigResult result = MmdHumanoidProxyRigFactory.CreateProxyRig(model);

            // Assert: optional shoulder / toe / eye bones are mapped and hidden.
            Assert.That(result.ProxyRoot, Is.Not.Null, "proxy root must exist");
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftShoulder), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.RightShoulder), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftToes), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.RightToes), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftEye), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.RightEye), Is.True);

            Assert.That(result.BoneMap[HumanBodyBones.LeftToes].hideFlags, Is.EqualTo(HideFlags.HideInHierarchy));
            Assert.That(result.BoneMap[HumanBodyBones.RightToes].hideFlags, Is.EqualTo(HideFlags.HideInHierarchy));
            Assert.That(result.BoneMap[HumanBodyBones.LeftEye].hideFlags, Is.EqualTo(HideFlags.HideInHierarchy));
            Assert.That(result.BoneMap[HumanBodyBones.RightEye].hideFlags, Is.EqualTo(HideFlags.HideInHierarchy));

            // 16 required + 7 optional = 23 mappings.
            Assert.That(result.BoneMap.Count, Is.EqualTo(23));
            Assert.That(result.Readiness, Is.EqualTo(MmdHumanoidSetupAsset.ReadyReadiness));

            // Cleanup
            Object.DestroyImmediate(result.ProxyRoot);
        }

        [Test]
        public void ProxyRigIgnoresIkTwistAndHelperBones()
        {
            // Arrange: add IK, twist, and operation helper bones alongside standard names.
            MmdModelDefinition model = CreateHumanoidMappingModel(
                "下半身",
                "上半身",
                "首",
                "頭",
                "左足",
                "左ひざ",
                "左足首",
                "右足",
                "右ひざ",
                "右足首",
                "左腕",
                "左ひじ",
                "左手首",
                "右腕",
                "右ひじ",
                "右手首",
                "左足ＩＫ",
                "右腕捩",
                "左目操作",
                "左足IK親",
                "右足IK",
                "腰捩");

            // Act
            MmdHumanoidProxyRigResult result = MmdHumanoidProxyRigFactory.CreateProxyRig(model);

            // Assert: standard bones are mapped
            Assert.That(result.ProxyRoot, Is.Not.Null);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.Hips), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.Head), Is.True);

            // Assert: IK/twist/operation helper bones are NOT created as transforms
            foreach (string ignoredName in new[] { "左足ＩＫ", "右腕捩", "左目操作", "左足IK親", "右足IK", "腰捩" })
            {
                bool foundInMatches = false;
                foreach (MmdHumanoidBoneMappingMatch match in result.Matches)
                {
                    if (match.MmdBoneName == ignoredName)
                    {
                        foundInMatches = true;
                        break;
                    }
                }
                Assert.That(foundInMatches, Is.False,
                    "helper bone '" + ignoredName + "' must not appear in matches");
            }

            // Assert: total mapped count is 16 (no helpers)
            Assert.That(result.BoneMap.Count, Is.EqualTo(16));

            // Cleanup
            Object.DestroyImmediate(result.ProxyRoot);
        }

        [Test]
        public void ProxyRigReportsMissingRequiredReadinessWithIncompleteBones()
        {
            // Arrange: only 3 required bones
            MmdModelDefinition model = CreateHumanoidMappingModel("下半身", "上半身", "頭");

            // Act
            MmdHumanoidProxyRigResult result = MmdHumanoidProxyRigFactory.CreateProxyRig(model);

            // Assert: readiness is MissingRequired
            Assert.That(result.Readiness, Is.EqualTo(MmdHumanoidSetupAsset.MissingRequiredReadiness));
            Assert.That(result.ProxyRoot, Is.Not.Null,
                "proxy root should still be created with whatever bones are available");
            Assert.That(result.BoneMap.Count, Is.EqualTo(3),
                "only the 3 available bones should be mapped");

            // Assert: diagnostics contain missing-required
            bool hasMissingDiagnostic = false;
            foreach (string d in result.Diagnostics)
            {
                if (d.Contains("missing-required"))
                {
                    hasMissingDiagnostic = true;
                    break;
                }
            }
            Assert.That(hasMissingDiagnostic, Is.True, "diagnostics must mention missing required bones");

            // Cleanup
            Object.DestroyImmediate(result.ProxyRoot);
        }

        [Test]
        public void ProxyRigDoesNotCreateAvatarOrAnimatorOrStoreAvatarFields()
        {
            // Arrange
            MmdModelDefinition model = CreateHumanoidMappingModel(
                "下半身", "上半身", "首", "頭",
                "左足", "左ひざ", "左足首",
                "右足", "右ひざ", "右足首",
                "左腕", "左ひじ", "左手首",
                "右腕", "右ひじ", "右手首");

            // Act
            MmdHumanoidProxyRigResult result = MmdHumanoidProxyRigFactory.CreateProxyRig(model);

            // Assert: no Avatar or Animator on the proxy root or any child
            Assert.That(result.ProxyRoot, Is.Not.Null);
            Assert.That(result.ProxyRoot!.GetComponentsInChildren<Animator>(includeInactive: true), Is.Empty,
                "proxy rig must not create Animator components");

            foreach (Transform child in result.ProxyRoot.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (child == result.ProxyRoot.transform)
                {
                    continue;
                }

                Assert.That(child.GetComponent<Animator>(), Is.Null,
                    "proxy bone " + child.name + " must not have an Animator component");
            }

            foreach (System.Reflection.FieldInfo field in typeof(MmdHumanoidProxyRigResult).GetFields(
                         System.Reflection.BindingFlags.Instance |
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.NonPublic))
            {
                Assert.That(field.FieldType, Is.Not.EqualTo(typeof(Avatar)), field.Name);
            }

            foreach (System.Reflection.PropertyInfo property in typeof(MmdHumanoidProxyRigResult).GetProperties(
                         System.Reflection.BindingFlags.Instance |
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.NonPublic))
            {
                Assert.That(property.PropertyType, Is.Not.EqualTo(typeof(Avatar)), property.Name);
            }

            // Cleanup
            Object.DestroyImmediate(result.ProxyRoot);
        }

        [Test]
        public void ProxyRigHierarchyMirrorsMmdParentChildStructure()
        {
            // Arrange: create a model with parent-child relationships and origins.
            // Origins (MMD space):
            //   0: 下半身 origin=[0, 0, 0]   (hips at model origin)
            //   1: 上半身 origin=[0, 10, 0]  (spine above hips)
            //   2: 首    origin=[0, 18, 0]   (neck)
            //   3: 頭    origin=[0, 20, 0]   (head)
            //   4: 左足  origin=[-4, 0, 0]   (left leg, MMD left = -x)
            //   5: 左ひざ origin=[-4, -6, 0] (knee)
            //   6: 左足首 origin=[-4, -12, 0](ankle)
            var model = new MmdModelDefinition();
            AddBoneWithOrigin(model, 0, "下半身", -1, new[] { 0f, 0f, 0f });
            AddBoneWithOrigin(model, 1, "上半身", 0, new[] { 0f, 10f, 0f });
            AddBoneWithOrigin(model, 2, "首", 1, new[] { 0f, 18f, 0f });
            AddBoneWithOrigin(model, 3, "頭", 2, new[] { 0f, 20f, 0f });
            AddBoneWithOrigin(model, 4, "左足", 0, new[] { -4f, 0f, 0f });
            AddBoneWithOrigin(model, 5, "左ひざ", 4, new[] { -4f, -6f, 0f });
            AddBoneWithOrigin(model, 6, "左足首", 5, new[] { -4f, -12f, 0f });

            // Expected Unity-space positions [-x, y, -z]:
            //   0: [0, 0, 0]
            //   1: [0, 10, 0]     parent=0: local = [0,10,0]-[0,0,0] = [0,10,0]
            //   2: [0, 18, 0]     parent=1: local = [0,18,0]-[0,10,0] = [0,8,0]
            //   3: [0, 20, 0]     parent=2: local = [0,20,0]-[0,18,0] = [0,2,0]
            //   4: [4, 0, 0]      parent=0: local = [4,0,0]-[0,0,0] = [4,0,0]
            //   5: [4, -6, 0]     parent=4: local = [4,-6,0]-[4,0,0] = [0,-6,0]
            //   6: [4, -12, 0]    parent=5: local = [4,-12,0]-[4,-6,0] = [0,-6,0]

            // Act
            MmdHumanoidProxyRigResult result = MmdHumanoidProxyRigFactory.CreateProxyRig(model);

            // Assert: hierarchy mirrors parent relationships
            Assert.That(result.ProxyRoot, Is.Not.Null);
            Transform hips = result.BoneMap[HumanBodyBones.Hips];
            Transform spine = result.BoneMap[HumanBodyBones.Spine];
            Transform neck = result.BoneMap[HumanBodyBones.Neck];
            Transform head = result.BoneMap[HumanBodyBones.Head];
            Transform leftUpperLeg = result.BoneMap[HumanBodyBones.LeftUpperLeg];
            Transform leftLowerLeg = result.BoneMap[HumanBodyBones.LeftLowerLeg];
            Transform leftFoot = result.BoneMap[HumanBodyBones.LeftFoot];

            // All bones should be children of the proxy root
            Assert.That(hips.parent, Is.EqualTo(result.ProxyRoot.transform),
                "Hips should be direct child of proxy root");

            // spine should be child of hips (上半身 parent is 下半身)
            Assert.That(spine.parent, Is.EqualTo(hips),
                "Spine should be child of Hips");

            // neck should be child of spine (首 parent is 上半身)
            Assert.That(neck.parent, Is.EqualTo(spine),
                "Neck should be child of Spine");

            // head should be child of neck (頭 parent is 首)
            Assert.That(head.parent, Is.EqualTo(neck),
                "Head should be child of Neck");

            // leftUpperLeg should be child of hips (左足 parent is 下半身)
            Assert.That(leftUpperLeg.parent, Is.EqualTo(hips),
                "LeftUpperLeg should be child of Hips");

            // leftLowerLeg should be child of leftUpperLeg (左ひざ parent is 左足)
            Assert.That(leftLowerLeg.parent, Is.EqualTo(leftUpperLeg),
                "LeftLowerLeg should be child of LeftUpperLeg");

            // leftFoot should be child of leftLowerLeg (左足首 parent is 左ひざ)
            Assert.That(leftFoot.parent, Is.EqualTo(leftLowerLeg),
                "LeftFoot should be child of LeftLowerLeg");

            // Assert: localPosition from origin (parent-relative)
            Assert.That(hips.localPosition, Is.EqualTo(new Vector3(0, 0, 0)).Within(1e-5f),
                "Hips localPosition should be derived from origin");
            Assert.That(spine.localPosition, Is.EqualTo(new Vector3(0, 10, 0)).Within(1e-5f),
                "Spine localPosition should be parent-relative from origin");
            Assert.That(neck.localPosition, Is.EqualTo(new Vector3(0, 8, 0)).Within(1e-5f),
                "Neck localPosition should be parent-relative from origin");
            Assert.That(head.localPosition, Is.EqualTo(new Vector3(0, 2, 0)).Within(1e-5f),
                "Head localPosition should be parent-relative from origin");
            Assert.That(leftUpperLeg.localPosition, Is.EqualTo(new Vector3(4, 0, 0)).Within(1e-5f),
                "LeftUpperLeg localPosition should be parent-relative from origin");
            Assert.That(leftLowerLeg.localPosition, Is.EqualTo(new Vector3(0, -6, 0)).Within(1e-5f),
                "LeftLowerLeg localPosition should be parent-relative from origin");
            Assert.That(leftFoot.localPosition, Is.EqualTo(new Vector3(0, -6, 0)).Within(1e-5f),
                "LeftFoot localPosition should be parent-relative from origin");

            // Cleanup
            Object.DestroyImmediate(result.ProxyRoot);
        }

        [Test]
        public void BuildAvatarWithFullBonesRunsBuildAndReturnsAvatar()
        {
            // Arrange: full required bone set with anatomically plausible synthetic origins.
            MmdModelDefinition model = CreateHumanoidMappingModelWithOrigins();

            // Act: create proxy rig first, then build avatar.
            MmdHumanoidProxyRigResult proxyResult = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.That(proxyResult.ProxyRoot, Is.Not.Null);
            Assert.That(proxyResult.Readiness, Is.EqualTo(MmdHumanoidSetupAsset.ReadyReadiness));

            MmdHumanoidAvatarBuildResult avatarResult =
                MmdHumanoidProxyRigFactory.BuildAvatar(proxyResult);

            // Assert: diagnostics produced
            Assert.That(avatarResult.Diagnostics, Is.Not.Empty,
                "BuildAvatar must produce diagnostics");
            Assert.That(avatarResult.Avatar, Is.Not.Null,
                "full required synthetic proxy rig should call BuildHumanAvatar and return an Avatar object");
            Assert.That(avatarResult.IsValidHumanAvatar, Is.True,
                "full required synthetic proxy rig should produce a valid human Avatar");
            Assert.That(avatarResult.Avatar!.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
            Assert.That(avatarResult.Diagnostics.Count, Is.GreaterThanOrEqualTo(2),
                "BuildAvatar should produce multiple diagnostic entries");
            bool hasAvatarBuildPrefix = false;
            foreach (string d in avatarResult.Diagnostics)
            {
                if (d.StartsWith("avatar-build:"))
                    hasAvatarBuildPrefix = true;
            }
            Assert.That(hasAvatarBuildPrefix, Is.True,
                "at least one diagnostic should start with 'avatar-build:'");

            // No Animator created
            Assert.That(proxyResult.ProxyRoot!.GetComponentsInChildren<Animator>(includeInactive: true),
                Is.Empty, "BuildAvatar must not create Animator");

            // The MmdHumanoidAvatarBuildResult does not reference or store Animator
            System.Type avatarResultType = typeof(MmdHumanoidAvatarBuildResult);
            foreach (System.Reflection.PropertyInfo prop in avatarResultType.GetProperties(
                         System.Reflection.BindingFlags.Instance |
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.NonPublic))
            {
                Assert.That(prop.PropertyType, Is.Not.EqualTo(typeof(Animator)),
                    "MmdHumanoidAvatarBuildResult must not have an Animator property: " + prop.Name);
            }

            // Cleanup
            if (avatarResult.Avatar != null)
            {
                Object.DestroyImmediate(avatarResult.Avatar, allowDestroyingAssets: true);
            }
            Object.DestroyImmediate(proxyResult.ProxyRoot);
        }

        [Test]
        public void BuildAvatarWithMissingBonesStillReportsDiagnosticsAndNoAnimator()
        {
            // Arrange: only 3 required bones with origins.
            MmdModelDefinition model = CreateHumanoidMappingModel("下半身", "上半身", "頭");
            // Add origins so the proxy rig positions are valid.
            foreach (MmdBoneDefinition b in model.bones)
            {
                if (b.origin == null || b.origin.Length < 3)
                    b.origin = new[] { 0f, (float)b.index * 10f, 0f };
            }

            MmdHumanoidProxyRigResult proxyResult = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.That(proxyResult.ProxyRoot, Is.Not.Null);
            Assert.That(proxyResult.Readiness, Is.EqualTo(MmdHumanoidSetupAsset.MissingRequiredReadiness));

            // Act
            MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyResult);

            // Assert: AvatarBuilder was not called, avoiding Unity error logs for incomplete skeletons.
            Assert.That(avatarResult.Diagnostics, Is.Not.Empty,
                "BuildAvatar must produce diagnostics even with missing bones");
            Assert.That(avatarResult.Avatar, Is.Null);
            Assert.That(string.Join("\n", avatarResult.Diagnostics), Does.Contain("skipped because proxy rig readiness is MissingRequired"));

            // No Animator
            Assert.That(proxyResult.ProxyRoot!.GetComponentsInChildren<Animator>(includeInactive: true),
                Is.Empty, "BuildAvatar must not create Animator with missing bones");

            // Cleanup
            if (avatarResult.Avatar != null)
            {
                Object.DestroyImmediate(avatarResult.Avatar, allowDestroyingAssets: true);
            }
            Object.DestroyImmediate(proxyResult.ProxyRoot);
        }

        [Test]
        public void BuildAvatarWithNoBonesThrowsInvalidOperation()
        {
            // Arrange: no bones -> ProxyRoot is null.
            var model = new MmdModelDefinition();
            MmdHumanoidProxyRigResult proxyResult = MmdHumanoidProxyRigFactory.CreateProxyRig(model);

            Assert.That(proxyResult.ProxyRoot, Is.Null);
            Assert.That(proxyResult.Readiness, Is.EqualTo(MmdHumanoidSetupAsset.NoBonesReadiness));

            // Act & Assert
            System.InvalidOperationException? ex = Assert.Throws<System.InvalidOperationException>(
                () => MmdHumanoidProxyRigFactory.BuildAvatar(proxyResult));
            Assert.That(ex!.Message, Does.Contain("proxy rig root is null"));
        }

        [Test]
        public void BuildAvatarWithNullProxyRigThrowsArgumentNull()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => MmdHumanoidProxyRigFactory.BuildAvatar(null!));
        }

        [Test]
        public void MmdOriginToUnityPositionConvertsCorrectly()
        {
            // MMD->Unity: [-x, y, -z]
            Assert.That(MmdHumanoidProxyRigFactory.MmdOriginToUnityPosition(new[] { 1f, 2f, 3f }),
                Is.EqualTo(new Vector3(-1, 2, -3)).Within(1e-5f));
            Assert.That(MmdHumanoidProxyRigFactory.MmdOriginToUnityPosition(new[] { -5f, 10f, -2f }),
                Is.EqualTo(new Vector3(5, 10, 2)).Within(1e-5f));
            Assert.That(MmdHumanoidProxyRigFactory.MmdOriginToUnityPosition(new[] { 0f, 0f, 0f }),
                Is.EqualTo(Vector3.zero).Within(1e-5f));
        }

        [Test]
        public void MmdOriginToUnityPositionFallsBackToZeroForNullEmptyShort()
        {
            Assert.That(MmdHumanoidProxyRigFactory.MmdOriginToUnityPosition(null),
                Is.EqualTo(Vector3.zero).Within(1e-5f), "null origin should return zero");
            Assert.That(MmdHumanoidProxyRigFactory.MmdOriginToUnityPosition(new float[0]),
                Is.EqualTo(Vector3.zero).Within(1e-5f), "empty origin should return zero");
            Assert.That(MmdHumanoidProxyRigFactory.MmdOriginToUnityPosition(new[] { 1f, 2f }),
                Is.EqualTo(Vector3.zero).Within(1e-5f), "short origin should return zero");
        }

        [Test]
        public void ProxyRigUsesTryMapBoneNameForMapping()
        {
            // Arrange: verify that standard MMD names work through TryMapBoneName
            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("下半身", out HumanBodyBones hipsBone, out bool hipsRequired), Is.True);
            Assert.That(hipsBone, Is.EqualTo(HumanBodyBones.Hips));
            Assert.That(hipsRequired, Is.True);

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("上半身2", out HumanBodyBones chestBone, out _), Is.True);
            Assert.That(chestBone, Is.EqualTo(HumanBodyBones.Chest));

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左肩", out HumanBodyBones shoulderBone, out _), Is.True);
            Assert.That(shoulderBone, Is.EqualTo(HumanBodyBones.LeftShoulder));

            // Helper bones must not map
            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左足ＩＫ", out _, out _), Is.False);
            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右腕捩", out _, out _), Is.False);
        }

        [Test]
        public void ProxyRigWithNoBonesReturnsNullRoot()
        {
            // Arrange
            var model = new MmdModelDefinition();
            // No bones added.

            // Act
            MmdHumanoidProxyRigResult result = MmdHumanoidProxyRigFactory.CreateProxyRig(model);

            // Assert
            Assert.That(result.ProxyRoot, Is.Null, "root must be null when there are no bones");
            Assert.That(result.Readiness, Is.EqualTo(MmdHumanoidSetupAsset.NoBonesReadiness));
            Assert.That(result.BoneMap, Is.Empty);
            Assert.That(result.Matches, Is.Empty);
        }

        [Test]
        public void TryMapBoneNameMapsLeftFingerBones()
        {
            // Left thumb
            bool ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左親指１", out HumanBodyBones bone, out bool req);
            Assert.That(ok, Is.True, "左親指１ should map");
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftThumbProximal));
            Assert.That(req, Is.False, "finger bones are optional");

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左親指２", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftThumbIntermediate));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左親指先", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftThumbDistal));

            // Left index (人指 variant)
            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左人指１", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftIndexProximal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左人指２", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftIndexIntermediate));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左人指３", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftIndexDistal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左人指先", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftIndexDistal));

            // Left index (人差指 variant)
            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左人差指１", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftIndexProximal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左人差指３", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftIndexDistal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左人差指先", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftIndexDistal));

            // Left middle
            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左中指１", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftMiddleProximal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左中指２", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftMiddleIntermediate));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左中指３", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftMiddleDistal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左中指先", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftMiddleDistal));

            // Left ring
            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左薬指１", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftRingProximal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左薬指３", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftRingDistal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左薬指先", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftRingDistal));

            // Left little
            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左小指１", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftLittleProximal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左小指２", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftLittleIntermediate));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左小指先", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.LeftLittleDistal));
        }

        [Test]
        public void TryMapBoneNameMapsRightFingerBones()
        {
            // Right thumb
            bool ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右親指１", out HumanBodyBones bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightThumbProximal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右親指２", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightThumbIntermediate));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右親指先", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightThumbDistal));

            // Right index (人指 variant)
            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右人指１", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightIndexProximal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右人指先", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightIndexDistal));

            // Right index (人差指 variant)
            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右人差指１", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightIndexProximal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右人差指３", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightIndexDistal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右人差指先", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightIndexDistal));

            // Right middle
            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右中指１", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightMiddleProximal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右中指先", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightMiddleDistal));

            // Right ring
            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右薬指１", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightRingProximal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右薬指先", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightRingDistal));

            // Right little
            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右小指１", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightLittleProximal));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右小指２", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightLittleIntermediate));

            ok = MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右小指先", out bone, out _);
            Assert.That(ok, Is.True);
            Assert.That(bone, Is.EqualTo(HumanBodyBones.RightLittleDistal));
        }

        [Test]
        public void ProxyRigCreatesFingerTransformsForLeftHandWithHierarchy()
        {
            // Arrange: a model with left arm + hand + finger bones.
            var model = new MmdModelDefinition();
            // Indices 0-3: 左腕, 左ひじ, 左手首, 左人指１(parent=2=左手首)
            AddBoneWithOrigin(model, 0, "左腕", -1, new[] { 20f, 135f, 0f });
            AddBoneWithOrigin(model, 1, "左ひじ", 0, new[] { 40f, 135f, 0f });
            AddBoneWithOrigin(model, 2, "左手首", 1, new[] { 60f, 135f, 0f });
            // Finger bones parented under 左手首
            AddBoneWithOrigin(model, 3, "左親指１", 2, new[] { 68f, 130f, 5f });
            AddBoneWithOrigin(model, 4, "左親指２", 3, new[] { 72f, 128f, 10f });
            AddBoneWithOrigin(model, 5, "左親指先", 4, new[] { 76f, 126f, 14f });
            AddBoneWithOrigin(model, 6, "左人指１", 2, new[] { 64f, 130f, 0f });
            AddBoneWithOrigin(model, 7, "左人指２", 6, new[] { 66f, 125f, 0f });
            AddBoneWithOrigin(model, 8, "左人指先", 7, new[] { 68f, 120f, 0f });
            AddBoneWithOrigin(model, 9, "左中指１", 2, new[] { 64f, 134f, 0f });
            AddBoneWithOrigin(model, 10, "左中指２", 9, new[] { 66f, 139f, 0f });
            AddBoneWithOrigin(model, 11, "左中指先", 10, new[] { 68f, 144f, 0f });
            AddBoneWithOrigin(model, 12, "左薬指１", 2, new[] { 64f, 138f, 0f });
            AddBoneWithOrigin(model, 13, "左薬指２", 12, new[] { 66f, 143f, 0f });
            AddBoneWithOrigin(model, 14, "左薬指先", 13, new[] { 68f, 148f, 0f });
            AddBoneWithOrigin(model, 15, "左小指１", 2, new[] { 64f, 142f, 0f });
            AddBoneWithOrigin(model, 16, "左小指２", 15, new[] { 66f, 147f, 0f });
            AddBoneWithOrigin(model, 17, "左小指先", 16, new[] { 68f, 152f, 0f });

            // Act
            MmdHumanoidProxyRigResult result = MmdHumanoidProxyRigFactory.CreateProxyRig(model);

            // Assert: proxy rig created with correct transforms
            Assert.That(result.ProxyRoot, Is.Not.Null);

            // Verify all finger HumanBodyBones are present
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftThumbProximal), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftThumbIntermediate), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftThumbDistal), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftIndexProximal), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftIndexIntermediate), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftIndexDistal), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftMiddleProximal), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftMiddleIntermediate), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftMiddleDistal), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftRingProximal), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftRingIntermediate), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftRingDistal), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftLittleProximal), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftLittleIntermediate), Is.True);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.LeftLittleDistal), Is.True);

            // Verify hierarchy: fingers are children of hand (LeftHand)
            Transform hand = result.BoneMap[HumanBodyBones.LeftHand];
            Assert.That(result.BoneMap[HumanBodyBones.LeftThumbProximal].parent, Is.EqualTo(hand));
            Assert.That(result.BoneMap[HumanBodyBones.LeftThumbIntermediate].parent,
                Is.EqualTo(result.BoneMap[HumanBodyBones.LeftThumbProximal]));
            Assert.That(result.BoneMap[HumanBodyBones.LeftThumbDistal].parent,
                Is.EqualTo(result.BoneMap[HumanBodyBones.LeftThumbIntermediate]));
            Assert.That(result.BoneMap[HumanBodyBones.LeftIndexProximal].parent, Is.EqualTo(hand));
            Assert.That(result.BoneMap[HumanBodyBones.LeftIndexIntermediate].parent,
                Is.EqualTo(result.BoneMap[HumanBodyBones.LeftIndexProximal]));
            Assert.That(result.BoneMap[HumanBodyBones.LeftIndexDistal].parent,
                Is.EqualTo(result.BoneMap[HumanBodyBones.LeftIndexIntermediate]));
            Assert.That(result.BoneMap[HumanBodyBones.LeftMiddleProximal].parent, Is.EqualTo(hand));
            Assert.That(result.BoneMap[HumanBodyBones.LeftMiddleDistal].parent,
                Is.EqualTo(result.BoneMap[HumanBodyBones.LeftMiddleIntermediate]));
            Assert.That(result.BoneMap[HumanBodyBones.LeftLittleProximal].parent, Is.EqualTo(hand));
            Assert.That(result.BoneMap[HumanBodyBones.LeftRingDistal].parent,
                Is.EqualTo(result.BoneMap[HumanBodyBones.LeftRingIntermediate]));

            // Verify finger count in total mapped (16 required + 3 optional + 15 finger = 34)
            // This model has: 16 required bones + shoulders(2) + chest(1) = all present
            // But only if we include all required/optional/finger.
            // The model only has 左腕/左ひじ/左手首 + 15 finger bones = 18 total
            // Mapped: LeftUpperArm, LeftLowerArm, LeftHand + 15 finger = 18
            int mappedCount = result.BoneMap.Count;
            Assert.That(mappedCount, Is.EqualTo(18),
                "Should have 3 arm bones + 15 finger bones mapped");

            // Cleanup
            Object.DestroyImmediate(result.ProxyRoot);
        }

        [Test]
        public void FingerDuplicateCandidatesReportAmbiguousDiagnostics()
        {
            var model = new MmdModelDefinition();
            AddBone(model, 0, "左人指３", -1);
            AddBone(model, 1, "左人指先", 0);

            MmdHumanoidBoneMappingReport report = MmdHumanoidBoneMappingEvaluator.Evaluate(model);

            Assert.That(report.Readiness, Is.EqualTo(MmdHumanoidSetupAsset.AmbiguousReadiness));
            Assert.That(report.AmbiguousMappingCount, Is.EqualTo(1));
            Assert.That(string.Join("\n", report.Diagnostics), Does.Contain("ambiguous: LeftIndexDistal"));
        }

        [Test]
        public void ProxyRigWithMultipleDuplicateNamesTakesFirstMatch()
        {
            // Arrange: two bones with the same MMD name "下半身" -> only the first should map to Hips
            var model = new MmdModelDefinition();
            AddBone(model, 0, "下半身", -1);
            AddBone(model, 1, "下半身", -1); // duplicate
            AddBone(model, 2, "上半身", 0);
            AddBone(model, 3, "首", 2);
            AddBone(model, 4, "頭", 3);

            // Act
            MmdHumanoidProxyRigResult result = MmdHumanoidProxyRigFactory.CreateProxyRig(model);

            // Assert: only one Hips transform (the first duplicate is ignored)
            Assert.That(result.ProxyRoot, Is.Not.Null);
            Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.Hips), Is.True);
            // Only 4 unique HumanBodyBones should be mapped (下半身 -> Hips, 上半身 -> Spine, 首 -> Neck, 頭 -> Head)
            Assert.That(result.BoneMap.Count, Is.EqualTo(4));

            // Cleanup
            Object.DestroyImmediate(result.ProxyRoot);
        }

        [Test]
        public void CreateProxyRigFromPmxAssetWithoutImportedRootReturnsHierarchyNotReady()
        {
            // Arrange: a fresh asset without ImportedRoot must be validated before attempting
            // proxy rig generation.
            MmdPmxAsset asset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                // Act
                MmdHumanoidProxyRigResult result = MmdHumanoidProxyRigFactory.CreateProxyRig(asset);

                // Assert
                Assert.That(result.Readiness, Is.EqualTo(MmdHumanoidSetupAsset.HierarchyNotReadyReadiness));
                Assert.That(result.ProxyRoot, Is.Null);
                Assert.That(string.Join("\n", result.Diagnostics), Does.StartWith("hierarchy-not-ready"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CreateProxyRigFromBoneTransformsMapsRequiredBonesAndComposesWorldPositions()
        {
            // Arrange: a synthetic importer hierarchy with all 16 required bones.
            var root = new GameObject("ImportedHierarchyRoot");
            root.transform.position = new Vector3(2f, 4f, 6f);
            root.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            try
            {
                Transform hips = AddBoneToHierarchy(root, "下半身", null, new Vector3(2f, 4f, 6f));
                Transform spine = AddBoneToHierarchy(root, "上半身", hips, new Vector3(2f, 14f, 6f));
                Transform neck = AddBoneToHierarchy(root, "首", spine, new Vector3(2f, 24f, 6f));
                Transform head = AddBoneToHierarchy(root, "頭", neck, new Vector3(2f, 34f, 6f));
                Transform leftUpperLeg = AddBoneToHierarchy(root, "左足", hips, new Vector3(6f, 4f, 6f));
                Transform leftLowerLeg = AddBoneToHierarchy(root, "左ひざ", leftUpperLeg, new Vector3(10f, 4f, 6f));
                Transform leftFoot = AddBoneToHierarchy(root, "左足首", leftLowerLeg, new Vector3(14f, 4f, 6f));
                Transform rightUpperLeg = AddBoneToHierarchy(root, "右足", hips, new Vector3(-2f, 4f, 6f));
                Transform rightLowerLeg = AddBoneToHierarchy(root, "右ひざ", rightUpperLeg, new Vector3(-6f, 4f, 6f));
                Transform rightFoot = AddBoneToHierarchy(root, "右足首", rightLowerLeg, new Vector3(-10f, 4f, 6f));
                Transform leftUpperArm = AddBoneToHierarchy(root, "左腕", spine, new Vector3(2f, 24f, 8f));
                Transform leftLowerArm = AddBoneToHierarchy(root, "左ひじ", leftUpperArm, new Vector3(2f, 24f, 12f));
                Transform leftHand = AddBoneToHierarchy(root, "左手首", leftLowerArm, new Vector3(2f, 24f, 16f));
                Transform rightUpperArm = AddBoneToHierarchy(root, "右腕", spine, new Vector3(2f, 24f, 4f));
                Transform rightLowerArm = AddBoneToHierarchy(root, "右ひじ", rightUpperArm, new Vector3(2f, 24f, 0f));
                Transform rightHand = AddBoneToHierarchy(root, "右手首", rightLowerArm, new Vector3(2f, 24f, -4f));

                var bones = new[]
                {
                    hips, spine, neck, head,
                    leftUpperLeg, leftLowerLeg, leftFoot,
                    rightUpperLeg, rightLowerLeg, rightFoot,
                    leftUpperArm, leftLowerArm, leftHand,
                    rightUpperArm, rightLowerArm, rightHand,
                };

                // Act
                MmdHumanoidProxyRigResult result =
                    MmdHumanoidProxyRigFactory.CreateProxyRigFromBoneTransforms(bones, root.transform, "ImportedProxyRig");

                // Assert
                Assert.That(result.Readiness, Is.EqualTo(MmdHumanoidSetupAsset.ReadyReadiness));
                Assert.That(result.ProxyRoot, Is.Not.Null);
                Assert.That(result.BoneMap.Count, Is.EqualTo(16));
                Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.Hips), Is.True);
                Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.Head), Is.True);
                Assert.That(result.BoneMap.ContainsKey(HumanBodyBones.RightHand), Is.True);
                Assert.That(Vector3.Distance(result.BoneMap[HumanBodyBones.Hips].position, hips.position), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(result.BoneMap[HumanBodyBones.Spine].position, spine.position), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(result.BoneMap[HumanBodyBones.RightHand].position, rightHand.position), Is.LessThan(0.0001f));
                Assert.That(string.Join(";", result.Diagnostics), Does.Contain("proxy-rig: input=ImportedHierarchy"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CreateProxyRigFromPmxAssetUsesImportedHierarchyAndMatchesModelBasedResultForFixture()
        {
            // Arrange: fixture-based PMX asset from package fixtures.
            const string pmxFixturePath = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube.pmx";
            MmdPmxAsset? asset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(pmxFixturePath);
            Assert.That(asset, Is.Not.Null);

            MmdModelDefinition model = asset!.LoadModel();

            // Act
            MmdHumanoidProxyRigResult fromAsset = MmdHumanoidProxyRigFactory.CreateProxyRig(asset);
            MmdHumanoidProxyRigResult fromModel = MmdHumanoidProxyRigFactory.CreateProxyRig(model);

            // Assert: readiness and mapping matches should be equivalent for imported hierarchy input.
            Assert.That(fromAsset.Readiness, Is.EqualTo(fromModel.Readiness));
            Assert.That(fromAsset.Matches.Count, Is.EqualTo(fromModel.Matches.Count),
                "imported hierarchy and model-based overloads should produce equivalent first-match results");
            Assert.That(string.Join(",", CanonicalizeMatches(fromAsset.Matches)),
                Is.EqualTo(string.Join(",", CanonicalizeMatches(fromModel.Matches))),
                "proxy rig matches should be equivalent (empty vs empty is acceptable)");
        }

        private static MmdModelDefinition CreateHumanoidMappingModel(params string[] boneNames)
        {
            var model = new MmdModelDefinition();
            for (int i = 0; i < boneNames.Length; i++)
            {
                model.bones.Add(new MmdBoneDefinition
                {
                    index = i,
                    name = boneNames[i],
                    parentIndex = i - 1,
                });
            }

            return model;
        }

        private static Transform AddBoneToHierarchy(
            GameObject root,
            string name,
            Transform? parent,
            Vector3 worldPosition)
        {
            var bone = new GameObject(name);
            Transform boneTransform = bone.transform;
            boneTransform.SetParent(parent != null ? parent : root.transform, worldPositionStays: false);
            boneTransform.position = worldPosition;
            return boneTransform;
        }

        private static IEnumerable<string> CanonicalizeMatches(IReadOnlyList<MmdHumanoidBoneMappingMatch> matches)
        {
            return matches
                .OrderBy(match => match.MmdBoneIndex)
                .Select(match =>
                    match.HumanBone + ":" + match.MmdBoneIndex + ":" + match.MmdBoneName);
        }

        private static void AddBone(MmdModelDefinition model, int index, string name, int parentIndex)
        {
            model.bones.Add(new MmdBoneDefinition
            {
                index = index,
                name = name,
                parentIndex = parentIndex,
            });
        }

        private static void AddBoneWithOrigin(
            MmdModelDefinition model, int index, string name, int parentIndex, float[] origin)
        {
            model.bones.Add(new MmdBoneDefinition
            {
                index = index,
                name = name,
                parentIndex = parentIndex,
                origin = origin,
            });
        }

        /// <summary>
        /// Create a model with all 16 required bones + some optional bones,
        /// each with a plausible synthetic origin for Avatar building.
        /// Origins are in MMD space and increase along Y for the spine/head chain.
        /// </summary>
        private static MmdModelDefinition CreateHumanoidMappingModelWithOrigins()
        {
            var model = new MmdModelDefinition();

            // Required bones with plausible origins (MMD space).
            // 下半身 (Hips) at origin.
            AddBoneWithOrigin(model, 0, "下半身", -1, new[] { 0f, 90f, 0f });
            // 上半身 (Spine) above hips.
            AddBoneWithOrigin(model, 1, "上半身", 0, new[] { 0f, 115f, 0f });
            // 首 (Neck) above spine.
            AddBoneWithOrigin(model, 2, "首", 1, new[] { 0f, 150f, 0f });
            // 頭 (Head) above neck.
            AddBoneWithOrigin(model, 3, "頭", 2, new[] { 0f, 165f, 0f });
            // 左足 (LeftUpperLeg) at hips level, left side.
            AddBoneWithOrigin(model, 4, "左足", 0, new[] { 8f, 85f, 0f });
            // 左ひざ (LeftLowerLeg) below left leg.
            AddBoneWithOrigin(model, 5, "左ひざ", 4, new[] { 8f, 45f, 0f });
            // 左足首 (LeftFoot) below knee.
            AddBoneWithOrigin(model, 6, "左足首", 5, new[] { 8f, 5f, 3f });
            // 右足 (RightUpperLeg) at hips level, right side.
            AddBoneWithOrigin(model, 7, "右足", 0, new[] { -8f, 85f, 0f });
            // 右ひざ (RightLowerLeg) below right leg.
            AddBoneWithOrigin(model, 8, "右ひざ", 7, new[] { -8f, 45f, 0f });
            // 右足首 (RightFoot) below knee.
            AddBoneWithOrigin(model, 9, "右足首", 8, new[] { -8f, 5f, 3f });
            // 左腕 (LeftUpperArm) at spine level, left side.
            AddBoneWithOrigin(model, 10, "左腕", 1, new[] { 25f, 135f, 0f });
            // 左ひじ (LeftLowerArm) below left arm.
            AddBoneWithOrigin(model, 11, "左ひじ", 10, new[] { 50f, 135f, 0f });
            // 左手首 (LeftHand) below elbow.
            AddBoneWithOrigin(model, 12, "左手首", 11, new[] { 70f, 135f, 0f });
            // 右腕 (RightUpperArm) at spine level, right side.
            AddBoneWithOrigin(model, 13, "右腕", 1, new[] { -25f, 135f, 0f });
            // 右ひじ (RightLowerArm) below right arm.
            AddBoneWithOrigin(model, 14, "右ひじ", 13, new[] { -50f, 135f, 0f });
            // 右手首 (RightHand) below elbow.
            AddBoneWithOrigin(model, 15, "右手首", 14, new[] { -70f, 135f, 0f });
            // Optional: 上半身2 (Chest) above spine.
            AddBoneWithOrigin(model, 16, "上半身2", 1, new[] { 0f, 130f, 0f });
            // Optional: 左肩 (LeftShoulder) at spine level.
            AddBoneWithOrigin(model, 17, "左肩", 1, new[] { 15f, 135f, 0f });
            // Optional: 右肩 (RightShoulder) at spine level.
            AddBoneWithOrigin(model, 18, "右肩", 1, new[] { -15f, 135f, 0f });

            return model;
        }
    }
}
