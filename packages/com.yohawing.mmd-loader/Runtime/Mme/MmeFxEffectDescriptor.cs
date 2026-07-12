#nullable enable

using System;
using System.Collections.Generic;

namespace Mmd.Mme
{
    [Serializable]
    public sealed class MmeFxFloatParameter
    {
        public string name = string.Empty;
        public float value;
    }

    [Serializable]
    public sealed class MmeFxEffectDescriptor
    {
        public string sourcePath = string.Empty;
        public string effectType = string.Empty;
        public int materialIndex = -1;

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

        public List<MmeFxFloatParameter> floatParameters = new();

        public bool useNormalMap;
        public bool useMaterialTexture;
        public bool useMaterialSpecular;
        public bool useMaterialSphere;
        public bool useSelfShadow;
        public bool useSoftShadow;
        public int maxAnisotropy;

        public bool TryGetFloatParameter(string name, out float value)
        {
            for (int i = 0; i < floatParameters.Count; i++)
            {
                MmeFxFloatParameter parameter = floatParameters[i];
                if (string.Equals(parameter.name, name, StringComparison.Ordinal))
                {
                    value = parameter.value;
                    return true;
                }
            }

            value = 0.0f;
            return false;
        }
    }
}
