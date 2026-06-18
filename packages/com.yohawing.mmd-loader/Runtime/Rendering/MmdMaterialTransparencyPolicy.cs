#nullable enable

using System;
using System.Collections.Generic;

namespace Mmd.Rendering
{
    /// <summary>
    /// Pure runtime/rendering policy for classifying PMX material transparency
    /// from material alpha, material name, texture extension, and texture alpha
    /// scan data. This type does not reference UnityEngine.
    /// </summary>
    public static class MmdMaterialTransparencyPolicy
    {
        // Real MMD (and the faithful saba reference) has no "opaque-enough" cutoff: it always
        // multiplies the diffuse texture alpha into the fragment and alpha-blends. So a used-UV
        // region is treated as opaque (and kept in the Geometry queue as an optimization) only
        // when every sampled texel is fully opaque (255). A texture carrying a genuine soft alpha
        // mask — e.g. Sour_Miku_Black's hair.png, whose strand edges fall to ~213/255 — must
        // alpha-blend like real MMD instead of being snapped to solid. (The earlier 195 threshold
        // absorbed the 195-254 soft band and rendered such hair fully opaque, diverging from the
        // GoldenOracle. The UV-coverage scan still ignores transparent atlas padding outside the
        // used UVs, so a normal opaque texture with transparent margins stays opaque.)
        public const int TextureOpaqueAlphaThreshold = 255;
        public const int TextureAlphaBlendThreshold = 100;
        public const float PartialAlphaRatioThreshold = 0.25f;

        public const string ModeOpaque = "opaque";
        public const string ModeAlphaTest = "alphaTest";
        public const string ModeAlphaBlend = "alphaBlend";

        /// <summary>Diagnostic summary produced by <see cref="ClassifyTextureAlpha"/>.</summary>
        public sealed class TextureAlphaScanResult
        {
            public int pixelCount;
            public int minAlpha;
            public int maxAlpha;
            public int middleAlphaCount;
            public float middleAlphaRatio;
            public float averageMiddleAlpha;
            public string textureAlphaMode = ModeOpaque;
            public string textureAlphaClassificationReason = string.Empty;
        }

