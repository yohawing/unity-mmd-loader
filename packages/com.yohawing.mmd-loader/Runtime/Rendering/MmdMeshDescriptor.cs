#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd.Parser;

namespace Mmd.Rendering
{
    [Serializable]
    public sealed class MmdMeshVertexDescriptor
    {
        public int vertexIndex;
        public float[] position = Array.Empty<float>();
        public float[] normal = Array.Empty<float>();
        public float[] uv = Array.Empty<float>();
        public float edgeScale = float.NaN;
    }

    internal static class MmdMeshDescriptorBuilder
    {
        public static IReadOnlyList<MmdMeshVertexDescriptor> BuildVertices(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            return BuildVerticesFromValidated(model);
        }

        /// <summary>
        /// Same as <see cref="BuildVertices"/> for a model the caller has already validated with
        /// <see cref="MmdModelValidator.ThrowIfInvalid"/>; skips the repeated validation.
        /// </summary>
        internal static IReadOnlyList<MmdMeshVertexDescriptor> BuildVerticesFromValidated(MmdModelDefinition model)
        {
            var descriptors = new List<MmdMeshVertexDescriptor>(model.vertices.Count);
            foreach (MmdVertexDefinition vertex in model.vertices.OrderBy(vertex => vertex.index))
            {
                descriptors.Add(new MmdMeshVertexDescriptor
                {
                    vertexIndex = vertex.index,
                    position = vertex.position.ToArray(),
                    normal = vertex.normal.ToArray(),
                    uv = vertex.uv.ToArray(),
                    edgeScale = vertex.edgeScale
                });
            }

            return descriptors;
        }

        public static IReadOnlyList<int> BuildIndices(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            return BuildIndicesFromValidated(model);
        }

        /// <summary>
        /// Same as <see cref="BuildIndices"/> for a model the caller has already validated with
        /// <see cref="MmdModelValidator.ThrowIfInvalid"/>; skips the repeated validation.
        /// </summary>
        internal static IReadOnlyList<int> BuildIndicesFromValidated(MmdModelDefinition model)
        {
            return model.indices.ToList();
        }
    }
}
