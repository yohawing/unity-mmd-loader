#nullable enable

using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdMaterialSurfaceStateTests
    {
        [TestCase(0, 0, 0.35f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 2000)]
        [TestCase(1, 0, 0.35f, 0.35f, 0.35f, 0.0f, 1.0f, 2.0f, 0.0f, 1.0f, 0.0f, 2000)]
        [TestCase(2, 4, 0.35f, 0.0f, 0.35f, 1.0f, 1.0f, 2.0f, 1.0f, 1.0f, 0.0f, 3004)]
        public void CreateProducesTheCompleteSurfaceTransition(
            int modeValue,
            int materialRenderOrder,
            float alphaClipThreshold,
            float expectedAlphaClipThreshold,
            float expectedShadowAlphaClipThreshold,
            float expectedTextureAlphaOutputWeight,
            float expectedTextureAlphaClipMask,
            float expectedAlphaClipMode,
            float expectedSurface,
            float expectedZWrite,
            float expectedBlend,
            int expectedRenderQueue)
        {
            MmdMaterialSurfaceMode mode = (MmdMaterialSurfaceMode)modeValue;
            MmdMaterialSurfaceState state = MmdMaterialSurfaceState.Create(
                mode,
                materialRenderOrder,
                alphaClipThreshold);

            Assert.That(state.Mode, Is.EqualTo(mode));
            Assert.That(state.AlphaClipThreshold, Is.EqualTo(expectedAlphaClipThreshold).Within(0.00001f));
            Assert.That(state.ShadowAlphaClipThreshold, Is.EqualTo(expectedShadowAlphaClipThreshold).Within(0.00001f));
            Assert.That(state.TextureAlphaOutputWeight, Is.EqualTo(expectedTextureAlphaOutputWeight).Within(0.00001f));
            Assert.That(state.TextureAlphaClipMask, Is.EqualTo(expectedTextureAlphaClipMask).Within(0.00001f));
            Assert.That(state.AlphaClipMode, Is.EqualTo(expectedAlphaClipMode).Within(0.00001f));
            Assert.That(state.Surface, Is.EqualTo(expectedSurface).Within(0.00001f));
            Assert.That(state.ZWrite, Is.EqualTo(expectedZWrite).Within(0.00001f));
            Assert.That(state.RenderQueue, Is.EqualTo(expectedRenderQueue));
            if (mode == MmdMaterialSurfaceMode.AlphaBlend)
            {
                Assert.That(state.Blend, Is.EqualTo(expectedBlend).Within(0.00001f));
                Assert.That(state.SourceBlend, Is.EqualTo((float)BlendMode.SrcAlpha).Within(0.00001f));
                Assert.That(state.DestinationBlend, Is.EqualTo((float)BlendMode.OneMinusSrcAlpha).Within(0.00001f));
            }
            else
            {
                Assert.That(state.Blend, Is.Null);
                Assert.That(state.SourceBlend, Is.EqualTo((float)BlendMode.One).Within(0.00001f));
                Assert.That(state.DestinationBlend, Is.EqualTo((float)BlendMode.Zero).Within(0.00001f));
            }
        }

        [Test]
        public void ApplierClearsStaleTransparentStateWhenReturningToOpaque()
        {
            Shader shader = Shader.Find("MMD Toon Lit")
                ?? throw new AssertionException("MMD Toon Lit shader was not found.");
            Material material = new Material(shader);
            try
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.EnableKeyword("_ALPHATEST_ON");

                MmdMaterialSurfaceApplier.Apply(
                    material,
                    MmdMaterialSurfaceState.Create(MmdMaterialSurfaceMode.Opaque, 0, 0.35f),
                    MmdMaterialRenderingTargets.BuiltIn);

                Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.False);
                Assert.That(material.IsKeywordEnabled("_ALPHABLEND_ON"), Is.False);
                Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.False);
                Assert.That(material.GetFloat("_SrcBlend"), Is.EqualTo((float)BlendMode.One).Within(0.00001f));
                Assert.That(material.GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.Zero).Within(0.00001f));
                Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Geometry));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }
    }
}
