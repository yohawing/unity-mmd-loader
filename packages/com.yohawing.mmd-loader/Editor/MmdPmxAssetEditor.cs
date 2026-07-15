#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Mmd.Parser;
using Mmd.Rendering.Universal;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    [CustomEditor(typeof(MmdPmxAsset))]
    public sealed class MmdPmxAssetEditor : UnityEditor.Editor
    {
        private bool showReadinessDiagnostics;

        public override void OnInspectorGUI()
        {
            if (target is not MmdPmxAsset asset)
            {
                return;
            }

            MmdAssetInspectorUtility.DrawSummary("PMX Asset", asset.SourceId, asset.SourcePath, asset.ByteLength);
            MmdAssetInspectorUtility.DrawImportScaleSummary(asset.ImportScale);
            MmdAssetInspectorUtility.DrawImportSettingsSummary(
                asset.ModelPreset,
                asset.ShaderPreset);
            MmdAssetInspectorUtility.DrawModelSummary(asset);
            MmdAssetInspectorUtility.DrawParseSummary(asset);

            using (new EditorGUI.DisabledScope(asset.ByteLength == 0))
            {
                if (GUILayout.Button("Load PMX Into Scene"))
                {
                    MmdEditorPmxLoader.LoadPmxIntoScene(asset);
                }

                if (GUILayout.Button("Create Prefab from PMX"))
                {
                    MmdPmxPrefabExporter.CreatePrefabWithFeedback(asset);
                }

            }

            DrawMissingTextureActions(asset);

            showReadinessDiagnostics = EditorGUILayout.Foldout(showReadinessDiagnostics, "Readiness Diagnostics", true);
            if (showReadinessDiagnostics)
            {
                MmdAssetInspectorUtility.DrawMaterialSummary(asset);
                MmdAssetInspectorUtility.DrawOutlineSummary(asset);
                MmdAssetInspectorUtility.DrawHierarchyReadinessSummary(asset);
                MmdAssetInspectorUtility.DrawPhysicsSummary(asset);
                MmdAssetInspectorUtility.DrawHumanoidSummary(asset);
                MmdAssetInspectorUtility.DrawAnimationTimelineSummary(asset);
            }
        }

        private static void DrawMissingTextureActions(MmdPmxAsset asset)
        {
            if (asset.MissingProjectTextureReferenceCount <= 0)
            {
                return;
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(asset.ByteLength == 0))
            {
                if (GUILayout.Button("Resolve First Missing Texture..."))
                {
                    string searchRoot = EditorUtility.OpenFolderPanel(
                        "Search Missing PMX Texture",
                        Application.dataPath,
                        string.Empty);
                    if (string.IsNullOrWhiteSpace(searchRoot))
                    {
                        return;
                    }

                    MmdPmxMissingTextureResolveResult result =
                        MmdPmxMissingTextureResolver.ResolveFirstMissingTextureReference(asset, searchRoot);
                    if (result.Success)
                    {
                        EditorUtility.DisplayDialog(
                            "Missing Texture Resolved",
                            $"Copied {result.Reference} to {result.TargetAssetPath}. PMX reimport was requested.",
                            "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Missing Texture Not Resolved", result.Message, "OK");
                    }
                }
            }
        }
    }

}
