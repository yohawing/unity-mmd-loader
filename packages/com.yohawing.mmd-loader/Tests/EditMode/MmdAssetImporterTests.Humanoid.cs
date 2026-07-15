#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Mmd.Editor;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Rendering;
using Mmd.Rendering.Universal;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed partial class MmdAssetImporterTests
    {
        [Test]
        public void SceneDragAndDropRejectsMixedRawPmxPathAndImportedVmdAsset()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            string pmxPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube.pmx");

            bool accepted = MmdSceneDragAndDrop.TryGetDraggedSources(
                new Object[] { vmdAsset },
                new[] { pmxPath },
                out MmdPmxAsset? draggedPmxAsset,
                out MmdVmdAsset? draggedVmdAsset,
                out string? draggedPmxPath,
                out string? draggedVmdPath);

            Assert.That(accepted, Is.False);
            Assert.That(draggedPmxAsset, Is.Null);
            Assert.That(draggedVmdAsset, Is.Null);
            Assert.That(draggedPmxPath, Is.Null);
            Assert.That(draggedVmdPath, Is.Null);
        }
        [Test]
        public void SceneDragAndDropCalculatesGroundDropPosition()
        {
            var ray = new Ray(new Vector3(2.0f, 4.0f, -3.0f), Vector3.down);

            Vector3 position = MmdSceneDragAndDrop.CalculateGroundDropPosition(ray);

            Assert.That(position.x, Is.EqualTo(2.0f).Within(0.0001f));
            Assert.That(position.y, Is.EqualTo(0.0f).Within(0.0001f));
            Assert.That(position.z, Is.EqualTo(-3.0f).Within(0.0001f));
        }
        [Test]
        public void SceneDragAndDropResolvesHierarchyDropParent()
        {
            var parent = new GameObject("mmd-drop-parent");
            try
            {
#pragma warning disable CS0618 // Legacy InstanceID input remains supported by Hierarchy drop callbacks.
                GameObject? resolved = MmdSceneDragAndDrop.GetHierarchyDropParent(parent.GetInstanceID());
#pragma warning restore CS0618
                GameObject? resolvedByEntityId = MmdSceneDragAndDrop.GetHierarchyDropParent(parent.GetEntityId());

                Assert.That(resolved, Is.SameAs(parent));
                Assert.That(resolvedByEntityId, Is.SameAs(parent));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
        [Test]
        public void SceneDragAndDropResolvesHierarchyDropHandlerParent()
        {
            var parent = new GameObject("mmd-drop-handler-parent");
            var sibling = new GameObject("mmd-drop-handler-sibling");
            var forcedParent = new GameObject("mmd-drop-handler-forced-parent");
            sibling.transform.SetParent(parent.transform, worldPositionStays: false);
            try
            {
                GameObject? rootDrop = MmdSceneDragAndDrop.GetHierarchyDropParent(
                    0,
                    HierarchyDropFlags.DropUpon,
                    forcedParent: null);
                GameObject? forcedDrop = MmdSceneDragAndDrop.GetHierarchyDropParent(
                    0,
                    HierarchyDropFlags.None,
                    forcedParent.transform);
#pragma warning disable CS0618 // Legacy InstanceID input remains supported by Hierarchy drop callbacks.
                GameObject? uponDrop = MmdSceneDragAndDrop.GetHierarchyDropParent(
                    parent.GetInstanceID(),
                    HierarchyDropFlags.DropUpon,
                    forcedParent: null);
                GameObject? betweenDrop = MmdSceneDragAndDrop.GetHierarchyDropParent(
                    sibling.GetInstanceID(),
                    HierarchyDropFlags.DropBetween,
                    forcedParent: null);
#pragma warning restore CS0618
                GameObject? entityIdUponDrop = MmdSceneDragAndDrop.GetHierarchyDropParent(
                    parent.GetEntityId(),
                    HierarchyDropFlags.DropUpon,
                    forcedParent: null);
                GameObject? entityIdBetweenDrop = MmdSceneDragAndDrop.GetHierarchyDropParent(
                    sibling.GetEntityId(),
                    HierarchyDropFlags.DropBetween,
                    forcedParent: null);

                Assert.That(rootDrop, Is.Null);
                Assert.That(forcedDrop, Is.SameAs(forcedParent));
                Assert.That(uponDrop, Is.SameAs(parent));
                Assert.That(betweenDrop, Is.SameAs(parent));
                Assert.That(entityIdUponDrop, Is.SameAs(parent));
                Assert.That(entityIdBetweenDrop, Is.SameAs(parent));
            }
            finally
            {
                Object.DestroyImmediate(forcedParent);
                Object.DestroyImmediate(parent);
            }
        }
        [Test]
        public void SceneDragAndDropCalculatesHierarchyDropPosition()
        {
            var parent = new GameObject("mmd-drop-position-parent");
            parent.transform.position = new Vector3(3.0f, 2.0f, -4.0f);
            try
            {
                Vector3 parentPosition = MmdSceneDragAndDrop.CalculateHierarchyDropPosition(parent);
                Vector3 rootPosition = MmdSceneDragAndDrop.CalculateHierarchyDropPosition(parent: null);

                Assert.That(parentPosition.x, Is.EqualTo(3.0f).Within(0.0001f));
                Assert.That(parentPosition.y, Is.EqualTo(2.0f).Within(0.0001f));
                Assert.That(parentPosition.z, Is.EqualTo(-4.0f).Within(0.0001f));
                Assert.That(rootPosition, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
        [Test]
        public void SceneDragAndDropLoadsPmxUnderHierarchyParent()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            var parent = new GameObject("mmd-pmx-drop-parent");
            MmdUnityModelInstance? instance = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(
                    pmxAsset,
                    new Vector3(5.0f, 0.0f, -2.0f),
                    parent);

                Assert.That(instance.Root.transform.parent, Is.SameAs(parent.transform));
                Assert.That(instance.Root.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(instance.VertexCount, Is.GreaterThan(0));
                Assert.That(instance.IndexCount, Is.GreaterThan(0));
                Assert.That(instance.BoneTransforms, Has.Length.GreaterThan(0));
                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller, Is.Not.Null);
                // PMX-only drag stores the model source on the controller.
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.ModelSourceId, Is.EqualTo(TempPmxPath));
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
            }
            finally
            {
                MmdTestInstanceScope.DestroyImporterCacheInstance(instance);
                Object.DestroyImmediate(parent);
            }
        }
        [Test]
        public void SceneDragAndDropLoadsPmxAssetAsImportedPrefabInstance()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null);
            MmdUnityModelInstance? instance = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(
                    pmxAsset,
                    new Vector3(1.0f, 0.0f, 2.0f),
                    parent: null);

                Assert.That(instance.Root.transform.parent, Is.Null);
                Assert.That(instance.Root.transform.position, Is.EqualTo(new Vector3(1.0f, 0.0f, 2.0f)));
                Assert.That(PrefabUtility.GetPrefabInstanceStatus(instance.Root), Is.EqualTo(PrefabInstanceStatus.Connected));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(instance.Root), Is.SameAs(pmxAsset.ImportedRoot));
                Assert.That(instance.Mesh, Is.SameAs(pmxAsset.ImportedMesh));
                Assert.That(instance.Materials, Is.Not.Empty);
                Assert.That(instance.Materials[0], Is.SameAs(pmxAsset.ImportedMaterials[0]));

                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
            }
            finally
            {
                MmdTestInstanceScope.DestroyImporterCacheInstance(instance);
            }
        }
        [Test]
        public void SceneDragAndDropLoadsRawPmxPathUnderHierarchyParent()
        {
            string pmxPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube.pmx");
            var parent = new GameObject("mmd-raw-pmx-drop-parent");
            MmdUnityModelInstance? instance = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxPathForDragAndDrop(
                    pmxPath,
                    new Vector3(2.0f, 0.0f, -3.0f),
                    parent);

                Assert.That(instance.Root.transform.parent, Is.SameAs(parent.transform));
                Assert.That(instance.Root.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(instance.VertexCount, Is.GreaterThan(0));
                Assert.That(instance.IndexCount, Is.GreaterThan(0));
                Assert.That(instance.BoneTransforms, Has.Length.GreaterThan(0));
                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller, Is.Not.Null);
                // Raw PMX path drag also creates model source provider (via RuntimeImporter).
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.ModelSourceId, Is.EqualTo(Path.GetFullPath(pmxPath)));
                MmdRuntimeImporterComponent importer = controller.GetComponent<MmdRuntimeImporterComponent>();
                Assert.That(importer, Is.Not.Null);
                Assert.That(importer.ModelPath, Is.EqualTo(Path.GetFullPath(pmxPath)));
                Assert.That(importer.MotionPath, Is.Empty);
                Assert.That(importer.LastConfigurationStatus, Is.EqualTo("configured-model-path-assigned"));
            }
            finally
            {
                MmdTestInstanceScope.DestroyImporterCacheInstance(instance);
                Object.DestroyImmediate(parent);
            }
        }
        [Test]
        public void SceneDragAndDropLoadsRawPmxPathAtSceneDropPosition()
        {
            string pmxPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube.pmx");
            MmdUnityModelInstance? instance = null;

            try
            {
                Vector3 dropPosition = new Vector3(4.0f, 0.0f, -5.0f);
                instance = MmdSceneDragAndDrop.LoadPmxPathForDragAndDrop(
                    pmxPath,
                    dropPosition,
                    parent: null);

                Assert.That(instance.Root.transform.parent, Is.Null);
                Assert.That(instance.Root.transform.position, Is.EqualTo(dropPosition));
                Assert.That(instance.VertexCount, Is.GreaterThan(0));
                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.ModelSourceId, Is.EqualTo(Path.GetFullPath(pmxPath)));
                MmdRuntimeImporterComponent importer = controller.GetComponent<MmdRuntimeImporterComponent>();
                Assert.That(importer, Is.Not.Null);
                Assert.That(importer.ModelPath, Is.EqualTo(Path.GetFullPath(pmxPath)));
                Assert.That(importer.MotionPath, Is.Empty);
            }
            finally
            {
                MmdTestInstanceScope.DestroyImporterCacheInstance(instance);
            }
        }
        [Test]
        public void SceneDragAndDropLoadsPlaybackUnderHierarchyParent()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            var parent = new GameObject("mmd-playback-drop-parent");
            MmdEditorPlaybackSceneLoadResult? result = null;

            try
            {
                result = MmdSceneDragAndDrop.LoadPlaybackForDragAndDrop(
                    pmxAsset,
                    vmdAsset,
                    new Vector3(-1.0f, 0.0f, 6.0f),
                    parent);

                Assert.That(result.Instance.Root.transform.parent, Is.SameAs(parent.transform));
                Assert.That(result.Instance.Root.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(result.Controller.IsConfigured, Is.True);
                Assert.That(result.Controller.ModelAssetSource, Is.SameAs(pmxAsset));
                Assert.That(result.Controller.MotionAssetSource, Is.SameAs(vmdAsset));
                Assert.That(result.Instance.VertexCount, Is.GreaterThan(0));
                Assert.That(result.Motion.maxFrame, Is.GreaterThan(0));
            }
            finally
            {
                MmdTestInstanceScope.DestroyImporterCacheInstance(result?.Instance);
                Object.DestroyImmediate(parent);
            }
        }
        [Test]
        public void SceneDragAndDropAttachesVmdSourceComponentToExistingPmxModel()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            MmdUnityModelInstance? instance = null;
            MmdVmdAsset? replacementVmd = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.zero, parent: null);
                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                // Precondition after PMX-only drag: controller owns the model source.
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                // controller (default Live) is sole source of truth for normal playback
                Renderer[] renderers = instance.Root.GetComponentsInChildren<Renderer>(includeInactive: true);
                GameObject childTarget = instance.Root.transform.childCount > 0
                    ? instance.Root.transform.GetChild(0).gameObject
                    : instance.Root;

                bool attached = MmdSceneDragAndDrop.TryAttachVmdSourceToExistingModel(
                    childTarget,
                    vmdAsset);

                // VMD attach stores motion on the controller.
                Assert.That(attached, Is.True);
                Assert.That(controller.MotionAssetSource, Is.SameAs(vmdAsset));
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.MotionSourceId, Is.EqualTo(TempVmdPath));

                replacementVmd = ScriptableObject.CreateInstance<MmdVmdAsset>();
                bool updated = MmdSceneDragAndDrop.TryAttachVmdSourceToExistingModel(
                    instance.Root,
                    replacementVmd);

                // Replacement attach updates the controller motion source.
                Assert.That(updated, Is.True);
                Assert.That(controller.MotionAssetSource, Is.SameAs(replacementVmd));

                // Re-attach fixture vmd so that motion-comp provides TempVmdPath identity for the MotionSourceId assert below.
                bool reattachedForConfig = MmdSceneDragAndDrop.TryAttachVmdSourceToExistingModel(instance.Root, vmdAsset);
                Assert.That(reattachedForConfig, Is.True);

                // Controller model+motion source allows ConfigureFrom... and controller configured.
                bool configured = controller.ConfigureFromPlaybackSourceIfAvailable();
                Assert.That(configured, Is.True);
                Assert.That(controller.IsConfigured, Is.True);
                Assert.That(controller.MotionSourceId, Is.EqualTo(TempVmdPath));
                foreach (Renderer renderer in renderers)
                {
                    Assert.That(renderer.enabled, Is.True, renderer.name);
                }
            }
            finally
            {
                if (replacementVmd != null)
                {
                    Object.DestroyImmediate(replacementVmd);
                }

                MmdTestInstanceScope.DestroyImporterCacheInstance(instance);
            }
        }
        [Test]
        public void PmxDragCreatesModelSourceProviderWithoutMotion()
        {
            // Focused test: PMX-only drag/load creates model source provider (not full source).
            // ModelAsset set, MotionAsset null, HasModelSource true, IsConfigured false, ModelSourceId stable.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdUnityModelInstance? instance = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.zero, parent: null);

                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.ModelSourceId, Is.EqualTo(TempPmxPath));
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));

                // Idempotent re-check of provider id stability (no motion yet).
                string modelIdBefore = controller.ModelSourceId;
                Assert.That(controller.ModelSourceId, Is.EqualTo(modelIdBefore));
            }
            finally
            {
                MmdTestInstanceScope.DestroyImporterCacheInstance(instance);
            }
        }
        [Test]
        public void VmdAttachToModelSourceUpdatesMotionWhilePreservingModelAndSettings()
        {
            // Focused test: VMD attach consumes existing model source provider (no duplicate component),
            // preserves model asset + local playback settings, sets motion asset.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            MmdUnityModelInstance? instance = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.zero, parent: null);
                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
                Assert.That(controller.IsConfigured, Is.False);

                // Attach creates/updates the controller motion source.
                bool ok = MmdSceneDragAndDrop.TryAttachVmdSourceToExistingModel(instance.Root, vmdAsset);
                Assert.That(ok, Is.True);
                Assert.That(controller.MotionAssetSource, Is.SameAs(vmdAsset));
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.MotionSourceId, Is.EqualTo(TempVmdPath));
            }
            finally
            {
                MmdTestInstanceScope.DestroyImporterCacheInstance(instance);
            }
        }
        [Test]
        public void SceneDragAndDropLoadsRawPmxAndRawVmdPlaybackUnderHierarchyParent()
        {
            string pmxPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube.pmx");
            string vmdPath = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", "test_1bone_cube_motion.vmd");
            var parent = new GameObject("mmd-raw-playback-drop-parent");
            MmdEditorPlaybackSceneLoadResult? result = null;

            try
            {
                result = MmdSceneDragAndDrop.LoadPlaybackPathForDragAndDrop(
                    pmxPath,
                    vmdPath,
                    new Vector3(7.0f, 0.0f, -8.0f),
                    parent);

                string fullPmxPath = Path.GetFullPath(pmxPath);
                string fullVmdPath = Path.GetFullPath(vmdPath);
                Assert.That(result.Instance.Root.transform.parent, Is.SameAs(parent.transform));
                Assert.That(result.Instance.Root.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(result.Controller.IsConfigured, Is.True);
                MmdRuntimeImporterComponent importer = result.Controller.GetComponent<MmdRuntimeImporterComponent>();
                Assert.That(importer, Is.Not.Null);
                Assert.That(importer.ModelPath, Is.EqualTo(fullPmxPath));
                Assert.That(importer.MotionPath, Is.EqualTo(fullVmdPath));
                Assert.That(result.Controller.ModelSourceId, Is.EqualTo(fullPmxPath));
                Assert.That(result.Controller.MotionSourceId, Is.EqualTo(fullVmdPath));
                Assert.That(result.ModelPath, Is.EqualTo(fullPmxPath));
                Assert.That(result.MotionPath, Is.EqualTo(fullVmdPath));
                Assert.That(result.InitialSnapshot.frame.frame, Is.EqualTo(0));
                Assert.That(result.Instance.VertexCount, Is.GreaterThan(0));
                Assert.That(result.Motion.maxFrame, Is.GreaterThan(0));

                GameObject sceneRoot = result.Instance.Root;
                Mesh sceneMesh = result.Instance.Mesh;
                result.Controller.ConfigureFromRuntimeImporterPaths(
                    fullPmxPath,
                    fullVmdPath,
                    new MmdPlaybackConfig(30.0f, 0, playOnStart: false),
                    allowRuntimeFallback: false);

                Assert.That(sceneRoot != null, Is.True, "Reconfiguration must not destroy the authored Scene root.");
                Assert.That(sceneMesh != null, Is.True, "Reconfiguration must preserve the authored Scene mesh.");
                Assert.That(result.Controller.gameObject, Is.SameAs(sceneRoot));
                Assert.That(result.Controller.IsConfigured, Is.True);
            }
            finally
            {
                MmdTestInstanceScope.DestroyImporterCacheInstance(result?.Instance);
                Object.DestroyImmediate(parent);
            }
        }
    }
}
