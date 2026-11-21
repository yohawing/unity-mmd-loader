#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

namespace Mmd.Rendering
{
    [Serializable]
    [MovedFrom(true, sourceNamespace: "Mmd.Rendering", sourceAssembly: "Mmd.Runtime", sourceClassName: "MmdMorphDescriptorBuilder/MmdUvMorphDescriptor")]
    public sealed class MmdUvMorphDescriptor
    {
        public int morphIndex;
        public string morphName = string.Empty;
        public string morphType = string.Empty;
        public int uvOffsetCount;
        public List<MmdUvMorphOffsetDescriptor> offsets = new();
    }

    [Serializable]
    [MovedFrom(true, sourceNamespace: "Mmd.Rendering", sourceAssembly: "Mmd.Runtime", sourceClassName: "MmdMorphDescriptorBuilder/MmdUvMorphOffsetDescriptor")]
    public sealed class MmdUvMorphOffsetDescriptor
    {
        public int vertexIndex;
        public float[] positionDelta = Array.Empty<float>();
        public bool targetVertexExists;
        public bool allPayloadFinite;
    }
}
