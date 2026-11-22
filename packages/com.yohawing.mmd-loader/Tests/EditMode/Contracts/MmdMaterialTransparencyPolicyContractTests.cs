#nullable enable

using System;
using System.Linq;
using NUnit.Framework;
using Mmd.Rendering;

namespace Mmd.Tests
{
    /// <summary>
    /// Pins the texture-alpha transparency thresholds. These values drove past visual regressions
    /// (the earlier 195 opaque cutoff rendered soft hair masks as solid), so each boundary is
    /// exercised directly on the pure policy instead of only through imported materials.
    /// </summary>
    [TestFixture]
    public sealed class MmdMaterialTransparencyPolicyContractTests
    {
        [Test]
        public void ThresholdConstantsMatchTheReferenceRule()
        {
            Assert.That(MmdMaterialTransparencyPolicy.TextureOpaqueAlphaThreshold, Is.EqualTo(255));
            Assert.That(MmdMaterialTransparencyPolicy.PartialAlphaRatioThreshold, Is.EqualTo(0.25f));
            Assert.That(MmdMaterialTransparencyPolicy.TextureAlphaBlendThreshold, Is.EqualTo(100));
        }

        [Test]
        public void ClassifyTextureAlphaRejectsNullAndTreatsEmptyAsOpaque()
        {
            Assert.Throws<ArgumentNullException>(() => MmdMaterialTransparencyPolicy.ClassifyTextureAlpha(null!));

            MmdMaterialTransparencyPolicy.TextureAlphaScanResult empty =
                MmdMaterialTransparencyPolicy.ClassifyTextureAlpha(Array.Empty<int>());
            Assert.That(empty.textureAlphaMode, Is.EqualTo(MmdMaterialTransparencyPolicy.ModeOpaque));
            Assert.That(empty.textureAlphaClassificationReason, Is.EqualTo("no-pixels"));
            Assert.That(empty.pixelCount, Is.Zero);
        }

        [TestCase(254, "alphaBlend", "fallback-alpha-blend")]
        [TestCase(255, "opaque", "all-pixels-above-opaque-threshold")]
        [TestCase(256, "opaque", "all-pixels-above-opaque-threshold")]
        public void ClassifyTextureAlphaOpaqueOnlyWhenEveryTexelReachesTheOpaqueThreshold(
            int minimumAlpha,
            string expectedMode,
            string expectedReason)
        {
            int[] alpha = Enumerable.Repeat(255, 99).Append(minimumAlpha).ToArray();

            MmdMaterialTransparencyPolicy.TextureAlphaScanResult scan =
                MmdMaterialTransparencyPolicy.ClassifyTextureAlpha(alpha);

            Assert.That(scan.textureAlphaMode, Is.EqualTo(expectedMode));
            Assert.That(scan.textureAlphaClassificationReason, Is.EqualTo(expectedReason));
            Assert.That(scan.minAlpha, Is.EqualTo(Math.Min(255, minimumAlpha)));
            Assert.That(scan.pixelCount, Is.EqualTo(100));
        }

        [TestCase(24, "alphaTest", "hard-cutout-sparse-partial-alpha")]
        [TestCase(25, "alphaBlend", "dense-partial-alpha")]
        [TestCase(26, "alphaBlend", "dense-partial-alpha")]
        public void ClassifyTextureAlphaDensePartialCoverageAtQuarterRatioBlends(
            int partialPercent,
            string expectedMode,
            string expectedReason)
        {
            // Low partial alpha (10) keeps the +100 margin far below max alpha, so only the coverage
            // ratio decides between hard cutout and dense blend.
            int[] alpha = Enumerable.Repeat(255, 100 - partialPercent)
                .Concat(Enumerable.Repeat(10, partialPercent))
                .ToArray();

            MmdMaterialTransparencyPolicy.TextureAlphaScanResult scan =
                MmdMaterialTransparencyPolicy.ClassifyTextureAlpha(alpha);

            Assert.That(scan.textureAlphaMode, Is.EqualTo(expectedMode));
            Assert.That(scan.textureAlphaClassificationReason, Is.EqualTo(expectedReason));
            Assert.That(scan.middleAlphaCount, Is.EqualTo(partialPercent));
            Assert.That(scan.middleAlphaRatio, Is.EqualTo(partialPercent / 100.0f).Within(1e-6f));
        }

        [TestCase(0, "alphaTest", "hard-cutout-sparse-partial-alpha")]
        [TestCase(154, "alphaTest", "hard-cutout-sparse-partial-alpha")]
        [TestCase(155, "alphaBlend", "fallback-alpha-blend")]
        [TestCase(200, "alphaBlend", "fallback-alpha-blend")]
        public void ClassifyTextureAlphaSparsePartialUsesTheBlendMarginAgainstMaxAlpha(
            int partialAlpha,
            string expectedMode,
            string expectedReason)
        {
            // 10% partial coverage stays below the dense ratio. Average partial alpha + 100 must be
            // strictly below max alpha (255) for a hard cutout; alpha 0 is not a partial texel.
            int[] alpha = Enumerable.Repeat(255, 90).Concat(Enumerable.Repeat(partialAlpha, 10)).ToArray();

            MmdMaterialTransparencyPolicy.TextureAlphaScanResult scan =
                MmdMaterialTransparencyPolicy.ClassifyTextureAlpha(alpha);

            Assert.That(scan.textureAlphaMode, Is.EqualTo(expectedMode));
            Assert.That(scan.textureAlphaClassificationReason, Is.EqualTo(expectedReason));
            Assert.That(scan.maxAlpha, Is.EqualTo(255));
            Assert.That(scan.averageMiddleAlpha, Is.EqualTo((float)partialAlpha).Within(1e-4f));
        }

