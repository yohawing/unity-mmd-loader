#nullable enable

using System;
using Mmd.Native;

namespace Mmd.Motion
{
    internal abstract class NativeVmdTrackSampler<TState> : IDisposable
    {
        private readonly IntPtr track;
        private readonly float[] sampleBuffer;
        private readonly Action<IntPtr> freeTrack;
        private bool disposed;

        protected NativeVmdTrackSampler(IntPtr track, int frameCount, int sampleFloatCount, Action<IntPtr> freeTrack)
        {
            this.track = track;
            this.freeTrack = freeTrack;
            sampleBuffer = new float[sampleFloatCount];
            FrameCount = frameCount;
        }

        public int FrameCount { get; }

        protected abstract TState DefaultState { get; }

        protected static bool TryCreateTrack<TSampler>(
            byte[]? vmdBytes,
            Func<byte[], IntPtr, IntPtr> createTrack,
            Func<IntPtr, IntPtr> getFrameCount,
            Action<IntPtr> freeTrack,
            Func<IntPtr, int, TSampler> createSampler,
            string frameCountLabel,
            out TSampler? sampler)
            where TSampler : NativeVmdTrackSampler<TState>
        {
            sampler = null;
            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                return false;
            }

            IntPtr track = IntPtr.Zero;
            try
            {
                MmdRuntimeFfiMethods.ValidateAbiVersion();
                track = createTrack(vmdBytes, new IntPtr(vmdBytes.Length));
                if (track == IntPtr.Zero)
                {
                    return false;
                }

                int frameCount = MmdRuntimeFfiSmoke.CheckedIntPtrToInt(
                    getFrameCount(track),
                    frameCountLabel);
                sampler = createSampler(track, frameCount);
                track = IntPtr.Zero;
                return true;
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
            finally
            {
                if (track != IntPtr.Zero)
                {
                    freeTrack(track);
                }
            }
        }

        public bool TrySample(float frame, out TState state)
        {
            state = DefaultState;
            if (disposed || track == IntPtr.Zero || !float.IsFinite(frame))
            {
                return false;
            }

            if (SampleTrack(track, frame, sampleBuffer, new IntPtr(sampleBuffer.Length)) == 0)
            {
                return false;
            }

            state = ToState(sampleBuffer);
            return true;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            freeTrack(track);
            disposed = true;
        }

        protected abstract byte SampleTrack(IntPtr track, float frame, float[] values, IntPtr valueCount);

        protected abstract TState ToState(float[] values);
    }
}
