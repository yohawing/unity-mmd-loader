#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Mmd.Native
{
    internal delegate int NativeStructCopyDelegate(
        IntPtr output,
        IntPtr capacity,
        out IntPtr written);

    internal delegate void NativeStatusValidator(int status, string operation);

    internal static class MmdFfiMarshal
    {
        internal static int CheckedIntPtrToInt(IntPtr value, string label)
        {
            long raw = value.ToInt64();
            if (raw < 0 || raw > int.MaxValue)
            {
                throw new InvalidOperationException($"mmd-runtime {label} is out of range: {raw}");
            }

            return (int)raw;
        }

        internal static T[] CopyStructArray<T>(
            int count,
            string label,
            NativeStructCopyDelegate copy,
            NativeStatusValidator validateStatus)
            where T : struct
        {
            if (count == 0)
            {
                return Array.Empty<T>();
            }

            int stride = Marshal.SizeOf<T>();
            IntPtr buffer = Marshal.AllocHGlobal(checked(stride * count));
            try
            {
                validateStatus(
                    copy(buffer, new IntPtr(count), out IntPtr written),
                    label);
                int copied = CheckedIntPtrToInt(written, label + " copied count");
                if (copied != count)
                {
                    throw new InvalidOperationException(
                        "mmd-runtime " + label + " count changed during readback: expected "
                        + count + ", copied " + copied + ".");
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
    }
}
