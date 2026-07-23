#nullable enable

using System.IO;
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
            Assert.That(instance.Materials[0].GetTexture("_BaseMap"), Is.EqualTo(instance.OwnedTextures[0]));
            Assert.That(instance.Materials[1].GetTexture("_BaseMap"), Is.EqualTo(instance.OwnedTextures[1]));
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
    }
}
