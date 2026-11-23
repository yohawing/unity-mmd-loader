#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Mmd.Parser;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class PmxUnityInstantiationContractTests
    {
        public static IEnumerable<TestCaseData> RenderablePackageModelFixtures()
        {
            foreach (ModelFixtureEntry fixture in MmdTestFixtures.LoadPackageModelFixtures())
            {
                if (fixture.format != "pmx")
                {
                    continue;
                }

                if (fixture.expected.minVertices <= 0 || fixture.expected.minIndices < 3 || fixture.expected.minMaterials <= 0)
                {
                    continue;
                }

                yield return new TestCaseData(fixture).SetName("PMX Unity instantiation fixture " + fixture.id);
            }
        }

        [TestCaseSource(nameof(RenderablePackageModelFixtures))]
        public void RenderablePmxFixtureInstantiatesStaticUnityModel(ModelFixtureEntry fixture)
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = MmdTestFixtures.ParseModel(fixture);

                instance = MmdUnityModelFactory.CreateStaticModel(model);

                Assert.That(instance.Root, Is.Not.Null, fixture.Context("Root"));
                Assert.That(instance.Root.GetComponent<MeshFilter>(), Is.Null, fixture.Context("Root MeshFilter"));
                Assert.That(instance.Root.GetComponent<MeshRenderer>(), Is.Null, fixture.Context("Root MeshRenderer"));
                Assert.That(instance.Root.transform.Find("Model"), Is.Not.Null, fixture.Context("Model child"));
                Assert.That(instance.MeshRenderer, Is.Not.Null, fixture.Context("MeshRenderer"));
                Assert.That(instance.Mesh, Is.Not.Null, fixture.Context("Mesh"));
                Assert.That(instance.VertexCount, Is.GreaterThanOrEqualTo(fixture.expected.minVertices), fixture.Context("VertexCount"));
                Assert.That(instance.IndexCount, Is.GreaterThanOrEqualTo(fixture.expected.minIndices), fixture.Context("IndexCount"));
                Assert.That(instance.SubmeshCount, Is.GreaterThanOrEqualTo(1), fixture.Context("SubmeshCount"));
                Assert.That(instance.Materials, Has.Length.GreaterThanOrEqualTo(fixture.expected.minMaterials), fixture.Context("Materials"));
                Assert.That(instance.BoneTransforms, Has.Length.GreaterThanOrEqualTo(fixture.expected.minBones), fixture.Context("BoneTransforms"));
            }
            finally
            {
                MmdTestInstanceScope.DestroyInstance(instance);
            }
        }

        [TestCaseSource(nameof(RenderablePackageModelFixtures))]
        public void RenderablePmxFixtureInstantiatesSkinnedUnityModel(ModelFixtureEntry fixture)
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = MmdTestFixtures.ParseModel(fixture);

                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                Assert.That(instance.SkinnedMeshRenderer, Is.Not.Null, fixture.Context("SkinnedMeshRenderer"));
                SkinnedMeshRenderer renderer = instance.SkinnedMeshRenderer!;
                Assert.That(instance.Mesh.bindposes, Has.Length.GreaterThanOrEqualTo(fixture.expected.minBones), fixture.Context("bindposes"));
                Assert.That(instance.Mesh.boneWeights, Has.Length.EqualTo(instance.VertexCount), fixture.Context("boneWeights"));
                Assert.That(instance.BoneTransforms, Has.Length.GreaterThanOrEqualTo(fixture.expected.minBones), fixture.Context("BoneTransforms"));
                Assert.That(renderer.bones, Has.Length.EqualTo(instance.BoneTransforms.Length), fixture.Context("renderer.bones"));
            }
            finally
            {
                MmdTestInstanceScope.DestroyInstance(instance);
            }
        }
    }
}
