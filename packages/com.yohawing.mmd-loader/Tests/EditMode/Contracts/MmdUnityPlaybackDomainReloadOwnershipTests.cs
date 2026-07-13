#nullable enable

using System.IO;
using System.Linq;
using Mmd.Editor;
using Mmd.Physics;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed class MmdUnityPlaybackDomainReloadOwnershipTests
    {
        private const string ModelFixture = "test_1bone_cube.pmx";
        private const string MotionFixture = "test_1bone_cube_motion.vmd";

        [Test]
        public void RawSceneRootSurvivesPrepareAndRestoresInPlace()
        {
            string pmxPath = MmdTestFixtures.FixtureAssetPath(ModelFixture);
            string vmdPath = MmdTestFixtures.FixtureAssetPath(MotionFixture);
            MmdEditorPlaybackSceneLoadResult? result = null;
            try
            {
                result = MmdEditorVerificationFacade.LoadPlaybackIntoScene(
                    pmxPath,
                    vmdPath,
                    playOnStart: false);
                GameObject root = result.Instance.Root;
                Mesh mesh = result.Instance.Mesh;

                MmdUnityPlaybackDomainReloadCoordinator.PrepareForReload(
                    new[] { result.Controller },
                    root.GetComponents<MmdTransientRuntimeInstanceMarker>());

                Assert.That(root != null, Is.True);
                Assert.That(mesh != null, Is.True);
                Assert.That(result.Controller.IsConfigured, Is.False);

                MmdUnityPlaybackDomainReloadCoordinator.RestoreAfterReload(
                    new[] { result.Controller },
                    System.Array.Empty<MmdTransientRuntimeInstanceMarker>());

                Assert.That(result.Controller.IsConfigured, Is.True);
                Assert.That(result.Controller.gameObject, Is.SameAs(root));
                Assert.That(FindMarkers(), Is.Empty);
            }
            finally
            {
                MmdTestInstanceScope.DestroyImporterCacheInstance(result?.Instance);
            }
        }

        [Test]
        public void TransientFallbackIsRemovedBeforeReloadAndRecreatedExactlyOnce()
        {
            string pmxPath = Path.GetFullPath(MmdTestFixtures.FixtureAssetPath(ModelFixture));
            string vmdPath = Path.GetFullPath(MmdTestFixtures.FixtureAssetPath(MotionFixture));
            var holder = new GameObject("reload-fallback-holder");
            try
            {
                MmdUnityPlaybackController controller = holder.AddComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                MmdRuntimeImporterComponent importer = holder.AddComponent<MmdRuntimeImporterComponent>();
                importer.ConfigurePaths(pmxPath, vmdPath, shouldPlayOnStart: false);
                Assert.That(importer.TryConfigureController(controller), Is.True);

                MmdTransientRuntimeInstanceMarker marker = FindMarkers().Single(value => value.Owner == controller);
                GameObject oldRoot = marker.gameObject;
                Mesh oldMesh = oldRoot.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;

                MmdUnityPlaybackDomainReloadCoordinator.PrepareForReload(
                    new[] { controller },
                    new[] { marker });

                Assert.That(holder != null, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(oldRoot == null, Is.True);
                Assert.That(oldMesh == null, Is.True);

                MmdUnityPlaybackDomainReloadCoordinator.RestoreAfterReload(
                    new[] { controller },
                    System.Array.Empty<MmdTransientRuntimeInstanceMarker>());

                MmdTransientRuntimeInstanceMarker[] restored = FindMarkers()
                    .Where(value => value.Owner == controller)
                    .ToArray();
                Assert.That(controller.IsConfigured, Is.True);
                Assert.That(restored, Has.Length.EqualTo(1));
                Assert.That(restored[0].Owner, Is.SameAs(controller));
                Assert.That(restored[0].gameObject, Is.Not.SameAs(holder));
            }
            finally
            {
                Object.DestroyImmediate(holder);
                foreach (MmdTransientRuntimeInstanceMarker marker in FindMarkers())
                {
                    marker.DestroyOwnedObjectsAndRoot();
                }
            }
        }

        [Test]
        public void OrphanTransientMarkerIsRemovedWithoutAnOwnerReference()
        {
            var holder = new GameObject("reload-orphan-holder");
            var root = new GameObject("reload-orphan-runtime");
            try
            {
                MmdTransientRuntimeInstanceMarker marker = root.AddComponent<MmdTransientRuntimeInstanceMarker>();
                var serialized = new SerializedObject(marker);
                serialized.FindProperty("owner").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                MmdUnityPlaybackDomainReloadCoordinator.RestoreAfterReload(
                    System.Array.Empty<MmdUnityPlaybackController>(),
                    new[] { marker });

                Assert.That(root == null, Is.True);
                Assert.That(holder != null, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(holder);
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void DisabledControllerDoesNotRecreateFallbackAfterReload()
        {
            string pmxPath = Path.GetFullPath(MmdTestFixtures.FixtureAssetPath(ModelFixture));
            string vmdPath = Path.GetFullPath(MmdTestFixtures.FixtureAssetPath(MotionFixture));
            var holder = new GameObject("reload-disabled-holder");
            try
            {
                MmdUnityPlaybackController controller = holder.AddComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                MmdRuntimeImporterComponent importer = holder.AddComponent<MmdRuntimeImporterComponent>();
                importer.ConfigurePaths(pmxPath, vmdPath, shouldPlayOnStart: false);
                Assert.That(importer.TryConfigureController(controller), Is.True);
                MmdTransientRuntimeInstanceMarker marker = FindMarkers().Single(value => value.Owner == controller);

                MmdUnityPlaybackDomainReloadCoordinator.PrepareForReload(
                    new[] { controller },
                    new[] { marker });
                controller.enabled = false;
                MmdUnityPlaybackDomainReloadCoordinator.RestoreAfterReload(
                    new[] { controller },
                    System.Array.Empty<MmdTransientRuntimeInstanceMarker>());

                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(FindMarkers().Where(value => value.Owner == controller), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(holder);
                foreach (MmdTransientRuntimeInstanceMarker marker in FindMarkers())
                {
                    marker.DestroyOwnedObjectsAndRoot();
                }
            }
        }

        [Test]
        public void PreviewSceneControllerIsExcludedFromAutomaticReloadCoordination()
        {
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            var previewObject = new GameObject("reload-preview-controller");
            try
            {
                SceneManager.MoveGameObjectToScene(previewObject, previewScene);
                MmdUnityPlaybackController controller = previewObject.AddComponent<MmdUnityPlaybackController>();

                Assert.That(
                    MmdUnityPlaybackDomainReloadCoordinator.IsMainStageLoadedSceneObject(controller),
                    Is.False);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static MmdTransientRuntimeInstanceMarker[] FindMarkers()
        {
            return Resources.FindObjectsOfTypeAll<MmdTransientRuntimeInstanceMarker>()
                .Where(value => value != null && value.gameObject.scene.IsValid() && value.gameObject.scene.isLoaded)
                .ToArray();
        }
    }
}
