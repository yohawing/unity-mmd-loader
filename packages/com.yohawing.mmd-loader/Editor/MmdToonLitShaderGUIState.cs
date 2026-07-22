#nullable enable

using System;
using UnityEditor;
using UnityEngine;

namespace Mmd.Editor
{
    internal enum MmdToonLitInspectorSection
    {
        SurfaceRendering,
        SurfaceMaps,
        ToonLighting,
        ShadowMmdSelfShadow,
        Normal,
        SphereMatCap,
        StylizedSpecular,
        RimLight,
        Emission,
        Outline,
        AdvancedDiagnostics,
    }

    internal enum MmdToonLitSectionState
    {
        Off,
        On,
        Mixed,
    }

    internal sealed class MmdToonLitSectionDefinition
    {
        public MmdToonLitSectionDefinition(
            MmdToonLitInspectorSection section,
            string displayName,
            bool expandedByDefault)
        {
            Section = section;
            DisplayName = displayName;
            ExpandedByDefault = expandedByDefault;
        }

        public MmdToonLitInspectorSection Section { get; }
        public string DisplayName { get; }
        public bool ExpandedByDefault { get; }
    }

    internal static class MmdToonLitShaderGUIState
    {
        internal const string ShaderName = "MMD Toon Lit";

        internal static readonly MmdToonLitSectionDefinition[] Sections =
        {
            new(MmdToonLitInspectorSection.SurfaceRendering, "Surface Rendering", true),
            new(MmdToonLitInspectorSection.SurfaceMaps, "Surface Maps", false),
            new(MmdToonLitInspectorSection.ToonLighting, "Toon Lighting", true),
            new(MmdToonLitInspectorSection.ShadowMmdSelfShadow, "Shadow / MMD Self Shadow", false),
            new(MmdToonLitInspectorSection.Normal, "Normal", false),
            new(MmdToonLitInspectorSection.SphereMatCap, "Sphere / MatCap", false),
            new(MmdToonLitInspectorSection.StylizedSpecular, "Stylized Specular", false),
            new(MmdToonLitInspectorSection.RimLight, "Rim Light", false),
            new(MmdToonLitInspectorSection.Emission, "Emission", false),
            new(MmdToonLitInspectorSection.Outline, "Outline", false),
            new(MmdToonLitInspectorSection.AdvancedDiagnostics, "Advanced / Diagnostics", false),
        };

        internal static readonly string[] RequiredProperties =
        {
            "_BaseMap",
            "_BaseColor",
            "_ToonStrength",
            "_ToonBoundary",
            "_ToonBandCount",
            "_StylizedSpecularBoundary",
            "_RimBoundary",
            "_MmdEmissionIntensity",
            "_OutlineWidth",
        };

        internal static bool[] CreateDefaultExpandedState()
        {
            bool[] expanded = new bool[Sections.Length];
            for (int i = 0; i < Sections.Length; i++)
            {
                expanded[i] = Sections[i].ExpandedByDefault;
            }

            return expanded;
        }

        internal static bool ShouldDrawFeatureDetails(MmdToonLitSectionState state)
        {
            // Mixed selections keep the controls visible because at least one target is enabled;
            // Unity's mixed-value rendering communicates which targets differ.
            return state != MmdToonLitSectionState.Off;
        }

        internal static bool IsMmdToonLitShader(Shader? shader)
        {
            return shader != null && string.Equals(shader.name, ShaderName, StringComparison.Ordinal);
        }

        internal static bool HasRequiredProperties(MaterialProperty[] properties, out string missing)
        {
            for (int i = 0; i < RequiredProperties.Length; i++)
            {
                string required = RequiredProperties[i];
                bool found = false;
                for (int j = 0; j < properties.Length; j++)
                {
                    if (string.Equals(properties[j].name, required, StringComparison.Ordinal))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    missing = required;
                    return false;
                }
            }

            missing = string.Empty;
            return true;
        }
    }
}
