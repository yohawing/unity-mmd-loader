#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Mmd.Rendering;

namespace Mmd.UnityIntegration
{
    internal static partial class MmdUnityMaterialBuilder
    {
        private static void ApplyMaterialRenderingPolicy(
            Material material,
            float alpha,
            MmdMaterialSurfaceMode transparencyMode,
            string cullingPolicy,
            int materialRenderOrder,
            MmdMaterialRenderingTargets targets)
        {
            if (!IsFinite(alpha) || alpha < 0.0f || alpha > 1.0f)
            {
                throw new ArgumentException("Material alpha must be finite and between 0 and 1.");
            }

            SetColorAlpha(material, targets.BaseColorProperty, alpha);
            SetColorAlpha(material, targets.ColorProperty, alpha);
            if (HasProperty(material, targets.AlphaProperty))
            {
                material.SetFloat(targets.AlphaProperty, alpha);
            }

            ApplyMaterialCullingPolicy(material, cullingPolicy, targets.CullProperty);
            MmdMaterialSurfaceState state = MmdMaterialSurfaceState.Create(
                transparencyMode,
                materialRenderOrder,
                AlphaClipThreshold);
            MmdMaterialSurfaceApplier.Apply(material, state, targets);
        }

        private static void ApplyMaterialCullingPolicy(
            Material material,
            string cullingPolicy,
            string cullProperty)
        {
            if (!HasProperty(material, cullProperty))
            {
                return;
            }

            if (string.Equals(cullingPolicy, "double-sided", StringComparison.Ordinal))
            {
                material.SetFloat(cullProperty, (float)CullMode.Off);
            }
            else if (string.Equals(cullingPolicy, "backface-culling", StringComparison.Ordinal))
            {
                material.SetFloat(cullProperty, (float)CullMode.Back);
            }
        }

        private static MmdMaterialSurfaceMode ResolveMaterialTransparencyMode(
            MmdRenderingDescriptor descriptor,
            MmdMaterialDescriptor source,
            MmdRuntimeTextureResolution textureResolution)
        {
            MmdResolvedTexture? diffuseTexture = FindResolvedDiffuseTexture(textureResolution, source.materialIndex);
            string? textureExtension = diffuseTexture != null
                ? System.IO.Path.GetExtension(diffuseTexture.ResolvedPath)
                : null;
            int[]? alphaValues = ExtractTextureAlphaValues(diffuseTexture?.Texture, descriptor, source.materialIndex);
            string effectiveMaterialName = source.name ?? string.Empty;
            if (diffuseTexture != null)
            {
                effectiveMaterialName += " " + System.IO.Path.GetFileNameWithoutExtension(diffuseTexture.Reference);
                effectiveMaterialName += " " + System.IO.Path.GetFileNameWithoutExtension(diffuseTexture.ResolvedPath);
            }

            string mode = MmdMaterialTransparencyPolicy.ResolveMaterialTransparencyMode(
                source.alpha,
                effectiveMaterialName,
                textureExtension,
                alphaValues);

            return MapTransparencyMode(mode);
        }

        private static int[]? ExtractTextureAlphaValues(
            Texture2D? texture,
            MmdRenderingDescriptor descriptor,
            int materialIndex)
        {
            if (texture == null)
            {
                return null;
            }

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (UnityException)
            {
                return null;
            }

            if (pixels.Length == 0)
            {
                return null;
            }

            List<int> usedAlphaValues = ExtractUsedTextureAlphaValues(texture, pixels, descriptor, materialIndex);
            if (usedAlphaValues.Count > 0)
            {
                return usedAlphaValues.ToArray();
            }

            var alphaValues = new int[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                alphaValues[i] = pixels[i].a;
            }

            return alphaValues;
        }

        private static List<int> ExtractUsedTextureAlphaValues(
            Texture2D texture,
            Color32[] pixels,
            MmdRenderingDescriptor descriptor,
            int materialIndex)
        {
            var alphaValues = new List<int>();
            if (descriptor.vertices == null || descriptor.indices == null || descriptor.submeshes == null)
            {
                return alphaValues;
            }

            MmdSubmeshDescriptor? submesh = null;
            for (int i = 0; i < descriptor.submeshes.Count; i++)
            {
                MmdSubmeshDescriptor candidate = descriptor.submeshes[i];
                if (candidate != null && candidate.materialIndex == materialIndex)
                {
                    submesh = candidate;
                    break;
                }
            }

            if (submesh == null)
            {
                return alphaValues;
            }

            int end = Math.Min(descriptor.indices.Count, submesh.indexStart + submesh.indexCount);
            for (int i = Math.Max(0, submesh.indexStart); i + 2 < end; i += 3)
            {
                if (!TryGetViewportUv(descriptor, descriptor.indices[i], out Vector2 a) ||
                    !TryGetViewportUv(descriptor, descriptor.indices[i + 1], out Vector2 b) ||
                    !TryGetViewportUv(descriptor, descriptor.indices[i + 2], out Vector2 c))
                {
                    continue;
                }

                AddTriangleTextureAlphaValues(texture, pixels, a, b, c, alphaValues);
            }

            return alphaValues;
        }

