#nullable enable

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd.Editor.Timeline;
using Mmd.Physics;
using Mmd.Timeline;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Editor
{
    public sealed class MmdEditorWindow : EditorWindow
    {
        private const string MenuPath = "Tools/MMD Loader/Open MMD Loader Window";

        private MmdEditorSelectionSnapshot snapshot = MmdEditorSelectionSnapshot.Empty;
        private bool snapshotDirty = true;
        private int selectedTab;
        private string lastStatus = "No action yet";
        private string lastError = string.Empty;

        [MenuItem(MenuPath, priority = -100)]
        public static void OpenWindow()
        {
            GetWindow<MmdEditorWindow>("MMD Loader");
        }

        [MenuItem("Assets/MMD Loader/Add VMD Clip to selected Timeline")]
        private static void AddVmdClipToSelectedTimeline()
        {
            try
            {
                MmdEditorSelectionSnapshot snapshot = BuildSelectionSnapshot(Selection.objects, Selection.activeGameObject);
                if (!CanCreateTimelineClip(snapshot))
                {
                    EditorUtility.DisplayDialog("Add VMD Clip to selected Timeline",
                        "Cannot create VMD Timeline clip. Requires: a VMD asset selected, a PlayableDirector with TimelineAsset, and an MmdUnityPlaybackController with HasModelSource.",
                        "OK");
                    return;
                }

                var result = ExecuteCreateTimelineClip(
                    snapshot.VmdAsset!,
                    snapshot.Director!,
                    snapshot.Controller!,
                    snapshot.TimelineAsset!,
                    useUndo: true);

                if (!result.success)
                {
                    EditorUtility.DisplayDialog("Add VMD Clip to selected Timeline", result.error, "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("MMD Add VMD Clip to selected Timeline failed:" + Environment.NewLine + ex);
                EditorUtility.DisplayDialog("Add VMD Clip to selected Timeline Failed", ex.Message, "OK");
            }
        }

        [MenuItem("Assets/MMD Loader/Add VMD Clip to selected Timeline", true)]
        private static bool AddVmdClipToSelectedTimelineValidate()
        {
            MmdEditorSelectionSnapshot snapshot = BuildSelectionSnapshot(Selection.objects, Selection.activeGameObject);
            return CanCreateTimelineClip(snapshot);
        }

        public static MmdEditorSelectionSnapshot BuildSelectionSnapshot(Object[] selection, GameObject? activeGameObject)
        {
            MmdPmxAsset? pmxAsset = null;
            MmdVmdAsset? vmdAsset = null;
            PlayableDirector? director = null;
            TimelineAsset? timelineAsset = null;
            GameObject? sceneObject = activeGameObject;

            if (selection != null)
            {
                foreach (Object selected in selection)
                {
                    if (selected == null)
                    {
                        continue;
                    }

                    // D1 compatibility: if the selected object is a GameObject and its asset
                    // path is a .pmx, resolve the metadata MmdPmxAsset sub-asset from the
                    // main object so snapshot.PmxAsset is populated correctly.
                    MmdPmxAsset? candidatePmxAsset = selected as MmdPmxAsset
                        ?? MmdEditorPmxLoader.TryResolveMmdPmxAssetFromMainGameObject(selected);
                    pmxAsset ??= candidatePmxAsset;
                    vmdAsset ??= selected as MmdVmdAsset;
                    timelineAsset ??= selected as TimelineAsset;

                    if (sceneObject == null && selected is GameObject selectedGameObject)
                    {
                        sceneObject = selectedGameObject;
                    }
                }
            }

            MmdUnityPlaybackController? controller = ResolveComponent<MmdUnityPlaybackController>(sceneObject);
            MmdRuntimeImporterComponent? runtimeImporter = ResolveComponent<MmdRuntimeImporterComponent>(sceneObject);
            director = ResolveComponent<PlayableDirector>(sceneObject);
            if (timelineAsset == null && director?.playableAsset is TimelineAsset directorTimeline)
            {
                timelineAsset = directorTimeline;
            }

            return new MmdEditorSelectionSnapshot(
                pmxAsset,
                vmdAsset,
                controller,
                director,
                timelineAsset,
                runtimeImporter);
        }

        public static bool CanCreateTimelineClip(MmdEditorSelectionSnapshot selectionSnapshot)
        {
            return selectionSnapshot.VmdAsset != null
                && selectionSnapshot.Controller != null
                && selectionSnapshot.Controller.HasModelSource
                && selectionSnapshot.Director != null
                && selectionSnapshot.TimelineAsset != null;
        }

        public static bool CanCreatePlaybackSource(MmdEditorSelectionSnapshot selectionSnapshot)
        {
            // "Create Playback Source" action (button + Execute) is the full PMX+VMD bundled source creation.
            // It requires both assets + controller, then writes controller-owned source references.
            // This is distinct from PMX-only model-source-provider creation (model only, Motion null, IsConfigured false).
            return selectionSnapshot.PmxAsset != null
                && selectionSnapshot.VmdAsset != null
                && selectionSnapshot.Controller != null;
        }

        public static bool CanCreatePlaybackConfig(MmdEditorSelectionSnapshot selectionSnapshot)
        {
            return selectionSnapshot.RuntimeImporter != null
                || selectionSnapshot.Controller != null;
        }

        internal static (bool success, string status, string error) ExecuteCreatePlaybackSource(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            MmdUnityPlaybackController controller,
            bool useUndo)
        {
            if (pmxAsset == null)
            {
                return (false, "Create Playback Source skipped", "PMX asset is required.");
            }

            if (vmdAsset == null)
            {
                return (false, "Create Playback Source skipped", "VMD asset is required.");
            }

            if (controller == null)
            {
                return (false, "Create Playback Source skipped", "Playback controller is required.");
            }

            // Full bundled PMX+VMD source creation ("Create Playback Source" action).
            MmdPlaybackConfig config = MmdUnityPlaybackControllerEditor.ResolvePlaybackConfigForNewSource(controller);
            if (useUndo)
            {
                Undo.RecordObject(controller, "Create MMD Playback Source");
            }

            bool created = controller.ModelAssetSource == null && controller.MotionAssetSource == null;
            controller.ConfigureModelAsset(pmxAsset);
            controller.ConfigureMotionAsset(vmdAsset);
            controller.SetPlayOnStart(config.PlayOnStart);
            controller.SetPhysicsMode(MmdPhysicsMode.Live);
            EditorUtility.SetDirty(controller);

            return (true, created ? "Playback source created (controller model+motion)" : "Playback source updated (controller model+motion)", string.Empty);
        }

        internal static (bool success, string status, string error) ExecuteCreateTimelineClip(
            MmdVmdAsset vmdAsset,
            PlayableDirector director,
            MmdUnityPlaybackController controller,
            TimelineAsset timelineAsset,
            bool useUndo)
        {
            if (vmdAsset == null)
            {
                return (false, "Create VMD Timeline Clip skipped", "VMD asset is required.");
            }

            if (director == null)
            {
                return (false, "Create VMD Timeline Clip skipped", "PlayableDirector is required.");
            }

            if (controller == null)
            {
                return (false, "Create VMD Timeline Clip skipped", "Playback controller is required.");
            }

            if (timelineAsset == null)
            {
                return (false, "Create VMD Timeline Clip skipped", "TimelineAsset is required.");
            }

            if (!controller.HasModelSource)
            {
                return (false, "Create VMD Timeline Clip skipped", "Playback controller needs a PMX model source.");
            }

            if (useUndo)
            {
                Undo.RegisterCompleteObjectUndo(
                    new Object[] { timelineAsset, director },
                    "Create MMD VMD Timeline Clip");
            }

            MmdVmdTimelineTrack track = MmdTimelineAssetWorkflow.FindFirstMmdVmdTrack(timelineAsset)
                ?? MmdTimelineAssetWorkflow.CreateVmdTrack(timelineAsset, director, controller);
            TimelineClip clip = MmdTimelineAssetWorkflow.CreateVmdClip(
                track,
                vmdAsset,
                controller,
                director: director);

            EditorUtility.SetDirty(timelineAsset);
            EditorUtility.SetDirty(director);
            if (director.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            }

            Selection.activeObject = clip.asset as Object;
            return (true, "Created VMD Timeline clip: " + clip.displayName, string.Empty);
        }

        private void OnEnable()
        {
            RefreshSnapshot();
        }

        private void OnSelectionChange()
        {
            RefreshSnapshot();
            Repaint();
        }

        private void OnHierarchyChange()
        {
            snapshotDirty = true;
            Repaint();
        }

        private void OnProjectChange()
        {
            snapshotDirty = true;
            Repaint();
        }

        private void OnGUI()
        {
            if (snapshotDirty)
            {
                RefreshSnapshot();
            }

            EditorGUILayout.LabelField("MMD Loader", EditorStyles.boldLabel);
            DrawSelectionSummary();

            EditorGUILayout.Space();
            selectedTab = GUILayout.Toolbar(selectedTab, new[] { "Overview", "Materials" });
            EditorGUILayout.Space();

            switch (selectedTab)
            {
                case 0:
                    DrawOverviewTab();
                    break;
                default:
                    DrawMaterialsTab();
                    break;
            }

            EditorGUILayout.Space();
            DrawActions();
            DrawStatus();
        }

        private void RefreshSnapshot()
        {
            snapshot = BuildSelectionSnapshot(Selection.objects, Selection.activeGameObject);
            snapshotDirty = false;
        }

        private void DrawSelectionSummary()
        {
            EditorGUILayout.LabelField("PMX", SnapshotLabel(snapshot.PmxAsset));
            EditorGUILayout.LabelField("VMD", SnapshotLabel(snapshot.VmdAsset));
            EditorGUILayout.LabelField("Controller", snapshot.Controller != null ? snapshot.Controller.name : "(none)");
            EditorGUILayout.LabelField("Timeline", snapshot.TimelineAsset != null ? snapshot.TimelineAsset.name : "(none)");
        }

        private void DrawOverviewTab()
        {
            if (snapshot.PmxAsset == null)
            {
                EditorGUILayout.HelpBox("Select an imported PMX asset to inspect model import state.", MessageType.Info);
                return;
            }

            MmdPmxAsset pmxAsset = snapshot.PmxAsset;
            EditorGUILayout.LabelField("Source ID", string.IsNullOrWhiteSpace(pmxAsset.SourceId) ? pmxAsset.name : pmxAsset.SourceId);
            EditorGUILayout.LabelField("Source Path", string.IsNullOrWhiteSpace(pmxAsset.SourcePath) ? "(unavailable)" : pmxAsset.SourcePath);
            EditorGUILayout.LabelField("Import Scale", pmxAsset.ImportScale.ToString("G9", System.Globalization.CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Bytes", pmxAsset.ByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Import Summary", pmxAsset.ImportSummaryStatus.ToString());
            EditorGUILayout.LabelField("Vertices", pmxAsset.VertexCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Materials", pmxAsset.MaterialCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Bones", pmxAsset.BoneCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Morphs", pmxAsset.MorphCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Rigidbodies", pmxAsset.RigidbodyCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Joints", pmxAsset.JointCount.ToString(System.Globalization.CultureInfo.InvariantCulture));

            if (snapshot.Controller != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Playback Controller", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Configured", snapshot.Controller.IsConfigured ? "yes" : "no");
                EditorGUILayout.LabelField("Model Source", snapshot.Controller.ModelSourceId);
                EditorGUILayout.LabelField("Motion Source", snapshot.Controller.MotionSourceId);
                EditorGUILayout.LabelField("Frame", snapshot.Controller.CurrentFrame.ToString(System.Globalization.CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Frame Rate", snapshot.Controller.FrameRate.ToString("G9", System.Globalization.CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Physics", snapshot.Controller.PhysicsMode.ToString());
            }
        }

        private void DrawMaterialsTab()
        {
            if (snapshot.PmxAsset != null)
            {
                MmdPmxAsset pmxAsset = snapshot.PmxAsset;
                EditorGUILayout.LabelField("Cached Import Summary", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Shader Preset", pmxAsset.ShaderPreset);
                EditorGUILayout.HelpBox(
                    "Shader preset is currently an import summary only. Runtime material generation still uses the package default shader selection.",
                    MessageType.Info);
                EditorGUILayout.LabelField("Materials", pmxAsset.MaterialCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Diffuse Texture Refs", pmxAsset.DiffuseTextureReferenceCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Sphere Texture Refs", pmxAsset.SphereTextureReferenceCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Toon Texture Refs", pmxAsset.ToonTextureReferenceCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Transparent Materials", pmxAsset.TransparentMaterialCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Edge Materials", pmxAsset.EdgeMaterialCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                EditorGUILayout.Space();
                MmdAssetInspectorUtility.DrawOutlineSummary(pmxAsset);
                EditorGUILayout.Space();
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(snapshot.PmxAsset == null))
            {
                if (GUILayout.Button("Load PMX Into Scene"))
                {
                    LoadPmxIntoScene();
                }
            }

            bool canCreatePrefab = snapshot.PmxAsset != null && AssetDatabase.Contains(snapshot.PmxAsset);
            using (new EditorGUI.DisabledScope(!canCreatePrefab))
            {
                if (GUILayout.Button("Create Prefab"))
                {
                    CreatePrefab();
                }
            }

            if (snapshot.PmxAsset != null && !AssetDatabase.Contains(snapshot.PmxAsset))
            {
                EditorGUILayout.HelpBox("Prefab export requires an imported PMX asset under the AssetDatabase.", MessageType.Warning);
            }

            // "Create Playback Setup" and "Create Playback Config" buttons removed:
            // these assets are advanced/deferred runtime-read helpers, not primary creation surfaces.
            // Explicit advanced creation surfaces are intentionally kept out of this product window.

            bool canCreatePlaybackSource = CanCreatePlaybackSource(snapshot);
            using (new EditorGUI.DisabledScope(!canCreatePlaybackSource))
            {
                // "Create Playback Source" button is the full bundled (PMX + VMD) source action.
                // PMX-only model source provider is created by "Load PMX Into Scene" (or drag) on the controller.
                if (GUILayout.Button("Create Playback Source"))
                {
                    CreatePlaybackSource();
                }
            }

            bool canCreateTimelineClip = CanCreateTimelineClip(snapshot);
            using (new EditorGUI.DisabledScope(!canCreateTimelineClip))
            {
                if (GUILayout.Button("Create VMD Timeline Clip"))
                {
                    CreateTimelineClip();
                }
            }

            if (snapshot.VmdAsset != null
                && snapshot.Controller != null
                && !snapshot.Controller.HasModelSource)
            {
                EditorGUILayout.HelpBox(
                    "Timeline clip creation requires a playback controller with a PMX model source.",
                    MessageType.Warning);
            }

            // Playback preview is an advanced/debug surface and is intentionally kept out of this product window.
            // Product golden path (PMX import -> scene placement -> VMD Timeline -> optional Humanoid bake) does not require it.
        }

        private void DrawStatus()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", lastStatus);
            if (!string.IsNullOrWhiteSpace(lastError))
            {
                EditorGUILayout.HelpBox(lastError, MessageType.Error);
            }
        }

        private void LoadPmxIntoScene()
        {
            if (snapshot.PmxAsset == null)
            {
                return;
            }

            try
            {
                MmdUnityModelInstance instance = MmdEditorPmxLoader.LoadPmxIntoScene(snapshot.PmxAsset);
                Selection.activeGameObject = instance.Root;
                lastStatus = "Loaded PMX into scene: " + instance.Root.name;
                lastError = string.Empty;
                snapshotDirty = true;
            }
            catch (Exception ex)
            {
                lastStatus = "Load PMX failed";
                lastError = ex.Message;
                Debug.LogError("MMD EditorWindow PMX load failed:" + Environment.NewLine + ex);
                EditorUtility.DisplayDialog("MMD PMX Load Failed", ex.Message, "OK");
            }
        }

        private void CreatePrefab()
        {
            if (snapshot.PmxAsset == null)
            {
                return;
            }

            try
            {
                string path = MmdPmxPrefabExporter.GetDefaultPrefabPath(snapshot.PmxAsset);
                MmdPmxPrefabExportResult result = MmdPmxPrefabExporter.CreatePrefab(snapshot.PmxAsset, path);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabAssetPath);
                lastStatus = "Created prefab: " + result.PrefabAssetPath;
                lastError = string.Empty;
                snapshotDirty = true;
            }
            catch (Exception ex)
            {
                lastStatus = "Create Prefab failed";
                lastError = ex.Message;
                Debug.LogError("MMD EditorWindow prefab export failed:" + Environment.NewLine + ex);
                EditorUtility.DisplayDialog("MMD PMX Prefab Export Failed", ex.Message, "OK");
            }
        }

        private void CreatePlaybackSource()
        {
            if (!CanCreatePlaybackSource(snapshot))
            {
                return;
            }

            try
            {
                // Explicit full bundled PMX+VMD source creation.
                var result = ExecuteCreatePlaybackSource(
                    snapshot.PmxAsset!,
                    snapshot.VmdAsset!,
                    snapshot.Controller!,
                    useUndo: true);
                lastStatus = result.status;
                lastError = result.error;
                snapshotDirty = true;
                Repaint();
            }
            catch (Exception ex)
            {
                lastStatus = "Create Playback Source failed";
                lastError = ex.Message;
                Debug.LogError("MMD EditorWindow playback source creation failed:" + Environment.NewLine + ex);
                EditorUtility.DisplayDialog("MMD Playback Source Failed", ex.Message, "OK");
            }
        }

        private void CreateTimelineClip()
        {
            if (!CanCreateTimelineClip(snapshot))
            {
                return;
            }

            try
            {
                var result = ExecuteCreateTimelineClip(
                    snapshot.VmdAsset!,
                    snapshot.Director!,
                    snapshot.Controller!,
                    snapshot.TimelineAsset!,
                    useUndo: true);
                lastStatus = result.status;
                lastError = result.error;
                snapshotDirty = true;
            }
            catch (Exception ex)
            {
                lastStatus = "Create VMD Timeline Clip failed";
                lastError = ex.Message;
                Debug.LogError("MMD EditorWindow timeline clip creation failed:" + Environment.NewLine + ex);
                EditorUtility.DisplayDialog("MMD Timeline Clip Creation Failed", ex.Message, "OK");
            }
        }

        private static string SnapshotLabel(Object? value)
        {
            return value != null ? value.name : "(none)";
        }

        private static T? ResolveComponent<T>(GameObject? gameObject)
            where T : Component
        {
            if (gameObject == null)
            {
                return null;
            }

            T? component = gameObject.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            component = gameObject.GetComponentInParent<T>();
            return component != null ? component : gameObject.GetComponentInChildren<T>();
        }
    }

    public readonly struct MmdEditorSelectionSnapshot
    {
        public static readonly MmdEditorSelectionSnapshot Empty = new(null, null, null, null, null, null);

        public MmdEditorSelectionSnapshot(
            MmdPmxAsset? pmxAsset,
            MmdVmdAsset? vmdAsset,
            MmdUnityPlaybackController? controller,
            PlayableDirector? director,
            TimelineAsset? timelineAsset,
            MmdRuntimeImporterComponent? runtimeImporter = null)
        {
            PmxAsset = pmxAsset;
            VmdAsset = vmdAsset;
            Controller = controller;
            Director = director;
            TimelineAsset = timelineAsset;
            RuntimeImporter = runtimeImporter;
        }

        public MmdPmxAsset? PmxAsset { get; }

        public MmdVmdAsset? VmdAsset { get; }

        public MmdUnityPlaybackController? Controller { get; }

        public PlayableDirector? Director { get; }

        public TimelineAsset? TimelineAsset { get; }

        public MmdRuntimeImporterComponent? RuntimeImporter { get; }
    }
}
