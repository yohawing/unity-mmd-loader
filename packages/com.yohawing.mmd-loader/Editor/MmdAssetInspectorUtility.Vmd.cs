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
    internal static partial class MmdAssetInspectorUtility
    {
        public static void DrawVmdMotionSummary(MmdVmdAsset asset)
        {
            EditorGUILayout.LabelField("VMD Motion Summary", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                try
                {
                    MmdVmdMotionSummary summary = GetVmdMotionSummary(asset);
                    EditorGUILayout.TextField("Target Model Name", summary.TargetModelName);
                    EditorGUILayout.IntField("Max Frame", summary.MaxFrame);
                    EditorGUILayout.IntField("Bone Keyframes", summary.BoneKeyframeCount);
                    EditorGUILayout.IntField("Morph Keyframes", summary.MorphKeyframeCount);
                    EditorGUILayout.IntField("Model Keyframes", summary.ModelKeyframeCount);
                    EditorGUILayout.IntField("Constraint States", summary.ConstraintStateCount);
                    EditorGUILayout.IntField("Camera Keyframes", summary.CameraKeyframeCount);
                    EditorGUILayout.IntField("Light Keyframes", summary.LightKeyframeCount);
                    EditorGUILayout.IntField("Self-Shadow Keyframes", summary.SelfShadowKeyframeCount);
                }
                catch (System.Exception ex)
                {
                    EditorGUILayout.HelpBox(
                        "Failed to get VMD motion summary: " + ex.Message,
                        MessageType.Error);
                }
            }
        }

        internal static MmdVmdMotionSummary GetVmdMotionSummary(MmdVmdAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (asset.ByteLength == 0)
            {
                throw new InvalidOperationException("VMD asset has no imported bytes.");
            }

            // Read exclusively from import-time cached summary. Never call LoadMotion()
            // here: selection or summary display must not trigger full VMD parse.
            return new MmdVmdMotionSummary(
                asset.TargetModelName,
                asset.MaxFrame,
                asset.BoneKeyframeCount,
                asset.MorphKeyframeCount,
                asset.ModelKeyframeCount,
                asset.ConstraintStateCount,
                asset.CameraKeyframeCount,
                asset.LightKeyframeCount,
                asset.SelfShadowKeyframeCount);
        }

        public static void DrawVmdStructuralDiagnostics(IReadOnlyList<string>? diagnostics)
        {
            EditorGUILayout.LabelField("Structural Validation", EditorStyles.boldLabel);
            if (diagnostics == null)
            {
                EditorGUILayout.HelpBox(
                    "Run VMD Diagnostics to check structural motion validity.",
                    MessageType.Info);
                return;
            }

            if (diagnostics.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No structural issues found. VMD motion is valid.",
                    MessageType.Info);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; i < diagnostics.Count; i++)
                {
                    EditorGUILayout.TextField("Issue " + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), diagnostics[i]);
                }
            }

            EditorGUILayout.HelpBox(
                diagnostics.Count == 1
                    ? "1 structural issue found."
                    : diagnostics.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " structural issues found.",
                MessageType.Warning);
        }

        internal static IReadOnlyList<string> GetVmdStructuralDiagnostics(MmdVmdAsset asset)
        {
            // Explicit revalidation path (calls LoadMotion + Validate). Intended for "Run VMD Diagnostics"
            // and focused tests that deliberately exercise full parse. Selection / summary display
            // paths must not invoke this.
            MmdMotionDefinition motion = asset.LoadMotion();
            return MmdMotionValidator.ValidateStructuralMotion(motion);
        }

        public static void DrawVmdTimelineReadiness(MmdVmdAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            MmdVmdTimelineReadiness readiness = GetVmdTimelineReadiness(asset);
            EditorGUILayout.LabelField("Timeline Readiness", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Max Frame (Duration)", readiness.MaxFrame);
                EditorGUILayout.TextField("Scene Motion", readiness.SceneMotionStatus);
                EditorGUILayout.TextField("Self-Shadow Motion", readiness.SelfShadowSceneMotionStatus);
                EditorGUILayout.TextField("Clip Creation", readiness.ClipCreationRequirement);
            }
            // Compact diagnostic rows only. No long normal-state HelpBox per UI contract.
            // Raw camera/light/self-shadow counts are already shown in VMD Motion Summary above.
        }

        internal static MmdVmdTimelineReadiness GetVmdTimelineReadiness(MmdVmdAsset? asset)
        {
            if (asset == null || asset.ByteLength <= 0)
            {
                return new MmdVmdTimelineReadiness(
                    maxFrame: 0,
                    cameraKeyframeCount: 0,
                    lightKeyframeCount: 0,
                    selfShadowKeyframeCount: 0,
                    clipDurationSource: "Unavailable",
                    sceneMotionStatus: "No VMD asset",
                    selfShadowSceneMotionStatus: "No VMD asset",
                    clipCreationRequirement: "PMX model source and Timeline are required for VMD Clip creation.");
            }

            // Read EXCLUSIVELY from import-time cached summary on MmdVmdAsset.
            // Never call LoadMotion(), parser, or any full parse path here.
            // This guarantees Inspector selection / readiness display does not parse bytes.
            int cam = asset.CameraKeyframeCount;
            int lit = asset.LightKeyframeCount;
            int shd = asset.SelfShadowKeyframeCount;
            int mf = asset.MaxFrame;

            string sceneStatus = (cam > 0 || lit > 0)
                ? string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Camera/light scene motion present (camera:{0}, light:{1})", cam, lit)
                : "Camera/light scene motion: none";

            string selfShadowStatus = shd > 0
                ? string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Self-shadow scene state present (selfShadow:{0}; MmdSceneEnvironmentBinding records sampled MMD self-shadow state)", shd)
                : "Self-shadow scene motion: none";

            string durationSrc = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Cached VMD MaxFrame ({0})", mf);

            return new MmdVmdTimelineReadiness(
                mf,
                cam,
                lit,
                shd,
                durationSrc,
                sceneStatus,
                selfShadowStatus,
                "PMX model source and Timeline are required for VMD Clip creation.");
        }

        // --- Humanoid Clip Readiness preview (VMD Inspector, cache-only, non-persistent) ---

    }
}
