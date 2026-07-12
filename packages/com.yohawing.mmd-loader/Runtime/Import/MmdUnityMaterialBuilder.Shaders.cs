#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Mmd.Rendering;

namespace Mmd.UnityIntegration
{
    internal static partial class MmdUnityMaterialBuilder
    {
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

        private static bool IsUrpLitShader(Shader shader)
        {
            return shader != null &&
                string.Equals(shader.name, UrpLitShaderName, StringComparison.Ordinal);
        }
    }
}