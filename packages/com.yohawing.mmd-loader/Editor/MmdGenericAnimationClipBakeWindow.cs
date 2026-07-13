#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mmd.Editor
{
    public sealed class MmdGenericAnimationClipBakeWindow : EditorWindow
    {
        private MmdPmxAsset? pmxAsset;
        private MmdVmdAsset? vmdAsset;
        private int startFrame;
        private int endFrame;
        private float frameRate = 30.0f;
        private string outputPath = string.Empty;
        private readonly List<string> diagnostics = new();

        public static MmdGenericAnimationClipBakeWindow Open(MmdPmxAsset? pmxAsset)
        {
            MmdGenericAnimationClipBakeWindow window = GetWindow<MmdGenericAnimationClipBakeWindow>();
            window.titleContent = new GUIContent("Generic Clip Bake");
            window.minSize = new Vector2(420.0f, 210.0f);
            window.pmxAsset = pmxAsset;
            window.vmdAsset = null;
            window.startFrame = 0;
            window.endFrame = 0;
            window.frameRate = 30.0f;
            window.diagnostics.Clear();
            window.RefreshDefaultOutputPath();
            window.Show();
            return window;
        }

        internal MmdPmxAsset? PmxAssetForTests => pmxAsset;
        internal MmdVmdAsset? VmdAssetForTests => vmdAsset;
        internal int EndFrameForTests => endFrame;
        internal string OutputPathForTests => outputPath;

        internal void SetVmdAssetForTests(MmdVmdAsset? asset)
        {
            SetVmdAsset(asset);
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            MmdPmxAsset? nextPmx = (MmdPmxAsset?)EditorGUILayout.ObjectField("PMX", pmxAsset, typeof(MmdPmxAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                pmxAsset = nextPmx;
                RefreshDefaultOutputPath();
            }

            EditorGUI.BeginChangeCheck();
            MmdVmdAsset? nextVmd = (MmdVmdAsset?)EditorGUILayout.ObjectField("VMD", vmdAsset, typeof(MmdVmdAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                SetVmdAsset(nextVmd);
            }

            startFrame = EditorGUILayout.IntField("Start Frame", startFrame);
            endFrame = EditorGUILayout.IntField("End Frame", endFrame);
            frameRate = EditorGUILayout.FloatField("Frame Rate", frameRate);
            outputPath = EditorGUILayout.TextField("Output Path", outputPath);

            using (new EditorGUI.DisabledScope(pmxAsset == null || vmdAsset == null))
            {
                if (GUILayout.Button("Create"))
                {
                    CreateClip();
                }
            }

            foreach (string diagnostic in diagnostics)
            {
                EditorGUILayout.HelpBox(diagnostic, MessageType.Error);
            }
        }

        private void SetVmdAsset(MmdVmdAsset? asset)
        {
            vmdAsset = asset;
            diagnostics.Clear();
            if (asset != null)
            {
                try
                {
                    endFrame = asset.MaxFrame;
                }
                catch (Exception ex)
                {
                    diagnostics.Add("VMD metadata could not be read: " + ex.Message);
                }
            }

            RefreshDefaultOutputPath();
            Repaint();
        }

        private void CreateClip()
        {
            diagnostics.Clear();
            MmdGenericAnimationClipWriterResult result = MmdGenericAnimationClipWriter.CreateAnimationClipAsset(
                pmxAsset!, vmdAsset!, frameRate, startFrame, endFrame, outputPath);
            if (result.Clip != null)
            {
                Selection.activeObject = result.Clip;
                EditorGUIUtility.PingObject(result.Clip);
                return;
            }

            diagnostics.AddRange(result.Diagnostics);
            if (diagnostics.Count == 0)
            {
                diagnostics.Add("Generic AnimationClip could not be created.");
            }
        }

        private void RefreshDefaultOutputPath()
        {
            outputPath = MmdGenericAnimationClipWriter.GetDefaultOutputPath(pmxAsset, vmdAsset);
        }
    }
}
