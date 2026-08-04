#nullable enable

using System;
using System.Linq;

namespace Mmd.Editor
{
    internal static partial class MmdAssetInspectorUtility
    {
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
