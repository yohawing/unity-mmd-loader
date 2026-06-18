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
        private const string MenuPath = "Tools/MMD Loader/Load PMX Into Scene";
        private const string SelectedAssetMenuPath = "Tools/MMD Loader/Load Selected PMX Asset Into Scene";

        [MenuItem(MenuPath)]
        public static void LoadPmxIntoSceneFromMenu()
        {
            string path = EditorUtility.OpenFilePanel("Load PMX Into Scene", string.Empty, "pmx");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                MmdUnityModelInstance instance = LoadPmxIntoScene(path);
                Selection.activeGameObject = instance.Root;
                Debug.LogFormat(
                    "Loaded PMX into scene: {0}; vertices={1}; indices={2}; submeshes={3}; bones={4}; loadedDiffuseTextures={5}; loadedSphereTextures={6}; loadedToonTextures={7}; missingTextures={8}; unsupportedTextures={9}; skippedSphereTextures={10}; skippedToonTextures={11}",
                    path,
                    instance.VertexCount,
                    instance.IndexCount,
                    instance.SubmeshCount,
                    instance.BoneTransforms.Length,
                    instance.LoadedDiffuseTextureCount,
                    instance.LoadedSphereTextureCount,
                    instance.LoadedToonTextureCount,
                    instance.MissingTextureReferenceCount,
                    instance.UnsupportedTextureReferenceCount,
                    instance.SkippedSphereTextureReferenceCount,
                    instance.SkippedToonTextureReferenceCount);

                foreach (string diagnostic in instance.TextureDiagnostics.Messages)
                {
                    Debug.Log("MMD texture diagnostic: " + diagnostic);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to load PMX into scene: " + path + Environment.NewLine + ex);
                EditorUtility.DisplayDialog("MMD PMX Load Failed", ex.Message, "OK");
            }
        }

        public static MmdUnityModelInstance LoadPmxIntoScene(string path)
        {
            MmdEditorPmxSceneLoadResult result = MmdEditorVerificationFacade.LoadPmxIntoScene(path);
            MmdUnityModelInstance instance = result.Instance;
            ConfigureRawPathModelSource(instance, result.ModelPath);
            Undo.RegisterCreatedObjectUndo(instance.Root, "Load PMX Into Scene");
            return instance;
        }

        [MenuItem(SelectedAssetMenuPath)]
        public static void LoadSelectedPmxAssetIntoSceneFromMenu()
        {
            // D1 compatibility: resolve either direct MmdPmxAsset or the GameObject main object
            // at a .pmx asset path to the metadata MmdPmxAsset sub-asset.
            MmdPmxAsset? pmxAsset = Selection.activeObject as MmdPmxAsset
                ?? TryResolveMmdPmxAssetFromMainGameObject(Selection.activeObject);
            if (pmxAsset == null)
            {
                EditorUtility.DisplayDialog("MMD PMX Load Failed", "Select one imported PMX asset.", "OK");
                return;
            }

            try
            {
                MmdUnityModelInstance instance = LoadPmxIntoScene(pmxAsset);
                Selection.activeGameObject = instance.Root;
                Debug.LogFormat(
                    "Loaded PMX asset into scene: source={0}; vertices={1}; indices={2}; bones={3}",
                    pmxAsset.SourceId,
                    instance.VertexCount,
                    instance.IndexCount,
                    instance.BoneTransforms.Length);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to load selected PMX asset into scene:" + Environment.NewLine + ex);
                EditorUtility.DisplayDialog("MMD PMX Load Failed", ex.Message, "OK");
            }
        }

        [MenuItem(SelectedAssetMenuPath, true)]
        public static bool ValidateLoadSelectedPmxAssetIntoSceneFromMenu()
        {
            // D1 compatibility: accept either the MmdPmxAsset metadata sub-asset
            // or the GameObject main object at a .pmx asset path.
            return Selection.activeObject is MmdPmxAsset
                || TryResolveMmdPmxAssetFromMainGameObject(Selection.activeObject) != null;
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
