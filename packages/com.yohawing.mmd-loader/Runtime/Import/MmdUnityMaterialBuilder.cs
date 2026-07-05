#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Mmd.Rendering;

namespace Mmd.UnityIntegration
{
    internal static class MmdUnityMaterialBuilder
    {
        internal const HideFlags RuntimeGeneratedAssetHideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        private const int OpaqueRenderQueue = (int)RenderQueue.Geometry;
        private const int TransparentRenderQueueBase = (int)RenderQueue.Transparent;
        private const float AlphaClipThreshold = 0.01f;
        private const int OutlineZTest = (int)CompareFunction.Less;
        // MMD's edge is a screen-space, constant-pixel silhouette (saba and babylon-mmd both expand
        // the hull by edgeSize pixels with a *w term that cancels the perspective divide). So the
        // outline shader runs in screen-space mode (_OutlineScreenSpaceWeight = 1) with the raw PMX
        // edgeSize as the pixel width — no object-space scale.
        private const float PmxOutlineScreenSpaceWeight = 1.0f;
        private const int TextureAlphaGeometryScanResolution = 512;

        private enum MmdMaterialTransparencyMode
        {
            Opaque,
            AlphaTest,
            AlphaBlend
        }

        internal static Material[] BuildMaterials(
            MmdRenderingDescriptor descriptor,
            MmdRuntimeTextureResolution textureResolution,
            out MmdShaderBindingDiagnostics shaderDiagnostics)
        {
            Shader shader = ResolveShader(ResolveRequestedShaderName(descriptor), out shaderDiagnostics);
            var materials = new Material[descriptor.materials.Count];
            for (int i = 0; i < descriptor.materials.Count; i++)
            {
                MmdMaterialDescriptor source = descriptor.materials[i];
                var material = new Material(shader)
                {
                    hideFlags = RuntimeGeneratedAssetHideFlags,
                    name = string.IsNullOrWhiteSpace(source.name)
                        ? $"MMD Material {source.materialIndex}"
                        : source.name
                };
                ApplyMaterialColors(material, source);
                MmdMaterialTransparencyMode transparencyMode = ResolveMaterialTransparencyMode(descriptor, source, textureResolution);
                ApplyMaterialRenderingPolicy(material, source.alpha, transparencyMode, source.cullingPolicy, i);
                materials[i] = material;
            }

            BindDiffuseTextures(descriptor, materials, textureResolution);
            BindDiagnosticTextures(
                descriptor,
                textureResolution.SphereTextures,
                materials,
                textureResolution.Diagnostics,
                "_SphereMap",
                "sphere");
            BindDiagnosticTextures(
                descriptor,
                textureResolution.ToonTextures,
                materials,
                textureResolution.Diagnostics,
                "_ToonMap",
                "toon");
            return materials;
        }

        private static void BindDiffuseTextures(
            MmdRenderingDescriptor descriptor,
            Material[] materials,
            MmdRuntimeTextureResolution textureResolution)
        {
            var materialSlotsByIndex = new Dictionary<int, int>(descriptor.materials.Count);
            for (int i = 0; i < descriptor.materials.Count; i++)
            {
                materialSlotsByIndex[descriptor.materials[i].materialIndex] = i;
            }

            foreach (MmdResolvedTexture resolvedTexture in textureResolution.DiffuseTextures)
            {
                if (!materialSlotsByIndex.TryGetValue(resolvedTexture.MaterialIndex, out int materialSlot))
                {
                    textureResolution.Diagnostics.AddMessage($"Texture for material {resolvedTexture.MaterialIndex} was loaded but no Unity material slot was found.");
                    continue;
                }

                Material material = materials[materialSlot];
                bool bound = false;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", resolvedTexture.Texture);
                    ApplyDiffuseBoundSideEffects(material);
                    bound = true;
                }

                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", resolvedTexture.Texture);
                    bound = true;
                }

