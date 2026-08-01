#nullable enable

using System;
using System.IO;
using Mmd;
using Mmd.Native;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackController
    {
        // Test-only seam for deterministic native-unavailable fallback coverage. Production code
        // leaves this null and calls the real binding setup below.
        internal static Func<byte[], byte[], bool>? NativeRuntimeAvailabilityOverrideForTests { get; set; }

        private static bool TryCheckNativeRuntimeAvailability(
            byte[] pmxBytes,
            byte[] vmdBytes,
            out string reason)
        {
            Func<byte[], byte[], bool>? overrideForTests = NativeRuntimeAvailabilityOverrideForTests;
            if (overrideForTests != null)
            {
                bool available = overrideForTests(pmxBytes, vmdBytes);
                reason = available ? string.Empty : "native runtime unavailable (forced by test).";
                if (!available)
                {
                    return false;
                }
            }

            try
            {
                MmdRuntimeFfiMethods.ValidateAbiVersion();
                reason = string.Empty;
                return true;
            }
            catch (DllNotFoundException ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (BadImageFormatException ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (InvalidOperationException ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryEnableNativeRuntime(
            MmdUnityPlaybackBinding candidate,
            byte[] pmxBytes,
            byte[] vmdBytes,
            out string reason)
        {
            return candidate.TryEnableFastRuntime(
                pmxBytes,
                vmdBytes,
                out reason,
                abiAlreadyValidated: true);
        }

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

    }
}
