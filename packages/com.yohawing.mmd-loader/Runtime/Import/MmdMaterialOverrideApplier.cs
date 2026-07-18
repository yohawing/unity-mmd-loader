#nullable enable

using System;
using Mmd.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mmd.UnityIntegration
{
    internal static class MmdMaterialOverrideApplier
    {
        private const string UrpNormalMapKeyword = "_NORMALMAP";
        private const string UrpMetallicGlossMapKeyword = "_METALLICSPECGLOSSMAP";
        private const string UrpOcclusionMapKeyword = "_OCCLUSIONMAP";
        private const string SurfaceTypeTransparentKeyword = "_SURFACE_TYPE_TRANSPARENT";
        private const string AlphaBlendKeyword = "_ALPHABLEND_ON";
        private const string AlphaTestKeyword = "_ALPHATEST_ON";
        private const int OpaqueRenderQueue = (int)RenderQueue.Geometry;
        private const int TransparentRenderQueueBase = (int)RenderQueue.Transparent;

        internal static void Apply(
            MmdMaterialOverrideAsset? overrideAsset,
            Material[]? materials,
            bool[]? excludedMaterialSlots = null)
        {
            if (overrideAsset == null ||
                overrideAsset.entries == null ||
                overrideAsset.entries.Length == 0 ||
                materials == null ||
                materials.Length == 0)
            {
                return;
            }

            var explicitSurfaceMaterialSlots = new System.Collections.Generic.HashSet<int>();
            foreach (MmdMaterialOverrideEntry? entry in overrideAsset.entries)
            {
                if (entry == null || !entry.enabled)
                {
                    continue;
                }

                int materialSlot = ResolveMaterialSlot(entry, materials);
                if (materialSlot < 0)
                {
                    continue;
                }
                if (IsExcludedMaterialSlot(materialSlot, excludedMaterialSlots))
                {
                    continue;
                }

                bool hasExplicitSurfaceMode = entry.hasSurfaceMode &&
                    entry.surfaceMode != MmdMaterialOverrideSurfaceMode.Preserve;
                ApplyEntry(
                    entry,
                    materials[materialSlot],
                    materialSlot,
                    preserveSurfaceClassification: explicitSurfaceMaterialSlots.Contains(materialSlot) && !hasExplicitSurfaceMode);
                if (hasExplicitSurfaceMode)
                {
                    explicitSurfaceMaterialSlots.Add(materialSlot);
                }
            }
        }

        internal static void ApplyToRenderingDescriptor(
            MmdMaterialOverrideAsset? overrideAsset,
            MmdRenderingDescriptor? descriptor,
            bool[]? excludedMaterialSlots = null)
        {
            if (overrideAsset == null ||
                overrideAsset.entries == null ||
                overrideAsset.entries.Length == 0 ||
                descriptor == null ||
                descriptor.materials == null ||
                descriptor.materials.Count == 0)
            {
                return;
            }

            var explicitSurfaceBindingSlots = new System.Collections.Generic.HashSet<int>();
            foreach (MmdMaterialOverrideEntry? entry in overrideAsset.entries)
            {
                if (entry == null || !entry.enabled)
                {
                    continue;
                }

                int materialSlot = ResolveMaterialDescriptorSlot(entry, descriptor.materials);
                if (materialSlot < 0)
                {
                    continue;
                }
                if (IsExcludedMaterialSlot(descriptor.materials[materialSlot].materialIndex, excludedMaterialSlots))
                {
                    continue;
                }

                ApplyDescriptorEntry(entry, descriptor.materials[materialSlot]);

                int bindingSlot = ResolveUrpMaterialBindingSlot(entry, descriptor.urpMaterialBindings);
                if (bindingSlot >= 0)
                {
                    bool hasExplicitSurfaceMode = entry.hasSurfaceMode &&
                        entry.surfaceMode != MmdMaterialOverrideSurfaceMode.Preserve;
                    ApplyUrpMaterialBindingEntry(
                        entry,
                        descriptor.urpMaterialBindings[bindingSlot],
                        preserveSurfaceClassification: explicitSurfaceBindingSlots.Contains(bindingSlot) && !hasExplicitSurfaceMode);
                    if (hasExplicitSurfaceMode)
                    {
                        explicitSurfaceBindingSlots.Add(bindingSlot);
                    }
                }
            }
        }

        private static int ResolveMaterialSlot(MmdMaterialOverrideEntry entry, Material[] materials)
        {
            if (entry.matchMode != MmdMaterialOverrideMatchMode.IndexThenName)
            {
                return -1;
            }

            if (entry.materialIndex >= 0 && entry.materialIndex < materials.Length)
            {
                return materials[entry.materialIndex] != null ? entry.materialIndex : -1;
            }

            if (string.IsNullOrWhiteSpace(entry.materialName))
            {
                return -1;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material != null &&
                    string.Equals(material.name, entry.materialName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int ResolveMaterialDescriptorSlot(
            MmdMaterialOverrideEntry entry,
            System.Collections.Generic.IReadOnlyList<MmdMaterialDescriptor> materials)
        {
            if (entry.matchMode != MmdMaterialOverrideMatchMode.IndexThenName)
            {
                return -1;
            }

            if (entry.materialIndex >= 0)
            {
                for (int i = 0; i < materials.Count; i++)
                {
                    if (materials[i] != null && materials[i].materialIndex == entry.materialIndex)
                    {
                        return i;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(entry.materialName))
            {
                return -1;
            }

            for (int i = 0; i < materials.Count; i++)
            {
                MmdMaterialDescriptor material = materials[i];
                if (material != null &&
                    string.Equals(material.name, entry.materialName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int ResolveUrpMaterialBindingSlot(
            MmdMaterialOverrideEntry entry,
            System.Collections.Generic.IReadOnlyList<MmdUrpMaterialBindingDescriptor>? bindings)
        {
            if (bindings == null || entry.matchMode != MmdMaterialOverrideMatchMode.IndexThenName)
            {
                return -1;
            }

            if (entry.materialIndex >= 0)
            {
                for (int i = 0; i < bindings.Count; i++)
                {
                    if (bindings[i] != null && bindings[i].materialIndex == entry.materialIndex)
                    {
                        return i;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(entry.materialName))
            {
                return -1;
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                MmdUrpMaterialBindingDescriptor binding = bindings[i];
                if (binding != null &&
                    string.Equals(binding.name, entry.materialName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        // has* flag を追加したら ApplyDescriptorEntry / ApplyUrpMaterialBindingEntry と MmdMaterialOverrideApplierSyncTests の inventory を同時に更新する（3 sink 同期契約）。
        private static void ApplyEntry(
            MmdMaterialOverrideEntry entry,
            Material material,
            int materialSlot,
            bool preserveSurfaceClassification)
        {
            if (entry.hasBaseColor)
            {
                SetColorIfPresent(material, MmdMaterialPropertyNames.BaseColor, entry.baseColor);
            }

            if (entry.hasColor)
            {
                SetColorIfPresent(material, MmdMaterialPropertyNames.Color, entry.color);
            }

            if (entry.hasAlpha)
            {
                SetMaterialAlpha(material, entry.alpha);
            }

            if (entry.hasAmbientColor)
            {
                SetColorIfPresent(material, MmdMaterialPropertyNames.AmbientColor, entry.ambientColor);
            }

            if (entry.hasToonBoundary)
            {
                SetFloatIfPresent(material, MmdMaterialPropertyNames.ToonBoundary, NormalizeToonOptional(entry.toonBoundary));
            }

            if (entry.hasToonFeather)
            {
                SetFloatIfPresent(material, MmdMaterialPropertyNames.ToonFeather, NormalizeToonOptional(entry.toonFeather));
            }

            if (entry.hasToonBandCount)
            {
                SetFloatIfPresent(material, MmdMaterialPropertyNames.ToonBandCount, NormalizeToonBandCount(entry.toonBandCount));
            }

            if (entry.hasOutlineColor)
            {
                SetColorIfPresent(material, MmdMaterialPropertyNames.OutlineColor, entry.outlineColor);
            }

            if (entry.hasOutlineWidth)
            {
                SetFloatIfPresent(material, MmdMaterialPropertyNames.OutlineWidth, entry.outlineWidth);
                SetFloatIfPresent(material, MmdMaterialPropertyNames.OutlineVisible, entry.outlineWidth > 0.0f ? 1.0f : 0.0f);
            }

            if (entry.hasMetallic)
            {
                SetFloatIfPresent(material, MmdMaterialPropertyNames.Metallic, entry.metallic);
            }

            if (entry.hasSmoothness)
            {
                SetFloatIfPresent(material, MmdMaterialPropertyNames.Smoothness, entry.smoothness);
            }

            if (entry.hasNormalMap && entry.normalMap != null)
            {
                bool mmdNormalMapBound = SetTextureIfPresent(
                    material,
                    MmdMaterialPropertyNames.MmdNormalMap,
                    entry.normalMap);
                bool urpNormalMapBound = SetTextureIfPresent(
                    material,
                    MmdMaterialPropertyNames.BumpMap,
                    entry.normalMap);
                if (urpNormalMapBound)
                {
                    material.EnableKeyword(UrpNormalMapKeyword);
                }

                if (mmdNormalMapBound)
                {
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.MmdNormalMapBound, 1.0f);
                }
            }

            if (entry.hasOcclusionStrength)
            {
                SetFloatIfPresent(material, MmdMaterialPropertyNames.OcclusionStrength, entry.occlusionStrength);
            }

            if (entry.hasEmissionColor)
            {
                SetEmissionColorIfPresent(material, entry.emissionColor);
            }

            if (entry.hasNormalScale)
            {
                SetFloatIfPresent(material, MmdMaterialPropertyNames.BumpScale, entry.normalScale);
            }

            if (entry.hasMetallicMap && entry.metallicMap != null)
            {
                bool mmdMetallicMapBound = SetTextureIfPresent(
                    material,
                    MmdMaterialPropertyNames.MmdMetallicMap,
                    entry.metallicMap);
                if (entry.metallicMapIncludesSmoothness)
                {
                    bool urpMetallicMapBound = SetTextureIfPresent(
                        material,
                        MmdMaterialPropertyNames.MetallicGlossMap,
                        entry.metallicMap);
                    if (urpMetallicMapBound)
                    {
                        material.EnableKeyword(UrpMetallicGlossMapKeyword);
                    }
                }

                if (mmdMetallicMapBound)
                {
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.MmdMetallicMapBound, 1.0f);
                }
            }

            if (entry.hasRoughnessMap && entry.roughnessMap != null)
            {
                bool mmdRoughnessMapBound = SetTextureIfPresent(
                    material,
                    MmdMaterialPropertyNames.MmdRoughnessMap,
                    entry.roughnessMap);
                if (mmdRoughnessMapBound)
                {
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.MmdRoughnessMapBound, 1.0f);
                }
            }

            if (entry.hasOcclusionMap && entry.occlusionMap != null)
            {
                bool mmdOcclusionMapBound = SetTextureIfPresent(
                    material,
                    MmdMaterialPropertyNames.MmdOcclusionMap,
                    entry.occlusionMap);
                bool urpOcclusionMapBound = SetTextureIfPresent(
                    material,
                    MmdMaterialPropertyNames.OcclusionMap,
                    entry.occlusionMap);
                if (urpOcclusionMapBound)
                {
                    material.EnableKeyword(UrpOcclusionMapKeyword);
                }

                if (mmdOcclusionMapBound)
                {
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.MmdOcclusionMapBound, 1.0f);
                }
            }

            if (entry.hasSurfaceMode && entry.surfaceMode != MmdMaterialOverrideSurfaceMode.Preserve)
            {
                ApplySurfaceMode(material, entry.surfaceMode, materialSlot, entry.hasAlphaClipThreshold ? entry.alphaClipThreshold : 0.01f);
            }
            else if (entry.hasSurfaceMode && entry.surfaceMode == MmdMaterialOverrideSurfaceMode.Preserve)
            {
                if (entry.hasAlphaClipThreshold)
                {
                    ApplyAlphaClipThreshold(material, entry.alphaClipThreshold);
                }
            }
            else if (!preserveSurfaceClassification && TryResolveAlphaOverride(entry, out float alphaOverride))
            {
                if (alphaOverride < 1.0f)
                {
                    ApplySurfaceMode(
                        material,
                        MmdMaterialOverrideSurfaceMode.AlphaBlend,
                        materialSlot,
                        entry.hasAlphaClipThreshold ? entry.alphaClipThreshold : 0.01f);
                }
                else if (entry.hasAlphaClipThreshold)
                {
                    ApplySurfaceMode(
                        material,
                        MmdMaterialOverrideSurfaceMode.AlphaTest,
                        materialSlot,
                        entry.alphaClipThreshold);
                }
            }
            else if (!preserveSurfaceClassification && entry.hasAlphaClipThreshold)
            {
                ApplySurfaceMode(
                    material,
                    MmdMaterialOverrideSurfaceMode.AlphaTest,
                    materialSlot,
                    entry.alphaClipThreshold);
            }
            else if (entry.hasAlphaClipThreshold)
            {
                ApplyAlphaClipThreshold(material, entry.alphaClipThreshold);
            }
        }

        private static void ApplyDescriptorEntry(MmdMaterialOverrideEntry entry, MmdMaterialDescriptor material)
        {
            if (entry.hasBaseColor)
            {
                material.diffuseColor = new[] { Clamp01(entry.baseColor.r), Clamp01(entry.baseColor.g), Clamp01(entry.baseColor.b) };
                material.alpha = Clamp01(entry.baseColor.a);
            }

            if (entry.hasAlpha)
            {
                material.alpha = Clamp01(entry.alpha);
            }

            if (entry.hasAmbientColor)
            {
                material.ambientColor = new[] { Clamp01(entry.ambientColor.r), Clamp01(entry.ambientColor.g), Clamp01(entry.ambientColor.b) };
            }

            if (entry.hasToonBoundary)
            {
                material.toonBoundary = NormalizeToonOptional(entry.toonBoundary);
            }

            if (entry.hasToonFeather)
            {
                material.toonFeather = NormalizeToonOptional(entry.toonFeather);
            }

            if (entry.hasToonBandCount)
            {
                material.toonBandCount = NormalizeToonBandCount(entry.toonBandCount);
            }

            if (entry.hasOutlineColor)
            {
                material.edgeColor = new[]
                {
                    Clamp01(entry.outlineColor.r),
                    Clamp01(entry.outlineColor.g),
                    Clamp01(entry.outlineColor.b),
                    Clamp01(entry.outlineColor.a)
                };
            }

            if (entry.hasOutlineWidth)
            {
                material.edgeSize = Math.Max(0.0f, IsFinite(entry.outlineWidth) ? entry.outlineWidth : 0.0f);
                material.drawEdgeFlag = material.edgeSize > 0.0f;
            }
        }

        private static void ApplyUrpMaterialBindingEntry(
            MmdMaterialOverrideEntry entry,
            MmdUrpMaterialBindingDescriptor binding,
            bool preserveSurfaceClassification)
        {
            if (entry.hasBaseColor)
            {
                binding.diffuseColor = new[] { Clamp01(entry.baseColor.r), Clamp01(entry.baseColor.g), Clamp01(entry.baseColor.b) };
                binding.alpha = Clamp01(entry.baseColor.a);
            }

            if (entry.hasAlpha)
            {
                binding.alpha = Clamp01(entry.alpha);
            }

            if (entry.hasAmbientColor)
            {
                binding.ambientColor = new[] { Clamp01(entry.ambientColor.r), Clamp01(entry.ambientColor.g), Clamp01(entry.ambientColor.b) };
            }

            if (entry.hasToonBoundary)
            {
                binding.toonBoundary = NormalizeToonOptional(entry.toonBoundary);
            }

            if (entry.hasToonFeather)
            {
                binding.toonFeather = NormalizeToonOptional(entry.toonFeather);
            }

            if (entry.hasToonBandCount)
            {
                binding.toonBandCount = NormalizeToonBandCount(entry.toonBandCount);
            }

            if (entry.hasOutlineColor)
            {
                binding.edgeColor = new[]
                {
                    Clamp01(entry.outlineColor.r),
                    Clamp01(entry.outlineColor.g),
                    Clamp01(entry.outlineColor.b),
                    Clamp01(entry.outlineColor.a)
                };
            }

            if (entry.hasOutlineWidth)
            {
                binding.edgeSize = Math.Max(0.0f, IsFinite(entry.outlineWidth) ? entry.outlineWidth : 0.0f);
                binding.drawEdgeFlag = binding.edgeSize > 0.0f;
            }

            if (entry.hasSurfaceMode && entry.surfaceMode != MmdMaterialOverrideSurfaceMode.Preserve)
            {
                ApplyBindingSurfaceMode(binding, entry.surfaceMode);
            }
            else if (entry.hasSurfaceMode && entry.surfaceMode == MmdMaterialOverrideSurfaceMode.Preserve)
            {
                return;
            }
            else if (!preserveSurfaceClassification)
            {
                if (entry.hasAlphaClipThreshold)
                {
                    ApplyBindingSurfaceMode(binding, MmdMaterialOverrideSurfaceMode.AlphaTest);
                }
                else if (binding.alpha < 1.0f)
                {
                    ApplyBindingAlphaClassification(binding);
                }
            }
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static bool IsExcludedMaterialSlot(int materialSlot, bool[]? excludedMaterialSlots)
        {
            return excludedMaterialSlots != null &&
                materialSlot >= 0 &&
                materialSlot < excludedMaterialSlots.Length &&
                excludedMaterialSlots[materialSlot];
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static bool SetTextureIfPresent(Material material, string propertyName, Texture value)
        {
            if (!material.HasProperty(propertyName))
            {
                return false;
            }

            material.SetTexture(propertyName, value);
            return true;
        }

        private static void SetEmissionColorIfPresent(Material material, Color value)
        {
            if (material.HasProperty(MmdMaterialPropertyNames.EmissionColor))
            {
                material.SetColor(MmdMaterialPropertyNames.EmissionColor, value);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }

        private static void SetMaterialAlpha(Material material, float alpha)
        {
            float clamped = Clamp01(alpha);
            SetColorAlphaIfPresent(material, MmdMaterialPropertyNames.BaseColor, clamped);
            SetColorAlphaIfPresent(material, MmdMaterialPropertyNames.Color, clamped);
            SetFloatIfPresent(material, MmdMaterialPropertyNames.Alpha, clamped);
        }

        private static bool TryResolveAlphaOverride(MmdMaterialOverrideEntry entry, out float alpha)
        {
            if (entry.hasAlpha)
            {
                alpha = Clamp01(entry.alpha);
                return true;
            }

            if (entry.hasBaseColor)
            {
                alpha = Clamp01(entry.baseColor.a);
                return true;
            }

            alpha = 1.0f;
            return false;
        }

        private static void SetColorAlphaIfPresent(Material material, string propertyName, float alpha)
        {
            if (!material.HasProperty(propertyName))
            {
                return;
            }

            Color color = material.GetColor(propertyName);
            color.a = alpha;
            material.SetColor(propertyName, color);
        }

        private static void ApplyAlphaClipThreshold(Material material, float threshold)
        {
            float clamped = Clamp01(threshold);
            SetFloatIfPresent(material, MmdMaterialPropertyNames.AlphaClipThreshold, clamped);
            SetFloatIfPresent(material, MmdMaterialPropertyNames.ShadowAlphaClipThreshold, clamped);
            SetFloatIfPresent(material, MmdMaterialPropertyNames.Cutoff, clamped);
        }

        private static void ApplySurfaceMode(
            Material material,
            MmdMaterialOverrideSurfaceMode surfaceMode,
            int materialSlot,
            float alphaClipThreshold)
        {
            switch (surfaceMode)
            {
                case MmdMaterialOverrideSurfaceMode.Opaque:
                    SetOpaqueSurface(material);
                    SetAlphaPolicy(material, MmdMaterialOverrideSurfaceMode.Opaque, 0.0f);
                    material.renderQueue = OpaqueRenderQueue;
                    break;
                case MmdMaterialOverrideSurfaceMode.AlphaTest:
                    SetOpaqueSurface(material);
                    SetAlphaPolicy(material, MmdMaterialOverrideSurfaceMode.AlphaTest, alphaClipThreshold);
                    material.EnableKeyword(AlphaTestKeyword);
                    material.renderQueue = OpaqueRenderQueue;
                    break;
                case MmdMaterialOverrideSurfaceMode.AlphaBlend:
                    SetAlphaPolicy(material, MmdMaterialOverrideSurfaceMode.AlphaBlend, alphaClipThreshold);
                    SetFloatIfPresent(material, "_Surface", 1.0f);
                    SetFloatIfPresent(material, "_Blend", 0.0f);
                    SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
                    SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    SetFloatIfPresent(material, "_ZWrite", 1.0f);
                    material.DisableKeyword(AlphaTestKeyword);
                    material.EnableKeyword(SurfaceTypeTransparentKeyword);
                    material.EnableKeyword(AlphaBlendKeyword);
                    material.renderQueue = TransparentRenderQueueBase + Math.Max(0, materialSlot);
                    break;
            }
        }

        private static void SetOpaqueSurface(Material material)
        {
            SetFloatIfPresent(material, "_Surface", 0.0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.One);
            SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.Zero);
            SetFloatIfPresent(material, "_ZWrite", 1.0f);
            material.DisableKeyword(SurfaceTypeTransparentKeyword);
            material.DisableKeyword(AlphaBlendKeyword);
            material.DisableKeyword(AlphaTestKeyword);
        }

        private static void SetAlphaPolicy(
            Material material,
            MmdMaterialOverrideSurfaceMode surfaceMode,
            float alphaClipThreshold)
        {
            float clamped = Clamp01(alphaClipThreshold);
            switch (surfaceMode)
            {
                case MmdMaterialOverrideSurfaceMode.AlphaTest:
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.AlphaClipThreshold, clamped);
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.ShadowAlphaClipThreshold, clamped);
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.Cutoff, clamped);
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.TextureAlphaOutputWeight, 0.0f);
                    break;
                case MmdMaterialOverrideSurfaceMode.AlphaBlend:
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.AlphaClipThreshold, 0.0f);
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.ShadowAlphaClipThreshold, clamped);
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.Cutoff, 0.0f);
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.TextureAlphaOutputWeight, 1.0f);
                    break;
                default:
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.AlphaClipThreshold, 0.0f);
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.ShadowAlphaClipThreshold, 0.0f);
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.Cutoff, 0.0f);
                    SetFloatIfPresent(material, MmdMaterialPropertyNames.TextureAlphaOutputWeight, 0.0f);
                    break;
            }
        }

        private static void ApplyBindingSurfaceMode(
            MmdUrpMaterialBindingDescriptor binding,
            MmdMaterialOverrideSurfaceMode surfaceMode)
        {
            switch (surfaceMode)
            {
                case MmdMaterialOverrideSurfaceMode.AlphaBlend:
                    binding.isTransparent = true;
                    binding.transparencyMode = "alphaBlend";
                    binding.renderOrderBucket = "alphaBlend";
                    break;
                case MmdMaterialOverrideSurfaceMode.AlphaTest:
                    binding.isTransparent = false;
                    binding.transparencyMode = "alphaTest";
                    binding.renderOrderBucket = "opaque";
                    break;
                default:
                    binding.isTransparent = false;
                    binding.transparencyMode = "opaque";
                    binding.renderOrderBucket = "opaque";
                    break;
            }
        }

        private static void ApplyBindingAlphaClassification(MmdUrpMaterialBindingDescriptor binding)
        {
            if (binding.alpha < 1.0f)
            {
                binding.isTransparent = true;
                binding.transparencyMode = "alphaBlend";
                binding.renderOrderBucket = "alphaBlend";
            }
            else
            {
                binding.isTransparent = false;
                binding.transparencyMode = "opaque";
                binding.renderOrderBucket = "opaque";
            }
        }

        private static float Clamp01(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0.0f;
        }

        private static float NormalizeToonOptional(float value)
        {
            return IsFinite(value) ? Mathf.Clamp(value, -1.0f, 1.0f) : -1.0f;
        }

        private static float NormalizeToonBandCount(float value)
        {
            return IsFinite(value) ? Mathf.Clamp(value, -1.0f, 8.0f) : -1.0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
