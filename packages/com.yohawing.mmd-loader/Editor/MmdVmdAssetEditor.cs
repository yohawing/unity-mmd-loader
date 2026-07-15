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
    [CustomEditor(typeof(MmdVmdAsset))]
    public sealed class MmdVmdAssetEditor : UnityEditor.Editor
    {
        private bool showReadinessDiagnostics;
        private IReadOnlyList<string>? lastStructuralDiagnostics;

        // Non-persistent preview references for Humanoid Clip Readiness (per Inspector lifetime).
        // Selection/inspector display must not trigger VMD parse; planner now uses import cache only.
        private MmdPmxAsset? previewPmxAsset;
        private MmdHumanoidSetupAsset? previewSetupAsset;

        public override void OnInspectorGUI()
        {
            if (target is not MmdVmdAsset asset)
            {
                return;
            }

            MmdAssetInspectorUtility.DrawSummary("VMD Asset", asset.SourceId, asset.SourcePath, asset.ByteLength);

            if (asset.ByteLength > 0)
            {
                EditorGUILayout.Space();
                MmdAssetInspectorUtility.DrawVmdMotionSummary(asset);

                EditorGUILayout.Space();
                using (new EditorGUI.DisabledScope(asset.ByteLength <= 0))
                {
                    if (GUILayout.Button("Bake to AnimationClip..."))
                    {
                        MmdGenericAnimationClipBakeWindow.OpenFromVmd(asset);
                    }
                }

                EditorGUILayout.Space();
                using (new EditorGUI.DisabledScope(false))
                {
                    if (GUILayout.Button("Run VMD Diagnostics"))
                    {
                        RefreshDiagnostics(asset, repaint: true);
                    }
                }

                showReadinessDiagnostics = EditorGUILayout.Foldout(showReadinessDiagnostics, "Readiness Diagnostics", true);
                if (showReadinessDiagnostics)
                {
                    // Initialize from import-time cached structural diagnostics.
                    // Do NOT call LoadMotion / Refresh on mere asset selection or inspector open.
                    if (lastStructuralDiagnostics == null)
                    {
                        lastStructuralDiagnostics = asset.StructuralDiagnostics;
                    }

                    EditorGUILayout.Space();
                    MmdAssetInspectorUtility.DrawVmdStructuralDiagnostics(lastStructuralDiagnostics);

                    EditorGUILayout.Space();
                    MmdAssetInspectorUtility.DrawVmdTimelineReadiness(asset);

                    EditorGUILayout.Space();
                    MmdAssetInspectorUtility.DrawVmdHumanoidClipReadinessSection(
                        asset,
                        ref previewPmxAsset,
                        ref previewSetupAsset);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("VMD asset has no imported bytes. Reimport the source .vmd file.", MessageType.Warning);
            }
        }

        private void RefreshDiagnostics(MmdVmdAsset asset, bool repaint)
        {
            // Only path that performs full LoadMotion for structural revalidation.
            // Called exclusively from the "Run VMD Diagnostics" button.
            try
            {
                MmdMotionDefinition motion = asset.LoadMotion();
                lastStructuralDiagnostics = MmdMotionValidator.ValidateStructuralMotion(motion);
            }
            catch (System.Exception ex)
            {
                lastStructuralDiagnostics = new[]
                {
                    "Failed to load or validate VMD motion: " + ex.Message
                };
            }

            if (repaint)
            {
                Repaint();
            }
        }
    }

}