        [Test]
        public void ResolveMaterialTransparencyModeAppliesMaterialAlphaBeforeTextureScan()
        {
            int[] opaque = Enumerable.Repeat(255, 16).ToArray();

            Assert.That(
                MmdMaterialTransparencyPolicy.ResolveMaterialTransparencyMode(0.99f, "body", opaque),
                Is.EqualTo(MmdMaterialTransparencyPolicy.ModeAlphaBlend));
            Assert.That(
                MmdMaterialTransparencyPolicy.ResolveMaterialTransparencyMode(1.0f, "body", null),
                Is.EqualTo(MmdMaterialTransparencyPolicy.ModeOpaque));
            Assert.That(
                MmdMaterialTransparencyPolicy.ResolveMaterialTransparencyMode(1.0f, "body", Array.Empty<int>()),
                Is.EqualTo(MmdMaterialTransparencyPolicy.ModeOpaque));
            Assert.That(
                MmdMaterialTransparencyPolicy.ResolveMaterialTransparencyMode(1.0f, "body", opaque),
                Is.EqualTo(MmdMaterialTransparencyPolicy.ModeOpaque));
        }

        [TestCase("body hair", "alphaTest")]
        [TestCase("hair shadow", "alphaBlend")]
        [TestCase("頰", "alphaBlend")]
        public void ResolveMaterialTransparencyModeUpgradesHardCutoutOnlyForSoftOverlayNames(
            string materialName,
            string expectedMode)
        {
            int[] hardCutout = Enumerable.Repeat(255, 90).Concat(Enumerable.Repeat(0, 10)).ToArray();

            Assert.That(
                MmdMaterialTransparencyPolicy.ResolveMaterialTransparencyMode(1.0f, materialName, hardCutout),
                Is.EqualTo(expectedMode));
        }

        [Test]
        public void ResolveMaterialTransparencyModeOverlayNameDoesNotChangeOpaqueOrBlendScans()
        {
            int[] opaque = Enumerable.Repeat(255, 16).ToArray();
            int[] dense = Enumerable.Repeat(255, 2).Concat(Enumerable.Repeat(128, 2)).ToArray();

            Assert.That(
                MmdMaterialTransparencyPolicy.ResolveMaterialTransparencyMode(1.0f, "影", opaque),
                Is.EqualTo(MmdMaterialTransparencyPolicy.ModeOpaque));
            Assert.That(
                MmdMaterialTransparencyPolicy.ResolveMaterialTransparencyMode(1.0f, "body", dense),
                Is.EqualTo(MmdMaterialTransparencyPolicy.ModeAlphaBlend));
        }

        [TestCase("Hair Shadow")]
        [TestCase("hairshadow")]
        [TestCase("SHADOW")]
        [TestCase("face_shade")]
        [TestCase("髪影")]
        [TestCase("髪の影")]
        [TestCase("前髪影２")]
        [TestCase("影")]
        public void AlphaOverlayNamesAreRecognized(string materialName)
        {
            Assert.That(MmdMaterialTransparencyPolicy.IsLikelyMmdAlphaOverlayMaterial(materialName), Is.True);
            Assert.That(MmdMaterialTransparencyPolicy.IsLikelyMmdSoftAlphaOverlayMaterial(materialName), Is.True);
        }

        [TestCase("cheek")]
        [TestCase("Blush")]
        [TestCase("頰")]
        [TestCase("ほほ")]
        [TestCase("チーク")]
        public void SoftOverlayOnlyNamesAreNotAlphaOverlayNames(string materialName)
        {
            Assert.That(MmdMaterialTransparencyPolicy.IsLikelyMmdAlphaOverlayMaterial(materialName), Is.False);
            Assert.That(MmdMaterialTransparencyPolicy.IsLikelyMmdSoftAlphaOverlayMaterial(materialName), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("body")]
        [TestCase("hair")]
        [TestCase("shad")]
        [TestCase("sh adow")]
        [TestCase("face")]
        [TestCase("髪")]
        public void UnrelatedNamesAreNotOverlayNames(string? materialName)
        {
            Assert.That(MmdMaterialTransparencyPolicy.IsLikelyMmdAlphaOverlayMaterial(materialName), Is.False);
            Assert.That(MmdMaterialTransparencyPolicy.IsLikelyMmdSoftAlphaOverlayMaterial(materialName), Is.False);
        }
    }
}
