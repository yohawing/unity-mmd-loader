#nullable enable

using System;
using System.Collections.Generic;
using Mmd.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mmd.Samples.UnityToonShader
{
    public enum UnityToonShaderDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public readonly struct UnityToonShaderDiagnostic
    {
        public UnityToonShaderDiagnostic(
            UnityToonShaderDiagnosticSeverity severity,
            string code,
            string message)
        {
            Severity = severity;
            Code = code;
            Message = message;
        }

        public UnityToonShaderDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }

        public override string ToString() => $"[{Severity}] {Code}: {Message}";
    }

    /// <summary>
    /// Optional UTS 0.14.1-preview material converter. It never mutates source materials and only
    /// returns replacements after every slot has converted successfully.
    /// </summary>
    public static class UnityToonShaderAdapter
    {
        public const string ExpectedShaderName = "Toon/Toon";
        public const string ExpectedUtsVersion = "0.14.1-preview";

        private static readonly string[] RequiredProperties =
        {
            "_MainTex", "_BaseMap", "_BaseColor", "_Color",
            "_1st_ShadeColor", "_2nd_ShadeColor",
            "_TransparentEnabled", "_ClippingMode", "_IsBaseMapAlphaAsClippingMask",
            "_Clipping_Level", "_CullMode", "_ZWriteMode", "_ZWrite", "_AutoRenderQueue",
            "_SPRDefaultUnlitColorMask", "_SRPDefaultUnlitColMode",
            "_MatCap", "_MatCap_Sampler", "_MatCapColor", "_Is_BlendAddToMatCap",
            "_OUTLINE", "_Outline_Width", "_Outline_Color"
        };

        private static readonly string[] RequiredPasses =
        {
            "ForwardLit", "Outline", "ShadowCaster", "DepthOnly", "DepthNormals"
        };

        public static bool TryConvertMaterials(
            IReadOnlyList<MmdMaterialDescriptor> descriptors,
            Material[] originalMaterials,
            bool containsMaterialMorphs,
            out Material[] materials,
            ICollection<UnityToonShaderDiagnostic> diagnostics)
        {
            return TryConvertMaterialsWithShader(
                descriptors,
                originalMaterials,
                Shader.Find(ExpectedShaderName),
                containsMaterialMorphs,
                out materials,
                diagnostics);
        }

        /// <summary>
        /// Explicit-shader entry point. Passing null intentionally exercises the fallback path and
        /// does not call Shader.Find.
        /// </summary>
        public static bool TryConvertMaterialsWithShader(
            IReadOnlyList<MmdMaterialDescriptor> descriptors,
            Material[] originalMaterials,
            Shader? utsShader,
            bool containsMaterialMorphs,
            out Material[] materials,
            ICollection<UnityToonShaderDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            materials = originalMaterials ?? Array.Empty<Material>();
            if (descriptors == null)
            {
                Add(diagnostics, UnityToonShaderDiagnosticSeverity.Error, "UTS_INPUT_INVALID",
                    "Material descriptors are null; original MMD Toon materials were retained.");
                return false;
            }

            if (originalMaterials == null || descriptors.Count != originalMaterials.Length)
            {
                Add(diagnostics, UnityToonShaderDiagnosticSeverity.Error, "UTS_SLOT_MISMATCH",
                    "Descriptor/material slot counts differ; original MMD Toon materials were retained.");
                return false;
            }

            if (!TryValidateSchema(utsShader, diagnostics))
            {
                Add(diagnostics, UnityToonShaderDiagnosticSeverity.Warning, "UTS_FALLBACK_MMD_TOON",
                    "UTS is unavailable or incompatible; all original MMD Toon material slots were retained.");
                return false;
            }

            var converted = new Material[originalMaterials.Length];
            try
            {
                for (int i = 0; i < converted.Length; i++)
                {
                    if (descriptors[i] == null || originalMaterials[i] == null)
                    {
                        throw new InvalidOperationException($"Material slot {i} is null.");
                    }

                    converted[i] = ConvertMaterial(descriptors[i], originalMaterials[i], utsShader!);
                }
            }
            catch (Exception exception)
            {
                DestroyMaterials(converted);
                Add(diagnostics, UnityToonShaderDiagnosticSeverity.Error, "UTS_CONVERSION_FAILED",
                    $"Conversion failed before slot replacement ({exception.Message}); original MMD Toon materials were retained.");
                return false;
            }

            AddCompatibilityDiagnostics(descriptors, containsMaterialMorphs, diagnostics);
            foreach (Material originalMaterial in originalMaterials)
            {
                if (GetFloat(originalMaterial, "_AlphaClipThreshold") > 0.5f)
                {
                    Add(diagnostics, UnityToonShaderDiagnosticSeverity.Warning, "UTS_ALPHA_CLIP_CLAMPED",
                        "UTS cannot represent an alpha cutoff above 0.5; the converted cutoff was clamped to 0.5.");
                    break;
                }
            }
            for (int i = 0; i < descriptors.Count; i++)
            {
                if (descriptors[i].drawEdgeFlag && descriptors[i].edgeSize > 0.0f &&
                    (descriptors[i].alpha < 0.999f || originalMaterials[i].renderQueue >= (int)RenderQueue.Transparent))
                {
                    Add(diagnostics, UnityToonShaderDiagnosticSeverity.Warning, "UTS_TRANSPARENT_OUTLINE_DISABLED",
                        "UTS Inspector normalization disables color writes for transparent outlines; the PMX edge is not rendered.");
                    break;
                }
            }
            materials = converted;
            return true;
        }

        public static bool TryValidateSchema(
            Shader? shader,
            ICollection<UnityToonShaderDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            if (shader == null)
            {
                Add(diagnostics, UnityToonShaderDiagnosticSeverity.Error, "UTS_SHADER_ABSENT",
                    $"Shader.Find(\"{ExpectedShaderName}\") returned null.");
                return false;
            }

            if (!string.Equals(shader.name, ExpectedShaderName, StringComparison.Ordinal))
            {
                Add(diagnostics, UnityToonShaderDiagnosticSeverity.Error, "UTS_SHADER_NAME_MISMATCH",
                    $"Expected shader '{ExpectedShaderName}' for UTS {ExpectedUtsVersion}, found '{shader.name}'.");
                return false;
            }

            var probe = new Material(shader);
            try
            {
                foreach (string property in RequiredProperties)
                {
                    if (!probe.HasProperty(property))
                    {
                        Add(diagnostics, UnityToonShaderDiagnosticSeverity.Error, "UTS_SCHEMA_PROPERTY_MISSING",
                            $"UTS {ExpectedUtsVersion} property '{property}' is missing.");
                        return false;
                    }
                }

                foreach (string pass in RequiredPasses)
                {
                    if (probe.FindPass(pass) < 0)
                    {
                        Add(diagnostics, UnityToonShaderDiagnosticSeverity.Error, "UTS_SCHEMA_PASS_MISSING",
                            $"UTS {ExpectedUtsVersion} pass '{pass}' is missing.");
                        return false;
                    }
                }
            }
            finally
            {
                DestroyMaterial(probe);
            }

            Add(diagnostics, UnityToonShaderDiagnosticSeverity.Info, "UTS_SCHEMA_OK",
                $"'{ExpectedShaderName}' matches the required UTS {ExpectedUtsVersion} adapter schema.");
            return true;
        }

        public static void DestroyMaterials(Material[]? materials)
        {
            if (materials == null)
            {
                return;
            }

            foreach (Material material in materials)
            {
                if (material != null)
                {
                    DestroyMaterial(material);
                }
            }
        }

        private static Material ConvertMaterial(
            MmdMaterialDescriptor descriptor,
            Material source,
            Shader utsShader)
        {
            var target = new Material(utsShader)
            {
                name = source.name + " (UTS)",
                hideFlags = source.hideFlags,
                renderQueue = source.renderQueue,
                enableInstancing = source.enableInstancing
            };
            try
            {
                float alpha = Mathf.Clamp01(descriptor.alpha);
                Color diffuse = ToColor(descriptor.diffuseColor, alpha);
                target.SetColor("_BaseColor", diffuse);
                target.SetColor("_Color", diffuse);
                target.SetColor("_1st_ShadeColor", ScaleRgb(diffuse, 0.65f));
                target.SetColor("_2nd_ShadeColor", ScaleRgb(diffuse, 0.35f));

                Texture? baseTexture = GetFirstTexture(source, "_BaseMap", "_MainTex");
                if (baseTexture != null)
                {
                    target.SetTexture("_BaseMap", baseTexture);
                    target.SetTexture("_MainTex", baseTexture);
                }

                bool alphaClip = GetFloat(source, "_AlphaClipThreshold") > 0.0f ||
                    source.IsKeywordEnabled("_ALPHATEST_ON");
                bool transparent = alpha < 0.999f || source.renderQueue >= (int)RenderQueue.Transparent;
                target.SetFloat("_AutoRenderQueue", 0.0f);
                target.SetFloat("_TransparentEnabled", transparent ? 1.0f : 0.0f);
                target.SetFloat("_ClippingMode", transparent ? 2.0f : alphaClip ? 1.0f : 0.0f);
                target.SetFloat("_IsBaseMapAlphaAsClippingMask", alphaClip || transparent ? 1.0f : 0.0f);
                float sourceAlphaThreshold = Mathf.Clamp01(GetFloat(source, "_AlphaClipThreshold"));
                target.SetFloat("_Clipping_Level", transparent
                    ? 0.5f
                    : alphaClip ? Mathf.Clamp01(0.5f - sourceAlphaThreshold) : 0.0f);
                target.SetFloat("_ZWriteMode", source.HasProperty("_ZWrite") ? source.GetFloat("_ZWrite") : 1.0f);
                target.SetFloat("_ZWrite", source.HasProperty("_ZWrite") ? source.GetFloat("_ZWrite") : 1.0f);
                target.SetFloat("_CullMode", string.Equals(descriptor.cullingPolicy, "double-sided", StringComparison.Ordinal) ? 0.0f : 2.0f);
                ApplyClippingKeywords(target, transparent, alphaClip);
                target.SetOverrideTag("RenderType", transparent ? "Transparent" : alphaClip ? "TransparentCutOut" : "Opaque");
                target.SetOverrideTag("IgnoreProjection", transparent ? "True" : "False");

                Texture? sphereTexture = GetFirstTexture(source, "_SphereMap");
                if (sphereTexture != null && !string.IsNullOrEmpty(descriptor.sphereTexture))
                {
                    target.SetFloat("_MatCap", 1.0f);
                    target.EnableKeyword("_MatCap");
                    target.SetTexture("_MatCap_Sampler", sphereTexture);
                    target.SetColor("_MatCapColor", Color.white);
                    target.SetFloat("_Is_BlendAddToMatCap",
                        string.Equals(descriptor.sphereTextureMode, "additive-sphere", StringComparison.Ordinal) ? 1.0f : 0.0f);
                }

                bool hasOutline = descriptor.drawEdgeFlag && descriptor.edgeSize > 0.0f;
                target.SetFloat("_OUTLINE", 0.0f);
                target.EnableKeyword("_OUTLINE_NML");
                target.DisableKeyword("_OUTLINE_POS");
                target.SetShaderPassEnabled("SRPDefaultUnlit", hasOutline);
                // Match UTS3GUI normalization so opening the material inspector does not change outline state.
                target.SetFloat("_SPRDefaultUnlitColorMask", transparent ? 0.0f : 15.0f);
                target.SetFloat("_SRPDefaultUnlitColMode", 1.0f);
                target.SetFloat("_Outline_Width", hasOutline ? Mathf.Clamp(descriptor.edgeSize * 0.1f, 0.0f, 10.0f) : 0.0f);
                target.SetColor("_Outline_Color", ToColor(descriptor.edgeColor, descriptor.edgeColor != null && descriptor.edgeColor.Length > 3
                    ? descriptor.edgeColor[3]
                    : 1.0f));
                return target;
            }
            catch
            {
                DestroyMaterial(target);
                throw;
            }
        }

        private static void AddCompatibilityDiagnostics(
            IReadOnlyList<MmdMaterialDescriptor> descriptors,
            bool containsMaterialMorphs,
            ICollection<UnityToonShaderDiagnostic> diagnostics)
        {
            Add(diagnostics, UnityToonShaderDiagnosticSeverity.Warning, "UTS_TOON_RAMP_APPROXIMATION",
                "MMD toon ramps are incompatible with UTS shade layers; diffuse-derived shade colors were used.");
            Add(diagnostics, UnityToonShaderDiagnosticSeverity.Warning, "UTS_SELF_SHADOW_INCOMPATIBLE",
                "MMD dedicated self-shadow is not transferred; UTS/URP shadow behavior applies.");

            if (containsMaterialMorphs)
            {
                Add(diagnostics, UnityToonShaderDiagnosticSeverity.Warning, "UTS_MATERIAL_MORPH_INCOMPATIBLE",
                    "MMD material morph writes target MMD shader properties and will not animate these UTS materials.");
            }

            bool hasMultiplySphere = false;
            bool hasOutline = false;
            foreach (MmdMaterialDescriptor descriptor in descriptors)
            {
                hasMultiplySphere |= string.Equals(descriptor.sphereTextureMode, "multiply-sphere", StringComparison.Ordinal);
                hasOutline |= descriptor.drawEdgeFlag && descriptor.edgeSize > 0.0f;
            }

            if (hasMultiplySphere)
            {
                Add(diagnostics, UnityToonShaderDiagnosticSeverity.Warning, "UTS_SPH_MATCAP_APPROXIMATION",
                    "MMD multiply sphere (.sph) has no exact UTS MatCap equation and was approximated.");
            }

            if (hasOutline)
            {
                Add(diagnostics, UnityToonShaderDiagnosticSeverity.Warning, "UTS_OUTLINE_WIDTH_APPROXIMATION",
                    "PMX screen-pixel edge width was scaled to UTS outline width; the spaces are not equivalent.");
            }
        }

        private static void ApplyClippingKeywords(Material material, bool transparent, bool alphaClip)
        {
            material.DisableKeyword("_IS_CLIPPING_OFF");
            material.DisableKeyword("_IS_CLIPPING_MODE");
            material.DisableKeyword("_IS_CLIPPING_TRANSMODE");
            material.DisableKeyword("_IS_TRANSCLIPPING_OFF");
            material.DisableKeyword("_IS_TRANSCLIPPING_ON");
            material.DisableKeyword("_IS_OUTLINE_CLIPPING_NO");
            material.DisableKeyword("_IS_OUTLINE_CLIPPING_YES");

            if (transparent)
            {
                material.EnableKeyword("_IS_CLIPPING_TRANSMODE");
                material.EnableKeyword("_IS_OUTLINE_CLIPPING_YES");
            }
            else if (alphaClip)
            {
                material.EnableKeyword("_IS_CLIPPING_MODE");
                material.EnableKeyword("_IS_OUTLINE_CLIPPING_YES");
            }
            else
            {
                material.EnableKeyword("_IS_CLIPPING_OFF");
                material.EnableKeyword("_IS_OUTLINE_CLIPPING_NO");
            }
        }

        private static Texture? GetFirstTexture(Material source, params string[] names)
        {
            foreach (string name in names)
            {
                if (source.HasProperty(name))
                {
                    Texture texture = source.GetTexture(name);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }

            return null;
        }

        private static float GetFloat(Material material, string property)
        {
            return material.HasProperty(property) ? material.GetFloat(property) : 0.0f;
        }

        private static Color ToColor(float[]? values, float alpha)
        {
            return new Color(
                values != null && values.Length > 0 ? values[0] : 1.0f,
                values != null && values.Length > 1 ? values[1] : 1.0f,
                values != null && values.Length > 2 ? values[2] : 1.0f,
                Mathf.Clamp01(alpha));
        }

        private static Color ScaleRgb(Color color, float scale)
        {
            return new Color(color.r * scale, color.g * scale, color.b * scale, color.a);
        }

        private static void Add(
            ICollection<UnityToonShaderDiagnostic> diagnostics,
            UnityToonShaderDiagnosticSeverity severity,
            string code,
            string message)
        {
            diagnostics.Add(new UnityToonShaderDiagnostic(severity, code, message));
        }

        private static void DestroyMaterial(Material material)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(material);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}
