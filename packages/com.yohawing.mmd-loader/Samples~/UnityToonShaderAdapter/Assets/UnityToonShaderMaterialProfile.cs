#nullable enable

using System;
using System.Collections.Generic;
using Mmd;
using UnityEngine;

namespace Mmd.Samples.UnityToonShader
{
    /// <summary>
    /// Configures a loader material profile for Unity Toon Shader 0.14.1-preview.
    /// The sample remains optional: no UTS assembly or shader type is referenced at compile time.
    /// </summary>
    public static class UnityToonShaderMaterialProfile
    {
        public static bool TryConfigure(
            MmdMaterialProfileAsset profile,
            Shader? utsShader,
            ICollection<UnityToonShaderDiagnostic> diagnostics)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            if (!UnityToonShaderAdapter.TryValidateSchema(utsShader, diagnostics))
            {
                Add(diagnostics, UnityToonShaderDiagnosticSeverity.Warning, "UTS_PROFILE_FALLBACK_MMD_TOON",
                    "UTS profile was not configured; assign a valid MMD Toon profile instead.");
                return false;
            }

            profile.schemaVersion = MmdMaterialProfileAsset.CurrentSchemaVersion;
            profile.shader = utsShader;
            profile.textureTargets = new MmdMaterialProfileTextureTargets
            {
                diffuseTextureProperties = new[] { "_BaseMap", "_MainTex" },
                sphereTextureProperty = "_MatCap_Sampler",
                sphereTextureBoundProperty = "_MatCap",
                toonTextureProperty = string.Empty,
                diffuseTextureBoundProperty = string.Empty,
                sphereModeProperty = "_Is_BlendAddToMatCap",
                toonTextureBoundProperty = string.Empty
            };
            profile.renderingTargets = new MmdMaterialProfileRenderingTargets
            {
                baseColorProperty = "_BaseColor",
                colorProperty = "_Color",
                ambientColorProperty = string.Empty,
                alphaProperty = string.Empty,
                alphaClipThresholdProperty = "_Clipping_Level",
                shadowAlphaClipThresholdProperty = string.Empty,
                textureAlphaOutputWeightProperty = string.Empty,
                textureAlphaClipMaskProperty = "_IsBaseMapAlphaAsClippingMask",
                alphaClipModeProperty = "_ClippingMode",
                cullProperty = "_CullMode",
                surfaceProperty = string.Empty,
                blendProperty = string.Empty,
                sourceBlendProperty = string.Empty,
                destinationBlendProperty = string.Empty,
                zWriteProperty = "_ZWrite",
                outlineColorProperty = "_Outline_Color",
                outlineWidthProperty = "_Outline_Width",
                outlineVisibleProperty = "_OUTLINE",
                outlineScreenSpaceWeightProperty = string.Empty,
                outlineZTestProperty = string.Empty,
                supportsRenderQueue = true,
                unsupportedFeatures = new[]
                {
                    "ambient-color",
                    "toon-texture",
                    "self-shadow",
                    "material-morph"
                },
                validatePropertyPresence = true,
                requiredKeywords = new[]
                {
                    "_OUTLINE_NML",
                    "_MatCap",
                    "_IS_CLIPPING_OFF",
                    "_IS_CLIPPING_TRANSMODE",
                    "_IS_TRANSCLIPPING_ON",
                    "_IS_OUTLINE_CLIPPING_NO"
                },
                requiredPasses = new[]
                {
                    "ForwardLit",
                    "ShadowCaster",
                    "DepthOnly",
                    "DepthNormals",
                    "SRPDefaultUnlit"
                },
                supportsMaterialMorphs = false
            };

            Add(diagnostics, UnityToonShaderDiagnosticSeverity.Info, "UTS_PROFILE_CONFIGURED",
                $"MMD Material Profile configured for '{utsShader!.name}' ({UnityToonShaderAdapter.ExpectedUtsVersion}).");
            return true;
        }

        private static void Add(
            ICollection<UnityToonShaderDiagnostic> diagnostics,
            UnityToonShaderDiagnosticSeverity severity,
            string code,
            string message)
        {
            diagnostics.Add(new UnityToonShaderDiagnostic(severity, code, message));
        }
    }
}
