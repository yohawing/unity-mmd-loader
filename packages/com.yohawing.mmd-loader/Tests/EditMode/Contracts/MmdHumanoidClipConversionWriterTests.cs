#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
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
        public void CreateInMemoryClipPreservesHumanoidBodyHeightAndRotationInRootCurves()
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

                Mmd.UnityIntegration.MmdUnityPlaybackController controller = pmxAsset.ImportedRoot!
                    .GetComponent<Mmd.UnityIntegration.MmdUnityPlaybackController>();
                Assert.That(controller.HumanoidProxyRoot, Is.Not.Null);
                MmdHumanoidRetargetBinding hipsBinding = controller.HumanoidRetargetEntries
                    .Single(binding => binding.HumanBone == HumanBodyBones.Hips);
                hipsBinding.ProxyTransform!.localRotation *= Quaternion.AngleAxis(20.0f, Vector3.forward);

                var baselinePose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
                using (var poseHandler = new HumanPoseHandler(
                           pmxAsset.ImportedAvatar!,
                           pmxAsset.ImportedRoot!.transform))
                {
                    poseHandler.GetHumanPose(ref baselinePose);
                }

                result = MmdHumanoidClipConversionWriter.CreateInMemoryClip(
                    pmxAsset,
                    vmdAsset!,
                    frameRate: 30.0f,
                    startFrame: 0,
                    endFrame: 2);

                Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                Vector3 actualPosition = ReadRootPosition(result.Clip!, 0.0f);
                Quaternion actualRotation = ReadRootRotation(result.Clip!, 0.0f);
                Assert.That(baselinePose.bodyPosition.y, Is.GreaterThan(0.0f), "precondition: Humanoid body height");
                Assert.That(actualPosition.x, Is.EqualTo(baselinePose.bodyPosition.x).Within(0.0001f));
                Assert.That(actualPosition.y, Is.EqualTo(baselinePose.bodyPosition.y).Within(0.0001f));
                Assert.That(actualPosition.z, Is.EqualTo(baselinePose.bodyPosition.z).Within(0.0001f));
                Assert.That(
                    Mathf.Abs(Quaternion.Dot(actualRotation, baselinePose.bodyRotation)),
                    Is.EqualTo(1.0f).Within(0.0001f));
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

        [Test]
        public void ReimportedHumanoidClipDrivesFreshAvatarWithBodyHeightAndHipsRotation()
        {
            string outputDirectory = "Assets/HumanoidClipApplicationTests_" + Guid.NewGuid().ToString("N");
            string outputPath = outputDirectory + "/baked-humanoid.anim";
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var ownedObjects = new List<UnityEngine.Object>();
            MmdHumanoidClipConversionWriterResult? result = null;
            AnimationClip? inMemoryClip = null;
            GameObject? referenceRoot = null;
            GameObject? playbackRoot = null;
            PlayableGraph referenceGraph = default;
            PlayableGraph playbackGraph = default;

            try
            {
                CreateReadyFixturePmx(out pmxAsset, ownedObjects);
                ConfigureImportedHumanoidState(pmxAsset, ownedObjects);
                CreateFolderIfMissing(outputDirectory);

                Mmd.UnityIntegration.MmdUnityPlaybackController controller = pmxAsset.ImportedRoot!
                    .GetComponent<Mmd.UnityIntegration.MmdUnityPlaybackController>();
                Assert.That(controller.HumanoidProxyRoot, Is.Not.Null);
                Transform sourceAvatarRoot = pmxAsset.ImportedRoot.transform;
                MmdHumanoidRetargetBinding sourceHipsBinding = controller.HumanoidRetargetEntries
                    .Single(binding => binding.HumanBone == HumanBodyBones.Hips);
                Assert.That(sourceHipsBinding.ProxyTransform, Is.Not.Null);

                string hipsPath = AnimationUtility.CalculateTransformPath(
                    sourceHipsBinding.ProxyTransform,
                    sourceAvatarRoot);
                referenceRoot = CreateFreshHumanoidPlaybackRoot(sourceAvatarRoot, "InMemoryHumanoidPlaybackRoot");
                playbackRoot = CreateFreshHumanoidPlaybackRoot(sourceAvatarRoot, "ReimportedHumanoidPlaybackRoot");
                Transform? referenceHips = referenceRoot.transform.Find(hipsPath);
                Transform? playbackHips = playbackRoot.transform.Find(hipsPath);
                Assert.That(referenceHips, Is.Not.Null, "reference clone must preserve the Avatar Hips path");
                Assert.That(playbackHips, Is.Not.Null, "fresh clone must preserve the Avatar Hips path");
                Quaternion bindHipsRotation = playbackHips!.rotation;

                sourceHipsBinding.ProxyTransform!.localRotation *=
                    Quaternion.AngleAxis(20.0f, Vector3.forward);
                result = MmdHumanoidClipConversionWriter.CreateHumanoidAnimationClipAsset(
                    pmxAsset,
                    vmdAsset,
                    frameRate: 30.0f,
                    startFrame: 0,
                    endFrame: 2,
                    outputPath);

                Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                inMemoryClip = UnityEngine.Object.Instantiate(result.Clip);
                AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
                AnimationClip? reimported = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
                Assert.That(reimported, Is.Not.Null);
                Assert.That(reimported!.humanMotion, Is.True);

                Avatar avatar = pmxAsset.ImportedAvatar!;
                Assert.That(avatar, Is.Not.Null);
                Assert.That(avatar.isValid && avatar.isHuman, Is.True);
                Animator referenceAnimator = ConfigureHumanoidPlaybackAnimator(referenceRoot, avatar);
                Animator playbackAnimator = ConfigureHumanoidPlaybackAnimator(playbackRoot, avatar);
                HumanPose baselinePose = ReadHumanPose(avatar, playbackRoot.transform);
                float baselineHipsHeight = playbackHips.position.y - playbackRoot.transform.position.y;
                Assert.That(baselinePose.bodyPosition.y, Is.GreaterThan(0.0f));
                Assert.That(baselineHipsHeight, Is.GreaterThan(0.0f));

                referenceGraph = CreateHumanoidPlaybackGraph(
                    "InMemoryHumanoidBake",
                    referenceAnimator,
                    inMemoryClip!,
                    out AnimationClipPlayable referencePlayable);
                playbackGraph = CreateHumanoidPlaybackGraph(
                    "ReimportedHumanoidBake",
                    playbackAnimator,
                    reimported,
                    out AnimationClipPlayable playbackPlayable);

                Vector3 expectedBodyPosition = ReadRootPosition(reimported, 0.0f);
                Assert.That(expectedBodyPosition.y, Is.GreaterThan(0.0f),
                    "persisted RootT must retain the baked Humanoid body height");

                foreach (double time in new[] { 0.0, 2.0 / 30.0 })
                {
                    referencePlayable.SetTime(time);
                    playbackPlayable.SetTime(time);
                    referenceGraph.Evaluate(0.0f);
                    playbackGraph.Evaluate(0.0f);

                    HumanPose referencePose = ReadHumanPose(avatar, referenceRoot.transform);
                    HumanPose appliedPose = ReadHumanPose(avatar, playbackRoot.transform);
                    Assert.That(Vector3.Distance(appliedPose.bodyPosition, referencePose.bodyPosition),
                        Is.LessThan(0.0001f), "body position round-trip parity at " + time);
                    Assert.That(Mathf.Abs(Quaternion.Dot(appliedPose.bodyRotation, referencePose.bodyRotation)),
                        Is.EqualTo(1.0f).Within(0.0001f), "body rotation round-trip parity at " + time);
                    Assert.That(Vector3.Distance(playbackRoot.transform.position, referenceRoot.transform.position),
                        Is.LessThan(0.0001f), "root position round-trip parity at " + time);
                    Assert.That(Quaternion.Angle(playbackRoot.transform.rotation, referenceRoot.transform.rotation),
                        Is.LessThan(0.01f), "root rotation round-trip parity at " + time);
                    Assert.That(Vector3.Distance(playbackHips.position, referenceHips!.position),
                        Is.LessThan(0.0001f), "Hips position round-trip parity at " + time);
                    Assert.That(Quaternion.Angle(playbackHips.rotation, referenceHips.rotation),
                        Is.LessThan(0.01f), "Hips rotation round-trip parity at " + time);

                    Assert.That(float.IsFinite(appliedPose.bodyPosition.y), Is.True);
                    Assert.That(appliedPose.bodyPosition.y, Is.GreaterThan(baselinePose.bodyPosition.y * 0.5f),
                        "reimported Humanoid clip must not collapse body/Hips height to Y=0");
                    float appliedHipsHeight = playbackHips.position.y - playbackRoot.transform.position.y;
                    Assert.That(appliedHipsHeight, Is.GreaterThan(baselineHipsHeight * 0.5f),
                        "fresh Avatar Hips must retain their bind-relative height after clip evaluation");
                    Assert.That(Quaternion.Angle(bindHipsRotation, playbackHips.rotation),
                        Is.GreaterThan(1.0f),
                        "reimported RootQ/muscle curves must visibly rotate the fresh Avatar Hips");
                }
            }
            finally
            {
                if (referenceGraph.IsValid()) referenceGraph.Destroy();
                if (playbackGraph.IsValid()) playbackGraph.Destroy();
                if (inMemoryClip != null) UnityEngine.Object.DestroyImmediate(inMemoryClip);
                if (referenceRoot != null) UnityEngine.Object.DestroyImmediate(referenceRoot);
                if (playbackRoot != null) UnityEngine.Object.DestroyImmediate(playbackRoot);
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
                    Is.EqualTo("Assets/ready-h6-writer-slice1_VMD.anim"));
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
                AnimationCurve verticalOffsetCurve = AnimationUtility.GetEditorCurve(
                    result.Clip!,
                    EditorCurveBinding.FloatCurve(
                        string.Empty,
                        typeof(MmdHumanoidRootMotionDriver),
                        "clipRootVerticalOffset"));
                Assert.That(verticalOffsetCurve.keys, Has.Length.EqualTo(2));
                Assert.That(verticalOffsetCurve.keys[0].time, Is.Zero.Within(0.0001f));
                Assert.That(verticalOffsetCurve.keys[1].time, Is.EqualTo(1.0f / 30.0f).Within(0.0001f));
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
        public void RootMotionKeysComposeAncestorDeltaWithPerFrameHumanoidBodyPose()
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
            var bodyPositions = new[]
            {
                new Vector3(0.25f, 1.5f, -0.5f),
                new Vector3(0.25f, 1.5f, -0.5f),
            };
            var bodyRotations = new[]
            {
                Quaternion.AngleAxis(30.0f, Vector3.forward),
                Quaternion.AngleAxis(75.0f, Vector3.right),
            };

            bool success = MmdHumanoidClipConversionWriter.TryBuildRootMotionKeys(
                model,
                motion,
                hipsBoneIndex: 2,
                importScale: 0.5f,
                humanScale: 2.0f,
                bodyPositions,
                bodyRotations,
                startFrame: 0,
                endFrame: 1,
                sampleFrameToTimeFactor: 1.0f / 30.0f,
                positionKeys,
                rotationKeys,
                out string diagnostic);

            Assert.That(success, Is.True, diagnostic);
            // The center rotation turns the Hips local translation before the root translation is added.
            // MMD end position delta: (14, 15, 2), then coordinate flip, import scale,
            // and HumanPose normalization => (-3.5, 3.75, -0.5), added to the baseline pose.
            Assert.That(positionKeys[0][1].value, Is.EqualTo(-3.25f).Within(0.0001f));
            Assert.That(positionKeys[1][1].value, Is.EqualTo(5.25f).Within(0.0001f));
            Assert.That(positionKeys[2][1].value, Is.EqualTo(-1.0f).Within(0.0001f));

            var actualRootRotation = new Quaternion(
                rotationKeys[0][1].value,
                rotationKeys[1][1].value,
                rotationKeys[2][1].value,
                rotationKeys[3][1].value);
            Quaternion expectedRootRotation =
                MmdCoordinateSpace.MmdToUnityRotation(parentRotation) * bodyRotations[1];
            Assert.That(Mathf.Abs(Quaternion.Dot(actualRootRotation, expectedRootRotation)),
                Is.EqualTo(1.0f).Within(0.0001f),
                "RootQ must add only non-Humanoid ancestor rotation to the per-frame body orientation.");
            Assert.That(positionKeys[1][0].value, Is.EqualTo(bodyPositions[0].y).Within(0.0001f));
            var initialRootRotation = new Quaternion(
                rotationKeys[0][0].value,
                rotationKeys[1][0].value,
                rotationKeys[2][0].value,
                rotationKeys[3][0].value);
            Assert.That(Mathf.Abs(Quaternion.Dot(initialRootRotation, bodyRotations[0])),
                Is.EqualTo(1.0f).Within(0.0001f));
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
                boneObject.transform.localPosition = GetHumanoidFixtureBonePosition(RequiredBoneNames[i]);
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

        private static Vector3 GetHumanoidFixtureBonePosition(string boneName)
        {
            return boneName switch
            {
                "下半身" => new Vector3(0.0f, 1.0f, 0.0f),
                "上半身" => new Vector3(0.0f, 1.25f, 0.0f),
                "首" => new Vector3(0.0f, 1.65f, 0.0f),
                "頭" => new Vector3(0.0f, 1.85f, 0.0f),
                "左足" => new Vector3(-0.18f, 0.9f, 0.0f),
                "左ひざ" => new Vector3(-0.18f, 0.5f, 0.0f),
                "左足首" => new Vector3(-0.18f, 0.1f, 0.0f),
                "右足" => new Vector3(0.18f, 0.9f, 0.0f),
                "右ひざ" => new Vector3(0.18f, 0.5f, 0.0f),
                "右足首" => new Vector3(0.18f, 0.1f, 0.0f),
                "左腕" => new Vector3(-0.35f, 1.5f, 0.0f),
                "左ひじ" => new Vector3(-0.65f, 1.5f, 0.0f),
                "左手首" => new Vector3(-0.9f, 1.5f, 0.0f),
                "右腕" => new Vector3(0.35f, 1.5f, 0.0f),
                "右ひじ" => new Vector3(0.65f, 1.5f, 0.0f),
                "右手首" => new Vector3(0.9f, 1.5f, 0.0f),
                _ => Vector3.zero,
            };
        }

        private static void ConfigureImportedHumanoidState(
            MmdPmxAsset pmxAsset,
            List<UnityEngine.Object> ownedObjects)
        {
            MmdHumanoidProxyRigResult proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(pmxAsset);
            Assert.That(proxyRig.ProxyRoot, Is.Not.Null, string.Join("\n", proxyRig.Diagnostics));
            Assert.That(proxyRig.Readiness, Is.EqualTo(MmdHumanoidMappingReadiness.Ready));

            GameObject importedRoot = pmxAsset.ImportedRoot!;
            proxyRig.ProxyRoot!.transform.SetParent(importedRoot.transform, worldPositionStays: false);
            MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(
                proxyRig,
                avatarRoot: importedRoot);
            Assert.That(avatarResult.Avatar, Is.Not.Null, string.Join("\n", avatarResult.Diagnostics));
            Assert.That(avatarResult.Avatar!.isValid, Is.True);
            Assert.That(avatarResult.Avatar.isHuman, Is.True);

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
            Assert.That(
                bindings.Any(binding => binding.type == typeof(MmdHumanoidRootMotionDriver)
                                        && string.IsNullOrEmpty(binding.path)
                                        && binding.propertyName == "clipRootVerticalOffset"),
                Is.True,
                "clipRootVerticalOffset");
        }

        private static GameObject CreateFreshHumanoidPlaybackRoot(Transform sourceRoot, string name)
        {
            GameObject clone = UnityEngine.Object.Instantiate(sourceRoot.gameObject);
            clone.name = name;
            clone.transform.SetParent(null, worldPositionStays: false);
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            clone.SetActive(true);
            return clone;
        }

        private static Animator ConfigureHumanoidPlaybackAnimator(GameObject root, Avatar avatar)
        {
            Animator animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return animator;
        }

        private static PlayableGraph CreateHumanoidPlaybackGraph(
            string name,
            Animator animator,
            AnimationClip clip,
            out AnimationClipPlayable playable)
        {
            PlayableGraph graph = PlayableGraph.Create(name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimationClipPlayable.Create(graph, clip);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, name, animator);
            output.SetSourcePlayable(playable);
            graph.Play();
            return graph;
        }

        private static HumanPose ReadHumanPose(Avatar avatar, Transform root)
        {
            var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
            using (var poseHandler = new HumanPoseHandler(avatar, root))
            {
                poseHandler.GetHumanPose(ref pose);
            }
            return pose;
        }

        private static Vector3 ReadRootPosition(AnimationClip clip, float time)
        {
            return new Vector3(
                ReadRootCurve(clip, "RootT.x").Evaluate(time),
                ReadRootCurve(clip, "RootT.y").Evaluate(time),
                ReadRootCurve(clip, "RootT.z").Evaluate(time));
        }

        private static Quaternion ReadRootRotation(AnimationClip clip, float time)
        {
            var rotation = new Quaternion(
                ReadRootCurve(clip, "RootQ.x").Evaluate(time),
                ReadRootCurve(clip, "RootQ.y").Evaluate(time),
                ReadRootCurve(clip, "RootQ.z").Evaluate(time),
                ReadRootCurve(clip, "RootQ.w").Evaluate(time));
            rotation.Normalize();
            return rotation;
        }

        private static AnimationCurve ReadRootCurve(AnimationClip clip, string propertyName)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), propertyName));
            Assert.That(curve, Is.Not.Null, propertyName);
            return curve;
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
