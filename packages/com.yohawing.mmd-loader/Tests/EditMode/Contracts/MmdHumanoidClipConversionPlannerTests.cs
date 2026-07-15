#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Editor;

namespace Mmd.Tests
{
    public sealed class MmdHumanoidClipConversionPlannerTests
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
        public void AnalyzePrerequisitesReturnsFailureForNullInputs()
        {
            MmdHumanoidClipConversionPlan result =
                MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(
                    pmxAsset: null,
                    vmdAsset: null,
                    setupAsset: null);

            Assert.That(result.PrerequisitesReady, Is.False);
            Assert.That(result.CanCreateClipNow, Is.False);
            Assert.That(result.Readiness, Is.EqualTo(MmdHumanoidClipConversionPlanner.NotReadyReadiness));
            Assert.That(result.Diagnostics, Is.Not.Empty);
            Assert.That(string.Join("\n", result.Diagnostics), Does.Contain("pmx asset is null"));
            Assert.That(string.Join("\n", result.Diagnostics), Does.Contain("vmd asset is null"));
        }

        [Test]
        public void AnalyzePrerequisitesWithoutSetupRejectsGenericPmxWithActionableDiagnostic()
        {
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(FixturePmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(vmdAsset, Is.Not.Null);

            MmdHumanoidClipConversionPlan result =
                MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, vmdAsset, setupAsset: null);

            Assert.That(result.PrerequisitesReady, Is.False);
            Assert.That(result.CanCreateClipNow, Is.False);
            Assert.That(
                string.Join("\n", result.Diagnostics),
                Does.Contain("AnimationType").And.Contain("Reimport the PMX"));
        }

