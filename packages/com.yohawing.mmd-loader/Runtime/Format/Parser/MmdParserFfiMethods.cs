#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Text;
using Mmd.Native;

namespace Mmd.Parser
{
    internal static class MmdParserFfiMethods
    {
        internal const string LibraryName = "mmd_runtime_ffi";

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct ByteBuffer
        {
            public readonly IntPtr Data;
            public readonly IntPtr Length;
        }

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_byte_buffer_free", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ByteBufferFree(ByteBuffer buffer);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_parse_vmd_json", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParseVmdJsonBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_parse_pmx_non_geometry_json", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxNonGeometryJsonBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_parse_pmx_positions_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxPositionsBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_parse_pmx_normals_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxNormalsBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_parse_pmx_uvs_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxUvsBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_parse_pmx_edge_scale_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxEdgeScaleBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_parse_pmx_indices_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxIndicesBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_parse_pmx_skin_indices_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxSkinIndicesBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_parse_pmx_skin_weights_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxSkinWeightsBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_parse_pmx_sdef_enabled_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxSdefEnabledBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_parse_pmx_sdef_c_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxSdefCBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_parse_pmx_sdef_r0_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxSdefR0Buffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_parse_pmx_sdef_r1_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxSdefR1Buffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_parse_pmx_skinning_modes_json", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer ParsePmxSkinningModesJsonBuffer(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_pmx_geometry_create", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PmxGeometryCreate(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_pmx_geometry_free", CallingConvention = CallingConvention.Cdecl)]
        private static extern void PmxGeometryFree(IntPtr geometry);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_pmx_geometry_positions_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxGeometryPositionsBuffer(IntPtr geometry);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_pmx_geometry_normals_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxGeometryNormalsBuffer(IntPtr geometry);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_pmx_geometry_uvs_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxGeometryUvsBuffer(IntPtr geometry);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_pmx_geometry_edge_scale_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxGeometryEdgeScaleBuffer(IntPtr geometry);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_pmx_geometry_indices_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxGeometryIndicesBuffer(IntPtr geometry);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_pmx_geometry_skin_indices_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxGeometrySkinIndicesBuffer(IntPtr geometry);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_pmx_geometry_skin_weights_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxGeometrySkinWeightsBuffer(IntPtr geometry);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_pmx_geometry_sdef_enabled_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxGeometrySdefEnabledBuffer(IntPtr geometry);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_pmx_geometry_sdef_c_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxGeometrySdefCBuffer(IntPtr geometry);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_pmx_geometry_sdef_r0_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxGeometrySdefR0Buffer(IntPtr geometry);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_pmx_geometry_sdef_r1_buffer", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxGeometrySdefR1Buffer(IntPtr geometry);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_pmx_geometry_skinning_modes_json", CallingConvention = CallingConvention.Cdecl)]
        private static extern ByteBuffer PmxGeometrySkinningModesJsonBuffer(IntPtr geometry);

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

        internal static IntPtr CreatePmxGeometry(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("PMX bytes are required.", nameof(data));
            }

            return PmxGeometryCreate(data, new IntPtr(data.Length));
        }

        internal static void FreePmxGeometry(IntPtr geometry)
        {
            if (geometry != IntPtr.Zero)
            {
                PmxGeometryFree(geometry);
            }
        }

        internal static float[] ParsePmxGeometryPositions(IntPtr geometry)
            => ByteBufferToFloatArray(PmxGeometryPositionsBuffer(geometry), "PMX geometry positions buffer");

        internal static float[] ParsePmxGeometryNormals(IntPtr geometry)
            => ByteBufferToFloatArray(PmxGeometryNormalsBuffer(geometry), "PMX geometry normals buffer");

        internal static float[] ParsePmxGeometryUvs(IntPtr geometry)
            => ByteBufferToFloatArray(PmxGeometryUvsBuffer(geometry), "PMX geometry uvs buffer");

        internal static float[] ParsePmxGeometryEdgeScale(IntPtr geometry)
            => ByteBufferToFloatArray(PmxGeometryEdgeScaleBuffer(geometry), "PMX geometry edge scale buffer");

        internal static uint[] ParsePmxGeometryIndices(IntPtr geometry)
            => ByteBufferToUintArray(PmxGeometryIndicesBuffer(geometry), "PMX geometry indices buffer");

        internal static uint[] ParsePmxGeometrySkinIndices(IntPtr geometry)
            => ByteBufferToUintArray(PmxGeometrySkinIndicesBuffer(geometry), "PMX geometry skin indices buffer");

        internal static float[] ParsePmxGeometrySkinWeights(IntPtr geometry)
            => ByteBufferToFloatArray(PmxGeometrySkinWeightsBuffer(geometry), "PMX geometry skin weights buffer");

        internal static bool[] ParsePmxGeometrySdefEnabled(IntPtr geometry)
            => ByteBufferToBoolArray(PmxGeometrySdefEnabledBuffer(geometry), "PMX geometry SDEF enabled buffer");

        internal static float[] ParsePmxGeometrySdefC(IntPtr geometry)
            => ByteBufferToFloatArray(PmxGeometrySdefCBuffer(geometry), "PMX geometry SDEF C buffer");

        internal static float[] ParsePmxGeometrySdefR0(IntPtr geometry)
            => ByteBufferToFloatArray(PmxGeometrySdefR0Buffer(geometry), "PMX geometry SDEF R0 buffer");

        internal static float[] ParsePmxGeometrySdefR1(IntPtr geometry)
            => ByteBufferToFloatArray(PmxGeometrySdefR1Buffer(geometry), "PMX geometry SDEF R1 buffer");

        internal static string ParsePmxGeometrySkinningModesJson(IntPtr geometry)
            => ReadString(PmxGeometrySkinningModesJsonBuffer(geometry), "PMX geometry skinning modes JSON");

        private static float[] ByteBufferToFloatArray(ByteBuffer buffer, string label)
            => ByteBufferToArray4<float>(buffer, label);

        private static uint[] ByteBufferToUintArray(ByteBuffer buffer, string label)
            => ByteBufferToArray4<uint>(buffer, label);

        private static T[] ByteBufferToArray4<T>(ByteBuffer buffer, string label) where T : struct
        {
            try
            {
                int byteLength = MmdFfiMarshal.CheckedIntPtrToInt(buffer.Length, label + " byte length");
                if (buffer.Data == IntPtr.Zero || byteLength == 0)
                    return System.Array.Empty<T>();
                if (byteLength % 4 != 0)
                    throw new InvalidOperationException($"mmd-runtime {label} byte length {byteLength} is not a multiple of 4.");
                byte[] bytes = new byte[byteLength];
                Marshal.Copy(buffer.Data, bytes, 0, byteLength);
                T[] result = new T[byteLength / 4];
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
                int byteLength = MmdFfiMarshal.CheckedIntPtrToInt(buffer.Length, label + " byte length");
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
                int byteLength = MmdFfiMarshal.CheckedIntPtrToInt(buffer.Length, label + " byte length");
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

    }
}
