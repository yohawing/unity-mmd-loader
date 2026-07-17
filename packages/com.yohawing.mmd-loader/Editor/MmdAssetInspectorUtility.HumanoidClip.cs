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
        internal static MmdHumanoidClipConversionPlan ComputeHumanoidClipReadinessForVmd(
            MmdVmdAsset vmdAsset,
            MmdPmxAsset? pmxAsset)
        {
            // Delegates to planner. Planner AnalyzePrerequisites uses VMD import cache only (no LoadMotion).
            if (vmdAsset == null)
            {
                // Planner handles null vmd; return its not-ready plan for consistency.
                return MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, null);
            }
            return MmdHumanoidClipConversionPlanner.AnalyzePrerequisites(pmxAsset, vmdAsset);
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
            // Keep compact: surface first 1-2 actionable items (PMX missing, VMD cache fail, hierarchy not ready, etc.).
            int take = System.Math.Min(2, diags.Count);
            return string.Join(" | ", diags.Take(take));
        }
    }
}
