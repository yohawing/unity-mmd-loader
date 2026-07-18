#nullable enable

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
    }
}
