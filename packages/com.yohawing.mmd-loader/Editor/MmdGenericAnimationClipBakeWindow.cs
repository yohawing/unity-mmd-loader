#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mmd.Editor
{
    public sealed class MmdGenericAnimationClipBakeWindow : EditorWindow
    {
        public enum ClipType
        {
            Generic,
            Humanoid,
        }

        private UnityEngine.Object? pmxDisplayObject;
        private MmdPmxAsset? pmxAsset;
        private MmdVmdAsset? vmdAsset;
        private ClipType clipType;
        private int startFrame;
        private int endFrame;
        private float frameRate = 30.0f;
        private string outputPath = string.Empty;
        private string? humanoidPlannerError;
        private readonly List<string> diagnostics = new();

        public static MmdGenericAnimationClipBakeWindow Open(MmdPmxAsset? pmxAsset)
        {
            return OpenFromPmx(pmxAsset, preferHumanoid: false);
        }

        public static MmdGenericAnimationClipBakeWindow OpenFromPmx(
            MmdPmxAsset? pmxAsset,
            bool preferHumanoid = false)
        {
            MmdGenericAnimationClipBakeWindow window = GetWindow<MmdGenericAnimationClipBakeWindow>();
            window.titleContent = new GUIContent("AnimationClip Bake");
            window.minSize = new Vector2(440.0f, 260.0f);
            window.pmxAsset = pmxAsset;
            window.pmxDisplayObject = GetPmxDisplayObject(pmxAsset, pmxAsset);
            window.vmdAsset = null;
            window.clipType = preferHumanoid ? ClipType.Humanoid : ClipType.Generic;
            window.startFrame = 0;
            window.endFrame = 0;
            window.frameRate = 30.0f;
            window.diagnostics.Clear();
            window.RefreshVmdMetadataAndOutputPath();
            window.Show();
            return window;
        }

        public static MmdGenericAnimationClipBakeWindow OpenFromVmd(MmdVmdAsset? vmdAsset)
        {
            MmdGenericAnimationClipBakeWindow window = GetWindow<MmdGenericAnimationClipBakeWindow>();
            window.titleContent = new GUIContent("AnimationClip Bake");
            window.minSize = new Vector2(440.0f, 260.0f);
            window.pmxAsset = null;
            window.pmxDisplayObject = null;
            window.vmdAsset = vmdAsset;
            window.clipType = ClipType.Generic;
            window.startFrame = 0;
            window.endFrame = 0;
            window.frameRate = 30.0f;
            window.diagnostics.Clear();
            window.RefreshVmdMetadataAndOutputPath();
            window.Show();
            return window;
        }

        internal MmdPmxAsset? PmxAssetForTests => pmxAsset;
        internal UnityEngine.Object? PmxDisplayObjectForTests => pmxDisplayObject;
        internal MmdVmdAsset? VmdAssetForTests => vmdAsset;
        internal ClipType ClipTypeForTests => clipType;
        internal int EndFrameForTests => endFrame;
        internal int MaxFrameForTests => vmdAsset?.MaxFrame ?? 0;
        internal string OutputPathForTests => outputPath;

        internal void SetPmxAssetForTests(MmdPmxAsset? asset)
        {
            SetPmxAsset(asset);
        }

        internal void SetPmxDisplayObjectForTests(UnityEngine.Object? asset)
        {
            SetPmxDisplayObject(asset);
        }

        internal void SetVmdAssetForTests(MmdVmdAsset? asset)
        {
            SetVmdAsset(asset);
        }

        internal void SetClipTypeForTests(ClipType type)
        {
            clipType = type;
            diagnostics.Clear();
            RefreshDefaultOutputPath();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            ClipType nextType = (ClipType)EditorGUILayout.EnumPopup("Clip Type", clipType);
            if (EditorGUI.EndChangeCheck())
            {
                SetClipType(nextType);
            }

            EditorGUI.BeginChangeCheck();
            UnityEngine.Object? nextPmxDisplayObject = EditorGUILayout.ObjectField(
                new GUIContent("PMX Model", "Select the visible main GameObject from an imported .pmx asset."),
                pmxDisplayObject,
                typeof(UnityEngine.Object),
                allowSceneObjects: false);
            if (EditorGUI.EndChangeCheck())
            {
                SetPmxDisplayObject(nextPmxDisplayObject);
            }

            if (pmxDisplayObject != null && pmxAsset == null)
            {
                EditorGUILayout.HelpBox("Select an imported .pmx model asset.", MessageType.Warning);
            }

            EditorGUI.BeginChangeCheck();
            MmdVmdAsset? nextVmd = (MmdVmdAsset?)EditorGUILayout.ObjectField(
                "VMD",
                vmdAsset,
                typeof(MmdVmdAsset),
                allowSceneObjects: false);
            if (EditorGUI.EndChangeCheck())
            {
                SetVmdAsset(nextVmd);
            }

            startFrame = EditorGUILayout.IntField("Start Frame", startFrame);
            endFrame = EditorGUILayout.IntField("End Frame", endFrame);
            frameRate = EditorGUILayout.FloatField("Frame Rate", frameRate);
            outputPath = EditorGUILayout.TextField("Output Path", outputPath);

            bool canCreate = pmxAsset != null && vmdAsset != null;
            MmdHumanoidClipConversionPlan? plan = null;
            if (clipType == ClipType.Humanoid)
            {
                plan = AnalyzeHumanoidPrerequisites();
                DrawHumanoidReadiness(plan);
                canCreate = canCreate && plan != null && plan.CanCreateClipNow;
            }

            if (!string.IsNullOrEmpty(humanoidPlannerError))
            {
                EditorGUILayout.HelpBox(humanoidPlannerError, MessageType.Error);
            }

            using (new EditorGUI.DisabledScope(!canCreate))
            {
                if (GUILayout.Button("Bake to AnimationClip"))
                {
                    CreateClip();
                }
            }

            foreach (string diagnostic in diagnostics)
            {
                EditorGUILayout.HelpBox(diagnostic, MessageType.Error);
            }
        }

        private void SetClipType(ClipType type)
        {
            clipType = type;
            diagnostics.Clear();
            RefreshDefaultOutputPath();
            Repaint();
        }

        private void SetPmxAsset(MmdPmxAsset? asset)
        {
            pmxAsset = asset;
            pmxDisplayObject = GetPmxDisplayObject(asset, asset);
            diagnostics.Clear();
            RefreshDefaultOutputPath();
            Repaint();
        }

        private void SetPmxDisplayObject(UnityEngine.Object? asset)
        {
            MmdPmxAsset? resolvedAsset = asset as MmdPmxAsset
                ?? MmdEditorPmxLoader.TryResolveMmdPmxAssetFromMainGameObject(asset);
            pmxAsset = resolvedAsset;
            pmxDisplayObject = GetPmxDisplayObject(resolvedAsset, asset);
            diagnostics.Clear();
            RefreshDefaultOutputPath();
            Repaint();
        }

        private static UnityEngine.Object? GetPmxDisplayObject(
            MmdPmxAsset? asset,
            UnityEngine.Object? fallback)
        {
            if (asset == null)
            {
                return fallback;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path)
                && path.EndsWith(".pmx", StringComparison.OrdinalIgnoreCase))
            {
                UnityEngine.Object? mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                if (mainAsset is GameObject)
                {
                    return mainAsset;
                }
            }

            return fallback ?? asset;
        }

        private void SetVmdAsset(MmdVmdAsset? asset)
        {
            vmdAsset = asset;
            diagnostics.Clear();
            endFrame = asset?.MaxFrame ?? 0;
            RefreshDefaultOutputPath();
            Repaint();
        }

        private static void DrawHumanoidReadiness(MmdHumanoidClipConversionPlan? plan)
        {
            if (plan == null)
            {
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Planner Readiness", plan.Readiness);
            }

            if (!plan.PrerequisitesReady)
            {
                string compact = MmdAssetInspectorUtility.FormatCompactHumanoidClipConversionIssues(plan);
                if (!string.IsNullOrEmpty(compact))
                {
                    EditorGUILayout.HelpBox(compact, MessageType.Info);
                }
            }
        }

        private MmdHumanoidClipConversionPlan? AnalyzeHumanoidPrerequisites()
        {
            humanoidPlannerError = null;
            try
            {
                return MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(
                    pmxAsset,
                    vmdAsset);
            }
            catch (Exception ex)
            {
                humanoidPlannerError = "Humanoid planner failed: " + ex.Message;
                return null;
            }
        }

        private void CreateClip()
        {
            diagnostics.Clear();
            if (pmxAsset == null || vmdAsset == null)
            {
                diagnostics.Add("PMX and VMD assets are required.");
                return;
            }

            if (clipType == ClipType.Generic)
            {
                MmdGenericAnimationClipWriterResult result = MmdGenericAnimationClipWriter.CreateAnimationClipAsset(
                    pmxAsset,
                    vmdAsset,
                    frameRate,
                    startFrame,
                    endFrame,
                    outputPath);
                if (result.Clip != null)
                {
                    SelectCreatedClip(result.Clip);
                    return;
                }

                diagnostics.AddRange(result.Diagnostics);
                if (diagnostics.Count == 0)
                {
                    diagnostics.Add("Generic AnimationClip could not be created.");
                }

                return;
            }

            MmdHumanoidClipConversionWriterResult humanoidResult =
                MmdHumanoidClipConversionWriter.CreateHumanoidAnimationClipAsset(
                    pmxAsset,
                    vmdAsset,
                    frameRate,
                    startFrame,
                    endFrame,
                    outputPath);
            if (humanoidResult.Clip != null)
            {
                SelectCreatedClip(humanoidResult.Clip);
                return;
            }

            diagnostics.AddRange(humanoidResult.Diagnostics);
            if (diagnostics.Count == 0)
            {
                diagnostics.Add("Humanoid AnimationClip could not be created.");
            }
        }

        private static void SelectCreatedClip(AnimationClip clip)
        {
            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);
        }

        private void RefreshVmdMetadataAndOutputPath()
        {
            endFrame = vmdAsset?.MaxFrame ?? 0;
            RefreshDefaultOutputPath();
        }

        private void RefreshDefaultOutputPath()
        {
            outputPath = clipType == ClipType.Generic
                ? MmdGenericAnimationClipWriter.GetDefaultOutputPath(pmxAsset, vmdAsset)
                : MmdHumanoidClipConversionWriter.GetDefaultOutputPath(pmxAsset, vmdAsset);
        }
    }
}
