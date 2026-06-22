#nullable enable

using System;
using System.Collections.Generic;

namespace Mmd.Tracing
{
    [Serializable]
    public sealed class MmdTrace
    {
        public int schemaVersion = 1;
        public string model = string.Empty;
        public string motion = string.Empty;
        public string space = "mmd";
        public List<MmdTraceFrame> frames = new();
    }

    [Serializable]
    public sealed class MmdTraceFrame
    {
        public int frame;
        public float time;
        public string checkpoint = string.Empty;
        public List<MmdTraceBone> bones = new();
        public List<MmdTraceMorph> morphs = new();
        public List<MmdTraceIk> ik = new();
    }

    [Serializable]
    public sealed class MmdTraceBone
    {
        public string name = string.Empty;
        public float[] localPosition = Array.Empty<float>();
        public float[] localRotation = Array.Empty<float>();
        public float[] localScale = Array.Empty<float>();
        public float[] worldMatrix = Array.Empty<float>();
    }

    [Serializable]
    public sealed class MmdTraceMorph
    {
        public string name = string.Empty;
        public float weight;
    }

    [Serializable]
    public sealed class MmdTraceIk
    {
        public string name = string.Empty;
        public bool enabled;
        public string target = string.Empty;
        public string effector = string.Empty;
        public List<string> chain = new();
    }
}