        private static bool TryGetViewportUv(MmdRenderingDescriptor descriptor, int vertexIndex, out Vector2 uv)
        {
            uv = default;
            if (vertexIndex < 0 || vertexIndex >= descriptor.vertices.Count)
            {
                return false;
            }

            float[] sourceUv = descriptor.vertices[vertexIndex].uv;
            if (sourceUv == null || sourceUv.Length < 2)
            {
                return false;
            }

            float[] viewportUv = MmdTextureOrientationDescriptorBuilder.ToViewportUv(sourceUv);
            uv = new Vector2(viewportUv[0], viewportUv[1]);
            return true;
        }

        private static void AddTriangleTextureAlphaValues(
            Texture2D texture,
            Color32[] pixels,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            List<int> alphaValues)
        {
            int scanWidth = Math.Min(texture.width, TextureAlphaGeometryScanResolution);
            int scanHeight = Math.Min(texture.height, TextureAlphaGeometryScanResolution);
            int minX = ClampPixel(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) * scanWidth), scanWidth);
            int maxX = ClampPixel(Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) * scanWidth), scanWidth);
            int minY = ClampPixel(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) * scanHeight), scanHeight);
            int maxY = ClampPixel(Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) * scanHeight), scanHeight);
            int before = alphaValues.Count;

            for (int y = minY; y <= maxY; y++)
            {
                float v = (y + 0.5f) / scanHeight;
                for (int x = minX; x <= maxX; x++)
                {
                    float u = (x + 0.5f) / scanWidth;
                    if (IsPointInTriangle(new Vector2(u, v), a, b, c))
                    {
                        int textureX = ClampPixel(Mathf.FloorToInt(u * texture.width), texture.width);
                        int textureY = ClampPixel(Mathf.FloorToInt(v * texture.height), texture.height);
                        alphaValues.Add(pixels[textureY * texture.width + textureX].a);
                    }
                }
            }

            if (alphaValues.Count == before)
            {
                AddNearestTextureAlpha(texture, pixels, a, alphaValues);
                AddNearestTextureAlpha(texture, pixels, b, alphaValues);
                AddNearestTextureAlpha(texture, pixels, c, alphaValues);
            }
        }

        private static bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNegative = d1 < 0.0f || d2 < 0.0f || d3 < 0.0f;
            bool hasPositive = d1 > 0.0f || d2 > 0.0f || d3 > 0.0f;
            return !(hasNegative && hasPositive);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private static void AddNearestTextureAlpha(Texture2D texture, Color32[] pixels, Vector2 uv, List<int> alphaValues)
        {
            int x = ClampPixel(Mathf.FloorToInt(uv.x * texture.width), texture.width);
            int y = ClampPixel(Mathf.FloorToInt(uv.y * texture.height), texture.height);
            alphaValues.Add(pixels[y * texture.width + x].a);
        }

        private static int ClampPixel(int value, int size)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value >= size)
            {
                return size - 1;
            }

            return value;
        }

        private static MmdMaterialSurfaceMode MapTransparencyMode(string mode)
        {
            return mode switch
            {
                MmdMaterialTransparencyPolicy.ModeAlphaBlend => MmdMaterialSurfaceMode.AlphaBlend,
                MmdMaterialTransparencyPolicy.ModeAlphaTest => MmdMaterialSurfaceMode.AlphaTest,
                _ => MmdMaterialSurfaceMode.Opaque
            };
        }

        internal static void ReapplyImportedMaterialTransparency(
            Material material,
            MmdRenderingDescriptor descriptor,
            MmdMaterialDescriptor source,
            int materialRenderOrder,
            string? diffuseReference,
            string? diffuseResolvedPath,
            Texture2D? diffuseTextureForAlpha,
            MmdMaterialRenderingTargets? renderingTargets = null)
        {
            if (material == null || descriptor == null || source == null) return;
            string? textureExtension = !string.IsNullOrEmpty(diffuseResolvedPath)
                ? System.IO.Path.GetExtension(diffuseResolvedPath) : null;
            int[]? alphaValues = ExtractTextureAlphaValues(diffuseTextureForAlpha, descriptor, source.materialIndex);
            string effectiveMaterialName = source.name ?? string.Empty;
            if (diffuseTextureForAlpha != null)
            {
                effectiveMaterialName += " " + System.IO.Path.GetFileNameWithoutExtension(diffuseReference ?? string.Empty);
                effectiveMaterialName += " " + System.IO.Path.GetFileNameWithoutExtension(diffuseResolvedPath ?? string.Empty);
            }
            string mode = MmdMaterialTransparencyPolicy.ResolveMaterialTransparencyMode(
                source.alpha, effectiveMaterialName, textureExtension, alphaValues);
            MmdMaterialRenderingTargets effectiveTargets =
                renderingTargets ?? MmdMaterialRenderingTargets.BuiltIn;
            ApplyMaterialRenderingPolicy(
                material,
                source.alpha,
                MapTransparencyMode(mode),
                source.cullingPolicy,
                materialRenderOrder,
                effectiveTargets);
            ApplyMapperShaderState(material, effectiveTargets);
        }
    }
}
