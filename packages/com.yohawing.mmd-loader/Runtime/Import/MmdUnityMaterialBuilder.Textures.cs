#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Mmd.Rendering;

namespace Mmd.UnityIntegration
{
    internal static partial class MmdUnityMaterialBuilder
    {
        private static void BindDiffuseTextures(
            MmdRenderingDescriptor descriptor,
            Material[] materials,
            MmdMaterialTextureTargets[] textureTargets,
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
                MmdMaterialTextureTargets targets = textureTargets[materialSlot];
                bool bound = false;
                foreach (string propertyName in targets.DiffuseTextureProperties)
                {
                    if (material.HasProperty(propertyName))
                    {
                        material.SetTexture(propertyName, resolvedTexture.Texture);
                        bound = true;
                    }
                }

                if (!bound)
                {
                    textureResolution.Diagnostics.AddMessage(
                        $"Material {resolvedTexture.MaterialIndex} has no declared diffuse texture property supported by shader '{material.shader.name}'.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(targets.DiffuseTextureBoundProperty) &&
                    material.HasProperty(targets.DiffuseTextureBoundProperty))
                {
                    material.SetFloat(targets.DiffuseTextureBoundProperty, 1.0f);
                }
            }
        }

        private static void BindDiagnosticTextures(
            MmdRenderingDescriptor descriptor,
            IReadOnlyList<MmdResolvedTexture> resolvedTextures,
            Material[] materials,
            MmdMaterialTextureTargets[] textureTargets,
            MmdTextureBindingDiagnostics diagnostics,
            MmdMappedTextureKind kind)
        {
            string label = kind == MmdMappedTextureKind.Toon ? "toon" : "sphere";
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
                MmdMaterialTextureTargets targets = textureTargets[materialSlot];
                string propertyName = kind == MmdMappedTextureKind.Toon
                    ? targets.ToonTextureProperty
                    : targets.SphereTextureProperty;
                if (string.IsNullOrWhiteSpace(propertyName) || !material.HasProperty(propertyName))
                {
                    diagnostics.AddMessage(
                        $"Material {resolvedTexture.MaterialIndex} has no declared {label} texture property supported by shader '{material.shader.name}'.");
                    continue;
                }

                material.SetTexture(propertyName, resolvedTexture.Texture);
                if (kind == MmdMappedTextureKind.Toon)
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

                    if (!string.IsNullOrWhiteSpace(targets.ToonTextureBoundProperty) &&
                        material.HasProperty(targets.ToonTextureBoundProperty))
                    {
                        material.SetFloat(targets.ToonTextureBoundProperty, 1.0f);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(targets.SphereModeProperty) &&
                    material.HasProperty(targets.SphereModeProperty))
                {
                    // Only enable the sphere blend once a sphere texture is actually bound: the
                    // default _SphereMap is black, so a multiply mode without a texture would
                    // zero the material out. 1 = multiply (.sph), 2 = additive (.spa).
                    material.SetFloat(
                        targets.SphereModeProperty,
                        ResolveSphereMode(descriptor.materials[materialSlot].sphereTextureMode));
                }
            }
        }

        private enum MmdMappedTextureKind
        {
            Sphere,
            Toon
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
    }
}
