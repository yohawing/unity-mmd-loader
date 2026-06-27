#nullable enable

using System;
using System.Collections.Generic;

namespace Mmd.Parser
{
    [Serializable]
    public sealed class MmdMotionDefinition
    {
        public string targetModelName = string.Empty;
        public int maxFrame;
        public List<MmdBoneKeyframeDefinition> boneKeyframes = new();
        public List<MmdMorphKeyframeDefinition> morphKeyframes = new();
        public List<MmdModelKeyframeDefinition> modelKeyframes = new();
        public List<MmdCameraKeyframeDefinition> cameraKeyframes = new();
        public int cameraKeyframeCount;
        public List<MmdLightKeyframeDefinition> lightKeyframes = new();
        public int lightKeyframeCount;
        public List<MmdSelfShadowKeyframeDefinition> selfShadowKeyframes = new();
        public int selfShadowKeyframeCount;
    }

    /// <summary>
    /// A VMD camera keyframe in MMD camera space. MMD cameras are look-at + distance rigs:
    /// the camera orbits a <see cref="position"/> target at <see cref="distance"/> along the
    /// orientation given by the Euler <see cref="rotation"/> (radians). Conversion to a Unity
    /// <c>Camera</c> transform/FOV is a later slice; this type only carries the raw VMD values.
    /// </summary>
    [Serializable]
    public sealed class MmdCameraKeyframeDefinition
    {
        public int frame;
        public float distance;
        public float[] position = Array.Empty<float>();
        public float[] rotation = Array.Empty<float>();
        public int viewAngle;
        public bool perspective = true;
        public byte[] interpolation = Array.Empty<byte>();
    }

    [Serializable]
    public sealed class MmdLightKeyframeDefinition
    {
        public int frame;
        public float[] color = Array.Empty<float>();
        public float[] direction = Array.Empty<float>();
    }

    /// <summary>
    /// A raw VMD self-shadow keyframe. Rendering application is intentionally deferred; this IR
    /// only preserves the parsed mode and distance for diagnostics and future shader policy work.
    /// </summary>
    [Serializable]
    public sealed class MmdSelfShadowKeyframeDefinition
    {
        public int frame;
        public byte mode;
        public float distance;
    }

    [Serializable]
    public sealed class MmdBoneKeyframeDefinition
    {
        public string boneName = string.Empty;
        public int frame;
        public float[] translation = Array.Empty<float>();
        public float[] rotation = Array.Empty<float>();
        public MmdBoneInterpolationDefinition interpolation = new();
        public bool physicsEnabled;
    }

    [Serializable]
    public sealed class MmdBoneInterpolationDefinition
    {
        public byte[] translationX = Array.Empty<byte>();
        public byte[] translationY = Array.Empty<byte>();
        public byte[] translationZ = Array.Empty<byte>();
        public byte[] rotation = Array.Empty<byte>();
    }

    [Serializable]
    public sealed class MmdMorphKeyframeDefinition
    {
        public string morphName = string.Empty;
        public int frame;
        public float weight;
    }

    [Serializable]
    public sealed class MmdModelKeyframeDefinition
    {
        public int frame;
        public bool visible = true;
        public List<MmdModelConstraintStateDefinition> constraintStates = new();
    }

    [Serializable]
    public sealed class MmdModelConstraintStateDefinition
    {
        public string boneName = string.Empty;
        public bool enabled = true;
    }
}
