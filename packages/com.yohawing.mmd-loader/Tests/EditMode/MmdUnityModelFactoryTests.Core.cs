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
        public void CreateStaticModelFromModelCreatesMeshObjectAndMaterials()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model));
            MmdUnityModelInstance instance = scope.Instance;

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
        [Test]
        public void CreateStaticModelWithMmdToonLitUsesSeparateShaderAndKeepsMmdPasses()
        {
            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(
                model,
                sourcePath: null,
                importScale: 1.0f,
                preset: MmdMaterialPreset.MmdToonLit));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Materials[0].shader.name,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName));
            Assert.That(instance.MaterialBindingDiagnostics[0].shaderName,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName));
            Assert.That(instance.Materials[0].FindPass("Outline"), Is.GreaterThanOrEqualTo(0));
            Assert.That(instance.Materials[0].FindPass("MmdSelfShadowCaster"), Is.GreaterThanOrEqualTo(0));
            Assert.That(instance.Materials[0].FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
        }
        [Test]
        public void MmdToonLitShaderUsesUrpMainLightShadowsWithoutChangingLegacyShader()
        {
            string shaderDirectory = Path.Combine(MmdTestFixtures.PackageRoot, "Runtime", "Shaders");
            string legacySource = File.ReadAllText(Path.Combine(shaderDirectory, "MmdBasicUrpToon.shader"));
            string toonLitSource = File.ReadAllText(Path.Combine(shaderDirectory, "MmdToonLit.shader"));

            Assert.That(legacySource, Does.Not.Contain("_MAIN_LIGHT_SHADOWS"));
            Assert.That(legacySource, Does.Not.Contain("mainLight.shadowAttenuation"));
            Assert.That(toonLitSource, Does.Contain("#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN"));
            Assert.That(toonLitSource, Does.Contain("#pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH"));
            Assert.That(toonLitSource, Does.Contain("GetMainLight(TransformWorldToShadowCoord(input.positionWS))"));
            Assert.That(toonLitSource, Does.Contain("mainLight.shadowAttenuation"));
            Assert.That(toonLitSource, Does.Contain("dot(_MmdLightDirection.xyz, _MmdLightDirection.xyz) > 0.0h"));
            Assert.That(toonLitSource, Does.Contain("LinearToSRGB(_MmdLightColor.rgb) * LinearToSRGB(mainLight.color)"));
            Assert.That(toonLitSource, Does.Contain("UsePass \"MMD Basic URP Toon/MmdSelfShadowCaster\""));
        }
        [Test]
        public void MmdToonLitShaderUsesUrpAmbientShAndFogWithoutChangingLegacyShader()
        {
            string shaderDirectory = Path.Combine(MmdTestFixtures.PackageRoot, "Runtime", "Shaders");
            string legacySource = File.ReadAllText(Path.Combine(shaderDirectory, "MmdBasicUrpToon.shader"));
            string toonLitSource = File.ReadAllText(Path.Combine(shaderDirectory, "MmdToonLit.shader"));

            Assert.That(legacySource, Does.Not.Contain("SampleSH("));
            Assert.That(legacySource, Does.Not.Contain("#pragma multi_compile_fog"));
            Assert.That(legacySource, Does.Not.Contain("ComputeFogFactor("));
            Assert.That(legacySource, Does.Not.Contain("MixFog("));

            Assert.That(toonLitSource, Does.Contain("half3 ambientShSrgb = LinearToSRGB(SampleSH(normalWS));"));
            Assert.That(toonLitSource, Does.Contain("LinearToSRGB(_BaseColor.rgb) * (mainLightSrgb + ambientShSrgb)"));
            Assert.That(toonLitSource, Does.Contain("#pragma multi_compile_fog"));
            Assert.That(toonLitSource, Does.Contain("output.fogFactor = ComputeFogFactor(output.positionCS.z);"));
            Assert.That(toonLitSource, Does.Contain("half3 foggedLinear = MixFog(SRGBToLinear(litSrgb), input.fogFactor);"));
            Assert.That(toonLitSource, Does.Contain("_GammaTarget > 0.5h ? LinearToSRGB(foggedLinear) : foggedLinear"));

            Shader toonLitShader = Shader.Find(MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName);
            Assert.That(toonLitShader, Is.Not.Null);
            Assert.That(toonLitShader!.name, Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName));
        }
        [Test]
        public void CreateStaticModelKeepsShadowCasterAndAddsHiddenSelfShadowTarget()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.MeshRenderer, Is.Not.Null);
            Assert.That(instance.MeshRenderer!.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.On));
            Assert.That(instance.MeshRenderer.receiveShadows, Is.True);
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_BodyVisible"), Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_AlphaClipThreshold"), Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_ShadowAlphaClipThreshold"), Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(instance.Materials[0].FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
            MmdSelfShadowTarget target = instance.Root!.GetComponent<MmdSelfShadowTarget>();
            Assert.That(target, Is.Not.Null);
            Assert.That((target.hideFlags & HideFlags.HideInInspector) != 0, Is.True);
            Assert.That(target.BoundsRoot, Is.EqualTo(instance.MeshRenderer!.transform));
        }
        [Test]
        public void CreateSkinnedModelKeepsShadowCasterAndAddsHiddenSelfShadowTarget()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.SkinnedMeshRenderer, Is.Not.Null);
            Assert.That(instance.SkinnedMeshRenderer!.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.On));
            Assert.That(instance.SkinnedMeshRenderer.receiveShadows, Is.True);
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_BodyVisible"), Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_AlphaClipThreshold"), Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_ShadowAlphaClipThreshold"), Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(instance.Materials[0].FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
            MmdSelfShadowTarget target = instance.Root!.GetComponent<MmdSelfShadowTarget>();
            Assert.That(target, Is.Not.Null);
            Assert.That((target.hideFlags & HideFlags.HideInInspector) != 0, Is.True);
            Assert.That(target.BoundsRoot, Is.EqualTo(instance.SkinnedMeshRenderer!.transform));
        }
        [Test]
        public void BasicUrpToonShaderUsesOnlyDedicatedSelfShadowForToonCoordinate()
        {
            string shaderPath = Path.Combine(
                MmdTestFixtures.PackageRoot,
                "Runtime",
                "Shaders",
                "MmdBasicUrpToon.shader");
            Assert.That(shaderPath, Does.Exist);

            string source = File.ReadAllText(shaderPath);

            Assert.That(
                source,
                Does.Contain("half selfShadowVisibility = SampleMmdSelfShadow(input.positionWS, selfShadowReceive);"),
                "Forward shading must derive toon shadow visibility only from the dedicated MMD self-shadow map.");
            Assert.That(
                source,
                Does.Not.Contain("mainLight.shadowAttenuation"),
                "URP main-light shadow attenuation must not affect MMD toon shading.");
            Assert.That(
                source,
                Does.Not.Contain("effectiveReceiveShadows"),
                "ForwardLit toon shadowing must not be gated by legacy standard-shadow receive wiring.");
            Assert.That(
                source,
                Does.Not.Contain("_MmdReceiveShadows"),
                "Legacy standard-shadow receive properties are not part of the MMD toon receive path.");
            Assert.That(
                source,
                Does.Not.Contain("_MmdSuppressStandardShadows"),
                "Legacy standard-shadow suppression properties are not part of the MMD toon receive path.");
            Assert.That(
                source,
                Does.Not.Contain("_MAIN_LIGHT_SHADOWS"),
                "ForwardLit must not compile URP main-light shadow variants it does not sample.");
            Assert.That(
                source,
                Does.Not.Contain("_SHADOWS_SOFT"),
                "ForwardLit must not compile URP soft-shadow variants it does not sample.");
            Assert.That(
                source,
                Does.Contain("half lightVisibility = saturate(dot(normalWS, lightDirection) * 3.0h);"),
                "MMD toon visibility should use the traced saturate(dot(N, -LightDir) * 3) shape.");
            Assert.That(
                source,
                Does.Contain("half toonVisibility = min(selfShadowVisibility, lightVisibility);"),
                "MMD self-shadow should combine shadow visibility and light-side toon visibility with min().");
            Assert.That(
                source,
                Does.Not.Contain("LinearToSRGB(_MmdLightColor.rgb) * selfShadowVisibility"),
                "Shadow visibility must not directly dim the base/direct light color.");
            Assert.That(
                source,
                Does.Contain("half3 fallbackSelfShadowToon = half3(1.0h, 1.0h, 1.0h);"),
                "Toonless material slots stay flat so face/skin materials without toon maps do not pick up dirty shadow bands.");
            Assert.That(
                source,
                Does.Contain("half3 mappedSelfShadowToon = SAMPLE_TEXTURE2D(_ToonMap, sampler_ToonMap, float2(0.5, 0.22)).rgb;"),
                "Unity's built-in shared toon strips put very dark bands at v=0, so the self-shadow ToonColor probe must not collapse shared-toon materials to black.");
            Assert.That(
                source,
                Does.Contain("half3 mmdToonLight = lerp(selfShadowToon, half3(1.0h, 1.0h, 1.0h), lightVisibility);"),
                "With self-shadow disabled, toon lighting must stay on the original NdotL-only path.");
            Assert.That(
                source,
                Does.Contain("half3 toonLight = lerp(ndotl.xxx, mmdToonLight, _ToonStrength);"),
                "Self-shadow OFF must match the original toon/NdotL blend.");
            Assert.That(
                source,
                Does.Contain("if (selfShadowVisibility < 0.999h)"),
                "Self-shadow toon color must only affect fragments shadowed by the dedicated self-shadow map.");
            Assert.That(
                source,
                Does.Contain("half3 selfShadowMmdToonLight = lerp(selfShadowToon, half3(1.0h, 1.0h, 1.0h), toonVisibility);"),
                "MMD final composition blends the self-shadow ToonColor toward white by toonVisibility.");
            Assert.That(
                source,
                Does.Contain("half3 selfShadowToonLight = lerp(ndotl.xxx, selfShadowMmdToonLight, _ToonStrength);"),
                "The crisp self-shadow branch should keep the same NdotL/toon blend model as the original path.");
            Assert.That(
                source,
                Does.Contain("toonLight = min(toonLight, selfShadowToonLight);"),
                "Self-shadow should darken the regular NdotL toon layer instead of replacing it, preserving the soft toon shading underneath the crisp self-shadow mask.");
            Assert.That(
                source,
                Does.Contain("half3 litSRGB = saturate(albedoSRGB * toonSRGB);"),
                "MMD toon/self-shadow darkening is shader arithmetic: diffuse base is multiplied by the toon color, then alpha-over blending happens at the draw level.");
            Assert.That(
                source,
                Does.Not.Contain("lerp(0.55h.xxx, 1.0h.xxx, selfShadowVisibility)"),
                "Toonless material slots must not invent a dedicated self-shadow fallback ramp.");
            Assert.That(
                source,
                Does.Contain("half sampledDepth = SAMPLE_TEXTURE2D(_MmdSelfShadowMap, sampler_MmdSelfShadowMap, shadowCoord.xy).r;"),
                "MMD self-shadow samples the R32F z/w map first, then compares the sampled depth.");
            Assert.That(
                source,
                Does.Contain("return ComputeMmdSelfShadowVisibility(shadowCoord.z, sampledDepth);"),
                "The receiver-side compare should mirror the MMD main pass texld + currentDepth-shadowDepth pattern.");
            Assert.That(
                source,
                Does.Contain("return 1.0h - saturate(occluderDepthDelta * 1500.0h - 0.3h);"),
                "MMD's traced main shader softens the self-shadow edge with mad_sat(diff * 1500 - 0.3).");
            Assert.That(
                source,
                Does.Not.Contain("LOAD_TEXTURE2D(_MmdSelfShadowMap"),
                "MMD's traced shader uses texld before the depth compare, not point-load compare-before-filter PCF.");
            Assert.That(
                source,
                Does.Contain("Name \"MmdSelfShadowCaster\""),
                "Dedicated MMD self-shadow caster must write z/w into the R32F map instead of using URP's depth-only ShadowCaster output.");
            Assert.That(
                source,
                Does.Contain("float shadowDepth = input.shadowCoord.z / max(input.shadowCoord.w, 1e-5);"),
                "The traced MMD caster pixel shader writes light clip z/w.");
            Assert.That(
                source,
                Does.Not.Contain("0.22h"),
                "Dedicated MMD self-shadow visibility must be a 0/1 mask before toon ramp sampling.");
            Assert.That(
                source,
                Does.Not.Contain("three-mmd-loader"),
                "The package shader contract must describe its own visibility path, not a copied reference note.");
        }
        [Test]
        public void CreateStaticModelSetsShadowAlphaClipForAlphaBlendMaterial()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].alpha = 0.5f;

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Materials[0].renderQueue, Is.GreaterThanOrEqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_AlphaClipThreshold"), Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_ShadowAlphaClipThreshold"), Is.EqualTo(0.01f).Within(0.00001f));
        }
        [Test]
        public void CreateStaticModelConvertsMeshPositionsAndNormalsAtUnityBoundary()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.vertices[1].position = new[] { 1.0f, 0.0f, 2.0f };
            model.vertices[1].normal = new[] { 0.0f, 0.0f, 1.0f };

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Vector3[] vertices = instance.Mesh.vertices;
            Vector3[] normals = instance.Mesh.normals;
            Assert.That(vertices[1], Is.EqualTo(new Vector3(-1.0f, 0.0f, -2.0f)));
            Assert.That(normals[1], Is.EqualTo(new Vector3(0.0f, 0.0f, -1.0f)));
            Assert.That(instance.Mesh.GetIndices(0), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(instance.Root.transform.localScale, Is.EqualTo(Vector3.one));
        }
        [Test]
        public void CreateStaticModelFromDescriptorReportsDescriptorCounts()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.vertices[1].position = new[] { 1.0f, 2.0f, 3.0f };
            model.vertices[1].normal = new[] { 0.0f, 1.0f, 0.0f };
            model.vertices[1].uv = new[] { 0.25f, 0.75f };
            model.vertices[1].boneIndices = new[] { 0 };
            model.vertices[1].boneWeights = new[] { 1.0f };
            MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(descriptor, "minimal-static-triangle"));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.VertexCount, Is.EqualTo(descriptor.vertices.Count));
            Assert.That(instance.IndexCount, Is.EqualTo(descriptor.indices.Count));
            Assert.That(instance.SubmeshCount, Is.EqualTo(descriptor.submeshes.Count));
            Assert.That(instance.Mesh.vertexCount, Is.EqualTo(descriptor.vertices.Count));
            Assert.That(instance.Mesh.subMeshCount, Is.EqualTo(descriptor.submeshes.Count));
            Assert.That(instance.Materials, Has.Length.EqualTo(descriptor.materials.Count));
            Assert.That(instance.SkippedTextureReferenceCount, Is.EqualTo(0));
        }
        [Test]
        public void CreateStaticModelCountsSkippedTextureSphereAndToonReferences()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: true);
            model.materials[0].diffuseColor = new[] { 0.6f, 0.4f, 0.2f };
            model.materials[0].ambientColor = new[] { 0.2f, 0.1f, 0.05f };
            model.materials[0].edgeColor = new[] { 0.01f, 0.02f, 0.03f, 0.75f };
            model.materials[0].edgeSize = 0.9f;

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.Materials, Has.Length.EqualTo(1));
            Assert.That(instance.SkippedTextureReferenceCount, Is.EqualTo(5));
            Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(3));
            Assert.That(instance.SkippedSphereTextureReferenceCount, Is.EqualTo(1));
            Assert.That(instance.SkippedToonTextureReferenceCount, Is.EqualTo(1));
        }
        [Test]
        public void CreateStaticModelWithSourcePathLoadsGrayscalePngDiffuseTexture()
        {
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "model.pmx");
            string textureDirectory = Path.Combine(temp.Path, "textures");
            Directory.CreateDirectory(textureDirectory);
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
            WriteGrayscalePng(Path.Combine(textureDirectory, "gray.png"));

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].texture = Path.Combine("textures", "gray.png");

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model, pmxPath));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.LoadedDiffuseTextureCount, Is.EqualTo(1));
            Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(0));
            Assert.That(instance.MaterialBindingDiagnostics[0].baseMapBound, Is.True);
        }
        [Test]
        public void CreateStaticModelWithSourcePathLoadsRgbPngDiffuseTexture()
        {
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "model.pmx");
            string textureDirectory = Path.Combine(temp.Path, "textures");
            Directory.CreateDirectory(textureDirectory);
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
            WriteRgbPng(Path.Combine(textureDirectory, "rgb.png"));

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].texture = Path.Combine("textures", "rgb.png");

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model, pmxPath));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.LoadedDiffuseTextureCount, Is.EqualTo(1));
            Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(0));
            Assert.That(instance.MaterialBindingDiagnostics[0].baseMapBound, Is.True);
        }
        [Test]
        public void CreateStaticModelClassifiesPngCutoutTextureAsAlphaTest()
        {
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "model.pmx");
            string textureDirectory = Path.Combine(temp.Path, "textures");
            Directory.CreateDirectory(textureDirectory);
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
            WriteCutoutPng(Path.Combine(textureDirectory, "cutout.png"));

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].texture = Path.Combine("textures", "cutout.png");

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model, pmxPath));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Geometry));
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_ZWrite"), Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_AlphaClipThreshold"), Is.EqualTo(0.01f).Within(0.00001f));
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_ShadowAlphaClipThreshold"), Is.EqualTo(0.01f).Within(0.00001f));
            Assert.That(instance.MaterialBindingDiagnostics[0].isTransparent, Is.True);
            Assert.That(instance.MaterialBindingDiagnostics[0].transparencyMode, Is.EqualTo("alphaTest"));
            Assert.That(instance.MaterialBindingDiagnostics[0].renderOrderBucket, Is.EqualTo("alphaTest"));
            Assert.That(instance.MaterialBindingDiagnostics[0].materialRenderOrder, Is.EqualTo(0));
            Assert.That(instance.MaterialBindingDiagnostics[0].outlineRenderOrder, Is.EqualTo(1));
            Assert.That(instance.MaterialBindingDiagnostics[0].transparentOrder, Is.EqualTo(-1));
            Assert.That(instance.MaterialBindingDiagnostics[0].transparentPolicy, Is.EqualTo("mmd-material-alpha-test-depth-write"));
        }
        [Test]
        public void CreateStaticModelBlendsNearOpaqueTextureAlphaLikeRealMmd()
        {
            // Real MMD always multiplies the diffuse texture alpha into the fragment and alpha-blends;
            // it has no "opaque-enough" threshold. A hair texture whose used-UV alpha is high but not
            // fully opaque (Sour_Miku_Black's hair.png strand edges fall to ~213/255) must therefore
            // alpha-blend, not snap to opaque. Regression: the old 195 opaque threshold absorbed the
            // 195-254 soft band and rendered such hair fully opaque, diverging from the GoldenOracle.
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "model.pmx");
            string textureDirectory = Path.Combine(temp.Path, "textures");
            Directory.CreateDirectory(textureDirectory);
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
            // Uniform 213/255: high but not fully opaque. Coverage-independent so the assertion
            // pins the threshold boundary regardless of which texels the triangle UV samples.
            WriteTga32Alpha(Path.Combine(textureDirectory, "near-opaque-hair.tga"), width: 4, height: 4, alpha: 213);

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].name = "hair";
            model.materials[0].texture = Path.Combine("textures", "near-opaque-hair.tga");

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model, pmxPath));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.MaterialBindingDiagnostics[0].transparencyMode, Is.EqualTo("alphaBlend"));
            Assert.That(instance.MaterialBindingDiagnostics[0].isTransparent, Is.True);
            Assert.That(instance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
            Assert.That(
                ReadMaterialFloat(instance.Materials[0], "_DstBlend"),
                Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha).Within(0.00001f));
        }
        [Test]
        public void CreateStaticModelIgnoresTransparentAtlasPaddingOutsideUsedUvs()
        {
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "model.pmx");
            string textureDirectory = Path.Combine(temp.Path, "textures");
            Directory.CreateDirectory(textureDirectory);
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
            WriteAtlasPaddingPng(Path.Combine(textureDirectory, "atlas-padding.png"));

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.vertices[0].uv = new[] { 0.40f, 0.60f };
            model.vertices[1].uv = new[] { 0.60f, 0.60f };
            model.vertices[2].uv = new[] { 0.40f, 0.40f };
            model.materials[0].name = "mat_atlas_padding";
            model.materials[0].texture = Path.Combine("textures", "atlas-padding.png");

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model, pmxPath));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Geometry));
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_AlphaClipThreshold"), Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(instance.MaterialBindingDiagnostics[0].isTransparent, Is.False);
            Assert.That(instance.MaterialBindingDiagnostics[0].transparencyMode, Is.EqualTo("opaque"));
            Assert.That(instance.MaterialBindingDiagnostics[0].renderOrderBucket, Is.EqualTo("opaque"));
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
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "model.pmx");
            string textureDirectory = Path.Combine(temp.Path, "textures");
            Directory.CreateDirectory(textureDirectory);
            WriteTga32Alpha(Path.Combine(textureDirectory, "tga-regular-masked.tga"), width: 4, height: 4, alpha: 96);
            WriteTga32Alpha(Path.Combine(textureDirectory, "tga-regular-solid.tga"), width: 4, height: 4, alpha: 255);

            MmdModelDefinition maskedModel = CreateMinimalTriangleModel(includeTextureReferences: false);
            maskedModel.materials[0].name = "mat_tga_regular_hair";
            maskedModel.materials[0].texture = Path.Combine("textures", "tga-regular-masked.tga");

            MmdModelDefinition opaqueModel = CreateMinimalTriangleModel(includeTextureReferences: false);
            opaqueModel.materials[0].name = "mat_tga_regular_solid";
            opaqueModel.materials[0].texture = Path.Combine("textures", "tga-regular-solid.tga");

            using var maskedScope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(maskedModel, pmxPath));
            MmdUnityModelInstance maskedInstance = maskedScope.Instance;
            using var opaqueScope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(opaqueModel, pmxPath));
            MmdUnityModelInstance opaqueInstance = opaqueScope.Instance;

            // Partial alpha on a regular-named TGA now blends (was wrongly forced opaque before).
            Assert.That(maskedInstance.MaterialBindingDiagnostics[0].isTransparent, Is.True);
            Assert.That(maskedInstance.MaterialBindingDiagnostics[0].transparencyMode, Is.EqualTo("alphaBlend"));
            Assert.That(maskedInstance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));

            // A fully-opaque TGA stays opaque purely from its alpha content.
            Assert.That(opaqueInstance.MaterialBindingDiagnostics[0].isTransparent, Is.False);
            Assert.That(opaqueInstance.MaterialBindingDiagnostics[0].transparencyMode, Is.EqualTo("opaque"));
            Assert.That(opaqueInstance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Geometry));
        }
        [Test]
        public void CreateStaticModelWithSourcePathLoadsRelativeTgaDiffuseTexture()
        {
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "model.pmx");
            string textureDirectory = Path.Combine(temp.Path, "tex");
            Directory.CreateDirectory(textureDirectory);
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
            WriteTga24(Path.Combine(textureDirectory, "diffuse.TGA"), width: 2, height: 2);

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].texture = Path.Combine("tex", "diffuse.TGA");

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model, pmxPath));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.LoadedDiffuseTextureCount, Is.EqualTo(1));
            Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(0));
            Assert.That(instance.OwnedTextures, Has.Length.EqualTo(1));
            Assert.That(instance.OwnedTextures[0].width, Is.EqualTo(2));
            Assert.That(instance.OwnedTextures[0].height, Is.EqualTo(2));
            Assert.That(ReadBoundDiffuseTexture(instance.Materials[0]), Is.EqualTo(instance.OwnedTextures[0]));
        }
        [Test]
        public void CreateStaticModelWithSourcePathLoadsRelativeDdsDiffuseTexture()
        {
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "model.pmx");
            string textureDirectory = Path.Combine(temp.Path, "tex");
            Directory.CreateDirectory(textureDirectory);
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
            WriteDdsDxt3(Path.Combine(textureDirectory, "diffuse.dds"));

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].texture = Path.Combine("tex", "diffuse.dds");

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model, pmxPath));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.LoadedDiffuseTextureCount, Is.EqualTo(1));
            Assert.That(instance.UnsupportedTextureReferenceCount, Is.EqualTo(0));
            Assert.That(instance.OwnedTextures, Has.Length.EqualTo(1));
            Assert.That(instance.OwnedTextures[0].width, Is.EqualTo(4));
            Assert.That(instance.OwnedTextures[0].height, Is.EqualTo(4));
            Assert.That(ReadBoundDiffuseTexture(instance.Materials[0]), Is.EqualTo(instance.OwnedTextures[0]));
        }
        [Test]
        public void CreateStaticModelWithSourcePathLoadsSphereAndToonTexturesForDiagnostics()
        {
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "model.pmx");
            string sphereDirectory = Path.Combine(temp.Path, "sp");
            string toonDirectory = Path.Combine(temp.Path, "spt");
            Directory.CreateDirectory(sphereDirectory);
            Directory.CreateDirectory(toonDirectory);
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
            WriteBmp24(Path.Combine(sphereDirectory, "sphere.bmp"), width: 2, height: 2);
            WriteJpg(Path.Combine(toonDirectory, "toon.jpg"), Color.green);

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].sphereTexture = Path.Combine("sp", "sphere.bmp");
            model.materials[0].toonTexture = Path.Combine("spt", "toon.jpg");

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model, pmxPath));
            MmdUnityModelInstance instance = scope.Instance;

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
    }
}
