#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mmd.Editor
{
    public static class MmdHumanoidSetupAssetBuilder
    {
        private const string MenuPath = "Assets/MMD Loader/Create Humanoid Setup Asset";

        [MenuItem(MenuPath)]
        public static void CreateSelectedHumanoidSetupAssetFromMenu()
        {
            if (Selection.activeObject is not MmdPmxAsset pmxAsset)
            {
                EditorUtility.DisplayDialog("MMD Humanoid Setup Failed", "Select one imported PMX asset.", "OK");
                return;
            }

            MmdHumanoidSetupAsset setup = CreateHumanoidSetupAsset(
                pmxAsset,
                GetDefaultSetupAssetPath(pmxAsset));
            Selection.activeObject = setup;
            EditorGUIUtility.PingObject(setup);
        }

        [MenuItem(MenuPath, true)]
        public static bool ValidateCreateSelectedHumanoidSetupAssetFromMenu()
        {
            return Selection.activeObject is MmdPmxAsset;
        }

        public static string GetDefaultSetupAssetPath(MmdPmxAsset pmxAsset)
        {
            string pmxPath = AssetDatabase.GetAssetPath(pmxAsset);
            string directory = "Assets";
            if (!string.IsNullOrWhiteSpace(pmxPath) &&
                pmxPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                directory = Path.GetDirectoryName(pmxPath)?.Replace('\\', '/') ?? "Assets";
            }

            return directory + "/MmdHumanoidSetup.asset";
        }

        public static MmdHumanoidSetupAsset CreateHumanoidSetupAsset(
            MmdPmxAsset pmxAsset,
            string assetPath,
            MmdHumanoidSetupPreset preset = MmdHumanoidSetupPreset.MmdSemiStandard)
        {
            if (pmxAsset == null)
            {
                throw new ArgumentNullException(nameof(pmxAsset));
            }

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException("Humanoid setup asset path is required.", nameof(assetPath));
            }

            string normalizedAssetPath = NormalizeSetupAssetPath(assetPath);
            string? directory = Path.GetDirectoryName(normalizedAssetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(Path.Combine(ProjectRoot, directory));
                AssetDatabase.Refresh();
            }

            string uniqueAssetPath = AssetDatabase.GenerateUniqueAssetPath(normalizedAssetPath);
            MmdHumanoidSetupAsset setup = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            setup.Initialize(pmxAsset, preset);
            AssetDatabase.CreateAsset(setup, uniqueAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(uniqueAssetPath, ImportAssetOptions.ForceUpdate);
            MmdHumanoidSetupAsset loaded = AssetDatabase.LoadAssetAtPath<MmdHumanoidSetupAsset>(uniqueAssetPath);
            if (loaded == null)
            {
                throw new InvalidOperationException("Created humanoid setup asset was not found: " + uniqueAssetPath);
            }

            return loaded;
        }

        private static string NormalizeSetupAssetPath(string assetPath)
        {
            if (Path.IsPathRooted(assetPath))
            {
                throw new ArgumentException("Humanoid setup asset path must be a project-relative Assets/*.asset path.", nameof(assetPath));
            }

            string normalizedAssetPath = assetPath.Replace('\\', '/');
            if (!normalizedAssetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !normalizedAssetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Humanoid setup asset path must be an Assets/*.asset path.", nameof(assetPath));
            }

            string[] segments = normalizedAssetPath.Split('/');
            foreach (string segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment) ||
                    string.Equals(segment, ".", StringComparison.Ordinal) ||
                    string.Equals(segment, "..", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Humanoid setup asset path must not contain empty or traversal segments.", nameof(assetPath));
                }
            }

            return normalizedAssetPath;
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }
}
