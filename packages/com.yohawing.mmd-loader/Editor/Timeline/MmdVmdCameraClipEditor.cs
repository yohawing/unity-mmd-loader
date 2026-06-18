#nullable enable

using System;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;
using Mmd.Timeline;

namespace Mmd.Editor.Timeline
{
    [CustomTimelineEditor(typeof(MmdVmdCameraClip))]
    public sealed class MmdVmdCameraClipEditor : ClipEditor
    {
        public override void OnCreate(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom)
        {
            ApplyDurationFromMotionAsset(clip);
        }

        // Sizes the clip to the VMD motion length, mirroring MmdVmdTimelineClipEditor. Returns false
        // when there is no motion asset to measure (e.g. an empty clip created before a VMD is dropped).
        public static bool ApplyDurationFromMotionAsset(TimelineClip clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            if (clip.asset is not MmdVmdCameraClip cameraClip || cameraClip.MotionAsset == null)
            {
                return false;
            }

            // Use the import-time cached MaxFrame so creating/sizing the clip never re-parses the VMD.
            clip.duration = MmdTimelineAssetWorkflow.CalculateClipDurationSeconds(
                cameraClip.MotionAsset.MaxFrame, cameraClip.FrameRate);
            return true;
        }
    }

    [CustomEditor(typeof(MmdVmdCameraClip))]
    public sealed class MmdVmdCameraClipInspector : UnityEditor.Editor
    {
        private const string MotionAssetField = "motionAsset";
        private const string FrameRateField = "frameRate";
        private const string MinFieldOfViewField = "minFieldOfView";

        private static readonly GUIContent MotionAssetLabel = new("Motion Asset");
        private static readonly GUIContent MinFieldOfViewLabel = new("Min Field Of View");
        private static readonly GUIContent DurationLabel = new("Duration");
        private static readonly GUIContent MaxFrameLabel = new("Max Frame");
        private static readonly GUIContent CameraKeyframesLabel = new("Camera Keyframes");
        private static readonly GUIContent LightKeyframesLabel = new("Light Keyframes");
        private static readonly GUIContent SourceIdLabel = new("Motion Source Id");

        private SerializedProperty? motionAssetProperty;
        private SerializedProperty? frameRateProperty;
        private SerializedProperty? minFieldOfViewProperty;

        private void OnEnable()
        {
            motionAssetProperty = serializedObject.FindProperty(MotionAssetField);
            frameRateProperty = serializedObject.FindProperty(FrameRateField);
            minFieldOfViewProperty = serializedObject.FindProperty(MinFieldOfViewField);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (motionAssetProperty != null)
            {
                EditorGUILayout.PropertyField(motionAssetProperty, MotionAssetLabel);
            }

            if (minFieldOfViewProperty != null)
            {
                EditorGUILayout.PropertyField(minFieldOfViewProperty, MinFieldOfViewLabel);
            }

            DrawMotionDiagnostics();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMotionDiagnostics()
        {
            if (motionAssetProperty?.objectReferenceValue is not MmdVmdAsset motionAsset)
            {
                EditorGUILayout.HelpBox(
                    "Assign a camera VMD Motion Asset. The Scene Environment binding belongs to the Timeline track.",
                    MessageType.Info);
                return;
            }

            // Counts are read from the import-time cached summary (no full parse). The track drives
            // BOTH camera and light from the same VMD, so it is only a true no-op when neither is
            // present; a light-only VMD still drives the light lane.
            bool hasCamera = motionAsset.CameraKeyframeCount > 0;
            bool hasLight = motionAsset.LightKeyframeCount > 0;
            if (!hasCamera && !hasLight)
            {
                EditorGUILayout.HelpBox(
                    "This VMD has no camera or light keyframes; the track will be a no-op.",
                    MessageType.Warning);
            }
            else if (!hasCamera)
            {
                EditorGUILayout.HelpBox(
                    "This VMD has no camera keyframes; only the light lane is driven.",
                    MessageType.Info);
            }

            // Diagnostics read EXCLUSIVELY from the import-time cached summary (MaxFrame /
            // Camera/Light keyframe counts) so repainting the Inspector never re-parses the VMD.
            float frameRate = frameRateProperty?.floatValue ?? 30.0f;
            double duration = MmdTimelineAssetWorkflow.CalculateClipDurationSeconds(motionAsset.MaxFrame, frameRate);
            string sourceId = string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(SourceIdLabel, sourceId);
                EditorGUILayout.IntField(CameraKeyframesLabel, motionAsset.CameraKeyframeCount);
                EditorGUILayout.IntField(LightKeyframesLabel, motionAsset.LightKeyframeCount);
                EditorGUILayout.IntField(MaxFrameLabel, motionAsset.MaxFrame);
                EditorGUILayout.TextField(DurationLabel, $"{duration:0.###} s @ {frameRate:0.###} fps");
            }
        }
    }
}
