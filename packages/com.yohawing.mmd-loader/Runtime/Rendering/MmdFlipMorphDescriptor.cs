#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

namespace Mmd.Rendering
{
    [Serializable]
    [MovedFrom(true, sourceNamespace: "Mmd.Rendering", sourceAssembly: "Mmd.Runtime", sourceClassName: "MmdMorphDescriptorBuilder/MmdFlipMorphDescriptor")]
    public sealed class MmdFlipMorphDescriptor
    {
        public int morphIndex;
        public string morphName = string.Empty;
        public int flipOffsetCount;
        public List<MmdFlipMorphOffsetDescriptor> offsets = new();
    }

    [Serializable]
    [MovedFrom(true, sourceNamespace: "Mmd.Rendering", sourceAssembly: "Mmd.Runtime", sourceClassName: "MmdMorphDescriptorBuilder/MmdFlipMorphOffsetDescriptor")]
    public sealed class MmdFlipMorphOffsetDescriptor
    {
        public int targetMorphIndex;
        public string targetMorphName = string.Empty;
        public string targetMorphType = string.Empty;
        public float weight;
        public bool finiteWeight;
    }
}
