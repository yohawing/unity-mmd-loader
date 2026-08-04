#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Mmd.Native
{
    /// <summary>
    /// Owns one native shared VMD parse. Raw scene/property channels are read
    /// from this object and clips created from it retain independent native data.
    /// </summary>
    internal sealed class MmdRuntimeFfiVmdContext : IDisposable
    {
        private delegate int CopyContextStructDelegate(
            IntPtr context,
            IntPtr output,
            IntPtr capacity,
            out IntPtr written);

        private IntPtr handle;
        private readonly Action<IntPtr> freeContext;

        private MmdRuntimeFfiVmdContext(IntPtr handle)
            : this(handle, MmdRuntimeFfiMethods.VmdContextFree)
        {
        }

        // This constructor keeps cleanup ownership testable without changing the
        // native ABI or the production creation path.
        internal MmdRuntimeFfiVmdContext(IntPtr handle, Action<IntPtr> freeContext)
        {
            if (handle == IntPtr.Zero)
            {
                throw new ArgumentException("Native VMD context handle is required.", nameof(handle));
            }

            this.handle = handle;
            this.freeContext = freeContext ?? throw new ArgumentNullException(nameof(freeContext));
        }

        internal static MmdRuntimeFfiVmdContext Create(byte[] vmdBytes)
        {
            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(vmdBytes));
            }

            return MmdRuntimeNativeBoundary.Invoke(
                "shared VMD context",
                () =>
                {
                    MmdRuntimeFfiMethods.ValidateVmdSharedContextCapability();
                    IntPtr nativeHandle = MmdRuntimeFfiMethods.VmdContextCreateFromVmdBytes(
                        vmdBytes,
                        new IntPtr(vmdBytes.Length));
                    if (nativeHandle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException(
                            "mmd-runtime shared VMD context creation returned null: " +
                            MmdRuntimeFfiMarshal.LastErrorMessage());
                    }

                    return new MmdRuntimeFfiVmdContext(nativeHandle);
                });
        }

        internal IntPtr GetNativeHandle()
        {
            ThrowIfDisposed();
            return handle;
        }

        internal MmdRuntimeFfiMethods.VmdContextSummary ReadSummary()
        {
            ThrowIfDisposed();
            return MmdRuntimeNativeBoundary.Invoke(
                "shared VMD context summary",
                () =>
                {
                    MmdRuntimeFfiMethods.ValidateVmdSharedContextCapability();
                    return ReadSummaryBuffer(
                        buffer => MmdRuntimeFfiMethods.VmdContextReadSummary(
                            handle,
                            buffer,
                            new IntPtr(MmdRuntimeFfiMethods.VmdContextSummarySizeV1)),
                        "summary");
                });
        }

        internal static MmdRuntimeFfiMethods.VmdContextSummary ReadSummaryFromVmdBytes(byte[] vmdBytes)
        {
            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(vmdBytes));
            }

            return MmdRuntimeNativeBoundary.Invoke(
                "VMD summary-only parse",
                () =>
                {
                    MmdRuntimeFfiMethods.ValidateVmdSummaryBytesCapability();
                    return ReadSummaryBuffer(
                        buffer => MmdRuntimeFfiMethods.VmdSummaryReadFromVmdBytes(
                            vmdBytes,
                            new IntPtr(vmdBytes.Length),
                            buffer,
                            new IntPtr(MmdRuntimeFfiMethods.VmdContextSummarySizeV1)),
                        "summary-only parse");
                });
        }

        internal MmdRuntimeFfiMethods.VmdCameraKeyframe[] GetCameraKeyframes()
        {
            return CopyStructArray<MmdRuntimeFfiMethods.VmdCameraKeyframe>(
                MmdRuntimeFfiMethods.VmdContextCameraFrameCount,
                MmdRuntimeFfiMethods.VmdContextCopyCameraKeyframes,
                "camera keyframes");
        }

        internal MmdRuntimeFfiMethods.VmdLightKeyframe[] GetLightKeyframes()
        {
            return CopyStructArray<MmdRuntimeFfiMethods.VmdLightKeyframe>(
                MmdRuntimeFfiMethods.VmdContextLightFrameCount,
                MmdRuntimeFfiMethods.VmdContextCopyLightKeyframes,
                "light keyframes");
        }

        internal MmdRuntimeFfiMethods.VmdSelfShadowKeyframe[] GetSelfShadowKeyframes()
        {
            return CopyStructArray<MmdRuntimeFfiMethods.VmdSelfShadowKeyframe>(
                MmdRuntimeFfiMethods.VmdContextSelfShadowFrameCount,
                MmdRuntimeFfiMethods.VmdContextCopySelfShadowKeyframes,
                "self-shadow keyframes");
        }

        internal MmdRuntimeFfiMethods.VmdPropertyKeyframe[] GetPropertyKeyframes()
        {
            return CopyStructArray<MmdRuntimeFfiMethods.VmdPropertyKeyframe>(
                MmdRuntimeFfiMethods.VmdContextPropertyFrameCount,
                MmdRuntimeFfiMethods.VmdContextCopyPropertyKeyframes,
                "property keyframes");
        }

        internal MmdRuntimeFfiMethods.VmdPropertyIkEntry[] GetPropertyIkEntries()
        {
            return CopyStructArray<MmdRuntimeFfiMethods.VmdPropertyIkEntry>(
                MmdRuntimeFfiMethods.VmdContextPropertyIkEntryCount,
                MmdRuntimeFfiMethods.VmdContextCopyPropertyIkEntries,
                "property IK entries");
        }

        internal MmdRuntimeFfiMethods.VmdRawBoneKeyframe[] GetRawBoneKeyframes()
        {
            return MmdRuntimeNativeBoundary.Invoke(
                "shared VMD model-less raw bone keyframes",
                () =>
                {
                    MmdRuntimeFfiMethods.ValidateVmdSharedContextRawReadbackCapability();
                    return CopyStructArray<MmdRuntimeFfiMethods.VmdRawBoneKeyframe>(
                        MmdRuntimeFfiMethods.VmdContextBoneKeyframeCount,
                        MmdRuntimeFfiMethods.VmdContextCopyBoneKeyframes,
                        "model-less raw bone keyframes");
                });
        }

        internal MmdRuntimeFfiMethods.VmdRawMorphKeyframe[] GetRawMorphKeyframes()
        {
            return MmdRuntimeNativeBoundary.Invoke(
                "shared VMD model-less raw morph keyframes",
                () =>
                {
                    MmdRuntimeFfiMethods.ValidateVmdSharedContextRawReadbackCapability();
                    return CopyStructArray<MmdRuntimeFfiMethods.VmdRawMorphKeyframe>(
                        MmdRuntimeFfiMethods.VmdContextMorphKeyframeCount,
                        MmdRuntimeFfiMethods.VmdContextCopyMorphKeyframes,
                        "model-less raw morph keyframes");
                });
        }

        public void Dispose()
        {
            IntPtr currentHandle = handle;
            if (currentHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                freeContext(currentHandle);
                // Keep the handle until native cleanup returns. If the P/Invoke
                // boundary throws, a later Dispose can retry the same ownership.
                handle = IntPtr.Zero;
            }
            catch (DllNotFoundException exception)
            {
                throw MmdRuntimeNativeBoundary.Unavailable("shared VMD context cleanup", exception);
            }
            catch (EntryPointNotFoundException exception)
            {
                throw MmdRuntimeNativeBoundary.Unavailable("shared VMD context cleanup", exception);
            }
            catch (BadImageFormatException exception)
            {
                throw MmdRuntimeNativeBoundary.Unavailable("shared VMD context cleanup", exception);
            }
        }

        private T[] CopyStructArray<T>(
            Func<IntPtr, IntPtr> getCount,
            CopyContextStructDelegate copy,
            string label)
            where T : struct
        {
            ThrowIfDisposed();
            try
            {
                int count = MmdFfiMarshal.CheckedIntPtrToInt(getCount(handle), label + " count");
                NativeStructCopyDelegate copyDelegate =
                    (IntPtr buffer, IntPtr capacity, out IntPtr written) =>
                        copy(handle, buffer, capacity, out written);
                return MmdFfiMarshal.CopyStructArray<T>(count, label, copyDelegate, ThrowForStatus);
            }
            catch (DllNotFoundException exception)
            {
                throw MmdRuntimeNativeBoundary.Unavailable("shared VMD context " + label, exception);
            }
            catch (EntryPointNotFoundException exception)
            {
                throw MmdRuntimeNativeBoundary.Unavailable("shared VMD context " + label, exception);
            }
            catch (BadImageFormatException exception)
            {
                throw MmdRuntimeNativeBoundary.Unavailable("shared VMD context " + label, exception);
            }
        }

        private static void ThrowForStatus(int status, string operation)
        {
            if (status == MmdRuntimeFfiMethods.StatusOk)
            {
                return;
            }

            string message = "mmd-runtime shared VMD context " + operation +
                             " failed with status " + status + ": " +
                             MmdRuntimeFfiMarshal.LastErrorMessage();
            if (status == MmdRuntimeFfiMethods.StatusUnsupported)
            {
                throw new MmdRuntimeUnsupportedException(message);
            }

            throw new InvalidOperationException(message);
        }

        private static MmdRuntimeFfiMethods.VmdContextSummary ReadSummaryBuffer(
            Func<IntPtr, int> read,
            string operation)
        {
            int managedSize = Marshal.SizeOf<MmdRuntimeFfiMethods.VmdContextSummary>();
            if (managedSize != MmdRuntimeFfiMethods.VmdContextSummarySizeV1)
            {
                throw new InvalidOperationException(
                    "mmd-runtime shared VMD context summary managed layout is " +
                    managedSize + " bytes; expected " +
                    MmdRuntimeFfiMethods.VmdContextSummarySizeV1 + ".");
            }

            IntPtr buffer = Marshal.AllocHGlobal(MmdRuntimeFfiMethods.VmdContextSummarySizeV1);
            try
            {
                ThrowForStatus(read(buffer), operation);
                MmdRuntimeFfiMethods.VmdContextSummary summary =
                    Marshal.PtrToStructure<MmdRuntimeFfiMethods.VmdContextSummary>(buffer);
                ValidateSummaryLayout(summary);
                return summary;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static void ValidateSummaryLayout(MmdRuntimeFfiMethods.VmdContextSummary summary)
        {
            if (summary.structSize != (uint)MmdRuntimeFfiMethods.VmdContextSummarySizeV1)
            {
                throw new InvalidOperationException(
                    "mmd-runtime shared VMD context summary reported " +
                    summary.structSize + " bytes; expected " +
                    MmdRuntimeFfiMethods.VmdContextSummarySizeV1 + ".");
            }

            if (summary.abiVersion != MmdRuntimeFfiMethods.VmdSharedContextSummaryAbiVersionV1)
            {
                throw new MmdRuntimeUnsupportedException(
                    "mmd-runtime shared VMD context summary ABI version " +
                    summary.abiVersion + " is not supported. Expected " +
                    MmdRuntimeFfiMethods.VmdSharedContextSummaryAbiVersionV1 + ".");
            }

            if (summary.targetModelNameBytes == null || summary.targetModelNameBytes.Length != 20)
            {
                throw new InvalidOperationException(
                    "mmd-runtime shared VMD context summary model-name layout is invalid.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeFfiVmdContext));
            }
        }
    }

    /// <summary>
    /// Indicates that the packaged native runtime cannot be loaded or does not expose a required entry point.
    /// </summary>
    public sealed class MmdRuntimeNativeUnavailableException : Exception
    {
        internal MmdRuntimeNativeUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    internal static class MmdRuntimeNativeBoundary
    {
        internal static T Invoke<T>(string operation, Func<T> action)
        {
            try
            {
                return action();
            }
            catch (DllNotFoundException exception)
            {
                throw Unavailable(operation, exception);
            }
            catch (EntryPointNotFoundException exception)
            {
                throw Unavailable(operation, exception);
            }
            catch (BadImageFormatException exception)
            {
                throw Unavailable(operation, exception);
            }
        }

        internal static MmdRuntimeNativeUnavailableException Unavailable(
            string operation,
            Exception exception)
        {
            return new MmdRuntimeNativeUnavailableException(
                "mmd-runtime native is unavailable for " + operation + ": " + exception.Message,
                exception);
        }
    }
}
