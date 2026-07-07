#nullable enable

using System;

namespace Mmd.Mme
{
    [Serializable]
    public sealed class MmeFxEffectDescriptor
    {
        public string sourcePath = string.Empty;
        public string effectType = string.Empty;

        public string? normalMapTexture;
        public string? thresholdTexture;
        public string? albedoMapTexture;
        public string? normalMapFile;
        public string? smoothnessMapFile;
        public string? metalnessMapFile;

        public float normalMapResolution = 1.0f;
        public float? smoothness;
        public float? metalness;

        public float? softShadowParam;
        public float? selfShadowPower;

        public bool useNormalMap;
        public bool useMaterialTexture;
        public bool useMaterialSpecular;
        public bool useMaterialSphere;
        public bool useSelfShadow;
        public bool useSoftShadow;
        public int maxAnisotropy;
    }
}
