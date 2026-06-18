#nullable enable

using System;
using Yohawing.MmdUnity.Rendering;

namespace Yohawing.MmdUnity.UnityIntegration
{
    public static partial class MmdUnityModelFactory
    {
        private static void ValidateDescriptor(MmdRenderingDescriptor descriptor)
        {
            if (descriptor.vertices == null)
            {
                throw new ArgumentException("Rendering descriptor vertices are required.", nameof(descriptor));
            }

            if (descriptor.indices == null)
            {
                throw new ArgumentException("Rendering descriptor indices are required.", nameof(descriptor));
            }

            if (descriptor.materials == null)
            {
                throw new ArgumentException("Rendering descriptor materials are required.", nameof(descriptor));
            }

            if (descriptor.submeshes == null)
            {
                throw new ArgumentException("Rendering descriptor submeshes are required.", nameof(descriptor));
            }

            for (int i = 0; i < descriptor.vertices.Count; i++)
            {
                MmdMeshVertexDescriptor vertex = descriptor.vertices[i];
                if (vertex == null)
                {
                    throw new ArgumentException($"Rendering vertex {i} is null.", nameof(descriptor));
                }

                ValidateVector3(vertex.position, $"vertices[{i}].position");
                if (vertex.normal != null && vertex.normal.Length > 0)
                {
                    ValidateVector3(vertex.normal, $"vertices[{i}].normal");
                }

                if (vertex.uv != null && vertex.uv.Length > 0 && vertex.uv.Length < 2)
                {
                    throw new ArgumentException($"Rendering vertex {i} uv must contain at least 2 values.", nameof(descriptor));
                }
            }

            for (int i = 0; i < descriptor.indices.Count; i++)
            {
                int index = descriptor.indices[i];
                if (index < 0 || index >= descriptor.vertices.Count)
                {
                    throw new ArgumentException($"Rendering index {i} points outside the vertex buffer.", nameof(descriptor));
                }
            }

            for (int i = 0; i < descriptor.submeshes.Count; i++)
            {
                MmdSubmeshDescriptor submesh = descriptor.submeshes[i];
                if (submesh == null)
                {
                    throw new ArgumentException($"Rendering submesh {i} is null.", nameof(descriptor));
                }

                if (submesh.submeshIndex != i)
                {
                    throw new ArgumentException($"Rendering submesh {i} has unexpected submeshIndex {submesh.submeshIndex}.", nameof(descriptor));
                }

                if (submesh.indexStart < 0 || submesh.indexCount < 0)
                {
                    throw new ArgumentException($"Rendering submesh {i} has a negative index range.", nameof(descriptor));
                }

                if (submesh.indexCount % 3 != 0 || submesh.indexStart % 3 != 0)
                {
                    throw new ArgumentException($"Rendering submesh {i} range must align to triangles.", nameof(descriptor));
                }

                if (submesh.indexStart + submesh.indexCount > descriptor.indices.Count)
                {
                    throw new ArgumentException($"Rendering submesh {i} range exceeds the index buffer.", nameof(descriptor));
                }
            }
        }

        private static void ValidateVector3(float[] values, string field)
        {
            if (values == null || values.Length < 3)
            {
                throw new ArgumentException($"Rendering {field} must contain at least 3 values.");
            }

            if (!IsFinite(values[0]) || !IsFinite(values[1]) || !IsFinite(values[2]))
            {
                throw new ArgumentException($"Rendering {field} contains a non-finite value.");
            }
        }
    }
}
