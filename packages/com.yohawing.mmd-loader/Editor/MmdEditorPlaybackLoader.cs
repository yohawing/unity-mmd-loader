#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
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
        private const string MenuPath = "Tools/MMD Loader/Load PMX+VMD Playback Into Scene";
        private const string SelectedAssetsMenuPath = "Tools/MMD Loader/Load Selected PMX+VMD Assets Into Scene";

        [MenuItem(MenuPath)]
        public static void LoadPlaybackIntoSceneFromMenu()
        {
            string pmxPath = EditorUtility.OpenFilePanel("Load PMX Into Scene", string.Empty, "pmx");
            if (string.IsNullOrWhiteSpace(pmxPath))
            {
                return;
            }

            string vmdPath = EditorUtility.OpenFilePanel("Load VMD Motion", Path.GetDirectoryName(pmxPath) ?? string.Empty, "vmd");
            if (string.IsNullOrWhiteSpace(vmdPath))
            {
                return;
            }

            try
            {
                MmdEditorPlaybackSceneLoadResult result = LoadPlaybackIntoScene(pmxPath, vmdPath);
                Selection.activeGameObject = result.Instance.Root;
                Debug.LogFormat(
                    "Loaded PMX+VMD playback into scene: pmx={0}; vmd={1}; frameRate={2}; vertices={3}; indices={4}; bones={5}; maxFrame={6}",
                    pmxPath,
                    vmdPath,
                    result.Controller.FrameRate,
                    result.Instance.VertexCount,
                    result.Instance.IndexCount,
                    result.Instance.BoneTransforms.Length,
                    result.Motion.maxFrame);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to load PMX+VMD playback into scene:" + Environment.NewLine + ex);
                EditorUtility.DisplayDialog("MMD Playback Load Failed", ex.Message, "OK");
            }
        }

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

        [MenuItem(SelectedAssetsMenuPath)]
        public static void LoadSelectedPlaybackAssetsIntoSceneFromMenu()
        {
            if (!TryGetSelectedPlaybackAssets(out MmdPmxAsset? pmxAsset, out MmdVmdAsset? vmdAsset))
            {
                EditorUtility.DisplayDialog("MMD Playback Load Failed", "Select one PMX asset and one VMD asset.", "OK");
                return;
            }

            try
            {
                MmdEditorPlaybackSceneLoadResult result = LoadPlaybackIntoScene(pmxAsset, vmdAsset);
                Selection.activeGameObject = result.Instance.Root;
                Debug.LogFormat(
                    "Loaded PMX+VMD assets into scene: pmx={0}; vmd={1}; frameRate={2}; vertices={3}; indices={4}; bones={5}; maxFrame={6}",
                    pmxAsset.SourceId,
                    vmdAsset.SourceId,
                    result.Controller.FrameRate,
                    result.Instance.VertexCount,
                    result.Instance.IndexCount,
                    result.Instance.BoneTransforms.Length,
                    result.Motion.maxFrame);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to load selected PMX+VMD assets into scene:" + Environment.NewLine + ex);
                EditorUtility.DisplayDialog("MMD Playback Load Failed", ex.Message, "OK");
            }
        }

        [MenuItem(SelectedAssetsMenuPath, true)]
        public static bool ValidateLoadSelectedPlaybackAssetsIntoSceneFromMenu()
        {
            return TryGetSelectedPlaybackAssets(out _, out _);
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

        private static bool TryGetSelectedPlaybackAssets(out MmdPmxAsset? pmxAsset, out MmdVmdAsset? vmdAsset)
        {
            pmxAsset = null;
            vmdAsset = null;
            foreach (UnityEngine.Object selected in Selection.objects)
            {
                pmxAsset ??= selected as MmdPmxAsset;
                vmdAsset ??= selected as MmdVmdAsset;
            }

            return pmxAsset != null && vmdAsset != null;
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
