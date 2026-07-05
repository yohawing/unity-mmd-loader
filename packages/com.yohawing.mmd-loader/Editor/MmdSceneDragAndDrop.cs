#nullable enable

using System;
using System.IO;
using System.Reflection;
using Mmd;
using Mmd.Parser;
using UnityEditor;
using UnityEngine;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Editor
{
    [InitializeOnLoad]
    public static class MmdSceneDragAndDrop
    {
        private static readonly MethodInfo? InstanceIdToObjectMethod = typeof(EditorUtility).GetMethod(
            "InstanceIDToObject",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(int) },
            modifiers: null);

        static MmdSceneDragAndDrop()
        {
            SceneView.duringSceneGui -= HandleSceneGui;
            SceneView.duringSceneGui += HandleSceneGui;
            DragAndDrop.RemoveDropHandlerV2(HandleHierarchyDrop);
            DragAndDrop.AddDropHandlerV2(HandleHierarchyDrop);
        }

        public static bool TryGetDraggedAssets(Object[] objectReferences, out MmdPmxAsset? pmxAsset, out MmdVmdAsset? vmdAsset)
        {
            pmxAsset = null;
            vmdAsset = null;
            if (objectReferences == null || objectReferences.Length == 0)
            {
                return false;
            }

            foreach (Object reference in objectReferences)
            {
                if (reference is MmdPmxAsset pmx)
                {
                    if (pmxAsset != null)
                    {
                        pmxAsset = null;
                        vmdAsset = null;
                        return false;
                    }

                    pmxAsset = pmx;
                    continue;
                }

                if (reference is MmdVmdAsset vmd)
                {
                    if (vmdAsset != null)
                    {
                        pmxAsset = null;
                        vmdAsset = null;
                        return false;
                    }

                    vmdAsset = vmd;
                    continue;
                }

                // D1 compatibility: resolve .pmx GameObject main object to metadata MmdPmxAsset.
                if (reference is GameObject go)
                {
                    MmdPmxAsset? resolved = MmdEditorPmxLoader.TryResolveMmdPmxAssetFromMainGameObject(go);
                    if (resolved != null)
                    {
                        if (pmxAsset != null)
                        {
                            pmxAsset = null;
                            vmdAsset = null;
                            return false; // duplicate PMX
                        }

                        pmxAsset = resolved;
                        continue;
                    }
                }

                return false;
            }

            return pmxAsset != null;
        }

        public static bool TryGetDraggedVmdAssetForExistingModel(
            Object[] objectReferences,
            string[] paths,
            out MmdVmdAsset? vmdAsset)
        {
            vmdAsset = null;
            if (ContainsRawMmdPath(paths))
            {
                return false;
            }

            if (objectReferences == null || objectReferences.Length == 0)
            {
                return false;
            }

            foreach (Object reference in objectReferences)
            {
                if (reference is MmdVmdAsset vmd)
                {
                    if (vmdAsset != null)
                    {
                        vmdAsset = null;
                        return false;
                    }

                    vmdAsset = vmd;
                    continue;
                }

                vmdAsset = null;
                return false;
            }

            return vmdAsset != null;
        }

        public static bool TryGetDraggedSources(
            Object[] objectReferences,
            string[] paths,
            out MmdPmxAsset? pmxAsset,
            out MmdVmdAsset? vmdAsset,
            out string? pmxPath)
        {
            return TryGetDraggedSources(objectReferences, paths, out pmxAsset, out vmdAsset, out pmxPath, out _);
        }

        public static bool TryGetDraggedSources(
            Object[] objectReferences,
            string[] paths,
            out MmdPmxAsset? pmxAsset,
            out MmdVmdAsset? vmdAsset,
            out string? pmxPath,
            out string? vmdPath)
        {
            pmxPath = null;
            vmdPath = null;
            if (TryGetDraggedAssets(objectReferences, out pmxAsset, out vmdAsset))
            {
                if (ContainsRawMmdPath(paths))
                {
                    pmxAsset = null;
                    vmdAsset = null;
                    return false;
                }

                return true;
            }

            pmxAsset = null;
            vmdAsset = null;
            if (ContainsMmdAssetReference(objectReferences))
            {
                return false;
            }

            if (paths == null || paths.Length == 0)
            {
                return false;
            }

            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }

                string extension = Path.GetExtension(path);
                if (string.Equals(extension, ".pmx", StringComparison.OrdinalIgnoreCase))
                {
                    if (pmxPath != null)
                    {
                        return false;
                    }

                    string fullPath = Path.GetFullPath(path);
                    if (!File.Exists(fullPath))
                    {
                        return false;
                    }

                    pmxPath = fullPath;
                    continue;
                }

                if (string.Equals(extension, ".vmd", StringComparison.OrdinalIgnoreCase))
                {
                    if (vmdPath != null)
                    {
                        return false;
                    }

                    string fullPath = Path.GetFullPath(path);
                    if (!File.Exists(fullPath))
                    {
                        return false;
                    }

                    vmdPath = fullPath;
                    continue;
                }

                return false;
            }

            if (pmxPath == null)
            {
                vmdPath = null;
                return false;
            }

            return true;
        }

        private static bool ContainsMmdAssetReference(Object[] objectReferences)
        {
            if (objectReferences == null)
            {
                return false;
            }

            foreach (Object reference in objectReferences)
            {
                if (reference is MmdPmxAsset || reference is MmdVmdAsset)
                {
                    return true;
                }

                // D1 compatibility: .pmx GameObject main object counts as MMD asset reference.
                if (reference is GameObject go)
                {
                    if (MmdEditorPmxLoader.TryResolveMmdPmxAssetFromMainGameObject(go) != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsRawMmdPath(string[] paths)
        {
            if (paths == null)
            {
                return false;
            }

            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                {
                    continue;
                }

                string extension = Path.GetExtension(path);
                if (string.Equals(extension, ".pmx", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".vmd", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static Vector3 CalculateGroundDropPosition(Ray worldRay)
        {
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(worldRay, out float distance))
            {
                return worldRay.GetPoint(distance);
            }

            return Vector3.zero;
        }

        public static Vector3 CalculateHierarchyDropPosition(GameObject? parent)
        {
            return parent == null ? Vector3.zero : parent.transform.position;
        }

        public static GameObject? GetHierarchyDropParent(int instanceId)
        {
            return InstanceIdToObjectMethod?.Invoke(null, new object[] { instanceId }) as GameObject;
        }

        public static GameObject? GetHierarchyDropParent(EntityId entityId)
        {
            return EditorUtility.EntityIdToObject(entityId) as GameObject;
        }

        public static bool TryAttachVmdSourceToExistingModel(
            GameObject? target,
            MmdVmdAsset? vmdAsset)
        {
            if (target == null || vmdAsset == null)
            {
                return false;
            }

            // Pure VMD attach path: controller owns the motion asset.
            MmdUnityPlaybackController? controller = target.GetComponent<MmdUnityPlaybackController>()
                ?? target.GetComponentInParent<MmdUnityPlaybackController>();
            if (controller == null || controller.ModelAssetSource == null)
            {
                return false;
            }

            MmdPlaybackConfig config = MmdUnityPlaybackControllerEditor.ResolvePlaybackConfigForNewSource(controller);

            Undo.RecordObject(controller, "Attach MMD VMD Source");
            controller.ConfigureMotionAsset(vmdAsset);
            controller.SetPlayOnStart(config.PlayOnStart);
            EditorUtility.SetDirty(controller);

            Selection.activeGameObject = controller.gameObject;
            Debug.LogFormat(
                "Attached VMD asset to existing MMD model source: target={0}; pmx={1}; vmd={2}",
                controller.name,
                controller.ModelSourceId,
                string.IsNullOrWhiteSpace(vmdAsset.SourceId) ? vmdAsset.name : vmdAsset.SourceId);
            return true;
        }

        public static GameObject? GetHierarchyDropParent(int instanceId, HierarchyDropFlags dropMode, Transform? forcedParent)
        {
            if (forcedParent != null)
            {
                return forcedParent.gameObject;
            }

            GameObject? target = GetHierarchyDropParent(instanceId);
            if (target == null)
            {
                return null;
            }

            if ((dropMode & HierarchyDropFlags.DropUpon) == HierarchyDropFlags.DropUpon)
            {
                return target;
            }

            return target.transform.parent == null ? null : target.transform.parent.gameObject;
        }

        public static GameObject? GetHierarchyDropParent(EntityId entityId, HierarchyDropFlags dropMode, Transform? forcedParent)
        {
            if (forcedParent != null)
            {
                return forcedParent.gameObject;
            }

            GameObject? target = GetHierarchyDropParent(entityId);
            if (target == null)
            {
                return null;
            }

            if ((dropMode & HierarchyDropFlags.DropUpon) == HierarchyDropFlags.DropUpon)
            {
                return target;
            }

            return target.transform.parent == null ? null : target.transform.parent.gameObject;
        }

        private static void HandleSceneGui(SceneView sceneView)
        {
            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)
            {
                return;
            }

            if (!TryGetDraggedSources(DragAndDrop.objectReferences, DragAndDrop.paths, out MmdPmxAsset? pmxAsset, out MmdVmdAsset? vmdAsset, out string? pmxPath, out string? vmdPath))
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (current.type == EventType.DragUpdated)
            {
                current.Use();
                return;
            }

            DragAndDrop.AcceptDrag();
            current.Use();

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Vector3 dropPosition = CalculateGroundDropPosition(ray);
            try
            {
                GameObject root = LoadForDragAndDrop(pmxAsset, vmdAsset, pmxPath, vmdPath, dropPosition, parent: null);
                Selection.activeGameObject = root;
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to drop MMD asset into scene:" + Environment.NewLine + ex);
            }
        }

        private static DragAndDropVisualMode HandleHierarchyDrop(EntityId dropTargetEntityId, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            if (TryGetDraggedVmdAssetForExistingModel(DragAndDrop.objectReferences, DragAndDrop.paths, out MmdVmdAsset? draggedVmdAsset))
            {
                GameObject? target = GetHierarchyVmdDropTarget(dropTargetEntityId, dropMode);
                if (!CanAttachVmdSourceToExistingModel(target))
                {
                    return DragAndDropVisualMode.None;
                }

                if (!perform)
                {
                    return DragAndDropVisualMode.Copy;
                }

                try
                {
                    if (!TryAttachVmdSourceToExistingModel(target, draggedVmdAsset))
                    {
                        return DragAndDropVisualMode.None;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("Failed to attach MMD VMD asset to existing hierarchy object:" + Environment.NewLine + ex);
                    return DragAndDropVisualMode.None;
                }

                return DragAndDropVisualMode.Copy;
            }

            if (!TryGetDraggedSources(DragAndDrop.objectReferences, DragAndDrop.paths, out MmdPmxAsset? pmxAsset, out MmdVmdAsset? vmdAsset, out string? pmxPath, out string? vmdPath))
            {
                return DragAndDropVisualMode.None;
            }

            if (!perform)
            {
                return DragAndDropVisualMode.Copy;
            }

            GameObject? parent = GetHierarchyDropParent(dropTargetEntityId, dropMode, parentForDraggedObjects);
            Vector3 dropPosition = CalculateHierarchyDropPosition(parent);
            try
            {
                GameObject root = LoadForDragAndDrop(pmxAsset, vmdAsset, pmxPath, vmdPath, dropPosition, parent);
                Selection.activeGameObject = root;
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to drop MMD asset into hierarchy:" + Environment.NewLine + ex);
            }

            return DragAndDropVisualMode.Copy;
        }

        private static bool CanAttachVmdSourceToExistingModel(GameObject? target)
        {
            if (target == null)
            {
                return false;
            }

            // Can-attach for VMD motion requires a model source provider already on the controller.
            // This guards the "attach motion to existing model source provider" pattern.
            MmdUnityPlaybackController? controller = target.GetComponent<MmdUnityPlaybackController>()
                ?? target.GetComponentInParent<MmdUnityPlaybackController>();
            return controller != null && controller.ModelAssetSource != null;
        }

        private static GameObject? GetHierarchyVmdDropTarget(EntityId dropTargetEntityId, HierarchyDropFlags dropMode)
        {
            if ((dropMode & HierarchyDropFlags.DropUpon) != HierarchyDropFlags.DropUpon)
            {
                return null;
            }

            return GetHierarchyDropParent(dropTargetEntityId);
        }

        public static MmdUnityModelInstance LoadPmxForDragAndDrop(MmdPmxAsset pmxAsset, Vector3 position, GameObject? parent)
        {
            MmdUnityModelInstance instance = LoadPmxPrefabInstanceForDragAndDrop(pmxAsset)
                ?? MmdEditorPmxLoader.LoadPmxIntoScene(pmxAsset);
            MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>()
                ?? instance.Root.AddComponent<MmdUnityPlaybackController>();
            controller.ConfigureModelAsset(pmxAsset);
            EditorUtility.SetDirty(controller);
            PlaceRoot(instance.Root, position, parent == null ? null : parent.transform);
            Debug.LogFormat(
                "Dropped PMX asset into scene: source={0}; position={1}; parent={2}; vertices={3}; indices={4}; bones={5}",
                pmxAsset.SourceId,
                position,
                parent == null ? "<scene-root>" : parent.name,
                instance.VertexCount,
                instance.IndexCount,
                instance.BoneTransforms.Length);
            return instance;
        }

        private static MmdUnityModelInstance? LoadPmxPrefabInstanceForDragAndDrop(MmdPmxAsset pmxAsset)
        {
            if (pmxAsset == null || pmxAsset.ImportedRoot == null)
            {
                return null;
            }

            GameObject? instanceRoot = null;
            try
            {
                instanceRoot = PrefabUtility.InstantiatePrefab(pmxAsset.ImportedRoot) as GameObject;
                if (instanceRoot == null)
                {
                    return null;
                }

                Undo.RegisterCreatedObjectUndo(instanceRoot, "Load PMX Asset Into Scene");
                MmdModelDefinition model = pmxAsset.LoadModel();
                return MmdUnityModelFactory.CreateFromInstantiatedImportedHierarchy(
                    instanceRoot,
                    model,
                    string.IsNullOrWhiteSpace(pmxAsset.SourcePath) ? null : pmxAsset.SourcePath,
                    pmxAsset.ImportScale,
                    MmdPmxModelPresetPolicy.AllowsAutomaticSelfShadowTarget(pmxAsset.ModelPreset));
            }
            catch
            {
                if (instanceRoot != null)
                {
                    Object.DestroyImmediate(instanceRoot);
                }

                throw;
            }
        }

        public static MmdUnityModelInstance LoadPmxPathForDragAndDrop(string pmxPath, Vector3 position, GameObject? parent)
        {
            MmdUnityModelInstance instance = MmdEditorPmxLoader.LoadPmxIntoScene(pmxPath);
            MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>()
                ?? instance.Root.AddComponent<MmdUnityPlaybackController>();
            // PMX-only (raw path) drag path also establishes a model source provider via RuntimeImporter.
            // Model path only; motion path empty. Mirrors the asset-based model-source-provider creation.
            MmdRuntimeImporterComponent importer = controller.GetComponent<MmdRuntimeImporterComponent>()
                ?? controller.gameObject.AddComponent<MmdRuntimeImporterComponent>();
            importer.ConfigureModelPath(pmxPath);
            PlaceRoot(instance.Root, position, parent == null ? null : parent.transform);
            Debug.LogFormat(
                "Dropped PMX path into scene: source={0}; position={1}; parent={2}; vertices={3}; indices={4}; bones={5}",
                pmxPath,
                position,
                parent == null ? "<scene-root>" : parent.name,
                instance.VertexCount,
                instance.IndexCount,
                instance.BoneTransforms.Length);
            return instance;
        }

        public static MmdEditorPlaybackSceneLoadResult LoadPlaybackForDragAndDrop(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            Vector3 position,
            GameObject? parent)
        {
            // Bundled PMX+VMD drag path delegates to loader/facade which stores source refs on the controller.
            MmdEditorPlaybackSceneLoadResult result = MmdEditorPlaybackLoader.LoadPlaybackIntoScene(pmxAsset, vmdAsset);
            PlaceRoot(result.Instance.Root, position, parent == null ? null : parent.transform);
            Debug.LogFormat(
                "Dropped PMX+VMD assets into scene: pmx={0}; vmd={1}; position={2}; parent={3}; frameRate={4}; vertices={5}; indices={6}; bones={7}; maxFrame={8}",
                pmxAsset.SourceId,
                vmdAsset.SourceId,
                position,
                parent == null ? "<scene-root>" : parent.name,
                result.Controller.FrameRate,
                result.Instance.VertexCount,
                result.Instance.IndexCount,
                result.Instance.BoneTransforms.Length,
                result.Motion.maxFrame);
            return result;
        }

        public static MmdEditorPlaybackSceneLoadResult LoadPlaybackPathForDragAndDrop(
            string pmxPath,
            string vmdPath,
            Vector3 position,
            GameObject? parent)
        {
            MmdEditorPlaybackSceneLoadResult result = MmdEditorPlaybackLoader.LoadPlaybackIntoScene(pmxPath, vmdPath);
            PlaceRoot(result.Instance.Root, position, parent == null ? null : parent.transform);
            Debug.LogFormat(
                "Dropped PMX+VMD paths into scene: pmx={0}; vmd={1}; position={2}; parent={3}; frameRate={4}; vertices={5}; indices={6}; bones={7}; maxFrame={8}",
                result.ModelPath,
                result.MotionPath,
                position,
                parent == null ? "<scene-root>" : parent.name,
                result.Controller.FrameRate,
                result.Instance.VertexCount,
                result.Instance.IndexCount,
                result.Instance.BoneTransforms.Length,
                result.Motion.maxFrame);
            return result;
        }

        private static GameObject LoadForDragAndDrop(
            MmdPmxAsset? pmxAsset,
            MmdVmdAsset? vmdAsset,
            string? pmxPath,
            string? vmdPath,
            Vector3 position,
            GameObject? parent)
        {
            if (pmxAsset != null)
            {
                // PMX-only stores the model source on the controller.
                // PMX+VMD bundled stores both source assets on the controller.
                // Pure VMD attach stores motion on the existing controller.
                return vmdAsset == null
                    ? LoadPmxForDragAndDrop(pmxAsset, position, parent).Root
                    : LoadPlaybackForDragAndDrop(pmxAsset, vmdAsset, position, parent).Instance.Root;
            }

            if (!string.IsNullOrWhiteSpace(pmxPath))
            {
                string resolvedPmxPath = pmxPath!;
                // Same split for raw paths: model-only provider vs full playback provider.
                if (string.IsNullOrWhiteSpace(vmdPath))
                {
                    return LoadPmxPathForDragAndDrop(resolvedPmxPath, position, parent).Root;
                }

                string resolvedVmdPath = vmdPath!;
                return LoadPlaybackPathForDragAndDrop(resolvedPmxPath, resolvedVmdPath, position, parent).Instance.Root;
            }

            throw new ArgumentException("Dragged PMX source is required.");
        }

        private static void PlaceRoot(GameObject root, Vector3 position, Transform? parent)
        {
            if (parent == null)
            {
                root.transform.position = position;
                return;
            }

            Undo.SetTransformParent(root.transform, parent, "Drop MMD Asset Into Hierarchy");
            root.transform.localPosition = Vector3.zero;
        }
    }
}
