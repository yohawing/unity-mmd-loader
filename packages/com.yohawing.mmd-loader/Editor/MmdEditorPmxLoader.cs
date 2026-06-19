#nullable enable

using System;
using UnityEditor;
using UnityEngine;
using Mmd;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Editor
{
    public static class MmdEditorPmxLoader
    {
        public static MmdUnityModelInstance LoadPmxIntoScene(string path)
        {
            MmdEditorPmxSceneLoadResult result = MmdEditorVerificationFacade.LoadPmxIntoScene(path);
            MmdUnityModelInstance instance = result.Instance;
            ConfigureRawPathModelSource(instance, result.ModelPath);
            Undo.RegisterCreatedObjectUndo(instance.Root, "Load PMX Into Scene");
            return instance;
        }

        /// <summary>
        /// When a .pmx asset's main object is a GameObject (D1), this resolves the
        /// metadata MmdPmxAsset sub-asset from that GameObject selection.
        /// Returns null if the selected object is not a GameObject under a .pmx asset.
        /// </summary>
        internal static MmdPmxAsset? TryResolveMmdPmxAssetFromMainGameObject(Object? selected)
        {
            if (selected is GameObject go)
            {
                string path = AssetDatabase.GetAssetPath(go);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".pmx", System.StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(path);
                }
            }
            return null;
        }

        public static MmdUnityModelInstance LoadPmxIntoScene(MmdPmxAsset pmxAsset)
        {
            MmdEditorPmxSceneLoadResult result = MmdEditorVerificationFacade.LoadPmxIntoScene(pmxAsset);
            MmdUnityModelInstance instance = result.Instance;
            ConfigureAssetModelSource(instance, pmxAsset);
            Undo.RegisterCreatedObjectUndo(instance.Root, "Load PMX Asset Into Scene");
            return instance;
        }

        private static MmdUnityPlaybackController EnsurePlaybackController(MmdUnityModelInstance instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (instance.Root == null)
            {
                throw new ArgumentException("MMD scene instance root is required.", nameof(instance));
            }

            return instance.Root.GetComponent<MmdUnityPlaybackController>()
                ?? instance.Root.AddComponent<MmdUnityPlaybackController>();
        }

        private static void ConfigureAssetModelSource(MmdUnityModelInstance instance, MmdPmxAsset pmxAsset)
        {
            MmdUnityPlaybackController controller = EnsurePlaybackController(instance);
            controller.ConfigureModelAsset(pmxAsset);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureRawPathModelSource(MmdUnityModelInstance instance, string pmxPath)
        {
            MmdUnityPlaybackController controller = EnsurePlaybackController(instance);
            MmdRuntimeImporterComponent importer = controller.GetComponent<MmdRuntimeImporterComponent>()
                ?? controller.gameObject.AddComponent<MmdRuntimeImporterComponent>();
            importer.ConfigureModelPath(pmxPath);
        }
    }
}
