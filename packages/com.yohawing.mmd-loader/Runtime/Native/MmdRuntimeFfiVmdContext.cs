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

            try
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
            }
            catch (DllNotFoundException exception)
            {
                throw MmdRuntimeNativeBoundary.Unavailable("shared VMD context", exception);
            }
            catch (EntryPointNotFoundException exception)
            {
                throw MmdRuntimeNativeBoundary.Unavailable("shared VMD context", exception);
            }
            catch (BadImageFormatException exception)
            {
                throw MmdRuntimeNativeBoundary.Unavailable("shared VMD context", exception);
            }
        }

        internal IntPtr GetNativeHandle()
        {
            ThrowIfDisposed();
            return handle;
        }

        internal int GetCameraFrameCount()
        {
            return GetCount(MmdRuntimeFfiMethods.VmdContextCameraFrameCount, "camera frame count");
        }

        internal int GetLightFrameCount()
        {
            return GetCount(MmdRuntimeFfiMethods.VmdContextLightFrameCount, "light frame count");
        }

        internal int GetSelfShadowFrameCount()
        {
            return GetCount(
                MmdRuntimeFfiMethods.VmdContextSelfShadowFrameCount,
                "self-shadow frame count");
        }

        internal int GetPropertyFrameCount()
        {
            return GetCount(MmdRuntimeFfiMethods.VmdContextPropertyFrameCount, "property frame count");
        }

        internal int GetPropertyIkEntryCount()
        {
            return GetCount(
                MmdRuntimeFfiMethods.VmdContextPropertyIkEntryCount,
                "property IK entry count");
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

        internal MmdRuntimeFfiMethods.VmdBoneKeyframe[] GetBoneKeyframesForModel(
            IntPtr model,
            out int skipped)
        {
            ThrowIfDisposed();
            if (model == IntPtr.Zero)
            {
                throw new ArgumentException("Native model handle is required.", nameof(model));
            }

            skipped = 0;
            try
            {
                MmdRuntimeFfiMethods.ValidateVmdSharedContextBoneReadbackCapability();
                int count = MmdFfiMarshal.CheckedIntPtrToInt(
                    MmdRuntimeFfiMethods.VmdContextBoneKeyframeCountForModel(model, handle),
                    "bone keyframes count");
                int stride = Marshal.SizeOf<MmdRuntimeFfiMethods.VmdBoneKeyframe>();
                IntPtr buffer = count == 0
                    ? IntPtr.Zero
                    : Marshal.AllocHGlobal(checked(stride * count));
                try
                {
                    int status = MmdRuntimeFfiMethods.VmdContextCopyBoneKeyframesForModel(
                        model,
                        handle,
                        buffer,
                        new IntPtr(count),
                        out IntPtr written,
                        out IntPtr skippedNative);
                    ThrowForStatus(status, "bone keyframes");

                    int copied = MmdFfiMarshal.CheckedIntPtrToInt(written, "bone keyframes copied count");
                    if (copied != count)
                    {
                        throw new InvalidOperationException(
                            "mmd-runtime bone keyframes count changed during readback: expected " +
                            count + ", copied " + copied + ".");
                    }

                    skipped = MmdFfiMarshal.CheckedIntPtrToInt(skippedNative, "bone keyframes skipped count");
                    if (count == 0)
                    {
                        return Array.Empty<MmdRuntimeFfiMethods.VmdBoneKeyframe>();
                    }

                    var result = new MmdRuntimeFfiMethods.VmdBoneKeyframe[count];
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = Marshal.PtrToStructure<MmdRuntimeFfiMethods.VmdBoneKeyframe>(
                            IntPtr.Add(buffer, checked(i * stride)));
                    }

                    return result;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (DllNotFoundException exception)
            {
                throw MmdRuntimeNativeBoundary.Unavailable("shared VMD context bone keyframes", exception);
            }
            catch (EntryPointNotFoundException exception)
            {
                throw MmdRuntimeNativeBoundary.Unavailable("shared VMD context bone keyframes", exception);
            }
            catch (BadImageFormatException exception)
            {
                throw MmdRuntimeNativeBoundary.Unavailable("shared VMD context bone keyframes", exception);
            }
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

        private int GetCount(Func<IntPtr, IntPtr> getCount, string label)
        {
            ThrowIfDisposed();
            try
            {
                return MmdFfiMarshal.CheckedIntPtrToInt(getCount(handle), label);
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
                if (count == 0)
                {
                    return Array.Empty<T>();
                }

                int stride = Marshal.SizeOf<T>();
                int byteCount = checked(stride * count);
                IntPtr buffer = Marshal.AllocHGlobal(byteCount);
                try
                {
                    int status = copy(handle, buffer, new IntPtr(count), out IntPtr written);
                    ThrowForStatus(status, label);
                    int copied = MmdFfiMarshal.CheckedIntPtrToInt(written, label + " copied count");
                    if (copied != count)
                    {
                        throw new InvalidOperationException(
                            "mmd-runtime " + label + " count changed during readback: expected " +
                            count + ", copied " + copied + ".");
                    }

                    var result = new T[count];
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = Marshal.PtrToStructure<T>(IntPtr.Add(buffer, checked(i * stride)));
                    }

                    return result;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
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

        private void ThrowIfDisposed()
        {
            if (handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeFfiVmdContext));
            }
        }
    }

    internal sealed class MmdRuntimeNativeUnavailableException : Exception
    {
        internal MmdRuntimeNativeUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    internal static class MmdRuntimeNativeBoundary
    {
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
