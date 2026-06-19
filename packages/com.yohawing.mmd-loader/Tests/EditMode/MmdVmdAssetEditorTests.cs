#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd;
using Mmd.Editor;
using Mmd.Parser;

namespace Mmd.Tests
{
    /// <summary>
    /// Focused EditMode tests for MmdVmdAssetEditor helper behavior.
    /// Tests call internal static helpers (GetVmdMotionSummary, GetVmdStructuralDiagnostics,
    /// GetVmdTimelineReadiness) rather than relying on GUI text capture.
    /// </summary>
    public sealed class MmdVmdAssetEditorTests
    {
        private const string FixtureVmdPath =
            "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube_motion.vmd";

        // -- Imports from Project fixture ---------------------------------------------------------

        [Test]
        public void GetVmdMotionSummary_WithImportedVmd_ReturnsExpectedValues()
        {
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            Assert.That(vmdAsset, Is.Not.Null);
            Assert.That(vmdAsset.ByteLength, Is.GreaterThan(0));

            MmdVmdMotionSummary summary = MmdAssetInspectorUtility.GetVmdMotionSummary(vmdAsset);

            // The fixture VMD targets a specific model and contains bone keyframes.
            Assert.That(summary.TargetModelName, Is.Not.Null);
            Assert.That(summary.MaxFrame, Is.GreaterThanOrEqualTo(0));
            Assert.That(summary.BoneKeyframeCount, Is.GreaterThan(0),
                "Fixture VMD is expected to contain bone keyframes.");
            Assert.That(summary.MorphKeyframeCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(summary.ModelKeyframeCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(summary.ConstraintStateCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(summary.CameraKeyframeCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(summary.LightKeyframeCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(summary.SelfShadowKeyframeCount, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void GetVmdStructuralDiagnostics_WithValidImportedVmd_ReturnsEmpty()
        {
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            Assert.That(vmdAsset, Is.Not.Null);

            IReadOnlyList<string> diagnostics =
                MmdAssetInspectorUtility.GetVmdStructuralDiagnostics(vmdAsset);

            Assert.That(diagnostics, Is.Not.Null);
            Assert.That(diagnostics.Count, Is.EqualTo(0),
                "A valid imported VMD fixture should produce no structural errors.");
        }

        // -- Timeline readiness (asset-backed, cache only) ---------------------------------------

        [Test]
        public void GetVmdTimelineReadiness_WithNullOrEmptyAsset_ReturnsSafeDefaults()
        {
            MmdVmdTimelineReadiness r0 = MmdAssetInspectorUtility.GetVmdTimelineReadiness(null);
            Assert.That(r0.MaxFrame, Is.EqualTo(0));
            Assert.That(r0.HasSceneMotion, Is.False);
            Assert.That(r0.ClipCreationRequirement, Does.Contain("PMX"));
            Assert.That(r0.SceneMotionStatus, Does.Contain("No VMD"));

            MmdVmdAsset empty = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                MmdVmdTimelineReadiness r1 = MmdAssetInspectorUtility.GetVmdTimelineReadiness(empty);
                Assert.That(r1.MaxFrame, Is.EqualTo(0));
                Assert.That(r1.ClipCreationRequirement, Does.Contain("PMX"));
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }

        [Test]
        public void GetVmdTimelineReadiness_WithInjectedSummary_ReadsCacheOnly_NoLoadMotion()
        {
            // Garbage bytes + injected summary: if helper called LoadMotion it would throw / corrupt.
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                byte[] garbage = new byte[] { 0x00, 0xDE, 0xAD, 0xBE, 0xEF, 0x42 };
                // scene counts all zero
                var injected = new MmdVmdParseSummary("scene-zero-model", 77, 5, 1, 0, 0, 0, 0, 0);
                asset.Initialize(garbage, "zero.vmd", "zero.vmd", injected, System.Array.Empty<string>());

                MmdVmdTimelineReadiness r = MmdAssetInspectorUtility.GetVmdTimelineReadiness(asset);

                Assert.That(r.MaxFrame, Is.EqualTo(77), "Duration must come from cached MaxFrame.");
                Assert.That(r.CameraKeyframeCount, Is.EqualTo(0));
                Assert.That(r.LightKeyframeCount, Is.EqualTo(0));
                Assert.That(r.SelfShadowKeyframeCount, Is.EqualTo(0));
                Assert.That(r.HasSceneMotion, Is.False);
                Assert.That(r.SceneMotionStatus, Does.Contain("None (model motion only)"));
                Assert.That(r.ClipCreationRequirement, Does.Contain("PMX"));
                Assert.That(r.ClipDurationSource, Does.Contain("77"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void GetVmdTimelineReadiness_WithInjectedSceneMotionCounts_DerivesPresenceFromCache()
        {
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                byte[] garbage = new byte[] { 0xFF, 0xCA, 0xFE };
                // camera>0, light>0, self>0 -> HasSceneMotion true, status "Present"
                var injected = new MmdVmdParseSummary("scene-present-model", 250, 120, 10, 2, 5, 4, 1, 7);
                asset.Initialize(garbage, "scene.vmd", "scene.vmd", injected, System.Array.Empty<string>());

                MmdVmdTimelineReadiness r = MmdAssetInspectorUtility.GetVmdTimelineReadiness(asset);

                Assert.That(r.MaxFrame, Is.EqualTo(250));
                Assert.That(r.CameraKeyframeCount, Is.EqualTo(4));
                Assert.That(r.LightKeyframeCount, Is.EqualTo(1));
                Assert.That(r.SelfShadowKeyframeCount, Is.EqualTo(7));
                Assert.That(r.HasSceneMotion, Is.True);
                Assert.That(r.SceneMotionStatus, Does.Contain("Present (camera:4, light:1, selfShadow:7)"));
                Assert.That(r.ClipDurationSource, Does.Contain("Cached VMD MaxFrame"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void GetVmdTimelineReadiness_LightOnlyCountsAsCameraTrackMotion()
        {
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                byte[] garbage = new byte[] { 0x6C, 0x69, 0x67, 0x68, 0x74 };
                var injected = new MmdVmdParseSummary("light-only", 120, 0, 0, 0, 0, 0, 3, 0);
                asset.Initialize(garbage, "light-only.vmd", "light-only.vmd", injected, System.Array.Empty<string>());

                MmdVmdTimelineReadiness r = MmdAssetInspectorUtility.GetVmdTimelineReadiness(asset);

                Assert.That(r.CameraKeyframeCount, Is.EqualTo(0));
                Assert.That(r.LightKeyframeCount, Is.EqualTo(3));
                Assert.That(r.SelfShadowKeyframeCount, Is.EqualTo(0));
                Assert.That(r.HasSceneMotion, Is.True);
                Assert.That(r.SceneMotionStatus, Does.Contain("Present (camera:0, light:3, selfShadow:0)"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void GetVmdTimelineReadiness_SelfShadowOnlyIsNotCameraTrackMotionYet()
        {
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                byte[] garbage = new byte[] { 0x73, 0x68, 0x64 };
                var injected = new MmdVmdParseSummary("self-shadow-only", 90, 0, 0, 0, 0, 0, 0, 4);
                asset.Initialize(garbage, "self-shadow-only.vmd", "self-shadow-only.vmd", injected, System.Array.Empty<string>());

                MmdVmdTimelineReadiness r = MmdAssetInspectorUtility.GetVmdTimelineReadiness(asset);

                Assert.That(r.CameraKeyframeCount, Is.EqualTo(0));
                Assert.That(r.LightKeyframeCount, Is.EqualTo(0));
                Assert.That(r.SelfShadowKeyframeCount, Is.EqualTo(4));
                Assert.That(r.HasSceneMotion, Is.False);
                Assert.That(r.SceneMotionStatus, Does.Contain("selfShadow:4 deferred"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        // -- Edge cases: in-memory asset with no bytes --------------------------------------------

        [Test]
        public void GetVmdMotionSummary_WithEmptyAsset_ThrowsInvalidOperationException()
        {
            MmdVmdAsset emptyAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                Assert.That(() => MmdAssetInspectorUtility.GetVmdMotionSummary(emptyAsset),
                    Throws.InvalidOperationException,
                    "LoadMotion on an asset with no bytes should throw.");
            }
            finally
            {
                Object.DestroyImmediate(emptyAsset);
            }
        }

        [Test]
        public void GetVmdStructuralDiagnostics_WithEmptyAsset_ThrowsInvalidOperationException()
        {
            MmdVmdAsset emptyAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                Assert.That(() => MmdAssetInspectorUtility.GetVmdStructuralDiagnostics(emptyAsset),
                    Throws.InvalidOperationException,
                    "LoadMotion on an asset with no bytes should throw.");
            }
            finally
            {
                Object.DestroyImmediate(emptyAsset);
            }
        }

        // -- MmdVmdMotionSummary struct member validation -----------------------------------------

        [Test]
        public void MmdVmdMotionSummary_DefaultsToZeroAndEmpty()
        {
            var summary = new MmdVmdMotionSummary(
                targetModelName: null!,
                maxFrame: -1,
                boneKeyframeCount: 0,
                morphKeyframeCount: 0,
                modelKeyframeCount: 0,
                constraintStateCount: 0,
                cameraKeyframeCount: -10,
                lightKeyframeCount: -1,
                selfShadowKeyframeCount: -99);

            Assert.That(summary.TargetModelName, Is.Empty,
                "Null model name should be normalized to empty string.");
            Assert.That(summary.MaxFrame, Is.EqualTo(-1));
            Assert.That(summary.BoneKeyframeCount, Is.EqualTo(0));
            Assert.That(summary.MorphKeyframeCount, Is.EqualTo(0));
            Assert.That(summary.ModelKeyframeCount, Is.EqualTo(0));
            Assert.That(summary.ConstraintStateCount, Is.EqualTo(0));
            Assert.That(summary.CameraKeyframeCount, Is.EqualTo(0), "Negative camera count clamped to zero in summary ctor.");
            Assert.That(summary.LightKeyframeCount, Is.EqualTo(0), "Negative light count clamped to zero in summary ctor.");
            Assert.That(summary.SelfShadowKeyframeCount, Is.EqualTo(0), "Negative self-shadow count clamped to zero in summary ctor.");
        }

        // -- Import-cached summary / diagnostics (no parse on select) ----------------------------

        [Test]
        public void ImportedVmdAssetExposesCachedSummaryValues()
        {
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            Assert.That(vmdAsset, Is.Not.Null);
            Assert.That(vmdAsset.ByteLength, Is.GreaterThan(0));

            Assert.That(vmdAsset.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.Passed),
                "Normal import must populate Passed cached summary.");
            Assert.That(vmdAsset.TargetModelName, Is.Not.Null);
            Assert.That(vmdAsset.MaxFrame, Is.GreaterThanOrEqualTo(0));
            Assert.That(vmdAsset.BoneKeyframeCount, Is.GreaterThan(0),
                "Fixture VMD is expected to contain bone keyframes in cached summary.");
            Assert.That(vmdAsset.MorphKeyframeCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(vmdAsset.ModelKeyframeCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(vmdAsset.ConstraintStateCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(vmdAsset.CameraKeyframeCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(vmdAsset.LightKeyframeCount, Is.GreaterThanOrEqualTo(0));
            Assert.That(vmdAsset.SelfShadowKeyframeCount, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void ImportedVmdAssetExposesCachedStructuralDiagnostics()
        {
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            Assert.That(vmdAsset, Is.Not.Null);

            IReadOnlyList<string> cached = vmdAsset.StructuralDiagnostics;

            Assert.That(cached, Is.Not.Null);
            Assert.That(cached.Count, Is.EqualTo(0),
                "Valid imported VMD must carry empty cached structural diagnostics from import.");
        }

        [Test]
        public void GetVmdMotionSummary_ReadsInjectedCachedSummaryWithoutParsingBytes()
        {
            // Use invalid (non-VMD) bytes + explicitly injected cached summary/diagnostics.
            // If GetVmdMotionSummary (or Draw path) called LoadMotion it would throw on garbage bytes.
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                byte[] garbageBytes = new byte[] { 0x00, 0xDE, 0xAD, 0xBE, 0xEF };
                var injectedSummary = new MmdVmdParseSummary("injected-model", 123, 7, 2, 1, 4, 5, 0, 3);
                var injectedDiags = new[] { "cached structural from import-time (parse-failure case)" };

                asset.Initialize(garbageBytes, "injected.vmd", "injected.vmd", injectedSummary, injectedDiags);

                // Summary helper must surface injected cache; status and diags also cached.
                MmdVmdMotionSummary got = MmdAssetInspectorUtility.GetVmdMotionSummary(asset);
                Assert.That(got.TargetModelName, Is.EqualTo("injected-model"));
                Assert.That(got.MaxFrame, Is.EqualTo(123));
                Assert.That(got.BoneKeyframeCount, Is.EqualTo(7));
                Assert.That(got.MorphKeyframeCount, Is.EqualTo(2));
                Assert.That(got.ModelKeyframeCount, Is.EqualTo(1));
                Assert.That(got.ConstraintStateCount, Is.EqualTo(4));
                Assert.That(got.CameraKeyframeCount, Is.EqualTo(5));
                Assert.That(got.LightKeyframeCount, Is.EqualTo(0));
                Assert.That(got.SelfShadowKeyframeCount, Is.EqualTo(3));

                MmdMotionDefinition nativeHeader = asset.CreateNativeClipMotionHeader();
                Assert.That(nativeHeader.targetModelName, Is.EqualTo("injected-model"));
                Assert.That(nativeHeader.maxFrame, Is.EqualTo(123));
                Assert.That(nativeHeader.boneKeyframes, Is.Empty);
                Assert.That(nativeHeader.morphKeyframes, Is.Empty);
                Assert.That(nativeHeader.modelKeyframes, Is.Empty);

                Assert.That(asset.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.Passed));
                Assert.That(asset.StructuralDiagnostics, Is.Not.Null);
                Assert.That(asset.StructuralDiagnostics.Count, Is.EqualTo(1));
                Assert.That(asset.StructuralDiagnostics[0], Does.Contain("cached structural"));

                // Cached diags readable directly from asset without invoking explicit Get helper (which parses).
                IReadOnlyList<string> directCached = asset.StructuralDiagnostics;
                Assert.That(directCached.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void GetVmdStructuralDiagnostics_WithValidImportedVmd_ReturnsEmpty_ExplicitPath()
        {
            // The explicit diagnostics helper (used by "Run VMD Diagnostics" and some tests)
            // is still allowed to parse and must continue to report empty for valid fixture.
            MmdVmdAsset? vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(FixtureVmdPath);
            Assert.That(vmdAsset, Is.Not.Null);

            IReadOnlyList<string> diagnostics =
                MmdAssetInspectorUtility.GetVmdStructuralDiagnostics(vmdAsset);

            Assert.That(diagnostics, Is.Not.Null);
            Assert.That(diagnostics.Count, Is.EqualTo(0),
                "Explicit GetVmdStructuralDiagnostics on valid imported fixture must report empty.");
        }

        // --- VMD Inspector readiness helpers (cache-only, no LoadMotion, no global Selection) ---

        [Test]
        public void ComputeHumanoidClipReadinessForVmd_MissingPmxAndSetup_ReturnsNotReadyWithActionableDiags()
        {
            MmdVmdAsset vmd = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                // Valid cache but missing pmx/setup -> planner reports nulls, UI helper surfaces short diags.
                byte[] bytes = new byte[] { 0x10 };
                var summary = new MmdVmdParseSummary("t", 100, 10, 0, 0, 0, 0, 0, 0);
                vmd.Initialize(bytes, "t.vmd", "t.vmd", summary, System.Array.Empty<string>());

                MmdHumanoidClipConversionPlan plan =
                    MmdAssetInspectorUtility.ComputeHumanoidClipReadinessForVmd(vmd, pmxAsset: null, setupAsset: null);

                Assert.That(plan.PrerequisitesReady, Is.False);
                Assert.That(plan.CanCreateClipNow, Is.False);
                string joined = string.Join("\n", plan.Diagnostics);
                Assert.That(joined, Does.Contain("pmx asset is null").Or.Contain("humanoid setup asset is null"));
            }
            finally
            {
                Object.DestroyImmediate(vmd);
            }
        }

        [Test]
        public void ComputeAndFormatVmdHumanoidReadiness_ReadySyntheticInputs_SucceedsWithoutLoadMotion()
        {
            MmdVmdAsset vmd = ScriptableObject.CreateInstance<MmdVmdAsset>();
            MmdPmxAsset pmx = null!;
            MmdHumanoidSetupAsset setup = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            var owned = new List<UnityEngine.Object> { vmd, setup };
            GameObject? root = null;
            Mesh? mesh = null;

            try
            {
                // Garbage bytes + good injected cache for VMD (proves no LoadMotion attempted in readiness path).
                byte[] garbage = new byte[] { 0xDE, 0xAD };
                var goodVmdSummary = new MmdVmdParseSummary("synth-vmd", 77, 33, 2, 1, 0, 0, 0, 0);
                vmd.Initialize(garbage, "synth.vmd", "synth.vmd", goodVmdSummary, System.Array.Empty<string>());

                CreateSyntheticReadyPmxAndSetup(out pmx, setup, out root, out mesh, owned);

                // Call the inspector-facing helper (testable, no IMGUI, delegates to planner cache path).
                int clipCountBefore = AssetDatabase.FindAssets("t:AnimationClip").Length;

                MmdHumanoidClipConversionPlan plan =
                    MmdAssetInspectorUtility.ComputeHumanoidClipReadinessForVmd(vmd, pmx, setup);

                int clipCountAfterFirst = AssetDatabase.FindAssets("t:AnimationClip").Length;
                Assert.That(clipCountAfterFirst, Is.EqualTo(clipCountBefore),
                    "VMD Inspector readiness preview (ComputeHumanoidClipReadinessForVmd) must not create AnimationClip assets.");

                Assert.That(plan.PrerequisitesReady, Is.True);
                Assert.That(plan.CanCreateClipNow, Is.True);
                Assert.That(plan.Readiness, Is.EqualTo(MmdHumanoidClipConversionPlanner.ReadyReadiness));
                Assert.That(plan.VmdMaxFrame, Is.EqualTo(77));
                Assert.That(plan.VmdBoneKeyframeCount, Is.EqualTo(33));

                string issues = MmdAssetInspectorUtility.FormatCompactVmdHumanoidIssues(plan);
                Assert.That(issues, Is.Empty, "Ready plan must format to no issues string.");

                // Missing one side still not ready (actionable).
                MmdHumanoidClipConversionPlan partial = MmdAssetInspectorUtility.ComputeHumanoidClipReadinessForVmd(vmd, pmx, null);
                int clipCountAfterPartial = AssetDatabase.FindAssets("t:AnimationClip").Length;
                Assert.That(clipCountAfterPartial, Is.EqualTo(clipCountBefore),
                    "VMD Inspector readiness preview must not create AnimationClip assets even on partial inputs.");

                Assert.That(partial.PrerequisitesReady, Is.False);
                Assert.That(MmdAssetInspectorUtility.FormatCompactVmdHumanoidIssues(partial), Is.Not.Empty);
            }
            finally
            {
                foreach (var o in owned)
                {
                    if (o != null) Object.DestroyImmediate(o);
                }
                if (root != null) Object.DestroyImmediate(root);
                if (mesh != null) Object.DestroyImmediate(mesh);
            }
        }

        // Local synthetic ready builder (duplicated slim from planner contract test to keep edits bounded to allowed files only).
        private static void CreateSyntheticReadyPmxAndSetup(
            out MmdPmxAsset pmxAsset,
            MmdHumanoidSetupAsset setupAsset,
            out GameObject hierarchyRoot,
            out Mesh mesh,
            List<UnityEngine.Object> ownedObjects)
        {
            hierarchyRoot = new GameObject("VmdEdTestRoot");
            var modelObject = new GameObject("Model");
            modelObject.transform.SetParent(hierarchyRoot.transform, worldPositionStays: false);

            SkinnedMeshRenderer smr = modelObject.AddComponent<SkinnedMeshRenderer>();
            mesh = new Mesh { name = "VmdEdTestMesh" };
            mesh.vertices = new Vector3[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0f, 1f, 0.5f) };
            mesh.triangles = new int[] { 0, 1, 2 };
            string[] boneNames = { "下半身", "上半身", "首", "頭", "左足", "左ひざ", "左足首", "右足", "右ひざ", "右足首", "左腕", "左ひじ", "左手首", "右腕", "右ひじ", "右手首" };
            mesh.bindposes = new Matrix4x4[boneNames.Length];
            smr.sharedMesh = mesh;

            Transform[] bones = new Transform[boneNames.Length];
            for (int i = 0; i < boneNames.Length; i++)
            {
                var bgo = new GameObject(boneNames[i]);
                bgo.transform.SetParent(modelObject.transform, worldPositionStays: false);
                bones[i] = bgo.transform;
            }
            smr.bones = bones;

            pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            pmxAsset.Initialize(
                new byte[] { 0xAA },
                "vmd-ed-ready.pmx",
                "Assets/vmd-ed-ready.pmx",
                importedMeshAsset: mesh,
                importedRootAsset: hierarchyRoot,
                hierarchyReadinessValue: MmdImportReadiness.Ready,
                rendererReadinessValue: MmdImportReadiness.Ready,
                boneBindingReadinessValue: MmdImportReadiness.Ready,
                parseSummary: new MmdPmxParseSummary(
                    "vmd-ed-model", 3, 3, boneNames.Length, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                    boundsMin: new Vector3(-0.5f, 0f, -0.5f), boundsMax: new Vector3(0.5f, 1f, 0.5f),
                    materialSummaries: System.Array.Empty<MmdPmxMaterialSummary>()));
            setupAsset.Initialize(pmxAsset);

            ownedObjects.Add(pmxAsset);
            ownedObjects.Add(hierarchyRoot);
            ownedObjects.Add(mesh);
            foreach (var b in bones) ownedObjects.Add(b.gameObject);
        }
    }
}