        /// <summary>
        /// Classify a list of per-pixel alpha values (0-255) into
        /// <c>opaque</c>, <c>alphaTest</c>, or <c>alphaBlend</c>.
        ///
        /// The heuristic follows the established MMD texture-alpha rule:
        /// - If every alpha is &gt;= <see cref="TextureOpaqueAlphaThreshold"/>: opaque.
        /// - If &gt;= <see cref="PartialAlphaRatioThreshold"/> of pixels have
        ///   partial (1-254) alpha: alphaBlend.
        /// - Otherwise, if the average partial alpha plus
        ///   <see cref="TextureAlphaBlendThreshold"/> is still below maxAlpha
        ///   : alphaTest (hard cutout). Otherwise: alphaBlend.
        /// </summary>
        public static TextureAlphaScanResult ClassifyTextureAlpha(IReadOnlyList<int> alphaValues)
        {
            if (alphaValues == null)
            {
                throw new ArgumentNullException(nameof(alphaValues));
            }

            if (alphaValues.Count == 0)
            {
                return new TextureAlphaScanResult
                {
                    pixelCount = 0,
                    textureAlphaMode = ModeOpaque,
                    textureAlphaClassificationReason = "no-pixels"
                };
            }

            int pixelCount = alphaValues.Count;
            int minAlpha = 255;
            int maxAlpha = 0;
            int middleAlphaTotal = 0;
            int middleAlphaCount = 0;

            for (int i = 0; i < pixelCount; i++)
            {
                int alpha = alphaValues[i];
                minAlpha = Math.Min(minAlpha, alpha);
                maxAlpha = Math.Max(maxAlpha, alpha);
                if (alpha > 0 && alpha < 255)
                {
                    middleAlphaTotal += alpha;
                    middleAlphaCount++;
                }
            }

            float middleAlphaRatio = (float)middleAlphaCount / pixelCount;

            if (minAlpha >= TextureOpaqueAlphaThreshold)
            {
                return new TextureAlphaScanResult
                {
                    pixelCount = pixelCount,
                    minAlpha = minAlpha,
                    maxAlpha = maxAlpha,
                    middleAlphaCount = middleAlphaCount,
                    middleAlphaRatio = middleAlphaRatio,
                    averageMiddleAlpha = 0f,
                    textureAlphaMode = ModeOpaque,
                    textureAlphaClassificationReason = "all-pixels-above-opaque-threshold"
                };
            }

            float averageMiddleAlpha = middleAlphaCount > 0
                ? (float)middleAlphaTotal / middleAlphaCount
                : 0f;

            if (middleAlphaRatio >= PartialAlphaRatioThreshold)
            {
                return new TextureAlphaScanResult
                {
                    pixelCount = pixelCount,
                    minAlpha = minAlpha,
                    maxAlpha = maxAlpha,
                    middleAlphaCount = middleAlphaCount,
                    middleAlphaRatio = middleAlphaRatio,
                    averageMiddleAlpha = averageMiddleAlpha,
                    textureAlphaMode = ModeAlphaBlend,
                    textureAlphaClassificationReason = "dense-partial-alpha"
                };
            }

            if (averageMiddleAlpha + TextureAlphaBlendThreshold < maxAlpha)
            {
                return new TextureAlphaScanResult
                {
                    pixelCount = pixelCount,
                    minAlpha = minAlpha,
                    maxAlpha = maxAlpha,
                    middleAlphaCount = middleAlphaCount,
                    middleAlphaRatio = middleAlphaRatio,
                    averageMiddleAlpha = averageMiddleAlpha,
                    textureAlphaMode = ModeAlphaTest,
                    textureAlphaClassificationReason = "hard-cutout-sparse-partial-alpha"
                };
            }

            return new TextureAlphaScanResult
            {
                pixelCount = pixelCount,
                minAlpha = minAlpha,
                maxAlpha = maxAlpha,
                middleAlphaCount = middleAlphaCount,
                middleAlphaRatio = middleAlphaRatio,
                averageMiddleAlpha = averageMiddleAlpha,
                textureAlphaMode = ModeAlphaBlend,
                textureAlphaClassificationReason = "fallback-alpha-blend"
            };
        }

        /// <summary>
        /// Resolve the final material transparency mode from:
        /// - PMX material alpha
        /// - material name (for the soft-overlay alphaTest-&gt;alphaBlend upgrade)
        /// - texture extension (accepted for signature compatibility; no longer affects
        ///   classification now that texture alpha drives every extension)
        /// - optional texture alpha scan values (may be null when no texture is present)
        ///
        /// Returns one of <see cref="ModeOpaque"/>, <see cref="ModeAlphaTest"/>,
        /// or <see cref="ModeAlphaBlend"/>.
        /// </summary>
        public static string ResolveMaterialTransparencyMode(
            float materialAlpha,
            string materialName,
            string? textureExtension,
            IReadOnlyList<int>? textureAlphaValues)
        {
            // PMX alpha below 1.0 is always alphaBlend.
            if (materialAlpha < 1.0f)
            {
                return ModeAlphaBlend;
            }

            // No texture scan means opaque.
            if (textureAlphaValues == null || textureAlphaValues.Count == 0)
            {
                return ModeOpaque;
            }

            // Texture alpha drives the transparency mode for every extension. Real MMD (and the
            // faithful saba reference) always multiplies the diffuse texture alpha into the fragment
            // and alpha-blends, so a TGA carrying a meaningful alpha mask (e.g. a hair shape that
            // fades to fully transparent) blends just like a PNG would. The earlier
            // "TGA alpha is ignored unless the material name looks like an overlay" heuristic was a
            // previous compatibility shortcut that diverged from the reference renderer: it painted such a TGA as a
            // solid opaque block instead of the alpha-shaped blend the real renderer produces. The
            // alpha scan already keeps fully-opaque textures (min alpha >= opaque threshold) opaque,
            // so a regular opaque TGA stays opaque without the extension special-case.
            TextureAlphaScanResult scan = ClassifyTextureAlpha(textureAlphaValues);

            // alphaTest upgraded to alphaBlend for likely soft overlay materials.
            if (scan.textureAlphaMode == ModeAlphaTest &&
                IsLikelyMmdSoftAlphaOverlayMaterial(materialName))
            {
                return ModeAlphaBlend;
            }

            return scan.textureAlphaMode;
        }

