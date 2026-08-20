#nullable enable

using Mmd.Native;
using Mmd.Parser;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// Immutable native playback inputs acquired before a scene binding is mutated.
    /// </summary>
    internal readonly struct MmdNativePlaybackSetup
    {
        internal MmdNativePlaybackSetup(
            MmdMotionDefinition motion,
            byte[] pmxBytes,
            byte[] vmdBytes,
            MmdRuntimeFfiVmdContext? sharedVmdContext = null,
            string? sharedVmdContextFailure = null)
        {
            Motion = motion;
            PmxBytes = pmxBytes;
            VmdBytes = vmdBytes;
            SharedVmdContext = sharedVmdContext;
            SharedVmdContextFailure = sharedVmdContextFailure;
        }

        internal MmdMotionDefinition Motion { get; }

        internal byte[] PmxBytes { get; }

        internal byte[] VmdBytes { get; }

        internal MmdRuntimeFfiVmdContext? SharedVmdContext { get; }

        internal string? SharedVmdContextFailure { get; }
    }
}
