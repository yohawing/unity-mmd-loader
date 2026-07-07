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

        // Material-morph write-set overrides (_BaseColor, _Color, _Alpha, _AmbientColor,
        // _OutlineColor, _OutlineWidth) are deferred until Slice 6 frame-applier layering.
        // Alpha cutoff and surface mode overrides are deferred to a surface slice with render policy.
        public bool hasNormalMap;
        public Texture2D? normalMap;

        public bool hasNormalScale;
        public float normalScale = 1.0f;
    }

    [CreateAssetMenu(
        fileName = "MmdMaterialOverride",
        menuName = "MMD/Material Override")]
    public sealed class MmdMaterialOverrideAsset : ScriptableObject
    {
        public MmdMaterialOverrideEntry[] entries = Array.Empty<MmdMaterialOverrideEntry>();
    }
}
