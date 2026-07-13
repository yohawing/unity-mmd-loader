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
        public void ImportedPmxAssetCarriesCachedMaterialReferenceSummaryFromImporter()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdModelDefinition model = pmxAsset.LoadModel();

            Assert.That(
                pmxAsset.DiffuseTextureReferenceCount,
                Is.EqualTo(CountMaterials(model, material => !string.IsNullOrWhiteSpace(material.texture))));
            Assert.That(
                pmxAsset.SphereTextureReferenceCount,
                Is.EqualTo(CountMaterials(model, material => !string.IsNullOrWhiteSpace(material.sphereTexture))));
            Assert.That(
                pmxAsset.ToonTextureReferenceCount,
                Is.EqualTo(CountMaterials(model, material => !string.IsNullOrWhiteSpace(material.toonTexture))));
            Assert.That(
                pmxAsset.TransparentMaterialCount,
                Is.EqualTo(CountMaterials(model, material => material.alpha < 1.0f)));
            Assert.That(
                pmxAsset.EdgeMaterialCount,
                Is.EqualTo(CountMaterials(model, material => material.drawEdgeFlag && material.edgeSize > 0.0f)));
            Assert.That(pmxAsset.MaterialSummaries, Has.Length.EqualTo(model.materials.Count));
            Assert.That(pmxAsset.MaterialSummaries[0].index, Is.EqualTo(model.materials[0].index));
            Assert.That(pmxAsset.MaterialSummaries[0].name, Is.EqualTo(model.materials[0].name));
            Assert.That(pmxAsset.MaterialSummaries[0].diffuseTexture, Is.EqualTo(model.materials[0].texture));
            Assert.That(pmxAsset.MaterialSummaries[0].sphereTexture, Is.EqualTo(model.materials[0].sphereTexture));
            Assert.That(pmxAsset.MaterialSummaries[0].toonTexture, Is.EqualTo(model.materials[0].toonTexture));
        }
        [Test]
        public void OutlineReadinessReportsCachedEdgeMaterialReleaseBoundary()
        {
            var pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                pmxAsset.Initialize(
                    new byte[] { 1, 2, 3, 4 },
                    "outline-source",
                    "outline-source.pmx",
                    assetShaderPreset: "MmdBasicUrpToon",
                    parseSummary: new MmdPmxParseSummary(
                        "outline-model",
                        vertexCount: 4,
                        indexCount: 6,
                        boneCount: 1,
                        morphCount: 0,
                        materialCount: 3,
                        diffuseTextureReferenceCount: 0,
                        sphereTextureReferenceCount: 0,
                        toonTextureReferenceCount: 0,
                        transparentMaterialCount: 0,
                        edgeMaterialCount: 2,
                        ikCount: 0,
                        rigidbodyCount: 0,
                        jointCount: 0,
                        boundsMin: Vector3.zero,
                        boundsMax: Vector3.one));

                MmdOutlineReadiness readiness = MmdAssetInspectorUtility.GetOutlineReadiness(pmxAsset);

                Assert.That(readiness.OutlineEligibleMaterialCount, Is.EqualTo(2));
                Assert.That(readiness.RuntimePath, Is.EqualTo("MmdOutlineRendererFeature (LightMode=MmdOutline)"));
                Assert.That(readiness.ReleaseMode, Is.EqualTo("Back-face mesh-normal extrusion"));
                Assert.That(readiness.FinalVisualParity, Is.EqualTo(MmdOutlineReadiness.NotClaimed));
            }
            finally
            {
                Object.DestroyImmediate(pmxAsset);
            }
        }
        [Test]
        public void OutlineReadinessDoesNotClaimRuntimeOutlineWhenNoEdgeMaterialsExist()
        {
            var pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                pmxAsset.Initialize(
                    new byte[] { 1, 2, 3, 4 },
                    "no-outline-source",
                    "no-outline-source.pmx",
                    parseSummary: new MmdPmxParseSummary(
                        "no-outline-model",
                        vertexCount: 4,
                        indexCount: 6,
                        boneCount: 1,
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
                        boundsMin: Vector3.zero,
                        boundsMax: Vector3.one));

                MmdOutlineReadiness readiness = MmdAssetInspectorUtility.GetOutlineReadiness(pmxAsset);

                Assert.That(readiness.OutlineEligibleMaterialCount, Is.EqualTo(0));
                Assert.That(readiness.RuntimePath, Is.EqualTo("No PMX draw-edge materials"));
                Assert.That(readiness.ReleaseMode, Is.EqualTo("Not needed"));
                Assert.That(readiness.FinalVisualParity, Is.EqualTo(MmdOutlineReadiness.NotClaimed));
            }
            finally
            {
                Object.DestroyImmediate(pmxAsset);
            }
        }
        [Test]
        public void SelfShadowRendererSetupReadinessReportsNoUrpAsset()
        {
            MmdSelfShadowRendererSetupReadiness readiness =
                MmdAssetInspectorUtility.EvaluateMmdSelfShadowRendererSetup(null);

            Assert.That(readiness.HasUrpAsset, Is.False);
            Assert.That(readiness.RendererDataCount, Is.EqualTo(0));
            Assert.That(readiness.FeatureCount, Is.EqualTo(0));
            Assert.That(readiness.EnabledFeatureCount, Is.EqualTo(0));
            Assert.That(readiness.FeaturePresentOnAnyRendererData, Is.False);
            Assert.That(readiness.FeatureEnabledOnAnyRendererData, Is.False);
        }
        [Test]
        public void SelfShadowRendererSetupReadinessReportsMissingFeature()
        {
            using var fixture = SelfShadowRendererSetupFixture.Create(includeFeature: false);

            MmdSelfShadowRendererSetupReadiness readiness =
                MmdAssetInspectorUtility.EvaluateMmdSelfShadowRendererSetup(fixture.Pipeline);

            Assert.That(readiness.HasUrpAsset, Is.True);
            Assert.That(readiness.RendererDataCount, Is.EqualTo(1));
            Assert.That(readiness.FeatureCount, Is.EqualTo(0));
            Assert.That(readiness.EnabledFeatureCount, Is.EqualTo(0));
            Assert.That(readiness.FeaturePresentOnAnyRendererData, Is.False);
            Assert.That(readiness.FeatureEnabledOnAnyRendererData, Is.False);
        }
        [Test]
        public void SelfShadowRendererSetupReadinessDistinguishesDisabledFeature()
        {
            using var fixture = SelfShadowRendererSetupFixture.Create(includeFeature: true, featureEnabled: false);

            MmdSelfShadowRendererSetupReadiness readiness =
                MmdAssetInspectorUtility.EvaluateMmdSelfShadowRendererSetup(fixture.Pipeline);

            Assert.That(readiness.HasUrpAsset, Is.True);
            Assert.That(readiness.RendererDataCount, Is.EqualTo(1));
            Assert.That(readiness.FeatureCount, Is.EqualTo(1));
            Assert.That(readiness.EnabledFeatureCount, Is.EqualTo(0));
            Assert.That(readiness.FeaturePresentOnAnyRendererData, Is.True);
            Assert.That(readiness.FeatureEnabledOnAnyRendererData, Is.False);
        }
        [Test]
        public void SelfShadowRendererSetupReadinessReportsEnabledFeature()
        {
            using var fixture = SelfShadowRendererSetupFixture.Create(includeFeature: true, featureEnabled: true);

            MmdSelfShadowRendererSetupReadiness readiness =
                MmdAssetInspectorUtility.EvaluateMmdSelfShadowRendererSetup(fixture.Pipeline);

            Assert.That(readiness.HasUrpAsset, Is.True);
            Assert.That(readiness.RendererDataCount, Is.EqualTo(1));
            Assert.That(readiness.FeatureCount, Is.EqualTo(1));
            Assert.That(readiness.EnabledFeatureCount, Is.EqualTo(1));
            Assert.That(readiness.FeaturePresentOnAnyRendererData, Is.True);
            Assert.That(readiness.FeatureEnabledOnAnyRendererData, Is.True);
        }
        [Test]
        public void SelfShadowRendererSetupReadinessUsesDefaultRendererDataForActiveFeature()
        {
            var pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            var defaultRendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            var unusedRendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            var feature = ScriptableObject.CreateInstance<MmdSelfShadowRendererFeature>();
            var bindingGo = new GameObject("binding");
            try
            {
                feature.SetActive(true);
                AddSelfShadowFeature(unusedRendererData, feature);
                SetRendererDataList(pipeline, 0, defaultRendererData, unusedRendererData);
                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                binding.SelfShadowEnabled = true;

                MmdSelfShadowRendererSetupReadiness readiness =
                    MmdAssetInspectorUtility.EvaluateMmdSelfShadowRendererSetup(pipeline);
                string warning = MmdAssetInspectorUtility.GetSelfShadowRendererSetupWarning(binding, readiness);

                Assert.That(readiness.FeatureEnabledOnAnyRendererData, Is.True);
                Assert.That(readiness.ActiveRendererDataIndex, Is.EqualTo(0));
                Assert.That(readiness.FeatureEnabledOnActiveRendererData, Is.False);
                Assert.That(warning, Does.Contain("MmdSelfShadowRendererFeature"));
                Assert.That(warning, Does.Contain("not configured"));
            }
            finally
            {
                Object.DestroyImmediate(bindingGo);
                Object.DestroyImmediate(feature);
                Object.DestroyImmediate(unusedRendererData);
                Object.DestroyImmediate(defaultRendererData);
                Object.DestroyImmediate(pipeline);
            }
        }
        [Test]
        public void SelfShadowRendererSetupReadinessUsesTargetCameraRendererOverride()
        {
            var pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            var defaultRendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            var cameraRendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            var feature = ScriptableObject.CreateInstance<MmdSelfShadowRendererFeature>();
            var bindingGo = new GameObject("binding");
            var cameraGo = new GameObject("camera");
            try
            {
                feature.SetActive(true);
                AddSelfShadowFeature(defaultRendererData, feature);
                SetRendererDataList(pipeline, 0, defaultRendererData, cameraRendererData);
                Camera camera = cameraGo.AddComponent<Camera>();
                var additionalCameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
                additionalCameraData.SetRenderer(1);
                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                binding.SelfShadowEnabled = true;
                binding.TargetCamera = camera;

                MmdSelfShadowRendererSetupReadiness readiness =
                    MmdAssetInspectorUtility.EvaluateMmdSelfShadowRendererSetup(pipeline, camera);
                string warning = MmdAssetInspectorUtility.GetSelfShadowRendererSetupWarning(binding, readiness);

                Assert.That(readiness.FeatureEnabledOnAnyRendererData, Is.True);
                Assert.That(readiness.ActiveRendererDataIndex, Is.EqualTo(1));
                Assert.That(readiness.FeatureEnabledOnActiveRendererData, Is.False);
                Assert.That(warning, Does.Contain("MmdSelfShadowRendererFeature"));
                Assert.That(warning, Does.Contain("not configured"));
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(bindingGo);
                Object.DestroyImmediate(feature);
                Object.DestroyImmediate(cameraRendererData);
                Object.DestroyImmediate(defaultRendererData);
                Object.DestroyImmediate(pipeline);
            }
        }
        [Test]
        public void ImportedPmxAssetCarriesCachedPhysicsSummaryFromImporter()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdModelDefinition model = pmxAsset.LoadModel();

            // The current repo-safe PMX fixture has no physics descriptors; this pins the importer/cache invariant for that case.
            Assert.That(pmxAsset.RigidbodyCount, Is.EqualTo(model.physics?.rigidbodies?.Count ?? 0));
            Assert.That(pmxAsset.JointCount, Is.EqualTo(model.physics?.joints?.Count ?? 0));
        }
        [Test]
        public void PmxParseSummaryCountsPhysicsDescriptorsFromModel()
        {
            var model = new MmdModelDefinition();
            model.physics.rigidbodies.Add(new MmdRigidbodyDefinition { index = 0, name = "body-a" });
            model.physics.rigidbodies.Add(new MmdRigidbodyDefinition { index = 1, name = "body-b" });
            model.physics.joints.Add(new MmdJointDefinition { index = 0, name = "joint-a" });

            MmdPmxParseSummary summary = MmdPmxParseSummary.FromModel(model);

            Assert.That(summary.RigidbodyCount, Is.EqualTo(2));
            Assert.That(summary.JointCount, Is.EqualTo(1));
        }
        [Test]
        public void PmxParseSummaryCarriesModelCredits()
        {
            var model = new MmdModelDefinition
            {
                name = "credit-model",
                englishName = "credit-model-en",
                comment = "日本語コメント",
                englishComment = "English comment"
            };

            MmdPmxParseSummary summary = MmdPmxParseSummary.FromModel(model);
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                pmxAsset.Initialize(
                    new byte[] { 1, 2, 3 },
                    "credit.pmx",
                    "External/Model/credit.pmx",
                    parseSummary: summary);

                Assert.That(summary.ModelEnglishName, Is.EqualTo("credit-model-en"));
                Assert.That(summary.ModelComment, Is.EqualTo("日本語コメント"));
                Assert.That(summary.ModelEnglishComment, Is.EqualTo("English comment"));
                Assert.That(pmxAsset.ModelEnglishName, Is.EqualTo("credit-model-en"));
                Assert.That(pmxAsset.ModelComment, Is.EqualTo("日本語コメント"));
                Assert.That(pmxAsset.ModelEnglishComment, Is.EqualTo("English comment"));
            }
            finally
            {
                Object.DestroyImmediate(pmxAsset);
            }
        }
        [Test]
        public void PmxParseSummaryCalculatesBoundsFromFiniteVertexPositions()
        {
            var model = new MmdModelDefinition();
            model.vertices.Add(new MmdVertexDefinition { position = new[] { -1.0f, 2.0f, -3.0f } });
            model.vertices.Add(new MmdVertexDefinition { position = new[] { 4.0f, -5.0f, 6.0f } });
            model.vertices.Add(new MmdVertexDefinition { position = new[] { float.NaN, 1.0f, 2.0f } });

            MmdPmxParseSummary summary = MmdPmxParseSummary.FromModel(model);

            AssertVector3(summary.BoundsMin, new Vector3(-1.0f, -5.0f, -3.0f));
            AssertVector3(summary.BoundsMax, new Vector3(4.0f, 2.0f, 6.0f));
            AssertVector3(summary.BoundsSize, new Vector3(5.0f, 7.0f, 9.0f));
        }
        [Test]
        public void PmxParseSummaryReturnsZeroBoundsWhenAllVertexPositionsAreNonFinite()
        {
            var model = new MmdModelDefinition();
            model.vertices.Add(new MmdVertexDefinition { position = new[] { float.NaN, 1.0f, 2.0f } });
            model.vertices.Add(new MmdVertexDefinition { position = new[] { float.PositiveInfinity, 0.0f, 0.0f } });
            model.vertices.Add(new MmdVertexDefinition { position = Array.Empty<float>() });

            MmdPmxParseSummary summary = MmdPmxParseSummary.FromModel(model);

            Assert.That(summary.VertexCount, Is.EqualTo(3));
            AssertVector3(summary.BoundsMin, Vector3.zero);
            AssertVector3(summary.BoundsMax, Vector3.zero);
            AssertVector3(summary.BoundsSize, Vector3.zero);
        }
        [Test]
        public void AnimationTimelineReadinessReportsBindingTargetStateWithoutLoadingPmx()
        {
            MmdPmxAsset readyAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdPmxAsset emptyAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                readyAsset.Initialize(new byte[] { 1, 2, 3 }, "ready.pmx", "External/Model/ready.pmx");

                MmdAnimationTimelineReadiness ready = MmdAssetInspectorUtility.GetAnimationTimelineReadiness(readyAsset);
                MmdAnimationTimelineReadiness unavailable = MmdAssetInspectorUtility.GetAnimationTimelineReadiness(emptyAsset);

                Assert.That(ready.TimelineBindingTarget, Is.EqualTo("Ready after Load PMX Into Scene"));
                Assert.That(ready.VmdDropReadiness, Is.EqualTo("Requires generated scene controller"));
                Assert.That(ready.PlaybackSource, Is.EqualTo("Scene component or Timeline clip, not PMX import side effect"));
                Assert.That(unavailable.TimelineBindingTarget, Is.EqualTo("Unavailable"));
                Assert.That(unavailable.VmdDropReadiness, Is.EqualTo("Unavailable"));
            }
            finally
            {
                Object.DestroyImmediate(readyAsset);
                Object.DestroyImmediate(emptyAsset);
            }
        }
    }
}
