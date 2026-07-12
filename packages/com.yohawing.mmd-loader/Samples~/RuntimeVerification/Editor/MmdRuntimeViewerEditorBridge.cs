#nullable enable

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mmd.Samples.RuntimeVerification.Editor
{
    [InitializeOnLoad]
    static class MmdRuntimeViewerEditorBridge
    {
        private const string SceneName = "RuntimeVerification";
        private const string SceneRootName = "Runtime Verification";
        private const string PreviewHostName = "MMD Runtime Viewer Editor Preview";

        static MmdRuntimeViewerEditorBridge()
        {
            MmdRuntimeViewerController.BrowseFileOverride = (title, extension) =>
                EditorUtility.OpenFilePanel(title, string.Empty, extension);
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            DestroyPreviewHosts();
            QueuePreviewRefresh();
        }

        private static void OnSceneOpened(Scene _, OpenSceneMode __)
        {
            QueuePreviewRefresh();
        }

        private static void OnActiveSceneChanged(Scene _, Scene __)
        {
            QueuePreviewRefresh();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                DestroyPreviewHosts();
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                QueuePreviewRefresh();
            }
        }

        private static void QueuePreviewRefresh()
        {
            EditorApplication.delayCall -= RefreshPreview;
            EditorApplication.delayCall += RefreshPreview;
            EditorApplication.update -= RefreshPreviewWhenEditorReady;
            EditorApplication.update += RefreshPreviewWhenEditorReady;
        }

        private static void RefreshPreviewWhenEditorReady()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.update -= RefreshPreviewWhenEditorReady;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.update -= RefreshPreviewWhenEditorReady;
            RefreshPreview();
        }

        private static void RefreshPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                DestroyPreviewHosts();
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.name != SceneName)
            {
                DestroyPreviewHosts();
                return;
            }

            GameObject? sceneRoot = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == SceneRootName)
                {
                    sceneRoot = root;
                    break;
                }
            }

            if (sceneRoot == null || FindPreviewHost(scene) != null)
            {
                return;
            }

            var host = new GameObject(PreviewHostName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            SceneManager.MoveGameObjectToScene(host, scene);
            host.transform.SetParent(sceneRoot.transform, worldPositionStays: false);
            var controller = host.AddComponent<MmdRuntimeViewerController>();
            controller.ConfigureEditorPreviewOwnership(host.transform);
            controller.Initialize(MmdRuntimeVerificationArguments.Parse(new[] { "--viewer" }));
        }

        private static GameObject? FindPreviewHost(Scene scene)
        {
            MmdRuntimeViewerController[] controllers =
                Resources.FindObjectsOfTypeAll<MmdRuntimeViewerController>();
            foreach (MmdRuntimeViewerController controller in controllers)
            {
                if (controller.gameObject.name == PreviewHostName &&
                    controller.gameObject.scene == scene)
                {
                    return controller.gameObject;
                }
            }

            return null;
        }

        private static void DestroyPreviewHosts()
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                GameObject? host = FindPreviewHost(scene);
                if (host != null)
                {
                    Object.DestroyImmediate(host);
                }
            }
        }
    }
}
