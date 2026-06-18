#nullable enable

using NUnit.Framework;
using Yohawing.MmdUnity.Parser;
using Yohawing.MmdUnity.Rendering;

namespace Yohawing.MmdUnity.Tests
{
    [TestFixture]
    public sealed class RenderingDescriptorContractTests
    {
        [Test]
        public void MeshSkinningSubmeshAndUrpDescriptorsPreserveModelSpaceHandoff()
        {
            MmdModelDefinition model = CreateMinimalTriangleModel();
            model.vertices[1].position = new[] { 1.0f, 2.0f, 3.0f };
            model.vertices[1].normal = new[] { 0.0f, 1.0f, 0.0f };
            model.vertices[1].uv = new[] { 0.25f, 0.75f };
            model.vertices[1].boneIndices = new[] { 0 };
            model.vertices[1].boneWeights = new[] { 1.0f };

            MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);

            Assert.That(descriptor.vertices[1].vertexIndex, Is.EqualTo(1));
            Assert.That(descriptor.vertices[1].position, Is.EqualTo(new[] { 1.0f, 2.0f, 3.0f }));
            Assert.That(descriptor.vertices[1].normal, Is.EqualTo(new[] { 0.0f, 1.0f, 0.0f }));
            Assert.That(descriptor.vertices[1].uv, Is.EqualTo(new[] { 0.25f, 0.75f }));
            Assert.That(descriptor.indices, Is.EqualTo(new[] { 0, 1, 2 }));

            Assert.That(descriptor.skinning[1].vertexIndex, Is.EqualTo(1));
            Assert.That(descriptor.skinning[1].boneIndices[0], Is.EqualTo(0));
            Assert.That(descriptor.skinning[1].boneWeights[0], Is.EqualTo(1.0f).Within(0.00001f));

            Assert.That(descriptor.submeshes, Has.Count.EqualTo(1));
            Assert.That(descriptor.submeshes[0].submeshIndex, Is.EqualTo(0));
            Assert.That(descriptor.submeshes[0].materialIndex, Is.EqualTo(0));
            Assert.That(descriptor.submeshes[0].indexStart, Is.EqualTo(0));
            Assert.That(descriptor.submeshes[0].indexCount, Is.EqualTo(3));

            Assert.That(descriptor.urpMaterialBindings, Has.Count.EqualTo(1));
            Assert.That(descriptor.urpMaterialBindings[0].shaderName, Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName));
        }

        [Test]
        public void MaterialDescriptorsPreserveTextureReferencesColorsAndEdgeHandoff()
        {
            MmdModelDefinition model = CreateMinimalTriangleModel();
            model.materials[0].texture = "diffuse.png";
            model.materials[0].sphereTexture = "sphere.spa";
            model.materials[0].toonTexture = "toon.bmp";
            model.materials[0].diffuseColor = new[] { 0.6f, 0.4f, 0.2f };
            model.materials[0].ambientColor = new[] { 0.2f, 0.1f, 0.05f };
            model.materials[0].edgeColor = new[] { 0.01f, 0.02f, 0.03f, 0.75f };
            model.materials[0].edgeSize = 0.9f;

            MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);

            Assert.That(descriptor.materials, Has.Count.EqualTo(1));
            Assert.That(descriptor.materials[0].materialIndex, Is.EqualTo(0));
            Assert.That(descriptor.materials[0].name, Is.EqualTo("rendering-contract-material"));
            Assert.That(descriptor.materials[0].texture, Is.EqualTo("diffuse.png"));
            Assert.That(descriptor.materials[0].sphereTexture, Is.EqualTo("sphere.spa"));
            Assert.That(descriptor.materials[0].toonTexture, Is.EqualTo("toon.bmp"));
            Assert.That(descriptor.materials[0].diffuseColor, Is.EqualTo(new[] { 0.6f, 0.4f, 0.2f }));
            Assert.That(descriptor.materials[0].ambientColor, Is.EqualTo(new[] { 0.2f, 0.1f, 0.05f }));
            Assert.That(descriptor.materials[0].edgeColor, Is.EqualTo(new[] { 0.01f, 0.02f, 0.03f, 0.75f }));
            Assert.That(descriptor.materials[0].edgeSize, Is.EqualTo(0.9f).Within(0.00001f));
            Assert.That(descriptor.materials[0].vertexStart, Is.EqualTo(0));
            Assert.That(descriptor.materials[0].vertexCount, Is.EqualTo(3));
        }

        [Test]
        public void MaterialAndUrpDescriptorsPreserveCullingPolicy()
        {
            MmdModelDefinition model = CreateTwoMaterialTriangleModel();
            model.materials[0].cullingPolicy = "double-sided";
            model.materials[1].cullingPolicy = "backface-culling";

            MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);

            Assert.That(descriptor.materials[0].cullingPolicy, Is.EqualTo("double-sided"));
            Assert.That(descriptor.urpMaterialBindings[0].cullingPolicy, Is.EqualTo("double-sided"));
            Assert.That(descriptor.materials[1].cullingPolicy, Is.EqualTo("backface-culling"));
            Assert.That(descriptor.urpMaterialBindings[1].cullingPolicy, Is.EqualTo("backface-culling"));
        }

        private static MmdModelDefinition CreateMinimalTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "minimal-rendering-contract-triangle"
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
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "rendering-contract-material",
                vertexCount = 3
            });
            return model;
        }

        private static MmdModelDefinition CreateTwoMaterialTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "two-material-rendering-contract-triangle"
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
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.vertices.Add(CreateVertex(3, 1.0f, 1.0f, 0.0f, 1.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2, 1, 3, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "front-contract-material",
                vertexCount = 3
            });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 1,
                name = "back-contract-material",
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
    }
}
