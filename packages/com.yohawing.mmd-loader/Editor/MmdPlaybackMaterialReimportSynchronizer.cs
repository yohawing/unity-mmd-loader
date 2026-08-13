#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd.UnityIntegration;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mmd.Editor
{
    internal static class MmdPlaybackMaterialReimportSynchronizer
    {
        private static readonly HashSet<string> PendingPmxPaths =
            new(StringComparer.OrdinalIgnoreCase);
        private static bool refreshScheduled;

        internal static void NotifyImportedAssets(IEnumerable<string> importedAssetPaths)
        {
            bool added = false;
            foreach (string path in importedAssetPaths)
            {
                if (!string.IsNullOrWhiteSpace(path) &&
                    path.EndsWith(".pmx", StringComparison.OrdinalIgnoreCase))
                {
                    added |= PendingPmxPaths.Add(path);
                }
            }

            if (!added || refreshScheduled)
            {
                return;
            }

            refreshScheduled = true;
            EditorApplication.delayCall += RefreshPendingControllers;
        }

        internal static int RefreshController(MmdUnityPlaybackController controller)
        {
            if (controller == null || !controller.IsConfigured)
            {
                return 0;
            }

            MmdPmxAsset? modelAsset = controller.ModelAssetSource;
            MmdUnityModelInstance? playbackInstance = controller.ConfiguredPlaybackInstance;
            if (modelAsset == null || playbackInstance == null)
            {
                return 0;
            }

            Material[] sourceMaterials = ResolveSourceMaterials(modelAsset);
            Material[] playbackMaterials = playbackInstance.Materials;
            if (sourceMaterials.Length != playbackMaterials.Length)
            {
                return 0;
            }

            int updatedPropertyCount = 0;
            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                if (!IsPlaybackCloneOf(sourceMaterials[i], playbackMaterials[i]))
                {
                    return 0;
                }
            }

            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                updatedPropertyCount += SynchronizeTextureProperties(
                    sourceMaterials[i],
                    playbackMaterials[i]);
            }

            return updatedPropertyCount;
        }

        private static void RefreshPendingControllers()
        {
            refreshScheduled = false;
            if (PendingPmxPaths.Count == 0)
            {
                return;
            }

            string[] importedPmxPaths = PendingPmxPaths.ToArray();
            PendingPmxPaths.Clear();
            foreach (MmdUnityPlaybackController controller in
                     Resources.FindObjectsOfTypeAll<MmdUnityPlaybackController>())
            {
                if (controller == null ||
                    !controller.gameObject.scene.IsValid() ||
                    !controller.gameObject.scene.isLoaded)
                {
                    continue;
                }

                MmdPmxAsset? modelAsset = controller.ModelAssetSource;
                if (modelAsset == null)
                {
                    continue;
                }

                string modelAssetPath = AssetDatabase.GetAssetPath(modelAsset);
                if (importedPmxPaths.Contains(modelAssetPath, StringComparer.OrdinalIgnoreCase))
                {
                    RefreshController(controller);
                }
            }
        }

        private static Material[] ResolveSourceMaterials(MmdPmxAsset modelAsset)
        {
            Material[] importedMaterials = modelAsset.ImportedMaterials;
            Material[] materialRemaps = modelAsset.MaterialRemaps;
            if (materialRemaps.Length == 0)
            {
                return importedMaterials;
            }

            Material[] resolved = (Material[])importedMaterials.Clone();
            int count = Math.Min(resolved.Length, materialRemaps.Length);
            for (int i = 0; i < count; i++)
            {
                if (materialRemaps[i] != null)
                {
                    resolved[i] = materialRemaps[i];
                }
            }

            return resolved;
        }

        private static bool IsPlaybackCloneOf(Material source, Material destination)
        {
            if (source == null || destination == null)
            {
                return false;
            }

            const string suffix = " Playback";
            string sourceName = source.name ?? string.Empty;
            while (sourceName.EndsWith(suffix, StringComparison.Ordinal))
            {
                sourceName = sourceName.Substring(0, sourceName.Length - suffix.Length);
            }

            return string.Equals(destination.name, sourceName + suffix, StringComparison.Ordinal);
        }

        private static int SynchronizeTextureProperties(Material source, Material destination)
        {
            if (source == null || destination == null || source.shader == null)
            {
                return 0;
            }

            int updatedPropertyCount = 0;
            Shader shader = source.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                if (shader.GetPropertyType(i) != ShaderPropertyType.Texture)
                {
                    continue;
                }

                int propertyId = shader.GetPropertyNameId(i);
                if (!destination.HasProperty(propertyId))
                {
                    continue;
                }

                Texture sourceTexture = source.GetTexture(propertyId);
                string propertyName = shader.GetPropertyName(i);
                Vector2 sourceScale = source.GetTextureScale(propertyName);
                Vector2 sourceOffset = source.GetTextureOffset(propertyName);
                if (destination.GetTexture(propertyId) == sourceTexture &&
                    destination.GetTextureScale(propertyName) == sourceScale &&
                    destination.GetTextureOffset(propertyName) == sourceOffset)
                {
                    continue;
                }

                destination.SetTexture(propertyId, sourceTexture);
                destination.SetTextureScale(propertyName, sourceScale);
                destination.SetTextureOffset(propertyName, sourceOffset);
                updatedPropertyCount++;
            }

            return updatedPropertyCount;
        }
    }

    public sealed class MmdPlaybackMaterialReimportPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            MmdPlaybackMaterialReimportSynchronizer.NotifyImportedAssets(importedAssets);
        }
    }
}
