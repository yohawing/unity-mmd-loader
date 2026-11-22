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
        public bool playbackEvaluateBeforePhysicsPresent;
        public bool playbackCopyEvaluatedOutputsPresent;
        public bool physicsWorldStepRuntimePresent;
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
        public double playbackEvaluateBeforePhysicsMs;
        public double playbackCopyEvaluatedOutputsMs;
        public double physicsWorldStepRuntimeMs;
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
        /// <summary>
        /// Seconds the native physics tick received for this frame, before clamping. Zero on a
        /// seed frame. Valid when <see cref="nativeStepReportPresent"/> is true.
        /// </summary>
        public float nativeInputDeltaTime;
        /// <summary>
        /// Seconds the native physics tick added to its fixed-step accumulator for this frame:
        /// the input clamped to the tick's maximum step. Never larger than
        /// <see cref="nativeInputDeltaTime"/>. This is not the simulated time: the substeps run
        /// from the accumulator, so a small input can run no substep and a carried remainder can
        /// make a frame simulate more than its input (see <see cref="nativeSubstepCount"/>).
        /// </summary>
        public float nativeClampedDeltaTime;
        /// <summary>
        /// Seconds left in the native fixed-step accumulator after this frame's substeps, carried
        /// into the next frame. The value is reported as is. It can be slightly negative, down to
        /// about -1.2e-7 (single-precision epsilon), because the native tick runs a substep once
        /// the remainder is within that epsilon of a full step and then subtracts the full step.
        /// </summary>
        public float nativeAccumulatorTime;
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

        internal MmdLivePhysicsFrameDiagnostics() { }
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
    internal sealed class MmdUnityFrameApplyTimingSummary
    {
        public double totalMs;
        public double bonePoseApplyMs;
        public MmdUnityMorphApplyTimingSummary morph = new();
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

        internal MmdLivePhysicsBodyDiagnostics() { }
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

        internal MmdLivePhysicsPinnedBodyDiagnostics() { }
    }
}