        /// <summary>
        /// Resolve complete rendering-queue / transparency diagnostics for a
        /// single material.  This is the pure-policy counterpart of what
        /// <c>MmdUnityMaterialBuilder.ApplyMaterialRenderingPolicy</c> does at
        /// Unity runtime.
        /// </summary>
        public static MaterialTransparencyDiagnostic ResolveDiagnostic(
            float materialAlpha,
            string materialName,
            string? textureExtension,
            IReadOnlyList<int>? textureAlphaValues,
            int materialRenderOrder)
        {
            string resolvedMode = ResolveMaterialTransparencyMode(
                materialAlpha,
                materialName,
                textureExtension,
                textureAlphaValues);

            int renderQueue;
            int renderQueueOffset;
            string depthWritePolicy = "enabled";

            if (resolvedMode == ModeAlphaBlend)
            {
                // PMX material/submesh order via RenderQueue.Transparent + materialRenderOrder.
                renderQueue = (int)RenderQueueValue.Transparent + materialRenderOrder;
                renderQueueOffset = materialRenderOrder;
            }
            else
            {
                // Opaque or alphaTest stays in the Geometry queue.
                renderQueue = (int)RenderQueueValue.Geometry;
                renderQueueOffset = -1;
            }

            return new MaterialTransparencyDiagnostic
            {
                resolvedTransparencyMode = resolvedMode,
                renderQueue = renderQueue,
                renderQueueOffset = renderQueueOffset,
                depthWritePolicy = depthWritePolicy,
                materialRenderOrder = materialRenderOrder
            };
        }

        public static bool IsLikelyMmdAlphaOverlayMaterial(string? materialName)
        {
            string name = (materialName ?? string.Empty).ToLowerInvariant();
            return ContainsOrdinal(name, "hair shadow") ||
                ContainsOrdinal(name, "hairshadow") ||
                ContainsOrdinal(name, "shadow") ||
                ContainsOrdinal(name, "shade") ||
                ContainsOrdinal(name, "\u9aea\u5f71") ||
                ContainsOrdinal(name, "\u9aea\u306e\u5f71") ||
                ContainsOrdinal(name, "\u5f71");
        }

        public static bool IsLikelyMmdSoftAlphaOverlayMaterial(string? materialName)
        {
            string name = (materialName ?? string.Empty).ToLowerInvariant();
            return IsLikelyMmdAlphaOverlayMaterial(name) ||
                ContainsOrdinal(name, "cheek") ||
                ContainsOrdinal(name, "blush") ||
                ContainsOrdinal(name, "\u9830") ||
                ContainsOrdinal(name, "\u307b\u307b") ||
                ContainsOrdinal(name, "\u30c1\u30fc\u30af");
        }

        private static bool ContainsOrdinal(string value, string needle)
        {
            return value.IndexOf(needle, StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// Diagnostic record for one material's resolved transparency policy,
        /// including queue offset and depth-write policy.
        /// </summary>
        public sealed class MaterialTransparencyDiagnostic
        {
            public string resolvedTransparencyMode = ModeOpaque;
            public int renderQueue;
            public int renderQueueOffset = -1;
            public string depthWritePolicy = "enabled";
            public int materialRenderOrder;
        }

        /// <summary>
        /// Well-known Unity RenderQueue values used by the pure policy
        /// (mirrors UnityEngine.Rendering.RenderQueue constants).
        /// </summary>
        public enum RenderQueueValue
        {
            Geometry = 2000,
            Transparent = 3000
        }
    }
}
