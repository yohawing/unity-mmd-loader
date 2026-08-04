#nullable enable

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
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
