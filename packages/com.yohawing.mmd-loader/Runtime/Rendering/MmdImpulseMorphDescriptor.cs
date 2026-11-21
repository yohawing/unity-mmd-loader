#nullable enable

using System;
using System.Collections.Generic;

namespace Mmd.Rendering
{
    [Serializable]
    internal sealed class MmdImpulseMorphDescriptor
    {
        public int morphIndex;
        public string morphName = string.Empty;
        public int impulseOffsetCount;
        public List<MmdImpulseMorphOffsetDescriptor> offsets = new();
    }

    [Serializable]
    internal sealed class MmdImpulseMorphOffsetDescriptor
    {
        public int rigidbodyIndex = -1;
        public string rigidbodyName = string.Empty;
        public bool targetRigidbodyExists;
        public float[] velocity = Array.Empty<float>();
        public float[] torque = Array.Empty<float>();
        public bool local;
        public bool allPayloadFinite;
    }
}
