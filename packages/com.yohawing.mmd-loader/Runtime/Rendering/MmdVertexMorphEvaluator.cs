#nullable enable

using System;
using System.Collections.Generic;

namespace Mmd.Rendering
{
    public static class MmdVertexMorphEvaluator
    {
        public static IReadOnlyList<MmdMeshVertexDescriptor> ApplyVertexMorphs(
            IReadOnlyList<MmdMeshVertexDescriptor> vertices,
            IReadOnlyList<MmdVertexMorphDescriptor> vertexMorphs,
            IReadOnlyDictionary<string, float> morphWeights)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            if (vertexMorphs == null)
            {
                throw new ArgumentNullException(nameof(vertexMorphs));
            }

            if (morphWeights == null)
            {
                throw new ArgumentNullException(nameof(morphWeights));
            }

            var result = new Dictionary<int, MmdMeshVertexDescriptor>(vertices.Count);
            for (int vertexListIndex = 0; vertexListIndex < vertices.Count; vertexListIndex++)
            {
                MmdMeshVertexDescriptor vertex = vertices[vertexListIndex] ?? throw new InvalidOperationException($"Vertex descriptor is required: {vertexListIndex}");
                if (result.ContainsKey(vertex.vertexIndex))
                {
                    throw new InvalidOperationException($"Duplicate vertex descriptor index: {vertex.vertexIndex}");
                }

                result[vertex.vertexIndex] = new MmdMeshVertexDescriptor
                {
                    vertexIndex = vertex.vertexIndex,
                    position = CopyRequiredVector(vertex.position, 3, $"vertices[{vertexListIndex}].position"),
                    normal = CopyRequiredVector(vertex.normal, 3, $"vertices[{vertexListIndex}].normal"),
                    uv = CopyRequiredVector(vertex.uv, 2, $"vertices[{vertexListIndex}].uv")
                };
            }

            var sortedMorphs = new List<IndexedVertexMorphDescriptor>(vertexMorphs.Count);
            for (int morphListIndex = 0; morphListIndex < vertexMorphs.Count; morphListIndex++)
            {
                sortedMorphs.Add(new IndexedVertexMorphDescriptor(vertexMorphs[morphListIndex], morphListIndex));
            }

            sortedMorphs.Sort(CompareIndexedMorphs);

            foreach (IndexedVertexMorphDescriptor entry in sortedMorphs)
            {
                MmdVertexMorphDescriptor morph = entry.Morph;
                if (morph == null)
                {
                    throw new InvalidOperationException("Vertex morph descriptor is required.");
                }

                if (string.IsNullOrWhiteSpace(morph.morphName))
                {
                    throw new InvalidOperationException($"Vertex morph name is required: {morph.morphIndex}");
                }

                if (!morphWeights.TryGetValue(morph.morphName, out float weight) || weight == 0.0f)
                {
                    continue;
                }

                if (float.IsNaN(weight) || float.IsInfinity(weight))
                {
                    throw new InvalidOperationException($"Morph weight must be finite: {morph.morphName}");
                }

                if (morph.offsets == null)
                {
                    throw new InvalidOperationException($"Vertex morph offsets are required: {morph.morphName}");
                }

                for (int offsetIndex = 0; offsetIndex < morph.offsets.Count; offsetIndex++)
                {
                    MmdVertexMorphOffsetDescriptor offset = morph.offsets[offsetIndex] ?? throw new InvalidOperationException($"Vertex morph offset is required: {morph.morphName}[{offsetIndex}]");
                    if (!result.TryGetValue(offset.vertexIndex, out MmdMeshVertexDescriptor? vertex))
                    {
                        continue;
                    }

                    float[] positionDelta = CopyRequiredVector(offset.positionDelta, 3, $"vertexMorphs[{morph.morphIndex}].offsets[{offsetIndex}].positionDelta");
                    vertex.position[0] += positionDelta[0] * weight;
                    vertex.position[1] += positionDelta[1] * weight;
                    vertex.position[2] += positionDelta[2] * weight;
                }
            }

            var resultList = new List<MmdMeshVertexDescriptor>(result.Values);
            resultList.Sort((a, b) => a.vertexIndex.CompareTo(b.vertexIndex));
            return resultList;
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

        private static int CompareIndexedMorphs(IndexedVertexMorphDescriptor left, IndexedVertexMorphDescriptor right)
        {
            int leftMorphIndex = left.Morph?.morphIndex ?? int.MaxValue;
            int rightMorphIndex = right.Morph?.morphIndex ?? int.MaxValue;
            int morphIndexComparison = leftMorphIndex.CompareTo(rightMorphIndex);
            if (morphIndexComparison != 0)
            {
                return morphIndexComparison;
            }

            return left.SourceIndex.CompareTo(right.SourceIndex);
        }

        private readonly struct IndexedVertexMorphDescriptor
        {
            public IndexedVertexMorphDescriptor(MmdVertexMorphDescriptor morph, int sourceIndex)
            {
                Morph = morph;
                SourceIndex = sourceIndex;
            }

            public MmdVertexMorphDescriptor Morph { get; }

            public int SourceIndex { get; }
        }
    }
}
