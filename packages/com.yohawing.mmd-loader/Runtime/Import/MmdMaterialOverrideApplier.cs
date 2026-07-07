#nullable enable

using System;
using Mmd.Rendering;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    internal static class MmdMaterialOverrideApplier
    {
        private const string UrpNormalMapKeyword = "_NORMALMAP";

        internal static void Apply(
            MmdMaterialOverrideAsset? overrideAsset,
            Material[]? materials)
        {
            if (overrideAsset == null ||
                overrideAsset.entries == null ||
                overrideAsset.entries.Length == 0 ||
                materials == null ||
                materials.Length == 0)
            {
                return;
            }

            foreach (MmdMaterialOverrideEntry? entry in overrideAsset.entries)
            {
                if (entry == null || !entry.enabled)
                {
                    continue;
                }

                Material? material = ResolveMaterial(entry, materials);
                if (material == null)
                {
                    continue;
                }

                ApplyEntry(entry, material);
            }
        }

        private static Material? ResolveMaterial(MmdMaterialOverrideEntry entry, Material[] materials)
        {
            if (entry.matchMode != MmdMaterialOverrideMatchMode.IndexThenName)
            {
                return null;
            }

            if (entry.materialIndex >= 0 && entry.materialIndex < materials.Length)
            {
                return materials[entry.materialIndex];
            }

            if (string.IsNullOrWhiteSpace(entry.materialName))
            {
                return null;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material != null &&
                    string.Equals(material.name, entry.materialName, StringComparison.Ordinal))
                {
                    return material;
                }
            }

            return null;
        }

        private static void ApplyEntry(MmdMaterialOverrideEntry entry, Material material)
        {
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
    }
}
