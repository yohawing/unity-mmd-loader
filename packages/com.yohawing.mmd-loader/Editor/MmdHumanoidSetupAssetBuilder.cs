#nullable enable

#pragma warning disable CS0618

using System;
using System.IO;
using UnityEditor;

namespace Mmd.Editor
{
    [Obsolete("MmdHumanoidSetupAssetBuilder is retained only for source compatibility. Reimport the PMX with Animation Type = Humanoid instead.")]
    public static class MmdHumanoidSetupAssetBuilder
    {
        public static string GetDefaultSetupAssetPath(MmdPmxAsset pmxAsset)
        {
            string pmxPath = AssetDatabase.GetAssetPath(pmxAsset);
            string directory = "Assets";
            if (!string.IsNullOrWhiteSpace(pmxPath)
                && pmxPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                directory = Path.GetDirectoryName(pmxPath)?.Replace('\\', '/') ?? "Assets";
            }

            return directory + "/MmdHumanoidSetup.asset";
        }

        [Obsolete("Legacy setup assets can no longer be created. Reimport the PMX with Animation Type = Humanoid instead.")]
        public static MmdHumanoidSetupAsset CreateHumanoidSetupAsset(
            MmdPmxAsset pmxAsset,
            string assetPath,
            MmdHumanoidSetupPreset preset = MmdHumanoidSetupPreset.MmdSemiStandard)
        {
            throw new NotSupportedException(
                "Legacy MmdHumanoidSetupAsset creation was removed. "
                + "Reimport the PMX with Animation Type = Humanoid to persist its Avatar and retarget mapping.");
        }
    }
}
