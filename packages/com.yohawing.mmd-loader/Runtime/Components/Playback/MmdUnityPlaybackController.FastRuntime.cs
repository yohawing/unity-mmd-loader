#nullable enable

using System;
using System.IO;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackController
    {
        public bool TryEnableFastRuntime(byte[] pmxBytes, byte[] vmdBytes, out string reason)
        {
            if (binding == null)
            {
                reason = "Playback controller must be configured before enabling fast runtime.";
                return false;
            }

            return binding.TryEnableFastRuntime(pmxBytes, vmdBytes, out reason);
        }

        public bool TryEnableFastRuntimeFromConfiguredSource(out string reason)
        {
            if (binding == null)
            {
                reason = "Playback controller must be configured before enabling fast runtime.";
                return false;
            }

            try
            {
                // Split: model/motion from controller assets; raw paths from importer.
                MmdPmxAsset? modelAsset = ModelAssetSource;
                MmdVmdAsset? motionAsset = MotionAssetSource;
                if (modelAsset != null && motionAsset != null)
                {
                    return binding.TryEnableFastRuntime(
                        modelAsset.GetBytesCopy(),
                        motionAsset.GetBytesCopy(),
                        out reason);
                }

                MmdRuntimeImporterComponent? importer = GetComponent<MmdRuntimeImporterComponent>();
                if (importer != null &&
                    !string.IsNullOrWhiteSpace(importer.ModelPath) &&
                    !string.IsNullOrWhiteSpace(importer.MotionPath))
                {
                    string pmxFull = Path.GetFullPath(importer.ModelPath);
                    string vmdFull = Path.GetFullPath(importer.MotionPath);
                    if (!File.Exists(pmxFull))
                    {
                        reason = "Configured PMX file was not found: " + pmxFull;
                        return false;
                    }
                    if (!File.Exists(vmdFull))
                    {
                        reason = "Configured VMD file was not found: " + vmdFull;
                        return false;
                    }

                    return binding.TryEnableFastRuntime(
                        File.ReadAllBytes(pmxFull),
                        File.ReadAllBytes(vmdFull),
                        out reason);
                }

                reason = "A complete provider-owned PMX/VMD source (controller asset source, or MmdRuntimeImporterComponent with both paths) is required before enabling fast runtime from source.";
                return false;
            }
            catch (Exception ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public void DisableFastRuntime()
        {
            binding?.DisableFastRuntime();
            lastFastRuntimeReason = string.Empty;
        }

        private void TryEnableFastRuntimeFromAssetBytesForDefaultPlayback(MmdPmxAsset pmxAsset, MmdVmdAsset vmdAsset)
        {
            TryEnableFastRuntimeFromBytesForDefaultPlayback(pmxAsset.GetBytesCopy(), vmdAsset.GetBytesCopy());
        }

        private void TryEnableFastRuntimeFromBytesForDefaultPlayback(byte[] pmxBytes, byte[] vmdBytes)
        {
            if (binding == null)
            {
                lastFastRuntimeReason = string.Empty;
                return;
            }

            if (TryEnableFastRuntime(pmxBytes, vmdBytes, out string reason))
            {
                lastFastRuntimeReason = string.Empty;
                return;
            }

            lastFastRuntimeReason = reason;
            Debug.LogWarning("MMD fast runtime unavailable; managed playback remains active: " + reason, this);
        }
    }
}