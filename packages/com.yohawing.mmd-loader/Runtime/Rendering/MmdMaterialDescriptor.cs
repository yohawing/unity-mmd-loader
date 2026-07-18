#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd.Parser;

namespace Mmd.Rendering
{
    [Serializable]
    public sealed class MmdMaterialDescriptor
    {
        public int materialIndex;
        public string name = string.Empty;
        public string texture = string.Empty;
        public string sphereTexture = string.Empty;
        public string toonTexture = string.Empty;
        public float alpha = 1.0f;
        public float[] diffuseColor = new[] { 1.0f, 1.0f, 1.0f };
        public float[] ambientColor = new[] { 0.25f, 0.25f, 0.25f };
        public float toonBoundary = -1.0f;
        public float toonFeather = -1.0f;
        public float toonBandCount = -1.0f;
        public float[] stylizedSpecularColor = new[] { 1.0f, 1.0f, 1.0f };
        public float stylizedSpecularBoundary = -1.0f;
        public float stylizedSpecularFeather = -1.0f;
        public float[] rimColor = new[] { 1.0f, 1.0f, 1.0f };
        public float rimBoundary = -1.0f;
        public float rimFeather = -1.0f;
        public float rimLightFollow;
        public float[] edgeColor = new[] { 0.0f, 0.0f, 0.0f, 1.0f };
        public float edgeSize;
        public string sphereTextureMode = string.Empty;
        public bool toonShared;

        /// <summary>
        /// 0-based MMD shared toon index (toon01..toon10) when <see cref="toonShared"/> is true;
        /// -1 when the material uses a custom toon texture or no toon at all.
        /// </summary>
        public int sharedToonIndex = -1;
        public string cullingPolicy = "unknown";
        public bool drawEdgeFlag;
        public int vertexStart;
        public int vertexCount;
    }

    public static class MmdMaterialDescriptorBuilder
    {
        public static IReadOnlyList<MmdMaterialDescriptor> Build(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            var descriptors = new List<MmdMaterialDescriptor>(model.materials.Count);
            int vertexStart = 0;
            foreach (MmdMaterialDefinition material in model.materials.OrderBy(item => item.index))
            {
                descriptors.Add(new MmdMaterialDescriptor
                {
                    materialIndex = material.index,
                    name = material.name,
                    texture = NormalizeOptionalString(material.texture),
                    sphereTexture = NormalizeOptionalString(material.sphereTexture),
                    toonTexture = NormalizeOptionalString(material.toonTexture),
                    alpha = material.alpha,
                    diffuseColor = CopyColor(material.diffuseColor, 3, new[] { 1.0f, 1.0f, 1.0f }),
                    ambientColor = CopyColor(material.ambientColor, 3, new[] { 0.25f, 0.25f, 0.25f }),
                    toonBoundary = -1.0f,
                    toonFeather = -1.0f,
                    toonBandCount = -1.0f,
                    stylizedSpecularColor = new[] { 1.0f, 1.0f, 1.0f },
                    stylizedSpecularBoundary = -1.0f,
                    stylizedSpecularFeather = -1.0f,
                    rimColor = new[] { 1.0f, 1.0f, 1.0f },
                    rimBoundary = -1.0f,
                    rimFeather = -1.0f,
                    rimLightFollow = 0.0f,
                    edgeColor = CopyColor(material.edgeColor, 4, new[] { 0.0f, 0.0f, 0.0f, 1.0f }),
                    edgeSize = ClampNonNegative(material.edgeSize),
                    sphereTextureMode = NormalizeOptionalString(material.sphereTextureMode),
                    toonShared = material.toonShared,
                    sharedToonIndex = material.sharedToonIndex,
                    cullingPolicy = NormalizeCullingPolicy(material.cullingPolicy),
                    drawEdgeFlag = material.drawEdgeFlag,
                    vertexStart = vertexStart,
                    vertexCount = material.vertexCount
                });
                vertexStart += material.vertexCount;
            }

            return descriptors;
        }

        private static string NormalizeOptionalString(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }

        private static string NormalizeCullingPolicy(string? value)
        {
            if (string.Equals(value, "double-sided", StringComparison.Ordinal) ||
                string.Equals(value, "backface-culling", StringComparison.Ordinal))
            {
                return value!;
            }

            return "unknown";
        }

        private static float[] CopyColor(float[]? values, int length, float[] fallback)
        {
            var result = new float[length];
            for (int i = 0; i < length; i++)
            {
                float value = values != null && i < values.Length ? values[i] : fallback[i];
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    value = fallback[i];
                }
                else if (value < 0.0f)
                {
                    value = 0.0f;
                }
                else if (value > 1.0f)
                {
                    value = 1.0f;
                }

                result[i] = value;
            }

            return result;
        }

        private static float ClampNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0.0f ? 0.0f : value;
        }
    }
}
