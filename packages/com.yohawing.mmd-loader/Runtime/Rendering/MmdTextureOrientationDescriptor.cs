#nullable enable

using System;
using System.Collections.Generic;

namespace Mmd.Rendering
{
    [Serializable]
    public sealed class MmdTextureOrientationDescriptor
    {
        public string sourceUvConvention = MmdTextureOrientationDescriptorBuilder.MmdUvConvention;
        public string headlessSnapshotUvConvention = MmdTextureOrientationDescriptorBuilder.MmdUvConvention;
        public string viewportUvConvention = MmdTextureOrientationDescriptorBuilder.UnityUvConvention;
        public string viewportVTransform = MmdTextureOrientationDescriptorBuilder.ViewportVTransform;
        public bool flipVForViewport = true;
        public bool flipTexturePixels = false;
        public int vertexCount;
        public int uvCount;
        public float rawMinU;
        public float rawMaxU;
        public float rawMinV;
        public float rawMaxV;
        public float viewportMinV;
        public float viewportMaxV;
    }

    public static class MmdTextureOrientationDescriptorBuilder
    {
        public const string MmdUvConvention = "mmd-texture-space";
        public const string UnityUvConvention = "unity-viewport-space";
        public const string ViewportVTransform = "v = 1 - sourceV";

        public static MmdTextureOrientationDescriptor Build(IReadOnlyList<MmdMeshVertexDescriptor> vertices)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            var descriptor = new MmdTextureOrientationDescriptor
            {
                vertexCount = vertices.Count
            };

            bool hasUv = false;
            for (int i = 0; i < vertices.Count; i++)
            {
                MmdMeshVertexDescriptor vertex = vertices[i];
                if (vertex == null)
                {
                    throw new ArgumentException($"Rendering vertex {i} is null.", nameof(vertices));
                }

                if (vertex.uv == null || vertex.uv.Length == 0)
                {
                    continue;
                }

                ValidateUv(vertex.uv, $"vertices[{i}].uv");
                float u = vertex.uv[0];
                float v = vertex.uv[1];
                float viewportV = ToViewportV(v);
                if (!hasUv)
                {
                    descriptor.rawMinU = u;
                    descriptor.rawMaxU = u;
                    descriptor.rawMinV = v;
                    descriptor.rawMaxV = v;
                    descriptor.viewportMinV = viewportV;
                    descriptor.viewportMaxV = viewportV;
                    hasUv = true;
                }
                else
                {
                    descriptor.rawMinU = Math.Min(descriptor.rawMinU, u);
                    descriptor.rawMaxU = Math.Max(descriptor.rawMaxU, u);
                    descriptor.rawMinV = Math.Min(descriptor.rawMinV, v);
                    descriptor.rawMaxV = Math.Max(descriptor.rawMaxV, v);
                    descriptor.viewportMinV = Math.Min(descriptor.viewportMinV, viewportV);
                    descriptor.viewportMaxV = Math.Max(descriptor.viewportMaxV, viewportV);
                }

                descriptor.uvCount++;
            }

            return descriptor;
        }

        public static float[] ToViewportUv(float[] sourceUv)
        {
            ValidateUv(sourceUv, nameof(sourceUv));
            return new[] { sourceUv[0], ToViewportV(sourceUv[1]) };
        }

        private static float ToViewportV(float sourceV)
        {
            if (!IsFinite(sourceV))
            {
                throw new ArgumentException("Texture coordinate V must be finite.", nameof(sourceV));
            }

            return 1.0f - sourceV;
        }

        private static void ValidateUv(float[] sourceUv, string field)
        {
            if (sourceUv == null || sourceUv.Length < 2)
            {
                throw new ArgumentException($"{field} must contain at least 2 values.");
            }

            if (!IsFinite(sourceUv[0]) || !IsFinite(sourceUv[1]))
            {
                throw new ArgumentException($"{field} must contain only finite values.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
