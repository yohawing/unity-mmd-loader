#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Mmd.Parser
{
    internal static class MmdParserFfiMethods
    {
        internal const string LibraryName = "mmd_runtime_ffi";
        internal const string ByteBufferFreeEntryPoint = "mmd_runtime_byte_buffer_free";
        internal const string ParseVmdJsonEntryPoint = "mmd_runtime_parse_vmd_json";
        internal const string ParsePmxNonGeometryJsonEntryPoint = "mmd_runtime_parse_pmx_non_geometry_json";

        // --- PMX geometry typed-buffer entrypoints ---
        internal const string ParsePmxPositionsBufferEntryPoint = "mmd_runtime_parse_pmx_positions_buffer";
        internal const string ParsePmxNormalsBufferEntryPoint = "mmd_runtime_parse_pmx_normals_buffer";
        internal const string ParsePmxUvsBufferEntryPoint = "mmd_runtime_parse_pmx_uvs_buffer";
        internal const string ParsePmxEdgeScaleBufferEntryPoint = "mmd_runtime_parse_pmx_edge_scale_buffer";
        internal const string ParsePmxIndicesBufferEntryPoint = "mmd_runtime_parse_pmx_indices_buffer";
        internal const string ParsePmxSkinIndicesBufferEntryPoint = "mmd_runtime_parse_pmx_skin_indices_buffer";
        internal const string ParsePmxSkinWeightsBufferEntryPoint = "mmd_runtime_parse_pmx_skin_weights_buffer";
        internal const string ParsePmxSdefEnabledBufferEntryPoint = "mmd_runtime_parse_pmx_sdef_enabled_buffer";
        internal const string ParsePmxSdefCBufferEntryPoint = "mmd_runtime_parse_pmx_sdef_c_buffer";
        internal const string ParsePmxSdefR0BufferEntryPoint = "mmd_runtime_parse_pmx_sdef_r0_buffer";
        internal const string ParsePmxSdefR1BufferEntryPoint = "mmd_runtime_parse_pmx_sdef_r1_buffer";
        internal const string ParsePmxSkinningModesJsonEntryPoint = "mmd_runtime_parse_pmx_skinning_modes_json";

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct ByteBuffer
        {
            public readonly IntPtr Data;
            public readonly IntPtr Length;
        }

        [DllImport(LibraryName, EntryPoint = ByteBufferFreeEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ByteBufferFree(ByteBuffer buffer);

        [DllImport(LibraryName, EntryPoint = ParseVmdJsonEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParseVmdJsonBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = ParsePmxNonGeometryJsonEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxNonGeometryJsonBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = ParsePmxPositionsBufferEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxPositionsBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = ParsePmxNormalsBufferEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxNormalsBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = ParsePmxUvsBufferEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxUvsBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = ParsePmxEdgeScaleBufferEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxEdgeScaleBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = ParsePmxIndicesBufferEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxIndicesBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = ParsePmxSkinIndicesBufferEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxSkinIndicesBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = ParsePmxSkinWeightsBufferEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxSkinWeightsBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = ParsePmxSdefEnabledBufferEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxSdefEnabledBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = ParsePmxSdefCBufferEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxSdefCBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = ParsePmxSdefR0BufferEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxSdefR0Buffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = ParsePmxSdefR1BufferEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxSdefR1Buffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = ParsePmxSkinningModesJsonEntryPoint, CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxSkinningModesJsonBuffer(byte[] data, IntPtr len);

        internal static string ParseVmdJson(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(data));
            }

            return ReadString(ParseVmdJsonBuffer(data, new IntPtr(data.Length)), "VMD parser JSON");
        }

        internal static string ParsePmxNonGeometryJson(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("PMX bytes are required.", nameof(data));
            }

            return ReadString(ParsePmxNonGeometryJsonBuffer(data, new IntPtr(data.Length)), "PMX non-geometry parser JSON");
        }

        internal static float[] ParsePmxPositions(byte[] data)
            => ByteBufferToFloatArray(ParsePmxPositionsBuffer(data, new IntPtr(data.Length)), "PMX positions buffer");

        internal static float[] ParsePmxNormals(byte[] data)
            => ByteBufferToFloatArray(ParsePmxNormalsBuffer(data, new IntPtr(data.Length)), "PMX normals buffer");

        internal static float[] ParsePmxUvs(byte[] data)
            => ByteBufferToFloatArray(ParsePmxUvsBuffer(data, new IntPtr(data.Length)), "PMX uvs buffer");

        internal static float[] ParsePmxEdgeScale(byte[] data)
            => ByteBufferToFloatArray(ParsePmxEdgeScaleBuffer(data, new IntPtr(data.Length)), "PMX edge scale buffer");

        internal static uint[] ParsePmxIndices(byte[] data)
            => ByteBufferToUintArray(ParsePmxIndicesBuffer(data, new IntPtr(data.Length)), "PMX indices buffer");

        internal static uint[] ParsePmxSkinIndices(byte[] data)
            => ByteBufferToUintArray(ParsePmxSkinIndicesBuffer(data, new IntPtr(data.Length)), "PMX skin indices buffer");

        internal static float[] ParsePmxSkinWeights(byte[] data)
            => ByteBufferToFloatArray(ParsePmxSkinWeightsBuffer(data, new IntPtr(data.Length)), "PMX skin weights buffer");

        internal static bool[] ParsePmxSdefEnabled(byte[] data)
            => ByteBufferToBoolArray(ParsePmxSdefEnabledBuffer(data, new IntPtr(data.Length)), "PMX sdef enabled buffer");

        internal static float[] ParsePmxSdefC(byte[] data)
            => ByteBufferToFloatArray(ParsePmxSdefCBuffer(data, new IntPtr(data.Length)), "PMX sdef C buffer");

        internal static float[] ParsePmxSdefR0(byte[] data)
            => ByteBufferToFloatArray(ParsePmxSdefR0Buffer(data, new IntPtr(data.Length)), "PMX sdef R0 buffer");

        internal static float[] ParsePmxSdefR1(byte[] data)
            => ByteBufferToFloatArray(ParsePmxSdefR1Buffer(data, new IntPtr(data.Length)), "PMX sdef R1 buffer");

        internal static string ParsePmxSkinningModesJson(byte[] data)
            => ReadString(ParsePmxSkinningModesJsonBuffer(data, new IntPtr(data.Length)), "PMX skinning modes JSON");

        private static float[] ByteBufferToFloatArray(ByteBuffer buffer, string label)
        {
            try
            {
                int byteLength = CheckedIntPtrToInt(buffer.Length, label + " byte length");
                if (buffer.Data == IntPtr.Zero || byteLength == 0)
                    return System.Array.Empty<float>();
                if (byteLength % 4 != 0)
                    throw new InvalidOperationException($"mmd-runtime {label} byte length {byteLength} is not a multiple of 4.");
                byte[] bytes = new byte[byteLength];
                Marshal.Copy(buffer.Data, bytes, 0, byteLength);
                float[] result = new float[byteLength / 4];
                Buffer.BlockCopy(bytes, 0, result, 0, byteLength);
                return result;
            }
            finally
            {
                ByteBufferFree(buffer);
            }
        }

        private static uint[] ByteBufferToUintArray(ByteBuffer buffer, string label)
        {
            try
            {
                int byteLength = CheckedIntPtrToInt(buffer.Length, label + " byte length");
                if (buffer.Data == IntPtr.Zero || byteLength == 0)
                    return System.Array.Empty<uint>();
                if (byteLength % 4 != 0)
                    throw new InvalidOperationException($"mmd-runtime {label} byte length {byteLength} is not a multiple of 4.");
                byte[] bytes = new byte[byteLength];
                Marshal.Copy(buffer.Data, bytes, 0, byteLength);
                uint[] result = new uint[byteLength / 4];
                Buffer.BlockCopy(bytes, 0, result, 0, byteLength);
                return result;
            }
            finally
            {
                ByteBufferFree(buffer);
            }
        }

        private static bool[] ByteBufferToBoolArray(ByteBuffer buffer, string label)
        {
            try
            {
                int byteLength = CheckedIntPtrToInt(buffer.Length, label + " byte length");
                if (buffer.Data == IntPtr.Zero || byteLength == 0)
                    return System.Array.Empty<bool>();
                byte[] bytes = new byte[byteLength];
                Marshal.Copy(buffer.Data, bytes, 0, byteLength);
                bool[] result = new bool[byteLength];
                for (int i = 0; i < byteLength; i++)
                    result[i] = bytes[i] != 0;
                return result;
            }
            finally
            {
                ByteBufferFree(buffer);
            }
        }

        private static string ReadString(ByteBuffer buffer, string label)
        {
            try
            {
                int byteLength = CheckedIntPtrToInt(buffer.Length, label + " byte length");
                if (buffer.Data == IntPtr.Zero || byteLength == 0)
                {
                    return string.Empty;
                }

                byte[] bytes = new byte[byteLength];
                Marshal.Copy(buffer.Data, bytes, 0, byteLength);
                return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            }
            finally
            {
                ByteBufferFree(buffer);
            }
        }

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
