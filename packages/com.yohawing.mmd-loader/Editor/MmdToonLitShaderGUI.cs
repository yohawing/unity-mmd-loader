#nullable enable

using System;
using UnityEditor;
using UnityEngine;

namespace Mmd.Editor
{
    /// <summary>
    /// Grouped Inspector for the opt-in MMD Toon Lit shader.
    /// Foldout state is intentionally held by this Inspector instance and never written to a Material.
    /// </summary>
    public sealed class MmdToonLitShaderGUI : ShaderGUI
    {
        private bool[]? expanded;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (!TryValidateSelection(materialEditor, properties, out string warning))
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
                base.OnGUI(materialEditor, properties);
                return;
            }

            expanded ??= MmdToonLitShaderGUIState.CreateDefaultExpandedState();
            if (expanded.Length != MmdToonLitShaderGUIState.Sections.Length)
            {
                expanded = MmdToonLitShaderGUIState.CreateDefaultExpandedState();
            }

            for (int i = 0; i < MmdToonLitShaderGUIState.Sections.Length; i++)
            {
                MmdToonLitSectionDefinition definition = MmdToonLitShaderGUIState.Sections[i];
                MmdToonLitSectionState state = GetSectionState(definition.Section, properties);
                string title = $"{definition.DisplayName} [{state.ToString().ToUpperInvariant()}]";
                expanded[i] = EditorGUILayout.BeginFoldoutHeaderGroup(expanded[i], title);
                if (expanded[i])
                {
                    EditorGUI.indentLevel++;
                    DrawSection(definition.Section, materialEditor, properties);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }

        private static bool TryValidateSelection(
            MaterialEditor materialEditor,
            MaterialProperty[] properties,
            out string warning)
        {
            UnityEngine.Object[] targets = materialEditor.targets;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is not Material material || !MmdToonLitShaderGUIState.IsMmdToonLitShader(material.shader))
                {
                    warning = "MMD Toon Lit Inspector is only available for the MMD Toon Lit shader. Showing Unity's default Material Inspector.";
                    return false;
                }
            }

            if (!MmdToonLitShaderGUIState.HasRequiredProperties(properties, out string missing))
            {
                warning = $"MMD Toon Lit shader is missing required property '{missing}'. Showing Unity's default Material Inspector.";
                return false;
            }

