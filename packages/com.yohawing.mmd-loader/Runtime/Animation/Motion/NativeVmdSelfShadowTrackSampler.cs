#nullable enable

using System;
using Mmd.Native;

namespace Mmd.Motion
{
    internal sealed class NativeVmdSelfShadowTrackSampler : NativeVmdTrackSampler<MmdSelfShadowState>
    {
        private const int SelfShadowSampleFloatCount = 2;

        private NativeVmdSelfShadowTrackSampler(IntPtr track, int frameCount)
            : base(track, frameCount, SelfShadowSampleFloatCount, MmdRuntimeFfiMethods.VmdSelfShadowTrackFree)
        {
        }

        protected override MmdSelfShadowState DefaultState => MmdSelfShadowState.Default;

        public static bool TryCreate(byte[]? vmdBytes, out NativeVmdSelfShadowTrackSampler? sampler)
        {
            return TryCreate(vmdBytes, out sampler, out _);
        }

        public static bool TryCreate(
            byte[]? vmdBytes,
            out NativeVmdSelfShadowTrackSampler? sampler,
            out string failureReason)
        {
            return TryCreateTrack(
                vmdBytes,
                MmdRuntimeFfiMethods.VmdSelfShadowTrackCreateFromVmdBytes,
                MmdRuntimeFfiMethods.VmdSelfShadowTrackFrameCount,
                MmdRuntimeFfiMethods.VmdSelfShadowTrackFree,
                (track, frameCount) => new NativeVmdSelfShadowTrackSampler(track, frameCount),
                "VMD self-shadow track frame count",
                out sampler,
                out failureReason);
        }

        protected override byte SampleTrack(IntPtr track, float frame, float[] values, IntPtr valueCount)
        {
            return MmdRuntimeFfiMethods.VmdSelfShadowTrackSample(track, frame, values, valueCount);
        }

        protected override MmdSelfShadowState ToState(float[] values)
        {
            byte mode = values[0] <= 0.0f ? (byte)0 : values[0] >= byte.MaxValue ? byte.MaxValue : (byte)values[0];
            return new MmdSelfShadowState(mode, values[1]);
        }
    }
}
