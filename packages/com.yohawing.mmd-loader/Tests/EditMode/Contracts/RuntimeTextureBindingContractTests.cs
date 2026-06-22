#nullable enable

using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Mmd.Parser;
using Mmd.Rendering;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class RuntimeTextureBindingContractTests
    {
        [Test]
        public void RelativeDiffuseTextureLoadsFromPmxSourceDirectoryAndBindsMaterialSlots()
        {
            MmdUnityModelInstance? instance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string textureDirectory = Path.Combine(tempRoot, "textures");
                Directory.CreateDirectory(textureDirectory);
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WritePng(Path.Combine(textureDirectory, "diffuse.png"), Color.red);

                MmdModelDefinition model = CreateMinimalTriangleModel();
                model.materials[0].texture = Path.Combine("textures", "diffuse.png");

                instance = MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Assert.That(instance.SourceContext, Is.Not.Null);
                Assert.That(instance.SourceContext.SourcePath, Is.EqualTo(Path.GetFullPath(pmxPath)));
                Assert.That(instance.LoadedDiffuseTextureCount, Is.EqualTo(1));
                Assert.That(instance.OwnedTextures, Has.Length.EqualTo(1));
                Assert.That(instance.MissingTextureReferenceCount, Is.EqualTo(0));
                Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(0));
                Assert.That(ReadBoundDiffuseTexture(instance.Materials[0]), Is.EqualTo(instance.OwnedTextures[0]));
                Assert.That(instance.MaterialBindingDiagnostics[0].baseMapBound, Is.True);
                Assert.That(instance.MaterialBindingDiagnostics[0].mainTexBound, Is.True);
                Assert.That(instance.MaterialBindingDiagnostics[0].baseMapTexture, Is.EqualTo(Path.Combine("textures", "diffuse.png")));
            }
            finally
            {
                DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void EscapedRelativeTexturePathIsRejectedWithStructuredDiagnostics()
        {
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxDirectory = Path.Combine(tempRoot, "model");
                Directory.CreateDirectory(pmxDirectory);
                string pmxPath = Path.Combine(pmxDirectory, "model.pmx");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });

                MmdModelDefinition model = CreateMinimalTriangleModel();
                model.materials[0].texture = "..\\outside.png";
                MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);

                MmdRuntimeTextureResolution resolution = MmdRuntimeTextureResolver.ResolveDiffuseTextures(
                    descriptor,
                    MmdUnityModelSourceContext.FromOptionalPath(pmxPath));

                Assert.That(resolution.DiffuseTextures, Is.Empty);
                Assert.That(resolution.Diagnostics.UnsupportedTextureReferenceCount, Is.EqualTo(1));
                Assert.That(resolution.Diagnostics.TextureReferences, Has.Count.EqualTo(1));
                Assert.That(resolution.Diagnostics.TextureReferences[0].materialIndex, Is.EqualTo(0));
                Assert.That(resolution.Diagnostics.TextureReferences[0].usage, Is.EqualTo("diffuse"));
                Assert.That(resolution.Diagnostics.TextureReferences[0].reference, Is.EqualTo("..\\outside.png"));
                Assert.That(resolution.Diagnostics.TextureReferences[0].status, Is.EqualTo("unsupported"));
                Assert.That(resolution.Diagnostics.TextureReferences[0].reason, Does.Contain("escapes the PMX source directory"));
                Assert.That(
                    resolution.Diagnostics.Messages.Any(message => message.Contains("escapes the PMX source directory")),
                    Is.True);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        private static MmdModelDefinition CreateMinimalTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "minimal-texture-contract-triangle"
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
                name = "texture-contract-material",
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

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "mmd-unity-texture-contract-" + Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            return path;
        }

        private static void WritePng(string path, Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixels(new[] { color, color, color, color });
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static Texture ReadBoundDiffuseTexture(Material material)
        {
            if (material.HasProperty("_BaseMap"))
            {
                Texture texture = material.GetTexture("_BaseMap");
                if (texture != null)
                {
                    return texture;
                }
            }

            return material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        }

        private static void DestroyInstance(MmdUnityModelInstance? instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.Root != null)
            {
                Object.DestroyImmediate(instance.Root);
            }

            if (instance.Mesh != null)
            {
                Object.DestroyImmediate(instance.Mesh);
            }

            foreach (Material material in instance.Materials.Where(material => material != null).Distinct())
            {
                Object.DestroyImmediate(material);
            }

            foreach (Texture2D texture in instance.OwnedTextures.Where(texture => texture != null).Distinct())
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
