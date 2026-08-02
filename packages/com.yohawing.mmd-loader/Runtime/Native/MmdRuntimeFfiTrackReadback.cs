#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Mmd.Native
{
    internal static class MmdRuntimeFfiTrackReadback
    {
        private delegate int CopyKeyframesDelegate(
            IntPtr track,
            IntPtr outKeys,
            IntPtr outKeyCapacity,
            out IntPtr outWritten);

        internal static MmdRuntimeFfiMethods.VmdCameraKeyframe[] CopyVmdCameraTrackKeyframes(IntPtr track)
        {
            int count = MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.VmdCameraTrackFrameCount(track),
                "VMD camera track keyframe count");
            return CopyStructArray<MmdRuntimeFfiMethods.VmdCameraKeyframe>(
                track,
                count,
                MmdRuntimeFfiMethods.VmdCameraTrackCopyKeyframes,
                "VMD camera track keyframes");
        }

        internal static MmdRuntimeFfiMethods.VmdLightKeyframe[] CopyVmdLightTrackKeyframes(IntPtr track)
        {
            int count = MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.VmdLightTrackFrameCount(track),
                "VMD light track keyframe count");
            return CopyStructArray<MmdRuntimeFfiMethods.VmdLightKeyframe>(
                track,
                count,
                MmdRuntimeFfiMethods.VmdLightTrackCopyKeyframes,
                "VMD light track keyframes");
        }

        internal static MmdRuntimeFfiMethods.VmdSelfShadowKeyframe[] CopyVmdSelfShadowTrackKeyframes(IntPtr track)
        {
            int count = MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.VmdSelfShadowTrackFrameCount(track),
                "VMD self-shadow track keyframe count");
            return CopyStructArray<MmdRuntimeFfiMethods.VmdSelfShadowKeyframe>(
                track,
                count,
                MmdRuntimeFfiMethods.VmdSelfShadowTrackCopyKeyframes,
                "VMD self-shadow track keyframes");
        }

        private static T[] CopyStructArray<T>(
            IntPtr track,
            int count,
            CopyKeyframesDelegate copyKeyframes,
            string label)
            where T : struct
        {
            if (track == IntPtr.Zero)
            {
                throw new ArgumentException("Native track handle is required.", nameof(track));
            }

            if (count == 0)
            {
                return Array.Empty<T>();
            }

            int stride = Marshal.SizeOf<T>();
            IntPtr buffer = Marshal.AllocHGlobal(checked(stride * count));
            try
            {
                int status = copyKeyframes(
                    track,
                    buffer,
                    new IntPtr(count),
                    out IntPtr written);
                if (status != MmdRuntimeFfiMethods.StatusOk)
                {
                    throw new InvalidOperationException(
                        "mmd-runtime " + label + " copy failed with status " + status + ": "
                        + MmdRuntimeFfiMarshal.LastErrorMessage());
                }

                int copied = MmdFfiMarshal.CheckedIntPtrToInt(written, label + " copied count");
                if (copied != count)
                {
                    throw new InvalidOperationException(
                        "mmd-runtime " + label + " count changed during readback: expected "
                        + count + ", copied " + copied + ".");
                }

                var result = new T[count];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = Marshal.PtrToStructure<T>(IntPtr.Add(buffer, checked(i * stride)));
                }

                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
