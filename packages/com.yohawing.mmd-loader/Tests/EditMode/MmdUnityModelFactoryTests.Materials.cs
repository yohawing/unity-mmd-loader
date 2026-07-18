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
    public sealed partial class MmdUnityModelFactoryTests
    {
        [Test]
        public void CreateStaticModelWithSharedToonResolvesBuiltInToonRamp()
        {
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "model.pmx");
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            // A shared toon material carries an index (toon01..toon10), not a texture path.
            model.materials[0].toonTexture = string.Empty;
            model.materials[0].toonShared = true;
            model.materials[0].sharedToonIndex = 0; // toon01

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model, pmxPath));
            MmdUnityModelInstance instance = scope.Instance;

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

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].alpha = 0.25f;
            model.materials[0].diffuseColor = new[] { 0.8f, 0.2f, 0.1f };
            model.materials[0].ambientColor = new[] { 0.3f, 0.1f, 0.05f };
            model.materials[0].edgeColor = new[] { 0.01f, 0.02f, 0.03f, 0.75f };
            model.materials[0].edgeSize = 1.25f;
            model.materials[0].drawEdgeFlag = true;

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model));
            MmdUnityModelInstance instance = scope.Instance;

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
        [Test]
        public void CreateStaticModelOffsetsTransparentQueuesByMaterialOrder()
        {

            MmdModelDefinition model = CreateTwoTransparentTriangleModel();

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model));
            MmdUnityModelInstance instance = scope.Instance;

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
        [Test]
        public void CreateStaticModelAppliesDescriptorCullingPolicy()
        {

            MmdModelDefinition model = CreateTwoTransparentTriangleModel();
            model.materials[0].cullingPolicy = "double-sided";
            model.materials[0].drawEdgeFlag = true;
            model.materials[0].edgeSize = 1.0f;
            model.materials[1].cullingPolicy = "backface-culling";
            model.materials[1].drawEdgeFlag = true;
            model.materials[1].edgeSize = 1.0f;

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(ReadMaterialFloat(instance.Materials[0], "_Cull"), Is.EqualTo((float)UnityEngine.Rendering.CullMode.Off).Within(0.00001f));
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_OutlineVisible"), Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(instance.MaterialBindingDiagnostics[0].cull, Is.EqualTo((float)UnityEngine.Rendering.CullMode.Off).Within(0.00001f));
            Assert.That(instance.MaterialBindingDiagnostics[0].cullingPolicy, Is.EqualTo("double-sided"));
            Assert.That(instance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));

            Assert.That(ReadMaterialFloat(instance.Materials[1], "_Cull"), Is.EqualTo((float)UnityEngine.Rendering.CullMode.Back).Within(0.00001f));
            // Body sidedness must not override the independent PMX draw-edge flag.
            Assert.That(ReadMaterialFloat(instance.Materials[1], "_OutlineVisible"), Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(instance.MaterialBindingDiagnostics[1].cull, Is.EqualTo((float)UnityEngine.Rendering.CullMode.Back).Within(0.00001f));
            Assert.That(instance.MaterialBindingDiagnostics[1].cullingPolicy, Is.EqualTo("backface-culling"));
            Assert.That(instance.Materials[1].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent + 1));
        }
        [Test]
        public void CreateStaticModelFallsBackWhenRequestedShaderIsMissing()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);
            descriptor.urpMaterialBindings[0].shaderName = "Missing/MMD Basic URP Toon Test";

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(descriptor, "missing-shader-fallback"));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.ShaderDiagnostics.requestedShaderName, Is.EqualTo("Missing/MMD Basic URP Toon Test"));
            Assert.That(instance.ShaderDiagnostics.shaderFallbackUsed, Is.True);
            Assert.That(instance.ShaderDiagnostics.fallbackReason, Is.EqualTo("requested-shader-not-found"));
            Assert.That(instance.ShaderDiagnostics.resolvedShaderName, Is.Not.Empty);
            Assert.That(instance.ShaderDiagnostics.fallbackCandidates, Does.Contain("Missing/MMD Basic URP Toon Test"));
            Assert.That(instance.Materials[0].shader, Is.Not.Null);
        }
        [Test]
        public void CreateStaticModelResolvesLegacyAndUrpLitShadersPerMaterialSlot()
        {
            MmdModelDefinition model = CreateTwoTransparentTriangleModel();
            MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);
            descriptor.urpMaterialBindings[0].shaderName = MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName;
            descriptor.urpMaterialBindings[1].shaderName = MmdUrpMaterialBindingDescriptorBuilder.UrpLitShaderName;

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(descriptor, "mixed-shader-smoke"));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Materials, Has.Length.EqualTo(2));
            Assert.That(instance.Materials[0].shader.name, Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName));
            Assert.That(instance.Materials[1].shader.name, Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.UrpLitShaderName));
            Assert.That(instance.MaterialBindingDiagnostics[0].resolvedShaderName,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName));
            Assert.That(instance.MaterialBindingDiagnostics[1].resolvedShaderName,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.UrpLitShaderName));
            Assert.That(instance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
            Assert.That(instance.Materials[1].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent + 1));

            // Model-level diagnostics remain the legacy scalar summary of the first requested shader.
            Assert.That(instance.ShaderDiagnostics.requestedShaderName,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName));
            Assert.That(instance.ShaderDiagnostics.resolvedShaderName,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName));
            Assert.That(instance.ShaderDiagnostics.shaderFallbackUsed, Is.False);
        }
        [Test]
        public void CreateStaticModelPreservesRawSnapshotUvAndFlipsViewportUv()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.vertices[1].uv = new[] { 0.25f, 0.75f };

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.RenderingDescriptor.vertices[1].uv[0], Is.EqualTo(0.25f).Within(0.00001f));
            Assert.That(instance.RenderingDescriptor.vertices[1].uv[1], Is.EqualTo(0.75f).Within(0.00001f));
            Assert.That(instance.RenderingDescriptor.textureOrientation.flipVForViewport, Is.True);
            Assert.That(instance.RenderingDescriptor.textureOrientation.flipTexturePixels, Is.False);

            Vector2[] viewportUvs = instance.Mesh.uv;
            Assert.That(viewportUvs, Has.Length.EqualTo(3));
            Assert.That(viewportUvs[1].x, Is.EqualTo(0.25f).Within(0.00001f));
            Assert.That(viewportUvs[1].y, Is.EqualTo(0.25f).Within(0.00001f));
        }
        [Test]
        public void CreateStaticModelViewportUvSamplesRawTextureWithFlippedV()
        {
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "model.pmx");
            string texturePath = Path.Combine(temp.Path, "orientation.png");
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
            WriteVerticalOrientationPng(texturePath);

            MmdModelDefinition model = CreateTexturedQuadModel("orientation.png");
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model, pmxPath));
            MmdUnityModelInstance instance = scope.Instance;

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
        [Test]
        public void CreateStaticModelWithSourcePathLoadsIndexedBmpSphereTextureForDiagnostics()
        {
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "model.pmx");
            string sphereDirectory = Path.Combine(temp.Path, "sp");
            Directory.CreateDirectory(sphereDirectory);
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
            WriteBmp8Indexed(Path.Combine(sphereDirectory, "sphere.bmp"), width: 2, height: 2);

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].sphereTexture = Path.Combine("sp", "sphere.bmp");

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model, pmxPath));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.LoadedSphereTextureCount, Is.EqualTo(1));
            Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(0));
            Assert.That(instance.OwnedTextures, Has.Length.EqualTo(1));
            Assert.That(instance.OwnedTextures[0].width, Is.EqualTo(2));
            Assert.That(instance.OwnedTextures[0].height, Is.EqualTo(2));
        }
        [Test]
        public void CreateStaticModelReportsMissingAndUnsupportedTexturesWithoutFailing()
        {
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "model.pmx");
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });

            MmdModelDefinition missingModel = CreateMinimalTriangleModel(includeTextureReferences: false);
            missingModel.materials[0].texture = "missing.png";
            using var missingScope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(missingModel, pmxPath));
            MmdUnityModelInstance missingInstance = missingScope.Instance;

            Assert.That(missingInstance.Root, Is.Not.Null);
            Assert.That(missingInstance.LoadedDiffuseTextureCount, Is.EqualTo(0));
            Assert.That(missingInstance.MissingTextureReferenceCount, Is.EqualTo(1));
            Assert.That(missingInstance.UnsupportedTextureReferenceCount, Is.EqualTo(0));

            MmdModelDefinition unsupportedModel = CreateMinimalTriangleModel(includeTextureReferences: false);
            unsupportedModel.materials[0].texture = "diffuse.webp";
            using var unsupportedScope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(unsupportedModel, pmxPath));
            MmdUnityModelInstance unsupportedInstance = unsupportedScope.Instance;

            Assert.That(unsupportedInstance.Root, Is.Not.Null);
            Assert.That(unsupportedInstance.LoadedDiffuseTextureCount, Is.EqualTo(0));
            Assert.That(unsupportedInstance.MissingTextureReferenceCount, Is.EqualTo(0));
            Assert.That(unsupportedInstance.UnsupportedTextureReferenceCount, Is.EqualTo(1));
        }
        [Test]
        public void CreateSkinnedModelCreatesSkinnedRendererBindposesAndBoneWeights()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.vertices[1].boneIndices = new[] { 0, 1 };
            model.vertices[1].boneWeights = new[] { 0.25f, 0.75f };

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

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
        [Test]
        public void CreateSkinnedModelCreatesPhysicsCollidersAndInspectableParameters()
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

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

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
        [Test]
        public void CreateSkinnedModelParentsPhysicsBodiesByPmxBoneIndex()
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

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.BoneTransforms, Has.Length.EqualTo(2));
            Assert.That(instance.BoneTransforms[1].name, Is.EqualTo("child"));
            Assert.That(instance.PhysicsBodies[0].transform.parent, Is.EqualTo(instance.BoneTransforms[1]));
            Assert.That(instance.PhysicsBodies[0].transform.localPosition, Is.EqualTo(new Vector3(0.0f, 1.0f, 0.0f)));
        }
        [Test]
        public void CreateStaticModelFromModelCreatesBoneTransformHierarchy()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model));
            MmdUnityModelInstance instance = scope.Instance;

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
        [Test]
        public void ApplyFrameUpdatesBoneTransformsFromBindPose()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

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
        [Test]
        public void ApplyFrameConvertsRotationAtUnityBoundary()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;
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
        [Test]
        public void ApplyColumnMajorWorldMatricesConvertsHierarchyWorldPoseAtUnityBoundary()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;
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
    }
}
