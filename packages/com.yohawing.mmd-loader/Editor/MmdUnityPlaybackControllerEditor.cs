#nullable enable

using System;
using UnityEditor;
using UnityEngine;
using Mmd.Physics;
using Mmd.UnityIntegration;

namespace Mmd.Editor
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
        public const string IkMaxIterationsCapFieldName = "ikMaxIterationsCap";

        public static readonly string[] DefaultInspectorExcludedProperties =
        {
            "m_Script",
            InitialFrameFieldName,
            FrameRateFieldName,
            PlayOnStartFieldName,
            PhysicsModeFieldName,
            IkMaxIterationsCapFieldName,
            LastFastRuntimeReasonFieldName
        };

        private bool advancedLivePhysicsSettingsExpanded;

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
            DrawAdvancedLivePhysicsSettings();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAdvancedLivePhysicsSettings()
        {
            SerializedProperty ikCap = serializedObject.FindProperty(IkMaxIterationsCapFieldName);
            if (ikCap == null)
            {
                return;
            }

            EditorGUILayout.Space();
            advancedLivePhysicsSettingsExpanded = EditorGUILayout.Foldout(
                advancedLivePhysicsSettingsExpanded,
                new GUIContent(
                    "Advanced Live Physics Settings",
                    "Optional runtime solver tuning. Leave collapsed to preserve authored PMX behavior."),
                toggleOnLabelClick: true);
            if (!advancedLivePhysicsSettingsExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                ikCap,
                new GUIContent(
                    "IK Max Iterations Cap",
                    "Zero preserves each PMX IK chain's authored iteration count. Positive values may reduce pose quality and are currently supported only for VMD Physics Off and Humanoid Live."));
            EditorGUI.indentLevel--;
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
