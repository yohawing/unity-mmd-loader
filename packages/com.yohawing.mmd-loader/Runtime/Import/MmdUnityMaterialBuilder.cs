#nullable enable

using System;
using UnityEngine;
using UnityEngine.Rendering;
using Mmd.Rendering;

namespace Mmd.UnityIntegration
{
    internal static partial class MmdUnityMaterialBuilder
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

        internal static Material[] BuildMaterials(
            MmdRenderingDescriptor descriptor,
            MmdRuntimeTextureResolution textureResolution,
            out MmdShaderBindingDiagnostics shaderDiagnostics)
        {
            return BuildMaterials(
                descriptor,
                textureResolution,
                MmdMaterialMapperSet.BuiltIn,
                out shaderDiagnostics);
        }

        internal static Material[] BuildMaterials(
            MmdRenderingDescriptor descriptor,
            MmdRuntimeTextureResolution textureResolution,
            MmdMaterialMapperSet materialMappers,
            out MmdShaderBindingDiagnostics shaderDiagnostics)
        {
            if (materialMappers == null)
            {
                throw new ArgumentNullException(nameof(materialMappers));
            }

            // Keep the existing model-level scalar diagnostics contract while resolving the
            // actual shader independently for each material binding below.
            ResolveShader(ResolveRequestedShaderName(descriptor), out shaderDiagnostics);
            var materials = new Material[descriptor.materials.Count];
            var textureTargets = new MmdMaterialTextureTargets[descriptor.materials.Count];
            try
            {
                for (int i = 0; i < descriptor.materials.Count; i++)
                {
                    MmdMaterialDescriptor source = descriptor.materials[i];
                    Shader shader = ResolveShader(
                        ResolveRequestedShaderName(descriptor, source.materialIndex),
                        out _);
                    MmdMaterialMapperRegistration mapper = materialMappers.Resolve(source.materialIndex);
                    Material material = mapper.Mapper(source, shader);
                    if (material == null)
                    {
                        throw new InvalidOperationException(
                            $"Material mapper returned null for MMD material index {source.materialIndex}.");
                    }

                    textureTargets[i] = mapper.TextureTargets;
                    material.hideFlags = RuntimeGeneratedAssetHideFlags;
                    material.name = string.IsNullOrWhiteSpace(source.name)
                        ? $"MMD Material {source.materialIndex}"
                        : source.name;
                    materials[i] = material;
                    ApplyMaterialColors(material, source);
                    MmdMaterialTransparencyMode transparencyMode = ResolveMaterialTransparencyMode(descriptor, source, textureResolution);
                    ApplyMaterialRenderingPolicy(material, source.alpha, transparencyMode, source.cullingPolicy, i);
                    if (IsUrpLitShader(material.shader))
                    {
                        ApplyUrpLitDefaults(new[] { material });
                    }
                }

                BindDiffuseTextures(descriptor, materials, textureTargets, textureResolution);
                BindDiagnosticTextures(
                    descriptor,
                    textureResolution.SphereTextures,
                    materials,
                    textureTargets,
                    textureResolution.Diagnostics,
                    MmdMappedTextureKind.Sphere);
                BindDiagnosticTextures(
                    descriptor,
                    textureResolution.ToonTextures,
                    materials,
                    textureTargets,
                    textureResolution.Diagnostics,
                    MmdMappedTextureKind.Toon);

                return materials;
            }
            catch
            {
                foreach (Material material in materials)
                {
                    if (material != null)
                    {
                        if (Application.isPlaying)
                        {
                            UnityEngine.Object.Destroy(material);
                        }
                        else
                        {
                            UnityEngine.Object.DestroyImmediate(material);
                        }
                    }
                }

                throw;
            }
        }
    }
}
