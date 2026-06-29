#nullable enable

using System;
using Mmd.Native;

namespace Mmd.Motion
{
    internal sealed class NativeVmdCameraTrackSampler : NativeVmdTrackSampler<MmdCameraState>
    {
        private const int CameraSampleFloatCount = 9;

        private NativeVmdCameraTrackSampler(IntPtr track, int frameCount)
            : base(track, frameCount, CameraSampleFloatCount, MmdRuntimeFfiMethods.VmdCameraTrackFree)
        {
        }

        protected override MmdCameraState DefaultState => MmdCameraState.Default;

        public static bool TryCreate(byte[]? vmdBytes, out NativeVmdCameraTrackSampler? sampler)
        {
            return TryCreateTrack(
                vmdBytes,
                MmdRuntimeFfiMethods.VmdCameraTrackCreateFromVmdBytes,
                MmdRuntimeFfiMethods.VmdCameraTrackFrameCount,
                MmdRuntimeFfiMethods.VmdCameraTrackFree,
                (track, frameCount) => new NativeVmdCameraTrackSampler(track, frameCount),
                "VMD camera track frame count",
                out sampler);
        }

        protected override byte SampleTrack(IntPtr track, float frame, float[] values, IntPtr valueCount)
        {
            return MmdRuntimeFfiMethods.VmdCameraTrackSample(track, frame, values, valueCount);
        }

        protected override MmdCameraState ToState(float[] values)
        {
            return new MmdCameraState(
                values[0],
                new[] { values[1], values[2], values[3] },
                new[] { values[4], values[5], values[6] },
                values[7],
                values[8] != 0.0f);
        }
    }
}
