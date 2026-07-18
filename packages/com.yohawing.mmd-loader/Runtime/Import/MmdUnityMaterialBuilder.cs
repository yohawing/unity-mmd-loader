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
            // Keep the existing model-level scalar diagnostics contract while resolving the
            // actual shader independently for each material binding below.
            ResolveShader(ResolveRequestedShaderName(descriptor), out shaderDiagnostics);
            var materials = new Material[descriptor.materials.Count];
            try
            {
                for (int i = 0; i < descriptor.materials.Count; i++)
                {
                    MmdMaterialDescriptor source = descriptor.materials[i];
                    Shader shader = ResolveShader(
                        ResolveRequestedShaderName(descriptor, source.materialIndex),
                        out _);
                    var material = new Material(shader)
                    {
                        hideFlags = RuntimeGeneratedAssetHideFlags,
                        name = string.IsNullOrWhiteSpace(source.name)
                            ? $"MMD Material {source.materialIndex}"
                            : source.name
                    };
                    materials[i] = material;
                    ApplyMaterialColors(material, source);
                    MmdMaterialTransparencyMode transparencyMode = ResolveMaterialTransparencyMode(descriptor, source, textureResolution);
                    ApplyMaterialRenderingPolicy(material, source.alpha, transparencyMode, source.cullingPolicy, i);
                    if (IsUrpLitShader(shader))
                    {
                        ApplyUrpLitDefaults(new[] { material });
                    }
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
