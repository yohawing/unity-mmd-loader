#nullable enable

using System;
using System.Globalization;

namespace Mmd.Editor
{
    internal static class MmdAnimationClipBakeBudget
    {
        // Four million Keyframes are about 112 MiB before AnimationCurve copies and
        // managed-array overhead. Keeping the raw dense working set below this value
        // leaves bounded headroom for Unity's curve copies in the Editor process.
        internal const long MaxDenseKeyEquivalentCount = 4_000_000;

        private const int GenericChannelsPerBone = 7;

        // Humanoid allocates one dense curve per muscle, eight root/offset curves,
        // plus body position and rotation working buffers whose combined element size
        // is approximately one Keyframe per sample.
        private const int HumanoidExtraKeyEquivalentChannels = 9;

        internal static bool TryValidateGeneric(
            long frameCount,
            int boneCount,
            int morphCount,
            out long denseKeyEquivalentCount,
            out string diagnostic)
        {
            if (boneCount < 0 || morphCount < 0)
            {
                return Reject(out denseKeyEquivalentCount, out diagnostic);
            }

            long channelCount = (long)boneCount * GenericChannelsPerBone + morphCount;
            return TryValidate(frameCount, channelCount, out denseKeyEquivalentCount, out diagnostic);
        }

        internal static bool TryValidateHumanoid(
            long frameCount,
            int muscleCount,
            out long denseKeyEquivalentCount,
            out string diagnostic)
        {
            if (muscleCount < 0)
            {
                return Reject(out denseKeyEquivalentCount, out diagnostic);
            }

            long channelCount = (long)muscleCount + HumanoidExtraKeyEquivalentChannels;
            return TryValidate(frameCount, channelCount, out denseKeyEquivalentCount, out diagnostic);
        }

        internal static bool TryValidate(
            long frameCount,
            long channelCount,
            out long denseKeyEquivalentCount,
            out string diagnostic)
        {
            if (frameCount <= 0 || frameCount > int.MaxValue || channelCount < 0)
            {
                return Reject(out denseKeyEquivalentCount, out diagnostic);
            }

            try
            {
                denseKeyEquivalentCount = checked(frameCount * channelCount);
            }
            catch (OverflowException)
            {
                return Reject(out denseKeyEquivalentCount, out diagnostic);
            }

            if (denseKeyEquivalentCount > MaxDenseKeyEquivalentCount)
            {
                diagnostic = CreateDiagnostic();
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool Reject(out long denseKeyEquivalentCount, out string diagnostic)
        {
            denseKeyEquivalentCount = long.MaxValue;
            diagnostic = CreateDiagnostic();
            return false;
        }

        private static string CreateDiagnostic()
        {
            return "validation: requested Frame Range exceeds the safe dense bake budget of "
                   + MaxDenseKeyEquivalentCount.ToString("N0", CultureInfo.InvariantCulture)
                   + " key equivalents. Narrow Frame Range and try again.";
        }
    }
}
