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

        private delegate int CopyClipTrackKeyframesDelegate(
            IntPtr clip,
            IntPtr trackIndex,
            IntPtr outKeys,
            IntPtr outKeyCapacity,
            out IntPtr outWritten);

        private delegate int CopyPropertyKeyframesDelegate(
            IntPtr clip,
            IntPtr outKeys,
            IntPtr outKeyCapacity,
            out IntPtr outWritten);

        internal static MmdRuntimeFfiMethods.VmdCameraKeyframe[] CopyVmdCameraTrackKeyframes(IntPtr track)
        {
            RequireFeature(
                MmdRuntimeFfiMethods.FeatureVmdTrackKeyframeIntrospection,
                "VMD track keyframe introspection");
            int count = MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.VmdCameraTrackFrameCount(track),
                "VMD camera track keyframe count");
            return CopyTrackStructArray<MmdRuntimeFfiMethods.VmdCameraKeyframe>(
                track,
                count,
                MmdRuntimeFfiMethods.VmdCameraTrackCopyKeyframes,
                "VMD camera track keyframes");
        }

        internal static MmdRuntimeFfiMethods.VmdLightKeyframe[] CopyVmdLightTrackKeyframes(IntPtr track)
        {
            RequireFeature(
                MmdRuntimeFfiMethods.FeatureVmdTrackKeyframeIntrospection,
                "VMD track keyframe introspection");
            int count = MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.VmdLightTrackFrameCount(track),
                "VMD light track keyframe count");
            return CopyTrackStructArray<MmdRuntimeFfiMethods.VmdLightKeyframe>(
                track,
                count,
                MmdRuntimeFfiMethods.VmdLightTrackCopyKeyframes,
                "VMD light track keyframes");
        }

        internal static MmdRuntimeFfiMethods.VmdSelfShadowKeyframe[] CopyVmdSelfShadowTrackKeyframes(IntPtr track)
        {
            RequireFeature(
                MmdRuntimeFfiMethods.FeatureVmdTrackKeyframeIntrospection,
                "VMD track keyframe introspection");
            int count = MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.VmdSelfShadowTrackFrameCount(track),
                "VMD self-shadow track keyframe count");
            return CopyTrackStructArray<MmdRuntimeFfiMethods.VmdSelfShadowKeyframe>(
                track,
                count,
                MmdRuntimeFfiMethods.VmdSelfShadowTrackCopyKeyframes,
                "VMD self-shadow track keyframes");
        }

        internal static int GetClipBoneTrackCount(IntPtr clip)
        {
            RequireFeature(
                MmdRuntimeFfiMethods.FeatureClipBoneTrackIntrospection,
                "compiled bone track introspection");
            return MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.ClipBoneTrackCount(clip),
                "compiled bone track count");
        }

        internal static MmdRuntimeFfiMethods.BoneTrackDescriptor GetClipBoneTrackDescriptor(
            IntPtr clip,
            int trackIndex)
        {
            RequireFeature(
                MmdRuntimeFfiMethods.FeatureClipBoneTrackIntrospection,
                "compiled bone track introspection");
            var descriptor = new MmdRuntimeFfiMethods.BoneTrackDescriptor();
            ThrowForStatus(
                MmdRuntimeFfiMethods.ClipBoneTrackDescriptor(
                    clip,
                    new IntPtr(trackIndex),
                    ref descriptor),
                "compiled bone track descriptor");
            return descriptor;
        }

        internal static MmdRuntimeFfiMethods.BoneTrackKey[] CopyClipBoneTrackKeys(
            IntPtr clip,
            int trackIndex)
        {
            RequireFeature(
                MmdRuntimeFfiMethods.FeatureClipBoneTrackIntrospection,
                "compiled bone track introspection");
            int count = MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.ClipBoneTrackKeyCount(clip, new IntPtr(trackIndex)),
                "compiled bone track key count");
            return CopyClipStructArray<MmdRuntimeFfiMethods.BoneTrackKey>(
                clip,
                trackIndex,
                count,
                MmdRuntimeFfiMethods.ClipCopyBoneTrackKeys,
                "compiled bone track keys");
        }

        internal static int GetClipMorphTrackCount(IntPtr clip)
        {
            RequireFeature(
                MmdRuntimeFfiMethods.FeatureClipMorphTrackIntrospection,
                "compiled morph track introspection");
            return MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.ClipMorphTrackCount(clip),
                "compiled morph track count");
        }

        internal static MmdRuntimeFfiMethods.MorphTrackDescriptor GetClipMorphTrackDescriptor(
            IntPtr clip,
            int trackIndex)
        {
            RequireFeature(
                MmdRuntimeFfiMethods.FeatureClipMorphTrackIntrospection,
                "compiled morph track introspection");
            var descriptor = new MmdRuntimeFfiMethods.MorphTrackDescriptor();
            ThrowForStatus(
                MmdRuntimeFfiMethods.ClipMorphTrackDescriptor(
                    clip,
                    new IntPtr(trackIndex),
                    ref descriptor),
                "compiled morph track descriptor");
            return descriptor;
        }

        internal static MmdRuntimeFfiMethods.MorphTrackKey[] CopyClipMorphTrackKeys(
            IntPtr clip,
            int trackIndex)
        {
            RequireFeature(
                MmdRuntimeFfiMethods.FeatureClipMorphTrackIntrospection,
                "compiled morph track introspection");
            int count = MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.ClipMorphTrackKeyCount(clip, new IntPtr(trackIndex)),
                "compiled morph track key count");
            return CopyClipStructArray<MmdRuntimeFfiMethods.MorphTrackKey>(
                clip,
                trackIndex,
                count,
                MmdRuntimeFfiMethods.ClipCopyMorphTrackKeys,
                "compiled morph track keys");
        }

        internal static int GetClipPropertyTrackCount(IntPtr clip)
        {
            RequireFeature(
                MmdRuntimeFfiMethods.FeatureClipPropertyTrackIntrospection,
                "compiled property track introspection");
            return MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.ClipPropertyTrackCount(clip),
                "compiled property track count");
        }

        internal static MmdRuntimeFfiMethods.PropertyTrackDescriptor GetClipPropertyTrackDescriptor(IntPtr clip)
        {
            RequireFeature(
                MmdRuntimeFfiMethods.FeatureClipPropertyTrackIntrospection,
                "compiled property track introspection");
            var descriptor = new MmdRuntimeFfiMethods.PropertyTrackDescriptor();
            ThrowForStatus(
                MmdRuntimeFfiMethods.ClipPropertyTrackDescriptor(clip, ref descriptor),
                "compiled property track descriptor");
            return descriptor;
        }

        internal static MmdRuntimeFfiMethods.PropertyTrackKey[] CopyClipPropertyTrackKeys(IntPtr clip)
        {
            RequireFeature(
                MmdRuntimeFfiMethods.FeatureClipPropertyTrackIntrospection,
                "compiled property track introspection");
            int count = MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.ClipPropertyTrackKeyCount(clip),
                "compiled property track key count");
            return CopyPropertyStructArray<MmdRuntimeFfiMethods.PropertyTrackKey>(
                clip,
                count,
                MmdRuntimeFfiMethods.ClipCopyPropertyTrackKeys,
                "compiled property track keys");
        }

        internal static byte[] CopyClipPropertyTrackIkEnabled(IntPtr clip)
        {
            RequireFeature(
                MmdRuntimeFfiMethods.FeatureClipPropertyTrackIntrospection,
                "compiled property track introspection");
            int count = MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.ClipPropertyTrackIkEnabledCount(clip),
                "compiled property IK state count");
            if (count == 0)
            {
                return Array.Empty<byte>();
            }

            IntPtr buffer = Marshal.AllocHGlobal(count);
            try
            {
                int status = MmdRuntimeFfiMethods.ClipCopyPropertyTrackIkEnabled(
                    clip,
                    buffer,
                    new IntPtr(count),
                    out IntPtr written);
                ThrowForStatus(status, "compiled property IK states");
                int copied = MmdFfiMarshal.CheckedIntPtrToInt(written, "compiled property IK state count");
                if (copied != count)
                {
                    throw new InvalidOperationException(
                        "mmd-runtime compiled property IK state count changed during readback: expected "
                        + count + ", copied " + copied + ".");
                }

                byte[] result = new byte[count];
                Marshal.Copy(buffer, result, 0, result.Length);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static T[] CopyTrackStructArray<T>(
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

            NativeStructCopyDelegate copy = (IntPtr buffer, IntPtr capacity, out IntPtr written) =>
                copyKeyframes(track, buffer, capacity, out written);
            return MmdFfiMarshal.CopyStructArray<T>(count, label, copy, ThrowForCopyStatus);
        }

        private static T[] CopyClipStructArray<T>(
            IntPtr clip,
            int trackIndex,
            int count,
            CopyClipTrackKeyframesDelegate copyKeyframes,
            string label)
            where T : struct
        {
            if (clip == IntPtr.Zero)
            {
                throw new ArgumentException("Native clip handle is required.", nameof(clip));
            }

            if (trackIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(trackIndex));
            }

            NativeStructCopyDelegate copy = (IntPtr buffer, IntPtr capacity, out IntPtr written) =>
                copyKeyframes(clip, new IntPtr(trackIndex), buffer, capacity, out written);
            return MmdFfiMarshal.CopyStructArray<T>(count, label, copy, ThrowForStatus);
        }

        private static T[] CopyPropertyStructArray<T>(
            IntPtr clip,
            int count,
            CopyPropertyKeyframesDelegate copyKeyframes,
            string label)
            where T : struct
        {
            if (clip == IntPtr.Zero)
            {
                throw new ArgumentException("Native clip handle is required.", nameof(clip));
            }

            NativeStructCopyDelegate copy = (IntPtr buffer, IntPtr capacity, out IntPtr written) =>
                copyKeyframes(clip, buffer, capacity, out written);
            return MmdFfiMarshal.CopyStructArray<T>(count, label, copy, ThrowForStatus);
        }

        private static void RequireFeature(uint feature, string label)
        {
            if ((MmdRuntimeFfiMethods.FeatureFlags() & feature) == 0)
            {
                throw new MmdRuntimeUnsupportedException(
                    "mmd-runtime does not provide " + label + ".");
            }
        }

        private static void ThrowForStatus(int status, string operation)
        {
            if (status == MmdRuntimeFfiMethods.StatusOk)
            {
                return;
            }

            string message = "mmd-runtime " + operation + " failed with status " + status + ": "
                             + MmdRuntimeFfiMarshal.LastErrorMessage();
            if (status == MmdRuntimeFfiMethods.StatusUnsupported)
            {
                throw new MmdRuntimeUnsupportedException(message);
            }

            throw new InvalidOperationException(message);
        }

        private static void ThrowForCopyStatus(int status, string operation)
        {
            if (status == MmdRuntimeFfiMethods.StatusOk)
            {
                return;
            }

            throw new InvalidOperationException(
                "mmd-runtime " + operation + " copy failed with status " + status + ": "
                + MmdRuntimeFfiMarshal.LastErrorMessage());
        }
    }
}
