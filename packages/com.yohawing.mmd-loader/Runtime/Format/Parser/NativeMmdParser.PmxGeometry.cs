#nullable enable
#pragma warning disable CS0649

using System;

namespace Mmd.Parser
{
    public sealed partial class NativeMmdParser
    {
        internal static PmxModelSourceGeometry CreatePmxGeometryFromNativeBuffers(byte[] data)
        {
            string modesJson = MmdParserFfiMethods.ParsePmxSkinningModesJson(data);
            SkinningModesWrapper modesWrapper = string.IsNullOrWhiteSpace(modesJson)
                ? new SkinningModesWrapper()
                : (UnityEngine.JsonUtility.FromJson<SkinningModesWrapper>(modesJson) ?? new SkinningModesWrapper());

            return new PmxModelSourceGeometry
            {
                positions = MmdParserFfiMethods.ParsePmxPositions(data),
                normals = MmdParserFfiMethods.ParsePmxNormals(data),
                uvs = MmdParserFfiMethods.ParsePmxUvs(data),
                edgeScale = MmdParserFfiMethods.ParsePmxEdgeScale(data),
                indices = MmdParserFfiMethods.ParsePmxIndices(data),
                skinningModes = modesWrapper.skinningModes,
                skinIndices = MmdParserFfiMethods.ParsePmxSkinIndices(data),
                skinWeights = MmdParserFfiMethods.ParsePmxSkinWeights(data),
                hasSdefParameters = MmdParserFfiMethods.ParsePmxSdefEnabled(data),
                sdefC = MmdParserFfiMethods.ParsePmxSdefC(data),
                sdefR0 = MmdParserFfiMethods.ParsePmxSdefR0(data),
                sdefR1 = MmdParserFfiMethods.ParsePmxSdefR1(data),
            };
        }

        [Serializable]
        private sealed class SkinningModesWrapper
        {
            public string[] skinningModes = Array.Empty<string>();
        }
    }
}
