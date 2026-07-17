#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;
using Mmd.Timeline;

namespace Mmd.Editor.Timeline
{
    [CustomTimelineEditor(typeof(MmdHumanoidAnimationClip))]
    public sealed class MmdHumanoidAnimationClipEditor : ClipEditor
    {
        public override void OnCreate(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom)
        {
            ApplyDurationFromAnimationClip(clip);
        }

        public static bool ApplyDurationFromAnimationClip(TimelineClip clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            if (clip.asset is not MmdHumanoidAnimationClip humanoidClip ||
                humanoidClip.clip == null ||
                humanoidClip.clip.length <= 0.0f)
            {
                return false;
            }

            clip.duration = humanoidClip.clip.length;
            return true;
        }
    }

    [CustomEditor(typeof(MmdHumanoidAnimationClip))]
    public sealed class MmdHumanoidAnimationClipInspector : UnityEditor.Editor
    {
        private static readonly GUIContent AnimationClipLabel = new("Animation Clip");

        private SerializedProperty? clipProperty;

        private void OnEnable()
        {
            clipProperty = serializedObject.FindProperty("clip");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            if (clipProperty != null)
            {
                EditorGUILayout.PropertyField(clipProperty, AnimationClipLabel);
            }

            bool clipChanged = EditorGUI.EndChangeCheck();
            List<TimelineClip> owningClips = clipChanged ? FindSelectedOwningClips() : new List<TimelineClip>();
            foreach (TimelineClip timelineClip in owningClips)
            {
                TrackAsset? parentTrack = timelineClip.GetParentTrack();
                if (parentTrack != null)
                {
                    Undo.RegisterCompleteObjectUndo(parentTrack, "Assign MMD Humanoid Animation Clip");
                }
            }

            serializedObject.ApplyModifiedProperties();

            if (!clipChanged)
            {
                return;
            }

            foreach (TimelineClip timelineClip in owningClips)
            {
                if (!MmdHumanoidAnimationClipEditor.ApplyDurationFromAnimationClip(timelineClip))
                {
                    continue;
                }

                TrackAsset? parentTrack = timelineClip.GetParentTrack();
                if (parentTrack != null)
                {
                    EditorUtility.SetDirty(parentTrack);
                }
            }

            TimelineEditor.Refresh(RefreshReason.ContentsModified);
        }

        private List<TimelineClip> FindSelectedOwningClips()
        {
            var result = new List<TimelineClip>();
            foreach (TimelineClip timelineClip in TimelineEditor.selectedClips)
            {
                if (ReferenceEquals(timelineClip.asset, target))
                {
                    result.Add(timelineClip);
                }
            }

            return result;
        }
    }
}
