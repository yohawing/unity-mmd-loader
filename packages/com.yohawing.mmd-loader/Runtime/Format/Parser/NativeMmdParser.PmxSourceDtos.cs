#nullable enable
#pragma warning disable CS0649

using System;

namespace Mmd.Parser
{
    public sealed partial class NativeMmdParser
    {
        [Serializable]
        internal sealed class PmxModelSourceSnapshot
        {
            public PmxModelSourceMetadata metadata = new();
            public PmxModelSourceGeometry geometry = new();
            public PmxModelSourceMaterial[] materials = Array.Empty<PmxModelSourceMaterial>();
            public PmxModelSourceSkeleton skeleton = new();
            public PmxModelSourceMorph[] morphs = Array.Empty<PmxModelSourceMorph>();
            public PmxModelSourceRigidBody[] rigidBodies = Array.Empty<PmxModelSourceRigidBody>();
            public PmxModelSourceJoint[] joints = Array.Empty<PmxModelSourceJoint>();
        }

        [Serializable]
        internal sealed class PmxModelSourceMetadata
        {
            public string name = string.Empty;
            public string englishName = string.Empty;
            public string comment = string.Empty;
            public string englishComment = string.Empty;
        }

        [Serializable]
        internal sealed class PmxModelSourceGeometry
        {
            public float[] positions = Array.Empty<float>();
            public float[] normals = Array.Empty<float>();
            public float[] uvs = Array.Empty<float>();
            public float[] edgeScale = Array.Empty<float>();
            public uint[] indices = Array.Empty<uint>();
            public string[] skinningModes = Array.Empty<string>();
            public uint[] skinIndices = Array.Empty<uint>();
            public float[] skinWeights = Array.Empty<float>();
            public bool[] hasSdefParameters = Array.Empty<bool>();
            public float[] sdefC = Array.Empty<float>();
            public float[] sdefR0 = Array.Empty<float>();
            public float[] sdefR1 = Array.Empty<float>();
        }

        [Serializable]
        internal sealed class PmxModelSourceSkeleton { public PmxModelSourceBone[] bones = Array.Empty<PmxModelSourceBone>(); }

        [Serializable]
        internal sealed class PmxModelSourceBone
        {
            public string name = string.Empty;
            public int parentIndex = -1;
            public int layer;
            public float[] position = Array.Empty<float>();
            public PmxModelSourceBoneFlags flags = new();
            public PmxModelSourceAppendTransform? appendTransform;
            public float[]? fixedAxis;
            public PmxModelSourceLocalAxis? localAxis;
            public int externalParentKey = -1;
            public PmxModelSourceIk? ik;
        }

        [Serializable]
        internal sealed class PmxModelSourceBoneFlags
        {
            public bool rotatable;
            public bool translatable;
            public bool appendLocal;
            public bool appendRotate;
            public bool appendTranslate;
            public bool externalParentTransform;
            public bool transformAfterPhysics;
        }

        [Serializable]
        internal sealed class PmxModelSourceAppendTransform { public int parentIndex = -1; public float weight; }
        [Serializable]
        internal sealed class PmxModelSourceLocalAxis { public float[] x = Array.Empty<float>(); public float[] z = Array.Empty<float>(); }
        [Serializable]
        internal sealed class PmxModelSourceIk { public int targetIndex; public int loopCount; public float limitAngle; public PmxModelSourceIkLink[] links = Array.Empty<PmxModelSourceIkLink>(); }
        [Serializable]
        internal sealed class PmxModelSourceIkLink { public int boneIndex; public PmxModelSourceIkLimit? limits; }
        [Serializable]
        internal sealed class PmxModelSourceIkLimit { public float[] lower = Array.Empty<float>(); public float[] upper = Array.Empty<float>(); }

