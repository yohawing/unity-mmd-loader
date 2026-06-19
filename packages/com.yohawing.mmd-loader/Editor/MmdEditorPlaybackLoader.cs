#nullable enable

using System.IO;
using UnityEditor;
using Mmd;
using Mmd.Parser;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    public static class MmdEditorPlaybackLoader
    {
        // Full PMX + VMD bundled playback loader entry points (model + motion together).
        // Bundled asset paths store PMX/VMD source refs on MmdUnityPlaybackController.
        // Raw paths use MmdRuntimeImporterComponent.
        public static MmdEditorPlaybackSceneLoadResult LoadPlaybackIntoScene(
            string pmxPath,
            string vmdPath,
            float frameRate = 30.0f,
            int initialFrame = 0,
            bool playOnStart = true)
        {
            // Delegates to verification facade (Editor diagnostic arbitrary-frame path forces controller physics Off; see facade).
            MmdEditorPlaybackSceneLoadResult result = MmdEditorVerificationFacade.LoadPlaybackIntoScene(
                pmxPath,
                vmdPath,
                frameRate,
                initialFrame,
                playOnStart);
            MmdUnityPlaybackBinding binding = result.Binding;
            Undo.RegisterCreatedObjectUndo(binding.Instance.Root, "Load PMX+VMD Playback Into Scene");
            return result;
        }

        public static MmdEditorPlaybackSceneLoadResult LoadPlaybackIntoScene(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            float frameRate = 30.0f,
            int initialFrame = 0,
            bool playOnStart = true)
        {
            // This is the full bundled playback (model source + motion source) creation path via facade.
            // Asset sources are stored on the controller; raw model-only paths still use RuntimeImporter.
            // Delegates to verification facade (Editor diagnostic arbitrary-frame path forces controller physics Off; see facade).
            MmdEditorPlaybackSceneLoadResult result = MmdEditorVerificationFacade.LoadPlaybackIntoScene(
                pmxAsset,
                vmdAsset,
                frameRate,
                initialFrame,
                playOnStart);
            Undo.RegisterCreatedObjectUndo(result.Binding.Instance.Root, "Load PMX+VMD Assets Into Scene");
            return result;
        }

    }

    public sealed class MmdEditorPlaybackSceneLoadResult
    {
        public MmdEditorPlaybackSceneLoadResult(
            MmdUnityModelInstance instance,
            MmdUnityPlaybackBinding binding,
            MmdUnityPlaybackController controller,
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            string modelPath,
            string motionPath,
            MmdPlaybackSnapshot initialSnapshot)
        {
            Instance = instance;
            Binding = binding;
            Controller = controller;
            Model = model;
            Motion = motion;
            ModelPath = string.IsNullOrWhiteSpace(modelPath) ? string.Empty : Path.GetFullPath(modelPath);
            MotionPath = string.IsNullOrWhiteSpace(motionPath) ? string.Empty : Path.GetFullPath(motionPath);
            InitialSnapshot = initialSnapshot;
        }

        public MmdUnityModelInstance Instance { get; }

        public MmdUnityPlaybackBinding Binding { get; }

        public MmdUnityPlaybackController Controller { get; }

        public MmdModelDefinition Model { get; }

        public MmdMotionDefinition Motion { get; }

        public string ModelPath { get; }

        public string MotionPath { get; }

        public MmdPlaybackSnapshot InitialSnapshot { get; }
    }
}
