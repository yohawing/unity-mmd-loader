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

        private enum SurfaceType
        {
            Opaque,
            Transparent,
        }

        private enum RenderFace
        {
            Front,
            Back,
            Both,
        }

        private enum GroupState
        {
            Off,
            On,
            Mixed,
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
                expanded[i] = EditorGUILayout.BeginFoldoutHeaderGroup(expanded[i], definition.DisplayName);
                DrawGroupStateCheckbox(GetGroupState(definition.Section, profile, materialEditor, properties));
                if (expanded[i])
                {
                    EditorGUI.indentLevel++;
                    DrawSection(definition.Section, profile, materialEditor, properties);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndFoldoutHeaderGroup();
            }
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
                    DrawSurfaceOptions(profile, materialEditor, properties);
                    break;
                case MmdToonLitInspectorSection.SurfaceInputs:
                    DrawSurfaceInputs(materialEditor, properties);
                    break;
                case MmdToonLitInspectorSection.DetailInputs:
                    DrawDetailInputs(materialEditor, properties);
                    break;
                case MmdToonLitInspectorSection.AdvancedOptions:
                    DrawAdvancedOptions(profile, materialEditor, properties);
                    break;
            }
        }

        private static void DrawGroupStateCheckbox(GroupState state)
        {
            Rect headerRect = GUILayoutUtility.GetLastRect();
            Rect checkboxRect = new(headerRect.xMax - 18.0f, headerRect.y + 2.0f, 16.0f, 16.0f);
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = state == GroupState.Mixed;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.Toggle(checkboxRect, state == GroupState.On);
            }

            EditorGUI.showMixedValue = previousMixed;
        }

        private static GroupState GetGroupState(
            MmdToonLitInspectorSection section,
            MmdToonInspectorProfile profile,
            MaterialEditor materialEditor,
            MaterialProperty[] properties)
        {
            return section switch
            {
                MmdToonLitInspectorSection.SurfaceOptions => GetSurfaceType(materialEditor, out bool mixed) == SurfaceType.Transparent
                    ? (mixed ? GroupState.Mixed : GroupState.On)
                    : (mixed ? GroupState.Mixed : GroupState.Off),
                MmdToonLitInspectorSection.SurfaceInputs => GroupState.On,
                MmdToonLitInspectorSection.DetailInputs => CombineGroupStates(
                    GetPropertyState(properties, "_ToonMapBound", value => value > 0.5f),
                    GetPropertyState(properties, "_SphereMode", value => value > 0.5f),
                    GetPropertyState(properties, "_MmdNormalMapBound", value => value > 0.5f)),
                MmdToonLitInspectorSection.AdvancedOptions => profile == MmdToonInspectorProfile.MmdToon
                    ? CombineGroupStates(
                        GetPropertyState(properties, "_ToonBoundary", value => value >= -0.5f),
                        GetPropertyState(properties, "_StylizedSpecularBoundary", value => value >= -0.5f),
                        GetPropertyState(properties, "_RimBoundary", value => value >= -0.5f),
                        GetPropertyState(properties, "_MmdEmissionIntensity", value => value >= 0.0f))
                    : GetPropertyState(properties, "_OutlineVisible", value => value > 0.5f),
                _ => GroupState.Off,
            };
        }

        private static SurfaceType GetSurfaceType(MaterialEditor materialEditor, out bool mixed)
        {
            return GetSurfaceType(GetMaterials(materialEditor), out mixed);
        }

        private static GroupState GetPropertyState(
            MaterialProperty[] properties,
            string propertyName,
            Func<float, bool> isEnabled)
        {
            MaterialProperty? property = FindProperty(propertyName, properties);
            if (property == null)
            {
                return GroupState.Off;
            }

            return property.hasMixedValue
                ? GroupState.Mixed
                : isEnabled(property.floatValue) ? GroupState.On : GroupState.Off;
        }

        private static GroupState CombineGroupStates(params GroupState[] states)
        {
            bool anyOn = false;
            bool anyOff = false;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] == GroupState.Mixed)
                {
                    return GroupState.Mixed;
                }

                anyOn |= states[i] == GroupState.On;
                anyOff |= states[i] == GroupState.Off;
            }

            return anyOn && anyOff ? GroupState.Mixed : anyOn ? GroupState.On : GroupState.Off;
        }

        private static void DrawSurfaceOptions(
            MmdToonInspectorProfile profile,
            MaterialEditor materialEditor,
            MaterialProperty[] properties)
        {
            Material[] materials = GetMaterials(materialEditor);
            DrawSurfaceType(profile, materials);
            DrawRenderFace(materials);
            DrawAlphaClipping(properties);
            DrawProperty(materialEditor, properties, "_ToonStrength", "Toon Strength");
            materialEditor.RenderQueueField();
        }

        private static void DrawSurfaceInputs(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            MaterialProperty? baseMap = FindProperty("_BaseMap", properties);
            MaterialProperty? baseColor = FindProperty("_BaseColor", properties);
            if (baseMap != null && baseColor != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, baseColor);
                materialEditor.TextureScaleOffsetProperty(baseMap);
            }

            DrawProperty(materialEditor, properties, "_Alpha", "Alpha");
        }

        private static void DrawDetailInputs(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            DrawTextureFeature(materialEditor, properties, "_ToonMapBound", "_ToonMap", "Toon Map");
            DrawSphereFeature(materialEditor, properties);
            DrawTextureFeature(materialEditor, properties, "_MmdNormalMapBound", "_MmdNormalMap", "Normal Map");
        }

        private static void DrawAdvancedOptions(
            MmdToonInspectorProfile profile,
            MaterialEditor materialEditor,
            MaterialProperty[] properties)
        {
            EditorGUILayout.LabelField("MMD Lighting", EditorStyles.boldLabel);
            DrawProperty(materialEditor, properties, "_AmbientColor", "Ambient Color");
            DrawProperty(materialEditor, properties, "_MmdLightColor", "Light Color");
            DrawProperty(materialEditor, properties, "_MmdLightDirection", "Light Direction Override");
            DrawProperty(materialEditor, properties, "_MmdSelfShadowReceive", "Receive MMD Self Shadow");

            EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            EditorGUILayout.LabelField("Outline", EditorStyles.boldLabel);
            DrawProperty(materialEditor, properties, "_OutlineVisible", "Enabled");
            DrawProperty(materialEditor, properties, "_OutlineColor", "Color");
            DrawProperty(materialEditor, properties, "_OutlineWidth", "Width");
            DrawProperty(materialEditor, properties, "_OutlineScreenSpaceWeight", "Screen Space Weight");

            if (profile != MmdToonInspectorProfile.MmdToon)
            {
                return;
            }

            EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            EditorGUILayout.LabelField("MMD URP Toon", EditorStyles.boldLabel);
            DrawProperty(materialEditor, properties, "_ToonBoundary", "Toon Boundary");
            DrawProperty(materialEditor, properties, "_ToonFeather", "Toon Boundary Feather");
            DrawProperty(materialEditor, properties, "_ToonBandCount", "Toon Band Count");
            DrawProperty(materialEditor, properties, "_ReflectionProbeWeight", "Reflection Probe Weight");
            DrawProperty(materialEditor, properties, "_StylizedSpecularColor", "Stylized Specular Color");
            DrawProperty(materialEditor, properties, "_StylizedSpecularBoundary", "Stylized Specular Boundary");
            DrawProperty(materialEditor, properties, "_StylizedSpecularFeather", "Stylized Specular Feather");
            DrawProperty(materialEditor, properties, "_RimColor", "Rim Color");
            DrawProperty(materialEditor, properties, "_RimBoundary", "Rim Boundary");
            DrawProperty(materialEditor, properties, "_RimFeather", "Rim Feather");
            DrawProperty(materialEditor, properties, "_RimLightFollow", "Rim Light Follow");
            DrawProperty(materialEditor, properties, "_MmdEmissionIntensity", "Emission Intensity");
            DrawProperty(materialEditor, properties, "_EmissionColor", "Emission Color");
            DrawProperty(materialEditor, properties, "_EmissionMap", "Emission Map");
            DrawProperty(materialEditor, properties, "_MmdEmissionMask", "Emission Mask");
        }

        private static void DrawSurfaceType(MmdToonInspectorProfile profile, Material[] materials)
        {
            SurfaceType current = GetSurfaceType(materials, out bool mixed);
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            SurfaceType selected = (SurfaceType)EditorGUILayout.EnumPopup("Surface Type", current);
            if (EditorGUI.EndChangeCheck())
            {
                ApplySurfaceType(profile, materials, selected);
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

        private static void DrawAlphaClipping(MaterialProperty[] properties)
        {
            MaterialProperty? threshold = FindProperty("_AlphaClipThreshold", properties);
            if (threshold == null)
            {
                return;
            }

            bool current = threshold.floatValue > 0.0f;
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = threshold.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            bool selected = EditorGUILayout.Toggle("Alpha Clipping", current);
            if (EditorGUI.EndChangeCheck())
            {
                threshold.floatValue = selected ? Mathf.Max(threshold.floatValue, 0.01f) : 0.0f;
            }

            EditorGUI.showMixedValue = previousMixed;
        }

        private static void DrawTextureFeature(
            MaterialEditor materialEditor,
            MaterialProperty[] properties,
            string enabledPropertyName,
            string texturePropertyName,
            string label)
        {
            MaterialProperty? enabled = FindProperty(enabledPropertyName, properties);
            MaterialProperty? texture = FindProperty(texturePropertyName, properties);
            if (enabled == null || texture == null)
            {
                return;
            }

            bool isEnabled = enabled.floatValue > 0.5f;
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = enabled.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            bool updated = EditorGUILayout.Toggle(label, isEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                enabled.floatValue = updated ? 1.0f : 0.0f;
                isEnabled = updated;
            }

            EditorGUI.showMixedValue = previousMixed;
            if (isEnabled || enabled.hasMixedValue)
            {
                EditorGUI.indentLevel++;
                materialEditor.TexturePropertySingleLine(GUIContent.none, texture);
                EditorGUI.indentLevel--;
            }
        }

        private static void DrawSphereFeature(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            MaterialProperty? mode = FindProperty("_SphereMode", properties);
            MaterialProperty? map = FindProperty("_SphereMap", properties);
            if (mode == null || map == null)
            {
                return;
            }

            string[] names = { "Off", "Multiply", "Add" };
            int current = Mathf.Clamp(Mathf.RoundToInt(mode.floatValue), 0, names.Length - 1);
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = mode.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup("Sphere Map", current, names);
            if (EditorGUI.EndChangeCheck())
            {
                mode.floatValue = selected;
                current = selected;
            }

            EditorGUI.showMixedValue = previousMixed;
            if (current > 0 || mode.hasMixedValue)
            {
                EditorGUI.indentLevel++;
                materialEditor.TexturePropertySingleLine(GUIContent.none, map);
                EditorGUI.indentLevel--;
            }
        }

        private static SurfaceType GetSurfaceType(Material[] materials, out bool mixed)
        {
            SurfaceType result = IsTransparent(materials[0]) ? SurfaceType.Transparent : SurfaceType.Opaque;
            mixed = false;
            for (int i = 1; i < materials.Length; i++)
            {
                if ((IsTransparent(materials[i]) ? SurfaceType.Transparent : SurfaceType.Opaque) != result)
                {
                    mixed = true;
                    break;
                }
            }

            return result;
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

        private static void ApplySurfaceType(MmdToonInspectorProfile profile, Material[] materials, SurfaceType selected)
        {
            Undo.RecordObjects(materials, "Change MMD Toon Surface Type");
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                bool wasTransparent = IsTransparent(material);
                bool isTransparent = selected == SurfaceType.Transparent;
                material.SetFloat("_SrcBlend", isTransparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
                material.SetFloat("_DstBlend", isTransparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1.0f);
                if (profile == MmdToonInspectorProfile.MmdToon)
                {
                    if (isTransparent)
                    {
                        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    }
                    else
                    {
                        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    }
                }

                int oldDefaultQueue = wasTransparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
                if (material.renderQueue == oldDefaultQueue)
                {
                    material.renderQueue = isTransparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
                }

                EditorUtility.SetDirty(material);
            }
        }

        private static bool IsTransparent(Material material)
        {
            return material.GetFloat("_DstBlend") != (float)BlendMode.Zero ||
                material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
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

            materialEditor.ShaderProperty(property, new GUIContent(label));
        }
    }
}
