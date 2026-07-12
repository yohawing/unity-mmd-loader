#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Mmd.Rendering;

namespace Mmd.UnityIntegration
{
    public static partial class MmdUnityModelFactory
    {
        private static Mesh BuildMesh(MmdRenderingDescriptor descriptor)
        {
            return BuildMesh(descriptor, importScale: 1.0f);
        }

        private static Mesh BuildMesh(MmdRenderingDescriptor descriptor, float importScale)
        {
            float scale = NormalizeImportScale(importScale);
            var mesh = new Mesh
            {
                name = "MMD Static Mesh",
                hideFlags = MmdUnityMaterialBuilder.RuntimeGeneratedAssetHideFlags,
                indexFormat = descriptor.vertices.Count > ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };

            var vertices = new List<Vector3>(descriptor.vertices.Count);
            var normals = new List<Vector3>(descriptor.vertices.Count);
            var uvs = new List<Vector2>(descriptor.vertices.Count);
            var edgeScales = new List<Vector2>(descriptor.vertices.Count);
            bool hasCompleteNormals = true;
            bool hasCompleteUvs = true;
            bool hasCompleteEdgeScale = true;

            foreach (MmdMeshVertexDescriptor vertex in descriptor.vertices)
            {
                vertices.Add(ToUnityPosition(vertex.position, scale));

                if (vertex.normal == null || vertex.normal.Length < 3)
                {
                    hasCompleteNormals = false;
                }
                else
                {
                    normals.Add(ToUnityNormal(vertex.normal));
                }

                if (vertex.uv == null || vertex.uv.Length < 2)
                {
                    hasCompleteUvs = false;
                }
                else
                {
                    float[] viewportUv = MmdTextureOrientationDescriptorBuilder.ToViewportUv(vertex.uv);
                    uvs.Add(new Vector2(viewportUv[0], viewportUv[1]));
                }

                if (!IsFinite(vertex.edgeScale))
                {
                    hasCompleteEdgeScale = false;
                }
                else
                {
                    edgeScales.Add(new Vector2(vertex.edgeScale, 1.0f));
                }
            }

            mesh.SetVertices(vertices);
            if (hasCompleteNormals && normals.Count == vertices.Count)
            {
                mesh.SetNormals(normals);
            }

            if (hasCompleteUvs && uvs.Count == vertices.Count)
            {
                mesh.SetUVs(0, uvs);
            }

            if (hasCompleteEdgeScale && edgeScales.Count == vertices.Count)
            {
                mesh.SetUVs(1, edgeScales);
            }

            mesh.subMeshCount = descriptor.submeshes.Count;
            foreach (MmdSubmeshDescriptor submesh in descriptor.submeshes)
            {
                int[] triangles = CopyUnityTriangleRange(descriptor.indices, submesh.indexStart, submesh.indexCount);
                mesh.SetTriangles(triangles, submesh.submeshIndex, false);
            }

            if (!hasCompleteNormals)
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static int[] CopyUnityTriangleRange(IReadOnlyList<int> source, int start, int count)
        {
            var triangles = new int[count];
            for (int offset = 0; offset < count; offset += 3)
            {
                triangles[offset] = source[start + offset];
                triangles[offset + 1] = source[start + offset + 1];
                triangles[offset + 2] = source[start + offset + 2];
            }

            return triangles;
        }
    }
}
