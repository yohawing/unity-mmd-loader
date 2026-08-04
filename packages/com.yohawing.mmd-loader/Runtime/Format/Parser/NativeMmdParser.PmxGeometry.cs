#nullable enable
#pragma warning disable CS0649

using System;

namespace Mmd.Parser
{
    internal sealed class PmxGeometryData
    {
        internal string skinningModesJson = string.Empty;
        internal float[] positions = Array.Empty<float>();
        internal float[] normals = Array.Empty<float>();
        internal float[] uvs = Array.Empty<float>();
        internal float[] edgeScale = Array.Empty<float>();
        internal uint[] indices = Array.Empty<uint>();
        internal uint[] skinIndices = Array.Empty<uint>();
        internal float[] skinWeights = Array.Empty<float>();
        internal bool[] hasSdefParameters = Array.Empty<bool>();
        internal float[] sdefC = Array.Empty<float>();
        internal float[] sdefR0 = Array.Empty<float>();
        internal float[] sdefR1 = Array.Empty<float>();
    }

    internal interface IPmxGeometryReader
    {
        IntPtr Create(byte[] data);
        PmxGeometryData ReadAll(IntPtr geometry);
        void Free(IntPtr geometry);
    }

    public sealed partial class NativeMmdParser
    {
        private sealed class NativePmxGeometryReader : IPmxGeometryReader
        {
            internal static readonly NativePmxGeometryReader Instance = new NativePmxGeometryReader();

            public IntPtr Create(byte[] data) => MmdParserFfiMethods.CreatePmxGeometry(data);
            public void Free(IntPtr geometry) => MmdParserFfiMethods.FreePmxGeometry(geometry);
            public PmxGeometryData ReadAll(IntPtr geometry)
            {
                return new PmxGeometryData
                {
                    skinningModesJson = MmdParserFfiMethods.ParsePmxGeometrySkinningModesJson(geometry),
                    positions = MmdParserFfiMethods.ParsePmxGeometryPositions(geometry),
                    normals = MmdParserFfiMethods.ParsePmxGeometryNormals(geometry),
                    uvs = MmdParserFfiMethods.ParsePmxGeometryUvs(geometry),
                    edgeScale = MmdParserFfiMethods.ParsePmxGeometryEdgeScale(geometry),
                    indices = MmdParserFfiMethods.ParsePmxGeometryIndices(geometry),
                    skinIndices = MmdParserFfiMethods.ParsePmxGeometrySkinIndices(geometry),
                    skinWeights = MmdParserFfiMethods.ParsePmxGeometrySkinWeights(geometry),
                    hasSdefParameters = MmdParserFfiMethods.ParsePmxGeometrySdefEnabled(geometry),
                    sdefC = MmdParserFfiMethods.ParsePmxGeometrySdefC(geometry),
                    sdefR0 = MmdParserFfiMethods.ParsePmxGeometrySdefR0(geometry),
                    sdefR1 = MmdParserFfiMethods.ParsePmxGeometrySdefR1(geometry),
                };
            }
        }

        internal static PmxModelSourceGeometry CreatePmxGeometryFromNativeBuffers(byte[] data)
            => CreatePmxGeometryFromNativeHandle(data, NativePmxGeometryReader.Instance);

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
                PmxGeometryData geometryData = reader.ReadAll(geometry);
                if (geometryData == null)
                {
                    throw new InvalidOperationException("mmd-runtime PMX geometry read returned null.");
                }

                return CreatePmxGeometry(geometryData);
            }
            finally
            {
                reader.Free(geometry);
            }
        }

        private static PmxModelSourceGeometry CreatePmxGeometry(PmxGeometryData data)
        {
            SkinningModesWrapper modesWrapper = string.IsNullOrWhiteSpace(data.skinningModesJson)
                ? new SkinningModesWrapper()
                : (UnityEngine.JsonUtility.FromJson<SkinningModesWrapper>(data.skinningModesJson) ?? new SkinningModesWrapper());

            return new PmxModelSourceGeometry
            {
                positions = data.positions,
                normals = data.normals,
                uvs = data.uvs,
                edgeScale = data.edgeScale,
                indices = data.indices,
                skinningModes = modesWrapper.skinningModes,
                skinIndices = data.skinIndices,
                skinWeights = data.skinWeights,
                hasSdefParameters = data.hasSdefParameters,
                sdefC = data.sdefC,
                sdefR0 = data.sdefR0,
                sdefR1 = data.sdefR1,
            };
        }

        [Serializable]
        private sealed class SkinningModesWrapper
        {
            public string[] skinningModes = Array.Empty<string>();
        }
    }
}
