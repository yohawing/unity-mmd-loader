#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Editor;
using Mmd.Parser;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdHumanoidClipConversionWriterTests
    {
        private const string FixturePmxPath =
            "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube.pmx";

        private const string FixtureVmdPath =
            "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube_motion.vmd";

        private static readonly string[] RequiredBoneNames =
        {
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
        };

        [Test]
        public void CreateInMemoryClipUsesImportedHumanoidAvatarAndBindings()
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            MmdHumanoidClipConversionWriterResult? result = null;

            try
            {
                CreateReadyFixturePmx(out pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                Assert.That(vmdAsset, Is.Not.Null);

                result = MmdHumanoidClipConversionWriter.CreateInMemoryClip(
                    pmxAsset,
                    vmdAsset!,
                    frameRate: 30.0f,
                    startFrame: 0,
                    endFrame: 2);

                Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                Assert.That(result.PrerequisitesReady, Is.True);
                Assert.That(result.CanCreateClipNow, Is.True);
                Assert.That(
                    string.Join("\n", result.Diagnostics),
                    Does.Contain(MmdHumanoidClipConversionPlanner.ImportedPmxHumanoidMappingSource));
                AssertHumanoidClipHasMuscleBindings(result.Clip!);
                AssertRootMotionBindings(result.Clip!);
            }
            finally
            {
                if (result?.Clip != null)
                {
                    UnityEngine.Object.DestroyImmediate(result.Clip);
                }

                foreach (UnityEngine.Object obj in ownedObjects)
                {
                    if (obj != null)
                    {
                        UnityEngine.Object.DestroyImmediate(obj);
                    }
                }
            }
        }

        [Test]
        public void CreateHumanoidAnimationClipAssetWritesAnimationClipToAssetsPath()
        {
            const string outputDirectory = "Assets/HumanoidClipWriterTests";
            const string outputPath = outputDirectory + "/baked.anim";
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            MmdHumanoidClipConversionWriterResult? result = null;

            try
            {
                CreateReadyFixturePmx(out pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                AssetDatabase.DeleteAsset(outputDirectory);
                CreateFolderIfMissing(outputDirectory);

                result = MmdHumanoidClipConversionWriter.CreateHumanoidAnimationClipAsset(
                    pmxAsset, vmdAsset, 30.0f, 0, 2, outputPath);

                Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                Assert.That(AssetDatabase.GetAssetPath(result.Clip), Is.EqualTo(outputPath));
                Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath), Is.SameAs(result.Clip));
                AssertRootMotionBindings(result.Clip!);
            }
            finally
            {
                AssetDatabase.DeleteAsset(outputDirectory);
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [TestCase("../outside.anim")]
        [TestCase("Assets/not-animation.asset")]
        public void CreateHumanoidAnimationClipAssetRejectsInvalidOutputPath(string invalidOutputPath)
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();

            try
            {
                CreateReadyFixturePmx(out pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);

                MmdHumanoidClipConversionWriterResult result =
                    MmdHumanoidClipConversionWriter.CreateHumanoidAnimationClipAsset(
                        pmxAsset, vmdAsset, 30.0f, 0, 2, invalidOutputPath);

                Assert.That(result.Clip, Is.Null);
                Assert.That(result.Diagnostics, Is.Not.Empty);
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void GetDefaultOutputPathUsesAssetsDirectoryAndSourceIds()
        {
            MmdPmxAsset pmxAsset = null!;
            var ownedObjects = new List<UnityEngine.Object>();
            try
            {
                CreateReadyFixturePmx(out pmxAsset, ownedObjects);
                Assert.That(
                    MmdHumanoidClipConversionWriter.GetDefaultOutputPath(pmxAsset, null),
                    Is.EqualTo("Assets/H6_HumanoidClip_ready-h6-writer-slice1_pmx_vmd.anim"));
            }
            finally
            {
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void CreateInMemoryClipUsesClipLocalTimeWhenStartFrameIsNonZero()
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            MmdHumanoidClipConversionWriterResult? result = null;
            try
            {
                CreateReadyFixturePmx(out pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                result = MmdHumanoidClipConversionWriter.CreateInMemoryClip(
                    pmxAsset, vmdAsset, 30.0f, startFrame: 1, endFrame: 2);

                Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                AnimationCurve rootCurve = AnimationUtility.GetEditorCurve(
                    result.Clip!,
                    EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), "RootT.x"));
                Assert.That(rootCurve.keys, Has.Length.EqualTo(2));
                Assert.That(rootCurve.keys[0].time, Is.Zero.Within(0.0001f));
                Assert.That(rootCurve.keys[1].time, Is.EqualTo(1.0f / 30.0f).Within(0.0001f));
            }
            finally
            {
                if (result?.Clip != null)
                {
                    UnityEngine.Object.DestroyImmediate(result.Clip);
                }
                DestroyOwnedObjects(ownedObjects);
            }
        }

        [Test]
        public void RootMotionKeysComposeHipsAncestorsConvertCoordinatesScaleAndIncludeHipsRotation()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "全ての親",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f },
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1,
                name = "センター",
                parentIndex = 0,
                origin = new[] { 0.0f, 1.0f, 0.0f },
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 2,
                name = "下半身",
                parentIndex = 1,
                origin = new[] { 0.0f, 2.0f, 0.0f },
            });

            Quaternion parentRotation = Quaternion.AngleAxis(90.0f, Vector3.up);
            Quaternion hipsRotation = Quaternion.AngleAxis(75.0f, Vector3.right);
            var motion = new MmdMotionDefinition { maxFrame = 1 };
            AddLinearBoneKeys(
                motion,
                "全ての親",
                new Vector3(1.0f, 2.0f, 3.0f),
                Quaternion.identity);
            AddLinearBoneKeys(
                motion,
                "センター",
                new Vector3(4.0f, 5.0f, 6.0f),
                parentRotation);
            AddLinearBoneKeys(
                motion,
                "下半身",
                new Vector3(7.0f, 8.0f, 9.0f),
                hipsRotation);

            var positionKeys = new[] { new Keyframe[2], new Keyframe[2], new Keyframe[2] };
            var rotationKeys = new[]
            {
                new Keyframe[2],
                new Keyframe[2],
                new Keyframe[2],
                new Keyframe[2],
            };

            bool success = MmdHumanoidClipConversionWriter.TryBuildRootMotionKeys(
                model,
                motion,
                hipsBoneIndex: 2,
                importScale: 0.5f,
                startFrame: 0,
                endFrame: 1,
                sampleFrameToTimeFactor: 1.0f / 30.0f,
                positionKeys,
                rotationKeys,
                out string diagnostic);

            Assert.That(success, Is.True, diagnostic);
            // The center rotation turns the Hips local translation before the root translation is added.
            // MMD end position delta: (14, 15, 2), then coordinate flip and scale => (-7, 7.5, -1).
            Assert.That(positionKeys[0][1].value, Is.EqualTo(-7.0f).Within(0.0001f));
            Assert.That(positionKeys[1][1].value, Is.EqualTo(7.5f).Within(0.0001f));
            Assert.That(positionKeys[2][1].value, Is.EqualTo(-1.0f).Within(0.0001f));

            var actualRootRotation = new Quaternion(
                rotationKeys[0][1].value,
                rotationKeys[1][1].value,
                rotationKeys[2][1].value,
                rotationKeys[3][1].value);
            Quaternion expectedRootRotation =
                MmdCoordinateSpace.MmdToUnityRotation(parentRotation * hipsRotation);
            Assert.That(Mathf.Abs(Quaternion.Dot(actualRootRotation, expectedRootRotation)),
                Is.EqualTo(1.0f).Within(0.0001f),
                "RootQ must contain the composed ancestor and Hips rotation because muscle curves do not store bodyRotation.");
            Assert.That(positionKeys[0][0].time, Is.Zero.Within(0.0001f));
            Assert.That(positionKeys[0][1].time, Is.EqualTo(1.0f / 30.0f).Within(0.0001f));
        }

        private static void CreateReadyFixturePmx(
            out MmdPmxAsset pmxAsset,
            List<UnityEngine.Object> ownedObjects)
        {
            var hierarchyRoot = new GameObject("H6WriterReadyPmxRoot");
            var modelObject = new GameObject("ReadyModel");
            modelObject.transform.SetParent(hierarchyRoot.transform, worldPositionStays: false);

            SkinnedMeshRenderer smr = modelObject.AddComponent<SkinnedMeshRenderer>();
            Mesh mesh = new Mesh
            {
                name = "H6WriterReadyPmxMesh",
            };
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0f, 1f, 0.5f),
            };
            mesh.triangles = new int[] { 0, 1, 2 };
            mesh.bindposes = new Matrix4x4[RequiredBoneNames.Length];
            smr.sharedMesh = mesh;

            Transform[] bones = new Transform[RequiredBoneNames.Length];
            for (int i = 0; i < RequiredBoneNames.Length; i++)
            {
                GameObject boneObject = new GameObject(RequiredBoneNames[i]);
                boneObject.transform.SetParent(modelObject.transform, worldPositionStays: false);
                bones[i] = boneObject.transform;
            }
            smr.bones = bones;

            pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            pmxAsset.Initialize(
                new byte[] { 0x10, 0x20, 0x30 },
                "ready-h6-writer-slice1.pmx",
                System.IO.Path.Combine("Assets", "ready-h6-writer-slice1.pmx"),
                importedMeshAsset: mesh,
                importedRootAsset: hierarchyRoot,
                hierarchyReadinessValue: MmdImportReadiness.Ready,
                rendererReadinessValue: MmdImportReadiness.Ready,
                boneBindingReadinessValue: MmdImportReadiness.Ready,
                parseSummary: new MmdPmxParseSummary(
                    "ready-h6-writer",
                    vertexCount: 3,
                    indexCount: 3,
                    boneCount: RequiredBoneNames.Length,
                    morphCount: 0,
                    materialCount: 1,
                    diffuseTextureReferenceCount: 0,
                    sphereTextureReferenceCount: 0,
                    toonTextureReferenceCount: 0,
                    transparentMaterialCount: 0,
                    edgeMaterialCount: 0,
                    ikCount: 0,
                    rigidbodyCount: 0,
                    jointCount: 0,
                    boundsMin: new Vector3(-0.5f, 0f, -0.5f),
                    boundsMax: new Vector3(0.5f, 1f, 0.5f),
                    materialSummaries: Array.Empty<MmdPmxMaterialSummary>()));

            ownedObjects.Add(pmxAsset);
            ownedObjects.Add(hierarchyRoot);
            ownedObjects.Add(mesh);
        }

        private static void ConfigureImportedHumanoidState(
            MmdPmxAsset pmxAsset,
            List<UnityEngine.Object> ownedObjects)
        {
            MmdHumanoidProxyRigResult proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(pmxAsset);
            Assert.That(proxyRig.ProxyRoot, Is.Not.Null, string.Join("\n", proxyRig.Diagnostics));
            Assert.That(proxyRig.Readiness, Is.EqualTo(MmdHumanoidMappingReadiness.Ready));

            MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyRig);
            Assert.That(avatarResult.Avatar, Is.Not.Null, string.Join("\n", avatarResult.Diagnostics));
            Assert.That(avatarResult.Avatar!.isValid, Is.True);
            Assert.That(avatarResult.Avatar.isHuman, Is.True);

            GameObject importedRoot = pmxAsset.ImportedRoot!;
            Transform[] nativeBones = importedRoot
                .GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true)!
                .bones;
            var bindings = new List<MmdHumanoidRetargetBinding>();
            foreach (MmdHumanoidBoneMappingMatch match in proxyRig.Matches)
            {
                proxyRig.BoneMap.TryGetValue(match.HumanBone, out Transform proxyTransform);
                bindings.Add(new MmdHumanoidRetargetBinding(
                    match.HumanBone,
                    match.MmdBoneIndex,
                    proxyTransform,
                    nativeBones[match.MmdBoneIndex]));
            }

            proxyRig.ProxyRoot!.transform.SetParent(importedRoot.transform, worldPositionStays: false);
            Mmd.UnityIntegration.MmdUnityPlaybackController controller =
                importedRoot.AddComponent<Mmd.UnityIntegration.MmdUnityPlaybackController>();
            controller.ConfigureHumanoidRetarget(
                proxyRig.ProxyRoot.transform,
                bindings,
                Array.Empty<MmdHumanoidAppendTransformBinding>());
            pmxAsset.ApplyHumanoidAvatarImportSummary(
                "Humanoid",
                avatarResult.Avatar,
                MmdHumanoidMappingReadiness.Ready,
                "test: imported Humanoid ready");

            ownedObjects.Add(proxyRig.ProxyRoot);
            ownedObjects.Add(avatarResult.Avatar);
        }

        private static void AssertHumanoidClipHasMuscleBindings(AnimationClip clip)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.That(clip.humanMotion, Is.True);
            Assert.That(bindings.Any(b => b.type == typeof(Animator) && string.IsNullOrEmpty(b.path)), Is.True);
            Assert.That(bindings.Any(b => b.propertyName == "m_LocalRotation.x"), Is.False);
        }

        private static void AssertRootMotionBindings(AnimationClip clip)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            string[] properties =
            {
                "RootT.x", "RootT.y", "RootT.z",
                "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w",
            };
            foreach (string property in properties)
            {
                Assert.That(
                    bindings.Any(binding => binding.type == typeof(Animator)
                                            && string.IsNullOrEmpty(binding.path)
                                            && binding.propertyName == property),
                    Is.True,
                    property);
            }
        }

        private static void DestroyOwnedObjects(IEnumerable<UnityEngine.Object> ownedObjects)
        {
            foreach (UnityEngine.Object obj in ownedObjects)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
        }

        private static void AddLinearBoneKeys(
            MmdMotionDefinition motion,
            string boneName,
            Vector3 endTranslation,
            Quaternion endRotation)
        {
            MmdBoneInterpolationDefinition interpolation = MmdTestFixtures.LinearBoneInterpolation();
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = boneName,
                frame = 0,
                translation = new[] { 0.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = interpolation,
            });
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = boneName,
                frame = 1,
                translation = new[] { endTranslation.x, endTranslation.y, endTranslation.z },
                rotation = new[] { endRotation.x, endRotation.y, endRotation.z, endRotation.w },
                interpolation = interpolation,
            });
        }

        private static void CreateFolderIfMissing(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string? parent = Path.GetDirectoryName(folderPath.Replace('/', '\\'))?.Replace('\\', '/');
                string folderName = Path.GetFileName(folderPath);
                if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
                {
                    CreateFolderIfMissing(parent!);
                }

                AssetDatabase.CreateFolder(
                    string.IsNullOrWhiteSpace(parent) ? "Assets" : parent,
                    folderName);
            }
        }
    }
}
