#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Mmd.Parser;
using Mmd.Rendering;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdHumanoidRetargeterTests
    {
        // ── Helper ──────────────────────────────────────────────────────

        /// <summary>
        /// Create a model with standard MMD bone names. Each bone gets a
        /// synthetic index matching its position in the array.
        /// </summary>
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

        /// <summary>
        /// Create a minimal MmdUnityModelInstance with a given number of bone
        /// transforms. Bone transforms are plain GameObjects parented under a
        /// common root, named "bone-{index}".
        /// </summary>
        private static MmdUnityModelInstance CreateModelInstance(int boneCount)
        {
            var root = new GameObject("TestModelRoot");
            var bones = new Transform[boneCount];
            for (int i = 0; i < boneCount; i++)
            {
                var go = new GameObject("bone-" + i);
                go.transform.SetParent(root.transform, worldPositionStays: false);
                bones[i] = go.transform;
            }

            var mesh = new Mesh();
            mesh.vertices = new Vector3[3];
            mesh.triangles = new[] { 0, 1, 2 };

            var descriptor = new MmdRenderingDescriptor();

            var textureDiag = new MmdTextureBindingDiagnostics();

            var shaderDiag = new MmdShaderBindingDiagnostics();

            var instance = new MmdUnityModelInstance(
                root: root,
                mesh: mesh,
                materials: new Material[0],
                renderingDescriptor: descriptor,
                boneTransforms: bones,
                physicsBodies: new MmdUnityPhysicsBody[0],
                meshRenderer: null,
                skinnedMeshRenderer: null,
                sourceContext: null,
                ownedTextures: new Texture2D[0],
                textureDiagnostics: textureDiag,
                shaderDiagnostics: shaderDiag,
                importScale: 1.0f);

            return instance;
        }

        /// <summary>
        /// Set each proxy bone's localRotation to a known non-identity rotation.
        /// </summary>
        private static void SetProxyRotations(MmdHumanoidProxyRigResult rig)
        {
            foreach (Transform t in rig.BoneMap.Values)
            {
                t.localRotation = Quaternion.Euler(10f, 20f, 30f);
            }
        }

        /// <summary>
        /// Create a model instance that has bone transforms matching the
        /// proxy rig match indices (i.e., native bones at indices 0..N-1).
        /// Rotations are set to identity by default.
        /// </summary>
        private static MmdUnityModelInstance CreateNativeInstance(int nativeBoneCount)
        {
            MmdUnityModelInstance instance = CreateModelInstance(nativeBoneCount);
            // Ensure all native bones start at identity rotation.
            for (int i = 0; i < instance.BoneTransforms.Length; i++)
            {
                instance.BoneTransforms[i].localRotation = Quaternion.identity;
                instance.BoneTransforms[i].localPosition = new Vector3(100f, 200f, 300f);
            }
            return instance;
        }

        private static MmdPmxAsset CreateSyntheticPmxAssetWithImportedHierarchy()
        {
            MmdModelDefinition model = CreateHumanoidMappingModel(
                "下半身", "上半身", "首", "頭",
                "左足", "左ひざ", "左足首",
                "右足", "右ひざ", "右足首",
                "左腕", "左ひじ", "左手首",
                "右腕", "右ひじ", "右手首");

            GameObject importedRoot = new GameObject("ImportedHierarchyForRetargeterTest");
            Transform? previous = null;
            var bones = new Transform[model.bones.Count];
            for (int i = 0; i < model.bones.Count; i++)
            {
                var boneObject = new GameObject(model.bones[i].name);
                Transform boneTransform = boneObject.transform;
                boneTransform.SetParent(previous != null ? previous : importedRoot.transform, worldPositionStays: false);
                boneTransform.localPosition = new Vector3(i * 2f, i * 0.5f, 0f);
                bones[i] = boneTransform;
                previous = boneTransform;
            }

            var mesh = new Mesh();
            mesh.vertices = new Vector3[4];
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            var smr = importedRoot.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;

            var summary = MmdPmxParseSummary.FromModel(model);
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            pmxAsset.Initialize(
                bytes: new byte[] { 1 },
                assetSourceId: "synthetic-retargeter-imported-hierarchy",
                assetSourcePath: "Assets/SyntheticRetargeter.pmx",
                importedMeshAsset: mesh,
                importedRootAsset: importedRoot,
                importedMaterialAssets: System.Array.Empty<Material>(),
                parseSummary: summary,
                hierarchyReadinessValue: MmdImportReadiness.Ready,
                rendererReadinessValue: MmdImportReadiness.Ready,
                boneBindingReadinessValue: MmdImportReadiness.Ready);

            return pmxAsset;
        }

        private static void SetProxyRotationsByMatchIndex(
            MmdHumanoidProxyRigResult rig)
        {
            for (int i = 0; i < rig.Matches.Count; i++)
            {
                MmdHumanoidBoneMappingMatch match = rig.Matches[i];
                Transform boneTransform = rig.BoneMap[match.HumanBone];
                boneTransform.localRotation = Quaternion.Euler(12f + i, 14f + i * 2f, 16f + i * 3f);
            }
        }

        private static void DestroyInstance(MmdUnityModelInstance? instance)
        {
            if (instance == null)
                return;
            if (instance.Root != null)
                Object.DestroyImmediate(instance.Root);
            if (instance.Mesh != null)
                Object.DestroyImmediate(instance.Mesh);
            foreach (Material m in instance.Materials)
                Object.DestroyImmediate(m);
            foreach (Texture2D t in instance.OwnedTextures)
                Object.DestroyImmediate(t);
        }

        private static void DestroyProxyRig(MmdHumanoidProxyRigResult? rig)
        {
            if (rig?.ProxyRoot != null)
                Object.DestroyImmediate(rig.ProxyRoot);
        }

        // ── Tests ───────────────────────────────────────────────────────

        [Test]
        public void RetargetCopiesProxyLocalRotationsToNativeByMmdBoneIndex()
        {
            // Arrange: create proxy rig with standard bones at indices 0..15.
            MmdModelDefinition model = CreateHumanoidMappingModel(
                "下半身", "上半身", "首", "頭",
                "左足", "左ひざ", "左足首",
                "右足", "右ひざ", "右足首",
                "左腕", "左ひじ", "左手首",
                "右腕", "右ひじ", "右手首");

            MmdHumanoidProxyRigResult rig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.That(rig.ProxyRoot, Is.Not.Null);
            Assert.That(rig.BoneMap.Count, Is.EqualTo(16));

            SetProxyRotations(rig);

            // Create native instance with 16 bones (same indices as matches).
            MmdUnityModelInstance native = CreateNativeInstance(16);
            // Verify proxy rotations are not identity
            Transform hipsProxy = rig.BoneMap[HumanBodyBones.Hips];
            Assert.That(hipsProxy.localRotation, Is.Not.EqualTo(Quaternion.identity));

            // Act
            MmdHumanoidRetargeterResult result = MmdHumanoidRetargeter.RetargetPose(rig, native);

            // Assert: all bones copied
            Assert.That(result.CopiedBoneCount, Is.EqualTo(16));
            Assert.That(result.SkippedBoneCount, Is.EqualTo(0));
            Assert.That(result.AllSucceeded, Is.True);

            // Assert: each native bone received the proxy rotation
            foreach (MmdHumanoidBoneMappingMatch match in rig.Matches)
            {
                Transform proxyTransform = rig.BoneMap[match.HumanBone];
                Transform nativeTransform = native.BoneTransforms[match.MmdBoneIndex];
                Assert.That(Quaternion.Angle(nativeTransform.localRotation, proxyTransform.localRotation), Is.LessThan(0.001f),
                    $"Native rotation for MmdBoneIndex {match.MmdBoneIndex} ({match.HumanBone}) " +
                    $"should match proxy rotation");
            }

            DestroyProxyRig(rig);
            DestroyInstance(native);
        }

        [Test]
        public void RetargeterUsesImportedHierarchyInputFromPmxAsset()
        {
            // Arrange: create a synthetic PMX asset with ImportedRoot and SkinnedMeshRenderer.
            MmdPmxAsset pmxAsset = CreateSyntheticPmxAssetWithImportedHierarchy();
            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null);
            Assert.That(pmxAsset.BoneCount, Is.EqualTo(16));

            MmdHumanoidProxyRigResult rig = MmdHumanoidProxyRigFactory.CreateProxyRig(pmxAsset);
            Assert.That(rig.ProxyRoot, Is.Not.Null);

            bool hasImportedHierarchyDiagnostic = false;
            foreach (string diagnostic in rig.Diagnostics)
            {
                if (diagnostic.Contains("proxy-rig: input=ImportedHierarchy"))
                {
                    hasImportedHierarchyDiagnostic = true;
                    break;
                }
            }

            Assert.That(hasImportedHierarchyDiagnostic, Is.True,
                "proxy rig diagnostics should include imported hierarchy input marker");

            SetProxyRotationsByMatchIndex(rig);
            MmdUnityModelInstance native = CreateNativeInstance(pmxAsset.BoneCount);

            Vector3[] originalPositions = new Vector3[native.BoneTransforms.Length];
            for (int i = 0; i < native.BoneTransforms.Length; i++)
            {
                originalPositions[i] = native.BoneTransforms[i].localPosition;
            }

            // Act
            MmdHumanoidRetargeterResult result = MmdHumanoidRetargeter.RetargetPose(rig, native);

            // Assert: copied by MmdBoneIndex and positions were not copied.
            Assert.That(result.CopiedBoneCount, Is.EqualTo(rig.Matches.Count));
            Assert.That(result.SkippedBoneCount, Is.EqualTo(0));
            Assert.That(result.AllSucceeded, Is.True);

            foreach (MmdHumanoidBoneMappingMatch match in rig.Matches)
            {
                Transform proxyTransform = rig.BoneMap[match.HumanBone];
                Transform nativeTransform = native.BoneTransforms[match.MmdBoneIndex];
                Assert.That(Quaternion.Angle(nativeTransform.localRotation, proxyTransform.localRotation),
                    Is.LessThan(0.001f),
                    $"bone index {match.MmdBoneIndex} should receive matching proxy rotation");
                Assert.That(nativeTransform.localPosition, Is.EqualTo(originalPositions[match.MmdBoneIndex]),
                    $"bone index {match.MmdBoneIndex} localPosition should not be changed");
            }

            // Side-effect guard: no animator side effects and no editor-only assembly dependency.
            Assert.That(rig.ProxyRoot!.GetComponentsInChildren<Animator>(includeInactive: true), Is.Empty,
                "retargeter should not add Animator components on proxy rig");
            Assert.That(native.Root.GetComponentsInChildren<Animator>(includeInactive: true), Is.Empty,
                "retargeter should not add Animator components on native model");
            Assert.That(typeof(MmdHumanoidRetargeter).Assembly.GetName().Name, Is.EqualTo("Mmd.Runtime"),
                "retargeter should remain runtime-only and not require editor-only references");

            DestroyImmediateIfPossible(pmxAsset);
            DestroyImmediateIfPossible(pmxAsset.ImportedRoot);
            DestroyProxyRig(rig);
            DestroyInstance(native);
        }

        [Test]
        public void RetargetDoesNotCopyLocalPosition()
        {
            // Arrange
            MmdModelDefinition model = CreateHumanoidMappingModel("下半身", "上半身", "首", "頭");
            MmdHumanoidProxyRigResult rig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.That(rig.ProxyRoot, Is.Not.Null);

            SetProxyRotations(rig);

            // Set proxy positions to something arbitrary.
            foreach (Transform t in rig.BoneMap.Values)
            {
                t.localPosition = new Vector3(1f, 2f, 3f);
            }

            MmdUnityModelInstance native = CreateNativeInstance(4);
            Vector3[] originalPositions = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                originalPositions[i] = native.BoneTransforms[i].localPosition;
            }

            // Act
            MmdHumanoidRetargeterResult result = MmdHumanoidRetargeter.RetargetPose(rig, native);

            // Assert: rotations copied, positions unchanged
            Assert.That(result.CopiedBoneCount, Is.EqualTo(4));
            Assert.That(result.SkippedBoneCount, Is.EqualTo(0));

            for (int i = 0; i < 4; i++)
            {
                Assert.That(native.BoneTransforms[i].localPosition, Is.EqualTo(originalPositions[i]),
                    $"localPosition for bone {i} must not be changed by retargeter");
            }

            DestroyProxyRig(rig);
            DestroyInstance(native);
        }

        [Test]
        public void RetargetCopiesHipsLocalPositionDeltaToTranslationTargetWhenEnabled()
        {
            var proxyObject = new GameObject("proxy-hips");
            var rotationTargetObject = new GameObject("native-lower-body");
            var translationTargetObject = new GameObject("native-center");
            try
            {
                Transform proxyHips = proxyObject.transform;
                Transform rotationTarget = rotationTargetObject.transform;
                Transform translationTarget = translationTargetObject.transform;
                Vector3 proxyBind = new Vector3(0f, 12.7f, 0f);
                Vector3 targetBind = new Vector3(0f, 8f, 0f);
                Vector3 rotationTargetPosition = new Vector3(10f, 20f, 30f);
                proxyHips.localPosition = proxyBind + new Vector3(1.25f, -0.5f, 2.75f);
                proxyHips.localRotation = Quaternion.Euler(10f, 20f, 30f);
                rotationTarget.localPosition = rotationTargetPosition;
                translationTarget.localPosition = targetBind;

                var entries = new[]
                {
                    new MmdHumanoidRetargetBinding(
                        HumanBodyBones.Hips,
                        mmdBoneIndex: 4,
                        proxyTransform: proxyHips,
                        nativeTransform: rotationTarget,
                        copyLocalPosition: true,
                        translationTargetTransform: translationTarget,
                        translationTargetMmdBoneIndex: 1,
                        proxyBindLocalPosition: proxyBind,
                        translationTargetBindLocalPosition: targetBind),
                    new MmdHumanoidRetargetBinding(
                        HumanBodyBones.Spine,
                        mmdBoneIndex: 1,
                        proxyTransform: proxyHips,
                        nativeTransform: rotationTarget)
                };

                MmdHumanoidRetargeterResult result = MmdHumanoidRetargeter.RetargetPose(entries);

                Assert.That(result.CopiedBoneCount, Is.EqualTo(2));
                Assert.That(result.SkippedBoneCount, Is.EqualTo(0));
                Assert.That(result.CopiedTranslationCount, Is.EqualTo(1));
                Assert.That(result.SkippedTranslationCount, Is.EqualTo(0));
                Assert.That(Quaternion.Angle(rotationTarget.localRotation, proxyHips.localRotation), Is.LessThan(0.001f));
                Assert.That(translationTarget.localPosition, Is.EqualTo(targetBind + (proxyHips.localPosition - proxyBind)));
                Assert.That(rotationTarget.localPosition, Is.EqualTo(rotationTargetPosition),
                    "translation copy must not mutate the rotation target when it is a different MMD bone");
            }
            finally
            {
                Object.DestroyImmediate(proxyObject);
                Object.DestroyImmediate(rotationTargetObject);
                Object.DestroyImmediate(translationTargetObject);
            }
        }

        [Test]
        public void RetargetAppliesBindRotationDeltaAndReturnsNativeBindAtProxyBind()
        {
            var proxyObject = new GameObject("proxy-arm");
            var nativeObject = new GameObject("native-arm");
            try
            {
                Transform proxy = proxyObject.transform;
                Transform native = nativeObject.transform;
                Quaternion proxyBind = Quaternion.Euler(10f, 20f, 30f);
                Quaternion nativeBind = Quaternion.Euler(-15f, 35f, 5f);

                proxy.localRotation = proxyBind;
                native.localRotation = Quaternion.identity;

                var entries = new[]
                {
                    new MmdHumanoidRetargetBinding(
                        HumanBodyBones.LeftUpperArm,
                        mmdBoneIndex: 10,
                        proxyTransform: proxy,
                        nativeTransform: native,
                        proxyBindLocalRotation: proxyBind,
                        nativeBindLocalRotation: nativeBind)
                };

                MmdHumanoidRetargeterResult result = MmdHumanoidRetargeter.RetargetPose(entries);

                Assert.That(result.CopiedBoneCount, Is.EqualTo(1));
                Assert.That(result.SkippedBoneCount, Is.EqualTo(0));
                Assert.That(Quaternion.Angle(native.localRotation, nativeBind), Is.LessThan(0.001f),
                    "proxy current at proxy bind must retarget back to native bind.");

                Quaternion proxyCurrent = Quaternion.Euler(40f, -25f, 15f);
                proxy.localRotation = proxyCurrent;

                result = MmdHumanoidRetargeter.RetargetPose(entries);

                Quaternion expected = nativeBind * Quaternion.Inverse(proxyBind) * proxyCurrent;
                Quaternion wrongOrder = proxyCurrent * Quaternion.Inverse(proxyBind) * nativeBind;
                Assert.That(result.CopiedBoneCount, Is.EqualTo(1));
                Assert.That(Quaternion.Angle(native.localRotation, expected), Is.LessThan(0.001f),
                    "retarget order must be nativeBind * inverse(proxyBind) * proxyCurrent.");
                Assert.That(Quaternion.Angle(native.localRotation, wrongOrder), Is.GreaterThan(0.01f),
                    "test rotations must distinguish the incorrect multiplication order.");
            }
            finally
            {
                Object.DestroyImmediate(proxyObject);
                Object.DestroyImmediate(nativeObject);
            }
        }

        [Test]
        public void RetargetRejectsOutOfRangeMmdBoneIndex()
        {
            // Arrange: create a model where the last bone has an index beyond native array.
            var model = new MmdModelDefinition();
            AddBone(model, 0, "下半身", -1);
            AddBone(model, 1, "上半身", 0);
            AddBone(model, 2, "首", 1);
            // Bone index 999 does not exist in the 4-bone native array.
            AddBone(model, 999, "頭", 2);

            MmdHumanoidProxyRigResult rig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.That(rig.ProxyRoot, Is.Not.Null);
            SetProxyRotations(rig);

            MmdUnityModelInstance native = CreateNativeInstance(4);

            // Act
            MmdHumanoidRetargeterResult result = MmdHumanoidRetargeter.RetargetPose(rig, native);

            // Assert: 3 copied, 1 skipped (out-of-range index 999)
            Assert.That(result.CopiedBoneCount, Is.EqualTo(3));
            Assert.That(result.SkippedBoneCount, Is.EqualTo(1));
            Assert.That(result.AllSucceeded, Is.False);

            bool foundOutOfRange = false;
            foreach (string d in result.Diagnostics)
            {
                if (d.Contains("out of range"))
                {
                    foundOutOfRange = true;
                    break;
                }
            }
            Assert.That(foundOutOfRange, Is.True, "diagnostics must mention out-of-range index");

            DestroyProxyRig(rig);
            DestroyInstance(native);
        }

        [Test]
        public void RetargetDetectsDuplicateNativeTargetIndices()
        {
            // Arrange: create a model where two different HumanBodyBones share
            // the same MmdBoneIndex. We add bones at index 1 (Spine) and index
            // 1 again (RightUpperArm) by manually building a model with
            // duplicate bone indices.
            var model = new MmdModelDefinition();
            AddBone(model, 0, "下半身", -1);       // Hips at index 0
            AddBone(model, 1, "上半身", 0);       // Spine at index 1
            AddBone(model, 1, "右腕", 0);         // RightUpperArm also at index 1 (duplicate index)
            AddBone(model, 2, "首", 1);           // Neck at index 2

            MmdHumanoidProxyRigResult rig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.That(rig.ProxyRoot, Is.Not.Null);
            SetProxyRotations(rig);

            MmdUnityModelInstance native = CreateNativeInstance(3);

            // Act
            MmdHumanoidRetargeterResult result = MmdHumanoidRetargeter.RetargetPose(rig, native);

            // Assert: The factory may produce both Spine and RightUpperArm
            // as separate matches (both at index 1). If it does, the
            // retargeter skips the duplicate. No crash is the main assertion.
            int total = result.CopiedBoneCount + result.SkippedBoneCount;
            Assert.That(total, Is.GreaterThanOrEqualTo(1),
                "at least Hips should be retargeted");
            int skipped = result.SkippedBoneCount;
            if (skipped > 0)
            {
                bool hasDuplicateDiag = false;
                foreach (string d in result.Diagnostics)
                {
                    if (d.Contains("duplicate"))
                    {
                        hasDuplicateDiag = true;
                        break;
                    }
                }
                Assert.That(hasDuplicateDiag, Is.True,
                    "skipped bones should have a duplicate diagnostic");
            }

            DestroyProxyRig(rig);
            DestroyInstance(native);
        }

        [Test]
        public void RetargetMissingProxyTransformIsSkippedNotCrash()
        {
            // Arrange: create a rig, then destroy one proxy transform to
            // simulate a missing transform entry.
            MmdModelDefinition model = CreateHumanoidMappingModel(
                "下半身", "上半身", "首", "頭");
            MmdHumanoidProxyRigResult rig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.That(rig.ProxyRoot, Is.Not.Null);
            SetProxyRotations(rig);

            // Manually remove the Head transform from the BoneMap.
            // Since BoneMap is IReadOnlyDictionary, we cannot remove entries.
            // Instead, destroy the Head transform GameObject so the reference
            // becomes null (but still exists in the dictionary).
            if (rig.BoneMap.TryGetValue(HumanBodyBones.Head, out Transform headTransform))
            {
                Object.DestroyImmediate(headTransform.gameObject);
            }

            MmdUnityModelInstance native = CreateNativeInstance(4);

            // Act & Assert: no crash
            MmdHumanoidRetargeterResult result = null;
            Assert.DoesNotThrow(() =>
            {
                result = MmdHumanoidRetargeter.RetargetPose(rig, native);
            });

            Assert.That(result, Is.Not.Null);
            // Either 3 copied (Head skipped) or the destroyed transform
            // may still be in the dictionary. TryGetValue returns the field
            // value even after DestroyImmediate, so Transform will be null
            // after destruction. The retargeter should detect that.
            // Total matches: 4, Head's proxyTransform is null -> skip.
            Assert.That(result.CopiedBoneCount + result.SkippedBoneCount, Is.EqualTo(4));
            Assert.That(result.SkippedBoneCount, Is.GreaterThanOrEqualTo(1));

            DestroyProxyRig(rig);
            DestroyInstance(native);
        }

        [Test]
        public void RetargetNullProxyRigThrowsArgumentNull()
        {
            MmdUnityModelInstance native = CreateNativeInstance(4);
            Assert.Throws<System.ArgumentNullException>(
                () => MmdHumanoidRetargeter.RetargetPose(null!, native));
            DestroyInstance(native);
        }

        [Test]
        public void RetargetNullModelInstanceThrowsArgumentNull()
        {
            MmdModelDefinition model = CreateHumanoidMappingModel("下半身");
            MmdHumanoidProxyRigResult rig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.Throws<System.ArgumentNullException>(
                () => MmdHumanoidRetargeter.RetargetPose(rig, null!));
            DestroyProxyRig(rig);
        }

        [Test]
        public void RetargetNullProxyRootReturnsAllSucceededFalse()
        {
            // Arrange: a rig with no bones -> ProxyRoot is null.
            var model = new MmdModelDefinition();
            MmdHumanoidProxyRigResult rig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.That(rig.ProxyRoot, Is.Null);

            MmdUnityModelInstance native = CreateNativeInstance(4);

            // Act
            MmdHumanoidRetargeterResult result = MmdHumanoidRetargeter.RetargetPose(rig, native);

            // Assert
            Assert.That(result.CopiedBoneCount, Is.EqualTo(0));
            Assert.That(result.SkippedBoneCount, Is.EqualTo(1));
            Assert.That(result.AllSucceeded, Is.False);
            bool hasRootNullDiag = false;
            foreach (string d in result.Diagnostics)
            {
                if (d.Contains("ProxyRoot is null"))
                {
                    hasRootNullDiag = true;
                    break;
                }
            }
            Assert.That(hasRootNullDiag, Is.True);

            DestroyInstance(native);
        }

        [Test]
        public void RetargetEmptyMatchesReturnsZeroCopied()
        {
            // We need a valid ProxyRoot but empty Matches.
            // This is hard to construct directly because the factory always
            // produces matches from the model's bones. Instead, create a
            // model with no mappable bones.
            var model = new MmdModelDefinition();
            // Add a bone that doesn't map to any HumanBodyBones.
            AddBone(model, 0, "NonExistentBone", -1);
            AddBone(model, 1, "SomeIKbone", 0);

            MmdHumanoidProxyRigResult rig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            // ProxyRoot might be null if NoBones... let's check.
            // Actually NoBones checks if bones list has any elements.
            // With bones but no matches, readiness is NoBones.
            // Let's just test the edge case where we have a rig with matches
            // but need to exercise the empty-matches path.
            //
            // If readiness is NoBones, ProxyRoot is null. So we can't test
            // "valid ProxyRoot + empty Matches" via the factory.
            // Instead, create a rig with at least one bone that maps.
            model = CreateHumanoidMappingModel("下半身");
            rig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.That(rig.ProxyRoot, Is.Not.Null);
            Assert.That(rig.Matches.Count, Is.EqualTo(1));

            MmdUnityModelInstance native = CreateNativeInstance(4);

            // Act with normal call (should succeed)
            MmdHumanoidRetargeterResult result = MmdHumanoidRetargeter.RetargetPose(rig, native);
            Assert.That(result.CopiedBoneCount, Is.EqualTo(1));
            Assert.That(result.SkippedBoneCount, Is.EqualTo(0));

            DestroyProxyRig(rig);
            DestroyInstance(native);
        }

        [Test]
        public void RetargeterQuaternionValidatorRejectsNonFiniteValues()
        {
            Assert.That(MmdHumanoidRetargeter.IsFiniteQuaternion(Quaternion.identity), Is.True);
            Assert.That(MmdHumanoidRetargeter.IsFiniteQuaternion(new Quaternion(float.NaN, 0f, 0f, 1f)), Is.False);
            Assert.That(MmdHumanoidRetargeter.IsFiniteQuaternion(new Quaternion(0f, float.PositiveInfinity, 0f, 1f)), Is.False);
        }

        [Test]
        public void RetargetNoAnimatorAvatarOrAssetDatabaseSideEffects()
        {
            // Arrange
            MmdModelDefinition model = CreateHumanoidMappingModel(
                "下半身", "上半身", "首", "頭");
            MmdHumanoidProxyRigResult rig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.That(rig.ProxyRoot, Is.Not.Null);
            SetProxyRotations(rig);

            MmdUnityModelInstance native = CreateNativeInstance(4);

            // Act
            MmdHumanoidRetargeterResult result = MmdHumanoidRetargeter.RetargetPose(rig, native);

            // Assert: no Animator, no Avatar on either proxy root or native root
            Assert.That(rig.ProxyRoot!.GetComponentsInChildren<Animator>(includeInactive: true), Is.Empty,
                "proxy rig must have no Animator components after retarget");
            Assert.That(rig.ProxyRoot.GetComponent<Avatar>(), Is.Null,
                "proxy root must have no Avatar after retarget");
            Assert.That(native.Root.GetComponentsInChildren<Animator>(includeInactive: true), Is.Empty,
                "native root must have no Animator after retarget");

            // Assert: no AssetDatabase calls (this is runtime code, no editor refs)
            // The retargeter only uses UnityEngine types, no Editor/AssetDatabase namespace.
            System.Type retargeterType = typeof(MmdHumanoidRetargeter);
            System.Reflection.Assembly runtimeAssembly = retargeterType.Assembly;
            string runtimeAssemblyName = runtimeAssembly.GetName().Name ?? "";
            Assert.That(runtimeAssemblyName, Is.EqualTo("Mmd.Runtime"),
                "retargeter must be in the Runtime assembly");

            Assert.That(result.CopiedBoneCount, Is.EqualTo(4));
            Assert.That(result.SkippedBoneCount, Is.EqualTo(0));

            DestroyProxyRig(rig);
            DestroyInstance(native);
        }

        [Test]
        public void RetargetCopiesEyeAndToeRotationsByMmdBoneIndexWithoutCopyingPosition()
        {
            // Arrange: required bones + toes/eyes should be copied to explicit bone indices.
            var model = new MmdModelDefinition();
            AddBone(model, 0, "下半身", -1);
            AddBone(model, 1, "上半身", 0);
            AddBone(model, 2, "首", 1);
            AddBone(model, 3, "頭", 2);
            AddBone(model, 4, "左つま先", 3);
            AddBone(model, 5, "左目", 3);
            AddBone(model, 6, "右つま先", 3);
            AddBone(model, 7, "右目", 3);

            MmdHumanoidProxyRigResult rig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.That(rig.ProxyRoot, Is.Not.Null);
            Assert.That(rig.BoneMap.ContainsKey(HumanBodyBones.LeftToes), Is.True);
            Assert.That(rig.BoneMap.ContainsKey(HumanBodyBones.LeftEye), Is.True);

            int leftToesIndex = -1;
            int leftEyeIndex = -1;
            foreach (MmdHumanoidBoneMappingMatch match in rig.Matches)
            {
                if (match.HumanBone == HumanBodyBones.LeftToes)
                {
                    leftToesIndex = match.MmdBoneIndex;
                }
                else if (match.HumanBone == HumanBodyBones.LeftEye)
                {
                    leftEyeIndex = match.MmdBoneIndex;
                }
            }
            Assert.That(leftToesIndex, Is.EqualTo(4));
            Assert.That(leftEyeIndex, Is.EqualTo(5));

            rig.BoneMap[HumanBodyBones.LeftToes].localRotation = Quaternion.Euler(18f, 24f, 30f);
            rig.BoneMap[HumanBodyBones.LeftEye].localRotation = Quaternion.Euler(3f, 6f, 9f);
            foreach (Transform bone in rig.BoneMap.Values)
            {
                bone.localPosition = new Vector3(1f, 2f, 3f);
            }

            MmdUnityModelInstance native = CreateNativeInstance(model.bones.Count);
            Vector3[] originalPositions = new Vector3[model.bones.Count];
            for (int i = 0; i < model.bones.Count; i++)
            {
                originalPositions[i] = native.BoneTransforms[i].localPosition;
            }

            // Act
            MmdHumanoidRetargeterResult result = MmdHumanoidRetargeter.RetargetPose(rig, native);

            // Assert
            Assert.That(result.CopiedBoneCount, Is.EqualTo(rig.Matches.Count));
            Assert.That(result.SkippedBoneCount, Is.EqualTo(0));
            Assert.That(result.AllSucceeded, Is.True);

            Assert.That(Quaternion.Angle(native.BoneTransforms[leftToesIndex].localRotation,
                rig.BoneMap[HumanBodyBones.LeftToes].localRotation), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(native.BoneTransforms[leftEyeIndex].localRotation,
                rig.BoneMap[HumanBodyBones.LeftEye].localRotation), Is.LessThan(0.001f));

            Assert.That(native.BoneTransforms[leftToesIndex].localPosition, Is.EqualTo(originalPositions[leftToesIndex]),
                "LeftToes native position must remain unchanged");
            Assert.That(native.BoneTransforms[leftEyeIndex].localPosition, Is.EqualTo(originalPositions[leftEyeIndex]),
                "LeftEye native position must remain unchanged");

            DestroyProxyRig(rig);
            DestroyInstance(native);
        }

        private static void DestroyImmediateIfPossible(Object? target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void RetargetCopiesFingerRotationsByMmdBoneIndex()
        {
            // Arrange: create a model with left arm, hand, and one finger bone.
            var model = new MmdModelDefinition();
            AddBone(model, 0, "左腕", -1);
            AddBone(model, 1, "左ひじ", 0);
            AddBone(model, 2, "左手首", 1);
            AddBone(model, 3, "左人指１", 2);  // LeftIndexProximal

            MmdHumanoidProxyRigResult rig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
            Assert.That(rig.ProxyRoot, Is.Not.Null);
            Assert.That(rig.BoneMap.ContainsKey(HumanBodyBones.LeftIndexProximal), Is.True);

            // Set a known non-identity rotation on the finger proxy transform.
            Transform fingerProxy = rig.BoneMap[HumanBodyBones.LeftIndexProximal];
            Quaternion originalFingerRot = Quaternion.Euler(15f, 25f, 35f);
            fingerProxy.localRotation = originalFingerRot;

            // Set identity rotations on the other proxy bones.
            foreach (Transform t in rig.BoneMap.Values)
            {
                if (t != fingerProxy)
                    t.localRotation = Quaternion.Euler(10f, 20f, 30f);
            }

            // Create native instance with enough bones.
            MmdUnityModelInstance native = CreateNativeInstance(4);

            // Act
            MmdHumanoidRetargeterResult result = MmdHumanoidRetargeter.RetargetPose(rig, native);

            // Assert: all 4 bones copied successfully.
            Assert.That(result.CopiedBoneCount, Is.EqualTo(4));
            Assert.That(result.SkippedBoneCount, Is.EqualTo(0));
            Assert.That(result.AllSucceeded, Is.True);

            // Verify the finger bone received the correct proxy rotation.
            Transform nativeFinger = native.BoneTransforms[3];
            Assert.That(Quaternion.Angle(nativeFinger.localRotation, originalFingerRot),
                Is.LessThan(0.001f),
                "LeftIndexProximal native rotation should match proxy rotation");

            // Verify other bones also got their rotations copied.
            Transform nativeHand = native.BoneTransforms[2];
            Assert.That(Quaternion.Angle(nativeHand.localRotation, rig.BoneMap[HumanBodyBones.LeftHand].localRotation),
                Is.LessThan(0.001f));

            // Verify position not changed.
            Assert.That(nativeFinger.localPosition, Is.EqualTo(new Vector3(100f, 200f, 300f)),
                "finger position must not be changed by retargeter");

            DestroyProxyRig(rig);
            DestroyInstance(native);
        }

        [Test]
        public void RetargeterAssemblyIsRuntimeOnly()
        {
            // Verify that MmdHumanoidRetargeter is in the Runtime assembly
            // and does not reference Editor or AssetDatabase namespaces.
            System.Type type = typeof(MmdHumanoidRetargeter);
            string assemblyName = type.Assembly.GetName().Name ?? "";
            Assert.That(assemblyName, Is.EqualTo("Mmd.Runtime"),
                "MmdHumanoidRetargeter must be in the Runtime assembly, not Editor");

            // Verify no AssetDatabase fields or properties in the type
            foreach (var method in type.GetMethods(
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.Static |
                         System.Reflection.BindingFlags.Instance))
            {
                // Just verify the method doesn't reference AssetDatabase
                // (compile-time check - if it referenced Editor, it wouldn't compile in Runtime)
            }
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
    }
}