                if (!bound)
                {
                    textureResolution.Diagnostics.AddMessage($"Material {resolvedTexture.MaterialIndex} has no supported diffuse texture property.");
                }
            }
        }

        private static void BindDiagnosticTextures(
            MmdRenderingDescriptor descriptor,
            IReadOnlyList<MmdResolvedTexture> resolvedTextures,
            Material[] materials,
            MmdTextureBindingDiagnostics diagnostics,
            string propertyName,
            string label)
        {
            var materialSlotsByIndex = new Dictionary<int, int>(descriptor.materials.Count);
            for (int i = 0; i < descriptor.materials.Count; i++)
            {
                materialSlotsByIndex[descriptor.materials[i].materialIndex] = i;
            }

            foreach (MmdResolvedTexture resolvedTexture in resolvedTextures)
            {
                if (!materialSlotsByIndex.TryGetValue(resolvedTexture.MaterialIndex, out int materialSlot))
                {
                    diagnostics.AddMessage($"{label} texture for material {resolvedTexture.MaterialIndex} was loaded but no Unity material slot was found.");
                    continue;
                }

                Material material = materials[materialSlot];
                if (!material.HasProperty(propertyName))
                {
                    diagnostics.AddMessage($"Material {resolvedTexture.MaterialIndex} has no supported {label} diagnostic texture property.");
                    continue;
                }

                material.SetTexture(propertyName, resolvedTexture.Texture);
                if (string.Equals(label, "toon", StringComparison.Ordinal))
                {
                    // MMD/GoldenOracle samples the toon ramp with bilinear filtering, producing a
                    // smooth shade gradient from the stepped ramp texture. Clamp the wrap so the
                    // dark/light bands do not bleed across the ndotl 0/1 edges, but keep bilinear
                    // (point sampling made the bands too crisp vs the reference render).
                    if (resolvedTexture.Texture != null)
                    {
                        resolvedTexture.Texture.filterMode = FilterMode.Bilinear;
                        resolvedTexture.Texture.wrapMode = TextureWrapMode.Clamp;
                    }

                    if (material.HasProperty("_ToonMapBound"))
                    {
                        material.SetFloat("_ToonMapBound", 1.0f);
                    }
                }
                else if (string.Equals(label, "sphere", StringComparison.Ordinal) &&
                    material.HasProperty("_SphereMode"))
                {
                    // Only enable the sphere blend once a sphere texture is actually bound: the
                    // default _SphereMap is black, so a multiply mode without a texture would
                    // zero the material out. 1 = multiply (.sph), 2 = additive (.spa).
                    material.SetFloat("_SphereMode", ResolveSphereMode(descriptor.materials[materialSlot].sphereTextureMode));
                }
            }
        }

        // Maps the descriptor's sphere-texture-mode hint onto the shader's _SphereMode enum:
        // 0 = none, 1 = multiply (.sph), 2 = additive (.spa). Sub-texture/unknown modes have no
        // dedicated shader path yet, so they fall through to no sphere blend.
        private static float ResolveSphereMode(string sphereTextureMode)
        {
            if (string.Equals(sphereTextureMode, "multiply-sphere", StringComparison.Ordinal))
            {
                return 1.0f;
            }

            if (string.Equals(sphereTextureMode, "additive-sphere", StringComparison.Ordinal))
            {
                return 2.0f;
            }

            return 0.0f;
        }

        /// <summary>
        /// Shared side-effect applicator for diffuse texture bind (runtime + importer parity).
        /// Sets _BaseMapBound when a _BaseMap texture is present. Matches the previous inline
        /// logic in BindDiffuseTextures; importer post-bind path calls this after SetTexture so
        /// that importer Material sub-assets receive equivalent parameters without embedding
        /// Texture2D sub-assets under .pmx.
        /// </summary>
        public static void ApplyDiffuseBoundSideEffects(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
            {
                if (material.HasProperty("_BaseMapBound"))
                {
                    material.SetFloat("_BaseMapBound", 1.0f);
                }
            }
        }

        private static string ResolveRequestedShaderName(MmdRenderingDescriptor descriptor)
        {
            foreach (MmdUrpMaterialBindingDescriptor binding in descriptor.urpMaterialBindings)
            {
                if (!string.IsNullOrWhiteSpace(binding.shaderName))
                {
                    return binding.shaderName;
                }
            }

            return MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName;
        }

        private static Shader ResolveShader(string requestedShaderName, out MmdShaderBindingDiagnostics diagnostics)
        {
            if (string.IsNullOrWhiteSpace(requestedShaderName))
            {
                throw new ArgumentException("Requested shader name is required.", nameof(requestedShaderName));
            }

            string[] candidates =
            {
                requestedShaderName,
                UrpLitShaderName,
                "Standard",
                "Diffuse",
                "Unlit/Color",
                "Hidden/InternalErrorShader"
            };

            var uniqueCandidates = new List<string>(candidates.Length);
            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate) || uniqueCandidates.Contains(candidate))
                {
                    continue;
                }

                uniqueCandidates.Add(candidate);
                Shader shader = Shader.Find(candidate);
                if (shader != null)
                {
                    diagnostics = new MmdShaderBindingDiagnostics
                    {
                        requestedShaderName = requestedShaderName,
                        resolvedShaderName = shader.name,
                        fallbackShaderName = string.Equals(candidate, requestedShaderName, StringComparison.Ordinal)
                            ? string.Empty
                            : candidate,
                        fallbackReason = string.Equals(candidate, requestedShaderName, StringComparison.Ordinal)
                            ? string.Empty
                            : "requested-shader-not-found",
                        shaderFallbackUsed = !string.Equals(candidate, requestedShaderName, StringComparison.Ordinal),
                        fallbackCandidates = uniqueCandidates.ToArray()
                    };
                    return shader;
                }
            }

            diagnostics = new MmdShaderBindingDiagnostics
            {
                requestedShaderName = requestedShaderName,
                fallbackReason = "no-shader-fallback-available",
                shaderFallbackUsed = true,
                fallbackCandidates = uniqueCandidates.ToArray()
            };
            throw new InvalidOperationException(
                "No Unity shader fallback was available for MMD material creation. requestedShader=" +
                requestedShaderName +
                "; candidates=" +
                string.Join(", ", uniqueCandidates));
        }

        internal static MmdShaderBindingDiagnostics BuildExistingShaderDiagnostics(SkinnedMeshRenderer renderer)
        {
            string resolvedShaderName = string.Empty;
            Material material = renderer.sharedMaterial;
            if (material != null && material.shader != null)
            {
                resolvedShaderName = material.shader.name;
            }

            return new MmdShaderBindingDiagnostics
            {
                resolvedShaderName = resolvedShaderName,
                fallbackCandidates = Array.Empty<string>()
            };
        }

        private static void ApplyMaterialRenderingPolicy(
            Material material,
            float alpha,
            MmdMaterialTransparencyMode transparencyMode,
            string cullingPolicy,
            int materialRenderOrder)
        {
            if (!IsFinite(alpha) || alpha < 0.0f || alpha > 1.0f)
            {
                throw new ArgumentException("Material alpha must be finite and between 0 and 1.");
            }

            SetColorAlpha(material, "_BaseColor", alpha);
            SetColorAlpha(material, "_Color", alpha);
            if (material.HasProperty("_Alpha"))
            {
                material.SetFloat("_Alpha", alpha);
            }

            if (material.HasProperty("_AlphaClipThreshold"))
            {
                material.SetFloat("_AlphaClipThreshold", transparencyMode == MmdMaterialTransparencyMode.AlphaTest
                    ? AlphaClipThreshold
                    : 0.0f);
            }

            if (material.HasProperty("_ShadowAlphaClipThreshold"))
            {
                material.SetFloat("_ShadowAlphaClipThreshold", transparencyMode == MmdMaterialTransparencyMode.Opaque
                    ? 0.0f
                    : AlphaClipThreshold);
            }

            if (material.HasProperty("_TextureAlphaOutputWeight"))
            {
                material.SetFloat("_TextureAlphaOutputWeight", transparencyMode == MmdMaterialTransparencyMode.AlphaBlend
                    ? 1.0f
                    : 0.0f);
            }

            ApplyMaterialCullingPolicy(material, cullingPolicy);

            if (transparencyMode == MmdMaterialTransparencyMode.Opaque ||
                transparencyMode == MmdMaterialTransparencyMode.AlphaTest)
            {
                if (material.HasProperty("_Surface"))
                {
                    material.SetFloat("_Surface", 0.0f);
                }

                if (material.HasProperty("_SrcBlend"))
                {
                    material.SetFloat("_SrcBlend", (float)BlendMode.One);
                }

                if (material.HasProperty("_DstBlend"))
                {
                    material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                }

                if (material.HasProperty("_ZWrite"))
                {
                    material.SetFloat("_ZWrite", 1.0f);
                }

                if (transparencyMode == MmdMaterialTransparencyMode.AlphaTest)
                {
                    material.EnableKeyword("_ALPHATEST_ON");
                }

                material.renderQueue = OpaqueRenderQueue;
                return;
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1.0f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0.0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 1.0f);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = TransparentRenderQueueBase + materialRenderOrder;
        }

        private static void ApplyMaterialCullingPolicy(Material material, string cullingPolicy)
        {
            if (!material.HasProperty("_Cull"))
            {
                ApplyOutlineCullingPolicy(material, cullingPolicy);
                return;
            }

            if (string.Equals(cullingPolicy, "double-sided", StringComparison.Ordinal))
            {
                material.SetFloat("_Cull", (float)CullMode.Off);
            }
            else if (string.Equals(cullingPolicy, "backface-culling", StringComparison.Ordinal))
            {
                material.SetFloat("_Cull", (float)CullMode.Back);
            }

            ApplyOutlineCullingPolicy(material, cullingPolicy);
        }

        private static void ApplyOutlineCullingPolicy(Material material, string cullingPolicy)
        {
            if (!material.HasProperty("_OutlineVisible") ||
                !string.Equals(cullingPolicy, "backface-culling", StringComparison.Ordinal))
            {
                return;
            }

            // The outline pass uses an inverted hull. If a PMX material is backface-culled,
            // the body pass writes no depth from the reverse side, so the hull can remain as a
            // black backface-only fill. Keep outline conservative for culled materials.
            material.SetFloat("_OutlineVisible", 0.0f);
        }

        private static MmdMaterialTransparencyMode ResolveMaterialTransparencyMode(
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

        private static MmdMaterialTransparencyMode MapTransparencyMode(string mode)
        {
            return mode switch
            {
                MmdMaterialTransparencyPolicy.ModeAlphaBlend => MmdMaterialTransparencyMode.AlphaBlend,
                MmdMaterialTransparencyPolicy.ModeAlphaTest => MmdMaterialTransparencyMode.AlphaTest,
                _ => MmdMaterialTransparencyMode.Opaque
            };
        }

        internal static void ReapplyImportedMaterialTransparency(
            Material material,
            MmdRenderingDescriptor descriptor,
            MmdMaterialDescriptor source,
            int materialRenderOrder,
            string? diffuseReference,
            string? diffuseResolvedPath,
            Texture2D? diffuseTextureForAlpha)
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
            ApplyMaterialRenderingPolicy(material, source.alpha, MapTransparencyMode(mode), source.cullingPolicy, materialRenderOrder);
        }

        private static MmdResolvedTexture? FindResolvedDiffuseTexture(
            MmdRuntimeTextureResolution textureResolution,
            int materialIndex)
        {
            foreach (MmdResolvedTexture resolvedTexture in textureResolution.DiffuseTextures)
            {
                if (resolvedTexture.MaterialIndex == materialIndex)
                {
                    return resolvedTexture;
                }
            }

            return null;
        }

        private static void ApplyMaterialColors(Material material, MmdMaterialDescriptor source)
        {
            Color diffuse = ToColor(source.diffuseColor, source.alpha, Color.white);
            Color ambient = ToColor(source.ambientColor, 1.0f, new Color(0.25f, 0.25f, 0.25f, 1.0f));
            float edgeAlpha = source.edgeColor != null && source.edgeColor.Length > 3 ? source.edgeColor[3] : 1.0f;
            Color edge = ToColor(source.edgeColor, edgeAlpha, Color.black);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", diffuse);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            if (material.HasProperty("_AmbientColor"))
            {
                material.SetColor("_AmbientColor", ambient);
            }

            if (material.HasProperty("_OutlineColor"))
            {
                material.SetColor("_OutlineColor", edge);
            }

            if (material.HasProperty("_OutlineWidth"))
            {
                material.SetFloat("_OutlineWidth", source.drawEdgeFlag ? source.edgeSize : 0.0f);
            }

            if (material.HasProperty("_OutlineVisible"))
            {
                material.SetFloat("_OutlineVisible", source.drawEdgeFlag ? 1.0f : 0.0f);
            }

            if (material.HasProperty("_OutlineScreenSpaceWeight"))
            {
                material.SetFloat("_OutlineScreenSpaceWeight", PmxOutlineScreenSpaceWeight);
            }

            if (material.HasProperty("_OutlineZTest"))
            {
                material.SetFloat("_OutlineZTest", OutlineZTest);
            }
        }

        private static Color ToColor(float[]? values, float alpha, Color fallback)
        {
            if (values == null)
            {
                return fallback;
            }

            float r = values.Length > 0 && IsFinite(values[0]) ? Mathf.Clamp01(values[0]) : fallback.r;
            float g = values.Length > 1 && IsFinite(values[1]) ? Mathf.Clamp01(values[1]) : fallback.g;
            float b = values.Length > 2 && IsFinite(values[2]) ? Mathf.Clamp01(values[2]) : fallback.b;
            float a = IsFinite(alpha) ? Mathf.Clamp01(alpha) : fallback.a;
            return new Color(r, g, b, a);
        }

        private static void SetColorAlpha(Material material, string propertyName, float alpha)
        {
            if (!material.HasProperty(propertyName))
            {
                return;
            }

            Color color = material.GetColor(propertyName);
            color.a = alpha;
            material.SetColor(propertyName, color);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
