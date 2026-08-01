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

        public string? LastFailureReason { get; private set; }

        protected abstract TState DefaultState { get; }

        protected static bool TryCreateTrack<TSampler>(
            byte[]? vmdBytes,
            Func<byte[], IntPtr, IntPtr> createTrack,
            Func<IntPtr, IntPtr> getFrameCount,
            Action<IntPtr> freeTrack,
            Func<IntPtr, int, TSampler> createSampler,
            string frameCountLabel,
            out TSampler? sampler,
            out string failureReason)
            where TSampler : NativeVmdTrackSampler<TState>
        {
            sampler = null;
            failureReason = string.Empty;
            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                failureReason = "source bytes are empty";
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

                int frameCount = MmdFfiMarshal.CheckedIntPtrToInt(
                    getFrameCount(track),
                    frameCountLabel);
                sampler = createSampler(track, frameCount);
                track = IntPtr.Zero;
                return true;
            }
            catch (DllNotFoundException)
            {
                failureReason = "native DLL is unavailable";
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                failureReason = "native entry point is unavailable";
                return false;
            }
            catch (BadImageFormatException)
            {
                failureReason = "native DLL has an incompatible image format";
                return false;
            }
            catch (InvalidOperationException exception)
            {
                failureReason = "ABI validation or native track creation failed: " + exception.Message;
                return false;
            }
            finally
            {
                if (track != IntPtr.Zero)
                {
                    freeTrack(track);
                }

                if (sampler == null && string.IsNullOrEmpty(failureReason))
                {
                    failureReason = "native track creation returned null";
                }
            }
        }

        public bool TrySample(float frame, out TState state)
        {
            state = DefaultState;
            if (disposed || track == IntPtr.Zero || !float.IsFinite(frame))
            {
                LastFailureReason = disposed
                    ? "sampler is disposed"
                    : "sample frame is invalid or native track is unavailable";
                return false;
            }

            if (SampleTrack(track, frame, sampleBuffer, new IntPtr(sampleBuffer.Length)) == 0)
            {
                LastFailureReason = "native track sample returned false";
                return false;
            }

            LastFailureReason = null;
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
