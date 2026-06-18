#nullable enable

using NUnit.Framework;
using UnityEngine;
using Yohawing.MmdUnity.Parser;
using Yohawing.MmdUnity.UnityIntegration;

namespace Yohawing.MmdUnity.Tests
{
    [TestFixture]
    public sealed class ImportScalePhysicsUnitContractTests
    {
        [Test]
        public void ImportScaleZeroDotOneProducesCharacterHeightMeshBoundsAndUnscaledDescriptor()
        {
            MmdUnityModelInstance instance = null;
            try
            {
                MmdModelDefinition model = CreateCharacterHeightTriangleModel();

                instance = MmdUnityModelFactory.CreateSkinnedModel(model, sourcePath: null, importScale: 0.1f);

                Bounds meshBounds = instance.Mesh.bounds;
                Assert.That(meshBounds.size.y, Is.EqualTo(2.0f).Within(0.001f));

                Assert.That(instance.Root.transform.localScale, Is.EqualTo(Vector3.one));
                Transform modelRoot = instance.Root.transform.Find("Model");
                Assert.That(modelRoot, Is.Not.Null);
                Assert.That(modelRoot.localScale, Is.EqualTo(Vector3.one));
                Assert.That(instance.ImportScale, Is.EqualTo(0.1f).Within(0.0001f));

                Assert.That(instance.RenderingDescriptor.vertices[0].position[1], Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(instance.RenderingDescriptor.vertices[1].position[1], Is.EqualTo(20.0f).Within(0.00001f));
                Assert.That(instance.RenderingDescriptor.vertices[2].position[1], Is.EqualTo(20.0f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        private static MmdModelDefinition CreateCharacterHeightTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "character-height-triangle"
            };
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                transformOrder = 0,
                origin = new[] { 0.0f, 0.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1,
                name = "child",
                parentIndex = 0,
                transformOrder = 0,
                origin = new[] { 0.0f, 10.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 20.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 20.0f, 1.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "character-mat",
                vertexCount = 3
            });
            return model;
        }

        private static MmdVertexDefinition CreateVertex(
            int index,
            float x,
            float y,
            float z,
            float u,
            float v)
        {
            return new MmdVertexDefinition
            {
                index = index,
                position = new[] { x, y, z },
                normal = new[] { 0.0f, 0.0f, 1.0f },
                uv = new[] { u, v },
                boneIndices = new[] { 0 },
                boneWeights = new[] { 1.0f }
            };
        }

        private static void DestroyInstance(MmdUnityModelInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.Root != null)
            {
                UnityEngine.Object.DestroyImmediate(instance.Root);
            }

            if (instance.Mesh != null)
            {
                UnityEngine.Object.DestroyImmediate(instance.Mesh);
            }
        }
    }
}
