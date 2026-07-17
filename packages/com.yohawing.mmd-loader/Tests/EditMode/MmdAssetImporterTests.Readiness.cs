#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Mmd.Editor;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Rendering;
using Mmd.Rendering.Universal;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed partial class MmdAssetImporterTests
    {
        [Test]
        public void HumanoidBoneMappingEvaluatorMapsStandardMmdNamesAndIgnoresHelpers()
        {
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
                "左足ＩＫ",
                "右腕捩",
                "左目操作");

            MmdHumanoidBoneMappingReport report = MmdHumanoidBoneMappingEvaluator.Evaluate(model);

            Assert.That(report.Readiness, Is.EqualTo(MmdHumanoidMappingReadiness.Ready));
            Assert.That(report.RequiredMappedBoneCount, Is.EqualTo(16));
            Assert.That(report.OptionalMappedBoneCount, Is.EqualTo(3));
            Assert.That(report.MissingRequiredBoneCount, Is.EqualTo(0));
            Assert.That(report.AmbiguousMappingCount, Is.EqualTo(0));
            Assert.That(report.IgnoredHelperBoneCount, Is.EqualTo(3));

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左足首", out HumanBodyBones humanBone, out bool required), Is.True);
            Assert.That(humanBone, Is.EqualTo(HumanBodyBones.LeftFoot));
            Assert.That(required, Is.True);
            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左足ＩＫ", out _, out _), Is.False);
        }
        [Test]
        public void HumanoidBoneMappingEvaluatorPinsShoulderToeEyeOptionalEntries()
        {
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

            MmdHumanoidBoneMappingReport report = MmdHumanoidBoneMappingEvaluator.Evaluate(model);

            Assert.That(report.Readiness, Is.EqualTo(MmdHumanoidMappingReadiness.Ready));
            Assert.That(report.OptionalMappedBoneCount, Is.EqualTo(7));

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左肩", out HumanBodyBones shoulderLeft, out bool shoulderLeftRequired),
                Is.True);
            Assert.That(shoulderLeft, Is.EqualTo(HumanBodyBones.LeftShoulder));
            Assert.That(shoulderLeftRequired, Is.False);

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右肩", out HumanBodyBones shoulderRight, out bool shoulderRightRequired),
                Is.True);
            Assert.That(shoulderRight, Is.EqualTo(HumanBodyBones.RightShoulder));
            Assert.That(shoulderRightRequired, Is.False);

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左つま先", out HumanBodyBones toeLeft, out bool toeLeftRequired),
                Is.True);
            Assert.That(toeLeft, Is.EqualTo(HumanBodyBones.LeftToes));
            Assert.That(toeLeftRequired, Is.False);

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右つま先", out HumanBodyBones toeRight, out bool toeRightRequired),
                Is.True);
            Assert.That(toeRight, Is.EqualTo(HumanBodyBones.RightToes));
            Assert.That(toeRightRequired, Is.False);

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("左目", out HumanBodyBones eyeLeft, out bool eyeLeftRequired),
                Is.True);
            Assert.That(eyeLeft, Is.EqualTo(HumanBodyBones.LeftEye));
            Assert.That(eyeLeftRequired, Is.False);

            Assert.That(MmdHumanoidBoneMappingEvaluator.TryMapBoneName("右目", out HumanBodyBones eyeRight, out bool eyeRightRequired),
                Is.True);
            Assert.That(eyeRight, Is.EqualTo(HumanBodyBones.RightEye));
            Assert.That(eyeRightRequired, Is.False);

            bool hasShoulderLeftEntry = false;
            bool hasShoulderRightEntry = false;
            bool hasToeLeftEntry = false;
            bool hasToeRightEntry = false;
            bool hasEyeLeftEntry = false;
            bool hasEyeRightEntry = false;
            bool hasChestEntry = false;

            foreach (MmdSerializableBoneMappingEntry entry in report.MappingEntries)
            {
                if (entry.Category != "Optional")
                {
                    continue;
                }

                Assert.That(entry.Required, Is.False);

                switch (entry.MmdBoneName)
                {
                    case "上半身2":
                        hasChestEntry = true;
                        break;
                    case "左肩":
                        hasShoulderLeftEntry = true;
                        break;
                    case "右肩":
                        hasShoulderRightEntry = true;
                        break;
                    case "左つま先":
                        hasToeLeftEntry = true;
                        break;
                    case "右つま先":
                        hasToeRightEntry = true;
                        break;
                    case "左目":
                        hasEyeLeftEntry = true;
                        break;
                    case "右目":
                        hasEyeRightEntry = true;
                        break;
                }
            }

            Assert.That(hasChestEntry, Is.True);
            Assert.That(hasShoulderLeftEntry, Is.True);
            Assert.That(hasShoulderRightEntry, Is.True);
            Assert.That(hasToeLeftEntry, Is.True);
            Assert.That(hasToeRightEntry, Is.True);
            Assert.That(hasEyeLeftEntry, Is.True);
            Assert.That(hasEyeRightEntry, Is.True);
        }
        [Test]
        public void HumanoidBoneMappingEvaluatorReportsMissingRequiredAndAmbiguousMappings()
        {
            MmdModelDefinition missingModel = CreateHumanoidMappingModel("下半身", "上半身", "頭");
            MmdHumanoidBoneMappingReport missingReport = MmdHumanoidBoneMappingEvaluator.Evaluate(missingModel);

            Assert.That(missingReport.Readiness, Is.EqualTo(MmdHumanoidMappingReadiness.MissingRequired));
            Assert.That(missingReport.RequiredMappedBoneCount, Is.EqualTo(3));
            Assert.That(missingReport.MissingRequiredBoneCount, Is.EqualTo(13));
            Assert.That(string.Join("\n", missingReport.Diagnostics), Does.Contain("missing-required"));

            MmdModelDefinition ambiguousModel = CreateHumanoidMappingModel(
                "下半身",
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
                "右手首");
            MmdHumanoidBoneMappingReport ambiguousReport = MmdHumanoidBoneMappingEvaluator.Evaluate(ambiguousModel);

            Assert.That(ambiguousReport.Readiness, Is.EqualTo(MmdHumanoidMappingReadiness.Ambiguous));
            Assert.That(ambiguousReport.AmbiguousMappingCount, Is.EqualTo(1));
            Assert.That(string.Join("\n", ambiguousReport.Diagnostics), Does.Contain("ambiguous: Hips"));
        }
        [Test]
        public void HumanoidBoneMappingEvaluatorReportCarriesMappingEntriesForRequiredOptionalAndFinger()
        {
            // Required: 下半身, 上半身, 首, 頭 (4)
            // Optional: 上半身2, 左肩 (2)
            // Finger:   左親指１, 左親指２, 右親指１, 右人指先 (4)
            MmdModelDefinition model = CreateHumanoidMappingModel(
                "下半身",
                "上半身",
                "首",
                "頭",
                "上半身2",
                "左肩",
                "左親指１",
                "左親指２",
                "右親指１",
                "右人指先");

            MmdHumanoidBoneMappingReport report = MmdHumanoidBoneMappingEvaluator.Evaluate(model);

            MmdSerializableBoneMappingEntry[] entries = report.MappingEntries;
            int requiredCount = 0, optionalCount = 0, fingerCount = 0;
            foreach (MmdSerializableBoneMappingEntry entry in entries)
            {
                Assert.That(entry.MmdBoneName, Is.Not.Empty);
                Assert.That(entry.MmdBoneIndex, Is.GreaterThanOrEqualTo(0));
                switch (entry.Category)
                {
                    case "Required":
                        Assert.That(entry.Required, Is.True);
                        requiredCount++;
                        break;
                    case "Optional":
                        Assert.That(entry.Required, Is.False);
                        optionalCount++;
                        break;
                    case "Finger":
                        Assert.That(entry.Required, Is.False);
                        fingerCount++;
                        break;
                    default:
                        Assert.Fail($"Unexpected category: {entry.Category}");
                        break;
                }
            }

            Assert.That(requiredCount, Is.EqualTo(4), "should have 4 required entries");
            Assert.That(optionalCount, Is.EqualTo(2), "should have 2 optional entries");
            Assert.That(fingerCount, Is.EqualTo(4), "should have 4 finger entries");
            Assert.That(entries.Length, Is.EqualTo(10), "total 10 mapping entries");
        }
        [Test]
        public void PmxPrefabExporterCreatesPrefabWithPersistentMeshMaterialAndProvenance()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Scene activeScene = SceneManager.GetActiveScene();
            bool wasDirty = activeScene.isDirty;

            MmdPmxPrefabExportResult result = MmdPmxPrefabExporter.CreatePrefab(pmxAsset, TempPrefabPath);

            Assert.That(result.PrefabAssetPath, Is.EqualTo(TempPrefabPath));
            Assert.That(result.OutputDirectory, Is.EqualTo(TempDirectory));
            Assert.That(result.MeshAssetPaths, Has.Length.EqualTo(1));
            Assert.That(result.MaterialAssetPaths, Has.Length.GreaterThan(0));
            Assert.That(result.TextureAssetPaths, Is.Empty);
            Assert.That(result.TextureCopyPolicy, Is.EqualTo(MmdPmxPrefabProvenance.TextureCopyPolicy));
            Assert.That(result.PmxSourceId, Is.EqualTo(TempPmxPath));
            Assert.That(result.VertexCount, Is.GreaterThan(0));
            Assert.That(result.MaterialCount, Is.EqualTo(result.MaterialAssetPaths.Length));
            Assert.That(activeScene.isDirty, Is.EqualTo(wasDirty));

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TempPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var provenance = prefab.GetComponent<MmdPmxPrefabProvenance>();
            Assert.That(provenance, Is.Not.Null);
            Assert.That(provenance.SourceAsset, Is.SameAs(pmxAsset));
            Assert.That(provenance.SourceId, Is.EqualTo(TempPmxPath));
            Assert.That(provenance.PrefabAssetPath, Is.EqualTo(TempPrefabPath));
            Assert.That(provenance.MeshAssetPaths, Is.EqualTo(result.MeshAssetPaths));
            Assert.That(provenance.MaterialAssetPaths, Is.EqualTo(result.MaterialAssetPaths));
            Assert.That(provenance.TextureAssetPaths, Is.Empty);
            Assert.That(provenance.CurrentExportOperationPolicy, Is.EqualTo(MmdPmxPrefabProvenance.ExportOperationPolicy));

            Mesh meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(result.MeshAssetPaths[0]);
            Assert.That(meshAsset, Is.Not.Null);
            Assert.That(AssetDatabase.Contains(meshAsset), Is.True);
            foreach (string materialAssetPath in result.MaterialAssetPaths)
            {
                Material materialAsset = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
                Assert.That(materialAsset, Is.Not.Null, materialAssetPath);
                Assert.That(AssetDatabase.Contains(materialAsset), Is.True, materialAssetPath);
            }

            GameObject loadedPrefab = PrefabUtility.LoadPrefabContents(TempPrefabPath);
            try
            {
                MmdUnityPlaybackController controller = loadedPrefab.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
                AssertNoMissingScripts(loadedPrefab);

                var renderer = loadedPrefab.GetComponentInChildren<Renderer>(includeInactive: true);
                Assert.That(renderer, Is.Not.Null);
                var skinned = renderer as SkinnedMeshRenderer;
                var meshFilter = renderer.GetComponent<MeshFilter>();
                Mesh? prefabMesh = skinned != null ? skinned.sharedMesh : meshFilter != null ? meshFilter.sharedMesh : null;
                Assert.That(prefabMesh, Is.SameAs(meshAsset));
                Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(result.MaterialAssetPaths.Length));
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.That(AssetDatabase.Contains(material), Is.True);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(loadedPrefab);
            }
        }
        [Test]
        public void PmxPrefabExporterFeedbackPathCreatesPrefabAndSelectsIt()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);

            MmdPmxPrefabExportResult? result = MmdPmxPrefabExporter.CreatePrefabWithFeedback(pmxAsset);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.PrefabAssetPath, Does.EndWith(".prefab"));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabAssetPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(Selection.activeObject, Is.SameAs(prefab));
        }
        [Test]
        public void PmxPrefabExporterRejectsTraversalPathBeforeCreatingDirectories()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            string outsideDirectoryName = "__MmdPrefabTraversalShouldNotExist_" + System.Guid.NewGuid().ToString("N");
            string traversalPrefabPath = "Assets/../" + outsideDirectoryName + "/model.prefab";

            Assert.Throws<System.ArgumentException>(
                () => MmdPmxPrefabExporter.CreatePrefab(pmxAsset, traversalPrefabPath));
            Assert.That(Directory.Exists(Path.Combine(ProjectRoot, outsideDirectoryName)), Is.False);
        }
        [Test]
        public void ImportedPmxAssetReportsReadyForAllReadinessFields()
        {
            // The existing test_1bone_cube fixture produces an importer-owned hierarchy
            // with SkinnedMeshRenderer + bones. All three readiness categories must be Ready.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(pmxAsset, Is.Not.Null);

            Assert.That(pmxAsset.HierarchyReadiness, Is.EqualTo(MmdImportReadiness.Ready),
                "ImportedRoot exists with children -> Ready");
            Assert.That(pmxAsset.RendererReadiness, Is.EqualTo(MmdImportReadiness.Ready),
                "SkinnedMeshRenderer exists with sharedMesh -> Ready");
            Assert.That(pmxAsset.BoneBindingReadiness, Is.EqualTo(MmdImportReadiness.Ready),
                "renderer.bones has no nulls, count matches -> Ready");

            Assert.That(pmxAsset.HierarchyReadinessDiagnostic, Does.Contain("ImportedRoot"),
                "Hierarchy diagnostic should mention ImportedRoot");
            Assert.That(pmxAsset.RendererReadinessDiagnostic, Does.Contain("SkinnedMeshRenderer"),
                "Renderer diagnostic should mention SkinnedMeshRenderer");
            Assert.That(pmxAsset.BoneBindingReadinessDiagnostic, Does.Contain("bone(s)"),
                "Bone binding diagnostic should mention bone count");

            // Verify reimport preserves readiness
            var importer = AssetImporter.GetAtPath(TempPmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);
            importer!.SaveAndReimport();

            MmdPmxAsset pmxAssetAfter = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(pmxAssetAfter.HierarchyReadiness, Is.EqualTo(MmdImportReadiness.Ready));
            Assert.That(pmxAssetAfter.RendererReadiness, Is.EqualTo(MmdImportReadiness.Ready));
            Assert.That(pmxAssetAfter.BoneBindingReadiness, Is.EqualTo(MmdImportReadiness.Ready));
        }
        [Test]
        public void NewUninitializedMmdPmxAssetReportsNotEvaluated()
        {
            // A fresh ScriptableObject (not initialized via Initialize) must report NotEvaluated.
            var asset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                Assert.That(asset.HierarchyReadiness, Is.EqualTo(MmdImportReadiness.NotEvaluated));
                Assert.That(asset.RendererReadiness, Is.EqualTo(MmdImportReadiness.NotEvaluated));
                Assert.That(asset.BoneBindingReadiness, Is.EqualTo(MmdImportReadiness.NotEvaluated));
                Assert.That(asset.HierarchyReadinessDiagnostic, Is.EqualTo(string.Empty));
                Assert.That(asset.RendererReadinessDiagnostic, Is.EqualTo(string.Empty));
                Assert.That(asset.BoneBindingReadinessDiagnostic, Is.EqualTo(string.Empty));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
        [Test]
        public void ComputeReadinessDetectsNullBonesInRenderer()
        {
            // Simulate a hierarchy with a SkinnedMeshRenderer that has a null bone.
            var root = new GameObject("RootWithNullBone");
            try
            {
                var modelObject = new GameObject("Model");
                modelObject.transform.SetParent(root.transform, worldPositionStays: false);
                SkinnedMeshRenderer smr = modelObject.AddComponent<SkinnedMeshRenderer>();
                var mesh = new Mesh();
                mesh.vertices = new Vector3[3];
                mesh.bindposes = new Matrix4x4[2];
                smr.sharedMesh = mesh;
                // Set bones with one null
                smr.bones = new Transform?[] { new GameObject("BoneA").transform, null };

                MmdPmxAsset.ComputeHierarchyReadiness(
                    root,
                    assetBoneCount: 2,
                    out MmdImportReadiness hierarchy,
                    out MmdImportReadiness renderer,
                    out MmdImportReadiness binding,
                    out string _, out string _, out string bindingDiag);

                Assert.That(binding, Is.EqualTo(MmdImportReadiness.Blocked));
                Assert.That(bindingDiag, Does.Contain("null bone(s)"));

                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(smr.bones[0].gameObject);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
        [Test]
        public void ComputeReadinessDetectsBoneCountMismatch()
        {
            // Simulate a hierarchy where renderer.bones length != asset bone count.
            var root = new GameObject("RootBoneMismatch");
            try
            {
                var modelObject = new GameObject("Model");
                modelObject.transform.SetParent(root.transform, worldPositionStays: false);
                SkinnedMeshRenderer smr = modelObject.AddComponent<SkinnedMeshRenderer>();
                var mesh = new Mesh();
                mesh.vertices = new Vector3[3];
                mesh.bindposes = new Matrix4x4[1]; // 1 bindpose
                smr.sharedMesh = mesh;
                smr.bones = new Transform[] { new GameObject("Bone0").transform };

                MmdPmxAsset.ComputeHierarchyReadiness(
                    root,
                    assetBoneCount: 3, // asset says 3 bones, but only 1 in renderer
                    out MmdImportReadiness hierarchy,
                    out MmdImportReadiness renderer,
                    out MmdImportReadiness binding,
                    out string _, out string _, out string bindingDiag);

                Assert.That(binding, Is.EqualTo(MmdImportReadiness.Blocked));
                Assert.That(bindingDiag, Does.Contain("Bone count mismatch"));

                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(smr.bones[0].gameObject);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
