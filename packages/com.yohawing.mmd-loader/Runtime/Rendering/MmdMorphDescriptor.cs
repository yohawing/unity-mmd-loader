#nullable enable

using System;
using System.Collections.Generic;

namespace Mmd.Rendering
{
    [Serializable]
    public sealed class MmdVertexMorphDescriptor
    {
        public int morphIndex;
        public string morphName = string.Empty;
        public List<MmdVertexMorphOffsetDescriptor> offsets = new();
    }

    [Serializable]
    public sealed class MmdVertexMorphOffsetDescriptor
    {
        public int vertexIndex;
        public float[] positionDelta = Array.Empty<float>();
    }

    [Serializable]
    public sealed class MmdGroupMorphDescriptor
    {
        public int morphIndex;
        public string morphName = string.Empty;
        public List<MmdGroupMorphOffsetDescriptor> offsets = new();
    }

    [Serializable]
    public sealed class MmdGroupMorphOffsetDescriptor
    {
        public int targetMorphIndex;
        public string targetMorphName = string.Empty;
        public float weight;
    }

    internal static partial class MmdMorphDescriptorBuilder
    {
        private static readonly HashSet<string> KnownMorphTypes = new(StringComparer.Ordinal)
        {
            "vertex",
            "group",
            "bone",
            "texture",
            "uva1",
            "uva2",
            "uva3",
            "uva4",
            "material",
            "flip",
            "impulse",
            "unknown"
        };

        internal static string NormalizeMorphType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            string trimmed = value.Trim();
            return KnownMorphTypes.Contains(trimmed) ? trimmed : "unknown";
        }
    }
}
