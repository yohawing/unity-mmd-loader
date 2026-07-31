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
    public sealed partial class MmdUnityModelFactoryTests
    {
        [Test]
        public void CreateStaticModelUsesMaterialIndexMapperOverrideOnlyForSelectedSlot()
        {
            MmdModelDefinition model = CreateTwoTransparentTriangleModel();
            MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);
            MmdMaterialDescriptor? mappedDescriptor = null;
            Shader? mappedDefaultShader = null;
            MmdMaterialMapperSet materialMappers = MmdMaterialMapperSet.BuiltIn.WithMaterialOverride(
                1,
                (source, defaultShader) =>
                {
                    mappedDescriptor = source;
                    mappedDefaultShader = defaultShader;
                    return new Material(defaultShader) { enableInstancing = true };
                });

            using var scope = new MmdTestInstanceScope(
                MmdUnityModelFactory.CreateStaticModel(descriptor, "material-mapper-smoke", materialMappers));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Materials[0].enableInstancing, Is.False);
            Assert.That(instance.Materials[1].enableInstancing, Is.True);
            Assert.That(mappedDescriptor, Is.SameAs(descriptor.materials[1]));
            Assert.That(mappedDefaultShader, Is.SameAs(instance.Materials[1].shader));
            Assert.That(instance.Materials[1].hideFlags,
                Is.EqualTo(HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild));
        }

        [Test]
        public void CreateSkinnedModelAcceptsPublicMaterialMapperSet()
        {
            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            var materialMappers = new MmdMaterialMapperSet(
                (source, defaultShader) => new Material(defaultShader) { enableInstancing = true });

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(
                model,
                sourcePath: null,
                importScale: 1.0f,
                MmdMaterialPreset.MmdToon,
                materialOverride: null,
                materialMappers));

            Assert.That(scope.Instance.Materials[0].enableInstancing, Is.True);
        }

        [Test]
        public void MaterialMapperCanDeclareDiffuseTextureDestination()
        {
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "model.pmx");
            string texturePath = Path.Combine(temp.Path, "diffuse.png");
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
            WriteRgbPng(texturePath);

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].texture = "diffuse.png";
            var targets = new MmdMaterialTextureTargets(
                new[] { "_ToonMap" },
                diffuseTextureBoundProperty: "_ToonMapBound");
            var materialMappers = new MmdMaterialMapperSet(
                (source, defaultShader) => new Material(defaultShader),
                targets);

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(
                model,
                pmxPath,
                importScale: 1.0f,
                preset: MmdMaterialPreset.MmdToonLit,
                materialOverride: null,
                materialMappers: materialMappers));

            Material material = scope.Instance.Materials[0];
            Assert.That(material.GetTexture("_ToonMap"), Is.EqualTo(scope.Instance.OwnedTextures[0]));
            Assert.That(material.GetTexture("_BaseMap"), Is.Null);
            Assert.That(material.GetFloat("_ToonMapBound"), Is.EqualTo(1.0f).Within(0.00001f));
        }

        [Test]
        public void MixedShaderMaterialsUseSlotSpecificMapperTextureDeclarations()
        {
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "mixed-mapper.pmx");
            string texturePath = Path.Combine(temp.Path, "diffuse.png");
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
            WriteRgbPng(texturePath);

            MmdModelDefinition model = CreateTwoTransparentTriangleModel();
            model.materials[0].texture = "diffuse.png";
            model.materials[1].texture = "diffuse.png";
            var slotOneTargets = new MmdMaterialTextureTargets(new[] { "_BaseMap" });
            MmdMaterialMapperSet materialMappers = MmdMaterialMapperSet.BuiltIn.WithMaterialOverride(
                1,
                (source, defaultShader) =>
                {
                    Shader urpLitShader = Shader.Find(MmdUrpMaterialBindingDescriptorBuilder.UrpLitShaderName);
                    Assert.That(urpLitShader, Is.Not.Null);
                    return new Material(urpLitShader) { enableInstancing = true };
                },
                slotOneTargets);

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(
                model,
                pmxPath,
                importScale: 1.0f,
                preset: MmdMaterialPreset.MmdToon,
                materialOverride: null,
                materialMappers: materialMappers));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Materials[0].shader.name,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName));
            Assert.That(instance.Materials[1].shader.name,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.UrpLitShaderName));
            Assert.That(instance.Materials[1].enableInstancing, Is.True);
            Assert.That(instance.OwnedTextures, Has.Length.EqualTo(1));
            Assert.That(instance.Materials[0].GetTexture("_BaseMap"), Is.EqualTo(instance.OwnedTextures[0]));
            Assert.That(instance.Materials[1].GetTexture("_BaseMap"), Is.EqualTo(instance.OwnedTextures[0]));
            Assert.That(instance.MaterialBindingDiagnostics[0].resolvedShaderName,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName));
            Assert.That(instance.MaterialBindingDiagnostics[1].resolvedShaderName,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.UrpLitShaderName));
        }

        [Test]
        public void MaterialMapperMissingDeclaredDiffusePropertyIsReported()
        {
            using var temp = new MmdTestTempScope();

            string pmxPath = Path.Combine(temp.Path, "missing-target.pmx");
            string texturePath = Path.Combine(temp.Path, "diffuse.png");
            File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
            WriteRgbPng(texturePath);

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].texture = "diffuse.png";
            var materialMappers = new MmdMaterialMapperSet(
                (source, defaultShader) => new Material(defaultShader),
                new MmdMaterialTextureTargets(new[] { "_MissingDiffuseProperty" }));

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(
                model,
                pmxPath,
                importScale: 1.0f,
                preset: MmdMaterialPreset.MmdToon,
                materialOverride: null,
                materialMappers: materialMappers));

            Assert.That(scope.Instance.LoadedDiffuseTextureCount, Is.EqualTo(1));
            Assert.That(scope.Instance.Materials[0].GetTexture("_BaseMap"), Is.Null);
            Assert.That(
                scope.Instance.TextureDiagnostics.Messages,
                Does.Contain("Material 0 has no declared diffuse texture property supported by shader 'MMD Basic Toon'."));
        }

        [Test]
        public void MaterialMapperCanDeclareRenderingPropertyTargets()
        {
            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].diffuseColor = new[] { 0.2f, 0.4f, 0.6f };
            model.materials[0].ambientColor = new[] { 0.7f, 0.8f, 0.9f };
            model.materials[0].alpha = 0.5f;
            model.materials[0].edgeColor = new[] { 0.1f, 0.2f, 0.3f, 0.4f };
            model.materials[0].drawEdgeFlag = true;
            model.materials[0].edgeSize = 2.0f;
            model.materials[0].cullingPolicy = "double-sided";

            var renderingTargets = new MmdMaterialRenderingTargets(
                baseColorProperty: "_Color",
                colorProperty: null,
                alphaProperty: "_BaseMapBound",
                outlineColorProperty: "_AmbientColor",
                outlineWidthProperty: "_OutlineScreenSpaceWeight",
                outlineVisibleProperty: "_OutlineVisible",
                outlineScreenSpaceWeightProperty: "_OutlineWidth",
                outlineZTestProperty: "_OutlineZWrite",
                supportsRenderQueue: false);
            var materialMappers = new MmdMaterialMapperSet(
                (source, defaultShader) => new Material(defaultShader),
                MmdMaterialTextureTargets.BuiltIn,
                renderingTargets);

            using var scope = new MmdTestInstanceScope(
                MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.MmdToon,
                    materialOverride: null,
                    materialMappers: materialMappers));
            Material material = scope.Instance.Materials[0];

            Color mappedDiffuse = material.GetColor("_Color");
            Assert.That(mappedDiffuse.r, Is.EqualTo(0.2f).Within(0.00001f));
            Assert.That(mappedDiffuse.g, Is.EqualTo(0.4f).Within(0.00001f));
            Assert.That(mappedDiffuse.b, Is.EqualTo(0.6f).Within(0.00001f));
            Assert.That(mappedDiffuse.a, Is.EqualTo(0.5f).Within(0.00001f));
            Color untouchedBaseColor = material.GetColor("_BaseColor");
            Assert.That(untouchedBaseColor.r, Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(untouchedBaseColor.g, Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(untouchedBaseColor.b, Is.EqualTo(1.0f).Within(0.00001f));
            Color mappedOutline = material.GetColor("_AmbientColor");
            Assert.That(mappedOutline.r, Is.EqualTo(0.1f).Within(0.00001f));
            Assert.That(mappedOutline.g, Is.EqualTo(0.2f).Within(0.00001f));
            Assert.That(mappedOutline.b, Is.EqualTo(0.3f).Within(0.00001f));
            Assert.That(mappedOutline.a, Is.EqualTo(0.4f).Within(0.00001f));
            Assert.That(material.GetFloat("_BaseMapBound"), Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(material.GetFloat("_Cull"), Is.EqualTo((float)UnityEngine.Rendering.CullMode.Off));
            Assert.That(material.GetFloat("_OutlineScreenSpaceWeight"), Is.EqualTo(2.0f).Within(0.00001f));
            Assert.That(material.GetFloat("_OutlineWidth"), Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(material.GetFloat("_OutlineZWrite"), Is.EqualTo(2.0f).Within(0.00001f));
            Assert.That(material.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Geometry));
        }

        [Test]
        public void MaterialMapperBindingDiagnosticsReportUnsupportedFeaturesAndMissingProperties()
        {
            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.materials[0].alpha = 0.5f;
            model.materials[0].cullingPolicy = "double-sided";
            model.materials[0].drawEdgeFlag = true;
            model.materials[0].edgeSize = 1.0f;
            var renderingTargets = new MmdMaterialRenderingTargets(
                baseColorProperty: "_MissingBaseColor",
                alphaProperty: "_MissingAlpha",
                cullProperty: "_MissingCull",
                outlineColorProperty: "_MissingOutlineColor",
                unsupportedFeatures: new[] { "sphere-texture", "toon-texture" });
            var materialMappers = new MmdMaterialMapperSet(
                (source, defaultShader) => new Material(defaultShader),
                MmdMaterialTextureTargets.BuiltIn,
                renderingTargets);

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(
                model,
                sourcePath: null,
                importScale: 1.0f,
                preset: MmdMaterialPreset.MmdToon,
                materialOverride: null,
                materialMappers: materialMappers));
            MmdUnityMaterialBindingDiagnostic diagnostic = scope.Instance.MaterialBindingDiagnostics[0];

            Assert.That(diagnostic.unsupportedFeatures,
                Is.EqualTo(new[] { "sphere-texture", "toon-texture" }));
            Assert.That(diagnostic.missingProperties.Any(item =>
                item.feature == "base-color" && item.property == "_MissingBaseColor"), Is.True);
            Assert.That(diagnostic.missingProperties.Any(item =>
                item.feature == "alpha" && item.property == "_MissingAlpha"), Is.True);
            Assert.That(diagnostic.missingProperties.Any(item =>
                item.feature == "culling" && item.property == "_MissingCull"), Is.True);
            Assert.That(diagnostic.missingProperties.Any(item =>
                item.feature == "outline-color" && item.property == "_MissingOutlineColor"), Is.True);
        }
    }
}