        [Test]
        public void AnalyzePrerequisitesFailsWhenSetupPmxAssetMismatchesInputPmx()
        {
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(FixturePmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(vmdAsset, Is.Not.Null);

            var otherPmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            otherPmxAsset.Initialize(new byte[] { 0x01, 0x02, 0x03 }, "synthetic-mismatch.pmx", "synthetic mismatch path");
            var setupAsset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();

            var objects = new List<UnityEngine.Object> { otherPmxAsset, setupAsset };
            try
            {
                setupAsset.Initialize(otherPmxAsset);
                MmdHumanoidClipConversionPlan result =
                    MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(
                        pmxAsset,
                        vmdAsset,
                        setupAsset);

                Assert.That(result.PrerequisitesReady, Is.False);
                Assert.That(result.CanCreateClipNow, Is.False);
                Assert.That(result.Readiness, Is.EqualTo(MmdHumanoidClipConversionPlanner.NotReadyReadiness));
                Assert.That(string.Join("\n", result.Diagnostics), Does.Contain("setup.PmxAsset mismatch"));
            }
            finally
            {
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
        public void AnalyzePrerequisitesFailsWhenSetupMappingIsNotReady()
        {
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(FixturePmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(vmdAsset, Is.Not.Null);

            var setupAsset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            try
            {
                setupAsset.Initialize(pmxAsset);

                MmdHumanoidClipConversionPlan result = MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(
                    pmxAsset,
                    vmdAsset,
                    setupAsset);

                Assert.That(result.PrerequisitesReady, Is.False);
                Assert.That(result.CanCreateClipNow, Is.False);
                Assert.That(result.Readiness, Is.EqualTo(MmdHumanoidClipConversionPlanner.NotReadyReadiness));
                Assert.That(string.Join("\n", result.Diagnostics), Does.Contain("mapping not ready"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(setupAsset);
            }
        }

        [Test]
        public void AnalyzePrerequisitesReturnsReadyForReadySetupWithFixtureVmd()
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            var setupAsset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            string[]? clipGuidsBefore = null;
            string[]? clipGuidsAfter = null;
            var ownedObjects = new List<UnityEngine.Object>();

            try
            {
                CreateReadyFixturePmxAndSetup(out pmxAsset, setupAsset, ownedObjects);
                Assert.That(vmdAsset, Is.Not.Null);
                Assert.That(pmxAsset, Is.Not.Null);

                clipGuidsBefore = AssetDatabase.FindAssets("t:AnimationClip");

                MmdHumanoidClipConversionPlan result = MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(
                    pmxAsset,
                    vmdAsset,
                    setupAsset);

                clipGuidsAfter = AssetDatabase.FindAssets("t:AnimationClip");

                Assert.That(result.PrerequisitesReady, Is.True);
                Assert.That(result.CanCreateClipNow, Is.True);
                Assert.That(result.Readiness, Is.EqualTo(MmdHumanoidClipConversionPlanner.ReadyReadiness));
                Assert.That(result.SetupPmxAssetMatch, Is.True);
                Assert.That(result.SetupMappingReadiness, Is.EqualTo(MmdHumanoidSetupAsset.ReadyReadiness));
                Assert.That(result.SetupMappingInputSource, Is.EqualTo(MmdHumanoidSetupAsset.ImportedHierarchyInputSource));
                Assert.That(result.PmxSourceId, Is.EqualTo(pmxAsset.SourceId));
                Assert.That(result.VmdSourceId, Is.EqualTo(vmdAsset!.SourceId));
                Assert.That(result.SetupSourceId, Is.EqualTo(pmxAsset.SourceId));
                Assert.That(result.PmxBoneCount, Is.EqualTo(RequiredBoneNames.Length));
                Assert.That(result.VmdBoneKeyframeCount, Is.GreaterThan(0));
                Assert.That(string.Join("\n", result.Diagnostics), Does.Contain("writer-status"));
                Assert.That(
                    clipGuidsBefore.Length,
                    Is.EqualTo(clipGuidsAfter.Length),
                    "AnalyzePrerequisites must not create or write AnimationClip assets in this slice.");
            }
            finally
            {
                if (setupAsset != null)
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

        private static void CreateReadyFixturePmxAndSetup(
            out MmdPmxAsset pmxAsset,
            MmdHumanoidSetupAsset setupAsset,
            List<UnityEngine.Object> ownedObjects)
        {
            var hierarchyRoot = new GameObject("H6ReadyPmxRoot");
            var modelObject = new GameObject("ReadyModel");
            modelObject.transform.SetParent(hierarchyRoot.transform, worldPositionStays: false);

            SkinnedMeshRenderer smr = modelObject.AddComponent<SkinnedMeshRenderer>();
            Mesh mesh = new Mesh
            {
                name = "H6ReadyPmxMesh",
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
                "ready-h6-slice0.pmx",
                Path.Combine("Assets", "ready-h6-slice0.pmx"),
                importedMeshAsset: mesh,
                importedRootAsset: hierarchyRoot,
                hierarchyReadinessValue: MmdImportReadiness.Ready,
                rendererReadinessValue: MmdImportReadiness.Ready,
                boneBindingReadinessValue: MmdImportReadiness.Ready,
                parseSummary: new MmdPmxParseSummary(
                    "ready-h6-model",
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

        // --- Cache-driven VMD analysis (no LoadMotion during AnalyzePrerequisites) -------------

        [Test]
        public void AnalyzePrerequisites_WithInvalidBytesButInjectedGoodCache_CanBecomeReadyWithSyntheticPmxSetup()
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            var setupAsset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            var ownedObjects = new List<UnityEngine.Object> { vmdAsset, setupAsset };

            try
            {
                CreateReadyFixturePmxAndSetup(out pmxAsset, setupAsset, ownedObjects);
                Assert.That(pmxAsset, Is.Not.Null);

                // Invalid (non-VMD) bytes + explicitly injected valid cached summary + empty structural diags.
                // If planner called LoadMotion it would throw; success without exception proves cache-only path.
                byte[] garbageBytes = new byte[] { 0x00, 0xDE, 0xAD, 0xBE, 0xEF, 0x42 };
                var goodSummary = new MmdVmdParseSummary("injected-cache-model", 240, 120, 15, 4, 8);
                var emptyDiags = Array.Empty<string>();
                vmdAsset.Initialize(garbageBytes, "injected-cache.vmd", "injected-cache.vmd", goodSummary, emptyDiags);

                Assert.That(vmdAsset.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.Passed));
                Assert.That(vmdAsset.StructuralDiagnostics.Count, Is.EqualTo(0));

                MmdHumanoidClipConversionPlan result = MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(
                    pmxAsset,
                    vmdAsset,
                    setupAsset);

                Assert.That(result.PrerequisitesReady, Is.True, "Injected good VMD cache + synthetic ready PMX/setup must be ready.");
                Assert.That(result.CanCreateClipNow, Is.True);
                Assert.That(result.Readiness, Is.EqualTo(MmdHumanoidClipConversionPlanner.ReadyReadiness));
                Assert.That(result.VmdMaxFrame, Is.EqualTo(240));
                Assert.That(result.VmdBoneKeyframeCount, Is.EqualTo(120));
                Assert.That(result.VmdMorphKeyframeCount, Is.EqualTo(15));
                Assert.That(result.VmdModelKeyframeCount, Is.EqualTo(4));
                Assert.That(string.Join("\n", result.Diagnostics), Does.Contain("conversion-prerequisites: ready"));
            }
            finally
            {
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
        public void AnalyzePrerequisites_ReturnsNotReady_ForFailedStatusOrNonEmptyCachedStructuralDiagnostics()
        {
            // Use minimal ready PMX/setup (reuse helper) but pair with VMD that has Failed status or diags.
            MmdPmxAsset pmxAsset = null!;
            var setupAsset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            var ownedObjects = new List<UnityEngine.Object> { setupAsset };

            try
            {
                CreateReadyFixturePmxAndSetup(out pmxAsset, setupAsset, ownedObjects);

                // Case 1: Failed status (via diags that trigger Failed in Apply)
                MmdVmdAsset vmdFailed = ScriptableObject.CreateInstance<MmdVmdAsset>();
                ownedObjects.Add(vmdFailed);
                byte[] bytes1 = new byte[] { 0x01 };
                var failSummary = new MmdVmdParseSummary("fail-model", 10, 1, 0, 0, 0);
                var failDiags = new[] { "Failed to parse VMD during import" };
                vmdFailed.Initialize(bytes1, "fail.vmd", "fail.vmd", failSummary, failDiags);
                Assert.That(vmdFailed.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.Failed));

                MmdHumanoidClipConversionPlan r1 = MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, vmdFailed, setupAsset);
                Assert.That(r1.PrerequisitesReady, Is.False);
                Assert.That(r1.CanCreateClipNow, Is.False);
                Assert.That(r1.Readiness, Is.EqualTo(MmdHumanoidClipConversionPlanner.NotReadyReadiness));
                Assert.That(string.Join("\n", r1.Diagnostics), Does.Contain("vmd validation failed").Or.Contain("Failed"));

                // Case 2: Passed but non-empty structural diagnostics (simulated; planner must treat as not ready)
                MmdVmdAsset vmdWithDiags = ScriptableObject.CreateInstance<MmdVmdAsset>();
                ownedObjects.Add(vmdWithDiags);
                byte[] bytes2 = new byte[] { 0x02 };
                var okSummary = new MmdVmdParseSummary("diag-model", 30, 5, 1, 0, 1);
                var structuralDiags = new[] { "structural: duplicate bone keyframe or invalid interp" };
                vmdWithDiags.Initialize(bytes2, "diag.vmd", "diag.vmd", okSummary, structuralDiags);
                // Note: Apply may set Failed if diags[0] contains "Failed to", here it won't; status may be Passed but we still check diags.
                MmdHumanoidClipConversionPlan r2 = MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, vmdWithDiags, setupAsset);
                Assert.That(r2.PrerequisitesReady, Is.False);
                Assert.That(string.Join("\n", r2.Diagnostics), Does.Contain("cached structural diagnostics present"));
            }
            finally
            {
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
        public void AnalyzePrerequisites_ReturnsNotReady_ForNotParsedCache()
        {
            MmdPmxAsset pmxAsset = null!;
            MmdVmdAsset vmdNotParsed = ScriptableObject.CreateInstance<MmdVmdAsset>();
            var setupAsset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            var ownedObjects = new List<UnityEngine.Object> { vmdNotParsed, setupAsset };

            try
            {
                CreateReadyFixturePmxAndSetup(out pmxAsset, setupAsset, ownedObjects);

                // Freshly created asset has NotParsed default, no Initialize call -> cache missing.
                Assert.That(vmdNotParsed.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.NotParsed));

                MmdHumanoidClipConversionPlan result = MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(
                    pmxAsset, vmdNotParsed, setupAsset);

                Assert.That(result.PrerequisitesReady, Is.False);
                Assert.That(result.CanCreateClipNow, Is.False);
                Assert.That(result.Readiness, Is.EqualTo(MmdHumanoidClipConversionPlanner.NotReadyReadiness));
                Assert.That(string.Join("\n", result.Diagnostics), Does.Contain("NotParsed").Or.Contain("cache missing").Or.Contain("Reimport"));
            }
            finally
            {
                foreach (UnityEngine.Object obj in ownedObjects)
                {
                    if (obj != null)
                    {
                        UnityEngine.Object.DestroyImmediate(obj);
                    }
                }
            }
        }
    }
}
