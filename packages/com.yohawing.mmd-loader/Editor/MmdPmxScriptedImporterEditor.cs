#nullable enable

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
        private MmdPmxAsset? cachedAsset;
        private bool onDemandRemapExpanded = true;

        private bool toonShaderSettingsExpanded = true;

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
            if (currentType == MmdPmxAnimationType.Humanoid && asset != null)
            {
                EditorGUILayout.Space();
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
            }
            else if (asset == null)
            {
                EditorGUILayout.HelpBox("Apply import settings to refresh rig data.", MessageType.Warning);
            }

            DrawPendingImportSettingsWarning(HasModified());
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
