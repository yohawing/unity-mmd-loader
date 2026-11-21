#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

namespace Mmd.Rendering
{
    [Serializable]
    [MovedFrom(true, sourceNamespace: "Mmd.Rendering", sourceAssembly: "Mmd.Runtime", sourceClassName: "MmdMorphDescriptorBuilder/MmdMaterialMorphDescriptor")]
    public sealed class MmdMaterialMorphDescriptor
    {
        public int morphIndex;
        public string morphName = string.Empty;
        public List<MmdMaterialMorphOffsetDescriptor> offsets = new();
    }

    [Serializable]
    [MovedFrom(true, sourceNamespace: "Mmd.Rendering", sourceAssembly: "Mmd.Runtime", sourceClassName: "MmdMorphDescriptorBuilder/MmdMaterialMorphOffsetDescriptor")]
    public sealed class MmdMaterialMorphOffsetDescriptor
    {
        public int materialIndex = -1;
        public string operation = "unknown";
        public float[] diffuseColor = Array.Empty<float>();
        public float diffuseOpacity = 1.0f;
        public float[] ambientColor = Array.Empty<float>();
        public float[] edgeColor = Array.Empty<float>();
        public float edgeOpacity = 1.0f;
        public float edgeSize;
        public bool allOffsetsFinite;
    }
}
