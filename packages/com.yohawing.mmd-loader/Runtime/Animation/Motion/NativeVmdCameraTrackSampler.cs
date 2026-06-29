#nullable enable

using System;
using Mmd.Native;

namespace Mmd.Motion
{
    internal sealed class NativeVmdCameraTrackSampler : IDisposable
    {
        private readonly IntPtr track;
        private bool disposed;

        private NativeVmdCameraTrackSampler(IntPtr track, int frameCount)
        {
            this.track = track;
            FrameCount = frameCount;
        }

        public int FrameCount { get; }

        public static bool TryCreate(byte[]? vmdBytes, out NativeVmdCameraTrackSampler? sampler)
        {
            sampler = null;
            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                return false;
            }

            try
            {
                MmdRuntimeFfiMethods.ValidateAbiVersion();
                IntPtr track = MmdRuntimeFfiMethods.VmdCameraTrackCreateFromVmdBytes(vmdBytes, new IntPtr(vmdBytes.Length));
                if (track == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    int frameCount = MmdRuntimeFfiSmoke.CheckedIntPtrToInt(
                        MmdRuntimeFfiMethods.VmdCameraTrackFrameCount(track),
                        "VMD camera track frame count");
                    sampler = new NativeVmdCameraTrackSampler(track, frameCount);
                    return true;
                }
                catch
                {
                    MmdRuntimeFfiMethods.VmdCameraTrackFree(track);
                    throw;
                }
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public bool TrySample(float frame, out MmdCameraState state)
        {
            state = MmdCameraState.Default;
            if (disposed || track == IntPtr.Zero || !float.IsFinite(frame))
            {
                return false;
            }

            if (MmdRuntimeFfiMethods.VmdCameraTrackSample(track, frame, out MmdRuntimeFfiCameraState camera) == 0)
            {
                return false;
            }

            state = ToCameraState(camera);
            return true;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            MmdRuntimeFfiMethods.VmdCameraTrackFree(track);
            disposed = true;
        }

        private static MmdCameraState ToCameraState(MmdRuntimeFfiCameraState camera)
        {
            return new MmdCameraState(
                camera.distance,
                new[] { camera.positionX, camera.positionY, camera.positionZ },
                new[] { camera.rotationX, camera.rotationY, camera.rotationZ },
                camera.fov,
                camera.perspective != 0);
        }
    }
}
