#nullable enable
#pragma warning disable CS0649

using System;

namespace Mmd.Parser
{
    public sealed partial class NativeMmdParser
    {
        internal sealed class VmdMotionSourceSnapshot
        {
            public string? TargetModelName = string.Empty;
            public uint MaxFrame;
            public uint CameraKeyframeCount;
            public uint LightKeyframeCount;
            public uint SelfShadowKeyframeCount;
            public VmdMotionSourceBoneFrame[] BoneFrames = Array.Empty<VmdMotionSourceBoneFrame>();
            public VmdMotionSourceMorphFrame[] MorphFrames = Array.Empty<VmdMotionSourceMorphFrame>();
            public VmdMotionSourcePropertyFrame[] PropertyFrames = Array.Empty<VmdMotionSourcePropertyFrame>();
            public VmdMotionSourceCameraFrame[] CameraFrames = Array.Empty<VmdMotionSourceCameraFrame>();
            public VmdMotionSourceLightFrame[] LightFrames = Array.Empty<VmdMotionSourceLightFrame>();
            public VmdMotionSourceSelfShadowFrame[] SelfShadowFrames = Array.Empty<VmdMotionSourceSelfShadowFrame>();
        }

        internal sealed class VmdMotionSourceCameraFrame
        {
            public uint Frame;
            public float Distance;
            public float[] Position = Array.Empty<float>();
            public float[] Rotation = Array.Empty<float>();
            public uint ViewAngle;
            public bool Perspective = true;
            public byte[] Interpolation = Array.Empty<byte>();
        }

        internal sealed class VmdMotionSourceLightFrame
        {
            public uint Frame;
            public float[] Color = Array.Empty<float>();
            public float[] Direction = Array.Empty<float>();
        }

        internal sealed class VmdMotionSourceSelfShadowFrame
        {
            public uint Frame;
            public byte Mode;
            public float Distance;
        }

        internal sealed class VmdMotionSourceBoneFrame
        {
            public string BoneName = string.Empty;
            public uint Frame;
            public float[] Translation = Array.Empty<float>();
            public float[] Rotation = Array.Empty<float>();
            public byte[] TranslationXInterpolation = Array.Empty<byte>();
            public byte[] TranslationYInterpolation = Array.Empty<byte>();
            public byte[] TranslationZInterpolation = Array.Empty<byte>();
            public byte[] RotationInterpolation = Array.Empty<byte>();
        }

        internal sealed class VmdMotionSourceMorphFrame
        {
            public string MorphName = string.Empty;
            public uint Frame;
            public float Weight;
        }

        internal sealed class VmdMotionSourcePropertyFrame
        {
            public uint Frame;
            public bool Visible = true;
            public VmdMotionSourceIkState[] IkStates = Array.Empty<VmdMotionSourceIkState>();
        }

        internal sealed class VmdMotionSourceIkState
        {
            public string BoneName = string.Empty;
            public bool Enabled = true;
        }
    }
}