            warning = string.Empty;
            return true;
        }

        private static MmdToonLitSectionState GetSectionState(
            MmdToonLitInspectorSection section,
            MaterialProperty[] properties)
        {
            switch (section)
            {
                case MmdToonLitInspectorSection.SurfaceMaps:
                    return Combine(
                        GetEnabledState(properties, "_BaseMapBound", value => value > 0.5f),
                        GetEnabledState(properties, "_ToonMapBound", value => value > 0.5f));
                case MmdToonLitInspectorSection.ToonLighting:
                    return Combine(
                        GetEnabledState(properties, "_ToonBoundary", value => value >= -0.5f),
                        GetEnabledState(properties, "_ToonBandCount", value => value >= 0.5f));
                case MmdToonLitInspectorSection.ShadowMmdSelfShadow:
                    return Combine(
                        GetEnabledState(properties, "_MmdSelfShadowReceive", value => value > 0.5f),
                        GetEnabledState(properties, "_ShadowAlphaClipThreshold", value => value > 0.0f));
                case MmdToonLitInspectorSection.Normal:
                    return GetEnabledState(properties, "_MmdNormalMapBound", value => value > 0.5f);
                case MmdToonLitInspectorSection.SphereMatCap:
                    return GetEnabledState(properties, "_SphereMode", value => value > 0.5f);
                case MmdToonLitInspectorSection.StylizedSpecular:
                    return GetEnabledState(properties, "_StylizedSpecularBoundary", value => value >= -0.5f);
                case MmdToonLitInspectorSection.RimLight:
                    return GetEnabledState(properties, "_RimBoundary", value => value >= -0.5f);
                case MmdToonLitInspectorSection.Emission:
                    return GetEnabledState(properties, "_MmdEmissionIntensity", value => value >= 0.0f);
                case MmdToonLitInspectorSection.Outline:
                    return Combine(
                        GetEnabledState(properties, "_OutlineWidth", value => value > 0.0f),
                        GetEnabledState(properties, "_OutlineVisible", value => value > 0.5f));
                case MmdToonLitInspectorSection.SurfaceRendering:
                case MmdToonLitInspectorSection.AdvancedDiagnostics:
                default:
                    return MmdToonLitSectionState.On;
            }
        }

        private static MmdToonLitSectionState GetEnabledState(
            MaterialProperty[] properties,
            string propertyName,
            Func<float, bool> enabled)
        {
            MaterialProperty? property = FindProperty(propertyName, properties);
            if (property == null)
            {
                return MmdToonLitSectionState.Off;
            }

            if (property.hasMixedValue)
            {
                return MmdToonLitSectionState.Mixed;
            }

            return enabled(property.floatValue)
                ? MmdToonLitSectionState.On
                : MmdToonLitSectionState.Off;
        }

        private static MmdToonLitSectionState Combine(params MmdToonLitSectionState[] states)
        {
            bool anyOn = false;
            bool anyMixed = false;
            for (int i = 0; i < states.Length; i++)
            {
                anyOn |= states[i] == MmdToonLitSectionState.On;
                anyMixed |= states[i] == MmdToonLitSectionState.Mixed;
            }

            if (anyMixed || (anyOn && states.Length > 1 && Array.Exists(states, state => state == MmdToonLitSectionState.Off)))
            {
                return MmdToonLitSectionState.Mixed;
            }

            return anyOn ? MmdToonLitSectionState.On : MmdToonLitSectionState.Off;
        }

        private static MaterialProperty? FindProperty(string name, MaterialProperty[] properties)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                if (string.Equals(properties[i].name, name, StringComparison.Ordinal))
                {
                    return properties[i];
                }
            }

            return null;
        }

        private static void DrawSection(
            MmdToonLitInspectorSection section,
            MaterialEditor materialEditor,
            MaterialProperty[] properties)
        {
            switch (section)
            {
                case MmdToonLitInspectorSection.SurfaceRendering:
                    DrawProperty(materialEditor, properties, "_Alpha", "Alpha");
                    DrawProperty(materialEditor, properties, "_BodyVisible", "Body Visible");
                    DrawProperty(materialEditor, properties, "_Cull", "Cull");
                    DrawProperty(materialEditor, properties, "_ZWrite", "ZWrite");
                    DrawProperty(materialEditor, properties, "_ZTest", "ZTest");
                    DrawProperty(materialEditor, properties, "_SrcBlend", "Src Blend");
                    DrawProperty(materialEditor, properties, "_DstBlend", "Dst Blend");
                    DrawProperty(materialEditor, properties, "_GammaTarget", "Gamma Composite Target");
                    materialEditor.RenderQueueField();
                    break;
                case MmdToonLitInspectorSection.SurfaceMaps:
                    DrawProperty(materialEditor, properties, "_BaseMap", "Base Map");
                    DrawProperty(materialEditor, properties, "_MainTex", "Main Texture");
                    DrawProperty(materialEditor, properties, "_BaseColor", "Base Color");
                    DrawProperty(materialEditor, properties, "_Color", "Color");
                    DrawProperty(materialEditor, properties, "_BaseMapBound", "Base Map Bound");
                    DrawProperty(materialEditor, properties, "_TextureAlphaOutputWeight", "Texture Alpha Output Weight");
                    DrawProperty(materialEditor, properties, "_ToonMapBound", "Toon Map Bound");
                    if (ShouldDrawFeatureDetails(properties, "_ToonMapBound", value => value > 0.5f))
                    {
                        DrawProperty(materialEditor, properties, "_ToonMap", "Toon Map");
                    }
                    break;
                case MmdToonLitInspectorSection.ToonLighting:
                    DrawProperty(materialEditor, properties, "_AmbientColor", "Ambient Color");
                    DrawProperty(materialEditor, properties, "_MmdLightColor", "MMD Light Color");
                    DrawProperty(materialEditor, properties, "_ToonStrength", "Toon Strength");
                    DrawProperty(materialEditor, properties, "_ToonBoundary", "Toon Boundary");
                    DrawProperty(materialEditor, properties, "_ToonBandCount", "Toon Band Count");
                    if (ShouldDrawFeatureDetails(properties, "_ToonBoundary", value => value >= -0.5f))
                    {
                        DrawProperty(materialEditor, properties, "_ToonFeather", "Toon Boundary Feather");
                    }
                    DrawProperty(materialEditor, properties, "_ReflectionProbeWeight", "Reflection Probe Weight");
                    break;
                case MmdToonLitInspectorSection.ShadowMmdSelfShadow:
                    DrawProperty(materialEditor, properties, "_MmdSelfShadowReceive", "Receive MMD Self Shadow");
                    DrawProperty(materialEditor, properties, "_ShadowAlphaClipThreshold", "Shadow Alpha Clip Threshold");
                    break;
                case MmdToonLitInspectorSection.Normal:
                    DrawProperty(materialEditor, properties, "_MmdNormalMapBound", "MMD Normal Map Bound");
                    if (ShouldDrawFeatureDetails(properties, "_MmdNormalMapBound", value => value > 0.5f))
                    {
                        DrawProperty(materialEditor, properties, "_MmdNormalMap", "MMD Normal Map");
                        DrawProperty(materialEditor, properties, "_BumpMap", "Normal Map");
                        DrawProperty(materialEditor, properties, "_BumpScale", "Normal Scale");
                    }
                    break;
                case MmdToonLitInspectorSection.SphereMatCap:
                    DrawProperty(materialEditor, properties, "_SphereMode", "Sphere Texture Mode");
                    if (ShouldDrawFeatureDetails(properties, "_SphereMode", value => value > 0.5f))
                    {
                        DrawProperty(materialEditor, properties, "_SphereMap", "Sphere Map");
                    }
                    break;
                case MmdToonLitInspectorSection.StylizedSpecular:
                    DrawProperty(materialEditor, properties, "_StylizedSpecularBoundary", "Boundary");
                    if (ShouldDrawFeatureDetails(properties, "_StylizedSpecularBoundary", value => value >= -0.5f))
                    {
                        DrawProperty(materialEditor, properties, "_StylizedSpecularColor", "Color");
                        DrawProperty(materialEditor, properties, "_StylizedSpecularFeather", "Feather");
                    }
                    break;
                case MmdToonLitInspectorSection.RimLight:
                    DrawProperty(materialEditor, properties, "_RimBoundary", "Boundary");
                    if (ShouldDrawFeatureDetails(properties, "_RimBoundary", value => value >= -0.5f))
                    {
                        DrawProperty(materialEditor, properties, "_RimColor", "Color");
                        DrawProperty(materialEditor, properties, "_RimFeather", "Feather");
                        DrawProperty(materialEditor, properties, "_RimLightFollow", "Light Follow");
                    }
                    break;
                case MmdToonLitInspectorSection.Emission:
                    DrawProperty(materialEditor, properties, "_MmdEmissionIntensity", "MMD Emission Intensity");
                    if (ShouldDrawFeatureDetails(properties, "_MmdEmissionIntensity", value => value >= 0.0f))
                    {
                        DrawProperty(materialEditor, properties, "_EmissionColor", "Emission Color");
                        DrawProperty(materialEditor, properties, "_EmissionMap", "Emission Map");
                        DrawProperty(materialEditor, properties, "_MmdEmissionMapBound", "Emission Map Bound");
                        DrawProperty(materialEditor, properties, "_MmdEmissionMaskBound", "Emission Mask Bound");
                        if (ShouldDrawFeatureDetails(properties, "_MmdEmissionMaskBound", value => value > 0.5f))
                        {
                            DrawProperty(materialEditor, properties, "_MmdEmissionMask", "Emission Mask");
                        }
                    }
                    break;
                case MmdToonLitInspectorSection.Outline:
                    DrawProperty(materialEditor, properties, "_OutlineWidth", "Outline Width");
                    DrawProperty(materialEditor, properties, "_OutlineVisible", "Outline Visible");
                    if (ShouldDrawFeatureDetails(properties, "_OutlineWidth", value => value > 0.0f))
                    {
                        DrawProperty(materialEditor, properties, "_OutlineColor", "Outline Color");
                        DrawProperty(materialEditor, properties, "_OutlineScreenSpaceWeight", "Screen Space Weight");
                        DrawProperty(materialEditor, properties, "_OutlineZTest", "Outline ZTest");
                        DrawProperty(materialEditor, properties, "_OutlineZWrite", "Outline ZWrite");
                    }
                    break;
                case MmdToonLitInspectorSection.AdvancedDiagnostics:
                    DrawProperty(materialEditor, properties, "_DiagnosticColor", "Diagnostic Color");
                    DrawProperty(materialEditor, properties, "_TextureFlatLightingWeight", "Texture Flat Lighting Weight");
                    DrawProperty(materialEditor, properties, "_TextureFlatLightingValue", "Texture Flat Lighting Value");
                    DrawProperty(materialEditor, properties, "_AlphaClipThreshold", "Alpha Clip Threshold");
                    DrawProperty(materialEditor, properties, "_MmdLightDirection", "Light Direction Override");
                    break;
            }
        }

        private static bool ShouldDrawFeatureDetails(
            MaterialProperty[] properties,
            string propertyName,
            Func<float, bool> enabled)
        {
            return MmdToonLitShaderGUIState.ShouldDrawFeatureDetails(
                GetEnabledState(properties, propertyName, enabled));
        }

        private static void DrawProperty(
            MaterialEditor materialEditor,
            MaterialProperty[] properties,
            string propertyName,
            string label)
        {
            MaterialProperty? property = FindProperty(propertyName, properties);
            if (property == null)
            {
                return;
            }

            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMixedValue;
            try
            {
                GUIContent content = new(label);
                if (property.type == MaterialProperty.PropType.Texture)
                {
                    materialEditor.TexturePropertySingleLine(content, property);
                }
                else
                {
                    materialEditor.ShaderProperty(property, content);
                }
            }
            finally
            {
                EditorGUI.showMixedValue = previousMixed;
            }
        }
    }
}
