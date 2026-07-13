#nullable enable

using System;

namespace Mmd.Native
{
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
    }
}
