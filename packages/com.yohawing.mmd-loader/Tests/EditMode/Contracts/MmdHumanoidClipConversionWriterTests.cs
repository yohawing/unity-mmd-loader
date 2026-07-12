#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Editor;

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
        public void CreateInMemoryClipRejectsWhenSetupDoesNotMatchPmx()
        {
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(FixturePmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(vmdAsset, Is.Not.Null);

            var otherPmx = ScriptableObject.CreateInstance<MmdPmxAsset>();
            otherPmx.Initialize(
                new byte[] { 0x01, 0x02, 0x03 },
                "writer-mismatch.pmx",
                "Assets/writer-mismatch.pmx");

            var setupAsset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            var objects = new List<UnityEngine.Object>();
            try
            {
                setupAsset.Initialize(otherPmx);
                objects.Add(otherPmx);

                MmdHumanoidClipConversionWriterResult result =
                    MmdHumanoidClipConversionWriter.CreateInMemoryClip(
                        pmxAsset!,
                        vmdAsset!,
                        setupAsset,
                        frameRate: 30.0f);

                Assert.That(result.Clip, Is.Null);
                Assert.That(result.CanCreateClipNow, Is.False);
                Assert.That(result.PrerequisitesReady, Is.False);
                Assert.That(result.Diagnostics, Is.Not.Empty);
                Assert.That(
                    string.Join("\n", result.Diagnostics),
                    Does.Contain("setup.PmxAsset mismatch"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(setupAsset);
                foreach (UnityEngine.Object obj in objects)
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
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var setupAsset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            var ownedObjects = new List<UnityEngine.Object>();
            MmdHumanoidClipConversionWriterResult? result = null;
            string tempFolder = "Assets/H6ClipConversionWriterTests_" + Guid.NewGuid().ToString("N");
            string outputPath = tempFolder + "/humanoid-conversion-" + Guid.NewGuid().ToString("N") + ".anim";
            int clipCountBefore = AssetDatabase.FindAssets("t:AnimationClip").Length;

            try
            {
                CreateReadyFixturePmxAndSetup(out pmxAsset, setupAsset, ownedObjects);
                Assert.That(vmdAsset, Is.Not.Null);
                Assert.That(pmxAsset, Is.Not.Null);

                result = MmdHumanoidClipConversionWriter.CreateHumanoidAnimationClipAsset(
                    pmxAsset,
                    vmdAsset,
                    setupAsset,
                    frameRate: 30.0f,
                    startFrame: 0,
                    endFrame: 2,
                    outputPath: outputPath);

                Assert.That(result.Clip, Is.Not.Null);
                Assert.That(result.Plan.CanCreateClipNow, Is.True);
                Assert.That(AssetDatabase.Contains(result.Clip), Is.True);
                Assert.That(AssetDatabase.GetAssetPath(result.Clip), Does.StartWith(tempFolder));
                Assert.That(AssetDatabase.FindAssets("t:AnimationClip").Length, Is.EqualTo(clipCountBefore + 1));
                AssertHumanoidClipHasMuscleBindings(result.Clip!);
            }
            finally
            {
                if (result != null && result.Clip != null)
                {
                    string savedPath = AssetDatabase.GetAssetPath(result.Clip);
                    if (!string.IsNullOrWhiteSpace(savedPath))
                    {
                        AssetDatabase.DeleteAsset(savedPath);
                    }
                }

                if (AssetDatabase.IsValidFolder(tempFolder))
                {
                    AssetDatabase.DeleteAsset(tempFolder);
                }

                UnityEngine.Object.DestroyImmediate(setupAsset);
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
        [TestCase("")]
        [TestCase("External/Generated/humanoid.anim")]
        [TestCase("/tmp/humanoid.anim")]
        [TestCase("Assets/../humanoid.anim")]
        [TestCase("Assets/./humanoid.anim")]
        [TestCase("Assets//humanoid.anim")]
        [TestCase("Temp/humanoid.anim")]
        [TestCase("Assets/humanoid")]
        public void CreateHumanoidAnimationClipAssetRejectsInvalidOutputPath(string invalidOutputPath)
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var setupAsset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            var ownedObjects = new List<UnityEngine.Object>();
            MmdHumanoidClipConversionWriterResult? result = null;
            int clipCountBefore = AssetDatabase.FindAssets("t:AnimationClip").Length;

            try
            {
                CreateReadyFixturePmxAndSetup(out pmxAsset, setupAsset, ownedObjects);
                Assert.That(vmdAsset, Is.Not.Null);
                Assert.That(pmxAsset, Is.Not.Null);

                result = MmdHumanoidClipConversionWriter.CreateHumanoidAnimationClipAsset(
                    pmxAsset,
                    vmdAsset,
                    setupAsset,
                    frameRate: 30.0f,
                    startFrame: 0,
                    endFrame: 2,
                    outputPath: invalidOutputPath);

                Assert.That(result.Clip, Is.Null);
                Assert.That(result.Diagnostics, Is.Not.Empty);
                Assert.That(result.Diagnostics.Any(d => d.StartsWith("validation: output path")), Is.True);
                Assert.That(AssetDatabase.FindAssets("t:AnimationClip").Length, Is.EqualTo(clipCountBefore));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(setupAsset);
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
        public void CreateHumanoidAnimationClipAssetDefaultOutputPathPrefersSetupAssetDirectory()
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var setupAsset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            var ownedObjects = new List<UnityEngine.Object>();
            string setupDirectory = "Assets/H6HumanoidClipSetupOutput_" + Guid.NewGuid().ToString("N");
            string setupAssetPath = setupDirectory + "/MmdHumanoidSetup.asset";

            try
            {
                CreateReadyFixturePmxAndSetup(out pmxAsset, setupAsset, ownedObjects);
                Assert.That(vmdAsset, Is.Not.Null);
                Assert.That(pmxAsset, Is.Not.Null);

                CreateFolderIfMissing(setupDirectory);
                setupAsset.Initialize(pmxAsset);
                AssetDatabase.CreateAsset(setupAsset, setupAssetPath);
                AssetDatabase.ImportAsset(setupAssetPath, ImportAssetOptions.ForceUpdate);

                string defaultOutputPath = MmdHumanoidClipConversionWriter.GetDefaultOutputPath(
                    setupAsset,
                    pmxAsset,
                    vmdAsset);

                Assert.That(defaultOutputPath, Does.StartWith(setupDirectory + "/"));
                Assert.That(defaultOutputPath, Does.EndWith(".anim"));
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(setupDirectory))
                {
                    AssetDatabase.DeleteAsset(setupDirectory);
                }

                if (setupAsset != null && !AssetDatabase.Contains(setupAsset))
                {
                    UnityEngine.Object.DestroyImmediate(setupAsset);
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
        public void CreateInMemoryClipWritesHumanoidMuscleCurvesWithoutSavingAnimationClipAsset()
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var setupAsset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            var clipGuidsBefore = AssetDatabase.FindAssets("t:AnimationClip");
            var ownedObjects = new List<UnityEngine.Object>();
            MmdHumanoidClipConversionWriterResult? result = null;

            try
            {
                CreateReadyFixturePmxAndSetup(out pmxAsset, setupAsset, ownedObjects);
                Assert.That(vmdAsset, Is.Not.Null);
                Assert.That(pmxAsset, Is.Not.Null);

                result = MmdHumanoidClipConversionWriter.CreateInMemoryClip(
                    pmxAsset,
                    vmdAsset,
                    setupAsset,
                    frameRate: 30.0f,
                    startFrame: 0);

                string[] clipGuidsAfter = AssetDatabase.FindAssets("t:AnimationClip");

                Assert.That(result.Clip, Is.Not.Null);
                Assert.That(result.Plan.CanCreateClipNow, Is.True);
                Assert.That(result.Clip!.frameRate, Is.EqualTo(30.0f));
                Assert.That(result.Clip.length, Is.GreaterThan(0.0f));
                Assert.That(AssetDatabase.Contains(result.Clip), Is.False);
                Assert.That(clipGuidsAfter.Length, Is.EqualTo(clipGuidsBefore.Length));
                Assert.That(result.Diagnostics, Is.Not.Empty);

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(result.Clip);
                Assert.That(result.Clip.humanMotion, Is.True);
                Assert.That(bindings.Any(b => b.type == typeof(Animator) && string.IsNullOrEmpty(b.path)), Is.True);
                Assert.That(bindings.Any(b => b.propertyName == "m_LocalRotation.x"), Is.False);
            }
            finally
            {
                if (result != null && result.Clip != null)
                {
                    UnityEngine.Object.DestroyImmediate(result.Clip);
                }

                UnityEngine.Object.DestroyImmediate(setupAsset);
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
        public void CreateHumanoidClipConversionWriterCanCreateFromInspectorEligibility()
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var setupAsset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            var ownedObjects = new List<UnityEngine.Object>();
            MmdHumanoidClipConversionWriterResult? result = null;

            try
            {
                CreateReadyFixturePmxAndSetup(out pmxAsset, setupAsset, ownedObjects);
                Assert.That(vmdAsset, Is.Not.Null);
                Assert.That(pmxAsset, Is.Not.Null);

                string outputPath = "Assets/H6HumanoidClipEligibility/humanoid-eligibility.anim";
                result = MmdHumanoidClipConversionWriter.CreateHumanoidAnimationClipAsset(
                    pmxAsset,
                    vmdAsset!,
                    setupAsset,
                    frameRate: 30.0f,
                    startFrame: 0,
                    endFrame: 0,
                    outputPath: outputPath);

                Assert.That(result.Plan.CanCreateClipNow, Is.True);
                Assert.That(result.Clip, Is.Not.Null);
            }
            finally
            {
                if (result != null && result.Clip != null)
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(result.Clip));
                }

                if (AssetDatabase.IsValidFolder("Assets/H6HumanoidClipEligibility"))
                {
                    AssetDatabase.DeleteAsset("Assets/H6HumanoidClipEligibility");
                }

                UnityEngine.Object.DestroyImmediate(setupAsset);
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
        public void CreateInMemoryClipUsesClipLocalTimeWhenStartFrameIsNonZero()
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var setupAsset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            var ownedObjects = new List<UnityEngine.Object>();
            MmdHumanoidClipConversionWriterResult? result = null;

            try
            {
                CreateReadyFixturePmxAndSetup(out pmxAsset, setupAsset, ownedObjects);
                Assert.That(vmdAsset, Is.Not.Null);
                Assert.That(pmxAsset, Is.Not.Null);

                result = MmdHumanoidClipConversionWriter.CreateInMemoryClip(
                    pmxAsset,
                    vmdAsset,
                    setupAsset,
                    frameRate: 30.0f,
                    startFrame: 1,
                    endFrame: 2);

                Assert.That(result.Clip, Is.Not.Null);

                EditorCurveBinding binding = AnimationUtility.GetCurveBindings(result.Clip!)
                    .FirstOrDefault(b => b.type == typeof(Animator) && string.IsNullOrEmpty(b.path));
                Assert.That(binding.propertyName, Is.Not.Empty);

                AnimationCurve curve = AnimationUtility.GetEditorCurve(result.Clip!, binding);
                Assert.That(curve, Is.Not.Null);
                Assert.That(curve.keys, Has.Length.EqualTo(2));
                Assert.That(curve.keys[0].time, Is.EqualTo(0.0f).Within(0.0001f));
                Assert.That(curve.keys[1].time, Is.EqualTo(1.0f / 30.0f).Within(0.0001f));
            }
            finally
            {
                if (result != null && result.Clip != null)
                {
                    UnityEngine.Object.DestroyImmediate(result.Clip);
                }

                UnityEngine.Object.DestroyImmediate(setupAsset);
                foreach (UnityEngine.Object obj in ownedObjects)
                {
                    if (obj != null)
                    {
                        UnityEngine.Object.DestroyImmediate(obj);
                    }
                }
            }
        }

        private static void CreateReadyFixturePmxAndSetup(
            out MmdPmxAsset pmxAsset,
            MmdHumanoidSetupAsset setupAsset,
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

            setupAsset.Initialize(pmxAsset);

            ownedObjects.Add(pmxAsset);
            ownedObjects.Add(hierarchyRoot);
            ownedObjects.Add(mesh);
        }

        private static void AssertHumanoidClipHasMuscleBindings(AnimationClip clip)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.That(clip.humanMotion, Is.True);
            Assert.That(bindings.Any(b => b.type == typeof(Animator) && string.IsNullOrEmpty(b.path)), Is.True);
            Assert.That(bindings.Any(b => b.propertyName == "m_LocalRotation.x"), Is.False);
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
