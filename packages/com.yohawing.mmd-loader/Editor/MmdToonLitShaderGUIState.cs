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
        ToonMap,
        NormalMap,
        SphereMatCap,
        ToonLighting,
        StylizedSpecular,
        RimLight,
        Emission,
        Outline,
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

    internal enum MmdToonFeature
    {
        ToonMap,
        NormalMap,
        SphereMatCap,
        StylizedSpecular,
        RimLight,
        Emission,
        Outline,
    }

    internal enum MmdToonFeatureState
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
            new(MmdToonLitInspectorSection.ToonMap, "Toon Map", false),
            new(MmdToonLitInspectorSection.NormalMap, "Normal Map", false),
            new(MmdToonLitInspectorSection.SphereMatCap, "Sphere / MatCap", false),
            new(MmdToonLitInspectorSection.ToonLighting, "Toon Lighting", false),
            new(MmdToonLitInspectorSection.StylizedSpecular, "Stylized Specular", false),
            new(MmdToonLitInspectorSection.RimLight, "Rim Light", false),
            new(MmdToonLitInspectorSection.Emission, "Emission", false),
            new(MmdToonLitInspectorSection.Outline, "Outline", false),
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

        internal static bool SupportsSection(
            MmdToonInspectorProfile profile,
            MmdToonLitInspectorSection section)
        {
            if (profile == MmdToonInspectorProfile.MmdToon)
            {
                return true;
            }

            return section != MmdToonLitInspectorSection.StylizedSpecular &&
                section != MmdToonLitInspectorSection.RimLight &&
                section != MmdToonLitInspectorSection.Emission;
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
        internal const string SphereModeBackup = "_MmdSphereModeBackup";
        internal const string StylizedSpecularBoundaryBackup = "_MmdStylizedSpecularBoundaryBackup";
        internal const string RimBoundaryBackup = "_MmdRimBoundaryBackup";
        internal const string EmissionIntensityBackup = "_MmdEmissionIntensityBackup";
        private const float DefaultAlphaClipThreshold = 0.01f;
        private const float DefaultSphereMode = 1.0f;
        private const float DefaultStylizedSpecularBoundary = 0.5f;
        private const float DefaultRimBoundary = 0.5f;
        private const float DefaultEmissionIntensity = 1.0f;

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

        internal static MmdToonFeatureState GetFeatureState(
            Material[] materials,
            MmdToonFeature feature)
        {
            if (materials == null || materials.Length == 0)
            {
                return MmdToonFeatureState.Off;
            }

            bool firstEnabled = IsFeatureEnabled(materials[0], feature);
            for (int i = 1; i < materials.Length; i++)
            {
                if (IsFeatureEnabled(materials[i], feature) != firstEnabled)
                {
                    return MmdToonFeatureState.Mixed;
                }
            }

            return firstEnabled ? MmdToonFeatureState.On : MmdToonFeatureState.Off;
        }

        internal static void SetFeatureEnabled(
            Material[] materials,
            MmdToonFeature feature,
            bool enabled)
        {
            if (materials == null || materials.Length == 0)
            {
                return;
            }

            Undo.RecordObjects(materials, $"{(enabled ? "Enable" : "Disable")} MMD {feature}");
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                switch (feature)
                {
                    case MmdToonFeature.ToonMap:
                        SetDirectFlag(material, "_ToonMapBound", enabled);
                        break;
                    case MmdToonFeature.NormalMap:
                        SetDirectFlag(material, "_MmdNormalMapBound", enabled);
                        break;
                    case MmdToonFeature.SphereMatCap:
                        SetSentinelFloat(
                            material,
                            "_SphereMode",
                            SphereModeBackup,
                            offValue: 0.0f,
                            restoreDefault: DefaultSphereMode,
                            enabled,
                            isEnabled: value => value > 0.5f,
                            isValidBackup: value => value > 0.5f);
                        break;
                    case MmdToonFeature.StylizedSpecular:
                        SetSentinelFloat(
                            material,
                            "_StylizedSpecularBoundary",
                            StylizedSpecularBoundaryBackup,
                            offValue: -1.0f,
                            restoreDefault: DefaultStylizedSpecularBoundary,
                            enabled,
                            isEnabled: value => value >= -0.5f,
                            isValidBackup: value => value >= -0.5f);
                        break;
                    case MmdToonFeature.RimLight:
                        SetSentinelFloat(
                            material,
                            "_RimBoundary",
                            RimBoundaryBackup,
                            offValue: -1.0f,
                            restoreDefault: DefaultRimBoundary,
                            enabled,
                            isEnabled: value => value >= -0.5f,
                            isValidBackup: value => value >= -0.5f);
                        break;
                    case MmdToonFeature.Emission:
                        SetSentinelFloat(
                            material,
                            "_MmdEmissionIntensity",
                            EmissionIntensityBackup,
                            offValue: -1.0f,
                            restoreDefault: DefaultEmissionIntensity,
                            enabled,
                            isEnabled: value => value >= 0.0f,
                            isValidBackup: value => value >= 0.0f);
                        break;
                    case MmdToonFeature.Outline:
                        SetDirectFlag(material, "_OutlineVisible", enabled);
                        break;
                }

                EditorUtility.SetDirty(material);
            }
        }

        private static bool IsFeatureEnabled(Material material, MmdToonFeature feature)
        {
            return feature switch
            {
                MmdToonFeature.ToonMap => material.GetFloat("_ToonMapBound") > 0.5f,
                MmdToonFeature.NormalMap => material.GetFloat("_MmdNormalMapBound") > 0.5f,
                MmdToonFeature.SphereMatCap => material.GetFloat("_SphereMode") > 0.5f,
                MmdToonFeature.StylizedSpecular => material.GetFloat("_StylizedSpecularBoundary") >= -0.5f,
                MmdToonFeature.RimLight => material.GetFloat("_RimBoundary") >= -0.5f,
                MmdToonFeature.Emission => material.GetFloat("_MmdEmissionIntensity") >= 0.0f,
                MmdToonFeature.Outline => material.GetFloat("_OutlineVisible") > 0.5f,
                _ => false,
            };
        }

        private static void SetDirectFlag(Material material, string propertyName, bool enabled)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, enabled ? 1.0f : 0.0f);
            }
        }

        private static void SetSentinelFloat(
            Material material,
            string propertyName,
            string backupPropertyName,
            float offValue,
            float restoreDefault,
            bool enabled,
            Func<float, bool> isEnabled,
            Func<float, bool> isValidBackup)
        {
            if (!material.HasProperty(propertyName))
            {
                return;
            }

            float current = material.GetFloat(propertyName);
            if (!enabled)
            {
                if (material.HasProperty(backupPropertyName) && isEnabled(current))
                {
                    material.SetFloat(backupPropertyName, current);
                }

                material.SetFloat(propertyName, offValue);
                return;
            }

            if (isEnabled(current))
            {
                return;
            }

            float restored = restoreDefault;
            if (material.HasProperty(backupPropertyName))
            {
                float backup = material.GetFloat(backupPropertyName);
                if (isValidBackup(backup))
                {
                    restored = backup;
                }
            }

            material.SetFloat(propertyName, restored);
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
