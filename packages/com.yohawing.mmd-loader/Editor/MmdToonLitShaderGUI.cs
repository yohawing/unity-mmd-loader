#nullable enable

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mmd.Editor
{
    /// <summary>
    /// URP Lit-shaped Inspector shared by the parity Basic Toon and URP-integrated Toon profiles.
    /// Public shader names are used for new materials; the previous names remain supported aliases.
    /// </summary>
    public sealed class MmdToonLitShaderGUI : ShaderGUI
    {
        private bool[]? expanded;

        private enum RenderFace
        {
            Front,
            Back,
            Both,
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (!TryValidateSelection(materialEditor, properties, out MmdToonInspectorProfile profile, out string warning))
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
                base.OnGUI(materialEditor, properties);
                return;
            }

            expanded ??= MmdToonLitShaderGUIState.CreateDefaultExpandedState();
            for (int i = 0; i < MmdToonLitShaderGUIState.Sections.Length; i++)
            {
                MmdToonLitSectionDefinition definition = MmdToonLitShaderGUIState.Sections[i];
                if (!MmdToonLitShaderGUIState.SupportsSection(profile, definition.Section))
                {
                    continue;
                }

                expanded[i] = DrawSectionHeader(
                    expanded[i], definition, materialEditor);
                if (expanded[i])
                {
                    EditorGUI.indentLevel++;
                    using (new EditorGUI.DisabledScope(
                        !IsSectionContentEditable(definition.Section, materialEditor)))
                    {
                        DrawSection(definition.Section, profile, materialEditor, properties);
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }

        private static bool IsSectionContentEditable(
            MmdToonLitInspectorSection section,
            MaterialEditor materialEditor)
        {
            return !TryGetFeature(section, out MmdToonFeature feature) ||
                MmdToonMaterialStateSync.GetFeatureState(GetMaterials(materialEditor), feature) !=
                MmdToonFeatureState.Off;
        }

        private static bool TryValidateSelection(
            MaterialEditor materialEditor,
            MaterialProperty[] properties,
            out MmdToonInspectorProfile profile,
            out string warning)
        {
            profile = default;
            UnityEngine.Object[] targets = materialEditor.targets;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is not Material material || !MmdToonLitShaderGUIState.TryGetProfile(material.shader, out MmdToonInspectorProfile targetProfile))
                {
                    warning = "This Inspector is only available for MMD Basic Toon and MMD URP Toon materials. Showing Unity's default Material Inspector.";
                    return false;
                }

                if (i == 0)
                {
                    profile = targetProfile;
                }
                else if (profile != targetProfile)
                {
                    warning = "MMD Basic Toon and MMD URP Toon cannot be edited in the same selection. Showing Unity's default Material Inspector.";
                    return false;
                }
            }

            if (!MmdToonLitShaderGUIState.HasRequiredProperties(properties, out string missing))
            {
                warning = $"{MmdToonLitShaderGUIState.GetDisplayName(profile)} is missing required property '{missing}'. Showing Unity's default Material Inspector.";
                return false;
            }

            warning = string.Empty;
            return true;
        }

        private static void DrawSection(
            MmdToonLitInspectorSection section,
            MmdToonInspectorProfile profile,
            MaterialEditor materialEditor,
            MaterialProperty[] properties)
        {
            switch (section)
            {
                case MmdToonLitInspectorSection.SurfaceOptions:
                    DrawSurfaceOptions(materialEditor, properties);
                    break;
                case MmdToonLitInspectorSection.SurfaceInputs:
                    DrawSurfaceInputs(materialEditor, properties);
                    break;
                case MmdToonLitInspectorSection.ToonMap:
                    DrawTextureMap(materialEditor, properties, "_ToonMap", "Toon Map");
                    break;
                case MmdToonLitInspectorSection.NormalMap:
                    DrawTextureMap(materialEditor, properties, "_MmdNormalMap", "Normal Map");
                    break;
                case MmdToonLitInspectorSection.SphereMatCap:
                    DrawSphereInputs(materialEditor, properties);
                    break;
                case MmdToonLitInspectorSection.ToonLighting:
                    DrawToonLighting(profile, materialEditor, properties);
                    break;
                case MmdToonLitInspectorSection.StylizedSpecular:
                    DrawStylizedSpecular(materialEditor, properties);
                    break;
                case MmdToonLitInspectorSection.RimLight:
                    DrawRimLight(materialEditor, properties);
                    break;
                case MmdToonLitInspectorSection.Emission:
                    DrawEmission(materialEditor, properties);
                    break;
                case MmdToonLitInspectorSection.Outline:
                    DrawOutline(materialEditor, properties);
                    break;
                case MmdToonLitInspectorSection.AdvancedOptions:
                    materialEditor.RenderQueueField();
                    break;
            }
        }

        private static bool DrawSectionHeader(
            bool isExpanded,
            MmdToonLitSectionDefinition definition,
            MaterialEditor materialEditor)
        {
            const float headerHeight = 17.0f;
            const float headerIndent = 15.0f;
            const float foldoutSize = 13.0f;
            Rect headerRect = GUILayoutUtility.GetRect(
                1.0f,
                headerHeight,
                GUILayout.ExpandWidth(true));
            if (headerRect.xMin != 0.0f)
            {
                headerRect.xMin = 1.0f + headerIndent * (EditorGUI.indentLevel + 1);
            }

            if (Event.current.type == EventType.Repaint)
            {
                float backgroundTint = EditorGUIUtility.isProSkin ? 0.1f : 1.0f;
                Rect backgroundRect = headerRect;
                backgroundRect.xMin = 0.0f;
                backgroundRect.width += 4.0f;
                EditorGUI.DrawRect(
                    backgroundRect,
                    new Color(backgroundTint, backgroundTint, backgroundTint, 0.2f));
            }

            const float featureSpacing = 2.0f;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            Rect titleRect = headerRect;
            if (TryGetFeature(definition.Section, out MmdToonFeature feature))
            {
                Material[] materials = GetMaterials(materialEditor);
                MmdToonFeatureState state = MmdToonMaterialStateSync.GetFeatureState(materials, feature);
                float toggleSize = lineHeight;
                Rect toggleRect = headerRect;
                toggleRect.xMin = headerRect.xMax - toggleSize;
                toggleRect.yMin = headerRect.y + (headerRect.height - lineHeight) * 0.5f;
                toggleRect.width = toggleSize;
                toggleRect.height = lineHeight;
                titleRect.xMax = Mathf.Max(titleRect.xMin, toggleRect.xMin - featureSpacing);

                bool previousMixed = EditorGUI.showMixedValue;
                try
                {
                    EditorGUI.showMixedValue = state == MmdToonFeatureState.Mixed;
                    EditorGUI.BeginChangeCheck();
                    bool enabled = EditorGUI.Toggle(toggleRect, state == MmdToonFeatureState.On);
                    if (EditorGUI.EndChangeCheck())
                    {
                        MmdToonMaterialStateSync.SetFeatureEnabled(materials, feature, enabled);
                    }
                }
                finally
                {
                    EditorGUI.showMixedValue = previousMixed;
                }
            }

            Rect labelRectForTitle = titleRect;
            labelRectForTitle.xMin += 16.0f;
            EditorGUI.LabelField(
                labelRectForTitle,
                new GUIContent(definition.DisplayName),
                EditorStyles.boldLabel);

            Rect foldoutRect = headerRect;
            foldoutRect.x = labelRectForTitle.xMin + headerIndent * (EditorGUI.indentLevel - 1);
            foldoutRect.y += 1.0f;
            foldoutRect.width = foldoutSize;
            foldoutRect.height = foldoutSize;
            bool nextExpanded = GUI.Toggle(
                foldoutRect,
                isExpanded,
                GUIContent.none,
                EditorStyles.foldout);

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                labelRectForTitle.Contains(Event.current.mousePosition))
            {
                nextExpanded = !isExpanded;
                Event.current.Use();
                GUI.changed = true;
            }

            return nextExpanded;
        }

        private static bool TryGetFeature(MmdToonLitInspectorSection section, out MmdToonFeature feature)
        {
            feature = section switch
            {
                MmdToonLitInspectorSection.ToonMap => MmdToonFeature.ToonMap,
                MmdToonLitInspectorSection.NormalMap => MmdToonFeature.NormalMap,
                MmdToonLitInspectorSection.SphereMatCap => MmdToonFeature.SphereMatCap,
                MmdToonLitInspectorSection.StylizedSpecular => MmdToonFeature.StylizedSpecular,
                MmdToonLitInspectorSection.RimLight => MmdToonFeature.RimLight,
                MmdToonLitInspectorSection.Emission => MmdToonFeature.Emission,
                MmdToonLitInspectorSection.Outline => MmdToonFeature.Outline,
                _ => default,
            };
            return section is MmdToonLitInspectorSection.ToonMap or
                MmdToonLitInspectorSection.NormalMap or
                MmdToonLitInspectorSection.SphereMatCap or
                MmdToonLitInspectorSection.StylizedSpecular or
                MmdToonLitInspectorSection.RimLight or
                MmdToonLitInspectorSection.Emission or
                MmdToonLitInspectorSection.Outline;
        }

        private static void DrawSurfaceOptions(
            MaterialEditor materialEditor,
            MaterialProperty[] properties)
        {
            Material[] materials = GetMaterials(materialEditor);
            DrawSurfaceType(materials);
            DrawRenderFace(materials);
            DrawCutoutThreshold(materialEditor, materials, properties);
        }

        private static void DrawSurfaceInputs(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            MaterialProperty? baseMap = FindOptionalProperty("_BaseMap", properties);
            MaterialProperty? baseColor = FindOptionalProperty("_BaseColor", properties);
            if (baseMap != null && baseColor != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, baseColor);
                materialEditor.TextureScaleOffsetProperty(baseMap);
            }

            DrawProperty(materialEditor, properties, "_Alpha", "Alpha");
        }

        private static void DrawTextureMap(
            MaterialEditor materialEditor,
            MaterialProperty[] properties,
            string propertyName,
            string label)
        {
            MaterialProperty? map = FindOptionalProperty(propertyName, properties);
            if (map != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent(label), map);
            }
        }

        private static void DrawToonLighting(
            MmdToonInspectorProfile profile,
            MaterialEditor materialEditor,
            MaterialProperty[] properties)
        {
            MaterialProperty? authoringMode = profile == MmdToonInspectorProfile.MmdToon
                ? FindOptionalProperty("_ToonAuthoringMode", properties)
                : null;
            if (authoringMode != null)
            {
                materialEditor.ShaderProperty(authoringMode, new GUIContent("Toon Authoring Mode"));
            }

            DrawProperty(materialEditor, properties, "_ToonStrength", "Toon Strength");
            DrawProperty(materialEditor, properties, "_AmbientColor", "Ambient Color");
            DrawProperty(materialEditor, properties, "_MmdLightColor", "Light Color");
            DrawProperty(materialEditor, properties, "_MmdLightDirection", "Light Direction Override");
            if (profile == MmdToonInspectorProfile.MmdToon)
            {
                DrawProperty(materialEditor, properties, "_ReceiveSSAO", "Receive SSAO");
                bool shadeColorsSelected = authoringMode != null && authoringMode.floatValue > 0.5f;
                bool showMmdRampProperties = authoringMode == null ||
                    authoringMode.hasMixedValue || !shadeColorsSelected;
                bool showShadeColorProperties = authoringMode != null &&
                    (authoringMode.hasMixedValue || shadeColorsSelected);
                if (showMmdRampProperties)
                {
                    DrawProperty(materialEditor, properties, "_ToonBoundary", "Toon Boundary");
                    DrawProperty(materialEditor, properties, "_ToonFeather", "Toon Boundary Feather");
                    DrawProperty(materialEditor, properties, "_ToonBandCount", "Toon Band Count");
                }
                if (showShadeColorProperties)
                {
                    DrawProperty(materialEditor, properties, "_ShadeBaseColor", "Base Shade Color");
                    DrawProperty(materialEditor, properties, "_FirstShadeColor", "1st Shade Color");
                    DrawProperty(materialEditor, properties, "_SecondShadeColor", "2nd Shade Color");
                    DrawProperty(materialEditor, properties, "_BaseToFirstShadeBoundary", "Base / 1st Boundary");
                    DrawProperty(materialEditor, properties, "_BaseToFirstShadeFeather", "Base / 1st Feather");
                    DrawProperty(materialEditor, properties, "_FirstToSecondShadeBoundary", "1st / 2nd Boundary");
                    DrawProperty(materialEditor, properties, "_FirstToSecondShadeFeather", "1st / 2nd Feather");
                }
                DrawProperty(materialEditor, properties, "_ReflectionProbeWeight", "Reflection Probe Weight");
            }
        }

        private static void DrawStylizedSpecular(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            DrawProperty(materialEditor, properties, "_StylizedSpecularColor", "Color");
            DrawProperty(materialEditor, properties, "_StylizedSpecularBoundary", "Boundary");
            DrawProperty(materialEditor, properties, "_StylizedSpecularFeather", "Feather");
        }

        private static void DrawRimLight(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            DrawProperty(materialEditor, properties, "_RimColor", "Color");
            DrawProperty(materialEditor, properties, "_RimBoundary", "Boundary");
            DrawProperty(materialEditor, properties, "_RimFeather", "Feather");
            DrawProperty(materialEditor, properties, "_RimLightFollow", "Light Follow");
        }

        private static void DrawEmission(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            DrawProperty(materialEditor, properties, "_MmdEmissionIntensity", "Intensity");
            DrawProperty(materialEditor, properties, "_EmissionColor", "Color");
            DrawTextureWithBound(materialEditor, properties, "_EmissionMap", "_MmdEmissionMapBound", "Emission Map");
            DrawTextureWithBound(materialEditor, properties, "_MmdEmissionMask", "_MmdEmissionMaskBound", "Mask");
        }

        private static void DrawOutline(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            DrawProperty(materialEditor, properties, "_OutlineColor", "Color");
            DrawProperty(materialEditor, properties, "_OutlineWidth", "Width");
            DrawProperty(materialEditor, properties, "_OutlineScreenSpaceWeight", "Screen Space Weight");
        }

        private static void DrawTextureWithBound(
            MaterialEditor materialEditor,
            MaterialProperty[] properties,
            string textureName,
            string boundName,
            string label)
        {
            MaterialProperty? texture = FindOptionalProperty(textureName, properties);
            MaterialProperty? bound = FindOptionalProperty(boundName, properties);
            if (texture != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent(label), texture, bound);
            }
        }

        private static void DrawSurfaceType(Material[] materials)
        {
            MmdToonSurfaceType current = MmdToonMaterialStateSync.GetSurfaceType(materials, out bool mixed);
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            MmdToonSurfaceType selected = (MmdToonSurfaceType)EditorGUILayout.EnumPopup("Surface Type", current);
            if (EditorGUI.EndChangeCheck())
            {
                MmdToonMaterialStateSync.ApplySurfaceType(materials, selected);
            }

            EditorGUI.showMixedValue = previousMixed;
        }

        private static void DrawRenderFace(Material[] materials)
        {
            RenderFace current = GetRenderFace(materials, out bool mixed);
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            RenderFace selected = (RenderFace)EditorGUILayout.EnumPopup("Render Face", current);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(materials, "Change MMD Toon Render Face");
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i].SetFloat("_Cull", selected switch
                    {
                        RenderFace.Back => (float)CullMode.Front,
                        RenderFace.Both => (float)CullMode.Off,
                        _ => (float)CullMode.Back,
                    });
                    EditorUtility.SetDirty(materials[i]);
                }
            }

            EditorGUI.showMixedValue = previousMixed;
        }

        private static void DrawCutoutThreshold(
            MaterialEditor materialEditor,
            Material[] materials,
            MaterialProperty[] properties)
        {
            if (MmdToonMaterialStateSync.GetSurfaceType(materials, out bool mixed) != MmdToonSurfaceType.Cutout && !mixed)
            {
                return;
            }

            MaterialProperty? threshold = FindOptionalProperty("_AlphaClipThreshold", properties);
            if (threshold == null)
            {
                return;
            }

            materialEditor.ShaderProperty(threshold, new GUIContent("Alpha Clip Threshold"));
            DrawProperty(materialEditor, properties, "_ShadowAlphaClipThreshold", "Shadow Alpha Clip Threshold");
        }

        private static void DrawSphereInputs(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            MaterialProperty? mode = FindOptionalProperty("_SphereMode", properties);
            MaterialProperty? map = FindOptionalProperty("_SphereMap", properties);
            if (mode == null || map == null)
            {
                return;
            }

            string[] names = { "Multiply", "Add" };
            int current = Mathf.Clamp(Mathf.RoundToInt(mode.floatValue) - 1, 0, names.Length - 1);
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = mode.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup("Blend Mode", current, names);
            if (EditorGUI.EndChangeCheck())
            {
                mode.floatValue = selected + 1;
                current = selected;
            }

            EditorGUI.showMixedValue = previousMixed;
            materialEditor.TexturePropertySingleLine(new GUIContent("Sphere Map"), map);
        }

        private static RenderFace GetRenderFace(Material[] materials, out bool mixed)
        {
            RenderFace result = ToRenderFace(materials[0].GetFloat("_Cull"));
            mixed = false;
            for (int i = 1; i < materials.Length; i++)
            {
                if (ToRenderFace(materials[i].GetFloat("_Cull")) != result)
                {
                    mixed = true;
                    break;
                }
            }

            return result;
        }

        private static RenderFace ToRenderFace(float cull)
        {
            return Mathf.RoundToInt(cull) switch
            {
                (int)CullMode.Front => RenderFace.Back,
                (int)CullMode.Off => RenderFace.Both,
                _ => RenderFace.Front,
            };
        }

        private static Material[] GetMaterials(MaterialEditor materialEditor)
        {
            var materials = new Material[materialEditor.targets.Length];
            for (int i = 0; i < materialEditor.targets.Length; i++)
            {
                materials[i] = (Material)materialEditor.targets[i];
            }

            return materials;
        }

        private static MaterialProperty? FindOptionalProperty(string name, MaterialProperty[] properties)
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

        private static void DrawProperty(
            MaterialEditor materialEditor,
            MaterialProperty[] properties,
            string propertyName,
            string label)
        {
            MaterialProperty? property = FindOptionalProperty(propertyName, properties);
            if (property == null)
            {
                return;
            }

            materialEditor.ShaderProperty(property, new GUIContent(label));
        }
    }
}
