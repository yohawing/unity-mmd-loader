#nullable enable

using System;
using Mmd.Native;

namespace Mmd.Motion
{
    internal abstract class NativeVmdTrackSampler<TState> : IDisposable
    {
        private const string NativeDllUnavailableReason = "native DLL is unavailable";
        private const string NativeEntryPointUnavailableReason = "native entry point is unavailable";
        private const string NativeImageFormatMismatchReason = "native DLL has an incompatible image format";
        private const string NativeAbiOrTrackCreationFailureReason =
            "ABI validation or native track creation failed";
        private const string NativeUnsupportedReason = "native runtime does not support this track";
        private const string NativeTrackCreationReturnedNullReason = "native track creation returned null";
        private const string NativeSampleOperationFailureReason = "native track sample operation failed";
        private const string NativeSampleReturnedFalseReason = "native track sample returned false";
        private const string NativeSampleInvalidReason = "native track sample returned invalid data";

        private IntPtr track;
        private readonly float[] sampleBuffer;
        private readonly Action<IntPtr> freeTrack;
        private bool disposed;
        private bool sampleUnavailable;

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
                if (sampler == null)
                {
                    failureReason = NativeTrackCreationReturnedNullReason;
                    return false;
                }

                track = IntPtr.Zero;
                return true;
            }
            catch (Exception exception) when (TryGetNativeBoundaryFailureReason(
                exception,
                sampleOperation: false,
                out failureReason))
            {
                return false;
            }
            finally
            {
                if (track != IntPtr.Zero)
                {
                    freeTrack(track);
                    track = IntPtr.Zero;
                }

                if (sampler == null && string.IsNullOrEmpty(failureReason))
                {
                    failureReason = NativeTrackCreationReturnedNullReason;
                }
            }
        }

        public bool TrySample(float frame, out TState state)
        {
            state = DefaultState;
            if (disposed)
            {
                LastFailureReason = "sampler is disposed";
                return false;
            }

            if (sampleUnavailable)
            {
                return false;
            }

            if (track == IntPtr.Zero || !float.IsFinite(frame))
            {
                LastFailureReason = "sample frame is invalid or native track is unavailable";
                return false;
            }

            byte sampleResult;
            try
            {
                sampleResult = SampleTrack(track, frame, sampleBuffer, new IntPtr(sampleBuffer.Length));
            }
            catch (Exception exception) when (TryGetNativeBoundaryFailureReason(
                exception,
                sampleOperation: true,
                out string sampleFailureReason))
            {
                return FailSample(sampleFailureReason);
            }

            if (sampleResult == 0)
            {
                return FailSample(NativeSampleReturnedFalseReason);
            }

            for (int i = 0; i < sampleBuffer.Length; i++)
            {
                if (!float.IsFinite(sampleBuffer[i]))
                {
                    return FailSample(NativeSampleInvalidReason);
                }
            }

            LastFailureReason = null;
            state = ToState(sampleBuffer);
            return true;
        }

        private bool FailSample(string failureReason)
        {
            LastFailureReason = failureReason;
            sampleUnavailable = true;
            return false;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            if (track == IntPtr.Zero)
            {
                disposed = true;
                return;
            }

            freeTrack(track);
            track = IntPtr.Zero;
            disposed = true;
        }

        private static bool TryGetNativeBoundaryFailureReason(
            Exception exception,
            bool sampleOperation,
            out string failureReason)
        {
            if (exception is DllNotFoundException)
            {
                failureReason = NativeDllUnavailableReason;
                return true;
            }

            if (exception is EntryPointNotFoundException)
            {
                failureReason = NativeEntryPointUnavailableReason;
                return true;
            }

            if (exception is BadImageFormatException)
            {
                failureReason = NativeImageFormatMismatchReason;
                return true;
            }

            if (exception is MmdRuntimeUnsupportedException)
            {
                failureReason = NativeUnsupportedReason;
                return true;
            }

            if (exception is InvalidOperationException)
            {
                string category = sampleOperation
                    ? NativeSampleOperationFailureReason
                    : NativeAbiOrTrackCreationFailureReason;
                failureReason = string.IsNullOrEmpty(exception.Message)
                    ? category
                    : category + ": " + exception.Message;
                return true;
            }

            failureReason = string.Empty;
            return false;
        }

        protected abstract byte SampleTrack(IntPtr track, float frame, float[] values, IntPtr valueCount);

        protected abstract TState ToState(float[] values);
    }
}
