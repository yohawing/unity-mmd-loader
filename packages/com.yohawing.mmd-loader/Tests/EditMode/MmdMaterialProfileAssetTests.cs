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

        // Bool members of the rendering targets. null = every bool at its default; otherwise only
        // that member is flipped, so two swapped bools cannot hide behind equal values.
        private static readonly object?[] RenderingTargetBoolCases =
        {
            null,
            nameof(MmdMaterialProfileRenderingTargets.supportsRenderQueue),
            nameof(MmdMaterialProfileRenderingTargets.validatePropertyPresence),
            nameof(MmdMaterialProfileRenderingTargets.supportsMaterialMorphs)
        };

        // profile asset -> runtime targets -> provenance (MmdPmxAsset) -> runtime targets. Members are
        // enumerated by reflection and filled with mutually distinct values, so a swapped or dropped
        // argument in any of the hand-written copies fails here.
        [TestCaseSource(nameof(RenderingTargetBoolCases))]
        public void RenderingTargetsRoundTripEveryMemberThroughRuntimeAndProvenance(string? flippedBool)
        {
            FieldInfo[] profileFields = typeof(MmdMaterialProfileRenderingTargets)
                .GetFields(BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo[] runtimeProperties = typeof(MmdMaterialRenderingTargets)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);
            AssertMemberBijection(profileFields, runtimeProperties);
            Assert.That(
                RenderingTargetBoolCases.Length - 1,
                Is.EqualTo(Array.FindAll(profileFields, field => field.FieldType == typeof(bool)).Length),
                "Add every bool member of the rendering targets to RenderingTargetBoolCases.");

            var profile = new MmdMaterialProfileRenderingTargets();
            FillDistinctValues(profile, profileFields, flippedBool);

            MmdMaterialRenderingTargets runtime = profile.ToRuntimeTargets();
            foreach (FieldInfo field in profileFields)
            {
                object? expected = field.GetValue(profile);
                if (field.Name == nameof(MmdMaterialProfileRenderingTargets.unsupportedFeatures) &&
                    !profile.supportsMaterialMorphs)
                {
                    expected = Append((string[])expected!, "material-morph");
                }

                AssertMemberValue(field.Name, expected, FindRuntimeProperty(runtimeProperties, field).GetValue(runtime));
            }

            MmdMaterialProfileProvenance provenance = MmdMaterialProfileProvenance.Create(
                "round-trip-id",
                "round-trip-name",
                new[] { runtime });
            foreach (FieldInfo field in profileFields)
            {
                AssertMemberValue(
                    "provenance." + field.Name,
                    FindRuntimeProperty(runtimeProperties, field).GetValue(runtime),
                    field.GetValue(provenance.renderingTargets));
            }

            Assert.That(
                provenance.TryCreateRuntimeRenderingTargets(1, out MmdMaterialRenderingTargets[] restored, out string reason),
                Is.True,
                reason);
            foreach (PropertyInfo property in runtimeProperties)
            {
                AssertMemberValue("restored." + property.Name, property.GetValue(runtime), property.GetValue(restored[0]));
            }
        }

        [Test]
        public void TextureTargetsMapEveryMemberToRuntimeTargets()
        {
            FieldInfo[] profileFields = typeof(MmdMaterialProfileTextureTargets)
                .GetFields(BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo[] runtimeProperties = typeof(MmdMaterialTextureTargets)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);
            AssertMemberBijection(profileFields, runtimeProperties);

            var profile = new MmdMaterialProfileTextureTargets();
            FillDistinctValues(profile, profileFields, flippedBool: null);

            MmdMaterialTextureTargets runtime = profile.ToRuntimeTargets();
            foreach (FieldInfo field in profileFields)
            {
                AssertMemberValue(field.Name, field.GetValue(profile), FindRuntimeProperty(runtimeProperties, field).GetValue(runtime));
            }
        }

        private static void AssertMemberBijection(FieldInfo[] profileFields, PropertyInfo[] runtimeProperties)
        {
            string[] profileNames = Array.ConvertAll(profileFields, field => ToPascalCase(field.Name));
            string[] runtimeNames = Array.ConvertAll(runtimeProperties, property => property.Name);
            Array.Sort(profileNames, StringComparer.Ordinal);
            Array.Sort(runtimeNames, StringComparer.Ordinal);
            Assert.That(profileNames, Is.EqualTo(runtimeNames),
                "Profile fields and runtime target properties must correspond one to one by name.");
        }

        private static PropertyInfo FindRuntimeProperty(PropertyInfo[] runtimeProperties, FieldInfo field)
        {
            string name = ToPascalCase(field.Name);
            return Array.Find(runtimeProperties, property => property.Name == name)
                ?? throw new AssertionException($"Runtime property {name} is missing.");
        }

        private static void FillDistinctValues(object target, FieldInfo[] fields, string? flippedBool)
        {
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == typeof(string))
                {
                    field.SetValue(target, "_RoundTrip_" + field.Name);
                }
                else if (field.FieldType == typeof(string[]))
                {
                    field.SetValue(target, new[] { "rt-" + field.Name + "-a", "rt-" + field.Name + "-b" });
                }
                else if (field.FieldType == typeof(bool))
                {
                    bool value = (bool)field.GetValue(target)!;
                    field.SetValue(target, field.Name == flippedBool ? !value : value);
                }
                else
                {
                    Assert.Fail($"Unsupported target member type {field.FieldType} on {field.Name}. Extend FillDistinctValues.");
                }
            }
        }

        private static void AssertMemberValue(string member, object? expected, object? actual)
        {
            if (expected is System.Collections.IEnumerable expectedList && expected is not string)
            {
                Assert.That(actual, Is.EqualTo(expectedList).AsCollection, member);
                return;
            }

            Assert.That(actual, Is.EqualTo(expected), member);
        }

        private static string[] Append(string[] values, string value)
        {
            var result = new string[values.Length + 1];
            Array.Copy(values, result, values.Length);
            result[values.Length] = value;
            return result;
        }

        private static string ToPascalCase(string name)
        {
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
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
