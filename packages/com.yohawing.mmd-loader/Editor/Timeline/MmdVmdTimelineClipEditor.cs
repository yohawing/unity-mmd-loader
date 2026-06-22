#nullable enable

using System;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;
using Mmd.Timeline;

namespace Mmd.Editor.Timeline
{
    [CustomTimelineEditor(typeof(MmdVmdTimelineClip))]
    public sealed class MmdVmdTimelineClipEditor : ClipEditor
    {
        public override void OnCreate(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom)
        {
            ApplyDurationFromMotionAsset(clip);
        }

        public static bool ApplyDurationFromMotionAsset(TimelineClip clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            if (clip.asset is not MmdVmdTimelineClip mmdClip || mmdClip.MotionAsset == null)
            {
                return false;
            }

            clip.duration = MmdTimelineAssetWorkflow.CalculateClipDurationSeconds(
                mmdClip.MotionAsset.MaxFrame,
                mmdClip.FrameRate);
            return true;
        }
    }

    [CustomEditor(typeof(MmdVmdTimelineClip))]
    public sealed class MmdVmdTimelineClipInspector : UnityEditor.Editor
    {
        private const string MotionAssetField = "motionAsset";
        private const string FrameRateField = "frameRate";

        private static readonly GUIContent MotionAssetLabel = new("Motion Asset");
        private static readonly GUIContent DurationLabel = new("Duration");
        private static readonly GUIContent MaxFrameLabel = new("Max Frame");
        private static readonly GUIContent SourceIdLabel = new("Motion Source Id");

        private SerializedProperty? motionAssetProperty;
        private SerializedProperty? frameRateProperty;

        private void OnEnable()
        {
            motionAssetProperty = serializedObject.FindProperty(MotionAssetField);
            frameRateProperty = serializedObject.FindProperty(FrameRateField);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (motionAssetProperty != null)
            {
                EditorGUILayout.PropertyField(motionAssetProperty, MotionAssetLabel);
            }

            DrawMotionDiagnostics();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMotionDiagnostics()
        {
            if (motionAssetProperty?.objectReferenceValue is not MmdVmdAsset motionAsset)
            {
                EditorGUILayout.HelpBox(
                    "Assign a VMD Motion Asset. Controller binding belongs to the Timeline track.",
                    MessageType.Info);
                return;
            }

            if (motionAsset.ImportSummaryStatus != MmdVmdImportSummaryStatus.Passed)
            {
                EditorGUILayout.HelpBox(
                    $"VMD import summary is {motionAsset.ImportSummaryStatus}; reimport the asset to refresh cached Timeline diagnostics.",
                    MessageType.Warning);
            }

            float frameRate = frameRateProperty?.floatValue ?? 30.0f;
            double duration = MmdTimelineAssetWorkflow.CalculateClipDurationSeconds(motionAsset.MaxFrame, frameRate);
            string sourceId = string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(SourceIdLabel, sourceId);
                EditorGUILayout.IntField(MaxFrameLabel, motionAsset.MaxFrame);
                EditorGUILayout.TextField(DurationLabel, $"{duration:0.###} s @ {frameRate:0.###} fps");
            }
        }
    }
}
