#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Mmd.Editor
{
    [CustomEditor(typeof(MmdPmxScriptedImporter))]
    public sealed class MmdPmxScriptedImporterEditor : ScriptedImporterEditor
    {
        private SerializedProperty? importScaleProperty;
        private SerializedProperty? modelPresetProperty;
        private SerializedProperty? materialTexturePolicyProperty;
        private SerializedProperty? shaderPresetProperty;
        private SerializedProperty? materialOverrideAssetProperty;
        private SerializedProperty? materialRemapsProperty;
        private SerializedProperty? animationTypeProperty;
        private SerializedProperty? humanoidBoneMappingOverridesProperty;
        private SerializedProperty? upperArmTwistProperty;
        private SerializedProperty? lowerArmTwistProperty;
        private SerializedProperty? upperLegTwistProperty;
        private SerializedProperty? lowerLegTwistProperty;
        private SerializedProperty? armStretchProperty;
        private SerializedProperty? legStretchProperty;
        private SerializedProperty? feetSpacingProperty;
        private SerializedProperty? hasTranslationDoFProperty;
        private MmdPmxAsset? cachedAsset;
        private bool onDemandRemapExpanded = true;

        private bool toonShaderSettingsExpanded = true;
        private bool advancedHumanoidSettingsExpanded;
        private bool humanoidMappingOverridesExpanded;
        private bool retargetQualitySettingsExpanded;

        private int selectedTab;
        private static readonly string[] TabNames = { "Model", "Rig", "Materials" };

        public override void OnEnable()
        {
            base.OnEnable();
            importScaleProperty = serializedObject.FindProperty("importScale");
            modelPresetProperty = serializedObject.FindProperty("modelPreset");
            materialTexturePolicyProperty = serializedObject.FindProperty("materialTexturePolicy");
            shaderPresetProperty = serializedObject.FindProperty("shaderPreset");
            materialOverrideAssetProperty = serializedObject.FindProperty("materialOverrideAsset");
            materialRemapsProperty = serializedObject.FindProperty("materialRemaps");
            animationTypeProperty = serializedObject.FindProperty("animationType");
            humanoidBoneMappingOverridesProperty = serializedObject.FindProperty("humanoidBoneMappingOverrides");
            upperArmTwistProperty = serializedObject.FindProperty("upperArmTwist");
            lowerArmTwistProperty = serializedObject.FindProperty("lowerArmTwist");
            upperLegTwistProperty = serializedObject.FindProperty("upperLegTwist");
            lowerLegTwistProperty = serializedObject.FindProperty("lowerLegTwist");
            armStretchProperty = serializedObject.FindProperty("armStretch");
            legStretchProperty = serializedObject.FindProperty("legStretch");
            feetSpacingProperty = serializedObject.FindProperty("feetSpacing");
            hasTranslationDoFProperty = serializedObject.FindProperty("hasTranslationDoF");
            ResolveImportedAsset();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            MmdPmxAsset? asset = ResolveImportedAsset();

            if (selectedTab >= TabNames.Length)
            {
                selectedTab = 0;
            }

            selectedTab = GUILayout.Toolbar(selectedTab, TabNames);
            EditorGUILayout.Space();

            switch (selectedTab)
            {
                case 0:
                    DrawModelTab(asset);
                    break;
                case 1:
                    DrawRigTab(asset);
                    break;
                case 2:
                    DrawMaterialTab(asset);
                    break;
            }

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }

        private void DrawModelTab(MmdPmxAsset? asset)
        {
            if (modelPresetProperty != null)
            {
                EditorGUILayout.PropertyField(modelPresetProperty, new GUIContent("Model Preset"));
            }

            if (importScaleProperty != null)
            {
                EditorGUILayout.PropertyField(importScaleProperty,
                    new GUIContent("Import Scale",
                        "Scale applied to the imported PMX model. Stored as an import setting and asset summary. Runtime playback and live physics are scale-aware."));
            }

            if (asset != null)
            {
                DrawReadOnlySelectableText("Model", string.IsNullOrWhiteSpace(asset.ModelName) ? asset.name : asset.ModelName);
            }
            else
            {
                EditorGUILayout.HelpBox("Apply import settings to refresh the PMX asset.", MessageType.Warning);
            }

            bool hasModifiedImportSettings = HasModified();
            if (asset != null)
            {
                DrawPendingImportSettingsWarning(hasModifiedImportSettings);
            }
        }

        private void DrawMaterialTab(MmdPmxAsset? asset)
        {
            if (asset != null)
            {
                DrawRemappedMaterials(asset);
            }
            else
            {
                EditorGUILayout.HelpBox("Apply import settings to refresh material data.", MessageType.Warning);
            }

            if (materialOverrideAssetProperty != null)
            {
                EditorGUILayout.PropertyField(
                    materialOverrideAssetProperty,
                    new GUIContent(
                        "Material Override",
                        "Applies per-material PBR overrides after PMX texture binding."));
            }

            // Minimal Toon Shader Settings surface. Shader Preset is an importer setting; diagnostics stay out of the normal UI.
            toonShaderSettingsExpanded = EditorGUILayout.Foldout(
                toonShaderSettingsExpanded, "Toon Shader Settings", toggleOnLabelClick: true);
            if (toonShaderSettingsExpanded)
            {
                if (shaderPresetProperty != null)
                {
                    EditorGUILayout.PropertyField(
                        shaderPresetProperty,
                        new GUIContent(
                            "Shader Preset",
                            "Selects the target shader family for material generation and Toon settings handoff. Only MmdBasicUrpToon is available in this slice. The value is an importer setting; it is summarized on the imported asset after Apply/Reimport. This UI is not a full Material Editor and does not mutate generated materials."));
                }
            }

            DrawPendingImportSettingsWarning(HasModified());
        }

        private void DrawRigTab(MmdPmxAsset? asset)
        {
            // Animation Type selection (D2: Rig tab owns animation type).
            if (animationTypeProperty != null)
            {
                EditorGUILayout.PropertyField(animationTypeProperty,
                    new GUIContent("Animation Type",
                        "Controls how the imported model's bones map to Unity's animation system. " +
                        "Generic leaves bones as-is. Humanoid builds a Unity Avatar sub-asset " +
                        "via automatic MMD-to-Humanoid bone mapping."));
            }

            MmdPmxAnimationType currentType = MmdPmxAnimationType.Generic;
            if (animationTypeProperty != null)
            {
                currentType = (MmdPmxAnimationType)animationTypeProperty.enumValueIndex;
            }

            // Normal Rig UI keeps Humanoid details implicit; only blocked Avatar creation needs a warning.
            if (currentType == MmdPmxAnimationType.Humanoid)
            {
                if (asset != null)
                {
                    if (asset.BoneCount == 0)
                    {
                        EditorGUILayout.HelpBox(
                            "No bones found. Avatar cannot be created for this model.",
                            MessageType.Warning);
                    }
                    else if (asset.ImportedAvatar == null)
                    {
                        EditorGUILayout.HelpBox(BuildAvatarReadinessTooltip(asset), MessageType.Warning);
                    }

                    DrawAdvancedHumanoidSettings(asset);
                }
            }
            else if (asset == null)
            {
                EditorGUILayout.HelpBox("Apply import settings to refresh rig data.", MessageType.Warning);
            }

            DrawPendingImportSettingsWarning(HasModified());
        }

        private static string BuildAvatarReadinessTooltip(MmdPmxAsset asset)
        {
            if (!string.IsNullOrWhiteSpace(asset.HumanoidAvatarDiagnostic))
            {
                return asset.HumanoidAvatarDiagnostic;
            }

            if (asset.ImportedAvatar != null)
            {
                return "Imported Avatar sub-asset is present. Reimport the PMX asset to rebuild it after changing Humanoid settings.";
            }

            return "Avatar sub-asset is not built yet. Apply/Reimport to refresh Humanoid import output.";
        }

        private void DrawAdvancedHumanoidSettings(MmdPmxAsset? asset)
        {
            bool hasOverrides = humanoidBoneMappingOverridesProperty != null
                && humanoidBoneMappingOverridesProperty.arraySize > 0;

            EditorGUILayout.Space();
            advancedHumanoidSettingsExpanded = EditorGUILayout.Foldout(
                advancedHumanoidSettingsExpanded,
                new GUIContent(
                    "Advanced Humanoid Settings",
                    hasOverrides
                        ? "Manual bone mapping overrides are applied. Expand to review or clear them."
                        : "Advanced repair and retarget tuning settings. Leave collapsed for the normal import workflow."),
                toggleOnLabelClick: true);

            if (!advancedHumanoidSettingsExpanded)
            {
                return;
            }

            DrawHumanoidMappingOverrides(asset);
            DrawRetargetQualitySettings();
        }

        private void DrawRetargetQualitySettings()
        {
            if (upperArmTwistProperty == null
                || lowerArmTwistProperty == null
                || upperLegTwistProperty == null
                || lowerLegTwistProperty == null
                || armStretchProperty == null
                || legStretchProperty == null
                || feetSpacingProperty == null
                || hasTranslationDoFProperty == null)
            {
                return;
            }

            EditorGUILayout.Space();
            retargetQualitySettingsExpanded = EditorGUILayout.Foldout(
                retargetQualitySettingsExpanded,
                "Retarget Quality Settings",
                toggleOnLabelClick: true);
            if (!retargetQualitySettingsExpanded)
            {
                return;
            }

            Draw01Slider(
                upperArmTwistProperty,
                new GUIContent(
                    "Upper Arm Twist",
                    "Controls how Unity distributes upper arm twist during Humanoid retargeting."));
            Draw01Slider(
                lowerArmTwistProperty,
                new GUIContent(
                    "Lower Arm Twist",
                    "Controls how Unity distributes lower arm twist during Humanoid retargeting."));
            Draw01Slider(
                upperLegTwistProperty,
                new GUIContent(
                    "Upper Leg Twist",
                    "Controls how Unity distributes upper leg twist during Humanoid retargeting."));
            Draw01Slider(
                lowerLegTwistProperty,
                new GUIContent(
                    "Lower Leg Twist",
                    "Controls how Unity distributes lower leg twist during Humanoid retargeting."));
            Draw01Slider(
                armStretchProperty,
                new GUIContent(
                    "Arm Stretch",
                    "Controls Unity's Humanoid arm stretch limit used by IK retargeting."));
            Draw01Slider(
                legStretchProperty,
                new GUIContent(
                    "Leg Stretch",
                    "Controls Unity's Humanoid leg stretch limit used by IK retargeting."));
            Draw01Slider(
                feetSpacingProperty,
                new GUIContent(
                    "Feet Spacing",
                    "Controls Unity's Humanoid feet spacing value used by AvatarBuilder."));

            EditorGUILayout.PropertyField(
                hasTranslationDoFProperty,
                new GUIContent(
                    "Has Translation DoF",
                    "Enables translation degrees of freedom on the generated Humanoid Avatar."));

            if (GUILayout.Button("Reset to Defaults"))
            {
                Undo.RecordObject(target, "Reset Retarget Quality Settings");
                ResetRetargetQualitySettings();
                EditorUtility.SetDirty(target);
            }
        }

        private static void Draw01Slider(SerializedProperty property, GUIContent content)
        {
            property.floatValue = EditorGUILayout.Slider(
                content,
                Mathf.Clamp01(property.floatValue),
                0.0f,
                1.0f);
        }

        private void ResetRetargetQualitySettings()
        {
            if (upperArmTwistProperty != null)
            {
                upperArmTwistProperty.floatValue = MmdHumanoidRetargetQualitySettings.DefaultUpperArmTwist;
            }

            if (lowerArmTwistProperty != null)
            {
                lowerArmTwistProperty.floatValue = MmdHumanoidRetargetQualitySettings.DefaultLowerArmTwist;
            }

            if (upperLegTwistProperty != null)
            {
                upperLegTwistProperty.floatValue = MmdHumanoidRetargetQualitySettings.DefaultUpperLegTwist;
            }

            if (lowerLegTwistProperty != null)
            {
                lowerLegTwistProperty.floatValue = MmdHumanoidRetargetQualitySettings.DefaultLowerLegTwist;
            }

            if (armStretchProperty != null)
            {
                armStretchProperty.floatValue = MmdHumanoidRetargetQualitySettings.DefaultArmStretch;
            }

            if (legStretchProperty != null)
            {
                legStretchProperty.floatValue = MmdHumanoidRetargetQualitySettings.DefaultLegStretch;
            }

            if (feetSpacingProperty != null)
            {
                feetSpacingProperty.floatValue = MmdHumanoidRetargetQualitySettings.DefaultFeetSpacing;
            }

            if (hasTranslationDoFProperty != null)
            {
                hasTranslationDoFProperty.boolValue = MmdHumanoidRetargetQualitySettings.DefaultHasTranslationDoF;
            }
        }

        private void DrawHumanoidMappingOverrides(MmdPmxAsset? asset)
        {
            if (humanoidBoneMappingOverridesProperty == null)
            {
                return;
            }

            humanoidMappingOverridesExpanded = EditorGUILayout.Foldout(
                humanoidMappingOverridesExpanded,
                "Manual Bone Mapping Overrides",
                toggleOnLabelClick: true);

            if (!humanoidMappingOverridesExpanded)
            {
                return;
            }

            string[] boneNames = GetImportedBoneNames(asset);
            if (asset == null)
            {
                EditorGUILayout.HelpBox("Apply import settings to refresh MMD bone candidates.", MessageType.Warning);
            }
            else if (boneNames.Length == 0)
            {
                EditorGUILayout.HelpBox("No imported bones are available for manual mapping.", MessageType.Warning);
            }

            for (int i = 0; i < humanoidBoneMappingOverridesProperty.arraySize; i++)
            {
                SerializedProperty entry = humanoidBoneMappingOverridesProperty.GetArrayElementAtIndex(i);
                SerializedProperty? mmdBoneNameProperty = entry.FindPropertyRelative("mmdBoneName");
                SerializedProperty? humanBoneProperty = entry.FindPropertyRelative("humanBone");

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMmdBoneNameField(mmdBoneNameProperty, boneNames);
                    if (humanBoneProperty != null)
                    {
                        EditorGUILayout.PropertyField(humanBoneProperty, GUIContent.none, GUILayout.MinWidth(140));
                    }

                    if (GUILayout.Button("-", GUILayout.Width(24)))
                    {
                        humanoidBoneMappingOverridesProperty.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Override"))
                {
                    AddHumanoidMappingOverride(
                        HumanBodyBones.LastBone,
                        boneNames.Length > 0 ? boneNames[0] : string.Empty,
                        "Add Humanoid Bone Mapping Override");
                }

                if (humanoidBoneMappingOverridesProperty.arraySize > 0)
                {
                    if (GUILayout.Button("Clear Overrides"))
                    {
                        humanoidBoneMappingOverridesProperty.ClearArray();
                    }
                }
            }
        }

        private void AddHumanoidMappingOverride(
            HumanBodyBones humanBone,
            string mmdBoneName,
            string undoName)
        {
            if (humanoidBoneMappingOverridesProperty == null)
            {
                return;
            }

            Undo.RecordObject(target, undoName);

            int index = humanoidBoneMappingOverridesProperty.arraySize;
            humanoidBoneMappingOverridesProperty.InsertArrayElementAtIndex(index);
            SerializedProperty entry = humanoidBoneMappingOverridesProperty.GetArrayElementAtIndex(index);
            SerializedProperty? mmdBoneNameProperty = entry.FindPropertyRelative("mmdBoneName");
            SerializedProperty? humanBoneProperty = entry.FindPropertyRelative("humanBone");

            if (mmdBoneNameProperty != null)
            {
                mmdBoneNameProperty.stringValue = mmdBoneName ?? string.Empty;
            }

            if (humanBoneProperty != null)
            {
                humanBoneProperty.intValue = (int)humanBone;
            }

            humanoidMappingOverridesExpanded = true;
            advancedHumanoidSettingsExpanded = true;
            EditorUtility.SetDirty(target);
            Repaint();
        }

        private static void DrawMmdBoneNameField(SerializedProperty? mmdBoneNameProperty, string[] boneNames)
        {
            if (mmdBoneNameProperty == null)
            {
                return;
            }

            if (boneNames.Length == 0)
            {
                mmdBoneNameProperty.stringValue = EditorGUILayout.TextField(
                    mmdBoneNameProperty.stringValue,
                    GUILayout.MinWidth(180));
                return;
            }

            string current = mmdBoneNameProperty.stringValue ?? string.Empty;
            int selectedIndex = System.Array.IndexOf(boneNames, current);
            string[] options = boneNames;
            if (selectedIndex < 0 && !string.IsNullOrWhiteSpace(current))
            {
                options = new string[boneNames.Length + 1];
                options[0] = current;
                System.Array.Copy(boneNames, 0, options, 1, boneNames.Length);
                selectedIndex = 0;
            }
            else if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            int nextIndex = EditorGUILayout.Popup(selectedIndex, options, GUILayout.MinWidth(180));
            if (nextIndex >= 0 && nextIndex < options.Length)
            {
                mmdBoneNameProperty.stringValue = options[nextIndex];
            }
        }

        private static string[] GetImportedBoneNames(MmdPmxAsset? asset)
        {
            GameObject? importedRoot = asset?.ImportedRoot;
            if (importedRoot == null)
            {
                return System.Array.Empty<string>();
            }

            SkinnedMeshRenderer? smr = importedRoot.GetComponentInChildren<SkinnedMeshRenderer>(
                includeInactive: true);
            if (smr == null || smr.bones == null || smr.bones.Length == 0)
            {
                return System.Array.Empty<string>();
            }

            var names = new List<string>(smr.bones.Length);
            for (int i = 0; i < smr.bones.Length; i++)
            {
                Transform? bone = smr.bones[i];
                if (bone == null || string.IsNullOrWhiteSpace(bone.name))
                {
                    continue;
                }

                names.Add(bone.name);
            }

            return names.ToArray();
        }

        private void DrawRemappedMaterials(MmdPmxAsset asset)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Remapped Materials", EditorStyles.boldLabel);
            onDemandRemapExpanded = EditorGUILayout.Foldout(
                onDemandRemapExpanded,
                new GUIContent(
                    "On Demand Remap",
                    "Materials are embedded inside the imported asset. Override material slots here when a project Material should replace an imported material."),
                toggleOnLabelClick: true);
            if (!onDemandRemapExpanded)
            {
                return;
            }

            MmdPmxMaterialSummary[] materials = asset.MaterialSummaries;
            if (materials == null || materials.Length == 0)
            {
                EditorGUILayout.LabelField("No materials");
                return;
            }

            if (materialRemapsProperty == null)
            {
                return;
            }

            if (materialRemapsProperty.arraySize != materials.Length)
            {
                materialRemapsProperty.arraySize = materials.Length;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                MmdPmxMaterialSummary material = materials[i];
                string materialName = string.IsNullOrWhiteSpace(material.name)
                    ? "Material " + material.index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : material.name;
                SerializedProperty remap = materialRemapsProperty.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(remap, new GUIContent(materialName));
            }
        }

        private static void DrawReadOnlySelectableText(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                EditorGUILayout.SelectableLabel(value, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private static void DrawPendingImportSettingsWarning(bool hasModifiedImportSettings)
        {
            if (!hasModifiedImportSettings)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "Apply pending import setting changes before running asset-dependent actions.",
                MessageType.Warning);
        }

        private MmdPmxAsset? ResolveImportedAsset()
        {
            if (cachedAsset != null)
            {
                return cachedAsset;
            }

            cachedAsset = target is AssetImporter importer
                ? AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(importer.assetPath)
                : null;
            return cachedAsset;
        }
    }
}
