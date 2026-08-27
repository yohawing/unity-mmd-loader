#nullable enable

using System;
using Mmd.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mmd.UnityIntegration
{
    internal enum MmdMaterialSurfaceMode
    {
        Opaque,
        AlphaTest,
        AlphaBlend
    }

    internal readonly struct MmdMaterialSurfaceState
    {
        private MmdMaterialSurfaceState(
            MmdMaterialSurfaceMode mode,
            float alphaClipThreshold,
            float shadowAlphaClipThreshold,
            float textureAlphaOutputWeight,
            float textureAlphaClipMask,
            float alphaClipMode,
            float surface,
            float? blend,
            float sourceBlend,
            float destinationBlend,
            float zWrite,
            int renderQueue)
        {
            Mode = mode;
            AlphaClipThreshold = alphaClipThreshold;
            ShadowAlphaClipThreshold = shadowAlphaClipThreshold;
            TextureAlphaOutputWeight = textureAlphaOutputWeight;
            TextureAlphaClipMask = textureAlphaClipMask;
            AlphaClipMode = alphaClipMode;
            Surface = surface;
            Blend = blend;
            SourceBlend = sourceBlend;
            DestinationBlend = destinationBlend;
            ZWrite = zWrite;
            RenderQueue = renderQueue;
        }

        internal MmdMaterialSurfaceMode Mode { get; }

        internal float AlphaClipThreshold { get; }

        internal float ShadowAlphaClipThreshold { get; }

        internal float TextureAlphaOutputWeight { get; }

        internal float TextureAlphaClipMask { get; }

        internal float AlphaClipMode { get; }

        internal float Surface { get; }

        internal float? Blend { get; }

        internal float SourceBlend { get; }

        internal float DestinationBlend { get; }

        internal float ZWrite { get; }

        internal int RenderQueue { get; }

        internal static MmdMaterialSurfaceState Create(
            MmdMaterialSurfaceMode mode,
            int materialRenderOrder,
            float alphaClipThreshold)
        {
            float clampedThreshold = Clamp01(alphaClipThreshold);
            int renderQueue = mode == MmdMaterialSurfaceMode.AlphaBlend
                ? (int)UnityEngine.Rendering.RenderQueue.Transparent + Math.Max(0, materialRenderOrder)
                : (int)UnityEngine.Rendering.RenderQueue.Geometry;

            return mode switch
            {
                MmdMaterialSurfaceMode.AlphaTest => new MmdMaterialSurfaceState(
                    mode,
                    clampedThreshold,
                    clampedThreshold,
                    0.0f,
                    1.0f,
                    2.0f,
                    0.0f,
                    null,
                    (float)BlendMode.One,
                    (float)BlendMode.Zero,
                    1.0f,
                    renderQueue),
                MmdMaterialSurfaceMode.AlphaBlend => new MmdMaterialSurfaceState(
                    mode,
                    0.0f,
                    clampedThreshold,
                    1.0f,
                    1.0f,
                    2.0f,
                    1.0f,
                    0.0f,
                    (float)BlendMode.SrcAlpha,
                    (float)BlendMode.OneMinusSrcAlpha,
                    1.0f,
                    renderQueue),
                _ => new MmdMaterialSurfaceState(
                    MmdMaterialSurfaceMode.Opaque,
                    0.0f,
                    0.0f,
                    0.0f,
                    0.0f,
                    0.0f,
                    0.0f,
                    null,
                    (float)BlendMode.One,
                    (float)BlendMode.Zero,
                    1.0f,
                    renderQueue)
            };
        }

        private static float Clamp01(float value)
        {
            if (!float.IsFinite(value) || value <= 0.0f)
            {
                return 0.0f;
            }

            return value >= 1.0f ? 1.0f : value;
        }
    }

    internal static class MmdMaterialSurfaceApplier
    {
        private const string SurfaceTypeTransparentKeyword = "_SURFACE_TYPE_TRANSPARENT";
        private const string AlphaBlendKeyword = "_ALPHABLEND_ON";
        private const string AlphaTestKeyword = "_ALPHATEST_ON";

        internal static void Apply(
            Material material,
            MmdMaterialSurfaceState state,
            MmdMaterialRenderingTargets targets)
        {
            SetFloatIfPresent(material, targets.AlphaClipThresholdProperty, state.AlphaClipThreshold);
            SetFloatIfPresent(material, targets.ShadowAlphaClipThresholdProperty, state.ShadowAlphaClipThreshold);
            SetFloatIfPresent(material, targets.TextureAlphaOutputWeightProperty, state.TextureAlphaOutputWeight);
            SetFloatIfPresent(material, targets.TextureAlphaClipMaskProperty, state.TextureAlphaClipMask);
            SetFloatIfPresent(material, targets.AlphaClipModeProperty, state.AlphaClipMode);
            SetFloatIfPresent(material, targets.SurfaceProperty, state.Surface);
            if (state.Blend.HasValue)
            {
                SetFloatIfPresent(material, targets.BlendProperty, state.Blend.Value);
            }

            SetFloatIfPresent(material, targets.SourceBlendProperty, state.SourceBlend);
            SetFloatIfPresent(material, targets.DestinationBlendProperty, state.DestinationBlend);
            SetFloatIfPresent(material, targets.ZWriteProperty, state.ZWrite);

            material.DisableKeyword(SurfaceTypeTransparentKeyword);
            material.DisableKeyword(AlphaBlendKeyword);
            material.DisableKeyword(AlphaTestKeyword);
            if (state.Mode == MmdMaterialSurfaceMode.AlphaBlend)
            {
                material.EnableKeyword(SurfaceTypeTransparentKeyword);
                material.EnableKeyword(AlphaBlendKeyword);
            }
            else if (state.Mode == MmdMaterialSurfaceMode.AlphaTest)
            {
                material.EnableKeyword(AlphaTestKeyword);
            }

            if (targets.SupportsRenderQueue)
            {
                material.renderQueue = state.RenderQueue;
            }
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (!string.IsNullOrEmpty(propertyName) && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }
}
