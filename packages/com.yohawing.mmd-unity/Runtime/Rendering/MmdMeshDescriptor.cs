using System;
using System.Collections.Generic;
using System.Linq;
using Yohawing.MmdUnity.Parser;

namespace Yohawing.MmdUnity.Rendering
{
    [Serializable]
    public sealed class MmdMeshVertexDescriptor
    {
        public int vertexIndex;
        public float[] position = Array.Empty<float>();
        public float[] normal = Array.Empty<float>();
        public float[] uv = Array.Empty<float>();
    }

    public static class MmdMeshDescriptorBuilder
    {
        public static IReadOnlyList<MmdMeshVertexDescriptor> BuildVertices(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            var descriptors = new List<MmdMeshVertexDescriptor>(model.vertices.Count);
            foreach (MmdVertexDefinition vertex in model.vertices.OrderBy(vertex => vertex.index))
            {
                descriptors.Add(new MmdMeshVertexDescriptor
                {
                    vertexIndex = vertex.index,
                    position = vertex.position.ToArray(),
                    normal = vertex.normal.ToArray(),
                    uv = vertex.uv.ToArray()
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
            return model.indices.ToList();
        }
    }
}
