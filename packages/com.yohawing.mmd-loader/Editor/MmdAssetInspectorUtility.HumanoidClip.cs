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
        public static void DrawVmdHumanoidClipReadinessSection(
            MmdVmdAsset vmdAsset,
            ref MmdPmxAsset? previewPmx,
            ref MmdHumanoidSetupAsset? previewSetup)
        {
            if (vmdAsset == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Humanoid Clip Readiness (preview)", EditorStyles.boldLabel);

            previewPmx = (MmdPmxAsset?)EditorGUILayout.ObjectField(
                "PMX Asset",
                previewPmx,
                typeof(MmdPmxAsset),
                allowSceneObjects: false);

            previewSetup = (MmdHumanoidSetupAsset?)EditorGUILayout.ObjectField(
                "Humanoid Setup Asset",
                previewSetup,
                typeof(MmdHumanoidSetupAsset),
                allowSceneObjects: false);

            MmdHumanoidClipConversionPlan plan = ComputeHumanoidClipReadinessForVmd(vmdAsset, previewPmx, previewSetup);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Status", plan.Readiness);
            }

            // Short actionable diagnostics only for not-ready states. No long normal-state HelpBoxes.
            if (!plan.PrerequisitesReady)
            {
                string compact = FormatCompactVmdHumanoidIssues(plan);
                if (!string.IsNullOrEmpty(compact))
                {
                    EditorGUILayout.HelpBox(compact, MessageType.Info);
                }
            }
        }

        internal static MmdHumanoidClipConversionPlan ComputeHumanoidClipReadinessForVmd(
            MmdVmdAsset vmdAsset,
            MmdPmxAsset? pmxAsset,
            MmdHumanoidSetupAsset? setupAsset)
        {
            // Delegates to planner. Planner AnalyzePrerequisites uses VMD import cache only (no LoadMotion).
            // This helper is testable without IMGUI and without triggering full VMD parse.
            if (vmdAsset == null)
            {
                // Planner handles null vmd; return its not-ready plan for consistency.
                return MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, null, setupAsset);
            }
            return MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, vmdAsset, setupAsset);
        }

        internal static string FormatCompactVmdHumanoidIssues(MmdHumanoidClipConversionPlan plan)
        {
            return FormatCompactHumanoidClipConversionIssues(plan);
        }

        internal static string FormatCompactHumanoidClipConversionIssues(MmdHumanoidClipConversionPlan plan)
        {
            if (plan == null || plan.PrerequisitesReady)
            {
                return string.Empty;
            }
            var diags = plan.Diagnostics;
            if (diags == null || diags.Count == 0)
            {
                return "Humanoid Clip prerequisites not ready.";
            }
            // Keep compact: surface first 1-2 actionable items (PMX missing, setup mismatch, VMD cache fail, hierarchy not ready, etc.).
            int take = System.Math.Min(2, diags.Count);
            return string.Join(" | ", diags.Take(take));
        }
    }
}
