#nullable enable

using System;
using System.Collections.Generic;

namespace Yohawing.MmdUnity.Rendering
{
    /// <summary>
    /// Evaluates texture UV morphs (PMX morph type "texture") on main mesh UV float2.
    /// Does not evaluate uva1-uva4 extra UV channels because
    /// <see cref="MmdMeshVertexDescriptor"/> currently has only <c>uv</c> and no uva1-uva4.
    /// </summary>
    public static class MmdTextureUvMorphEvaluator
    {
        /// <summary>
        /// Applies texture UV morph offsets to a copy of the input vertex descriptors.
        /// Only morph descriptors with <c>morphType == "texture"</c> are applied.
        /// For each active morph, <c>positionDelta[0] * weight</c> and
        /// <c>positionDelta[1] * weight</c> are added to the vertex <c>uv</c> float2.
        /// Vertices whose target index is not found in the base vertices are silently skipped.
        /// </summary>
        /// <param name="vertices">Base mesh vertex descriptors.</param>
        /// <param name="uvMorphs">UV morph descriptors built by <see cref="MmdMorphDescriptorBuilder.BuildUvMorphs"/>.</param>
        /// <param name="morphWeights">Sampled morph weights keyed by morph name.</param>
        /// <returns>New vertex descriptors with texture UV morph offsets applied.</returns>
        public static IReadOnlyList<MmdMeshVertexDescriptor> ApplyTextureUvMorphs(
            IReadOnlyList<MmdMeshVertexDescriptor> vertices,
            IReadOnlyList<MmdMorphDescriptorBuilder.MmdUvMorphDescriptor> uvMorphs,
            IReadOnlyDictionary<string, float> morphWeights)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            if (uvMorphs == null)
            {
                throw new ArgumentNullException(nameof(uvMorphs));
            }

            if (morphWeights == null)
            {
                throw new ArgumentNullException(nameof(morphWeights));
            }

            var result = new Dictionary<int, MmdMeshVertexDescriptor>(vertices.Count);
            for (int vertexListIndex = 0; vertexListIndex < vertices.Count; vertexListIndex++)
            {
                MmdMeshVertexDescriptor vertex = vertices[vertexListIndex]
                    ?? throw new InvalidOperationException($"Vertex descriptor is required: {vertexListIndex}");
                if (result.ContainsKey(vertex.vertexIndex))
                {
                    throw new InvalidOperationException($"Duplicate vertex descriptor index: {vertex.vertexIndex}");
                }

                float[] uv = CopyRequiredVector(vertex.uv, 2, $"vertices[{vertexListIndex}].uv");
                result[vertex.vertexIndex] = new MmdMeshVertexDescriptor
                {
                    vertexIndex = vertex.vertexIndex,
                    position = CopyRequiredVector(vertex.position, 3, $"vertices[{vertexListIndex}].position"),
                    normal = CopyRequiredVector(vertex.normal, 3, $"vertices[{vertexListIndex}].normal"),
                    uv = uv
                };
            }

            var sortedMorphs = new List<IndexedUvMorphDescriptor>(uvMorphs.Count);
            for (int morphListIndex = 0; morphListIndex < uvMorphs.Count; morphListIndex++)
            {
                MmdMorphDescriptorBuilder.MmdUvMorphDescriptor m = uvMorphs[morphListIndex];
                if (m == null || !string.Equals(m.morphType, "texture", StringComparison.Ordinal))
                {
                    continue;
                }

                sortedMorphs.Add(new IndexedUvMorphDescriptor(m, morphListIndex));
            }

            sortedMorphs.Sort(CompareIndexedMorphs);

            foreach (IndexedUvMorphDescriptor entry in sortedMorphs)
            {
                MmdMorphDescriptorBuilder.MmdUvMorphDescriptor morph = entry.Morph;

                if (string.IsNullOrWhiteSpace(morph.morphName))
                {
                    throw new InvalidOperationException($"Texture UV morph name is required: {morph.morphIndex}");
                }

                if (!morphWeights.TryGetValue(morph.morphName, out float weight) || weight == 0.0f)
                {
                    continue;
                }

                if (float.IsNaN(weight) || float.IsInfinity(weight))
                {
                    throw new InvalidOperationException($"Texture UV morph weight must be finite: {morph.morphName}");
                }

                if (morph.offsets == null)
                {
                    throw new InvalidOperationException($"Texture UV morph offsets are required: {morph.morphName}");
                }

                for (int offsetIndex = 0; offsetIndex < morph.offsets.Count; offsetIndex++)
                {
                    MmdMorphDescriptorBuilder.MmdUvMorphOffsetDescriptor offset = morph.offsets[offsetIndex];
                    if (offset == null)
                    {
                        throw new InvalidOperationException($"Texture UV morph offset is required: {morph.morphName}[{offsetIndex}]");
                    }

                    if (!result.TryGetValue(offset.vertexIndex, out MmdMeshVertexDescriptor? vertex))
                    {
                        continue;
                    }

                    float[] positionDelta = CopyRequiredVector(
                        offset.positionDelta, 4,
                        $"uvMorphs[{morph.morphIndex}].offsets[{offsetIndex}].positionDelta");

                    vertex.uv[0] += positionDelta[0] * weight;
                    vertex.uv[1] += positionDelta[1] * weight;
                }
            }

            var resultList = new List<MmdMeshVertexDescriptor>(result.Count);
            foreach (MmdMeshVertexDescriptor v in result.Values)
            {
                resultList.Add(v);
            }

            resultList.Sort((a, b) => a.vertexIndex.CompareTo(b.vertexIndex));
            return resultList;
        }

        private static int CompareIndexedMorphs(IndexedUvMorphDescriptor left, IndexedUvMorphDescriptor right)
        {
            int morphIndexComparison = left.Morph.morphIndex.CompareTo(right.Morph.morphIndex);
            if (morphIndexComparison != 0)
            {
                return morphIndexComparison;
            }

            return left.SourceIndex.CompareTo(right.SourceIndex);
        }

        private static float[] CopyRequiredVector(float[] values, int length, string field)
        {
            if (values == null || values.Length != length)
            {
                throw new InvalidOperationException($"{field} must have {length} values.");
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                {
                    throw new InvalidOperationException($"{field} must contain only finite values.");
                }
            }

            float[] copy = new float[length];
            Array.Copy(values, copy, length);
            return copy;
        }

        private readonly struct IndexedUvMorphDescriptor
        {
            public IndexedUvMorphDescriptor(MmdMorphDescriptorBuilder.MmdUvMorphDescriptor morph, int sourceIndex)
            {
                Morph = morph;
                SourceIndex = sourceIndex;
            }

            public MmdMorphDescriptorBuilder.MmdUvMorphDescriptor Morph { get; }

            public int SourceIndex { get; }
        }
    }
}
