#nullable enable

using System;
using UnityEditor;
using UnityEngine;

namespace Mmd.Editor
{
    internal enum MmdToonLitInspectorSection
    {
        SurfaceOptions,
        SurfaceInputs,
        DetailInputs,
        AdvancedOptions,
    }

    internal enum MmdToonInspectorProfile
    {
        BasicToon,
        MmdToon,
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
        internal const string MmdToonShaderName = "MMD URP Toon";
        internal const string BasicToonShaderName = "MMD Basic Toon";
        internal const string LegacyMmdToonShaderName = "MMD Toon Lit";
        internal const string LegacyBasicToonShaderName = "MMD Basic URP Toon";
        internal const string MmdToonDisplayName = "MMD URP Toon";
        internal const string BasicToonDisplayName = "MMD Basic Toon";

        internal static readonly MmdToonLitSectionDefinition[] Sections =
        {
            new(MmdToonLitInspectorSection.SurfaceOptions, "Surface Options", true),
            new(MmdToonLitInspectorSection.SurfaceInputs, "Surface Inputs", true),
            new(MmdToonLitInspectorSection.DetailInputs, "Detail Inputs", false),
            new(MmdToonLitInspectorSection.AdvancedOptions, "Advanced Options", false),
        };

        internal static readonly string[] RequiredProperties =
        {
            "_BaseMap",
            "_BaseColor",
            "_ToonStrength",
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

        internal static bool TryGetProfile(Shader? shader, out MmdToonInspectorProfile profile)
        {
            if (shader != null &&
                (string.Equals(shader.name, MmdToonShaderName, StringComparison.Ordinal) ||
                 string.Equals(shader.name, LegacyMmdToonShaderName, StringComparison.Ordinal)))
            {
                profile = MmdToonInspectorProfile.MmdToon;
                return true;
            }

            if (shader != null &&
                (string.Equals(shader.name, BasicToonShaderName, StringComparison.Ordinal) ||
                 string.Equals(shader.name, LegacyBasicToonShaderName, StringComparison.Ordinal)))
            {
                profile = MmdToonInspectorProfile.BasicToon;
                return true;
            }

            profile = default;
            return false;
        }

        internal static string GetDisplayName(MmdToonInspectorProfile profile)
        {
            return profile == MmdToonInspectorProfile.MmdToon
                ? MmdToonDisplayName
                : BasicToonDisplayName;
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
