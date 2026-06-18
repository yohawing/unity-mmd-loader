#nullable enable

using System;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;
using Yohawing.MmdUnity.Parser;
using Yohawing.MmdUnity.Timeline;

namespace Yohawing.MmdUnity.Editor.Timeline
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

            MmdMotionDefinition motion = mmdClip.MotionAsset.LoadMotion();
            clip.duration = MmdTimelineAssetWorkflow.CalculateClipDurationSeconds(motion, mmdClip.FrameRate);
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

            try
            {
                MmdMotionDefinition motion = motionAsset.LoadMotion();
                float frameRate = frameRateProperty?.floatValue ?? 30.0f;
                double duration = MmdTimelineAssetWorkflow.CalculateClipDurationSeconds(motion, frameRate);
                string sourceId = string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId;

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField(SourceIdLabel, sourceId);
                    EditorGUILayout.IntField(MaxFrameLabel, motion.maxFrame);
                    EditorGUILayout.TextField(DurationLabel, $"{duration:0.###} s @ {frameRate:0.###} fps");
                }
            }
            catch (Exception ex)
            {
                EditorGUILayout.HelpBox($"Motion diagnostics unavailable: {ex.Message}", MessageType.Warning);
            }
        }
    }
}
