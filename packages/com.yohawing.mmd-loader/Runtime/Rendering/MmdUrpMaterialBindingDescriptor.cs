#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mmd.Rendering
{
    [Serializable]
    public sealed class MmdUrpMaterialBindingDescriptor
    {
        public int materialIndex;
        public string name = string.Empty;
        public string shaderName = string.Empty;
        public string baseMapTexture = string.Empty;
        public string sphereTexture = string.Empty;
        public string toonTexture = string.Empty;
        public string sphereTextureModeHint = string.Empty;
        public string toonTextureSourceHint = string.Empty;
        public bool usesSphereTexture;
        public bool usesToonTexture;
        public float alpha = 1.0f;
        public float[] diffuseColor = new[] { 1.0f, 1.0f, 1.0f };
        public float[] ambientColor = new[] { 0.25f, 0.25f, 0.25f };
        public float[] emissionColor = new[] { 1.0f, 1.0f, 1.0f };
        public float emissionIntensity = -1.0f;
        public bool usesEmissionMap;
        public bool usesEmissionMask;
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
        public bool drawEdgeFlag;
        public bool isTransparent;
        public string transparencyMode = "opaque";
        public string renderOrderBucket = "opaque";
        public string cullingPolicy = "unknown";
        public int vertexStart;
        public int vertexCount;
    }

    public static class MmdUrpMaterialBindingDescriptorBuilder
    {
        public const string DefaultShaderName = "MMD Basic URP Toon";
        public const string MmdToonLitShaderName = "MMD Toon Lit";
        public const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        public static IReadOnlyList<MmdUrpMaterialBindingDescriptor> Build(
            IReadOnlyList<MmdMaterialDescriptor> materials,
            MmdMaterialPreset preset)
        {
            string shaderName = preset switch
            {
                MmdMaterialPreset.UrpLit => UrpLitShaderName,
                MmdMaterialPreset.MmdToonLit => MmdToonLitShaderName,
                _ => DefaultShaderName
            };
            return Build(materials, shaderName);
        }

        public static IReadOnlyList<MmdUrpMaterialBindingDescriptor> Build(
            IReadOnlyList<MmdMaterialDescriptor> materials,
            string shaderName = DefaultShaderName)
        {
            if (materials == null)
            {
                throw new ArgumentNullException(nameof(materials));
            }

            if (string.IsNullOrWhiteSpace(shaderName))
            {
                throw new ArgumentException("Shader name is required.", nameof(shaderName));
            }

            var bindings = new List<MmdUrpMaterialBindingDescriptor>(materials.Count);
            foreach (MmdMaterialDescriptor material in materials.OrderBy(material => material.materialIndex))
            {
                bindings.Add(new MmdUrpMaterialBindingDescriptor
                {
                    materialIndex = material.materialIndex,
                    name = material.name ?? string.Empty,
                    shaderName = shaderName,
                    baseMapTexture = NormalizeOptionalString(material.texture),
                    sphereTexture = NormalizeOptionalString(material.sphereTexture),
                    toonTexture = NormalizeOptionalString(material.toonTexture),
                    sphereTextureModeHint = ResolveSphereTextureModeHint(material.sphereTextureMode, material.sphereTexture),
                    toonTextureSourceHint = ResolveToonTextureSourceHint(material.toonShared, material.toonTexture),
                    usesSphereTexture = !string.IsNullOrWhiteSpace(material.sphereTexture),
                    usesToonTexture = !string.IsNullOrWhiteSpace(material.toonTexture),
                    alpha = material.alpha,
                    diffuseColor = CopyColor(material.diffuseColor, 3, new[] { 1.0f, 1.0f, 1.0f }),
                    ambientColor = CopyColor(material.ambientColor, 3, new[] { 0.25f, 0.25f, 0.25f }),
                    emissionColor = CopyHdrColor(material.emissionColor, 3, new[] { 1.0f, 1.0f, 1.0f }),
                    emissionIntensity = material.emissionIntensity,
                    usesEmissionMap = material.usesEmissionMap,
                    usesEmissionMask = material.usesEmissionMask,
                    toonBoundary = material.toonBoundary,
                    toonFeather = material.toonFeather,
                    toonBandCount = material.toonBandCount,
                    stylizedSpecularColor = CopyColor(material.stylizedSpecularColor, 3, new[] { 1.0f, 1.0f, 1.0f }),
                    stylizedSpecularBoundary = material.stylizedSpecularBoundary,
                    stylizedSpecularFeather = material.stylizedSpecularFeather,
                    rimColor = CopyColor(material.rimColor, 3, new[] { 1.0f, 1.0f, 1.0f }),
                    rimBoundary = material.rimBoundary,
                    rimFeather = material.rimFeather,
                    rimLightFollow = material.rimLightFollow,
                    edgeColor = CopyColor(material.edgeColor, 4, new[] { 0.0f, 0.0f, 0.0f, 1.0f }),
                    edgeSize = material.drawEdgeFlag ? ClampNonNegative(material.edgeSize) : 0.0f,
                    drawEdgeFlag = material.drawEdgeFlag,
                    isTransparent = material.alpha < 1.0f,
                    transparencyMode = material.alpha < 1.0f ? "alphaBlend" : "opaque",
                    renderOrderBucket = material.alpha < 1.0f ? "alphaBlend" : "opaque",
                    cullingPolicy = NormalizeCullingPolicy(material.cullingPolicy),
                    vertexStart = material.vertexStart,
                    vertexCount = material.vertexCount
                });
            }

            return bindings;
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

        private static string ResolveSphereTextureModeHint(string? mode, string? texture)
        {
            if (!string.IsNullOrWhiteSpace(mode))
            {
                return mode;
            }

            if (string.IsNullOrWhiteSpace(texture))
            {
                return "none";
            }

            string extension = Path.GetExtension(texture).ToLowerInvariant();
            return extension switch
            {
                ".spa" => "additive-sphere",
                ".sph" => "multiply-sphere",
                _ => "unknown-sphere"
            };
        }

        private static string ResolveToonTextureSourceHint(bool toonShared, string? texture)
        {
            if (string.IsNullOrWhiteSpace(texture))
            {
                return "none";
            }

            if (toonShared)
            {
                return "shared-toon-reference";
            }

            string fileName = Path.GetFileName(texture);
            return fileName.StartsWith("toon", StringComparison.OrdinalIgnoreCase)
                ? "toon-reference"
                : "custom-toon-reference";
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

        private static float[] CopyHdrColor(float[]? values, int length, float[] fallback)
        {
            var result = new float[length];
            for (int i = 0; i < length; i++)
            {
                float value = values != null && i < values.Length ? values[i] : fallback[i];
                result[i] = float.IsNaN(value) || float.IsInfinity(value) ? fallback[i] : value;
            }

            return result;
        }

        private static float ClampNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0.0f ? 0.0f : value;
        }
    }
}
