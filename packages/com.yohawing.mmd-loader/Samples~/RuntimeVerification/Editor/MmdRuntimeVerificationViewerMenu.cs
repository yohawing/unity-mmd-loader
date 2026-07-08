#nullable enable

using UnityEditor;
using UnityEditor.SceneManagement;

namespace Mmd.Samples.RuntimeVerification.Editor
{
    static class MmdRuntimeVerificationViewerMenu
    {
        private const string ScenePath = "Assets/RuntimeVerification/RuntimeVerification.unity";

        [MenuItem("Tools/MMDLoaderDevTools/Open Runtime Verification Viewer", false, 1700)]
        private static void OpenViewerScene()
        {
            OpenViewerScene(enterPlayMode: false);
        }

        [MenuItem("Tools/MMDLoaderDevTools/Play Runtime Verification Viewer", false, 1701)]
        private static void PlayViewerScene()
        {
            OpenViewerScene(enterPlayMode: true);
        }

        private static void OpenViewerScene(bool enterPlayMode)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog(
                    "Runtime Verification Viewer",
                    "RuntimeVerification sample scene was not found at " + ScenePath + ".",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
            if (enterPlayMode && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = true;
            }
        }
    }
}
