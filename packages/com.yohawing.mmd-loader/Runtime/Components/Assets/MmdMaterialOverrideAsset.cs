#nullable enable

using System;
using UnityEngine;

namespace Mmd
{
    public enum MmdMaterialOverrideMatchMode
    {
        IndexThenName = 0
    }

    public enum MmdMaterialOverrideSourceKind
    {
        User = 0,
        MmeFx = 1,
        TextureScan = 2
    }

    public enum MmdMaterialOverrideSurfaceMode
    {
        Preserve = 0,
        Opaque = 1,
        AlphaTest = 2,
        AlphaBlend = 3
    }

    [Serializable]
    public sealed class MmdMaterialOverrideEntry
    {
        public bool enabled = true;
        public int materialIndex = -1;
        public string materialName = string.Empty;
        public MmdMaterialOverrideMatchMode matchMode = MmdMaterialOverrideMatchMode.IndexThenName;
        public MmdMaterialOverrideSourceKind sourceKind = MmdMaterialOverrideSourceKind.User;
        public string sourcePath = string.Empty;
        public string effectType = string.Empty;

        public bool hasMetallic;
        public float metallic;

        public bool hasSmoothness;
        public float smoothness = 0.5f;

        public bool hasOcclusionStrength;
        public float occlusionStrength = 1.0f;

        public bool hasEmissionColor;
        public Color emissionColor = Color.black;

        public bool hasBaseColor;
        public Color baseColor = Color.white;

        public bool hasColor;
        public Color color = Color.white;

        public bool hasAlpha;
        public float alpha = 1.0f;

        public bool hasAmbientColor;
        public Color ambientColor = new(0.25f, 0.25f, 0.25f, 1.0f);

        public bool hasToonBoundary;
        public float toonBoundary = -1.0f;

        public bool hasToonFeather;
        public float toonFeather = -1.0f;
        public bool hasToonBandCount;
        public float toonBandCount = -1.0f;

        public bool hasStylizedSpecularColor;
        public Color stylizedSpecularColor = Color.white;

        public bool hasStylizedSpecularBoundary;
        public float stylizedSpecularBoundary = -1.0f;

        public bool hasStylizedSpecularFeather;
        public float stylizedSpecularFeather = -1.0f;

        public bool hasRimColor;
        public Color rimColor = Color.white;

        public bool hasRimBoundary;
        public float rimBoundary = -1.0f;

        public bool hasRimFeather;
        public float rimFeather = -1.0f;

        public bool hasRimLightFollow;
        public float rimLightFollow;

        public bool hasOutlineColor;
        public Color outlineColor = Color.black;

        public bool hasOutlineWidth;
        public float outlineWidth;

        public bool hasAlphaClipThreshold;
        public float alphaClipThreshold = 0.01f;

        public bool hasSurfaceMode;
        public MmdMaterialOverrideSurfaceMode surfaceMode = MmdMaterialOverrideSurfaceMode.Preserve;

        public bool hasNormalMap;
        public Texture2D? normalMap;

        public bool hasNormalScale;
        public float normalScale = 1.0f;

        public bool hasMetallicMap;
        public Texture2D? metallicMap;
        public bool metallicMapIncludesSmoothness;

        public bool hasRoughnessMap;
        public Texture2D? roughnessMap;

        public bool hasOcclusionMap;
        public Texture2D? occlusionMap;
    }

    [CreateAssetMenu(
        fileName = "MmdMaterialOverride",
        menuName = "MMD/Material Override")]
    public sealed class MmdMaterialOverrideAsset : ScriptableObject
    {
        public MmdMaterialOverrideEntry[] entries = Array.Empty<MmdMaterialOverrideEntry>();
    }
}
