#nullable enable

using Mmd.Mme;
using NUnit.Framework;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmeFxParserTests
    {
        private const string AlternativeFullSample = @"/* ------------------------------------------------------------
 * AlternativeFull
 * ------------------------------------------------------------ */
/* created by AlternativeFullFrontend. */
#define TEXTURE_THRESHOLD ""shading_hint.png""
#define USE_MATERIAL_TEXTURE
#define USE_NORMALMAP
#define TEXTURE_NORMALMAP ""body.png""
float NormalMapResolution = 1;
#define USE_SELFSHADOW_MODE
#define USE_NONE_SELFSHADOW_MODE
#define USE_SOFT_SHADOW
float SoftShadowParam = 2;
float SelfShadowPower = 1;
#define USE_MATERIAL_SPECULAR
#define USE_MATERIAL_SPHERE
float3 DefaultModeShadowColor = {1,1,1};
#define MAX_ANISOTROPY 16

#include ""AlternativeFull.fxsub""
";

        private const string RayMmdSample = @"#define ALBEDO_MAP_FROM 1
#define ALBEDO_MAP_FILE ""albedo.png""
#define NORMAL_MAP_FROM 1
#define NORMAL_MAP_FILE ""normal.png""
#define SMOOTHNESS_MAP_FROM 0
#define METALNESS_MAP_FROM 0

static const float smoothness = 0.3;
static const float metalness = 0.0;

#include ""../../ray-mmd/Materials/material_common_2.0.fxsub""
";

        [Test]
        public void TryParse_AlternativeFull_ExtractsEffectType()
        {
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(AlternativeFullSample, "fx/body.fx");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.effectType, Is.EqualTo("AlternativeFull"));
        }

        [Test]
        public void TryParse_AlternativeFull_ExtractsNormalMapTexture()
        {
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(AlternativeFullSample, "fx/body.fx");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.normalMapTexture, Is.EqualTo("body.png"));
        }

        [Test]
        public void TryParse_AlternativeFull_ExtractsThresholdTexture()
        {
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(AlternativeFullSample, "fx/body.fx");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.thresholdTexture, Is.EqualTo("shading_hint.png"));
        }

        [Test]
        public void TryParse_AlternativeFull_ExtractsFeatureFlags()
        {
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(AlternativeFullSample, "fx/body.fx");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.useNormalMap, Is.True);
            Assert.That(result.useMaterialTexture, Is.True);
            Assert.That(result.useMaterialSpecular, Is.True);
            Assert.That(result.useMaterialSphere, Is.True);
            Assert.That(result.useSelfShadow, Is.True);
            Assert.That(result.useSoftShadow, Is.True);
        }

        [Test]
        public void TryParse_AlternativeFull_ExtractsFloatParameters()
        {
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(AlternativeFullSample, "fx/body.fx");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.normalMapResolution, Is.EqualTo(1.0f));
            Assert.That(result.softShadowParam, Is.EqualTo(2.0f));
            Assert.That(result.selfShadowPower, Is.EqualTo(1.0f));
        }

        [Test]
        public void TryParse_AlternativeFull_ExtractsMaxAnisotropy()
        {
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(AlternativeFullSample, "fx/body.fx");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.maxAnisotropy, Is.EqualTo(16));
        }

        [Test]
        public void TryParse_RayMmd_ExtractsEffectType()
        {
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(RayMmdSample, "material.fx");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.effectType, Is.EqualTo("ray-mmd"));
        }

        [Test]
        public void TryParse_RayMmd_ExtractsAlbedoMapFile()
        {
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(RayMmdSample, "material.fx");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.albedoMapTexture, Is.EqualTo("albedo.png"));
        }

        [Test]
        public void TryParse_RayMmd_ExtractsNormalMapFile()
        {
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(RayMmdSample, "material.fx");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.normalMapFile, Is.EqualTo("normal.png"));
        }

        [Test]
        public void TryParse_RayMmd_ExtractsPbrParameters()
        {
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(RayMmdSample, "material.fx");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.smoothness, Is.EqualTo(0.3f));
            Assert.That(result.metalness, Is.EqualTo(0.0f));
        }

        [Test]
        public void TryParse_PreservesExtendedFloatParameters()
        {
            string content = @"float IBL_SkyIntensity = 1.25f;
static const float RimLightPower = .5;
float Rim_Intensity = 2;
float SpecularLight_Strength = 0.75;
float Subsurface_Scale = 0.125;
float Toon_RampOffset = -1.5e-1;
float Alpha_Threshold = 0.02;
#include ""AlternativeFull.fxsub""
";

            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(content, "fx/body.fx");

            Assert.That(result, Is.Not.Null);
            AssertFloatParameter(result!, "IBL_SkyIntensity", 1.25f);
            AssertFloatParameter(result, "RimLightPower", 0.5f);
            AssertFloatParameter(result, "Rim_Intensity", 2.0f);
            AssertFloatParameter(result, "SpecularLight_Strength", 0.75f);
            AssertFloatParameter(result, "Subsurface_Scale", 0.125f);
            AssertFloatParameter(result, "Toon_RampOffset", -0.15f);
            AssertFloatParameter(result, "Alpha_Threshold", 0.02f);
        }

        [Test]
        public void TryParse_EmptyContent_ReturnsNull()
        {
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse("", "test.fx");
            Assert.That(result, Is.Null);
        }

        [Test]
        public void TryParse_NullContent_ReturnsNull()
        {
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(null!, "test.fx");
            Assert.That(result, Is.Null);
        }

        [Test]
        public void TryParse_NoInclude_ReturnsNull()
        {
            string content = @"#define USE_NORMALMAP
float SoftShadowParam = 2;
";
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(content, "test.fx");
            Assert.That(result, Is.Null);
        }

        [Test]
        public void TryParse_PreservesSourcePath()
        {
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(AlternativeFullSample, "fx/body.fx");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.sourcePath, Is.EqualTo("fx/body.fx"));
        }

        [Test]
        public void TryParse_UnknownInclude_ReturnsUnknownType()
        {
            string content = @"#define USE_NORMALMAP
#include ""CustomEffect.fxsub""
";
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(content, "test.fx");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.effectType, Is.EqualTo("unknown"));
        }

        [Test]
        public void TryParse_RayMmd_DefaultsForUnsetFlags()
        {
            MmeFxEffectDescriptor? result = MmeFxParser.TryParse(RayMmdSample, "material.fx");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.useNormalMap, Is.False);
            Assert.That(result.useSoftShadow, Is.False);
            Assert.That(result.maxAnisotropy, Is.EqualTo(0));
        }

        private static void AssertFloatParameter(
            MmeFxEffectDescriptor descriptor,
            string name,
            float expected)
        {
            Assert.That(descriptor.TryGetFloatParameter(name, out float value), Is.True);
            Assert.That(value, Is.EqualTo(expected).Within(1e-6f));
        }
    }
}
