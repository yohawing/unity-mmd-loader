#nullable enable

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

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

    internal enum MmdToonSurfaceType
    {
        Opaque,
        Cutout,
        Transparent,
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

    internal static class MmdToonMaterialStateSync
    {
        internal const string AlphaClipThresholdBackup = "_MmdAlphaClipThresholdBackup";
        internal const string ShadowAlphaClipThresholdBackup = "_MmdShadowAlphaClipThresholdBackup";
        private const float DefaultAlphaClipThreshold = 0.01f;

        internal static MmdToonSurfaceType GetSurfaceType(Material material)
        {
            if (material.GetFloat("_DstBlend") != (float)BlendMode.Zero ||
                material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
            {
                return MmdToonSurfaceType.Transparent;
            }

            return material.GetFloat("_AlphaClipThreshold") > 0.0f
                ? MmdToonSurfaceType.Cutout
                : MmdToonSurfaceType.Opaque;
        }

        internal static MmdToonSurfaceType GetSurfaceType(Material[] materials, out bool mixed)
        {
            MmdToonSurfaceType result = GetSurfaceType(materials[0]);
            mixed = false;
            for (int i = 1; i < materials.Length; i++)
            {
                if (GetSurfaceType(materials[i]) != result)
                {
                    mixed = true;
                    break;
                }
            }

            return result;
        }

        internal static void ApplySurfaceType(Material[] materials, MmdToonSurfaceType selected)
        {
            Undo.RecordObjects(materials, "Change MMD Toon Surface Type");
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                MmdToonSurfaceType previous = GetSurfaceType(material);
                SavePositiveThreshold(material, "_AlphaClipThreshold", AlphaClipThresholdBackup);
                SavePositiveThreshold(material, "_ShadowAlphaClipThreshold", ShadowAlphaClipThresholdBackup);

                bool transparent = selected == MmdToonSurfaceType.Transparent;
                material.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
                material.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1.0f);
                SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
                SetKeyword(material, "_ALPHABLEND_ON", transparent);
                SetKeyword(material, "_ALPHATEST_ON", selected == MmdToonSurfaceType.Cutout);

                switch (selected)
                {
                    case MmdToonSurfaceType.Cutout:
                        material.SetFloat("_AlphaClipThreshold", RestoreThreshold(material, AlphaClipThresholdBackup));
                        material.SetFloat("_ShadowAlphaClipThreshold", RestoreThreshold(material, ShadowAlphaClipThresholdBackup));
                        break;
                    case MmdToonSurfaceType.Transparent:
                        material.SetFloat("_AlphaClipThreshold", 0.0f);
                        material.SetFloat("_ShadowAlphaClipThreshold", RestoreThreshold(material, ShadowAlphaClipThresholdBackup));
                        break;
                    default:
                        material.SetFloat("_AlphaClipThreshold", 0.0f);
                        material.SetFloat("_ShadowAlphaClipThreshold", 0.0f);
                        break;
                }

                int previousDefaultQueue = GetDefaultRenderQueue(previous);
                if (material.renderQueue == previousDefaultQueue)
                {
                    material.renderQueue = GetDefaultRenderQueue(selected);
                }

                EditorUtility.SetDirty(material);
            }
        }

        private static int GetDefaultRenderQueue(MmdToonSurfaceType surfaceType)
        {
            return surfaceType switch
            {
                MmdToonSurfaceType.Transparent => (int)RenderQueue.Transparent,
                _ => (int)RenderQueue.Geometry,
            };
        }

        private static void SavePositiveThreshold(Material material, string propertyName, string backupPropertyName)
        {
            float value = material.GetFloat(propertyName);
            if (value > 0.0f && material.HasProperty(backupPropertyName))
            {
                material.SetFloat(backupPropertyName, value);
            }
        }

        private static float RestoreThreshold(Material material, string backupPropertyName)
        {
            if (!material.HasProperty(backupPropertyName))
            {
                return DefaultAlphaClipThreshold;
            }

            float value = material.GetFloat(backupPropertyName);
            return value > 0.0f ? value : DefaultAlphaClipThreshold;
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }
    }
}
