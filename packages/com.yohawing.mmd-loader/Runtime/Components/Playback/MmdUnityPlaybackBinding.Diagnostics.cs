#nullable enable

using System;
using System.Diagnostics;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    [Serializable]
    public sealed class MmdLivePhysicsFrameDiagnostics
    {
        public int frame;
        public string backendName = string.Empty;
        /// <summary>
        /// Identifies which host-pose contract produced this frame. A valid zero-cost phase is
        /// represented by a present flag and a zero duration; it must not be confused with an
        /// unavailable phase or a missing report.
        /// </summary>
        public string evaluationPath = "Unavailable";
        public bool phaseDiagnosticsPresent;
        public bool nativeStepReportPresent;
        public bool hostPoseCapturePresent;
        public bool pinnedDiagnosticsPresent;
        public bool pinMarshalPresent;
        public bool nativeHostFramePresent;
        public bool nativeRigidbodyCopyPresent;
        public bool managedRigidbodyFanOutPresent;
        public bool managedBodyTransformApplyPresent;
        public bool afterPhysicsMatrixReadbackPresent;
        public bool matrixTransformApplyPresent;
        public bool sampledDiagnosticsPresent;
        public bool sampledBodyDiagnosticsThisFrame;
        public bool evaluatedFrameRefreshPresent;
        public bool diagnosticsConstructionPresent;
        public bool ensureBackendPresent;
        public bool evaluateFramePresent;
        public bool applyAnimationFramePresent;
        public bool snapshotBuildPresent;
        public float deltaTime;
        public double totalMs;
        public double bridgeTotalMs;
        public double hostPoseCaptureMs;
        public double pinnedDiagnosticsMs;
        public double pinMarshalMs;
        public double nativeHostFrameMs;
        public double nativeRigidbodyCopyMs;
        public double managedRigidbodyFanOutMs;
        public double managedBodyTransformApplyMs;
        public double afterPhysicsMatrixReadbackMs;
        public double matrixTransformApplyMs;
        public double sampledDiagnosticsMs;
        public double evaluatedFrameRefreshMs;
        public double diagnosticsConstructionMs;
        public double snapshotBuildMs;
        public double ensureBackendMs;
        public double evaluateFrameMs;
        public double applyAnimationFrameMs;
        public double syncBoneDrivenBodiesMs;
        public double stepPhysicsMs;
        public double applyPhysicsBodiesMs;
        public double refreshSnapshotFrameMs;
        public int readbackTransformCount;
        public int readbackShapeTypeCount;
        public int nativeRigidbodyCount;
        public int nativeBoneCount;
        public int nativeSubstepCount;
        public int nativeKinematicRigidbodiesFed;
        public int nativeBonesWrittenBack;
        public int bodyDiagnosticsFrame = -1;
        public int unsupportedWorldAnchorJointCount;
        public string comparisonSpace = "runtime-forward-playback-diagnostics";
        public float importScale = 1.0f;
        public int modelBoneCount;
        public int appliedBoneCount;
        public int modelMorphCount;
        public int appliedMorphCount;
        public MmdLivePhysicsPinnedBodyDiagnostics pinnedBodies = new();
        public MmdLivePhysicsBodyDiagnostics[] bodyDiagnostics = System.Array.Empty<MmdLivePhysicsBodyDiagnostics>();
    }

    internal static class MmdLivePhysicsDiagnosticsClock
    {
        internal static double Milliseconds(long startTimestamp, long endTimestamp)
        {
            long elapsed = endTimestamp - startTimestamp;
            if (elapsed <= 0)
            {
                return 0.0;
            }

            return elapsed * 1000.0 / Stopwatch.Frequency;
        }
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
