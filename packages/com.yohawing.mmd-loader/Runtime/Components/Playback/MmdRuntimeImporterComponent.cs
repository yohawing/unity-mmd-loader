#nullable enable

using System;
using System.IO;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MmdUnityPlaybackController))]
    public sealed class MmdRuntimeImporterComponent : MonoBehaviour
    {
        [SerializeField] private string modelPath = string.Empty;
        [SerializeField] private string motionPath = string.Empty;
        [SerializeField] private MmdPlaybackConfigAsset? playbackConfig;
        [SerializeField] private float frameRate = 30.0f;
        [SerializeField] private int initialFrame;
        [SerializeField] private bool playOnStart = true;

        public string ModelPath => modelPath;

        public string MotionPath => motionPath;

        public MmdPlaybackConfigAsset? PlaybackConfigAsset => playbackConfig;

        public float FrameRate => playbackConfig != null ? playbackConfig.FrameRate : frameRate;

        public int InitialFrame => playbackConfig != null ? playbackConfig.InitialFrame : initialFrame;

        public bool PlayOnStart => playbackConfig != null ? playbackConfig.PlayOnStart : playOnStart;

        public string LastConfigurationStatus { get; private set; } = "not-attempted";

        public string LastConfiguredSourceId { get; private set; } = string.Empty;

        public int LastConfiguredControllerRevision { get; private set; }

        public MmdPlaybackConfig ToConfig()
        {
            return playbackConfig != null
                ? playbackConfig.ToConfig()
                : new MmdPlaybackConfig(FrameRate, InitialFrame, PlayOnStart);
        }

        public void ConfigurePlaybackConfigAsset(MmdPlaybackConfigAsset? configAsset)
        {
            if (configAsset != null)
            {
                _ = configAsset.ToConfig();
            }

            playbackConfig = configAsset;
            LastConfigurationStatus = configAsset == null
                ? "configured-local-settings"
                : "configured-playback-config-asset";
        }

        public void ConfigureLocalPlaybackSettings(
            float playbackFrameRate,
            int startFrame,
            bool shouldPlayOnStart)
        {
            MmdPlaybackConfig config = new(playbackFrameRate, startFrame, shouldPlayOnStart);
            playbackConfig = null;
            frameRate = config.FrameRate;
            initialFrame = config.InitialFrame;
            playOnStart = config.PlayOnStart;
            LastConfigurationStatus = "configured-local-settings";
        }

        public void ConfigurePaths(
            string pmxPath,
            string vmdPath,
            float playbackFrameRate = 30.0f,
            int startFrame = 0,
            bool shouldPlayOnStart = true)
        {
            ValidatePaths(pmxPath, vmdPath, playbackFrameRate, startFrame);
            modelPath = Path.GetFullPath(pmxPath);
            motionPath = Path.GetFullPath(vmdPath);
            frameRate = playbackFrameRate;
            initialFrame = startFrame;
            playOnStart = shouldPlayOnStart;
            LastConfigurationStatus = "configured-paths-assigned";
        }

        public void ConfigureModelPath(string pmxPath)
        {
            if (string.IsNullOrWhiteSpace(pmxPath))
            {
                throw new ArgumentException("PMX path is required.", nameof(pmxPath));
            }

            modelPath = Path.GetFullPath(pmxPath);
            motionPath = string.Empty;
            initialFrame = 0;
            LastConfigurationStatus = "configured-model-path-assigned";
        }

        public bool TryConfigureController(
            MmdUnityPlaybackController? controller = null,
            bool allowRuntimeFallback = true)
        {
            controller ??= GetComponent<MmdUnityPlaybackController>();
            if (controller == null)
            {
                LastConfigurationStatus = "controller-missing";
                return false;
            }

            if (controller.IsConfigured)
            {
                LastConfigurationStatus = "controller-already-configured";
                return false;
            }

            if (string.IsNullOrWhiteSpace(modelPath) || string.IsNullOrWhiteSpace(motionPath))
            {
                LastConfigurationStatus = "path-missing";
                return false;
            }

            if (!File.Exists(modelPath))
            {
                LastConfigurationStatus = "model-path-missing";
                return false;
            }

            if (!File.Exists(motionPath))
            {
                LastConfigurationStatus = "motion-path-missing";
                return false;
            }

            MmdPlaybackConfig config = ToConfig();
            controller.ConfigureFromRuntimeImporterPaths(
                modelPath,
                motionPath,
                config,
                allowRuntimeFallback);

            LastConfiguredSourceId = modelPath + " -> " + motionPath;
            LastConfiguredControllerRevision = controller.ConfigurationRevision;
            LastConfigurationStatus = "configured";
            return true;
        }

        private static void ValidatePaths(
            string pmxPath,
            string vmdPath,
            float playbackFrameRate,
            int startFrame)
        {
            if (string.IsNullOrWhiteSpace(pmxPath))
            {
                throw new ArgumentException("PMX path is required.", nameof(pmxPath));
            }

            if (string.IsNullOrWhiteSpace(vmdPath))
            {
                throw new ArgumentException("VMD path is required.", nameof(vmdPath));
            }

            _ = new MmdPlaybackConfig(playbackFrameRate, startFrame, playOnStart: true);
        }
    }
}
