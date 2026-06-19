#nullable enable

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
    public static class MmdEditorWorkflow
    {
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