        [Serializable]
        internal sealed class PmxModelSourceMorph
        {
            public string name = string.Empty;
            public string type = string.Empty;
            public string panel = string.Empty;
            public PmxModelSourceVertexMorphOffset[] vertexOffsets = Array.Empty<PmxModelSourceVertexMorphOffset>();
            public PmxModelSourceGroupMorphOffset[] groupOffsets = Array.Empty<PmxModelSourceGroupMorphOffset>();
            public PmxModelSourceBoneMorphOffset[] boneOffsets = Array.Empty<PmxModelSourceBoneMorphOffset>();
            public PmxModelSourceUvMorphOffset[] uvOffsets = Array.Empty<PmxModelSourceUvMorphOffset>();
            public PmxModelSourceAdditionalUvMorphOffset[] additionalUvOffsets = Array.Empty<PmxModelSourceAdditionalUvMorphOffset>();
            public PmxModelSourceMaterialMorphOffset[] materialOffsets = Array.Empty<PmxModelSourceMaterialMorphOffset>();
            public PmxModelSourceGroupMorphOffset[] flipOffsets = Array.Empty<PmxModelSourceGroupMorphOffset>();
            public PmxModelSourceImpulseMorphOffset[] impulseOffsets = Array.Empty<PmxModelSourceImpulseMorphOffset>();
        }

        [Serializable]
        internal sealed class PmxModelSourceVertexMorphOffset { public uint vertexIndex; public float[] position = Array.Empty<float>(); }
        [Serializable]
        internal sealed class PmxModelSourceGroupMorphOffset { public int morphIndex; public float weight; }
        [Serializable]
        internal sealed class PmxModelSourceBoneMorphOffset { public int boneIndex; public float[] translation = Array.Empty<float>(); public float[] rotation = Array.Empty<float>(); }
        [Serializable]
        internal class PmxModelSourceUvMorphOffset { public uint vertexIndex; public float[] uv = Array.Empty<float>(); }
        [Serializable]
        internal sealed class PmxModelSourceAdditionalUvMorphOffset : PmxModelSourceUvMorphOffset { public byte uvIndex; }
        [Serializable]
        internal sealed class PmxModelSourceMaterialMorphOffset
        {
            public int materialIndex = -1;
            public string operation = "unknown";
            public float[] diffuse = Array.Empty<float>();
            public float[] specular = Array.Empty<float>();
            public float specularPower;
            public float[] ambient = Array.Empty<float>();
            public float[] edgeColor = Array.Empty<float>();
            public float edgeSize;
            public float[] textureFactor = Array.Empty<float>();
            public float[] sphereTextureFactor = Array.Empty<float>();
            public float[] toonTextureFactor = Array.Empty<float>();
        }

        [Serializable]
        internal sealed class PmxModelSourceImpulseMorphOffset { public int rigidBodyIndex = -1; public bool local; public float[] velocity = Array.Empty<float>(); public float[] torque = Array.Empty<float>(); }

        [Serializable]
        internal sealed class PmxModelSourceMaterial
        {
            public string name = string.Empty;
            public string texturePath = string.Empty;
            public string sphereTexturePath = string.Empty;
            public string sphereMode = string.Empty;
            public string toonTexturePath = string.Empty;
            public int sharedToonIndex = -1;
            public float[] diffuse = Array.Empty<float>();
            public float[] ambient = Array.Empty<float>();
            public float[] edgeColor = Array.Empty<float>();
            public float edgeSize;
            public PmxModelSourceMaterialFlags flags = new();
            public int faceCount;
        }

        [Serializable]
        internal sealed class PmxModelSourceMaterialFlags { public bool doubleSided; public bool edge; }
        [Serializable]
        internal sealed class PmxModelSourceRigidBody
        {
            public string name = string.Empty;
            public int boneIndex = -1;
            public int group;
            public int mask;
            public string shape = string.Empty;
            public float[] size = Array.Empty<float>();
            public float[] position = Array.Empty<float>();
            public float[] rotation = Array.Empty<float>();
            public float mass;
            public float linearDamping;
            public float angularDamping;
            public float restitution;
            public float friction;
            public string mode = string.Empty;
        }

        [Serializable]
        internal sealed class PmxModelSourceJoint
        {
            public string name = string.Empty;
            public int rigidBodyIndexA = -1;
            public int rigidBodyIndexB = -1;
            public float[] position = Array.Empty<float>();
            public float[] rotation = Array.Empty<float>();
            public float[] translationLowerLimit = Array.Empty<float>();
            public float[] translationUpperLimit = Array.Empty<float>();
            public float[] rotationLowerLimit = Array.Empty<float>();
            public float[] rotationUpperLimit = Array.Empty<float>();
            public float[] springTranslationFactor = Array.Empty<float>();
            public float[] springRotationFactor = Array.Empty<float>();
        }
    }
}