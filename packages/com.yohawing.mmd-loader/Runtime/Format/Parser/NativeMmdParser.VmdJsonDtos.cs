#nullable enable
#pragma warning disable CS0649

using System;

namespace Mmd.Parser
{
    public sealed partial class NativeMmdParser
    {
        [Serializable]
        internal sealed class VmdParsedAnimationJson
        {
            public VmdParsedMetadataJson metadata = new();
            public VmdParsedBoneFrameJson[] boneFrames = Array.Empty<VmdParsedBoneFrameJson>();
            public VmdParsedMorphFrameJson[] morphFrames = Array.Empty<VmdParsedMorphFrameJson>();
            public VmdParsedCameraFrameJson[] cameraFrames = Array.Empty<VmdParsedCameraFrameJson>();
            public VmdParsedLightFrameJson[] lightFrames = Array.Empty<VmdParsedLightFrameJson>();
            public VmdParsedSelfShadowFrameJson[] selfShadowFrames = Array.Empty<VmdParsedSelfShadowFrameJson>();
            public VmdParsedPropertyFrameJson[] propertyFrames = Array.Empty<VmdParsedPropertyFrameJson>();
        }

        [Serializable]
        internal sealed class VmdParsedMetadataJson
        {
            public string modelName = string.Empty;
            public VmdParsedCountsJson counts = new();
            public uint maxFrame;
        }

        [Serializable]
        internal sealed class VmdParsedCountsJson
        {
            public int bones;
            public int morphs;
            public int cameras;
            public int lights;
            public int selfShadows;
            public int properties;
        }

        [Serializable]
        internal sealed class VmdParsedBoneFrameJson
        {
            public string boneName = string.Empty;
            public uint frame;
            public float[] translation = Array.Empty<float>();
            public float[] rotation = Array.Empty<float>();
            public int[] interpolation = Array.Empty<int>();
        }

        [Serializable]
        internal sealed class VmdParsedMorphFrameJson
        {
            public string morphName = string.Empty;
            public uint frame;
            public float weight;
        }

        [Serializable]
        internal sealed class VmdParsedPropertyFrameJson
        {
            public uint frame;
            public bool visible = true;
            public VmdParsedIkStateJson[] ikStates = Array.Empty<VmdParsedIkStateJson>();
        }

        [Serializable]
        internal sealed class VmdParsedIkStateJson
        {
            public string boneName = string.Empty;
            public bool enabled = true;
        }

        [Serializable]
        internal sealed class VmdParsedCameraFrameJson
        {
            public uint frame;
            public float distance;
            public float[] position = Array.Empty<float>();
            public float[] rotation = Array.Empty<float>();
            public int[] interpolation = Array.Empty<int>();
            public uint fov;
            public uint viewAngle;
            public bool perspective = true;

            public uint ViewAngle => fov != 0u || viewAngle == 0u ? fov : viewAngle;
        }

        [Serializable]
        internal sealed class VmdParsedLightFrameJson
        {
            public uint frame;
            public float[] color = Array.Empty<float>();
            public float[] direction = Array.Empty<float>();
        }

        [Serializable]
        internal sealed class VmdParsedSelfShadowFrameJson
        {
            public uint frame;
            public byte mode;
            public float distance;
        }
    }
}