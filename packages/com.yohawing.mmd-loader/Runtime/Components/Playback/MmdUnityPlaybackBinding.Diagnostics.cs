#nullable enable

using System;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    [Serializable]
    public sealed class MmdLivePhysicsFrameDiagnostics
    {
        public int frame;
        public string backendName = string.Empty;
        public float deltaTime;
        public double totalMs;
        public double ensureBackendMs;
        public double evaluateFrameMs;
        public double applyAnimationFrameMs;
        public double syncBoneDrivenBodiesMs;
        public double stepPhysicsMs;
        public double applyPhysicsBodiesMs;
        public double refreshSnapshotFrameMs;
        public int unsupportedWorldAnchorJointCount;
        public string comparisonSpace = "runtime-forward-playback-diagnostics";
        public float importScale = 1.0f;
        public MmdLivePhysicsPinnedBodyDiagnostics pinnedBodies = new();
        public MmdLivePhysicsBodyDiagnostics[] bodyDiagnostics = System.Array.Empty<MmdLivePhysicsBodyDiagnostics>();
    }

    [Serializable]
    public sealed class MmdLivePhysicsBodyDiagnostics
    {
        public int bodyIndex;
        public string bodyName = string.Empty;
        public int boneIndex = -1;
        public string boneName = string.Empty;
        public string physicsKind = string.Empty;
        public string shapeType = string.Empty;
        public string nativeShapeType = string.Empty;
        public float mass;
        public Vector3 descriptorSize;
        public Vector3 descriptorPosition;
        public Vector3 descriptorRotation;
        public string debugColliderType = string.Empty;
        public Vector3 debugColliderSize;
        public Vector3 boneWorldPosition;
        public Vector3 boneModelPosition;
        public Vector3 readbackMmdPosition;
        public Quaternion readbackMmdRotation;
        public Vector3 readbackWorldPosition;
        public Quaternion readbackWorldRotation;
        public Vector3 debugColliderWorldPosition;
        public Quaternion debugColliderWorldRotation;
        public float debugToReadbackWorldDistance;
        public float boneToDebugWorldDistance;
        public float boneToReadbackWorldDistance;
    }

    [Serializable]
    public sealed class MmdLivePhysicsPinnedBodyDiagnostics
    {
        public int pinnedBodyCount;
        public int staticPinnedBodyCount;
        public int dynamicOrientationPinnedBodyCount;
        public int dynamicInitialPinnedBodyCount;
        public float maxPinnedBodySyncDistance;
        public float maxPinnedBodyRotationAngle;
        public float worstPinnedBodySyncDistance;
        public int worstPinnedBodyIndex = -1;
        public string worstPinnedBodyName = string.Empty;
        public int worstPinnedBodyBoneIndex = -1;
        public string worstPinnedBodyBoneName = string.Empty;
        public string worstPinnedBodyPhysicsKind = string.Empty;
        public float worstPinnedBodyRotationAngle;
        public int worstPinnedBodyRotationIndex = -1;
        public string worstPinnedBodyRotationName = string.Empty;
        public int worstPinnedBodyRotationBoneIndex = -1;
        public string worstPinnedBodyRotationBoneName = string.Empty;
        public string worstPinnedBodyRotationPhysicsKind = string.Empty;
    }
}
