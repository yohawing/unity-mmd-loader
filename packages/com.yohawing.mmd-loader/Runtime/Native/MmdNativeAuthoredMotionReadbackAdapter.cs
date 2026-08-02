#nullable enable

using System;
using Mmd.Parser;

namespace Mmd.Native
{
    /// <summary>
    /// Opt-in bridge from a model-aware native VMD clip back to the existing managed
    /// authored-motion definition. Production playback intentionally does not call this.
    /// </summary>
    internal static class MmdNativeAuthoredMotionReadbackAdapter
    {
        internal static MmdMotionDefinition Read(
            MmdModelDefinition model,
            byte[] pmxBytes,
            byte[] vmdBytes)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (pmxBytes == null || pmxBytes.Length == 0)
            {
                throw new ArgumentException("PMX bytes are required.", nameof(pmxBytes));
            }

            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(vmdBytes));
            }

            MmdVmdParseSummary summary = MmdVmdBinarySummaryReader.Read(vmdBytes);
            using var session = MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes);
            RejectCompiledPropertyTrack(session, summary);

            int boneTrackCount = session.GetBoneTrackCount();
            var boneDescriptors = new MmdRuntimeFfiMethods.BoneTrackDescriptor[boneTrackCount];
            var boneKeys = new MmdRuntimeFfiMethods.BoneTrackKey[boneTrackCount][];
            for (int trackIndex = 0; trackIndex < boneTrackCount; trackIndex++)
            {
                boneDescriptors[trackIndex] = session.GetBoneTrackDescriptor(trackIndex);
                boneKeys[trackIndex] = session.GetBoneTrackKeys(trackIndex);
            }

            int morphTrackCount = session.GetMorphTrackCount();
            var morphDescriptors = new MmdRuntimeFfiMethods.MorphTrackDescriptor[morphTrackCount];
            var morphKeys = new MmdRuntimeFfiMethods.MorphTrackKey[morphTrackCount][];
            for (int trackIndex = 0; trackIndex < morphTrackCount; trackIndex++)
            {
                morphDescriptors[trackIndex] = session.GetMorphTrackDescriptor(trackIndex);
                morphKeys[trackIndex] = session.GetMorphTrackKeys(trackIndex);
            }

            MmdRuntimeFfiMethods.VmdCameraKeyframe[] cameraKeys = ReadRawTrack(
                vmdBytes,
                summary.CameraKeyframeCount,
                MmdRuntimeFfiMethods.VmdCameraTrackCreateFromVmdBytes,
                MmdRuntimeFfiTrackReadback.CopyVmdCameraTrackKeyframes,
                MmdRuntimeFfiMethods.VmdCameraTrackFree,
                "camera");
            MmdRuntimeFfiMethods.VmdLightKeyframe[] lightKeys = ReadRawTrack(
                vmdBytes,
                summary.LightKeyframeCount,
                MmdRuntimeFfiMethods.VmdLightTrackCreateFromVmdBytes,
                MmdRuntimeFfiTrackReadback.CopyVmdLightTrackKeyframes,
                MmdRuntimeFfiMethods.VmdLightTrackFree,
                "light");
            MmdRuntimeFfiMethods.VmdSelfShadowKeyframe[] selfShadowKeys = ReadRawTrack(
                vmdBytes,
                summary.SelfShadowKeyframeCount,
                MmdRuntimeFfiMethods.VmdSelfShadowTrackCreateFromVmdBytes,
                MmdRuntimeFfiTrackReadback.CopyVmdSelfShadowTrackKeyframes,
                MmdRuntimeFfiMethods.VmdSelfShadowTrackFree,
                "self-shadow");

            return MmdNativeMotionReadbackConverter.Build(
                model,
                summary,
                boneDescriptors,
                boneKeys,
                morphDescriptors,
                morphKeys,
                cameraKeys,
                lightKeys,
                selfShadowKeys,
                vmdBytes);
        }

        private static void RejectCompiledPropertyTrack(
            MmdRuntimeFfiPlaybackSession session,
            MmdVmdParseSummary summary)
        {
            int propertyTrackCount = session.GetPropertyTrackCount();
            if (propertyTrackCount != 0)
            {
                throw new MmdRuntimeUnsupportedException(
                    "Native authored-motion readback does not support compiled VMD property/IK tracks " +
                    "because native metadata does not retain visibility or IK names " +
                    "(tracks=" + propertyTrackCount + ").");
            }

            MmdRuntimeFfiMethods.PropertyTrackKey[] propertyKeys = session.GetPropertyTrackKeys();
            byte[] ikStates = session.GetPropertyTrackIkEnabled();
            if (propertyKeys.Length != 0 || ikStates.Length != 0)
            {
                throw new MmdRuntimeUnsupportedException(
                    "Native authored-motion readback does not support compiled VMD property/IK tracks " +
                    "because native metadata does not retain visibility or IK names " +
                    "(tracks=" + propertyTrackCount + ", keys=" + propertyKeys.Length +
                    ", IK states=" + ikStates.Length + ").");
            }

            if (summary.ModelKeyframeCount != 0)
            {
                throw new MmdRuntimeUnsupportedException(
                    "Native authored-motion readback cannot represent VMD property/IK records " +
                    "because native metadata does not retain visibility or IK names " +
                    "(summary records=" + summary.ModelKeyframeCount + ").");
            }
        }

        private static TKey[] ReadRawTrack<TKey>(
            byte[] vmdBytes,
            int expectedCount,
            Func<byte[], IntPtr, IntPtr> createTrack,
            Func<IntPtr, TKey[]> copyKeys,
            Action<IntPtr> freeTrack,
            string label)
        {
            if (expectedCount == 0)
            {
                return Array.Empty<TKey>();
            }

            IntPtr track = createTrack(vmdBytes, new IntPtr(vmdBytes.Length));
            if (track == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "mmd-runtime " + label + " track creation returned null: " +
                    MmdRuntimeFfiMarshal.LastErrorMessage());
            }

            try
            {
                TKey[] keys = copyKeys(track);
                if (keys.Length != expectedCount)
                {
                    throw new InvalidOperationException(
                        "Native " + label + " readback count " + keys.Length +
                        " does not match the VMD summary count " + expectedCount + ".");
                }

                return keys;
            }
            finally
            {
                freeTrack(track);
            }
        }
    }
}
