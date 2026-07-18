#nullable enable

using System;
using UnityEngine;
using Mmd.Rendering;

namespace Mmd.UnityIntegration
{
    internal static partial class MmdUnityMaterialBuilder
    {
        private static void ApplyUrpLitDefaults(Material[] materials)
        {
            foreach (Material material in materials)
            {
                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", 0.0f);
                }

                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", 0.5f);
                }

                if (material.HasProperty("_SpecularHighlights"))
                {
                    material.SetFloat("_SpecularHighlights", 1.0f);
                }

                if (material.HasProperty("_EnvironmentReflections"))
                {
                    material.SetFloat("_EnvironmentReflections", 1.0f);
                }

                if (material.HasProperty("_Cutoff"))
                {
                    material.SetFloat("_Cutoff", AlphaClipThreshold);
                }
            }
        }

        private static void ApplyMaterialColors(Material material, MmdMaterialDescriptor source)
        {
            Color diffuse = ToColor(source.diffuseColor, source.alpha, Color.white);
            Color ambient = ToColor(source.ambientColor, 1.0f, new Color(0.25f, 0.25f, 0.25f, 1.0f));
            float edgeAlpha = source.edgeColor != null && source.edgeColor.Length > 3 ? source.edgeColor[3] : 1.0f;
            Color edge = ToColor(source.edgeColor, edgeAlpha, Color.black);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", diffuse);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            if (material.HasProperty("_AmbientColor"))
            {
                material.SetColor("_AmbientColor", ambient);
            }

            if (material.HasProperty(MmdMaterialPropertyNames.ToonBoundary))
            {
                material.SetFloat(MmdMaterialPropertyNames.ToonBoundary, source.toonBoundary);
            }

            if (material.HasProperty(MmdMaterialPropertyNames.ToonFeather))
            {
                material.SetFloat(MmdMaterialPropertyNames.ToonFeather, source.toonFeather);
            }

            if (material.HasProperty(MmdMaterialPropertyNames.ToonBandCount))
            {
                material.SetFloat(MmdMaterialPropertyNames.ToonBandCount, source.toonBandCount);
            }

            if (material.HasProperty("_OutlineColor"))
            {
                material.SetColor("_OutlineColor", edge);
            }

            if (material.HasProperty("_OutlineWidth"))
            {
                material.SetFloat("_OutlineWidth", source.drawEdgeFlag ? source.edgeSize : 0.0f);
            }

            if (material.HasProperty("_OutlineVisible"))
            {
                material.SetFloat("_OutlineVisible", source.drawEdgeFlag ? 1.0f : 0.0f);
            }

            if (material.HasProperty("_OutlineScreenSpaceWeight"))
            {
                material.SetFloat("_OutlineScreenSpaceWeight", PmxOutlineScreenSpaceWeight);
            }

            if (material.HasProperty("_OutlineZTest"))
            {
                material.SetFloat("_OutlineZTest", OutlineZTest);
            }
        }

        private static Color ToColor(float[]? values, float alpha, Color fallback)
        {
            if (values == null)
            {
                return fallback;
            }

            float r = values.Length > 0 && IsFinite(values[0]) ? Mathf.Clamp01(values[0]) : fallback.r;
            float g = values.Length > 1 && IsFinite(values[1]) ? Mathf.Clamp01(values[1]) : fallback.g;
            float b = values.Length > 2 && IsFinite(values[2]) ? Mathf.Clamp01(values[2]) : fallback.b;
            float a = IsFinite(alpha) ? Mathf.Clamp01(alpha) : fallback.a;
            return new Color(r, g, b, a);
        }

        private static void SetColorAlpha(Material material, string propertyName, float alpha)
        {
            if (!material.HasProperty(propertyName))
            {
                return;
            }

            Color color = material.GetColor(propertyName);
            color.a = alpha;
            material.SetColor(propertyName, color);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
