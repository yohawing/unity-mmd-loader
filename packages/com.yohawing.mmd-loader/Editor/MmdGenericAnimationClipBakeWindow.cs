#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
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

        private MmdPmxAsset? pmxAsset;
        private MmdVmdAsset? vmdAsset;
        private ClipType clipType;
        private int startFrame;
        private int endFrame;
        private float frameRate = 30.0f;
        private string outputPath = string.Empty;
        private bool reduceKeys = true;
        private bool highPrecision;
        private string? humanoidPlannerError;
        private readonly List<string> diagnostics = new();

        [Obsolete("Use OpenFromVmd so AnimationClip baking starts from the motion asset.")]
        public static MmdGenericAnimationClipBakeWindow Open(MmdPmxAsset? pmxAsset)
        {
            return OpenFromPmx(pmxAsset, preferHumanoid: false);
        }

        [Obsolete("Use OpenFromVmd so AnimationClip baking starts from the motion asset.")]
        public static MmdGenericAnimationClipBakeWindow OpenFromPmx(
            MmdPmxAsset? pmxAsset,
            bool preferHumanoid = false)
        {
            MmdGenericAnimationClipBakeWindow window = GetWindow<MmdGenericAnimationClipBakeWindow>();
            window.titleContent = new GUIContent("AnimationClip Bake");
            window.minSize = new Vector2(440.0f, 300.0f);
            window.pmxAsset = pmxAsset;
            window.vmdAsset = null;
            window.clipType = preferHumanoid ? ClipType.Humanoid : ClipType.Generic;
            window.startFrame = 0;
            window.endFrame = 0;
            window.frameRate = 30.0f;
            window.reduceKeys = true;
            window.highPrecision = false;
            window.diagnostics.Clear();
            window.RefreshVmdMetadataAndOutputPath();
            window.Show();
            return window;
        }

        public static MmdGenericAnimationClipBakeWindow OpenFromVmd(MmdVmdAsset? vmdAsset)
        {
            MmdGenericAnimationClipBakeWindow window = GetWindow<MmdGenericAnimationClipBakeWindow>();
            window.titleContent = new GUIContent("AnimationClip Bake");
            window.minSize = new Vector2(440.0f, 300.0f);
            window.pmxAsset = null;
            window.vmdAsset = vmdAsset;
            window.clipType = ClipType.Generic;
            window.startFrame = 0;
            window.endFrame = 0;
            window.frameRate = 30.0f;
            window.reduceKeys = true;
            window.highPrecision = false;
            window.diagnostics.Clear();
            window.RefreshVmdMetadataAndOutputPath();
            window.Show();
            return window;
        }

        internal MmdPmxAsset? PmxAssetForTests => pmxAsset;
        internal MmdVmdAsset? VmdAssetForTests => vmdAsset;
        internal ClipType ClipTypeForTests => clipType;
        internal int StartFrameForTests => startFrame;
        internal int EndFrameForTests => endFrame;
        internal float FrameRateForTests => frameRate;
        internal int MaxFrameForTests => vmdAsset?.MaxFrame ?? 0;
        internal string OutputPathForTests => outputPath;
        internal bool ReduceKeysForTests => reduceKeys;
        internal bool HighPrecisionForTests => highPrecision;

        internal void SetPmxAssetForTests(MmdPmxAsset? asset)
        {
            SetPmxAsset(asset);
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
            MmdPmxAsset? nextPmx = (MmdPmxAsset?)EditorGUILayout.ObjectField(
                new GUIContent("PMX Model", "Select an imported MmdPmxAsset."),
                pmxAsset,
                typeof(MmdPmxAsset),
                allowSceneObjects: false);
            if (EditorGUI.EndChangeCheck())
            {
                SetPmxAsset(nextPmx);
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

            DrawFrameRangeField();
            frameRate = EditorGUILayout.IntPopup(
                "Frame Rate",
                Mathf.RoundToInt(frameRate),
                new[] { "30 fps", "60 fps" },
                new[] { 30, 60 });

            if (clipType == ClipType.Generic)
            {
                reduceKeys = EditorGUILayout.Toggle(
                    new GUIContent("Reduce Keys", "Use mmd-anim sparse curve reduction. Disable to retain every sampled frame."),
                    reduceKeys);
                using (new EditorGUI.DisabledScope(!reduceKeys))
                {
                    highPrecision = EditorGUILayout.Toggle(
                        new GUIContent(
                            "High Precision",
                            "Use a tighter 1 mm Unity-space position tolerance. This increases bake time and output size."),
                        highPrecision);
                }
            }

            DrawOutputPathField();

            bool canCreate = pmxAsset != null && vmdAsset != null;
            MmdHumanoidClipConversionPlan? plan = null;
            if (clipType == ClipType.Humanoid)
            {
                plan = AnalyzeHumanoidPrerequisites();
                DrawHumanoidReadiness(plan);
                DrawHumanoidDenseCurveWarning();
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
            diagnostics.Clear();
            RefreshDefaultOutputPath();
            Repaint();
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

        private void DrawHumanoidDenseCurveWarning()
        {
            int frameCount = Math.Max(0, endFrame - startFrame + 1);
            long estimatedKeyCount = (long)frameCount * (HumanTrait.MuscleCount + 7);
            if (estimatedKeyCount < 500_000)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "Humanoid key reduction is not available yet. This range writes about "
                + estimatedKeyCount.ToString("N0")
                + " dense keys and may create a very large .anim asset. Narrow Frame Range or use Generic.",
                MessageType.Warning);
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
                    outputPath,
                    new MmdGenericAnimationClipBakeOptions(reduceKeys, highPrecision));
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

        private void DrawOutputPathField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Output", "Project-relative .anim asset path."));
                outputPath = EditorGUILayout.TextField(outputPath);
                if (GUILayout.Button("...", GUILayout.Width(30.0f)))
                {
                    string defaultName = Path.GetFileNameWithoutExtension(outputPath);
                    if (string.IsNullOrWhiteSpace(defaultName))
                    {
                        defaultName = "PMX_VMD";
                    }

                    string directory = Path.GetDirectoryName(outputPath)?.Replace('\\', '/') ?? "Assets";
                    string selected = EditorUtility.SaveFilePanelInProject(
                        "Bake to AnimationClip",
                        defaultName,
                        "anim",
                        "Choose where to save the baked AnimationClip.",
                        directory);
                    if (!string.IsNullOrEmpty(selected))
                    {
                        outputPath = selected;
                    }
                }
            }
        }

        private void DrawFrameRangeField()
        {
            int maxFrame = Math.Max(0, vmdAsset?.MaxFrame ?? 0);
            float minValue = Mathf.Clamp(startFrame, 0, maxFrame);
            float maxValue = Mathf.Clamp(endFrame, minValue, maxFrame);

            using (new EditorGUI.DisabledScope(vmdAsset == null))
            {
                if (maxFrame > 0)
                {
                    EditorGUILayout.MinMaxSlider(
                        new GUIContent("Frame Range", "Limit the bake to a frame range within the VMD motion."),
                        ref minValue,
                        ref maxValue,
                        0.0f,
                        maxFrame);
                }
                else
                {
                    EditorGUILayout.LabelField("Frame Range", "0");
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    int indent = EditorGUI.indentLevel;
                    EditorGUI.indentLevel = indent + 1;
                    int nextStart = EditorGUILayout.IntField("Start", Mathf.RoundToInt(minValue));
                    int nextEnd = EditorGUILayout.IntField("End", Mathf.RoundToInt(maxValue));
                    EditorGUI.indentLevel = indent;
                    startFrame = Mathf.Clamp(nextStart, 0, maxFrame);
                    endFrame = Mathf.Clamp(nextEnd, startFrame, maxFrame);
                }
            }
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
