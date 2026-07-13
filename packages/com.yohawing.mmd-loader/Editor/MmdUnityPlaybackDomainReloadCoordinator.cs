#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd.UnityIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mmd.Editor
{
    [InitializeOnLoad]
    internal static class MmdUnityPlaybackDomainReloadCoordinator
    {
        static MmdUnityPlaybackDomainReloadCoordinator()
        {
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
            EditorApplication.delayCall += RestoreLoadedSceneControllers;
        }

        internal static void PrepareForReload(
            IReadOnlyList<MmdUnityPlaybackController> controllers,
            IReadOnlyList<MmdTransientRuntimeInstanceMarker> markers)
        {
            foreach (MmdUnityPlaybackController controller in controllers)
            {
                if (controller != null)
                {
                    controller.PrepareForAssemblyReload();
                }
            }

            CleanupTransientMarkers(markers);
        }

        internal static void RestoreAfterReload(
            IReadOnlyList<MmdUnityPlaybackController> controllers,
            IReadOnlyList<MmdTransientRuntimeInstanceMarker> staleMarkers)
        {
            CleanupTransientMarkers(staleMarkers);
            foreach (MmdUnityPlaybackController controller in controllers)
            {
                if (controller == null || !controller.isActiveAndEnabled || controller.IsConfigured)
                {
                    continue;
                }

                try
                {
                    controller.ConfigureFromPlaybackSourceIfAvailable();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "MMD playback could not be restored after assembly reload:" + Environment.NewLine + ex,
                        controller);
                }
            }
        }

        private static void BeforeAssemblyReload()
        {
            PrepareForReload(FindLoadedSceneObjects<MmdUnityPlaybackController>(), FindLoadedSceneObjects<MmdTransientRuntimeInstanceMarker>());
        }

        private static void RestoreLoadedSceneControllers()
        {
            RestoreAfterReload(FindLoadedSceneObjects<MmdUnityPlaybackController>(), FindLoadedSceneObjects<MmdTransientRuntimeInstanceMarker>());
        }

        private static void CleanupTransientMarkers(IReadOnlyList<MmdTransientRuntimeInstanceMarker> markers)
        {
            foreach (MmdTransientRuntimeInstanceMarker marker in markers)
            {
                if (marker != null && !marker.DestroyOwnedObjectsAndRoot())
                {
                    Debug.LogError(
                        "MMD transient runtime marker was not safe to remove; preserving its GameObject.",
                        marker);
                }
            }
        }

        private static T[] FindLoadedSceneObjects<T>() where T : Component
        {
            return Resources.FindObjectsOfTypeAll<T>()
                .Where(IsMainStageLoadedSceneObject)
                .ToArray();
        }

        internal static bool IsMainStageLoadedSceneObject(Component? value)
        {
            return value != null &&
                !EditorUtility.IsPersistent(value) &&
                value.gameObject.scene.IsValid() &&
                value.gameObject.scene.isLoaded &&
                StageUtility.GetStageHandle(value.gameObject) == StageUtility.GetMainStageHandle();
        }
    }
}
