#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Mmd.Parser;
using Mmd.Rendering;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdUnityModelFactoryTests
    {
        [Test]
        public void CreateStaticModelFromModelCreatesMeshObjectAndMaterials()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);

                instance = MmdUnityModelFactory.CreateStaticModel(model);

                Assert.That(instance, Is.Not.Null);
                Assert.That(instance.Root, Is.Not.Null);
                Assert.That(instance.Mesh, Is.Not.Null);
                Assert.That(instance.Materials, Is.Not.Null);
                GameObject root = instance.Root!;
                Assert.That(root.GetComponent<MeshFilter>(), Is.Null);
                Assert.That(root.GetComponent<MeshRenderer>(), Is.Null);
                Assert.That(root.transform.Find("Model"), Is.Not.Null);
                Assert.That(instance.MeshRenderer, Is.Not.Null);
                MeshRenderer meshRenderer = instance.MeshRenderer!;
                Assert.That(meshRenderer.transform.parent, Is.EqualTo(root.transform));
                Assert.That(instance.SkinnedMeshRenderer, Is.Null);
                Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(instance.Mesh.vertexCount, Is.EqualTo(3));
                Assert.That(instance.Mesh.subMeshCount, Is.EqualTo(1));
                Assert.That(instance.Materials, Has.Length.EqualTo(1));
                Assert.That(instance.ShaderDiagnostics.requestedShaderName, Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName));
                Assert.That(instance.ShaderDiagnostics.resolvedShaderName, Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName));
                Assert.That(instance.ShaderDiagnostics.shaderFallbackUsed, Is.False);
                Assert.That(instance.MaterialBindingDiagnostics, Has.Length.EqualTo(1));
                Assert.That(instance.MaterialBindingDiagnostics[0].materialIndex, Is.EqualTo(0));
                Assert.That(instance.MaterialBindingDiagnostics[0].shaderName, Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName));
                Assert.That(instance.MaterialBindingDiagnostics[0].resolvedShaderName, Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName));
                Assert.That(instance.MaterialBindingDiagnostics[0].cullingPolicy, Is.EqualTo("unknown"));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateStaticModelConvertsMeshPositionsAndNormalsAtUnityBoundary()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.vertices[1].position = new[] { 1.0f, 0.0f, 2.0f };
                model.vertices[1].normal = new[] { 0.0f, 0.0f, 1.0f };

                instance = MmdUnityModelFactory.CreateStaticModel(model);

                Vector3[] vertices = instance.Mesh.vertices;
                Vector3[] normals = instance.Mesh.normals;
                Assert.That(vertices[1], Is.EqualTo(new Vector3(-1.0f, 0.0f, -2.0f)));
                Assert.That(normals[1], Is.EqualTo(new Vector3(0.0f, 0.0f, -1.0f)));
                Assert.That(instance.Mesh.GetIndices(0), Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(instance.Root.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateStaticModelFromDescriptorReportsDescriptorCounts()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.vertices[1].position = new[] { 1.0f, 2.0f, 3.0f };
                model.vertices[1].normal = new[] { 0.0f, 1.0f, 0.0f };
                model.vertices[1].uv = new[] { 0.25f, 0.75f };
                model.vertices[1].boneIndices = new[] { 0 };
                model.vertices[1].boneWeights = new[] { 1.0f };
                MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);

                instance = MmdUnityModelFactory.CreateStaticModel(descriptor, "minimal-static-triangle");

                Assert.That(instance, Is.Not.Null);
                Assert.That(instance.VertexCount, Is.EqualTo(descriptor.vertices.Count));
                Assert.That(instance.IndexCount, Is.EqualTo(descriptor.indices.Count));
                Assert.That(instance.SubmeshCount, Is.EqualTo(descriptor.submeshes.Count));
                Assert.That(instance.Mesh.vertexCount, Is.EqualTo(descriptor.vertices.Count));
                Assert.That(instance.Mesh.subMeshCount, Is.EqualTo(descriptor.submeshes.Count));
                Assert.That(instance.Materials, Has.Length.EqualTo(descriptor.materials.Count));
                Assert.That(instance.SkippedTextureReferenceCount, Is.EqualTo(0));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateStaticModelCountsSkippedTextureSphereAndToonReferences()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: true);
                model.materials[0].diffuseColor = new[] { 0.6f, 0.4f, 0.2f };
                model.materials[0].ambientColor = new[] { 0.2f, 0.1f, 0.05f };
                model.materials[0].edgeColor = new[] { 0.01f, 0.02f, 0.03f, 0.75f };
                model.materials[0].edgeSize = 0.9f;

                instance = MmdUnityModelFactory.CreateStaticModel(model);

                Assert.That(instance, Is.Not.Null);
                Assert.That(instance.Materials, Has.Length.EqualTo(1));
                Assert.That(instance.SkippedTextureReferenceCount, Is.EqualTo(5));
                Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(3));
                Assert.That(instance.SkippedSphereTextureReferenceCount, Is.EqualTo(1));
                Assert.That(instance.SkippedToonTextureReferenceCount, Is.EqualTo(1));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateStaticModelWithSourcePathLoadsGrayscalePngDiffuseTexture()
        {
            MmdUnityModelInstance? instance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string textureDirectory = Path.Combine(tempRoot, "textures");
                Directory.CreateDirectory(textureDirectory);
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WriteGrayscalePng(Path.Combine(textureDirectory, "gray.png"));

                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.materials[0].texture = Path.Combine("textures", "gray.png");

                instance = MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Assert.That(instance.LoadedDiffuseTextureCount, Is.EqualTo(1));
                Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(0));
                Assert.That(instance.MaterialBindingDiagnostics[0].baseMapBound, Is.True);
            }
            finally
            {
                DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void CreateStaticModelWithSourcePathLoadsRgbPngDiffuseTexture()
        {
            MmdUnityModelInstance? instance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string textureDirectory = Path.Combine(tempRoot, "textures");
                Directory.CreateDirectory(textureDirectory);
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WriteRgbPng(Path.Combine(textureDirectory, "rgb.png"));

                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.materials[0].texture = Path.Combine("textures", "rgb.png");

                instance = MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Assert.That(instance.LoadedDiffuseTextureCount, Is.EqualTo(1));
                Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(0));
                Assert.That(instance.MaterialBindingDiagnostics[0].baseMapBound, Is.True);
            }
            finally
            {
                DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void CreateStaticModelClassifiesPngCutoutTextureAsAlphaTest()
        {
            MmdUnityModelInstance? instance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string textureDirectory = Path.Combine(tempRoot, "textures");
                Directory.CreateDirectory(textureDirectory);
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WriteCutoutPng(Path.Combine(textureDirectory, "cutout.png"));

                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.materials[0].texture = Path.Combine("textures", "cutout.png");

                instance = MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Assert.That(instance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Geometry));
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_ZWrite"), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_AlphaClipThreshold"), Is.EqualTo(0.01f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].isTransparent, Is.True);
                Assert.That(instance.MaterialBindingDiagnostics[0].transparencyMode, Is.EqualTo("alphaTest"));
                Assert.That(instance.MaterialBindingDiagnostics[0].renderOrderBucket, Is.EqualTo("alphaTest"));
                Assert.That(instance.MaterialBindingDiagnostics[0].materialRenderOrder, Is.EqualTo(0));
                Assert.That(instance.MaterialBindingDiagnostics[0].outlineRenderOrder, Is.EqualTo(1));
                Assert.That(instance.MaterialBindingDiagnostics[0].transparentOrder, Is.EqualTo(-1));
                Assert.That(instance.MaterialBindingDiagnostics[0].transparentPolicy, Is.EqualTo("mmd-material-alpha-test-depth-write"));
            }
            finally
            {
                DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void CreateStaticModelBlendsNearOpaqueTextureAlphaLikeRealMmd()
        {
            // Real MMD always multiplies the diffuse texture alpha into the fragment and alpha-blends;
            // it has no "opaque-enough" threshold. A hair texture whose used-UV alpha is high but not
            // fully opaque (Sour_Miku_Black's hair.png strand edges fall to ~213/255) must therefore
            // alpha-blend, not snap to opaque. Regression: the old 195 opaque threshold absorbed the
            // 195-254 soft band and rendered such hair fully opaque, diverging from the GoldenOracle.
            MmdUnityModelInstance? instance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string textureDirectory = Path.Combine(tempRoot, "textures");
                Directory.CreateDirectory(textureDirectory);
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                // Uniform 213/255: high but not fully opaque. Coverage-independent so the assertion
                // pins the threshold boundary regardless of which texels the triangle UV samples.
                WriteTga32Alpha(Path.Combine(textureDirectory, "near-opaque-hair.tga"), width: 4, height: 4, alpha: 213);

                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.materials[0].name = "hair";
                model.materials[0].texture = Path.Combine("textures", "near-opaque-hair.tga");

                instance = MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Assert.That(instance.MaterialBindingDiagnostics[0].transparencyMode, Is.EqualTo("alphaBlend"));
                Assert.That(instance.MaterialBindingDiagnostics[0].isTransparent, Is.True);
                Assert.That(instance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
                Assert.That(
                    ReadMaterialFloat(instance.Materials[0], "_DstBlend"),
                    Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void CreateStaticModelIgnoresTransparentAtlasPaddingOutsideUsedUvs()
        {
            MmdUnityModelInstance? instance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string textureDirectory = Path.Combine(tempRoot, "textures");
                Directory.CreateDirectory(textureDirectory);
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WriteAtlasPaddingPng(Path.Combine(textureDirectory, "atlas-padding.png"));

                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.vertices[0].uv = new[] { 0.40f, 0.60f };
                model.vertices[1].uv = new[] { 0.60f, 0.60f };
                model.vertices[2].uv = new[] { 0.40f, 0.40f };
                model.materials[0].name = "mat_atlas_padding";
                model.materials[0].texture = Path.Combine("textures", "atlas-padding.png");

                instance = MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Assert.That(instance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Geometry));
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_AlphaClipThreshold"), Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].isTransparent, Is.False);
                Assert.That(instance.MaterialBindingDiagnostics[0].transparencyMode, Is.EqualTo("opaque"));
                Assert.That(instance.MaterialBindingDiagnostics[0].renderOrderBucket, Is.EqualTo("opaque"));
            }
            finally
            {
                DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void CreateStaticModelClassifiesTgaTransparencyByTextureAlphaContent()
        {
            // Real MMD (and the faithful saba reference) always multiplies the diffuse texture alpha
            // into the fragment and alpha-blends, regardless of texture extension or material name. So
            // a regular (non-overlay) TGA whose alpha is a meaningful mask must classify as alphaBlend,
            // while a fully-opaque TGA stays opaque. The texture alpha content — not the ".tga"
            // extension or an overlay-looking name — drives the mode. (Earlier behavior wrongly forced
            // every non-overlay TGA to opaque, painting masked TGAs as solid blocks vs the GoldenOracle.)
            MmdUnityModelInstance? maskedInstance = null;
            MmdUnityModelInstance? opaqueInstance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string textureDirectory = Path.Combine(tempRoot, "textures");
                Directory.CreateDirectory(textureDirectory);
                WriteTga32Alpha(Path.Combine(textureDirectory, "tga-regular-masked.tga"), width: 4, height: 4, alpha: 96);
                WriteTga32Alpha(Path.Combine(textureDirectory, "tga-regular-solid.tga"), width: 4, height: 4, alpha: 255);

                MmdModelDefinition maskedModel = CreateMinimalTriangleModel(includeTextureReferences: false);
                maskedModel.materials[0].name = "mat_tga_regular_hair";
                maskedModel.materials[0].texture = Path.Combine("textures", "tga-regular-masked.tga");

                MmdModelDefinition opaqueModel = CreateMinimalTriangleModel(includeTextureReferences: false);
                opaqueModel.materials[0].name = "mat_tga_regular_solid";
                opaqueModel.materials[0].texture = Path.Combine("textures", "tga-regular-solid.tga");

                maskedInstance = MmdUnityModelFactory.CreateStaticModel(maskedModel, pmxPath);
                opaqueInstance = MmdUnityModelFactory.CreateStaticModel(opaqueModel, pmxPath);

                // Partial alpha on a regular-named TGA now blends (was wrongly forced opaque before).
                Assert.That(maskedInstance.MaterialBindingDiagnostics[0].isTransparent, Is.True);
                Assert.That(maskedInstance.MaterialBindingDiagnostics[0].transparencyMode, Is.EqualTo("alphaBlend"));
                Assert.That(maskedInstance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));

                // A fully-opaque TGA stays opaque purely from its alpha content.
                Assert.That(opaqueInstance.MaterialBindingDiagnostics[0].isTransparent, Is.False);
                Assert.That(opaqueInstance.MaterialBindingDiagnostics[0].transparencyMode, Is.EqualTo("opaque"));
                Assert.That(opaqueInstance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Geometry));
            }
            finally
            {
                DestroyInstance(maskedInstance);
                DestroyInstance(opaqueInstance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void CreateStaticModelWithSourcePathLoadsRelativeTgaDiffuseTexture()
        {
            MmdUnityModelInstance? instance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string textureDirectory = Path.Combine(tempRoot, "tex");
                Directory.CreateDirectory(textureDirectory);
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WriteTga24(Path.Combine(textureDirectory, "diffuse.TGA"), width: 2, height: 2);

                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.materials[0].texture = Path.Combine("tex", "diffuse.TGA");

                instance = MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Assert.That(instance.LoadedDiffuseTextureCount, Is.EqualTo(1));
                Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(0));
                Assert.That(instance.OwnedTextures, Has.Length.EqualTo(1));
                Assert.That(instance.OwnedTextures[0].width, Is.EqualTo(2));
                Assert.That(instance.OwnedTextures[0].height, Is.EqualTo(2));
                Assert.That(ReadBoundDiffuseTexture(instance.Materials[0]), Is.EqualTo(instance.OwnedTextures[0]));
            }
            finally
            {
                DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void CreateStaticModelWithSourcePathLoadsRelativeDdsDiffuseTexture()
        {
            MmdUnityModelInstance? instance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string textureDirectory = Path.Combine(tempRoot, "tex");
                Directory.CreateDirectory(textureDirectory);
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WriteDdsDxt3(Path.Combine(textureDirectory, "diffuse.dds"));

                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.materials[0].texture = Path.Combine("tex", "diffuse.dds");

                instance = MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Assert.That(instance.LoadedDiffuseTextureCount, Is.EqualTo(1));
                Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(0));
                Assert.That(instance.OwnedTextures, Has.Length.EqualTo(1));
                Assert.That(instance.OwnedTextures[0].width, Is.EqualTo(4));
                Assert.That(instance.OwnedTextures[0].height, Is.EqualTo(4));
                Assert.That(ReadBoundDiffuseTexture(instance.Materials[0]), Is.EqualTo(instance.OwnedTextures[0]));
            }
            finally
            {
                DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void CreateStaticModelWithSourcePathLoadsSphereAndToonTexturesForDiagnostics()
        {
            MmdUnityModelInstance? instance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string sphereDirectory = Path.Combine(tempRoot, "sp");
                string toonDirectory = Path.Combine(tempRoot, "spt");
                Directory.CreateDirectory(sphereDirectory);
                Directory.CreateDirectory(toonDirectory);
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WriteBmp24(Path.Combine(sphereDirectory, "sphere.bmp"), width: 2, height: 2);
                WriteJpg(Path.Combine(toonDirectory, "toon.jpg"), Color.green);

                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.materials[0].sphereTexture = Path.Combine("sp", "sphere.bmp");
                model.materials[0].toonTexture = Path.Combine("spt", "toon.jpg");

                instance = MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Assert.That(instance.LoadedDiffuseTextureCount, Is.EqualTo(0));
                Assert.That(instance.LoadedSphereTextureCount, Is.EqualTo(1));
                Assert.That(instance.LoadedToonTextureCount, Is.EqualTo(1));
                Assert.That(instance.SkippedSphereTextureReferenceCount, Is.EqualTo(1));
                Assert.That(instance.SkippedToonTextureReferenceCount, Is.EqualTo(1));
                Assert.That(instance.MissingTextureReferenceCount, Is.EqualTo(0));
                Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(0));
                Assert.That(instance.OwnedTextures, Has.Length.EqualTo(2));
                Assert.That(instance.OwnedTextures[0].hideFlags, Is.EqualTo(HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild));
                Assert.That(instance.OwnedTextures[1].hideFlags, Is.EqualTo(HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild));
                Assert.That(instance.MaterialBindingDiagnostics[0].sphereMapBound, Is.True);
                Assert.That(instance.MaterialBindingDiagnostics[0].toonMapBound, Is.True);
                Assert.That(ReadMaterialTexture(instance.Materials[0], "_SphereMap"), Is.EqualTo(instance.OwnedTextures[0]));
                Assert.That(ReadMaterialTexture(instance.Materials[0], "_ToonMap"), Is.EqualTo(instance.OwnedTextures[1]));
                Assert.That(
                    instance.TextureDiagnostics.Messages.Any(message => message.Contains("loaded for diagnostics")),
                    Is.True);
            }
            finally
            {
                DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void CreateStaticModelWithSharedToonResolvesBuiltInToonRamp()
        {
            MmdUnityModelInstance? instance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });

                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                // A shared toon material carries an index (toon01..toon10), not a texture path.
                model.materials[0].toonTexture = string.Empty;
                model.materials[0].toonShared = true;
                model.materials[0].sharedToonIndex = 0; // toon01

                instance = MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Assert.That(instance.LoadedToonTextureCount, Is.EqualTo(1));
                Texture? nullableToonMap = ReadMaterialTexture(instance.Materials[0], "_ToonMap");
                Assert.That(nullableToonMap, Is.Not.Null, "shared toon ramp should be bound to _ToonMap");
                Texture toonMap = nullableToonMap!;
                // 1x32 vertical ramp (toon carries no horizontal detail; shader samples U=0.5).
                Assert.That(toonMap.width, Is.EqualTo(1));
                Assert.That(toonMap.height, Is.EqualTo(32));
                Assert.That(instance.Materials[0].GetFloat("_ToonMapBound"), Is.EqualTo(1.0f));
                Assert.That(instance.MaterialBindingDiagnostics[0].toonMapBound, Is.True);
                Assert.That(
                    instance.OwnedTextures.Any(texture =>
                        texture == toonMap &&
                        texture.hideFlags == (HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild)),
                    Is.True,
                    "shared toon ramp should be an owned, non-persisted runtime texture");
            }
            finally
            {
                DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void SharedToonTexturesDecodeValidIndicesAndRejectOutOfRange()
        {
            Assert.That(MmdSharedToonTextures.IsSharedToonIndex(-1), Is.False);
            Assert.That(MmdSharedToonTextures.IsSharedToonIndex(0), Is.True);
            Assert.That(MmdSharedToonTextures.IsSharedToonIndex(MmdSharedToonTextures.SharedToonCount - 1), Is.True);
            Assert.That(MmdSharedToonTextures.IsSharedToonIndex(MmdSharedToonTextures.SharedToonCount), Is.False);

            Assert.That(MmdSharedToonTextures.TryCreateSharedToonTexture(-1), Is.Null);
            Assert.That(MmdSharedToonTextures.TryCreateSharedToonTexture(MmdSharedToonTextures.SharedToonCount), Is.Null);

            for (int index = 0; index < MmdSharedToonTextures.SharedToonCount; index++)
            {
                Texture2D? toon = MmdSharedToonTextures.TryCreateSharedToonTexture(index);
                try
                {
                    Assert.That(toon, Is.Not.Null, $"shared toon {index} should decode");
                    Texture2D decodedToon = toon!;
                    Assert.That(decodedToon.width, Is.EqualTo(1));
                    Assert.That(decodedToon.height, Is.EqualTo(32));
                }
                finally
                {
                    if (toon != null)
                    {
                        UnityEngine.Object.DestroyImmediate(toon);
                    }
                }
            }
        }

        [Test]
        public void CreateStaticModelAppliesTransparentMaterialAlpha()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.materials[0].alpha = 0.25f;
                model.materials[0].diffuseColor = new[] { 0.8f, 0.2f, 0.1f };
                model.materials[0].ambientColor = new[] { 0.3f, 0.1f, 0.05f };
                model.materials[0].edgeColor = new[] { 0.01f, 0.02f, 0.03f, 0.75f };
                model.materials[0].edgeSize = 1.25f;
                model.materials[0].drawEdgeFlag = true;

                instance = MmdUnityModelFactory.CreateStaticModel(model);

                Assert.That(instance.RenderingDescriptor.materials[0].alpha, Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(instance.RenderingDescriptor.materials[0].diffuseColor[0], Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(instance.RenderingDescriptor.materials[0].edgeSize, Is.EqualTo(1.25f).Within(0.00001f));
                Assert.That(instance.RenderingDescriptor.materials[0].drawEdgeFlag, Is.True);
                Assert.That(instance.RenderingDescriptor.urpMaterialBindings[0].edgeSize, Is.EqualTo(1.25f).Within(0.00001f));
                Assert.That(instance.RenderingDescriptor.urpMaterialBindings[0].drawEdgeFlag, Is.True);
                Assert.That(instance.RenderingDescriptor.urpMaterialBindings[0].isTransparent, Is.True);
                Assert.That(instance.Mesh.hideFlags, Is.EqualTo(HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild));
                Assert.That(instance.Materials[0].hideFlags, Is.EqualTo(HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild));
                Assert.That(instance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
                Assert.That(ReadMaterialAlpha(instance.Materials[0]), Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(ReadMaterialColor(instance.Materials[0], "_BaseColor").r, Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(ReadMaterialColor(instance.Materials[0], "_AmbientColor").r, Is.EqualTo(0.3f).Within(0.00001f));
                Assert.That(ReadMaterialColor(instance.Materials[0], "_OutlineColor").a, Is.EqualTo(0.75f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_OutlineWidth"), Is.EqualTo(1.25f).Within(0.00001f));
                // MMD's edge is a screen-space, constant-pixel silhouette: the loader runs the outline
                // shader in screen-space mode (weight 1) with edgeSize as the raw pixel width.
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_OutlineScreenSpaceWeight"), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_OutlineZTest"), Is.EqualTo((float)UnityEngine.Rendering.CompareFunction.Less).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_Alpha"), Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_ZWrite"), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_SrcBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.SrcAlpha).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_DstBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
                Assert.That(instance.MaterialBindingDiagnostics[0].isTransparent, Is.True);
                Assert.That(instance.MaterialBindingDiagnostics[0].diffuseColor[0], Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].diffuseColor[1], Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].diffuseColor[2], Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].ambientColor[0], Is.EqualTo(0.3f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].edgeColor[3], Is.EqualTo(0.75f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].baseColorProperty[0], Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].baseColorProperty[3], Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].ambientColorProperty[0], Is.EqualTo(0.3f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].outlineColorProperty[3], Is.EqualTo(0.75f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].edgeSize, Is.EqualTo(1.25f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].outlineWidth, Is.EqualTo(1.25f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].transparencyMode, Is.EqualTo("alphaBlend"));
                Assert.That(instance.MaterialBindingDiagnostics[0].renderOrderBucket, Is.EqualTo("alphaBlend"));
                Assert.That(instance.MaterialBindingDiagnostics[0].materialRenderOrder, Is.EqualTo(0));
                Assert.That(instance.MaterialBindingDiagnostics[0].outlineRenderOrder, Is.EqualTo(1));
                Assert.That(instance.MaterialBindingDiagnostics[0].transparentOrder, Is.EqualTo(0));
                Assert.That(instance.MaterialBindingDiagnostics[0].renderQueueOffset, Is.EqualTo(0));
                Assert.That(instance.MaterialBindingDiagnostics[0].sortingPriority, Is.EqualTo(0));
                Assert.That(instance.MaterialBindingDiagnostics[0].transparentPolicy, Is.EqualTo("mmd-material-order-queue-depth-write"));
                Assert.That(instance.MaterialBindingDiagnostics[0].zWrite, Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].srcBlend, Is.EqualTo((float)UnityEngine.Rendering.BlendMode.SrcAlpha).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].dstBlend, Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateStaticModelOffsetsTransparentQueuesByMaterialOrder()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateTwoTransparentTriangleModel();

                instance = MmdUnityModelFactory.CreateStaticModel(model);

                Assert.That(instance.Mesh.subMeshCount, Is.EqualTo(2));
                Assert.That(instance.Materials, Has.Length.EqualTo(2));
                Assert.That(instance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
                Assert.That(instance.Materials[1].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent + 1));
                Assert.That(instance.MaterialBindingDiagnostics[0].materialIndex, Is.EqualTo(0));
                Assert.That(instance.MaterialBindingDiagnostics[0].materialSlot, Is.EqualTo(0));
                Assert.That(instance.MaterialBindingDiagnostics[0].submeshIndex, Is.EqualTo(0));
                Assert.That(instance.MaterialBindingDiagnostics[0].transparentOrder, Is.EqualTo(0));
                Assert.That(instance.MaterialBindingDiagnostics[0].transparentPolicy, Is.EqualTo("mmd-material-order-queue-depth-write"));
                Assert.That(instance.MaterialBindingDiagnostics[1].materialIndex, Is.EqualTo(1));
                Assert.That(instance.MaterialBindingDiagnostics[1].materialSlot, Is.EqualTo(1));
                Assert.That(instance.MaterialBindingDiagnostics[1].submeshIndex, Is.EqualTo(1));
                Assert.That(instance.MaterialBindingDiagnostics[1].transparentOrder, Is.EqualTo(1));
                Assert.That(instance.MaterialBindingDiagnostics[1].renderQueueOffset, Is.EqualTo(1));
                Assert.That(instance.MaterialBindingDiagnostics[1].sortingPriority, Is.EqualTo(1));
                Assert.That(instance.MaterialBindingDiagnostics[1].zWrite, Is.EqualTo(1.0f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateStaticModelAppliesDescriptorCullingPolicy()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateTwoTransparentTriangleModel();
                model.materials[0].cullingPolicy = "double-sided";
                model.materials[0].drawEdgeFlag = true;
                model.materials[0].edgeSize = 1.0f;
                model.materials[1].cullingPolicy = "backface-culling";
                model.materials[1].drawEdgeFlag = true;
                model.materials[1].edgeSize = 1.0f;

                instance = MmdUnityModelFactory.CreateStaticModel(model);

                Assert.That(ReadMaterialFloat(instance.Materials[0], "_Cull"), Is.EqualTo((float)UnityEngine.Rendering.CullMode.Off).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_OutlineVisible"), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].cull, Is.EqualTo((float)UnityEngine.Rendering.CullMode.Off).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[0].cullingPolicy, Is.EqualTo("double-sided"));
                Assert.That(instance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));

                Assert.That(ReadMaterialFloat(instance.Materials[1], "_Cull"), Is.EqualTo((float)UnityEngine.Rendering.CullMode.Back).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[1], "_OutlineVisible"), Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[1].cull, Is.EqualTo((float)UnityEngine.Rendering.CullMode.Back).Within(0.00001f));
                Assert.That(instance.MaterialBindingDiagnostics[1].cullingPolicy, Is.EqualTo("backface-culling"));
                Assert.That(instance.Materials[1].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent + 1));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateStaticModelFallsBackWhenRequestedShaderIsMissing()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);
                descriptor.urpMaterialBindings[0].shaderName = "Missing/MMD Basic URP Toon Test";

                instance = MmdUnityModelFactory.CreateStaticModel(descriptor, "missing-shader-fallback");

                Assert.That(instance.ShaderDiagnostics.requestedShaderName, Is.EqualTo("Missing/MMD Basic URP Toon Test"));
                Assert.That(instance.ShaderDiagnostics.shaderFallbackUsed, Is.True);
                Assert.That(instance.ShaderDiagnostics.fallbackReason, Is.EqualTo("requested-shader-not-found"));
                Assert.That(instance.ShaderDiagnostics.resolvedShaderName, Is.Not.Empty);
                Assert.That(instance.ShaderDiagnostics.fallbackCandidates, Does.Contain("Missing/MMD Basic URP Toon Test"));
                Assert.That(instance.Materials[0].shader, Is.Not.Null);
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateStaticModelPreservesRawSnapshotUvAndFlipsViewportUv()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.vertices[1].uv = new[] { 0.25f, 0.75f };

                instance = MmdUnityModelFactory.CreateStaticModel(model);

                Assert.That(instance.RenderingDescriptor.vertices[1].uv[0], Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(instance.RenderingDescriptor.vertices[1].uv[1], Is.EqualTo(0.75f).Within(0.00001f));
                Assert.That(instance.RenderingDescriptor.textureOrientation.flipVForViewport, Is.True);
                Assert.That(instance.RenderingDescriptor.textureOrientation.flipTexturePixels, Is.False);

                Vector2[] viewportUvs = instance.Mesh.uv;
                Assert.That(viewportUvs, Has.Length.EqualTo(3));
                Assert.That(viewportUvs[1].x, Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(viewportUvs[1].y, Is.EqualTo(0.25f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateStaticModelViewportUvSamplesRawTextureWithFlippedV()
        {
            MmdUnityModelInstance? instance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string texturePath = Path.Combine(tempRoot, "orientation.png");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WriteVerticalOrientationPng(texturePath);

                MmdModelDefinition model = CreateTexturedQuadModel("orientation.png");
                instance = MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Texture? nullableTexture = ReadBoundDiffuseTexture(instance.Materials[0]);
                Assert.That(nullableTexture, Is.Not.Null);
                var texture = (Texture2D)nullableTexture!;
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                Vector2[] viewportUv = instance.Mesh.uv;
                Color upper = texture.GetPixelBilinear(0.5f, (viewportUv[0].y + viewportUv[1].y) * 0.5f);
                Color lower = texture.GetPixelBilinear(0.5f, (viewportUv[2].y + viewportUv[3].y) * 0.5f);

                Assert.That(instance.RenderingDescriptor.vertices[0].uv[1], Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(viewportUv[0].y, Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(upper.r, Is.GreaterThan(upper.b), "upper viewport UV should sample the red top texture row");
                Assert.That(lower.b, Is.GreaterThan(lower.r), "lower viewport UV should sample the blue bottom texture row");
            }
            finally
            {
                DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void CreateStaticModelWithSourcePathLoadsIndexedBmpSphereTextureForDiagnostics()
        {
            MmdUnityModelInstance? instance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string sphereDirectory = Path.Combine(tempRoot, "sp");
                Directory.CreateDirectory(sphereDirectory);
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WriteBmp8Indexed(Path.Combine(sphereDirectory, "sphere.bmp"), width: 2, height: 2);

                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.materials[0].sphereTexture = Path.Combine("sp", "sphere.bmp");

                instance = MmdUnityModelFactory.CreateStaticModel(model, pmxPath);

                Assert.That(instance.LoadedSphereTextureCount, Is.EqualTo(1));
                Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(0));
                Assert.That(instance.OwnedTextures, Has.Length.EqualTo(1));
                Assert.That(instance.OwnedTextures[0].width, Is.EqualTo(2));
                Assert.That(instance.OwnedTextures[0].height, Is.EqualTo(2));
            }
            finally
            {
                DestroyInstance(instance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void CreateStaticModelReportsMissingAndUnsupportedTexturesWithoutFailing()
        {
            MmdUnityModelInstance? missingInstance = null;
            MmdUnityModelInstance? unsupportedInstance = null;
            string tempRoot = CreateTempDirectory();
            try
            {
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });

                MmdModelDefinition missingModel = CreateMinimalTriangleModel(includeTextureReferences: false);
                missingModel.materials[0].texture = "missing.png";
                missingInstance = MmdUnityModelFactory.CreateStaticModel(missingModel, pmxPath);

                Assert.That(missingInstance.Root, Is.Not.Null);
                Assert.That(missingInstance.LoadedDiffuseTextureCount, Is.EqualTo(0));
                Assert.That(missingInstance.MissingTextureReferenceCount, Is.EqualTo(1));
                Assert.That(missingInstance.UnsupportedTextureReferenceCount, Is.EqualTo(0));

                MmdModelDefinition unsupportedModel = CreateMinimalTriangleModel(includeTextureReferences: false);
                unsupportedModel.materials[0].texture = "diffuse.webp";
                unsupportedInstance = MmdUnityModelFactory.CreateStaticModel(unsupportedModel, pmxPath);

                Assert.That(unsupportedInstance.Root, Is.Not.Null);
                Assert.That(unsupportedInstance.LoadedDiffuseTextureCount, Is.EqualTo(0));
                Assert.That(unsupportedInstance.MissingTextureReferenceCount, Is.EqualTo(0));
                Assert.That(unsupportedInstance.UnsupportedTextureReferenceCount, Is.EqualTo(1));
            }
            finally
            {
                DestroyInstance(missingInstance);
                DestroyInstance(unsupportedInstance);
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void CreateSkinnedModelCreatesSkinnedRendererBindposesAndBoneWeights()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.vertices[1].boneIndices = new[] { 0, 1 };
                model.vertices[1].boneWeights = new[] { 0.25f, 0.75f };

                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                GameObject root = instance.Root!;
                Assert.That(root.GetComponent<MeshFilter>(), Is.Null);
                Assert.That(root.GetComponent<MeshRenderer>(), Is.Null);
                Assert.That(root.transform.Find("Model"), Is.Not.Null);
                Assert.That(instance.MeshRenderer, Is.Null);
                Assert.That(instance.SkinnedMeshRenderer, Is.Not.Null);
                SkinnedMeshRenderer renderer = instance.SkinnedMeshRenderer!;
                Assert.That(renderer.transform.parent, Is.EqualTo(root.transform));
                Assert.That(renderer.sharedMesh, Is.EqualTo(instance.Mesh));
                Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(1));
                Assert.That(renderer.bones, Has.Length.EqualTo(2));
                Assert.That(renderer.bones[0], Is.EqualTo(instance.BoneTransforms[0]));
                Assert.That(instance.Mesh.bindposes, Has.Length.EqualTo(2));
                Assert.That(instance.Mesh.boneWeights, Has.Length.EqualTo(3));
                Assert.That(instance.Mesh.boneWeights[1].boneIndex0, Is.EqualTo(0));
                Assert.That(instance.Mesh.boneWeights[1].boneIndex1, Is.EqualTo(1));
                Assert.That(instance.Mesh.boneWeights[1].weight0, Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(instance.Mesh.boneWeights[1].weight1, Is.EqualTo(0.75f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateSkinnedModelCreatesPhysicsCollidersAndInspectableParameters()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
                {
                    index = 0,
                    name = "child sphere",
                    boneIndex = 1,
                    boneName = "child",
                    shapeType = "sphere",
                    size = new[] { 0.25f, 0.0f, 0.0f },
                    position = new[] { 0.0f, 2.0f, 3.0f },
                    rotation = new[] { 0.0f, 0.0f, 0.0f },
                    mass = 1.5f,
                    linearDamping = 0.25f,
                    angularDamping = 0.75f,
                    friction = 0.4f,
                    restitution = 0.2f,
                    group = 3,
                    mask = 0x000f,
                    physicsKind = "dynamic"
                });
                model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
                {
                    index = 1,
                    name = "root box",
                    boneIndex = 0,
                    boneName = "root",
                    shapeType = "box",
                    size = new[] { 1.0f, 2.0f, 3.0f },
                    position = new[] { 1.0f, 0.0f, 0.0f },
                    rotation = new[] { 0.0f, 0.0f, 0.0f },
                    mass = 0.0f,
                    linearDamping = 0.1f,
                    angularDamping = 0.2f,
                    friction = 0.3f,
                    restitution = 0.4f,
                    group = 2,
                    mask = 0x00ff,
                    physicsKind = "static"
                });
                model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
                {
                    index = 2,
                    name = "root capsule",
                    boneIndex = 0,
                    boneName = "root",
                    shapeType = "capsule",
                    size = new[] { 0.5f, 2.0f, 0.0f },
                    position = new[] { 0.0f, 0.0f, 0.0f },
                    rotation = new[] { 0.0f, 0.0f, 0.0f },
                    mass = 0.75f,
                    linearDamping = 0.0f,
                    angularDamping = 0.0f,
                    friction = 0.1f,
                    restitution = 0.0f,
                    group = 1,
                    mask = 0xffff,
                    physicsKind = "dynamic-orientation"
                });

                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                Assert.That(instance.PhysicsBodies, Has.Length.EqualTo(3));
                MmdUnityPhysicsBody sphereBody = instance.PhysicsBodies[0];
                Assert.That(sphereBody.transform.parent, Is.EqualTo(instance.BoneTransforms[1]));
                Assert.That(sphereBody.transform.localPosition, Is.EqualTo(new Vector3(0.0f, 1.0f, -3.0f)));
                Assert.That(sphereBody.ShapeType, Is.EqualTo("sphere"));
                Assert.That(sphereBody.DescriptorSize, Is.EqualTo(new Vector3(0.25f, 0.0f, 0.0f)));
                Assert.That(sphereBody.DescriptorPosition, Is.EqualTo(new Vector3(0.0f, 2.0f, 3.0f)));
                Assert.That(sphereBody.DescriptorRotation, Is.EqualTo(Vector3.zero));
                Assert.That(sphereBody.GetComponent<SphereCollider>().radius, Is.EqualTo(0.25f).Within(0.00001f));
                Rigidbody sphereRigidbody = sphereBody.GetComponent<Rigidbody>();
                Assert.That(sphereRigidbody.isKinematic, Is.True);
                Assert.That(sphereRigidbody.useGravity, Is.False);
                Assert.That(sphereRigidbody.detectCollisions, Is.False);
                Assert.That(sphereRigidbody.mass, Is.EqualTo(1.5f).Within(0.00001f));
                Assert.That(sphereRigidbody.linearDamping, Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(sphereRigidbody.angularDamping, Is.EqualTo(0.75f).Within(0.00001f));
                Assert.That(sphereBody.Friction, Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(sphereBody.Restitution, Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(sphereBody.CollisionGroup, Is.EqualTo(3));
                Assert.That(sphereBody.CollisionMask, Is.EqualTo(0x000f));

                BoxCollider boxCollider = instance.PhysicsBodies[1].GetComponent<BoxCollider>();
                Assert.That(instance.PhysicsBodies[1].ShapeType, Is.EqualTo("box"));
                Assert.That(instance.PhysicsBodies[1].DescriptorSize, Is.EqualTo(new Vector3(1.0f, 2.0f, 3.0f)));
                Assert.That(instance.PhysicsBodies[1].DescriptorPosition, Is.EqualTo(new Vector3(1.0f, 0.0f, 0.0f)));
                Assert.That(boxCollider.size, Is.EqualTo(new Vector3(2.0f, 4.0f, 6.0f)));
                CapsuleCollider capsuleCollider = instance.PhysicsBodies[2].GetComponent<CapsuleCollider>();
                Assert.That(instance.PhysicsBodies[2].ShapeType, Is.EqualTo("capsule"));
                Assert.That(instance.PhysicsBodies[2].DescriptorSize, Is.EqualTo(new Vector3(0.5f, 2.0f, 0.0f)));
                Assert.That(instance.PhysicsBodies[2].PhysicsKind, Is.EqualTo("dynamic-orientation"));
                Assert.That(capsuleCollider.radius, Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(capsuleCollider.height, Is.EqualTo(3.0f).Within(0.00001f));
                Assert.That(capsuleCollider.direction, Is.EqualTo(1));
                Assert.That(instance.PhysicsBodies[2].HasNativeTransform, Is.False);
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateSkinnedModelParentsPhysicsBodiesByPmxBoneIndex()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.bones[0].index = 10;
                model.bones[1].index = 20;
                model.bones[1].parentIndex = 10;
                foreach (MmdVertexDefinition vertex in model.vertices)
                {
                    vertex.boneIndices = new[] { 10 };
                }

                model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
                {
                    index = 0,
                    name = "non-contiguous child body",
                    boneIndex = 20,
                    boneName = "child",
                    shapeType = "sphere",
                    size = new[] { 0.25f, 0.0f, 0.0f },
                    position = new[] { 0.0f, 2.0f, 0.0f },
                    rotation = new[] { 0.0f, 0.0f, 0.0f },
                    mass = 1.0f,
                    linearDamping = 0.0f,
                    angularDamping = 0.0f,
                    friction = 0.5f,
                    restitution = 0.0f,
                    group = 0,
                    mask = 0,
                    physicsKind = "dynamic"
                });

                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                Assert.That(instance.BoneTransforms, Has.Length.EqualTo(2));
                Assert.That(instance.BoneTransforms[1].name, Is.EqualTo("child"));
                Assert.That(instance.PhysicsBodies[0].transform.parent, Is.EqualTo(instance.BoneTransforms[1]));
                Assert.That(instance.PhysicsBodies[0].transform.localPosition, Is.EqualTo(new Vector3(0.0f, 1.0f, 0.0f)));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateStaticModelFromModelCreatesBoneTransformHierarchy()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);

                instance = MmdUnityModelFactory.CreateStaticModel(model);

                Assert.That(instance.BoneTransforms, Has.Length.EqualTo(2));
                Assert.That(instance.BoneTransforms[0].name, Is.EqualTo("root"));
                Assert.That(instance.BoneTransforms[1].name, Is.EqualTo("child"));
                Transform modelRoot = instance.Root.transform.Find("Model");
                Assert.That(modelRoot, Is.Not.Null);
                Assert.That(instance.BoneTransforms[0].parent, Is.EqualTo(modelRoot));
                Assert.That(instance.BoneTransforms[1].parent, Is.EqualTo(instance.BoneTransforms[0]));
                Assert.That(instance.BoneTransforms[0].localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(instance.BoneTransforms[1].localPosition, Is.EqualTo(new Vector3(0.0f, 1.0f, 0.0f)));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyFrameUpdatesBoneTransformsFromBindPose()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                MmdUnityFrameApplier.ApplyFrame(instance, CreateFrame(
                    CreateBonePose(0, "root", 1.0f, 2.0f, 3.0f),
                    CreateBonePose(1, "child", 0.0f, 0.0f, 2.0f)));

                Assert.That(instance.BoneTransforms[0].localPosition, Is.EqualTo(new Vector3(-1.0f, 2.0f, -3.0f)));
                Assert.That(instance.BoneTransforms[1].localPosition, Is.EqualTo(new Vector3(0.0f, 1.0f, -2.0f)));

                MmdUnityFrameApplier.ApplyFrame(instance, CreateFrame(
                    CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f),
                    CreateBonePose(1, "child", 0.0f, 0.0f, 4.0f)));

                Assert.That(instance.BoneTransforms[0].localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(instance.BoneTransforms[1].localPosition, Is.EqualTo(new Vector3(0.0f, 1.0f, -4.0f)));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyFrameConvertsRotationAtUnityBoundary()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);
                Quaternion mmdRotation = Quaternion.Euler(10.0f, 20.0f, 30.0f);
                Quaternion expectedUnityRotation = new Quaternion(
                    -mmdRotation.x,
                    mmdRotation.y,
                    -mmdRotation.z,
                    mmdRotation.w);

                MmdUnityFrameApplier.ApplyFrame(instance, CreateFrame(new MmdEvaluatedBonePose
                {
                    index = 0,
                    name = "root",
                    localPosition = new[] { 0.0f, 0.0f, 0.0f },
                    localRotation = new[] { mmdRotation.x, mmdRotation.y, mmdRotation.z, mmdRotation.w },
                    localScale = new[] { 1.0f, 1.0f, 1.0f },
                    worldMatrix = new[]
                    {
                        1.0f, 0.0f, 0.0f, 0.0f,
                        0.0f, 1.0f, 0.0f, 0.0f,
                        0.0f, 0.0f, 1.0f, 0.0f,
                        0.0f, 0.0f, 0.0f, 1.0f
                    }
                }));

                Assert.That(Quaternion.Angle(instance.BoneTransforms[0].localRotation, expectedUnityRotation), Is.LessThan(0.0001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyColumnMajorWorldMatricesConvertsHierarchyWorldPoseAtUnityBoundary()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);
                Quaternion rootMmdRotation = Quaternion.Euler(0.0f, 45.0f, 0.0f);
                Quaternion childMmdRotation = Quaternion.Euler(15.0f, 30.0f, 10.0f);
                float[] worldMatrices = Concatenate(
                    CreateColumnMajorWorldMatrix(new Vector3(1.0f, 2.0f, 3.0f), rootMmdRotation),
                    CreateColumnMajorWorldMatrix(new Vector3(-2.0f, 4.0f, 5.0f), childMmdRotation));

                MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(instance, worldMatrices);

                Assert.That(Vector3.Distance(instance.BoneTransforms[0].position, new Vector3(-1.0f, 2.0f, -3.0f)), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(instance.BoneTransforms[1].position, new Vector3(2.0f, 4.0f, -5.0f)), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(instance.BoneTransforms[0].rotation, ToUnityModelRotation(rootMmdRotation)), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(instance.BoneTransforms[1].rotation, ToUnityModelRotation(childMmdRotation)), Is.LessThan(0.0001f));
                Assert.That(instance.BoneTransforms[1].parent, Is.EqualTo(instance.BoneTransforms[0]));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyColumnMajorWorldMatricesScalesPositionOnlyWithImportScale()
        {
            MmdUnityModelInstance? scaleOneInstance = null;
            MmdUnityModelInstance? scalePointOneInstance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                scaleOneInstance = MmdUnityModelFactory.CreateSkinnedModel(model, sourcePath: null, importScale: 1.0f);
                scalePointOneInstance = MmdUnityModelFactory.CreateSkinnedModel(model, sourcePath: null, importScale: 0.1f);
                Quaternion rootMmdRotation = Quaternion.Euler(0.0f, 45.0f, 0.0f);
                Quaternion childMmdRotation = Quaternion.Euler(15.0f, 30.0f, 10.0f);
                float[] worldMatrices = Concatenate(
                    CreateColumnMajorWorldMatrix(new Vector3(1.0f, 2.0f, 3.0f), rootMmdRotation),
                    CreateColumnMajorWorldMatrix(new Vector3(-2.0f, 4.0f, 5.0f), childMmdRotation));

                MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(scaleOneInstance, worldMatrices);
                MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(scalePointOneInstance, worldMatrices);

                Assert.That(Vector3.Distance(scaleOneInstance.BoneTransforms[0].position, new Vector3(-1.0f, 2.0f, -3.0f)), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(scaleOneInstance.BoneTransforms[1].position, new Vector3(2.0f, 4.0f, -5.0f)), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(scalePointOneInstance.BoneTransforms[0].position, scaleOneInstance.BoneTransforms[0].position * 0.1f), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(scalePointOneInstance.BoneTransforms[1].position, scaleOneInstance.BoneTransforms[1].position * 0.1f), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(scalePointOneInstance.BoneTransforms[0].rotation, scaleOneInstance.BoneTransforms[0].rotation), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(scalePointOneInstance.BoneTransforms[1].rotation, scaleOneInstance.BoneTransforms[1].rotation), Is.LessThan(0.0001f));
                Assert.That(scalePointOneInstance.BoneTransforms[0].localScale, Is.EqualTo(Vector3.one));
                Assert.That(scalePointOneInstance.BoneTransforms[1].localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                DestroyInstance(scaleOneInstance);
                DestroyInstance(scalePointOneInstance);
            }
        }

        [Test]
        public void ApplyFrameRejectsBoneIndexWithoutUnityTransform()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                var ex = Assert.Throws<ArgumentException>(() =>
                    MmdUnityFrameApplier.ApplyFrame(instance, CreateFrame(CreateBonePose(2, "missing", 0.0f, 0.0f, 0.0f))));

                Assert.That(ex.Message, Does.Contain("no Unity bone transform"));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void PlaybackBindingBuildsSnapshotAndAppliesFrameToSkinnedModel()
        {
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                MmdMotionDefinition motion = CreateRootTranslationMotion();

                binding = MmdUnityPlaybackBinding.CreateSkinned(model, motion, "model.pmx", "motion.vmd");
                MmdPlaybackSnapshot snapshot = binding.ApplyFrame(frame: 10, frameRate: 30.0f);

                Assert.That(snapshot.model, Is.EqualTo("model.pmx"));
                Assert.That(snapshot.motion, Is.EqualTo("motion.vmd"));
                Assert.That(snapshot.frame.frame, Is.EqualTo(10));
                Assert.That(snapshot.rendering, Is.SameAs(binding.Instance.RenderingDescriptor));
                Assert.That(binding.Instance.SkinnedMeshRenderer, Is.Not.Null);
                Assert.That(binding.Instance.BoneTransforms[0].localPosition, Is.EqualTo(new Vector3(-2.0f, 0.0f, 0.0f)));
                Assert.That(binding.Instance.Root.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                DestroyInstance(binding?.Instance);
            }
        }

        [Test]
        public void PlaybackBindingAppliesVertexMorphWeightsToSkinnedMeshWithoutAccumulation()
        {
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.morphs.Add(new MmdMorphDefinition
                {
                    index = 0,
                    name = "blink",
                    type = "vertex",
                    panel = "eye",
                    vertexOffsets =
                    {
                        new MmdVertexMorphOffsetDefinition
                        {
                            vertexIndex = 1,
                            positionDelta = new[] { 0.0f, 2.0f, 0.0f }
                        }
                    }
                });

                binding = MmdUnityPlaybackBinding.CreateSkinned(model, CreateBlinkMorphMotion(), "morph-model.pmx", "blink.vmd");

                Assert.That(binding.Instance.SkinnedMeshRenderer, Is.Not.Null);
                SkinnedMeshRenderer renderer = binding.Instance.SkinnedMeshRenderer!;
                int blinkShapeIndex = binding.Instance.Mesh.GetBlendShapeIndex("blink");
                Assert.That(blinkShapeIndex, Is.GreaterThanOrEqualTo(0));

                binding.ApplyFrame(frame: 10, frameRate: 30.0f);
                float morphedWeight = renderer.GetBlendShapeWeight(blinkShapeIndex);
                Bounds morphedBounds = renderer.localBounds;

                binding.ApplyFrame(frame: 10, frameRate: 30.0f);
                float repeatedWeight = renderer.GetBlendShapeWeight(blinkShapeIndex);

                binding.ApplyFrame(frame: 0, frameRate: 30.0f);
                float restoredWeight = renderer.GetBlendShapeWeight(blinkShapeIndex);

                Assert.That(morphedWeight, Is.EqualTo(100f).Within(0.001f));
                Assert.That(morphedBounds.Contains(new Vector3(-1.0f, 2.0f, 0.0f)), Is.True);
                Assert.That(repeatedWeight, Is.EqualTo(morphedWeight).Within(0.001f));
                Assert.That(restoredWeight, Is.EqualTo(0f).Within(0.001f));
                Assert.That(binding.Instance.RenderingDescriptor.vertices[1].position[1], Is.EqualTo(0.0f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(binding?.Instance);
            }
        }

        [Test]
        public void CreateSkinnedModelSplitsSharedVerticesPerSubmeshAndDuplicatesMorphOffsets()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateSharedVertexTwoSubmeshMorphModel();

                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                Assert.That(instance.Mesh.vertexCount, Is.EqualTo(6));
                Assert.That(instance.Mesh.subMeshCount, Is.EqualTo(2));
                Assert.That(instance.Mesh.GetIndices(0), Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(instance.Mesh.GetIndices(1), Is.EqualTo(new[] { 3, 4, 5 }));
                Assert.That(instance.RenderingDescriptor.vertices.Select(vertex => vertex.vertexIndex), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
                Assert.That(instance.RenderingDescriptor.skinning[0].skinningMode, Is.EqualTo("sdef"));
                Assert.That(instance.RenderingDescriptor.skinning[0].supportStatus, Is.EqualTo(MmdSkinningDescriptorBuilder.LinearFallbackStatus));
                Assert.That(instance.RenderingDescriptor.skinning[0].linearFallbackToBoneWeights, Is.True);
                Assert.That(instance.RenderingDescriptor.skinning[3].skinningMode, Is.EqualTo("sdef"));
                Assert.That(instance.RenderingDescriptor.skinning[3].supportStatus, Is.EqualTo(MmdSkinningDescriptorBuilder.LinearFallbackStatus));
                Assert.That(instance.RenderingDescriptor.skinning[3].linearFallbackToBoneWeights, Is.True);
                Assert.That(instance.RenderingDescriptor.vertexMorphs[0].offsets.Select(offset => offset.vertexIndex), Is.EqualTo(new[] { 0, 3 }));
                Assert.That(instance.Mesh.blendShapeCount, Is.EqualTo(1));

                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "shared-up", weight = 1.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                int sharedUpIndex = instance.Mesh.GetBlendShapeIndex("shared-up");
                Assert.That(sharedUpIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(instance.SkinnedMeshRenderer, Is.Not.Null);
                SkinnedMeshRenderer renderer = instance.SkinnedMeshRenderer!;
                Vector3[] deltaVertices = new Vector3[instance.Mesh.vertexCount];
                Vector3[] deltaNormals = new Vector3[instance.Mesh.vertexCount];
                Vector3[] deltaTangents = new Vector3[instance.Mesh.vertexCount];
                instance.Mesh.GetBlendShapeFrameVertices(sharedUpIndex, 0, deltaVertices, deltaNormals, deltaTangents);
                Assert.That(deltaVertices[0], Is.EqualTo(new Vector3(0.0f, 1.0f, 0.0f)));
                Assert.That(deltaVertices[3], Is.EqualTo(new Vector3(0.0f, 1.0f, 0.0f)));
                Assert.That(renderer.GetBlendShapeWeight(sharedUpIndex), Is.EqualTo(100f).Within(0.001f));
                Assert.That(instance.Mesh.vertices[0], Is.EqualTo(new Vector3(0.0f, 0.0f, 0.0f)));
                Assert.That(instance.Mesh.vertices[3], Is.EqualTo(new Vector3(0.0f, 0.0f, 0.0f)));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void VertexOnlyBlendShapeMorphFrameDoesNotUploadMeshVertices()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateSharedVertexTwoSubmeshMorphModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                int sharedUpIndex = instance.Mesh.GetBlendShapeIndex("shared-up");
                Assert.That(sharedUpIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(instance.SkinnedMeshRenderer, Is.Not.Null);
                SkinnedMeshRenderer renderer = instance.SkinnedMeshRenderer!;

                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "shared-up", weight = 1.0f });

                MmdUnityMorphApplyTimingSummary timing = MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);

                Assert.That(timing.blendShapePathUsed, Is.True);
                Assert.That(timing.meshUploadRequired, Is.False);
                Assert.That(timing.setVerticesMs, Is.EqualTo(0.0));
                Assert.That(timing.setUvsMs, Is.EqualTo(0.0));
                Assert.That(timing.recalculateBoundsMs, Is.EqualTo(0.0));
                Assert.That(renderer.GetBlendShapeWeight(sharedUpIndex), Is.EqualTo(100f).Within(0.001f));
                Assert.That(instance.Mesh.vertices[0], Is.EqualTo(new Vector3(0.0f, 0.0f, 0.0f)));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void BlendShapeVertexMorphWithTextureUvMorphReportsUvMeshUpload()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateTextureUvMorphTriangleModel();
                model.morphs.Add(new MmdMorphDefinition
                {
                    index = 1,
                    name = "blink",
                    type = "vertex",
                    panel = "eye",
                    vertexOffsets =
                    {
                        new MmdVertexMorphOffsetDefinition
                        {
                            vertexIndex = 1,
                            positionDelta = new[] { 0.0f, 1.0f, 0.0f }
                        }
                    }
                });
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "blink", weight = 1.0f });
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "uv-shift", weight = 1.0f });

                MmdUnityMorphApplyTimingSummary timing = MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);

                Assert.That(timing.blendShapePathUsed, Is.True);
                Assert.That(timing.meshUploadRequired, Is.True);
                Assert.That(timing.setVerticesMs, Is.EqualTo(0.0));
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Assert.That(renderer.GetBlendShapeWeight(instance.BlendShapeIndexMap["blink"]), Is.EqualTo(100f).Within(0.001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void BlendShapeBoundsUseLocalBoundsWithoutRecalculateForResolvedWeightsAboveOne()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateGroupMorphTriangleModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "smile", weight = 1.0f });
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 0.5f });

                MmdUnityMorphApplyTimingSummary timing = MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);

                Assert.That(timing.blendShapePathUsed, Is.True);
                Assert.That(timing.localBoundsAssigned, Is.True);
                Assert.That(timing.localBoundsSkipped, Is.False);
                Assert.That(timing.recalculateBoundsMs, Is.EqualTo(0.0));
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Assert.That(renderer.localBounds.Contains(new Vector3(-1.0f, 2.5f, 0.0f)), Is.True);
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void BlendShapeLocalBoundsSkipWhenResolvedWeightsAreUnchanged()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateGroupMorphTriangleModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "smile", weight = 1.0f });
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 0.5f });

                MmdUnityMorphApplyTimingSummary first = MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Bounds firstBounds = renderer.localBounds;
                MmdUnityMorphApplyTimingSummary second = MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);
                Bounds secondBounds = renderer.localBounds;

                Assert.That(first.localBoundsAssigned, Is.True);
                Assert.That(first.localBoundsSkipped, Is.False);
                Assert.That(second.localBoundsAssigned, Is.False);
                Assert.That(second.localBoundsSkipped, Is.True);
                Assert.That(second.localBoundsAssignMs, Is.EqualTo(0.0));
                Assert.That(secondBounds.center, Is.EqualTo(firstBounds.center));
                Assert.That(secondBounds.size, Is.EqualTo(firstBounds.size));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void BlendShapeLocalBoundsRecalculateWhenResolvedWeightsChangeAfterSkip()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateGroupMorphTriangleModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "smile", weight = 1.0f });
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 0.5f });

                MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);
                MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);

                MmdEvaluatedFrame changed = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                changed.morphs.Add(new MmdEvaluatedMorphWeight { name = "smile", weight = 1.0f });
                changed.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 1.0f });
                MmdUnityMorphApplyTimingSummary timing = MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, changed);

                Assert.That(timing.localBoundsAssigned, Is.True);
                Assert.That(timing.localBoundsSkipped, Is.False);
                Assert.That(timing.recalculateBoundsMs, Is.EqualTo(0.0));
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Assert.That(renderer.localBounds.Contains(new Vector3(-1.0f, 3.0f, 0.0f)), Is.True);
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void DuplicateNameVertexMorphsBakeDistinctBlendShapesAndShareResolvedWeight()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateDuplicateNameVertexMorphModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                Assert.That(instance.Mesh.blendShapeCount, Is.EqualTo(2));
                Assert.That(instance.VertexMorphBlendShapes.Select(binding => binding.BlendShapeName), Is.EqualTo(new[] { "0:duplicate", "1:duplicate" }));

                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "duplicate", weight = 0.5f });

                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Assert.That(renderer.GetBlendShapeWeight(instance.VertexMorphBlendShapes[0].BlendShapeIndex), Is.EqualTo(50f).Within(0.001f));
                Assert.That(renderer.GetBlendShapeWeight(instance.VertexMorphBlendShapes[1].BlendShapeIndex), Is.EqualTo(50f).Within(0.001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void DuplicateVertexMorphOffsetsAggregateIntoBakedBlendShapeDelta()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateDuplicateOffsetVertexMorphModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                int blendShapeIndex = instance.Mesh.GetBlendShapeIndex("stacked");
                Assert.That(blendShapeIndex, Is.GreaterThanOrEqualTo(0));

                Vector3[] deltaVertices = new Vector3[instance.Mesh.vertexCount];
                Vector3[] deltaNormals = new Vector3[instance.Mesh.vertexCount];
                Vector3[] deltaTangents = new Vector3[instance.Mesh.vertexCount];
                instance.Mesh.GetBlendShapeFrameVertices(blendShapeIndex, 0, deltaVertices, deltaNormals, deltaTangents);

                Assert.That(deltaVertices[1], Is.EqualTo(new Vector3(0.0f, 1.0f, 0.0f)));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyFrameAppliesTextureUvMorphToMeshUv()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateTextureUvMorphTriangleModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                // Step 1: Apply texture UV morph at weight 1.0.
                MmdEvaluatedFrame frame1 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame1.morphs.Add(new MmdEvaluatedMorphWeight { name = "uv-shift", weight = 1.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame1);
                Vector2[] uv1 = instance.Mesh.uv;

                // Vertex 1 source UV moves by (0.25, 0.5), then is converted to Unity viewport UV.
                Assert.That(uv1[1].x, Is.EqualTo(1.25f).Within(0.00001f));
                Assert.That(uv1[1].y, Is.EqualTo(0.5f).Within(0.00001f));
                // Unmorphed vertices keep base viewport UV.
                Assert.That(uv1[0].x, Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(uv1[0].y, Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(uv1[2].x, Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(uv1[2].y, Is.EqualTo(0.0f).Within(0.00001f));

                // Step 2: Apply the same frame again; UVs must not accumulate.
                MmdUnityFrameApplier.ApplyFrame(instance, frame1);
                Vector2[] uv2 = instance.Mesh.uv;
                Assert.That(uv2[1].x, Is.EqualTo(uv1[1].x).Within(0.00001f));
                Assert.That(uv2[1].y, Is.EqualTo(uv1[1].y).Within(0.00001f));

                // Step 3: Apply zero-weight frame to restore base UV.
                MmdEvaluatedFrame frame0 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame0.morphs.Add(new MmdEvaluatedMorphWeight { name = "uv-shift", weight = 0.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame0);
                Vector2[] uv0 = instance.Mesh.uv;

                Assert.That(uv0[1].x, Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(uv0[1].y, Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(uv0[0].x, Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(uv0[0].y, Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(uv0[2].x, Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(uv0[2].y, Is.EqualTo(0.0f).Within(0.00001f));

                // Step 4: Underlying descriptor base UVs are unchanged.
                Assert.That(instance.RenderingDescriptor.vertices[1].uv[0], Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(instance.RenderingDescriptor.vertices[1].uv[1], Is.EqualTo(0.0f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyTextureUvMorphToSplitSkinnedModelMovesBothCopies()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateSharedVertexTwoSubmeshTextureUvMorphModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                // Confirm split structure and duplicated UV morph offsets.
                Assert.That(instance.Mesh.vertexCount, Is.EqualTo(6));
                Assert.That(instance.Mesh.subMeshCount, Is.EqualTo(2));
                Assert.That(
                    instance.RenderingDescriptor.uvMorphs[0].offsets.Select(offset => offset.vertexIndex),
                    Is.EqualTo(new[] { 0, 3 }));

                // Apply texture UV morph at weight 1.0.
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "uv-shift", weight = 1.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame);
                Vector2[] uv = instance.Mesh.uv;

                // Both split copies of source vertex 0 should receive the same UV delta.
                Assert.That(uv[0].x, Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(uv[0].y, Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(uv[3].x, Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(uv[3].y, Is.EqualTo(0.5f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ExistingSkinnedModelRebindAssignsRuntimeOwnedMeshBeforeVertexMorphApplication()
        {
            MmdUnityModelInstance? sceneInstance = null;
            MmdUnityModelInstance? reboundInstance = null;
            Mesh? originalMesh = null;
            Mesh? reboundMesh = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.morphs.Add(new MmdMorphDefinition
                {
                    index = 0,
                    name = "blink",
                    type = "vertex",
                    panel = "eye",
                    vertexOffsets =
                    {
                        new MmdVertexMorphOffsetDefinition
                        {
                            vertexIndex = 1,
                            positionDelta = new[] { 0.0f, 2.0f, 0.0f }
                        }
                    }
                });

                sceneInstance = MmdUnityModelFactory.CreateSkinnedModel(model);
                originalMesh = sceneInstance.Mesh;
                Vector3 originalVertex = originalMesh.vertices[1];
                reboundInstance = MmdUnityModelFactory.CreateExistingSkinnedModelInstance(sceneInstance.Root, model, sourcePath: null);
                reboundMesh = reboundInstance.Mesh;
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "blink", weight = 1.0f });

                MmdUnityFrameApplier.ApplyFrame(reboundInstance, frame);

                // Existing mesh is preserved (not rebuilt) when it is already valid.
                Assert.That(reboundMesh, Is.SameAs(originalMesh),
                    "existing valid mesh must be preserved, not rebuilt with a new mesh");
                SkinnedMeshRenderer sceneRenderer = RequireSkinnedRenderer(sceneInstance);
                Assert.That(sceneRenderer.sharedMesh, Is.SameAs(reboundMesh));
                int blinkIndex = reboundMesh.GetBlendShapeIndex("blink");
                Assert.That(blinkIndex, Is.GreaterThanOrEqualTo(0));
                SkinnedMeshRenderer reboundRenderer = RequireSkinnedRenderer(reboundInstance);
                Assert.That(reboundRenderer.GetBlendShapeWeight(blinkIndex), Is.EqualTo(100f).Within(0.001f));
                Assert.That(originalMesh.vertices[1], Is.EqualTo(originalVertex));
            }
            finally
            {
                DestroyInstance(sceneInstance);
                // No separate DestroyImmediate needed; sceneInstance owns originalMesh/reboundMesh.
            }
        }

        [Test]
        public void ExistingSkinnedModelRebindAllowsRendererWithoutSharedMesh()
        {
            MmdUnityModelInstance? sceneInstance = null;
            MmdUnityModelInstance? reboundInstance = null;
            Mesh? originalMesh = null;
            Mesh? reboundMesh = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                sceneInstance = MmdUnityModelFactory.CreateSkinnedModel(model);
                originalMesh = sceneInstance.Mesh;
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(sceneInstance);
                renderer.sharedMesh = null;

                reboundInstance = MmdUnityModelFactory.CreateExistingSkinnedModelInstance(sceneInstance.Root, model, sourcePath: null);
                reboundMesh = reboundInstance.Mesh;

                Assert.That(reboundMesh, Is.Not.Null);
                Assert.That(reboundMesh, Is.Not.SameAs(originalMesh));
                Assert.That(renderer.sharedMesh, Is.SameAs(reboundMesh));
                Assert.That(reboundInstance.Root, Is.SameAs(sceneInstance.Root));
                Assert.That(reboundInstance.SkinnedMeshRenderer, Is.SameAs(renderer));
            }
            finally
            {
                DestroyInstance(sceneInstance);
                if (reboundMesh != null && reboundMesh != originalMesh)
                {
                    UnityEngine.Object.DestroyImmediate(reboundMesh);
                }
            }
        }

        [Test]
        public void ExistingSkinnedModelRebindCollectsPhysicsBodiesFromControllerRoot()
        {
            MmdUnityModelInstance? sceneInstance = null;
            MmdUnityModelInstance? reboundInstance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
                {
                    index = 0,
                    name = "child sphere",
                    boneIndex = 1,
                    boneName = "child",
                    shapeType = "sphere",
                    size = new[] { 0.25f, 0.0f, 0.0f },
                    position = new[] { 0.0f, 1.0f, 0.0f },
                    rotation = new[] { 0.0f, 0.0f, 0.0f },
                    mass = 1.0f,
                    linearDamping = 0.0f,
                    angularDamping = 0.0f,
                    friction = 0.0f,
                    restitution = 0.0f,
                    group = 0,
                    mask = 0,
                    physicsKind = "dynamic"
                });

                sceneInstance = MmdUnityModelFactory.CreateSkinnedModel(model);
                SkinnedMeshRenderer originalRenderer = RequireSkinnedRenderer(sceneInstance);
                GameObject rendererObject = new GameObject("Renderer");
                rendererObject.transform.SetParent(originalRenderer.transform, worldPositionStays: false);
                SkinnedMeshRenderer movedRenderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
                movedRenderer.sharedMesh = originalRenderer.sharedMesh;
                movedRenderer.sharedMaterials = originalRenderer.sharedMaterials;
                movedRenderer.bones = originalRenderer.bones;
                movedRenderer.rootBone = originalRenderer.rootBone;
                UnityEngine.Object.DestroyImmediate(originalRenderer);

                reboundInstance = MmdUnityModelFactory.CreateExistingSkinnedModelInstance(sceneInstance.Root, model, sourcePath: null);

                Assert.That(reboundInstance.SkinnedMeshRenderer, Is.SameAs(movedRenderer));
                Assert.That(reboundInstance.PhysicsBodies, Has.Length.EqualTo(1));
                Assert.That(reboundInstance.PhysicsBodies[0].BodyIndex, Is.EqualTo(0));
            }
            finally
            {
                DestroyInstance(sceneInstance);
            }
        }

        [Test]
        public void CreateStaticModelScalesMeshPositionsButNotNormalsOrRootScale()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.vertices[1].position = new[] { 1.0f, 0.0f, 2.0f };
                model.vertices[1].normal = new[] { 0.0f, 0.0f, 1.0f };

                instance = MmdUnityModelFactory.CreateStaticModel(model, sourcePath: null, importScale: 2.5f);

                Vector3[] vertices = instance.Mesh.vertices;
                Vector3[] normals = instance.Mesh.normals;
                // basis conv then * scale
                Assert.That(vertices[1], Is.EqualTo(new Vector3(-2.5f, 0.0f, -5.0f)));
                // normals only basis, no scale
                Assert.That(normals[1], Is.EqualTo(new Vector3(0.0f, 0.0f, -1.0f)));
                Assert.That(instance.Root.transform.localScale, Is.EqualTo(Vector3.one));
                Transform modelRoot = instance.Root.transform.Find("Model");
                Assert.That(modelRoot, Is.Not.Null);
                Assert.That(modelRoot.localScale, Is.EqualTo(Vector3.one));
                Assert.That(instance.ImportScale, Is.EqualTo(2.5f).Within(0.0001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateSkinnedModelBoneBindPositionsAndFrameTranslationDeltasUseSameImportScaleWithoutAccumulation()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                // child origin at (0,1,0) in MMD
                instance = MmdUnityModelFactory.CreateSkinnedModel(model, sourcePath: null, importScale: 0.5f);

                // bind local for child: ToUnity( (0,1,0) - (0,0,0) ) * 0.5 = (0,0.5,0)
                Assert.That(instance.BoneTransforms[1].localPosition, Is.EqualTo(new Vector3(0.0f, 0.5f, 0.0f)));

                // apply delta (0,2,0) MMD -> basis (0,2,0)*0.5 = (0,1,0) added to bind
                MmdUnityFrameApplier.ApplyFrame(instance, CreateFrame(
                    CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f),
                    CreateBonePose(1, "child", 0.0f, 2.0f, 0.0f)));

                Assert.That(instance.BoneTransforms[1].localPosition, Is.EqualTo(new Vector3(0.0f, 1.5f, 0.0f)));

                // re-apply zero delta, back to bind (no accum)
                MmdUnityFrameApplier.ApplyFrame(instance, CreateFrame(
                    CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f),
                    CreateBonePose(1, "child", 0.0f, 0.0f, 0.0f)));

                Assert.That(instance.BoneTransforms[1].localPosition, Is.EqualTo(new Vector3(0.0f, 0.5f, 0.0f)));
                Assert.That(instance.ImportScale, Is.EqualTo(0.5f).Within(0.0001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateSkinnedModelVertexMorphAndBlendShapePositionDeltasScaleWithImportScale()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.morphs.Add(new MmdMorphDefinition
                {
                    index = 0,
                    name = "up",
                    type = "vertex",
                    panel = "eye",
                    vertexOffsets =
                    {
                        new MmdVertexMorphOffsetDefinition
                        {
                            vertexIndex = 1,
                            positionDelta = new[] { 0.0f, 4.0f, 0.0f }
                        }
                    }
                });

                instance = MmdUnityModelFactory.CreateSkinnedModel(model, sourcePath: null, importScale: 0.1f);

                int upIndex = instance.Mesh.GetBlendShapeIndex("up");
                Assert.That(upIndex, Is.GreaterThanOrEqualTo(0));

                Vector3[] deltaVertices = new Vector3[instance.Mesh.vertexCount];
                Vector3[] deltaNormals = new Vector3[instance.Mesh.vertexCount];
                Vector3[] deltaTangents = new Vector3[instance.Mesh.vertexCount];
                instance.Mesh.GetBlendShapeFrameVertices(upIndex, 0, deltaVertices, deltaNormals, deltaTangents);

                // delta (0,4,0) MMD -> basis (0,4,0) * 0.1 = (0, 0.4, 0)
                Assert.That(deltaVertices[1], Is.EqualTo(new Vector3(0.0f, 0.4f, 0.0f)));

                // apply and check
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "up", weight = 1.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Assert.That(renderer.GetBlendShapeWeight(upIndex), Is.EqualTo(100f).Within(0.001f));
                // descriptor remains unscaled
                Assert.That(instance.RenderingDescriptor.vertices[1].position[1], Is.EqualTo(0.0f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void CreateSkinnedModelPhysicsDebugBodyAndColliderScaleWhileDescriptorMetadataUnscaled()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
                {
                    index = 0,
                    name = "test-body",
                    boneIndex = 1,
                    boneName = "child",
                    shapeType = "sphere",
                    size = new[] { 0.5f, 0.0f, 0.0f },
                    position = new[] { 0.0f, 2.0f, 0.0f },
                    rotation = new[] { 0.0f, 0.0f, 0.0f },
                    mass = 1.0f,
                    linearDamping = 0.0f,
                    angularDamping = 0.0f,
                    friction = 0.0f,
                    restitution = 0.0f,
                    group = 0,
                    mask = 0,
                    physicsKind = "dynamic"
                });

                instance = MmdUnityModelFactory.CreateSkinnedModel(model, sourcePath: null, importScale: 0.2f);

                Assert.That(instance.PhysicsBodies, Has.Length.EqualTo(1));
                MmdUnityPhysicsBody body = instance.PhysicsBodies[0];
                // local pos: body(0,2,0) - bone origin child(0,1,0) = (0,1,0) MMD basis -> (0,1,0)*0.2 = (0,0.2,0)
                Assert.That(body.transform.localPosition, Is.EqualTo(new Vector3(0.0f, 0.2f, 0.0f)));
                SphereCollider collider = body.GetComponent<SphereCollider>();
                Assert.That(collider.radius, Is.EqualTo(0.1f).Within(0.00001f)); // 0.5 * 0.2

                // descriptor metadata unscaled
                Assert.That(body.DescriptorSize, Is.EqualTo(new Vector3(0.5f, 0.0f, 0.0f)));
                Assert.That(body.DescriptorPosition, Is.EqualTo(new Vector3(0.0f, 2.0f, 0.0f)));
                Assert.That(instance.ImportScale, Is.EqualTo(0.2f).Within(0.0001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        private static MmdModelDefinition CreateMinimalTriangleModel(bool includeTextureReferences)
        {
            var model = new MmdModelDefinition
            {
                name = "minimal-static-triangle"
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
                origin = new[] { 0.0f, 1.0f, 0.0f },
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
                name = "triangle-material",
                texture = includeTextureReferences ? "diffuse.png" : string.Empty,
                sphereTexture = includeTextureReferences ? "sphere.spa" : string.Empty,
                toonTexture = includeTextureReferences ? "toon.bmp" : string.Empty,
                vertexCount = 3
            });
            return model;
        }

        private static MmdModelDefinition CreateTextureUvMorphTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "texture-uv-morph-triangle"
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
                origin = new[] { 0.0f, 1.0f, 0.0f },
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
                name = "uv-morph-material",
                vertexCount = 3
            });
            // Texture UV morph that moves vertex 1 main UV by (0.25, 0.5).
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "uv-shift",
                type = "texture",
                panel = "other",
                uvOffsets =
                {
                    new MmdUvMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.25f, 0.5f, 0.0f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateSharedVertexTwoSubmeshMorphModel()
        {
            var model = new MmdModelDefinition
            {
                name = "shared-vertex-two-submesh"
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
            MmdVertexDefinition sharedSdefVertex = CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
            sharedSdefVertex.skinningMode = "sdef";
            sharedSdefVertex.boneIndices = new[] { 0, 0 };
            sharedSdefVertex.boneWeights = new[] { 0.6f, 0.4f };
            model.vertices.Add(sharedSdefVertex);
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.vertices.Add(CreateVertex(3, -1.0f, 0.0f, 0.0f, 1.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "first-submesh",
                vertexCount = 3
            });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 1,
                name = "second-submesh",
                vertexCount = 3
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "shared-up",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 0,
                        positionDelta = new[] { 0.0f, 1.0f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateDuplicateNameVertexMorphModel()
        {
            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.name = "duplicate-name-vertex-morph";
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "duplicate",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 1.0f, 0.0f }
                    }
                }
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "duplicate",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 2,
                        positionDelta = new[] { 0.0f, 2.0f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateDuplicateOffsetVertexMorphModel()
        {
            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.name = "duplicate-offset-vertex-morph";
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "stacked",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 0.25f, 0.0f }
                    },
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 0.75f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateSharedVertexTwoSubmeshTextureUvMorphModel()
        {
            var model = new MmdModelDefinition
            {
                name = "shared-vertex-uv-morph"
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
            model.vertices.Add(CreateVertex(3, -1.0f, 0.0f, 0.0f, 1.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "first-submesh",
                vertexCount = 3
            });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 1,
                name = "second-submesh",
                vertexCount = 3
            });
            // Texture UV morph targeting vertex 0 which spans both submeshes.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "uv-shift",
                type = "texture",
                panel = "other",
                uvOffsets =
                {
                    new MmdUvMorphOffsetDefinition
                    {
                        vertexIndex = 0,
                        positionDelta = new[] { 0.25f, 0.5f, 0.0f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateTwoTransparentTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "two-transparent-triangles"
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
            model.vertices.Add(CreateVertex(0, -0.5f, 0.5f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 0.5f, 0.5f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, -0.5f, -0.5f, 0.0f, 0.0f, 1.0f));
            model.vertices.Add(CreateVertex(3, 0.5f, 0.5f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(4, 0.5f, -0.5f, 0.0f, 1.0f, 1.0f));
            model.vertices.Add(CreateVertex(5, -0.5f, -0.5f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2, 3, 4, 5 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "transparent-front-a",
                alpha = 0.45f,
                vertexCount = 3
            });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 1,
                name = "transparent-front-b",
                alpha = 0.45f,
                vertexCount = 3
            });
            return model;
        }

        private static MmdEvaluatedFrame CreateFrame(params MmdEvaluatedBonePose[] bones)
        {
            return new MmdEvaluatedFrame
            {
                frame = 5,
                time = 5.0f / 30.0f,
                bones = new List<MmdEvaluatedBonePose>(bones)
            };
        }

        private static float[] CreateColumnMajorWorldMatrix(Vector3 position, Quaternion rotation)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            return new[]
            {
                matrix[0, 0], matrix[1, 0], matrix[2, 0], matrix[3, 0],
                matrix[0, 1], matrix[1, 1], matrix[2, 1], matrix[3, 1],
                matrix[0, 2], matrix[1, 2], matrix[2, 2], matrix[3, 2],
                matrix[0, 3], matrix[1, 3], matrix[2, 3], matrix[3, 3]
            };
        }

        private static float[] Concatenate(float[] first, float[] second)
        {
            var combined = new float[first.Length + second.Length];
            Array.Copy(first, 0, combined, 0, first.Length);
            Array.Copy(second, 0, combined, first.Length, second.Length);
            return combined;
        }

        private static Quaternion ToUnityModelRotation(Quaternion rotation)
        {
            return new Quaternion(-rotation.x, rotation.y, -rotation.z, rotation.w);
        }

        private static MmdMotionDefinition CreateRootTranslationMotion()
        {
            var motion = new MmdMotionDefinition
            {
                targetModelName = "minimal-static-triangle",
                maxFrame = 10
            };
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = 0,
                translation = new[] { 0.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = LinearInterpolation()
            });
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = 10,
                translation = new[] { 2.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = LinearInterpolation()
            });
            return motion;
        }

        private static MmdMotionDefinition CreateBlinkMorphMotion()
        {
            var motion = new MmdMotionDefinition
            {
                targetModelName = "minimal-static-triangle",
                maxFrame = 10
            };
            motion.morphKeyframes.Add(new MmdMorphKeyframeDefinition
            {
                morphName = "blink",
                frame = 0,
                weight = 0.0f
            });
            motion.morphKeyframes.Add(new MmdMorphKeyframeDefinition
            {
                morphName = "blink",
                frame = 10,
                weight = 1.0f
            });
            return motion;
        }

        private static MmdBoneInterpolationDefinition LinearInterpolation()
        {
            byte[] linear = { 20, 20, 107, 107 };
            return new MmdBoneInterpolationDefinition
            {
                translationX = linear,
                translationY = linear,
                translationZ = linear,
                rotation = linear
            };
        }

        private static MmdEvaluatedBonePose CreateBonePose(int index, string name, float x, float y, float z)
        {
            return new MmdEvaluatedBonePose
            {
                index = index,
                name = name,
                localPosition = new[] { x, y, z },
                localRotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                localScale = new[] { 1.0f, 1.0f, 1.0f },
                worldMatrix = new[]
                {
                    1.0f, 0.0f, 0.0f, x,
                    0.0f, 1.0f, 0.0f, y,
                    0.0f, 0.0f, 1.0f, z,
                    0.0f, 0.0f, 0.0f, 1.0f
                }
            };
        }

        private static MmdModelDefinition CreateTexturedQuadModel(string texture)
        {
            var model = new MmdModelDefinition
            {
                name = "viewport-texture-orientation-quad"
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
            model.vertices.Add(CreateVertex(0, -1.0f, 1.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 1.0f, -1.0f, 0.0f, 1.0f, 1.0f));
            model.vertices.Add(CreateVertex(3, -1.0f, -1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "viewport-texture-orientation-material",
                texture = texture,
                vertexCount = 6
            });
            return model;
        }

        private static string CreateTempDirectory()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "yohawing-mmd-unity-tests", Path.GetRandomFileName());
            Directory.CreateDirectory(tempRoot);
            return tempRoot;
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
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void WriteCutoutPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixels(new[]
                {
                    new Color(1.0f, 1.0f, 1.0f, 1.0f),
                    new Color(1.0f, 1.0f, 1.0f, 0.0f),
                    new Color(1.0f, 1.0f, 1.0f, 1.0f),
                    new Color(1.0f, 1.0f, 1.0f, 0.0f)
                });
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void WriteAtlasPaddingPng(string path)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false);
            try
            {
                var pixels = new Color[16];
                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        bool inner = x >= 1 && x <= 2 && y >= 1 && y <= 2;
                        pixels[y * 4 + x] = inner
                            ? new Color(1.0f, 1.0f, 1.0f, 1.0f)
                            : new Color(1.0f, 1.0f, 1.0f, 0.0f);
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void WriteVerticalOrientationPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixels(new[]
                {
                    Color.blue, Color.blue,
                    Color.red, Color.red
                });
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void WriteGrayscalePng(string path)
        {
            WriteMinimalPng(
                path,
                width: 2,
                height: 2,
                colorType: 0,
                bytesPerPixel: 1,
                pixelBytes: new byte[]
                {
                    0, 64,
                    128, 255
                });
        }

        private static void WriteRgbPng(string path)
        {
            WriteMinimalPng(
                path,
                width: 2,
                height: 2,
                colorType: 2,
                bytesPerPixel: 3,
                pixelBytes: new byte[]
                {
                    255, 0, 0,   0, 255, 0,
                    0, 0, 255,   128, 128, 128
                });
        }

        private static void WriteMinimalPng(
            string path,
            int width,
            int height,
            byte colorType,
            int bytesPerPixel,
            byte[] pixelBytes)
        {
            byte[] scanlines = new byte[(width * bytesPerPixel + 1) * height];
            int source = 0;
            int destination = 0;
            for (int y = 0; y < height; y++)
            {
                scanlines[destination++] = 0;
                int rowBytes = width * bytesPerPixel;
                Array.Copy(pixelBytes, source, scanlines, destination, rowBytes);
                source += rowBytes;
                destination += rowBytes;
            }

            using var stream = new MemoryStream();
            stream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);

            using (var ihdr = new MemoryStream())
            {
                WriteInt32BigEndian(ihdr, width);
                WriteInt32BigEndian(ihdr, height);
                ihdr.WriteByte(8);
                ihdr.WriteByte(colorType);
                ihdr.WriteByte(0);
                ihdr.WriteByte(0);
                ihdr.WriteByte(0);
                WritePngChunk(stream, "IHDR", ihdr.ToArray());
            }

            WritePngChunk(stream, "IDAT", CreateZlibStream(scanlines));
            WritePngChunk(stream, "IEND", Array.Empty<byte>());
            File.WriteAllBytes(path, stream.ToArray());
        }

        private static byte[] CreateZlibStream(byte[] data)
        {
            using var stream = new MemoryStream();
            stream.WriteByte(0x78);
            stream.WriteByte(0x01);
            using (var deflate = new DeflateStream(stream, System.IO.Compression.CompressionLevel.NoCompression, leaveOpen: true))
            {
                deflate.Write(data, 0, data.Length);
            }

            uint adler = Adler32(data);
            WriteInt32BigEndian(stream, unchecked((int)adler));
            return stream.ToArray();
        }

        private static uint Adler32(byte[] data)
        {
            const uint Mod = 65521;
            uint a = 1;
            uint b = 0;
            foreach (byte value in data)
            {
                a = (a + value) % Mod;
                b = (b + a) % Mod;
            }

            return (b << 16) | a;
        }

        private static void WritePngChunk(Stream stream, string type, byte[] data)
        {
            WriteInt32BigEndian(stream, data.Length);
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes, 0, typeBytes.Length);
            stream.Write(data, 0, data.Length);
            WriteInt32BigEndian(stream, 0);
        }

        private static void WriteInt32BigEndian(Stream stream, int value)
        {
            stream.WriteByte((byte)((value >> 24) & 0xff));
            stream.WriteByte((byte)((value >> 16) & 0xff));
            stream.WriteByte((byte)((value >> 8) & 0xff));
            stream.WriteByte((byte)(value & 0xff));
        }

        private static void WriteJpg(string path, Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixels(new[] { color, color, color, color });
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToJPG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void WriteBmp24(string path, int width, int height)
        {
            int rowStride = ((width * 3 + 3) / 4) * 4;
            int pixelBytes = rowStride * height;
            byte[] bytes = new byte[54 + pixelBytes];
            bytes[0] = (byte)'B';
            bytes[1] = (byte)'M';
            WriteInt32LittleEndian(bytes, 2, bytes.Length);
            WriteInt32LittleEndian(bytes, 10, 54);
            WriteInt32LittleEndian(bytes, 14, 40);
            WriteInt32LittleEndian(bytes, 18, width);
            WriteInt32LittleEndian(bytes, 22, height);
            bytes[26] = 1;
            bytes[28] = 24;
            WriteInt32LittleEndian(bytes, 34, pixelBytes);

            int cursor = 54;
            for (int y = 0; y < height; y++)
            {
                int rowStart = cursor;
                for (int x = 0; x < width; x++)
                {
                    bytes[cursor++] = 255;
                    bytes[cursor++] = 0;
                    bytes[cursor++] = 0;
                }

                cursor = rowStart + rowStride;
            }

            File.WriteAllBytes(path, bytes);
        }

        private static void WriteBmp8Indexed(string path, int width, int height)
        {
            int rowStride = ((width + 3) / 4) * 4;
            int paletteBytes = 256 * 4;
            int pixelOffset = 54 + paletteBytes;
            int pixelBytes = rowStride * height;
            byte[] bytes = new byte[pixelOffset + pixelBytes];
            bytes[0] = (byte)'B';
            bytes[1] = (byte)'M';
            WriteInt32LittleEndian(bytes, 2, bytes.Length);
            WriteInt32LittleEndian(bytes, 10, pixelOffset);
            WriteInt32LittleEndian(bytes, 14, 40);
            WriteInt32LittleEndian(bytes, 18, width);
            WriteInt32LittleEndian(bytes, 22, height);
            bytes[26] = 1;
            bytes[28] = 8;
            WriteInt32LittleEndian(bytes, 34, pixelBytes);
            WriteInt32LittleEndian(bytes, 46, 256);

            int paletteCursor = 54;
            for (int i = 0; i < 256; i++)
            {
                bytes[paletteCursor++] = (byte)i;
                bytes[paletteCursor++] = 0;
                bytes[paletteCursor++] = (byte)(255 - i);
                bytes[paletteCursor++] = 0;
            }

            int cursor = pixelOffset;
            for (int y = 0; y < height; y++)
            {
                int rowStart = cursor;
                for (int x = 0; x < width; x++)
                {
                    bytes[cursor++] = (byte)((x + y) & 0xff);
                }

                cursor = rowStart + rowStride;
            }

            File.WriteAllBytes(path, bytes);
        }

        private static void WriteInt32LittleEndian(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)(value & 0xff);
            bytes[offset + 1] = (byte)((value >> 8) & 0xff);
            bytes[offset + 2] = (byte)((value >> 16) & 0xff);
            bytes[offset + 3] = (byte)((value >> 24) & 0xff);
        }

        private static void WriteTga24(string path, int width, int height)
        {
            byte[] bytes = new byte[18 + width * height * 3];
            bytes[2] = 2;
            bytes[12] = (byte)(width & 0xff);
            bytes[13] = (byte)((width >> 8) & 0xff);
            bytes[14] = (byte)(height & 0xff);
            bytes[15] = (byte)((height >> 8) & 0xff);
            bytes[16] = 24;
            bytes[17] = 0x20;

            int cursor = 18;
            for (int i = 0; i < width * height; i++)
            {
                bytes[cursor++] = 0;
                bytes[cursor++] = 0;
                bytes[cursor++] = 255;
            }

            File.WriteAllBytes(path, bytes);
        }

        private static void WriteTga32Alpha(string path, int width, int height, byte alpha)
        {
            byte[] bytes = new byte[18 + width * height * 4];
            bytes[2] = 2;
            bytes[12] = (byte)(width & 0xff);
            bytes[13] = (byte)((width >> 8) & 0xff);
            bytes[14] = (byte)(height & 0xff);
            bytes[15] = (byte)((height >> 8) & 0xff);
            bytes[16] = 32;
            bytes[17] = 0x28;

            int cursor = 18;
            for (int i = 0; i < width * height; i++)
            {
                bytes[cursor++] = 255;
                bytes[cursor++] = 255;
                bytes[cursor++] = 255;
                bytes[cursor++] = alpha;
            }

            File.WriteAllBytes(path, bytes);
        }

        private static void WriteDdsDxt3(string path)
        {
            byte[] bytes = new byte[128 + 16];
            bytes[0] = (byte)'D';
            bytes[1] = (byte)'D';
            bytes[2] = (byte)'S';
            bytes[3] = (byte)' ';
            WriteInt32LittleEndian(bytes, 4, 124);
            WriteInt32LittleEndian(bytes, 8, 0x0002100f);
            WriteInt32LittleEndian(bytes, 12, 4);
            WriteInt32LittleEndian(bytes, 16, 4);
            WriteInt32LittleEndian(bytes, 20, 16);
            WriteInt32LittleEndian(bytes, 28, 1);
            WriteInt32LittleEndian(bytes, 76, 32);
            WriteInt32LittleEndian(bytes, 80, 0x00000004);
            bytes[84] = (byte)'D';
            bytes[85] = (byte)'X';
            bytes[86] = (byte)'T';
            bytes[87] = (byte)'3';
            WriteInt32LittleEndian(bytes, 108, 0x00001000);

            int cursor = 128;
            for (int i = 0; i < 8; i++)
            {
                bytes[cursor++] = 0xff;
            }

            WriteUInt16LittleEndian(bytes, cursor, 0xf800);
            WriteUInt16LittleEndian(bytes, cursor + 2, 0x001f);
            WriteInt32LittleEndian(bytes, cursor + 4, 0);
            File.WriteAllBytes(path, bytes);
        }

        private static void WriteUInt16LittleEndian(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)(value & 0xff);
            bytes[offset + 1] = (byte)((value >> 8) & 0xff);
        }

        private static Texture? ReadBoundDiffuseTexture(Material material)
        {
            if (material.HasProperty("_BaseMap"))
            {
                Texture texture = material.GetTexture("_BaseMap");
                if (texture != null)
                {
                    return texture;
                }
            }

            return material.HasProperty("_MainTex")
                ? material.GetTexture("_MainTex")
                : null;
        }

        private static Texture? ReadMaterialTexture(Material material, string propertyName)
        {
            return material.HasProperty(propertyName)
                ? material.GetTexture(propertyName)
                : null;
        }

        private static float ReadMaterialAlpha(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor").a;
            }

            return material.HasProperty("_Color")
                ? material.GetColor("_Color").a
                : 1.0f;
        }

        private static float ReadMaterialFloat(Material material, string propertyName)
        {
            return material.HasProperty(propertyName)
                ? material.GetFloat(propertyName)
                : float.NaN;
        }

        private static Color ReadMaterialColor(Material material, string propertyName)
        {
            return material.HasProperty(propertyName)
                ? material.GetColor(propertyName)
                : Color.clear;
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

        [Test]
        public void ApplyFrameExpandsGroupMorphWeightToTargetVertexMorph()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateGroupMorphTriangleModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                // Frame with group morph weight only, no direct vertex morph weight.
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 1.0f });

                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                // groupWeight(1.0) * offsetWeight(0.5) = resolved smile weight 0.5 -> BlendShape 50f
                int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
                Assert.That(smileIndex, Is.GreaterThanOrEqualTo(0));
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Assert.That(renderer.GetBlendShapeWeight(smileIndex), Is.EqualTo(50f).Within(0.001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void FastRuntimeMorphFrameDoesNotReExpandNativeResolvedGroupMorphWeights()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateGroupMorphTriangleModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
                Assert.That(smileIndex, Is.GreaterThanOrEqualTo(0));

                // Reproduce the native fast-runtime morph weight array for a VMD that drives the GROUP
                // morph "happy-face" at 1.0. RuntimeInstance::expand_group_morphs writes the expanded
                // member weight into "smile" (1.0 * 0.5 = 0.5) AND leaves the group morph's own weight
                // (1.0) in the array; the fast binding feeds BOTH into the applier.
                MmdEvaluatedFrame fastFrame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                fastFrame.morphs.Add(new MmdEvaluatedMorphWeight { name = "smile", weight = 0.5f });
                fastFrame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 1.0f });

                // Fast path: group morphs are already resolved by the native runtime, so the applier
                // must NOT expand them again. The native-resolved smile weight stays 0.5 -> BlendShape 50f.
                MmdUnityFrameApplier.ApplyMorphs(instance, fastFrame, groupMorphsResolvedExternally: true);
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Assert.That(
                    renderer.GetBlendShapeWeight(smileIndex),
                    Is.EqualTo(50f).Within(0.001f),
                    "Fast path must apply the native-resolved member weight as-is, not re-expand the group.");

                // Regression guard: the default (managed) resolution WOULD re-expand the residual group
                // weight, doubling "smile" to 1.0 -> BlendShape 100f. That over-driven blend shape is
                // exactly the bug the fast path must avoid.
                MmdUnityFrameApplier.ApplyMorphs(instance, fastFrame);
                Assert.That(
                    renderer.GetBlendShapeWeight(smileIndex),
                    Is.EqualTo(100f).Within(0.001f),
                    "Managed group resolution double-applies an already-resolved group weight (documents the bug).");
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyFrameSumsDirectVertexWeightAndGroupContribution()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateGroupMorphTriangleModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                // Frame with both direct vertex morph weight and group morph weight.
                // Direct "smile" at 1.0 + group "happy-face" at 0.5 targeting "smile" with offset 0.5.
                // Final smile weight = 1.0 + 0.5 * 0.5 = 1.25
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "smile", weight = 1.0f });
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 0.5f });

                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                // smile(1.0) + happy-face(0.5) * 0.5 = resolved smile 1.25 -> BlendShape 125f
                int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Assert.That(renderer.GetBlendShapeWeight(smileIndex), Is.EqualTo(125f).Within(0.001f));
                Assert.That(renderer.localBounds.Contains(new Vector3(-1.0f, 2.5f, 0.0f)), Is.True);
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void RepeatedApplyFrameDoesNotAccumulateGroupMorphDeltas()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateGroupMorphTriangleModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 1.0f });

                int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
                Assert.That(smileIndex, Is.GreaterThanOrEqualTo(0));

                // First apply.
                MmdUnityFrameApplier.ApplyFrame(instance, frame);
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                float firstWeight = renderer.GetBlendShapeWeight(smileIndex);

                // Second apply of the same frame.
                MmdUnityFrameApplier.ApplyFrame(instance, frame);
                float secondWeight = renderer.GetBlendShapeWeight(smileIndex);

                // groupWeight(1.0) * offsetWeight(0.5) = 0.5 -> BlendShape 50f; must not accumulate
                Assert.That(firstWeight, Is.EqualTo(50f).Within(0.001f));
                Assert.That(secondWeight, Is.EqualTo(firstWeight).Within(0.001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ZeroGroupMorphWeightRestoresBaseShape()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateGroupMorphTriangleModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
                Assert.That(smileIndex, Is.GreaterThanOrEqualTo(0));

                // Apply with group morph weight.
                MmdEvaluatedFrame frame1 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame1.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 1.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame1);
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Assert.That(renderer.GetBlendShapeWeight(smileIndex), Is.EqualTo(50f).Within(0.001f));

                // Apply with zero group morph weight.
                MmdEvaluatedFrame frame0 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame0.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 0.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame0);

                Assert.That(renderer.GetBlendShapeWeight(smileIndex), Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyFrameWithGroupMorphOnSplitSkinnedModelMovesBothCopies()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateSplitGroupMorphModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                Assert.That(instance.Mesh.vertexCount, Is.EqualTo(6));
                Assert.That(instance.Mesh.subMeshCount, Is.EqualTo(2));

                // Frame with group morph weight only.
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 1.0f });

                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                // group "happy-face"(1.0) targets "shared-up"(1.0) -> resolved weight 1.0 -> BlendShape 100f
                int sharedUpIndex = instance.Mesh.GetBlendShapeIndex("shared-up");
                Assert.That(sharedUpIndex, Is.GreaterThanOrEqualTo(0));
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Assert.That(renderer.GetBlendShapeWeight(sharedUpIndex), Is.EqualTo(100f).Within(0.001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void GroupMorphCycleDetectionThrows()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateCycleGroupMorphModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "loop-a", weight = 1.0f });

                var ex = Assert.Throws<InvalidOperationException>(() =>
                    MmdUnityFrameApplier.ApplyFrame(instance, frame));

                Assert.That(ex.Message, Does.Contain("cycle"));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyFrameWithMaterialMorphMutatesMaterialProperties()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMaterialMorphTriangleModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                // Base values before morph application.
                Color baseColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Color baseAmbient = ReadMaterialColor(instance.Materials[0], "_AmbientColor");
                Color baseOutline = ReadMaterialColor(instance.Materials[0], "_OutlineColor");
                float baseOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

                Assert.That(baseColor.r, Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(baseColor.g, Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(baseColor.a, Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(baseAmbient.r, Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(baseAmbient.g, Is.EqualTo(0.3f).Within(0.00001f));
                Assert.That(baseOutline.a, Is.EqualTo(1.0f).Within(0.00001f));
                // Outline width is the raw PMX edgeSize (screen-space pixel width), base edgeSize = 1.0.
                Assert.That(baseOutlineWidth, Is.EqualTo(1.0f).Within(0.00001f));

                // Apply material morph at weight 1.0.
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "color-change", weight = 1.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                Color morphedColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Color morphedAmbient = ReadMaterialColor(instance.Materials[0], "_AmbientColor");
                Color morphedOutline = ReadMaterialColor(instance.Materials[0], "_OutlineColor");
                float morphedOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

                // diffuseColor: base(0.8,0.2,0.6) + offset(0.0,0.5,0.0)*1.0 = (0.8,0.7,0.6)
                Assert.That(morphedColor.r, Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(morphedColor.g, Is.EqualTo(0.7f).Within(0.00001f));
                Assert.That(morphedColor.b, Is.EqualTo(0.6f).Within(0.00001f));
                // alpha: base(1.0) + opacity(-0.3)*1.0 = 0.7
                Assert.That(morphedColor.a, Is.EqualTo(0.7f).Within(0.00001f));

                // ambientColor: base(0.1,0.3,0.5) + offset(0.0,0.0,0.4)*1.0 = (0.1,0.3,0.9)
                Assert.That(morphedAmbient.r, Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(morphedAmbient.g, Is.EqualTo(0.3f).Within(0.00001f));
                Assert.That(morphedAmbient.b, Is.EqualTo(0.9f).Within(0.00001f));

                // edgeColor: base(0.0,0.0,0.0,1.0) + offset(0.5,0.0,0.0)*1.0 + opacity(0.0)*1.0 = (0.5,0.0,0.0,1.0)
                Assert.That(morphedOutline.r, Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(morphedOutline.g, Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(morphedOutline.b, Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(morphedOutline.a, Is.EqualTo(1.0f).Within(0.00001f));

                // edgeSize: base(1.0) + offset(2.0)*1.0 = 3.0 -> _OutlineWidth = raw edgeSize = 3.0
                Assert.That(morphedOutlineWidth, Is.EqualTo(3.0f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyMaterialMorphTwiceDoesNotAccumulate()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMaterialMorphTriangleModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "color-change", weight = 1.0f });

                // First apply.
                MmdUnityFrameApplier.ApplyFrame(instance, frame);
                Color firstColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                float firstOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

                // Second apply of the same frame.
                MmdUnityFrameApplier.ApplyFrame(instance, frame);
                Color secondColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                float secondOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

                Assert.That(secondColor.r, Is.EqualTo(firstColor.r).Within(0.00001f));
                Assert.That(secondColor.g, Is.EqualTo(firstColor.g).Within(0.00001f));
                Assert.That(secondColor.b, Is.EqualTo(firstColor.b).Within(0.00001f));
                Assert.That(secondColor.a, Is.EqualTo(firstColor.a).Within(0.00001f));
                Assert.That(secondOutlineWidth, Is.EqualTo(firstOutlineWidth).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyMaterialMorphWithZeroWeightRestoresBaseMaterialValues()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMaterialMorphTriangleModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                // Record base values.
                Color baseColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                float baseOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

                // Apply morph at full weight.
                MmdEvaluatedFrame frame1 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame1.morphs.Add(new MmdEvaluatedMorphWeight { name = "color-change", weight = 1.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame1);

                Color morphedColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                float morphedOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");
                Assert.That(morphedColor.g, Is.EqualTo(0.7f).Within(0.00001f), "Material should be morphed before zero-weight apply");
                Assert.That(morphedOutlineWidth, Is.EqualTo(3.0f).Within(0.00001f), "Outline width should be morphed");

                // Apply morph at zero weight.
                MmdEvaluatedFrame frame0 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame0.morphs.Add(new MmdEvaluatedMorphWeight { name = "color-change", weight = 0.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame0);

                Color restoredColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                float restoredOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

                Assert.That(restoredColor.r, Is.EqualTo(baseColor.r).Within(0.00001f));
                Assert.That(restoredColor.g, Is.EqualTo(baseColor.g).Within(0.00001f));
                Assert.That(restoredColor.b, Is.EqualTo(baseColor.b).Within(0.00001f));
                Assert.That(restoredColor.a, Is.EqualTo(baseColor.a).Within(0.00001f));
                Assert.That(restoredOutlineWidth, Is.EqualTo(baseOutlineWidth).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void GroupMorphWeightExpansionDrivesMaterialMorph()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateGroupMaterialMorphModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                // Frame has no direct material morph weight, only group morph weight.
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "mood-group", weight = 1.0f });

                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                // Group morph "mood-group" targets "color-change" with weight 0.8.
                // Resolved "color-change" weight = 1.0 * 0.8 = 0.8.
                // diffuseColor: base(0.8,0.2,0.6) + offset(0.0,0.5,0.0)*0.8 = (0.8,0.6,0.6)
                Color morphedColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Assert.That(morphedColor.r, Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(morphedColor.g, Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(morphedColor.b, Is.EqualTo(0.6f).Within(0.00001f));
                // alpha: 1.0 + (-0.3)*0.8 = 0.76
                Assert.That(morphedColor.a, Is.EqualTo(0.76f).Within(0.00001f));

                // edgeSize: 1.0 + 2.0*0.8 = 2.6 -> _OutlineWidth = raw edgeSize = 2.6
                float morphedOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");
                Assert.That(morphedOutlineWidth, Is.EqualTo(2.6f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyMaterialMorphMultiplyMutatesMaterialProperties()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMaterialMorphMultiplyModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                // Base values before morph application.
                Color baseColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Assert.That(baseColor.r, Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(baseColor.g, Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(baseColor.b, Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(baseColor.a, Is.EqualTo(1.0f).Within(0.00001f));

                // Apply multiply morph at weight 1.0.
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "multiply-change", weight = 1.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                Color morphedColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Color morphedAmbient = ReadMaterialColor(instance.Materials[0], "_AmbientColor");
                Color morphedOutline = ReadMaterialColor(instance.Materials[0], "_OutlineColor");
                float morphedOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

                // diffuseColor: base(0.8,0.2,0.6) * offset(0.5,2.0,0.75) = (0.4,0.4,0.45)
                Assert.That(morphedColor.r, Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(morphedColor.g, Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(morphedColor.b, Is.EqualTo(0.45f).Within(0.00001f));
                // alpha: 1.0 * 0.5 = 0.5
                Assert.That(morphedColor.a, Is.EqualTo(0.5f).Within(0.00001f));

                // ambientColor: base(0.1,0.3,0.5) * offset(2.0,0.5,1.0) = (0.2,0.15,0.5)
                Assert.That(morphedAmbient.r, Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(morphedAmbient.g, Is.EqualTo(0.15f).Within(0.00001f));
                Assert.That(morphedAmbient.b, Is.EqualTo(0.5f).Within(0.00001f));

                // edgeColor: base(0,0,0,1) * offset(0.5,0.5,0.5,0) = (0,0,0,0)
                Assert.That(morphedOutline.r, Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(morphedOutline.g, Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(morphedOutline.b, Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(morphedOutline.a, Is.EqualTo(0.0f).Within(0.00001f));

                // edgeSize: 1.0 * 2.0 = 2.0 -> _OutlineWidth = raw edgeSize = 2.0
                Assert.That(morphedOutlineWidth, Is.EqualTo(2.0f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyMaterialMorphAllMaterialAddTargetMutatesAllMaterials()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMaterialMorphAllMaterialAddModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                Assert.That(instance.Materials, Has.Length.EqualTo(2));

                // Apply add morph at weight 1.0 (materialIndex = -1 => all materials).
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "all-add", weight = 1.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                // Material 0: base(0.8,0.2,0.6) + offset(0.0,0.5,0.0) = (0.8,0.7,0.6)
                Color mat0Color = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Assert.That(mat0Color.r, Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(mat0Color.g, Is.EqualTo(0.7f).Within(0.00001f));
                Assert.That(mat0Color.b, Is.EqualTo(0.6f).Within(0.00001f));
                // alpha: 1.0 + (-0.3) = 0.7
                Assert.That(mat0Color.a, Is.EqualTo(0.7f).Within(0.00001f));

                // Material 1: base(0.9,0.7,0.4) + offset(0.0,0.5,0.0) = (0.9,1.0,0.4)
                Color mat1Color = ReadMaterialColor(instance.Materials[1], "_BaseColor");
                Assert.That(mat1Color.r, Is.EqualTo(0.9f).Within(0.00001f));
                Assert.That(mat1Color.g, Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(mat1Color.b, Is.EqualTo(0.4f).Within(0.00001f));
                // alpha: 0.8 + (-0.3) = 0.5
                Assert.That(mat1Color.a, Is.EqualTo(0.5f).Within(0.00001f));

                // Both materials edge size changed: material 0: 1.0+2.0=3.0, material 1: 0.5+2.0=2.5
                // (_OutlineWidth = raw edgeSize)
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_OutlineWidth"), Is.EqualTo(3.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[1], "_OutlineWidth"), Is.EqualTo(2.5f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyMaterialMorphMultiplyAllMaterialTargetMutatesAllMaterials()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMaterialMorphMultiplyAllMaterialModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                Assert.That(instance.Materials, Has.Length.EqualTo(2));

                // Apply multiply morph at weight 1.0 (materialIndex = -1 => all materials).
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "all-multiply", weight = 1.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                // Material 0: base(0.8,0.2,0.6) * offset(0.5,1.5,0.75) = (0.4,0.3,0.45)
                Color mat0Color = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Assert.That(mat0Color.r, Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(mat0Color.g, Is.EqualTo(0.3f).Within(0.00001f));
                Assert.That(mat0Color.b, Is.EqualTo(0.45f).Within(0.00001f));
                // alpha: 1.0 * 0.5 = 0.5
                Assert.That(mat0Color.a, Is.EqualTo(0.5f).Within(0.00001f));

                // Material 1: base(0.9,0.7,0.4) * offset(0.5,1.5,0.75) = (0.45,1.0,0.3)
                Color mat1Color = ReadMaterialColor(instance.Materials[1], "_BaseColor");
                Assert.That(mat1Color.r, Is.EqualTo(0.45f).Within(0.00001f));
                Assert.That(mat1Color.g, Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(mat1Color.b, Is.EqualTo(0.3f).Within(0.00001f));
                // alpha: 0.8 * 0.5 = 0.4
                Assert.That(mat1Color.a, Is.EqualTo(0.4f).Within(0.00001f));

                // Material 0: base ambient(0.1,0.3,0.5) * offset(2.0,0.5,1.0) = (0.2,0.15,0.5)
                Color mat0Ambient = ReadMaterialColor(instance.Materials[0], "_AmbientColor");
                Assert.That(mat0Ambient.r, Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(mat0Ambient.g, Is.EqualTo(0.15f).Within(0.00001f));
                Assert.That(mat0Ambient.b, Is.EqualTo(0.5f).Within(0.00001f));

                // Material 1: base ambient(0.2,0.1,0.3) * offset(2.0,0.5,1.0) = (0.4,0.05,0.3)
                Color mat1Ambient = ReadMaterialColor(instance.Materials[1], "_AmbientColor");
                Assert.That(mat1Ambient.r, Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(mat1Ambient.g, Is.EqualTo(0.05f).Within(0.00001f));
                Assert.That(mat1Ambient.b, Is.EqualTo(0.3f).Within(0.00001f));

                // Both materials edge size changed (_OutlineWidth = raw edgeSize):
                // material 0: 1.0*2.0=2.0, material 1: 0.5*2.0=1.0
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_OutlineWidth"), Is.EqualTo(2.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[1], "_OutlineWidth"), Is.EqualTo(1.0f).Within(0.00001f));

                // Material 0: edgeColor(0,0,0,1) * offset(0.5,0.5,0.5,0.2) = (0,0,0,0.2)
                Assert.That(ReadMaterialColor(instance.Materials[0], "_OutlineColor").a, Is.EqualTo(0.2f).Within(0.00001f));
                // Material 1: edgeColor(0,0,0,0.9) * offset(0.5,0.5,0.5,0.2) = (0,0,0,0.18)
                Assert.That(ReadMaterialColor(instance.Materials[1], "_OutlineColor").a, Is.EqualTo(0.18f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void MultiplyAllMaterialMorphDoesNotAccumulateAndRestoresOnZeroWeight()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMaterialMorphMultiplyAllMaterialModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                Assert.That(instance.Materials, Has.Length.EqualTo(2));

                // Record base values.
                Color base0Color = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Color base1Color = ReadMaterialColor(instance.Materials[1], "_BaseColor");
                float base0Outline = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

                // Step 1: Apply multiply all-material morph at weight 1.0.
                MmdEvaluatedFrame frame1 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame1.morphs.Add(new MmdEvaluatedMorphWeight { name = "all-multiply", weight = 1.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame1);

                Color firstMat0Color = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Assert.That(firstMat0Color.r, Is.EqualTo(0.4f).Within(0.00001f), "First apply should morph material 0");

                // Step 2: apply the same frame again; it must not accumulate.
                MmdUnityFrameApplier.ApplyFrame(instance, frame1);
                Color secondMat0Color = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Assert.That(secondMat0Color.r, Is.EqualTo(firstMat0Color.r).Within(0.00001f), "Repeated apply should not accumulate");
                Assert.That(secondMat0Color.g, Is.EqualTo(firstMat0Color.g).Within(0.00001f));
                Assert.That(secondMat0Color.b, Is.EqualTo(firstMat0Color.b).Within(0.00001f));
                Assert.That(secondMat0Color.a, Is.EqualTo(firstMat0Color.a).Within(0.00001f));

                // Step 3: Apply zero-weight frame to restore base values.
                MmdEvaluatedFrame frame0 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame0.morphs.Add(new MmdEvaluatedMorphWeight { name = "all-multiply", weight = 0.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame0);

                Color restored0Color = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Color restored1Color = ReadMaterialColor(instance.Materials[1], "_BaseColor");
                float restored0Outline = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

                Assert.That(restored0Color.r, Is.EqualTo(base0Color.r).Within(0.00001f), "Zero weight should restore material 0 diffuse red");
                Assert.That(restored0Color.g, Is.EqualTo(base0Color.g).Within(0.00001f));
                Assert.That(restored0Color.b, Is.EqualTo(base0Color.b).Within(0.00001f));
                Assert.That(restored0Color.a, Is.EqualTo(base0Color.a).Within(0.00001f));
                Assert.That(restored1Color.r, Is.EqualTo(base1Color.r).Within(0.00001f), "Zero weight should restore material 1 diffuse red");
                Assert.That(restored1Color.a, Is.EqualTo(base1Color.a).Within(0.00001f), "Zero weight should restore material 1 alpha");
                Assert.That(restored0Outline, Is.EqualTo(base0Outline).Within(0.00001f), "Zero weight should restore material 0 outline width");
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        private static MmdModelDefinition CreateMaterialMorphMultiplyModel()
        {
            var model = new MmdModelDefinition
            {
                name = "material-morph-multiply"
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
                origin = new[] { 0.0f, 1.0f, 0.0f },
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
                name = "multiply-target",
                alpha = 1.0f,
                diffuseColor = new[] { 0.8f, 0.2f, 0.6f },
                ambientColor = new[] { 0.1f, 0.3f, 0.5f },
                edgeColor = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                edgeSize = 1.0f,
                drawEdgeFlag = true,
                vertexCount = 3
            });
            // Material morph: "multiply-change" with operation "multiply".
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "multiply-change",
                type = "material",
                panel = "other",
                materialOffsets =
                {
                    new MmdMaterialMorphOffsetDefinition
                    {
                        materialIndex = 0,
                        operation = "multiply",
                        diffuseColor = new[] { 0.5f, 2.0f, 0.75f },
                        diffuseOpacity = 0.5f,
                        ambientColor = new[] { 2.0f, 0.5f, 1.0f },
                        specularColor = new[] { 1.0f, 1.0f, 1.0f },
                        specularPower = 0.0f,
                        edgeColor = new[] { 0.5f, 0.5f, 0.5f },
                        edgeOpacity = 0.0f,
                        edgeSize = 2.0f,
                        diffuseTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        sphereTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        toonTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateTwoMaterialTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "two-material-triangle"
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
                origin = new[] { 0.0f, 1.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.vertices.Add(CreateVertex(3, -1.0f, 0.0f, 0.0f, 1.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "material-a",
                alpha = 1.0f,
                diffuseColor = new[] { 0.8f, 0.2f, 0.6f },
                ambientColor = new[] { 0.1f, 0.3f, 0.5f },
                edgeColor = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                edgeSize = 1.0f,
                drawEdgeFlag = true,
                vertexCount = 3
            });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 1,
                name = "material-b",
                alpha = 0.8f,
                diffuseColor = new[] { 0.9f, 0.7f, 0.4f },
                ambientColor = new[] { 0.2f, 0.1f, 0.3f },
                edgeColor = new[] { 0.0f, 0.0f, 0.0f, 0.9f },
                edgeSize = 0.5f,
                drawEdgeFlag = true,
                vertexCount = 3
            });
            return model;
        }

        private static MmdModelDefinition CreateMaterialMorphAllMaterialAddModel()
        {
            var model = CreateTwoMaterialTriangleModel();
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "all-add",
                type = "material",
                panel = "other",
                materialOffsets =
                {
                    new MmdMaterialMorphOffsetDefinition
                    {
                        materialIndex = -1,
                        operation = "add",
                        diffuseColor = new[] { 0.0f, 0.5f, 0.0f },
                        diffuseOpacity = -0.3f,
                        ambientColor = new[] { 0.0f, 0.0f, 0.4f },
                        specularColor = new[] { 0.0f, 0.0f, 0.0f },
                        specularPower = 0.0f,
                        edgeColor = new[] { 0.5f, 0.0f, 0.0f },
                        edgeOpacity = 0.0f,
                        edgeSize = 2.0f,
                        diffuseTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        sphereTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        toonTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateMaterialMorphMultiplyAllMaterialModel()
        {
            var model = CreateTwoMaterialTriangleModel();
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "all-multiply",
                type = "material",
                panel = "other",
                materialOffsets =
                {
                    new MmdMaterialMorphOffsetDefinition
                    {
                        materialIndex = -1,
                        operation = "multiply",
                        diffuseColor = new[] { 0.5f, 1.5f, 0.75f },
                        diffuseOpacity = 0.5f,
                        ambientColor = new[] { 2.0f, 0.5f, 1.0f },
                        specularColor = new[] { 0.0f, 0.0f, 0.0f },
                        specularPower = 0.0f,
                        edgeColor = new[] { 0.5f, 0.5f, 0.5f },
                        edgeOpacity = 0.2f,
                        edgeSize = 2.0f,
                        diffuseTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        sphereTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        toonTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateGroupMaterialMorphModel()
        {
            var model = CreateMaterialMorphTriangleModel();
            // Add a group morph targeting material morph index 0 ("color-change") with weight 0.8.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = model.morphs.Count,
                name = "mood-group",
                type = "group",
                panel = "other",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 0, weight = 0.8f }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateMaterialMorphTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "material-morph-triangle"
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
                origin = new[] { 0.0f, 1.0f, 0.0f },
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
                name = "morph-target-material",
                alpha = 1.0f,
                diffuseColor = new[] { 0.8f, 0.2f, 0.6f },
                ambientColor = new[] { 0.1f, 0.3f, 0.5f },
                edgeColor = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                edgeSize = 1.0f,
                drawEdgeFlag = true,
                vertexCount = 3
            });
            // Material morph: "color-change" with operation "add".
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "color-change",
                type = "material",
                panel = "other",
                materialOffsets =
                {
                    new MmdMaterialMorphOffsetDefinition
                    {
                        materialIndex = 0,
                        operation = "add",
                        diffuseColor = new[] { 0.0f, 0.5f, 0.0f },
                        diffuseOpacity = -0.3f,
                        ambientColor = new[] { 0.0f, 0.0f, 0.4f },
                        specularColor = new[] { 1.0f, 1.0f, 1.0f },
                        specularPower = 0.0f,
                        edgeColor = new[] { 0.5f, 0.0f, 0.0f },
                        edgeOpacity = 0.0f,
                        edgeSize = 2.0f,
                        diffuseTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        sphereTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        toonTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f }
                    }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateGroupMorphTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "group-morph-triangle"
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
                name = "group-morph-material",
                vertexCount = 3
            });
            // Vertex morph: "smile" moves vertex 1 up by 2.0.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "smile",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 2.0f, 0.0f }
                    }
                }
            });
            // Group morph: "happy-face" targets "smile" with coefficient 0.5.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "happy-face",
                type = "group",
                panel = "other",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 0, weight = 0.5f }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateSplitGroupMorphModel()
        {
            var model = CreateSharedVertexTwoSubmeshMorphModel();
            // Replace the direct vertex morph with a group morph that targets it.
            // The existing "shared-up" vertex morph (index 0) targets vertex 0 with delta (0,1,0).
            // Add a group morph "happy-face" targeting morphIndex 0 ("shared-up") with weight 1.0.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "happy-face",
                type = "group",
                panel = "other",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 0, weight = 1.0f }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateCycleGroupMorphModel()
        {
            var model = new MmdModelDefinition
            {
                name = "cycle-group-morph"
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
                name = "cycle-material",
                vertexCount = 3
            });
            // Vertex morph "blink" (index 0) so the cycle test has a terminal target.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "blink",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 1.0f, 0.0f }
                    }
                }
            });
            // Group morph "loop-a" targets "loop-b" with weight 1.0.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "loop-a",
                type = "group",
                panel = "other",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 2, weight = 1.0f }
                }
            });
            // Group morph "loop-b" targets "loop-a" with weight 1.0 (creates cycle).
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 2,
                name = "loop-b",
                type = "group",
                panel = "other",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 1, weight = 1.0f }
                }
            });
            return model;
        }

        [Test]
        public void ApplyFrameExpandsFlipMorphWeightToTargetVertexMorph()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateFlipMorphTriangleModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                // Frame with flip morph weight only, no direct vertex morph weight.
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "flip-smile", weight = 1.0f });

                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                // flipWeight(1.0) * offsetWeight(0.5) = resolved smile 0.5 -> BlendShape 50f
                int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
                Assert.That(smileIndex, Is.GreaterThanOrEqualTo(0));
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Assert.That(renderer.GetBlendShapeWeight(smileIndex), Is.EqualTo(50f).Within(0.001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyFrameExpandsFlipMorphToMaterialMorph()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateFlipMaterialMorphModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                // Frame with flip morph weight only, no direct material morph weight.
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "flip-color", weight = 1.0f });

                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                // Flip morph "flip-color" targets "color-change" with weight 0.8.
                // Resolved "color-change" weight = 1.0 * 0.8 = 0.8.
                // diffuseColor: base(0.8,0.2,0.6) + offset(0.0,0.5,0.0)*0.8 = (0.8,0.6,0.6)
                Color morphedColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Assert.That(morphedColor.r, Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(morphedColor.g, Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(morphedColor.b, Is.EqualTo(0.6f).Within(0.00001f));
                // alpha: 1.0 + (-0.3)*0.8 = 0.76
                Assert.That(morphedColor.a, Is.EqualTo(0.76f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ApplyFrameWithGroupToFlipRecursiveExpansion()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateGroupFlipRecursiveModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                // Frame has group morph weight that targets a flip morph.
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "mood-group", weight = 1.0f });

                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                // mood-group(1.0) -> flip-smile(1.0) -> smile(0.5) = resolved smile 0.5 -> BlendShape 50f
                int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
                Assert.That(smileIndex, Is.GreaterThanOrEqualTo(0));
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Assert.That(renderer.GetBlendShapeWeight(smileIndex), Is.EqualTo(50f).Within(0.001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void FlipMorphCycleDetectionThrows()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateCycleFlipMorphModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "loop-a", weight = 1.0f });

                var ex = Assert.Throws<InvalidOperationException>(() =>
                    MmdUnityFrameApplier.ApplyFrame(instance, frame));

                Assert.That(ex.Message, Does.Contain("cycle"));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        private static MmdModelDefinition CreateFlipMorphTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "flip-morph-triangle"
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
                name = "flip-morph-material",
                vertexCount = 3
            });
            // Vertex morph: "smile" moves vertex 1 up by 2.0.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "smile",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 2.0f, 0.0f }
                    }
                }
            });
            // Flip morph: "flip-smile" targets "smile" with coefficient 0.5.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "flip-smile",
                type = "flip",
                panel = "other",
                flipOffsets = new List<MmdFlipMorphOffsetDefinition>
                {
                    new MmdFlipMorphOffsetDefinition { morphIndex = 0, weight = 0.5f }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateFlipMaterialMorphModel()
        {
            var model = CreateMaterialMorphTriangleModel();
            // Add a flip morph targeting material morph index 0 ("color-change") with weight 0.8.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = model.morphs.Count,
                name = "flip-color",
                type = "flip",
                panel = "other",
                flipOffsets = new List<MmdFlipMorphOffsetDefinition>
                {
                    new MmdFlipMorphOffsetDefinition { morphIndex = 0, weight = 0.8f }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateGroupFlipRecursiveModel()
        {
            var model = CreateFlipMorphTriangleModel();
            // Add a group morph "mood-group" targeting flip morph "flip-smile" (index 1) with weight 1.0.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = model.morphs.Count,
                name = "mood-group",
                type = "group",
                panel = "other",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 1, weight = 1.0f }
                }
            });
            return model;
        }

        private static MmdModelDefinition CreateCycleFlipMorphModel()
        {
            var model = new MmdModelDefinition
            {
                name = "cycle-flip-morph"
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
                name = "cycle-flip-material",
                vertexCount = 3
            });
            // Vertex morph "blink" (index 0) so the cycle test has a terminal target.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "blink",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 1.0f, 0.0f }
                    }
                }
            });
            // Flip morph "loop-a" targets "loop-b" with weight 1.0.
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "loop-a",
                type = "flip",
                panel = "other",
                flipOffsets = new List<MmdFlipMorphOffsetDefinition>
                {
                    new MmdFlipMorphOffsetDefinition { morphIndex = 2, weight = 1.0f }
                }
            });
            // Flip morph "loop-b" targets "loop-a" with weight 1.0 (creates cycle).
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 2,
                name = "loop-b",
                type = "flip",
                panel = "other",
                flipOffsets = new List<MmdFlipMorphOffsetDefinition>
                {
                    new MmdFlipMorphOffsetDefinition { morphIndex = 1, weight = 1.0f }
                }
            });
            return model;
        }

        [Test]
        public void CreateSkinnedModelBakesBlendShapeFramesForVertexMorphs()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.morphs.Add(new MmdMorphDefinition
                {
                    index = 0,
                    name = "blink",
                    type = "vertex",
                    panel = "eye",
                    vertexOffsets =
                    {
                        new MmdVertexMorphOffsetDefinition
                        {
                            vertexIndex = 1,
                            positionDelta = new[] { 0.0f, 2.0f, 0.0f }
                        }
                    }
                });

                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                Assert.That(instance.Mesh.blendShapeCount, Is.EqualTo(1));
                Assert.That(instance.Mesh.GetBlendShapeName(0), Is.EqualTo("blink"));
                Assert.That(instance.BlendShapeIndexMap.ContainsKey("blink"), Is.True);
                Assert.That(instance.BlendShapeIndexMap["blink"], Is.EqualTo(0));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void SkinnedModelZeroBlendShapeWeightReturnsToBaseThroughSetBlendShapeWeightPath()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.morphs.Add(new MmdMorphDefinition
                {
                    index = 0,
                    name = "blink",
                    type = "vertex",
                    panel = "eye",
                    vertexOffsets =
                    {
                        new MmdVertexMorphOffsetDefinition
                        {
                            vertexIndex = 1,
                            positionDelta = new[] { 0.0f, 2.0f, 0.0f }
                        }
                    }
                });
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);
                int blinkIndex = instance.Mesh.GetBlendShapeIndex("blink");

                MmdEvaluatedFrame frame1 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame1.morphs.Add(new MmdEvaluatedMorphWeight { name = "blink", weight = 1.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame1);
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Assert.That(renderer.GetBlendShapeWeight(blinkIndex), Is.EqualTo(100f).Within(0.001f));

                MmdEvaluatedFrame frame0 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame0.morphs.Add(new MmdEvaluatedMorphWeight { name = "blink", weight = 0.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame0);
                Assert.That(renderer.GetBlendShapeWeight(blinkIndex), Is.EqualTo(0f).Within(0.001f));

                Assert.That(instance.RenderingDescriptor.vertices[1].position[1], Is.EqualTo(0.0f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void SplitVertexCopyReceivesMorphOffsetInBakedBlendShapeFrame()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateSharedVertexTwoSubmeshMorphModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                Assert.That(instance.Mesh.blendShapeCount, Is.EqualTo(1));
                int shapeIndex = instance.Mesh.GetBlendShapeIndex("shared-up");
                Assert.That(shapeIndex, Is.GreaterThanOrEqualTo(0));

                var deltaVertices = new Vector3[instance.Mesh.vertexCount];
                var deltaNormals = new Vector3[instance.Mesh.vertexCount];
                var deltaTangents = new Vector3[instance.Mesh.vertexCount];
                instance.Mesh.GetBlendShapeFrameVertices(shapeIndex, 0, deltaVertices, deltaNormals, deltaTangents);

                Assert.That(deltaVertices[0], Is.EqualTo(new Vector3(0.0f, 1.0f, 0.0f)));
                Assert.That(deltaVertices[3], Is.EqualTo(new Vector3(0.0f, 1.0f, 0.0f)));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void GroupOrFlipResolvedWeightReachesBlendShapeWeight()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateGroupMorphTriangleModel();
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);
                int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
                Assert.That(smileIndex, Is.GreaterThanOrEqualTo(0));

                // Group "happy-face" at 0.75 targeting "smile" with weight 0.5
                // => resolved smile = 0.75 * 0.5 = 0.375 -> BlendShape 37.5f
                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 0.75f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame);

                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
                Assert.That(renderer.GetBlendShapeWeight(smileIndex), Is.EqualTo(37.5f).Within(0.001f));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void VertexOnlyApplyMorphsWithTimingReportsNoMeshUpload()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
                model.morphs.Add(new MmdMorphDefinition
                {
                    index = 0,
                    name = "blink",
                    type = "vertex",
                    panel = "eye",
                    vertexOffsets =
                    {
                        new MmdVertexMorphOffsetDefinition
                        {
                            vertexIndex = 1,
                            positionDelta = new[] { 0.0f, 1.0f, 0.0f }
                        }
                    }
                });
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);

                MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "blink", weight = 1.0f });

                MmdUnityMorphApplyTimingSummary timing = MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);

                Assert.That(timing.hasVertexMorphs, Is.True);
                Assert.That(timing.hasTextureUvMorphs, Is.False);
                Assert.That(timing.meshUploadRequired, Is.False);
                Assert.That(timing.blendShapePathUsed, Is.True);
                Assert.That(timing.setVerticesMs, Is.EqualTo(0.0).Within(0.000001));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void ReapplyImportedMaterialTransparencyReclassifiesByTextureAlpha()
        {
            // Prove that ReapplyImportedMaterialTransparency re-classifies and re-applies the
            // transparency mode using the decoded texture alpha without touching AssetDatabase.
            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            // Ensure PMX alpha is 1.0 so the test exercises the texture-alpha path, not PMX-alpha.
            model.materials[0].alpha = 1.0f;

            MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);

            Shader shader = Shader.Find("MMD Basic URP Toon")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? throw new InvalidOperationException("No fallback shader found.");

            Material material = new Material(shader);
            Texture2D? alphaTex = null;
            Texture2D? opaqueTex = null;
            try
            {
                // --- alphaBlend case: all pixels at alpha 128 (middle alpha) ---
                alphaTex = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false);
                var alphaPixels = new Color32[16];
                for (int k = 0; k < 16; k++)
                {
                    alphaPixels[k] = new Color32(255, 255, 255, 128);
                }
                alphaTex.SetPixels32(alphaPixels);
                alphaTex.Apply();

                MmdUnityMaterialBuilder.ReapplyImportedMaterialTransparency(
                    material, descriptor, descriptor.materials[0], 0, "tex.png", "tex.png", alphaTex);

                Assert.That(
                    material.GetFloat("_DstBlend"),
                    Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha).Within(0.00001f),
                    "alpha-128 texture should classify as alphaBlend (_DstBlend = OneMinusSrcAlpha)");
                Assert.That(
                    material.renderQueue,
                    Is.GreaterThanOrEqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent),
                    "alpha-128 texture should put material in Transparent queue");

                // --- opaque case: all pixels at alpha 255 ---
                opaqueTex = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false);
                var opaquePixels = new Color32[16];
                for (int k = 0; k < 16; k++)
                {
                    opaquePixels[k] = new Color32(255, 255, 255, 255);
                }
                opaqueTex.SetPixels32(opaquePixels);
                opaqueTex.Apply();

                MmdUnityMaterialBuilder.ReapplyImportedMaterialTransparency(
                    material, descriptor, descriptor.materials[0], 0, "tex.png", "tex.png", opaqueTex);

                Assert.That(
                    material.GetFloat("_DstBlend"),
                    Is.EqualTo((float)UnityEngine.Rendering.BlendMode.Zero).Within(0.00001f),
                    "alpha-255 texture should classify as opaque (_DstBlend = Zero)");
                Assert.That(
                    material.renderQueue,
                    Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Geometry),
                    "alpha-255 texture should put material in Geometry queue");
            }
            finally
            {
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (alphaTex != null) UnityEngine.Object.DestroyImmediate(alphaTex);
                if (opaqueTex != null) UnityEngine.Object.DestroyImmediate(opaqueTex);
            }
        }

        private static SkinnedMeshRenderer RequireSkinnedRenderer(MmdUnityModelInstance instance)
        {
            Assert.That(instance.SkinnedMeshRenderer, Is.Not.Null);
            return instance.SkinnedMeshRenderer!;
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

            if (instance.Materials == null)
            {
                return;
            }

            foreach (Material material in instance.Materials.Where(material => material != null).Distinct())
            {
                UnityEngine.Object.DestroyImmediate(material);
            }

            foreach (Texture2D texture in instance.OwnedTextures.Where(texture => texture != null).Distinct())
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
