#nullable enable

using NUnit.Framework;
using UnityEngine;
using Mmd.Parser;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class ImportScalePhysicsUnitContractTests
    {
        [Test]
        public void ImportScaleZeroDotOneProducesCharacterHeightMeshBoundsAndUnscaledDescriptor()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateCharacterHeightTriangleModel();
                model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
                {
                    index = 0,
                    name = "child-body",
                    boneIndex = 1,
                    boneName = "child",
                    shapeType = "sphere",
                    size = new[] { 0.5f, 0.0f, 0.0f },
                    position = new[] { 0.0f, 12.0f, 0.0f },
                    rotation = new[] { 0.0f, 0.0f, 0.0f },
                    mass = 1.0f,
                    linearDamping = 0.0f,
                    angularDamping = 0.0f,
                    friction = 0.0f,
                    restitution = 0.0f,
                    group = 0,
                    mask = 0xffff,
                    physicsKind = "dynamic"
                });
                model.physics.joints.Add(new MmdJointDefinition
                {
                    index = 0,
                    name = "child-joint",
                    rigidbodyAIndex = 0,
                    rigidbodyBIndex = 0,
                    position = new[] { 0.0f, 11.0f, 0.0f },
                    rotation = new[] { 0.0f, 0.0f, 0.0f },
                    linearLowerLimit = new[] { -0.25f, -0.5f, -0.75f },
                    linearUpperLimit = new[] { 0.25f, 0.5f, 0.75f },
                    angularLowerLimit = new[] { -0.1f, -0.2f, -0.3f },
                    angularUpperLimit = new[] { 0.1f, 0.2f, 0.3f },
                    linearSpring = new[] { 1.0f, 2.0f, 3.0f },
                    angularSpring = new[] { 4.0f, 5.0f, 6.0f }
                });

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

                Assert.That(instance.PhysicsBodies, Has.Length.EqualTo(1));
                MmdUnityPhysicsBody physicsBody = instance.PhysicsBodies[0];
                Assert.That(physicsBody.transform.localPosition, Is.EqualTo(new Vector3(0.0f, 0.2f, 0.0f)));
                SphereCollider collider = physicsBody.GetComponent<SphereCollider>();
                Assert.That(collider.radius, Is.EqualTo(0.05f).Within(0.00001f));
                Assert.That(physicsBody.DescriptorSize, Is.EqualTo(new Vector3(0.5f, 0.0f, 0.0f)));
                Assert.That(physicsBody.DescriptorPosition, Is.EqualTo(new Vector3(0.0f, 12.0f, 0.0f)));
                Assert.That(model.physics.rigidbodies[0].size, Is.EqualTo(new[] { 0.5f, 0.0f, 0.0f }));
                Assert.That(model.physics.rigidbodies[0].position, Is.EqualTo(new[] { 0.0f, 12.0f, 0.0f }));
                Assert.That(model.physics.joints[0].position, Is.EqualTo(new[] { 0.0f, 11.0f, 0.0f }));
                Assert.That(model.physics.joints[0].linearLowerLimit, Is.EqualTo(new[] { -0.25f, -0.5f, -0.75f }));
                Assert.That(model.physics.joints[0].linearUpperLimit, Is.EqualTo(new[] { 0.25f, 0.5f, 0.75f }));
                Assert.That(model.physics.joints[0].angularLowerLimit, Is.EqualTo(new[] { -0.1f, -0.2f, -0.3f }));
                Assert.That(model.physics.joints[0].angularUpperLimit, Is.EqualTo(new[] { 0.1f, 0.2f, 0.3f }));
                Assert.That(model.physics.joints[0].linearSpring, Is.EqualTo(new[] { 1.0f, 2.0f, 3.0f }));
                Assert.That(model.physics.joints[0].angularSpring, Is.EqualTo(new[] { 4.0f, 5.0f, 6.0f }));
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

        private static void DestroyInstance(MmdUnityModelInstance? instance)
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
