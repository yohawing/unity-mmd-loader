#nullable enable

using System;
using Mmd.Parser;

namespace Mmd.Native
{
    /// <summary>
    /// Opt-in bridge from one model-aware native VMD context back to the existing
    /// managed authored-motion definition. Production playback intentionally does not call this.
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

            using var context = MmdRuntimeFfiVmdContext.Create(vmdBytes);
            MmdVmdParseSummary summary = MmdVmdNativeSummaryAdapter.Read(context);
            using var session = MmdRuntimeFfiPlaybackSession.CreateFromVmdContext(
                pmxBytes,
                context,
                abiAlreadyValidated: true);

            MmdRuntimeFfiMethods.VmdBoneKeyframe[] rawBoneKeys = context.GetBoneKeyframesForModel(
                session.GetNativeModelHandle(),
                out int skippedBoneKeys);
            if (skippedBoneKeys != 0)
            {
                throw new MmdRuntimeUnsupportedException(
                    "Native shared VMD context raw bone readback skipped " + skippedBoneKeys +
                    " unresolved model bone name(s); authored motion conversion is refused.");
            }

            int morphTrackCount = session.GetMorphTrackCount();
            var morphDescriptors = new MmdRuntimeFfiMethods.MorphTrackDescriptor[morphTrackCount];
            var morphKeys = new MmdRuntimeFfiMethods.MorphTrackKey[morphTrackCount][];
            for (int trackIndex = 0; trackIndex < morphTrackCount; trackIndex++)
            {
                morphDescriptors[trackIndex] = session.GetMorphTrackDescriptor(trackIndex);
                morphKeys[trackIndex] = session.GetMorphTrackKeys(trackIndex);
            }

            MmdRuntimeFfiMethods.VmdCameraKeyframe[] cameraKeys = context.GetCameraKeyframes();
            MmdRuntimeFfiMethods.VmdLightKeyframe[] lightKeys = context.GetLightKeyframes();
            MmdRuntimeFfiMethods.VmdSelfShadowKeyframe[] selfShadowKeys = context.GetSelfShadowKeyframes();
            MmdRuntimeFfiMethods.VmdPropertyKeyframe[] propertyKeys = context.GetPropertyKeyframes();
            MmdRuntimeFfiMethods.VmdPropertyIkEntry[] propertyIkEntries = context.GetPropertyIkEntries();

            return MmdNativeMotionReadbackConverter.Build(
                model,
                summary,
                rawBoneKeys,
                morphDescriptors,
                morphKeys,
                cameraKeys,
                lightKeys,
                selfShadowKeys,
                propertyKeys,
                propertyIkEntries,
                vmdBytes);
        }
    }
}
