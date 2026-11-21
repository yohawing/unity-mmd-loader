#nullable enable

using System;
using System.Collections.Generic;

namespace Mmd.Rendering
{
    [Serializable]
    internal sealed class MmdBoneMorphDescriptor
    {
        public int morphIndex;
        public string morphName = string.Empty;
        public int boneOffsetCount;
        public List<MmdBoneMorphOffsetDescriptor> offsets = new();
    }

    [Serializable]
    internal sealed class MmdBoneMorphOffsetDescriptor
    {
        public int boneIndex;
        public string targetBoneName = string.Empty;
        public bool targetBoneExists;
        public float[] translation = Array.Empty<float>();
        public float[] orientation = Array.Empty<float>();
        public bool allPayloadFinite;
    }
}
