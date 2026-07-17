#nullable enable

using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Mmd.Editor
{
    [CustomEditor(typeof(MmdVmdScriptedImporter))]
    public sealed class MmdVmdScriptedImporterEditor : ScriptedImporterEditor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            MmdVmdAsset? asset = ResolveImportedAsset();
            DrawImportIssues(asset);
            using (new EditorGUI.DisabledScope(asset == null || asset.ByteLength <= 0 || HasModified()))
            {
                if (GUILayout.Button("Bake to AnimationClip..."))
                {
                    MmdGenericAnimationClipBakeWindow.OpenFromVmd(asset);
                }
            }

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }

        private static void DrawImportIssues(MmdVmdAsset? asset)
        {
            if (asset == null)
            {
                return;
            }

            if (asset.ByteLength <= 0)
            {
                EditorGUILayout.HelpBox(
                    "VMD could not be imported. Reimport the source file and check the Console.",
                    MessageType.Error);
                return;
            }

            var diagnostics = asset.StructuralDiagnostics;
            for (int i = 0; i < diagnostics.Count; i++)
            {
                EditorGUILayout.HelpBox(diagnostics[i], MessageType.Warning);
            }
        }

        private MmdVmdAsset? ResolveImportedAsset()
        {
            if (target is not MmdVmdScriptedImporter importer || string.IsNullOrWhiteSpace(importer.assetPath))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(importer.assetPath);
        }
    }
}
