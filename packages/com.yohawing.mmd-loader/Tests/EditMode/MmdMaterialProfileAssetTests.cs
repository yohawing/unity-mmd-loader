#nullable enable

using NUnit.Framework;
using UnityEngine;
using Mmd.Rendering;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdMaterialProfileAssetTests
    {
        [Test]
        public void ProfileCreatesExplicitMapperContract()
        {
            var profile = ScriptableObject.CreateInstance<MmdMaterialProfileAsset>();
            try
            {
                Shader shader = Shader.Find(MmdUrpMaterialBindingDescriptorBuilder.UrpLitShaderName);
                Assert.That(shader, Is.Not.Null);
                profile.shader = shader;
                profile.textureTargets = new MmdMaterialProfileTextureTargets
                {
                    diffuseTextureProperties = new[] { "_BaseMap" },
                    sphereTextureProperty = string.Empty,
                    sphereTextureBoundProperty = "_MatCap",
                    toonTextureProperty = string.Empty
                };
                profile.renderingTargets = new MmdMaterialProfileRenderingTargets
                {
                    baseColorProperty = "_BaseColor",
                    alphaClipThresholdProperty = "_Cutoff",
                    textureAlphaClipMaskProperty = "_AlphaMask",
                    alphaClipModeProperty = "_ClipMode",
                    outlineColorProperty = string.Empty,
                    unsupportedFeatures = new[] { "sphere-texture", "toon-texture", "outline" },
                    requiredKeywords = new[] { "_PROFILE_KEYWORD" },
                    requiredPasses = new[] { "ForwardLit" },
                    supportsMaterialMorphs = false
                };

                Assert.That(profile.TryCreateMapperSet(out MmdMaterialMapperSet? mapperSet, out string reason), Is.True,
                    reason);
                Assert.That(mapperSet, Is.Not.Null);
                Assert.That(mapperSet!.DefaultTextureTargets.SphereTextureProperty, Is.Empty);
                Assert.That(mapperSet.DefaultTextureTargets.SphereTextureBoundProperty, Is.EqualTo("_MatCap"));
                Assert.That(mapperSet.DefaultRenderingTargets.AlphaClipThresholdProperty, Is.EqualTo("_Cutoff"));
                Assert.That(mapperSet.DefaultRenderingTargets.TextureAlphaClipMaskProperty, Is.EqualTo("_AlphaMask"));
                Assert.That(mapperSet.DefaultRenderingTargets.AlphaClipModeProperty, Is.EqualTo("_ClipMode"));
                Assert.That(mapperSet.DefaultRenderingTargets.UnsupportedFeatures,
                    Is.EqualTo(new[] { "sphere-texture", "toon-texture", "outline", "material-morph" }));
                Assert.That(mapperSet.DefaultRenderingTargets.RequiredKeywords,
                    Is.EqualTo(new[] { "_PROFILE_KEYWORD" }));
                Assert.That(mapperSet.DefaultRenderingTargets.RequiredPasses,
                    Is.EqualTo(new[] { "ForwardLit" }));
                Assert.That(mapperSet.DefaultRenderingTargets.SupportsMaterialMorphs, Is.False);

                Material material = mapperSet.DefaultMapper(
                    new MmdMaterialDescriptor(),
                    shader);
                try
                {
                    Assert.That(material.shader, Is.SameAs(shader));
                }
                finally
                {
                    Object.DestroyImmediate(material);
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ProfileRejectsUnsupportedSchemaAndMissingShader()
        {
            var profile = ScriptableObject.CreateInstance<MmdMaterialProfileAsset>();
            try
            {
                profile.schemaVersion = MmdMaterialProfileAsset.CurrentSchemaVersion + 1;
                Assert.That(profile.TryCreateMapperSet(out MmdMaterialMapperSet? mapperSet, out string reason), Is.False);
                Assert.That(mapperSet, Is.Null);
                Assert.That(reason, Is.EqualTo("profile-schema-version-unsupported:2"));

                profile.schemaVersion = MmdMaterialProfileAsset.CurrentSchemaVersion;
                Assert.That(profile.TryCreateMapperSet(out mapperSet, out reason), Is.False);
                Assert.That(mapperSet, Is.Null);
                Assert.That(reason, Is.EqualTo("profile-shader-missing"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
