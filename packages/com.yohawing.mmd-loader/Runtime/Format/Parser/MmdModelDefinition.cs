#nullable enable

using System;
using System.Collections.Generic;

namespace Mmd.Parser
{
    [Serializable]
    public sealed class MmdModelDefinition
    {
        public string name = string.Empty;
        public string englishName = string.Empty;
        public string comment = string.Empty;
        public string englishComment = string.Empty;
        public List<MmdVertexDefinition> vertices = new();
        public List<int> indices = new();
        public List<MmdBoneDefinition> bones = new();
        public List<MmdMorphDefinition> morphs = new();
        public List<MmdMaterialDefinition> materials = new();
        public List<MmdIkDefinition> ik = new();
        public MmdPhysicsDefinition physics = new();

        public bool HasDeformAfterPhysicsBones
        {
            get
            {
                if (bones == null)
                {
                    return false;
                }

                for (int i = 0; i < bones.Count; i++)
                {
                    if (bones[i]?.deformAfterPhysics == true)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    [Serializable]
    public sealed class MmdVertexDefinition
    {
        public int index;
        public float[] position = Array.Empty<float>();
        public float[] normal = Array.Empty<float>();
        public float[] uv = Array.Empty<float>();
        public string skinningMode = "unknown";
        public int[] boneIndices = Array.Empty<int>();
        public float[] boneWeights = Array.Empty<float>();
        public bool hasSdefParameters;
        public float[] sdefC = Array.Empty<float>();
        public float[] sdefR0 = Array.Empty<float>();
        public float[] sdefR1 = Array.Empty<float>();
    }

    [Serializable]
    public sealed class MmdBoneDefinition
    {
        public int index;
        public string name = string.Empty;
        public int parentIndex = -1;
        public int transformOrder;
        public float[] origin = Array.Empty<float>();
        public bool isMovable;
        public bool isRotatable;
        public int appendParentIndex = -1;
        public float appendRatio;
        public bool appendRotation;
        public bool appendTranslation;
        public bool appendLocal;
        public bool fixedAxis;
        public float[] fixedAxisVector = Array.Empty<float>();
        public bool localAxes;
        public float[] localXAxis = Array.Empty<float>();
        public float[] localZAxis = Array.Empty<float>();
        public bool externalParentTransform;
        public bool deformAfterPhysics;
    }

    [Serializable]
    public sealed class MmdIkDefinition
    {
        public int boneIndex;
        public int targetBoneIndex;
        public int iterationCount;
        public float angleLimit;
        public List<MmdIkLinkDefinition> links = new();
    }

    [Serializable]
    public sealed class MmdIkLinkDefinition
    {
        public int boneIndex;
        public bool hasLimit;
        public float[] minimumAngle = Array.Empty<float>();
        public float[] maximumAngle = Array.Empty<float>();
    }

    [Serializable]
    public sealed class MmdMorphDefinition
    {
        public int index;
        public string name = string.Empty;
        public string type = string.Empty;
        public string panel = string.Empty;
        public List<MmdVertexMorphOffsetDefinition> vertexOffsets = new();
        public List<MmdGroupMorphOffsetDefinition> groupOffsets = new();
        public List<MmdMaterialMorphOffsetDefinition> materialOffsets = new();
        public List<MmdUvMorphOffsetDefinition> uvOffsets = new();
        public List<MmdBoneMorphOffsetDefinition> boneOffsets = new();
        public List<MmdFlipMorphOffsetDefinition> flipOffsets = new();
        public List<MmdImpulseMorphOffsetDefinition> impulseOffsets = new();
    }

    [Serializable]
    public sealed class MmdImpulseMorphOffsetDefinition
    {
        public int rigidbodyIndex = -1;
        public string rigidbodyName = string.Empty;
        public float[] velocity = Array.Empty<float>();
        public float[] torque = Array.Empty<float>();
        public bool local;
    }

    [Serializable]
    public sealed class MmdFlipMorphOffsetDefinition
    {
        public int morphIndex;
        public float weight;
    }

    [Serializable]
    public sealed class MmdMaterialMorphOffsetDefinition
    {
        public int materialIndex = -1;
        public string operation = "unknown";
        public float[] diffuseColor = Array.Empty<float>();
        public float diffuseOpacity = 1.0f;
        public float[] ambientColor = Array.Empty<float>();
        public float[] specularColor = Array.Empty<float>();
        public float specularPower;
        public float[] edgeColor = Array.Empty<float>();
        public float edgeOpacity = 1.0f;
        public float edgeSize;
        public float[] diffuseTextureBlend = Array.Empty<float>();
        public float[] sphereTextureBlend = Array.Empty<float>();
        public float[] toonTextureBlend = Array.Empty<float>();
    }

    [Serializable]
    public sealed class MmdVertexMorphOffsetDefinition
    {
        public int vertexIndex;
        public float[] positionDelta = Array.Empty<float>();
    }

    [Serializable]
    public sealed class MmdGroupMorphOffsetDefinition
    {
        public int morphIndex;
        public float weight;
    }

    [Serializable]
    public sealed class MmdUvMorphOffsetDefinition
    {
        public int vertexIndex;
        public float[] positionDelta = Array.Empty<float>();
    }

    [Serializable]
    public sealed class MmdBoneMorphOffsetDefinition
    {
        public int boneIndex;
        public float[] translation = Array.Empty<float>();
        public float[] orientation = Array.Empty<float>();
    }

    [Serializable]
    public sealed class MmdMaterialDefinition
    {
        public int index;
        public string name = string.Empty;
        public string texture = string.Empty;
        public string sphereTexture = string.Empty;
        public string toonTexture = string.Empty;
        public float alpha = 1.0f;
        public float[] diffuseColor = new[] { 1.0f, 1.0f, 1.0f };
        public float[] ambientColor = new[] { 0.25f, 0.25f, 0.25f };
        public float[] edgeColor = new[] { 0.0f, 0.0f, 0.0f, 1.0f };
        public float edgeSize;
        public string sphereTextureMode = string.Empty;
        public bool toonShared;

        /// <summary>
        /// 0-based MMD shared toon index (toon01..toon10) when <see cref="toonShared"/> is true;
        /// -1 when the material uses a custom toon texture or no toon at all.
        /// </summary>
        public int sharedToonIndex = -1;
        public string cullingPolicy = "unknown";
        public bool drawEdgeFlag;
        public int vertexCount;
    }

    [Serializable]
    public sealed class MmdPhysicsDefinition
    {
        public List<MmdRigidbodyDefinition> rigidbodies = new();
        public List<MmdJointDefinition> joints = new();
    }

    [Serializable]
    public sealed class MmdRigidbodyDefinition
    {
        public int index;
        public string name = string.Empty;
        public int boneIndex = -1;
        public string boneName = string.Empty;
        public string shapeType = string.Empty;
        public float[] size = Array.Empty<float>();
        public float[] position = Array.Empty<float>();
        public float[] rotation = Array.Empty<float>();
        public float mass;
        public float linearDamping;
        public float angularDamping;
        public float friction;
        public float restitution;
        public int group;
        public int mask;
        public string physicsKind = string.Empty;
    }

    [Serializable]
    public sealed class MmdJointDefinition
    {
        public int index;
        public string name = string.Empty;
        public int rigidbodyAIndex = -1;
        public int rigidbodyBIndex = -1;
        public float[] position = Array.Empty<float>();
        public float[] rotation = Array.Empty<float>();
        public float[] linearLowerLimit = Array.Empty<float>();
        public float[] linearUpperLimit = Array.Empty<float>();
        public float[] angularLowerLimit = Array.Empty<float>();
        public float[] angularUpperLimit = Array.Empty<float>();
        public float[] linearSpring = Array.Empty<float>();
        public float[] angularSpring = Array.Empty<float>();
    }
}
