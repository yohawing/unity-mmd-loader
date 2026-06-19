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
        private SerializedProperty? meshGenerationModeProperty;
        private SerializedProperty? materialTexturePolicyProperty;
        private SerializedProperty? shaderPresetProperty;
        private SerializedProperty? materialRemapsProperty;
        private SerializedProperty? animationTypeProperty;
        private SerializedProperty? humanoidBoneMappingOverridesProperty;
        private MmdPmxAsset? cachedAsset;
        private bool onDemandRemapExpanded = true;

        private bool toonShaderSettingsExpanded = true;
        private bool humanoidMappingOverridesExpanded;
        private bool humanoidMappingDiagnosticsExpanded = true;

        private int selectedTab;
        private static readonly string[] TabNames = { "Model", "Rig", "Materials" };

        public override void OnEnable()
        {
            base.OnEnable();
            importScaleProperty = serializedObject.FindProperty("importScale");
            modelPresetProperty = serializedObject.FindProperty("modelPreset");
            meshGenerationModeProperty = serializedObject.FindProperty("meshGenerationMode");
            materialTexturePolicyProperty = serializedObject.FindProperty("materialTexturePolicy");
            shaderPresetProperty = serializedObject.FindProperty("shaderPreset");
            materialRemapsProperty = serializedObject.FindProperty("materialRemaps");
            animationTypeProperty = serializedObject.FindProperty("animationType");
            humanoidBoneMappingOverridesProperty = serializedObject.FindProperty("humanoidBoneMappingOverrides");
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
                        "Scale applied to the imported PMX model. Stored as an import setting and asset summary. Scale-aware runtime instantiation is not applied yet."));
            }

            if (meshGenerationModeProperty != null)
            {
                EditorGUILayout.PropertyField(meshGenerationModeProperty,
                    new GUIContent("Mesh Generation",
                        "Mesh generation mode stored as an import setting. Normal PMX import still does not write generated Mesh assets; persistent split mesh export is available only through the explicit export workflow."));
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
                EditorGUILayout.HelpBox(
                    "Materials are embedded inside the imported asset. Material assignments can be remapped below.",
                    MessageType.Info);
                DrawRemappedMaterials(asset);
            }
            else
            {
                EditorGUILayout.HelpBox("Apply import settings to refresh material data.", MessageType.Warning);
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

            // When Humanoid, show read-only Avatar import status.
            if (currentType == MmdPmxAnimationType.Humanoid)
            {
                DrawHumanoidMappingOverrides(asset);
                EditorGUILayout.Space();
                if (asset != null)
                {
                    EditorGUILayout.LabelField("Humanoid Avatar", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField("Avatar Readiness", asset.HumanoidAvatarReadiness);
                        EditorGUILayout.ObjectField("Avatar", asset.ImportedAvatar, typeof(Avatar), allowSceneObjects: false);
                    }

                    if (asset.ImportedAvatar != null)
                    {
                        EditorGUILayout.HelpBox(
                            "Avatar sub-asset is present. Reimport to rebuild it after bone changes.",
                            MessageType.Info);
                    }
                    else if (asset.BoneCount == 0)
                    {
                        EditorGUILayout.HelpBox(
                            "No bones found. Avatar cannot be created for this model.",
                            MessageType.Warning);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(
                            string.IsNullOrWhiteSpace(asset.HumanoidAvatarDiagnostic)
                                ? "Avatar sub-asset is not built yet. Apply/Reimport to generate it."
                                : asset.HumanoidAvatarDiagnostic,
                            MessageType.Info);
                    }

                    DrawHumanoidMappingDiagnostics(asset);
                }
            }
            else if (asset == null)
            {
                EditorGUILayout.HelpBox("Apply import settings to refresh rig data.", MessageType.Warning);
            }

            DrawPendingImportSettingsWarning(HasModified());
        }

        private void DrawHumanoidMappingDiagnostics(MmdPmxAsset asset)
        {
            EditorGUILayout.Space();
            humanoidMappingDiagnosticsExpanded = EditorGUILayout.Foldout(
                humanoidMappingDiagnosticsExpanded,
                "Humanoid Bone Mapping Diagnostics",
                toggleOnLabelClick: true);
            if (!humanoidMappingDiagnosticsExpanded)
            {
                return;
            }

            MmdHumanoidBoneMappingDiagnosticSummary summary = asset.HumanoidBoneMappingDiagnostics;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Readiness", summary.Readiness);
            }

            DrawMissingRequiredBones(summary);
            DrawMappedHumanoidBones(summary);
            DrawConflictDiagnostics(summary);
        }

        private static void DrawMissingRequiredBones(MmdHumanoidBoneMappingDiagnosticSummary summary)
        {
            if (summary.MissingRequiredBones.Count == 0)
            {
                EditorGUILayout.HelpBox("All required Humanoid bones are mapped.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "Missing required Humanoid bones prevent a valid Avatar. Add manual overrides for these targets.",
                MessageType.Warning);
            foreach (MmdHumanoidMissingRequiredBone missing in summary.MissingRequiredBones)
            {
                EditorGUILayout.LabelField(
                    missing.HumanBone.ToString(),
                    string.IsNullOrWhiteSpace(missing.ExpectedMmdBoneName)
                        ? "(expected MMD bone unknown)"
                        : "Expected MMD bone: " + missing.ExpectedMmdBoneName,
                    EditorStyles.boldLabel);
            }
        }

        private static void DrawMappedHumanoidBones(MmdHumanoidBoneMappingDiagnosticSummary summary)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mapped Bones", EditorStyles.boldLabel);
            if (summary.MappedEntries.Count == 0)
            {
                EditorGUILayout.LabelField("No mapped Humanoid bones.");
                return;
            }

            foreach (MmdHumanoidBoneMappingDiagnosticEntry entry in summary.MappedEntries)
            {
                string mmdName = string.IsNullOrWhiteSpace(entry.MmdBoneName)
                    ? "(unnamed MMD bone)"
                    : entry.MmdBoneName + "#" + entry.MmdBoneIndex;
                string source = string.IsNullOrWhiteSpace(entry.Source) ? "Automatic" : entry.Source;
                string suffix = entry.Required ? "required" : "optional";
                DrawReadOnlySelectableText(
                    entry.HumanBone.ToString(),
                    mmdName + "  [" + source + ", " + suffix + "]");
            }
        }

        private static void DrawConflictDiagnostics(MmdHumanoidBoneMappingDiagnosticSummary summary)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Conflicts / Suppressed Overrides", EditorStyles.boldLabel);
            if (summary.ConflictDiagnostics.Count == 0)
            {
                EditorGUILayout.LabelField("No conflicts or suppressed override entries.");
                return;
            }

            foreach (string diagnostic in summary.ConflictDiagnostics)
            {
                EditorGUILayout.HelpBox(diagnostic, MessageType.Warning);
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
                    int index = humanoidBoneMappingOverridesProperty.arraySize;
                    humanoidBoneMappingOverridesProperty.InsertArrayElementAtIndex(index);
                    SerializedProperty entry = humanoidBoneMappingOverridesProperty.GetArrayElementAtIndex(index);
                    SerializedProperty? mmdBoneNameProperty = entry.FindPropertyRelative("mmdBoneName");
                    SerializedProperty? humanBoneProperty = entry.FindPropertyRelative("humanBone");
                    if (mmdBoneNameProperty != null)
                    {
                        mmdBoneNameProperty.stringValue = boneNames.Length > 0 ? boneNames[0] : string.Empty;
                    }

                    if (humanBoneProperty != null)
                    {
                        humanBoneProperty.intValue = (int)HumanBodyBones.LastBone;
                    }
                }

                using (new EditorGUI.DisabledScope(humanoidBoneMappingOverridesProperty.arraySize == 0))
                {
                    if (GUILayout.Button("Clear Overrides"))
                    {
                        humanoidBoneMappingOverridesProperty.ClearArray();
                    }
                }
            }
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
            if (asset == null || asset.ImportedRoot == null)
            {
                return System.Array.Empty<string>();
            }

            SkinnedMeshRenderer? smr = asset.ImportedRoot.GetComponentInChildren<SkinnedMeshRenderer>(
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
            onDemandRemapExpanded = EditorGUILayout.Foldout(onDemandRemapExpanded, "On Demand Remap", toggleOnLabelClick: true);
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
