#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Rendering;
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
    }
}
