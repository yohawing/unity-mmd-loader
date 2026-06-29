#nullable enable

using System;
using Mmd.Native;

namespace Mmd.Motion
{
    internal sealed class NativeVmdLightTrackSampler : NativeVmdTrackSampler<MmdLightState>
    {
        private const int LightSampleFloatCount = 6;

        private NativeVmdLightTrackSampler(IntPtr track, int frameCount)
            : base(track, frameCount, LightSampleFloatCount, MmdRuntimeFfiMethods.VmdLightTrackFree)
        {
        }

        protected override MmdLightState DefaultState => MmdLightState.Default;

        public static bool TryCreate(byte[]? vmdBytes, out NativeVmdLightTrackSampler? sampler)
        {
            return TryCreateTrack(
                vmdBytes,
                MmdRuntimeFfiMethods.VmdLightTrackCreateFromVmdBytes,
                MmdRuntimeFfiMethods.VmdLightTrackFrameCount,
                MmdRuntimeFfiMethods.VmdLightTrackFree,
                (track, frameCount) => new NativeVmdLightTrackSampler(track, frameCount),
                "VMD light track frame count",
                out sampler);
        }

        protected override byte SampleTrack(IntPtr track, float frame, float[] values, IntPtr valueCount)
        {
            return MmdRuntimeFfiMethods.VmdLightTrackSample(track, frame, values, valueCount);
        }

        protected override MmdLightState ToState(float[] values)
        {
            return new MmdLightState(
                new[] { values[0], values[1], values[2] },
                new[] { values[3], values[4], values[5] });
        }
    }
}
