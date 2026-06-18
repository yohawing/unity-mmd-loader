#nullable enable

using System;
using UnityEditor;
using UnityEngine;
using Yohawing.MmdUnity.Physics;
using Yohawing.MmdUnity.UnityIntegration;

namespace Yohawing.MmdUnity.Editor
{
    [CustomEditor(typeof(MmdUnityPlaybackController))]
    public sealed class MmdUnityPlaybackControllerEditor : UnityEditor.Editor
    {
        public const string PhysicsModeFieldName = "physicsMode";
        public const string InitialFrameFieldName = "initialFrame";
        public const string FrameRateFieldName = "frameRate";
        public const string PlayOnStartFieldName = "playOnStart";
        public const string CacheNotImplementedMessage = "Physics Cache is not implemented yet. Use Off for random access or Live for forward Play Mode playback.";
        public const string LastFastRuntimeReasonFieldName = "lastFastRuntimeReason";

        public static readonly string[] DefaultInspectorExcludedProperties =
        {
            "m_Script",
            InitialFrameFieldName,
            FrameRateFieldName,
            PlayOnStartFieldName,
            PhysicsModeFieldName,
            LastFastRuntimeReasonFieldName
        };

        private static readonly GUIContent PhysicsModeLabel = new("Physics Mode");
        private static readonly GUIContent[] PhysicsModeOptions =
        {
            new("Off"),
            new("Live")
        };

        private static readonly int[] PhysicsModeValues =
        {
            (int)MmdPhysicsMode.Off,
            (int)MmdPhysicsMode.Live
        };

        public static bool InspectorAllowsPhysicsMode(MmdPhysicsMode mode)
        {
            return mode == MmdPhysicsMode.Off || mode == MmdPhysicsMode.Live;
        }

        public static MmdPlaybackConfig ResolvePlaybackConfigForNewSource(
            MmdUnityPlaybackController controller,
            int fallbackInitialFrame = 0)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            MmdRuntimeImporterComponent? importer = controller.GetComponent<MmdRuntimeImporterComponent>();
            if (importer != null)
            {
                return importer.ToConfig();
            }

            return new MmdPlaybackConfig(
                controller.FrameRate,
                fallbackInitialFrame,
                controller.PlayOnStart);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, DefaultInspectorExcludedProperties);
            DrawPhysicsMode();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPhysicsMode()
        {
            SerializedProperty physicsMode = serializedObject.FindProperty(PhysicsModeFieldName);
            if (physicsMode == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Physics", EditorStyles.boldLabel);

            if (physicsMode.hasMultipleDifferentValues)
            {
                EditorGUILayout.PropertyField(physicsMode, PhysicsModeLabel);
                return;
            }

            var current = (MmdPhysicsMode)physicsMode.enumValueIndex;
            if (!InspectorAllowsPhysicsMode(current))
            {
                EditorGUILayout.HelpBox(CacheNotImplementedMessage, MessageType.Warning);
                if (GUILayout.Button("Reset Physics Mode To Off"))
                {
                    physicsMode.enumValueIndex = (int)MmdPhysicsMode.Off;
                }

                return;
            }

            int selectedIndex = current == MmdPhysicsMode.Live ? 1 : 0;
            int nextIndex = EditorGUILayout.IntPopup(PhysicsModeLabel, selectedIndex, PhysicsModeOptions, PhysicsModeValues);
            physicsMode.enumValueIndex = nextIndex;

            DrawLivePhysicsDiagnostics();
        }

        private void DrawLivePhysicsDiagnostics()
        {
            var controller = (MmdUnityPlaybackController)target;
            MmdLivePhysicsFrameDiagnostics? diagnostics = controller.LastLivePhysicsDiagnostics;
            if (diagnostics == null)
            {
                return;
            }

            string summary = BuildLivePhysicsDiagnosticsSummary(diagnostics);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live Physics Diagnostics", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(summary, MessageType.None);
        }

        internal static string BuildLivePhysicsDiagnosticsSummary(MmdLivePhysicsFrameDiagnostics diagnostics)
        {
            string pinned = diagnostics.pinnedBodies.pinnedBodyCount.ToString();
            string staticCount = diagnostics.pinnedBodies.staticPinnedBodyCount.ToString();
            string dynOriCount = diagnostics.pinnedBodies.dynamicOrientationPinnedBodyCount.ToString();
            return $"frame={diagnostics.frame}  stepMs={diagnostics.stepPhysicsMs:F2}  pinned={pinned}  (static={staticCount}  dynOri={dynOriCount})";
        }
    }
}
