#nullable enable
#pragma warning disable CS0649

using System;

namespace Mmd.Parser
{
    internal interface IPmxGeometryReader
    {
        IntPtr Create(byte[] data);
        void Free(IntPtr geometry);
        float[] Positions(IntPtr geometry);
        float[] Normals(IntPtr geometry);
        float[] Uvs(IntPtr geometry);
        float[] EdgeScale(IntPtr geometry);
        uint[] Indices(IntPtr geometry);
        uint[] SkinIndices(IntPtr geometry);
        float[] SkinWeights(IntPtr geometry);
        bool[] SdefEnabled(IntPtr geometry);
        float[] SdefC(IntPtr geometry);
        float[] SdefR0(IntPtr geometry);
        float[] SdefR1(IntPtr geometry);
        string SkinningModesJson(IntPtr geometry);
    }

    public sealed partial class NativeMmdParser
    {
        private sealed class NativePmxGeometryReader : IPmxGeometryReader
        {
            internal static readonly NativePmxGeometryReader Instance = new NativePmxGeometryReader();

            public IntPtr Create(byte[] data) => MmdParserFfiMethods.CreatePmxGeometry(data);
            public void Free(IntPtr geometry) => MmdParserFfiMethods.FreePmxGeometry(geometry);
            public float[] Positions(IntPtr geometry) => MmdParserFfiMethods.ParsePmxGeometryPositions(geometry);
            public float[] Normals(IntPtr geometry) => MmdParserFfiMethods.ParsePmxGeometryNormals(geometry);
            public float[] Uvs(IntPtr geometry) => MmdParserFfiMethods.ParsePmxGeometryUvs(geometry);
            public float[] EdgeScale(IntPtr geometry) => MmdParserFfiMethods.ParsePmxGeometryEdgeScale(geometry);
            public uint[] Indices(IntPtr geometry) => MmdParserFfiMethods.ParsePmxGeometryIndices(geometry);
            public uint[] SkinIndices(IntPtr geometry) => MmdParserFfiMethods.ParsePmxGeometrySkinIndices(geometry);
            public float[] SkinWeights(IntPtr geometry) => MmdParserFfiMethods.ParsePmxGeometrySkinWeights(geometry);
            public bool[] SdefEnabled(IntPtr geometry) => MmdParserFfiMethods.ParsePmxGeometrySdefEnabled(geometry);
            public float[] SdefC(IntPtr geometry) => MmdParserFfiMethods.ParsePmxGeometrySdefC(geometry);
            public float[] SdefR0(IntPtr geometry) => MmdParserFfiMethods.ParsePmxGeometrySdefR0(geometry);
            public float[] SdefR1(IntPtr geometry) => MmdParserFfiMethods.ParsePmxGeometrySdefR1(geometry);
            public string SkinningModesJson(IntPtr geometry) => MmdParserFfiMethods.ParsePmxGeometrySkinningModesJson(geometry);
        }

        internal static PmxModelSourceGeometry CreatePmxGeometryFromNativeBuffers(byte[] data)
        {
            try
            {
                return CreatePmxGeometryFromNativeHandle(data, NativePmxGeometryReader.Instance);
            }
            catch (EntryPointNotFoundException)
            {
                // Packages carrying an older ABI-3 DLL keep the legacy calls at this boundary only.
                return CreatePmxGeometryFromLegacyBuffers(data);
            }
        }

        internal static PmxModelSourceGeometry CreatePmxGeometryFromNativeHandle(byte[] data, IPmxGeometryReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            IntPtr geometry = reader.Create(data);
            if (geometry == IntPtr.Zero)
            {
                throw new InvalidOperationException("mmd-runtime PMX geometry handle creation returned null.");
            }

            try
            {
                string modesJson = reader.SkinningModesJson(geometry);
                return CreatePmxGeometry(
                    modesJson,
                    reader.Positions(geometry),
                    reader.Normals(geometry),
                    reader.Uvs(geometry),
                    reader.EdgeScale(geometry),
                    reader.Indices(geometry),
                    reader.SkinIndices(geometry),
                    reader.SkinWeights(geometry),
                    reader.SdefEnabled(geometry),
                    reader.SdefC(geometry),
                    reader.SdefR0(geometry),
                    reader.SdefR1(geometry));
            }
            finally
            {
                reader.Free(geometry);
            }
        }

        private static PmxModelSourceGeometry CreatePmxGeometryFromLegacyBuffers(byte[] data)
        {
            return CreatePmxGeometry(
                MmdParserFfiMethods.ParsePmxSkinningModesJson(data),
                MmdParserFfiMethods.ParsePmxPositions(data),
                MmdParserFfiMethods.ParsePmxNormals(data),
                MmdParserFfiMethods.ParsePmxUvs(data),
                MmdParserFfiMethods.ParsePmxEdgeScale(data),
                MmdParserFfiMethods.ParsePmxIndices(data),
                MmdParserFfiMethods.ParsePmxSkinIndices(data),
                MmdParserFfiMethods.ParsePmxSkinWeights(data),
                MmdParserFfiMethods.ParsePmxSdefEnabled(data),
                MmdParserFfiMethods.ParsePmxSdefC(data),
                MmdParserFfiMethods.ParsePmxSdefR0(data),
                MmdParserFfiMethods.ParsePmxSdefR1(data));
        }

        private static PmxModelSourceGeometry CreatePmxGeometry(
            string modesJson,
            float[] positions,
            float[] normals,
            float[] uvs,
            float[] edgeScale,
            uint[] indices,
            uint[] skinIndices,
            float[] skinWeights,
            bool[] hasSdefParameters,
            float[] sdefC,
            float[] sdefR0,
            float[] sdefR1)
        {
            SkinningModesWrapper modesWrapper = string.IsNullOrWhiteSpace(modesJson)
                ? new SkinningModesWrapper()
                : (UnityEngine.JsonUtility.FromJson<SkinningModesWrapper>(modesJson) ?? new SkinningModesWrapper());

            return new PmxModelSourceGeometry
            {
                positions = positions,
                normals = normals,
                uvs = uvs,
                edgeScale = edgeScale,
                indices = indices,
                skinningModes = modesWrapper.skinningModes,
                skinIndices = skinIndices,
                skinWeights = skinWeights,
                hasSdefParameters = hasSdefParameters,
                sdefC = sdefC,
                sdefR0 = sdefR0,
                sdefR1 = sdefR1,
            };
        }

        [Serializable]
        private sealed class SkinningModesWrapper
        {
            public string[] skinningModes = Array.Empty<string>();
        }
    }
}
