#nullable enable

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Mmd.Rendering;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

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

        [TestCase(false)]
        [TestCase(true)]
        public void ProfileWithoutResolvedProvenanceFailsClosedAcrossBothResolvers(bool useEmptyMarker)
        {
            var modelAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            var profile = ScriptableObject.CreateInstance<MmdMaterialProfileAsset>();
            try
            {
                SetProfileState(
                    modelAsset,
                    profile,
                    useEmptyMarker ? new MmdMaterialProfileProvenance() : null);

                InvalidOperationException renderingFailure = Assert.Throws<InvalidOperationException>(
                    () => modelAsset.ResolveMaterialRenderingTargets(1))!;
                InvalidOperationException mapperFailure = Assert.Throws<InvalidOperationException>(
                    () => modelAsset.ResolveMaterialMapperSet())!;

                Assert.That(renderingFailure.Message, Is.EqualTo("custom-material-profile-provenance-missing"));
                Assert.That(mapperFailure.Message, Is.EqualTo(renderingFailure.Message));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(modelAsset);
            }
        }

        [Test]
        public void LegacyProfileAbsenceMatchesExplicitDefaultPreset()
        {
            var legacyAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            var emptyMarkerAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            var explicitDefaultAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                SetProfileState(
                    emptyMarkerAsset,
                    profile: null,
                    provenance: new MmdMaterialProfileProvenance());
                explicitDefaultAsset.Initialize(
                    new byte[] { 1 },
                    "default.pmx",
                    "Assets/default.pmx",
                    assetShaderPreset: "MMD Basic Toon");

                Assert.That(legacyAsset.MaterialProfileAsset, Is.Null);
                Assert.That(legacyAsset.MaterialProfileProvenance, Is.Null);
                Assert.That(legacyAsset.ResolveMaterialMapperSet(), Is.Null);
                Assert.That(legacyAsset.ResolveMaterialRenderingTargets(1), Is.Null);
                Assert.That(
                    MmdUnityPlaybackBinding.ResolveMaterialPreset(legacyAsset),
                    Is.EqualTo(MmdUnityPlaybackBinding.ResolveMaterialPreset(explicitDefaultAsset)));
                Assert.That(
                    MmdUnityPlaybackBinding.ResolveMaterialPreset(legacyAsset),
                    Is.EqualTo(MmdMaterialPreset.MmdToon));
                Assert.That(emptyMarkerAsset.ResolveMaterialMapperSet(), Is.Null);
                Assert.That(emptyMarkerAsset.ResolveMaterialRenderingTargets(1), Is.Null);
                Assert.That(
                    MmdUnityPlaybackBinding.ResolveMaterialPreset(emptyMarkerAsset),
                    Is.EqualTo(MmdMaterialPreset.MmdToon));
                Assert.That(explicitDefaultAsset.ResolveMaterialMapperSet(), Is.Null);
                Assert.That(explicitDefaultAsset.ResolveMaterialRenderingTargets(1), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(explicitDefaultAsset);
                Object.DestroyImmediate(emptyMarkerAsset);
                Object.DestroyImmediate(legacyAsset);
            }
        }

        [Test]
        public void PersistedProvenanceSupportsSceneTargetsWithoutProfileAssetButFreshMapperFailsClosed()
        {
            var modelAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                MmdMaterialProfileProvenance provenance = MmdMaterialProfileProvenance.Create(
                    "profile-id",
                    "profile-name",
                    new[]
                    {
                        new MmdMaterialRenderingTargets(supportsMaterialMorphs: false)
                    });
                SetProfileState(modelAsset, profile: null, provenance: provenance);

                MmdMaterialRenderingTargets[] targets = modelAsset.ResolveMaterialRenderingTargets(1)!;
                Assert.That(targets, Has.Length.EqualTo(1));
                Assert.That(targets[0].SupportsMaterialMorphs, Is.False);
                InvalidOperationException mapperFailure = Assert.Throws<InvalidOperationException>(
                    () => modelAsset.ResolveMaterialMapperSet())!;
                Assert.That(mapperFailure.Message, Is.EqualTo("custom-material-profile-asset-missing"));
            }
            finally
            {
                Object.DestroyImmediate(modelAsset);
            }
        }

        private static void SetProfileState(
            MmdPmxAsset modelAsset,
            MmdMaterialProfileAsset? profile,
            MmdMaterialProfileProvenance? provenance)
        {
            FieldInfo profileField = typeof(MmdPmxAsset).GetField(
                "materialProfileAsset",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo provenanceField = typeof(MmdPmxAsset).GetField(
                "materialProfileProvenance",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            profileField.SetValue(modelAsset, profile);
            provenanceField.SetValue(modelAsset, provenance);
        }
    }
}
