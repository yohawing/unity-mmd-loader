#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mmd;
using Mmd.Native;
using Mmd.Motion;
using Mmd.Parser;
using Mmd.Physics;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackBinding
    {
        private MmdUnityModelInstance? livePhysicsMetadataInstance;
        private IMmdLivePhysicsBackend? livePhysicsMetadataBackend;
        private LivePhysicsBodyMetadata[]? livePhysicsBodyMetadata;
        private MmdPhysicsBodyTransform[]? livePhysicsReadbackTransforms;
        private float[]? livePhysicsAfterPhysicsWorldMatrices;
        private int livePhysicsBodyDiagnosticsSampleInterval;
        private bool nativeHumanoidHostPoseEnabled;

        internal bool NativeHumanoidHostPoseEnabled => nativeHumanoidHostPoseEnabled;

        /// <summary>
        /// Enables the Humanoid Live path in which the native runtime owns append/IK and the
        /// final world-matrix pose. The backend is established eagerly so callers can retain the
        /// managed append path as a fallback before mutating the pose.
        /// </summary>
        internal bool TryEnableNativeHumanoidHostPose()
        {
            if (physicsMode != MmdPhysicsMode.Live)
            {
                return false;
            }

            try
            {
                EnsureLivePhysicsBackend(resetOnCreate: false);
                nativeHumanoidHostPoseEnabled = true;
                return true;
            }
            catch (Exception exception) when (
                exception is MmdPhysicsBackendException ||
                exception is MmdRuntimeNativeUnavailableException ||
                exception is DllNotFoundException ||
                exception is EntryPointNotFoundException ||
                exception is BadImageFormatException ||
                exception is InvalidOperationException ||
                exception is ArgumentException)
            {
                nativeHumanoidHostPoseEnabled = false;
                return false;
            }
        }

        internal bool ReapplyNativeHumanoidHostPoseWorldMatrices()
        {
            if (!nativeHumanoidHostPoseEnabled || livePhysicsAfterPhysicsWorldMatrices == null)
            {
                return false;
            }

            MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(
                playbackInstance,
                livePhysicsAfterPhysicsWorldMatrices);
            return true;
        }

        /// <summary>
        /// Drives Timeline playback while Live physics is enabled. Only a forward-advancing frame steps
        /// the simulation. A non-advancing frame (a backward scrub/seek) does NOT run physics — physics
        /// cannot integrate backward, and stepping in place leaves the 揺れもの stuck or torn — so every
        /// bone (including the physics bones) is placed at its bone-driven animation pose and the live
        /// simulation is reset, so that resuming forward playback re-seeds cleanly from the scrubbed pose.
        /// Requires Live mode.
        /// </summary>
        public MmdPlaybackSnapshot ApplyLivePhysicsForwardFrame(int frame, float frameRate)
        {
            MmdPlaybackTime.ValidateFrame(frame);
            MmdPlaybackTime.ValidateFrameRate(frameRate);
            if (physicsMode != MmdPhysicsMode.Live)
            {
                throw new InvalidOperationException(
                    "ApplyLivePhysicsForwardFrame requires Live physics mode. Set the binding physics mode to Live first.");
            }

            EnsureBorrowedMutationActive();
            bool isBackwardScrub = lastForwardPlaybackFrame >= 0 && frame < lastForwardPlaybackFrame;
            if (isBackwardScrub)
            {
                // Scrub/seek backward: physics cannot integrate backward, so reset the live simulation
                // (reusing the Bullet world, saba-style: clean contact pairs + zero velocities). The
                // re-seed below (lastLiveFrame is now -1) then EASES the physics into the scrubbed pose
                // (saba SyncPhysics) so the 揺れもの show a settled, physics-plausible pose for that frame
                // instead of snapping to the straight bind pose. This is a settle (deltaTime 0), NOT a
                // forward integration, so it cannot explode the chain. The world is kept alive so a
                // continuous backward drag does not pay a full world rebuild every frame.
                SoftResetLivePhysicsSimulation();
            }

            lastForwardPlaybackFrame = frame;
            return ApplyLivePhysicsFrame(frame, frameRate, allowArbitraryStart: true);
        }

        internal void StepLivePhysicsFromCurrentPose(int sequenceFrame, float deltaTime, bool resetOnFirstStep)
        {
            MmdPlaybackTime.ValidateFrame(sequenceFrame);
            if (deltaTime < 0.0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be a non-negative finite value.");
            }

            if (physicsMode != MmdPhysicsMode.Live)
            {
                throw new InvalidOperationException(
                    "StepLivePhysicsFromCurrentPose requires Live physics mode. Set the binding physics mode to Live first.");
            }

            EnsureBorrowedMutationActive();
            if (resetOnFirstStep)
            {
                SoftResetLivePhysicsSimulation();
            }

            var totalWatch = Stopwatch.StartNew();
            var stageWatch = Stopwatch.StartNew();
            IMmdLivePhysicsBackend backend = EnsureLivePhysicsBackend();
            double ensureBackendMs = stageWatch.Elapsed.TotalMilliseconds;
            stageWatch.Restart();
            MmdLivePhysicsFrameDiagnostics diagnostics = StepLivePhysicsCore(
                backend,
                sequenceFrame,
                resetOnFirstStep,
                resetOnFirstStep ? 0.0f : deltaTime,
                totalWatch,
                ensureBackendMs,
                evaluateFrameMs: 0.0,
                applyAnimationFrameMs: 0.0,
                evaluatedFrame: null,
                out _);
            totalWatch.Stop();
            diagnostics.totalMs = totalWatch.Elapsed.TotalMilliseconds;
            lastLivePhysicsDiagnostics = diagnostics;
        }

        public void ResetLivePhysics()
        {
            nativeHumanoidHostPoseEnabled = false;
            ResetLivePhysicsState();
        }

        internal void ResetLivePhysicsForDriveSource()
        {
            ResetLivePhysicsState();
        }

        private void ResetLivePhysicsState()
        {
            if (!nativeHumanoidHostPoseEnabled)
            {
                livePhysicsBackend?.Dispose();
                livePhysicsBackend = null;
            }
            // A preflight-created Humanoid backend is intentionally retained without resetting;
            // the immediately following seed step performs the single required SoftReset.
            lastLiveFrame = -1;
            lastForwardPlaybackFrame = -1;
            lastLiveSnapshot = null;
            lastLivePhysicsDiagnostics = null;
            livePhysicsAfterPhysicsWorldMatrices = null;
            ClearLivePhysicsBodyDiagnostics();
        }

        /// <summary>
        /// Resets the live simulation while REUSING the Bullet world (saba MMDRigidBody::Reset): the native
        /// reset returns bodies to their descriptor transforms, zeroes velocities, and cleans contact pairs.
        /// Used on a backward scrub so a continuous drag does not rebuild the whole world every frame, and so
        /// the next forward re-seed (ease-in) starts from clean contact state. lastForwardPlaybackFrame is
        /// preserved (scrub-direction tracking is the caller's responsibility).
        /// </summary>
        private void SoftResetLivePhysicsSimulation()
        {
            livePhysicsBackend?.Reset();
            lastLiveFrame = -1;
            lastLiveSnapshot = null;
            lastLivePhysicsDiagnostics = null;
            ClearLivePhysicsBodyDiagnostics();
        }

        private MmdPlaybackSnapshot ApplyLivePhysicsFrame(int frame, float frameRate, bool allowArbitraryStart = false)
        {
            if (lastLiveFrame < 0 && frame != 0 && !allowArbitraryStart)
            {
                throw new InvalidOperationException("Physics Live playback must start from frame 0.");
            }

            if (frame < lastLiveFrame)
            {
                throw new InvalidOperationException("Physics Live does not support reverse frame evaluation. Reset live physics before restarting from frame 0.");
            }

            if (frame == lastLiveFrame && lastLiveSnapshot != null)
            {
                return lastLiveSnapshot;
            }

            var totalWatch = Stopwatch.StartNew();
            var stageWatch = Stopwatch.StartNew();
            IMmdLivePhysicsBackend backend = EnsureLivePhysicsBackend();
            double ensureBackendMs = stageWatch.Elapsed.TotalMilliseconds;
            stageWatch.Restart();
            float time = MmdPlaybackTime.ToTime(frame, frameRate);
            MmdEvaluatedFrame? evaluatedFrame = null;
            if (fastSession == null)
            {
                evaluatedFrame = session.EvaluateBeforePhysicsFrame(frame, time);
            }

            double evaluateFrameMs = stageWatch.Elapsed.TotalMilliseconds;
            stageWatch.Restart();
            if (fastSession != null)
            {
                ApplyFastFrame(frame, frameRate);
                evaluatedFrame = BuildFastLivePhysicsFrame(frame, time);
            }
            else
            {
                MmdUnityFrameApplier.ApplyFrame(playbackInstance, evaluatedFrame!);
            }

            double applyAnimationFrameMs = stageWatch.Elapsed.TotalMilliseconds;
            stageWatch.Restart();
            bool initializeDynamicBodies = lastLiveFrame < 0;
            float deltaTime = initializeDynamicBodies ? 0.0f : (frame - lastLiveFrame) / frameRate;
            MmdLivePhysicsFrameDiagnostics diagnostics = StepLivePhysicsCore(
                backend,
                frame,
                initializeDynamicBodies,
                deltaTime,
                totalWatch,
                ensureBackendMs,
                evaluateFrameMs,
                applyAnimationFrameMs,
                evaluatedFrame,
                out double refreshSnapshotFrameMs);
            lastLiveFrame = frame;
            long snapshotBuildStartTimestamp = Stopwatch.GetTimestamp();
            lastLiveSnapshot = BuildOrUpdateLiveSnapshot(evaluatedFrame!);
            long snapshotBuildEndTimestamp = Stopwatch.GetTimestamp();
            totalWatch.Stop();
            diagnostics.refreshSnapshotFrameMs = refreshSnapshotFrameMs;
            diagnostics.snapshotBuildMs = MmdLivePhysicsDiagnosticsClock.Milliseconds(
                snapshotBuildStartTimestamp,
                snapshotBuildEndTimestamp);
            diagnostics.snapshotBuildPresent = true;
            diagnostics.totalMs = totalWatch.Elapsed.TotalMilliseconds;
            lastLivePhysicsDiagnostics = diagnostics;
            return lastLiveSnapshot;
        }

        private MmdLivePhysicsFrameDiagnostics StepLivePhysicsCore(
            IMmdLivePhysicsBackend backend,
            int sequenceFrame,
            bool resetSeed,
            float deltaTime,
            Stopwatch totalWatch,
            double ensureBackendMs,
            double evaluateFrameMs,
            double applyAnimationFrameMs,
            MmdEvaluatedFrame? evaluatedFrame,
            out double refreshSnapshotFrameMs)
        {
            livePhysicsReadbackTransformCount = 0;
            livePhysicsReadbackShapeTypeCount = 0;
            long bridgeStartTimestamp = Stopwatch.GetTimestamp();
            var stageWatch = Stopwatch.StartNew();
            long hostPoseCaptureStartTimestamp = Stopwatch.GetTimestamp();
            CaptureHostPoseFromUnityTransforms(
                out float[] localPositionOffsets,
                out float[] localRotations,
                out float[] localScales,
                out float[] morphWeights,
                out byte[] ikEnabled);
            long hostPoseCaptureEndTimestamp = Stopwatch.GetTimestamp();
            long pinnedDiagnosticsStartTimestamp = Stopwatch.GetTimestamp();
            MmdLivePhysicsPinnedBodyDiagnostics pinnedBodyDiagnostics =
                BuildHostPosePinnedBodyDiagnostics(resetSeed);
            long pinnedDiagnosticsEndTimestamp = Stopwatch.GetTimestamp();
            double hostPoseCaptureMs = MmdLivePhysicsDiagnosticsClock.Milliseconds(
                hostPoseCaptureStartTimestamp,
                hostPoseCaptureEndTimestamp);
            double pinnedDiagnosticsMs = MmdLivePhysicsDiagnosticsClock.Milliseconds(
                pinnedDiagnosticsStartTimestamp,
                pinnedDiagnosticsEndTimestamp);
            double syncBoneDrivenBodiesMs = stageWatch.Elapsed.TotalMilliseconds;
            stageWatch.Restart();
            if (backend is not MmdAnimPhysicsBackend nativeBackend)
            {
                throw new MmdPhysicsBackendException(
                    "StepFromHostPose",
                    backend.Name,
                    "host-pose-unsupported",
                    "Live physics requires the mmd-anim host-pose backend.",
                    modelId,
                    motionId);
            }

            nativeBackend.StepFromHostPose(
                sequenceFrame,
                localPositionOffsets,
                localRotations,
                localScales,
                morphWeights,
                ikEnabled,
                resetSeed,
                resetSeed ? 0.0f : deltaTime,
                out MmdPhysicsHostStepDiagnostics nativeStepDiagnostics);
            double stepPhysicsMs = stageWatch.Elapsed.TotalMilliseconds;
            if (resetSeed)
            {
                deltaTime = 0.0f;
            }

            long managedRigidbodyFanOutStartTimestamp = Stopwatch.GetTimestamp();
            stageWatch.Restart();
            CaptureLivePhysicsReadback(backend);
            long managedRigidbodyFanOutEndTimestamp = Stopwatch.GetTimestamp();
            double managedRigidbodyFanOutMs = MmdLivePhysicsDiagnosticsClock.Milliseconds(
                managedRigidbodyFanOutStartTimestamp,
                managedRigidbodyFanOutEndTimestamp);

            long managedBodyTransformApplyStartTimestamp = Stopwatch.GetTimestamp();
            bool managedBodyTransformApplyPresent = !nativeHumanoidHostPoseEnabled;
            if (managedBodyTransformApplyPresent)
            {
                ApplyPhysicsBodyTransforms();
            }
            long managedBodyTransformApplyEndTimestamp = Stopwatch.GetTimestamp();
            double managedBodyTransformApplyMs = MmdLivePhysicsDiagnosticsClock.Milliseconds(
                managedBodyTransformApplyStartTimestamp,
                managedBodyTransformApplyEndTimestamp);
            double applyPhysicsBodiesMs = stageWatch.Elapsed.TotalMilliseconds;

            stageWatch.Restart();
            long afterPhysicsMatrixReadbackStartTimestamp = Stopwatch.GetTimestamp();
            bool afterPhysicsMatrixWorkPerformed = CaptureAfterPhysicsWorldMatrices(nativeBackend);
            long afterPhysicsMatrixReadbackEndTimestamp = Stopwatch.GetTimestamp();
            double afterPhysicsMatrixReadbackMs = MmdLivePhysicsDiagnosticsClock.Milliseconds(
                afterPhysicsMatrixReadbackStartTimestamp,
                afterPhysicsMatrixReadbackEndTimestamp);

            long matrixTransformApplyStartTimestamp = Stopwatch.GetTimestamp();
            if (afterPhysicsMatrixWorkPerformed)
            {
                ApplyAfterPhysicsWorldMatrices();
            }
            long matrixTransformApplyEndTimestamp = Stopwatch.GetTimestamp();
            double matrixTransformApplyMs = MmdLivePhysicsDiagnosticsClock.Milliseconds(
                matrixTransformApplyStartTimestamp,
                matrixTransformApplyEndTimestamp);
            if (nativeHumanoidHostPoseEnabled)
            {
                // Preserve the historical Humanoid aggregate: it covered the complete native
                // matrix readback/apply stage in addition to the shared rigidbody fan-out.
                applyPhysicsBodiesMs = MmdLivePhysicsDiagnosticsClock.Milliseconds(
                    managedRigidbodyFanOutStartTimestamp,
                    matrixTransformApplyEndTimestamp);
            }

            long sampledDiagnosticsStartTimestamp = Stopwatch.GetTimestamp();
            bool sampledBodyDiagnosticsThisFrame = ShouldSampleLivePhysicsBodyDiagnostics(sequenceFrame);
            if (sampledBodyDiagnosticsThisFrame)
            {
                ApplyPhysicsBodyDebugTransforms(backend);
                lastLivePhysicsBodyDiagnostics = BuildBodyDiagnostics(backend);
                lastLivePhysicsBodyDiagnosticsFrame = sequenceFrame;
            }
            long sampledDiagnosticsEndTimestamp = Stopwatch.GetTimestamp();
            double sampledDiagnosticsMs = MmdLivePhysicsDiagnosticsClock.Milliseconds(
                sampledDiagnosticsStartTimestamp,
                sampledDiagnosticsEndTimestamp);

            long evaluatedFrameRefreshStartTimestamp = Stopwatch.GetTimestamp();
            bool evaluatedFrameRefreshPresent = evaluatedFrame != null;
            if (evaluatedFrame != null)
            {
                RefreshEvaluatedFrameFromUnityTransforms(evaluatedFrame);
            }
            long evaluatedFrameRefreshEndTimestamp = Stopwatch.GetTimestamp();
            double evaluatedFrameRefreshMs = MmdLivePhysicsDiagnosticsClock.Milliseconds(
                evaluatedFrameRefreshStartTimestamp,
                evaluatedFrameRefreshEndTimestamp);

            refreshSnapshotFrameMs = stageWatch.Elapsed.TotalMilliseconds;
            long diagnosticsConstructionStartTimestamp = Stopwatch.GetTimestamp();
            MmdLivePhysicsFrameDiagnostics diagnostics = new MmdLivePhysicsFrameDiagnostics
            {
                frame = sequenceFrame,
                backendName = backend.Name,
                evaluationPath = nativeHumanoidHostPoseEnabled ? "HumanoidNativeFinal" : "VMDCompatibility",
                phaseDiagnosticsPresent = true,
                nativeStepReportPresent = nativeStepDiagnostics.reportPresent,
                hostPoseCapturePresent = true,
                pinnedDiagnosticsPresent = true,
                pinMarshalPresent = nativeStepDiagnostics.reportPresent,
                nativeHostFramePresent = nativeStepDiagnostics.reportPresent,
                nativeRigidbodyCopyPresent = nativeStepDiagnostics.reportPresent,
                managedRigidbodyFanOutPresent = true,
                managedBodyTransformApplyPresent = managedBodyTransformApplyPresent,
                // These production phases are measured even when this model has no
                // deform-after-physics bones. In that valid no-work case their duration is zero,
                // while presence remains true so it is distinct from unavailable instrumentation.
                afterPhysicsMatrixReadbackPresent = true,
                matrixTransformApplyPresent = true,
                sampledDiagnosticsPresent = true,
                sampledBodyDiagnosticsThisFrame = sampledBodyDiagnosticsThisFrame,
                evaluatedFrameRefreshPresent = evaluatedFrameRefreshPresent,
                diagnosticsConstructionPresent = true,
                ensureBackendPresent = true,
                evaluateFramePresent = !nativeHumanoidHostPoseEnabled,
                applyAnimationFramePresent = !nativeHumanoidHostPoseEnabled,
                snapshotBuildPresent = false,
                deltaTime = deltaTime,
                totalMs = totalWatch.Elapsed.TotalMilliseconds,
                hostPoseCaptureMs = hostPoseCaptureMs,
                pinnedDiagnosticsMs = pinnedDiagnosticsMs,
                pinMarshalMs = nativeStepDiagnostics.pinMarshalMs,
                nativeHostFrameMs = nativeStepDiagnostics.nativeHostFrameMs,
                nativeRigidbodyCopyMs = nativeStepDiagnostics.nativeRigidbodyCopyMs,
                managedRigidbodyFanOutMs = managedRigidbodyFanOutMs,
                managedBodyTransformApplyMs = managedBodyTransformApplyMs,
                afterPhysicsMatrixReadbackMs = afterPhysicsMatrixReadbackMs,
                matrixTransformApplyMs = matrixTransformApplyMs,
                sampledDiagnosticsMs = sampledDiagnosticsMs,
                evaluatedFrameRefreshMs = evaluatedFrameRefreshMs,
                ensureBackendMs = ensureBackendMs,
                evaluateFrameMs = evaluateFrameMs,
                applyAnimationFrameMs = applyAnimationFrameMs,
                syncBoneDrivenBodiesMs = syncBoneDrivenBodiesMs,
                stepPhysicsMs = stepPhysicsMs,
                applyPhysicsBodiesMs = applyPhysicsBodiesMs,
                refreshSnapshotFrameMs = refreshSnapshotFrameMs,
                readbackTransformCount = livePhysicsReadbackTransformCount,
                readbackShapeTypeCount = livePhysicsReadbackShapeTypeCount,
                nativeRigidbodyCount = nativeStepDiagnostics.nativeRigidbodyCount,
                nativeBoneCount = nativeStepDiagnostics.nativeBoneCount,
                nativeSubstepCount = nativeStepDiagnostics.nativeSubstepCount,
                nativeKinematicRigidbodiesFed = nativeStepDiagnostics.nativeKinematicRigidbodiesFed,
                nativeBonesWrittenBack = nativeStepDiagnostics.nativeBonesWrittenBack,
                pinnedBodies = pinnedBodyDiagnostics,
                unsupportedWorldAnchorJointCount = backend.SkippedWorldAnchorJointCount,
                comparisonSpace = "runtime-forward-playback-diagnostics",
                importScale = playbackInstance.ImportScale,
                modelBoneCount = model.bones.Count,
                appliedBoneCount = fastSession != null ? fastPoseBoneIndices.Length : evaluatedFrame?.bones.Count ?? model.bones.Count,
                modelMorphCount = model.morphs.Count,
                appliedMorphCount = fastSession != null ? fastMorphIndices.Length : evaluatedFrame?.morphs.Count ?? model.morphs.Count,
                bodyDiagnosticsFrame = lastLivePhysicsBodyDiagnosticsFrame,
                bodyDiagnostics = lastLivePhysicsBodyDiagnostics
            };
            long diagnosticsConstructionEndTimestamp = Stopwatch.GetTimestamp();
            diagnostics.diagnosticsConstructionMs = MmdLivePhysicsDiagnosticsClock.Milliseconds(
                diagnosticsConstructionStartTimestamp,
                diagnosticsConstructionEndTimestamp);
            diagnostics.bridgeTotalMs = MmdLivePhysicsDiagnosticsClock.Milliseconds(
                bridgeStartTimestamp,
                diagnosticsConstructionEndTimestamp);
            // Presence is represented independently from duration. A phase can be valid and do
            // zero work (for example no deform-after-physics bones), while unsampled or unused
            // phases remain explicitly absent rather than being mistaken for measured zeroes.
            if (!managedBodyTransformApplyPresent)
            {
                diagnostics.managedBodyTransformApplyMs = 0.0;
            }

            if (!afterPhysicsMatrixWorkPerformed)
            {
                diagnostics.afterPhysicsMatrixReadbackMs = 0.0;
                diagnostics.matrixTransformApplyMs = 0.0;
            }

            if (!sampledBodyDiagnosticsThisFrame)
            {
                diagnostics.sampledDiagnosticsMs = 0.0;
            }

            if (!evaluatedFrameRefreshPresent)
            {
                diagnostics.evaluatedFrameRefreshMs = 0.0;
            }

            lastLivePhysicsDiagnostics = diagnostics;
            return diagnostics;
        }

        private MmdPlaybackSnapshot BuildOrUpdateLiveSnapshot(MmdEvaluatedFrame frame)
        {
            lastLiveSnapshot ??= new MmdPlaybackSnapshot();
            lastLiveSnapshot.model = modelId;
            lastLiveSnapshot.motion = motionId;
            lastLiveSnapshot.frame = frame;
            lastLiveSnapshot.rendering = playbackInstance.RenderingDescriptor;
            return lastLiveSnapshot;
        }

        private bool CaptureAfterPhysicsWorldMatrices(MmdAnimPhysicsBackend nativeBackend)
        {
            if (!nativeHumanoidHostPoseEnabled && !model.HasDeformAfterPhysicsBones)
            {
                return false;
            }

            livePhysicsAfterPhysicsWorldMatrices ??= new float[nativeBackend.WorldMatrixFloatCount];
            if (livePhysicsAfterPhysicsWorldMatrices.Length != nativeBackend.WorldMatrixFloatCount)
            {
                livePhysicsAfterPhysicsWorldMatrices = new float[nativeBackend.WorldMatrixFloatCount];
            }

            nativeBackend.CopyAfterPhysicsWorldMatrices(livePhysicsAfterPhysicsWorldMatrices);
            return true;
        }

        private void ApplyAfterPhysicsWorldMatrices()
        {
            if (livePhysicsAfterPhysicsWorldMatrices == null)
            {
                return;
            }

            if (nativeHumanoidHostPoseEnabled)
            {
                // Humanoid Live intentionally applies the complete native post-physics pose once:
                // this preserves append/IK results for helper bones while keeping static, dynamic,
                // dynamic-orientation, and after-physics ownership in the native runtime.
                MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(
                    playbackInstance,
                    livePhysicsAfterPhysicsWorldMatrices);
                return;
            }

            MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(
                playbackInstance,
                livePhysicsAfterPhysicsWorldMatrices,
                fastAfterPhysicsBoneIndices);
        }

        private void CaptureHostPoseFromUnityTransforms(
            out float[] localPositionOffsets,
            out float[] localRotations,
            out float[] localScales,
            out float[] morphWeights,
            out byte[] ikEnabled)
        {
            int boneCount = model.bones.Count;
            localPositionOffsets = EnsureLivePhysicsBuffer(
                ref livePhysicsLocalPositionOffsets,
                checked(boneCount * 3));
            localRotations = EnsureLivePhysicsBuffer(
                ref livePhysicsLocalRotations,
                checked(boneCount * 4));
            localScales = EnsureLivePhysicsBuffer(
                ref livePhysicsLocalScales,
                checked(boneCount * 3));
            Array.Clear(localPositionOffsets, 0, localPositionOffsets.Length);
            Array.Clear(localRotations, 0, localRotations.Length);
            Array.Clear(localScales, 0, localScales.Length);
            for (int i = 0; i < boneCount; i++)
            {
                localRotations[i * 4 + 3] = 1.0f;
                localScales[i * 3] = 1.0f;
                localScales[i * 3 + 1] = 1.0f;
                localScales[i * 3 + 2] = 1.0f;
            }

            float importScale = NormalizeImportScale(playbackInstance.ImportScale);
            for (int i = 0; i < model.bones.Count; i++)
            {
                MmdBoneDefinition bone = model.bones[i];
                int index = bone.index;
                if (index < 0 || index >= playbackInstance.BoneTransforms.Length || index >= boneCount)
                {
                    continue;
                }

                Transform boneTransform = playbackInstance.BoneTransforms[index];
                Vector3 localDelta = boneTransform.localPosition - playbackInstance.BindLocalPositions[index];
                Quaternion localRotation = Quaternion.Inverse(playbackInstance.BindLocalRotations[index]) *
                    boneTransform.localRotation;
                Vector3 modelPosition = ToMmdModelPosition(localDelta, importScale);
                Quaternion modelRotation = ToMmdModelRotation(localRotation);
                int positionOffset = index * 3;
                localPositionOffsets[positionOffset] = modelPosition.x;
                localPositionOffsets[positionOffset + 1] = modelPosition.y;
                localPositionOffsets[positionOffset + 2] = modelPosition.z;
                int rotationOffset = index * 4;
                localRotations[rotationOffset] = modelRotation.x;
                localRotations[rotationOffset + 1] = modelRotation.y;
                localRotations[rotationOffset + 2] = modelRotation.z;
                localRotations[rotationOffset + 3] = modelRotation.w;
                int scaleOffset = index * 3;
                Vector3 scale = boneTransform.localScale;
                localScales[scaleOffset] = scale.x;
                localScales[scaleOffset + 1] = scale.y;
                localScales[scaleOffset + 2] = scale.z;
            }

            morphWeights = EnsureLivePhysicsBuffer(
                ref livePhysicsMorphWeights,
                model.morphs.Count);
            Array.Clear(morphWeights, 0, morphWeights.Length);
            SkinnedMeshRenderer? renderer = playbackInstance.SkinnedMeshRenderer;
            if (renderer != null)
            {
                for (int i = 0; i < model.morphs.Count; i++)
                {
                    MmdMorphDefinition morph = model.morphs[i];
                    if (morph.index < 0 || morph.index >= morphWeights.Length ||
                        !playbackInstance.BlendShapeIndexMap.TryGetValue(morph.name, out int blendShapeIndex))
                    {
                        continue;
                    }

                    morphWeights[morph.index] = renderer.GetBlendShapeWeight(blendShapeIndex) / 100.0f;
                }
            }

            // Humanoid Live supplies the retargeted pre-append pose to the native runtime, so native
            // IK remains enabled. Ordinary VMD Live keeps the legacy managed-pose contract and
            // disables native IK to avoid a second solve.
            ikEnabled = EnsureLivePhysicsBuffer(ref livePhysicsIkEnabled, model.ik.Count);
            if (nativeHumanoidHostPoseEnabled)
            {
                for (int i = 0; i < ikEnabled.Length; i++)
                {
                    ikEnabled[i] = 1;
                }
            }
            else
            {
                Array.Clear(ikEnabled, 0, ikEnabled.Length);
            }
        }

        private static float[] EnsureLivePhysicsBuffer(ref float[]? buffer, int length)
        {
            if (buffer == null || buffer.Length != length)
            {
                buffer = new float[length];
            }

            return buffer;
        }

        private static byte[] EnsureLivePhysicsBuffer(ref byte[]? buffer, int length)
        {
            if (buffer == null || buffer.Length != length)
            {
                buffer = new byte[length];
            }

            return buffer;
        }

        private MmdLivePhysicsPinnedBodyDiagnostics BuildHostPosePinnedBodyDiagnostics(bool resetSeed)
        {
            var diagnostics = new MmdLivePhysicsPinnedBodyDiagnostics();
            for (int i = 0; i < model.physics.rigidbodies.Count; i++)
            {
                MmdRigidbodyDefinition body = model.physics.rigidbodies[i];
                bool isStatic = IsStaticPhysicsKind(body.physicsKind);
                bool isDynamicOrientation = IsDynamicWithBonePhysicsKind(body.physicsKind);
                bool isDynamic = IsDynamicPhysicsKind(body.physicsKind);
                if (!resetSeed && !isStatic)
                {
                    continue;
                }

                if (!isStatic && !isDynamicOrientation && !isDynamic)
                {
                    continue;
                }

                diagnostics.pinnedBodyCount++;
                if (isStatic)
                {
                    diagnostics.staticPinnedBodyCount++;
                }
                else if (isDynamicOrientation)
                {
                    diagnostics.dynamicOrientationPinnedBodyCount++;
                }
                else if (isDynamic)
                {
                    diagnostics.dynamicInitialPinnedBodyCount++;
                }
            }

            return diagnostics;
        }

        private IMmdLivePhysicsBackend EnsureLivePhysicsBackend(bool resetOnCreate = true)
        {
            if (livePhysicsBackend != null)
            {
                return livePhysicsBackend;
            }

            if (!MmdAnimPhysicsBackend.TryCreate(
                    model.sourceBytes,
                    modelId,
                    motionId,
                    out MmdAnimPhysicsBackend? nativeBackend,
                    out string reason))
            {
                throw new MmdPhysicsBackendException(
                    "EnsureLivePhysicsBackend",
                    "mmd-anim-bullet-native",
                    "backend-unavailable",
                    reason,
                    modelId,
                    motionId);
            }

            try
            {
                nativeBackend!.InitializeWorld(model);
                if (resetOnCreate)
                {
                    nativeBackend.Reset();
                }
                livePhysicsBackend = nativeBackend;
                return nativeBackend;
            }
            catch
            {
                nativeBackend?.Dispose();
                throw;
            }
        }

        private void CaptureLivePhysicsReadback(IMmdLivePhysicsBackend backend)
        {
            int count = model.physics.rigidbodies.Count;
            if (livePhysicsReadbackTransforms == null || livePhysicsReadbackTransforms.Length != count)
            {
                livePhysicsReadbackTransforms = new MmdPhysicsBodyTransform[count];
                for (int i = 0; i < count; i++)
                {
                    livePhysicsReadbackTransforms[i] = new MmdPhysicsBodyTransform
                    {
                        position = new float[3],
                        rotation = new float[4]
                    };
                }
            }

            for (int i = 0; i < count; i++)
            {
                MmdPhysicsBodyTransform destination = livePhysicsReadbackTransforms[i];
                backend.CopyRigidbodyTransform(i, destination.position, destination.rotation);
                livePhysicsReadbackTransformCount++;
            }
        }

        private void ApplyPhysicsBodyTransforms()
        {
            float importScale = NormalizeImportScale(playbackInstance.ImportScale);
            MmdPhysicsBodyTransform[] readbackTransforms = GetLivePhysicsReadbackTransforms();
            for (int i = 0; i < model.physics.rigidbodies.Count; i++)
            {
                MmdRigidbodyDefinition body = model.physics.rigidbodies[i];
                if (body.boneIndex < 0 || body.boneIndex >= playbackInstance.BoneTransforms.Length)
                {
                    continue;
                }

                if (IsStaticPhysicsKind(body.physicsKind))
                {
                    continue;
                }

                MmdPhysicsBodyTransform bodyTransform = readbackTransforms[i];
                Transform bone = playbackInstance.BoneTransforms[body.boneIndex];
                Transform root = playbackInstance.Root.transform;
                Vector3 bodyOffset = ToMmdVector3(body.position) - GetBoneOrigin(body.boneIndex);
                Quaternion bodyModelRotation = ToMmdQuaternion(bodyTransform.rotation);
                Quaternion bodyLocalRotation = ToMmdEulerRotation(body.rotation);
                Quaternion boneModelRotation = bodyModelRotation * Quaternion.Inverse(bodyLocalRotation);
                if (!IsDynamicWithBonePhysicsKind(body.physicsKind))
                {
                    Vector3 boneModelPosition = ToMmdVector3(bodyTransform.position) - (boneModelRotation * bodyOffset);
                    bone.position = root.TransformPoint(ToUnityModelPosition(boneModelPosition, importScale));
                }

                bone.rotation = root.rotation * ToUnityModelRotation(boneModelRotation);
            }
        }

        private void ApplyPhysicsBodyDebugTransforms(IMmdLivePhysicsBackend backend)
        {
            LivePhysicsBodyMetadata[] metadata = GetLivePhysicsBodyMetadata(backend);
            if (metadata.Length == 0)
            {
                return;
            }

            Transform root = playbackInstance.Root.transform;
            float importScale = NormalizeImportScale(playbackInstance.ImportScale);
            MmdPhysicsBodyTransform[] readbackTransforms = GetLivePhysicsReadbackTransforms();
            for (int i = 0; i < model.physics.rigidbodies.Count; i++)
            {
                MmdUnityPhysicsBody? physicsBody = metadata[i].physicsBody;
                if (physicsBody == null)
                {
                    continue;
                }

                MmdPhysicsBodyTransform bodyTransform = readbackTransforms[i];
                physicsBody.transform.position = root.TransformPoint(ToUnityModelPosition(bodyTransform.position, importScale));
                physicsBody.transform.rotation = root.rotation * ToUnityModelRotation(bodyTransform.rotation);
                physicsBody.RecordNativeTransform(bodyTransform.position, bodyTransform.rotation);
            }
        }

        private MmdLivePhysicsBodyDiagnostics[] BuildBodyDiagnostics(IMmdLivePhysicsBackend backend)
        {
            Transform root = playbackInstance.Root.transform;
            float importScale = NormalizeImportScale(playbackInstance.ImportScale);
            int count = model.physics.rigidbodies.Count;
            LivePhysicsBodyMetadata[] metadata = GetLivePhysicsBodyMetadata(backend);
            MmdPhysicsBodyTransform[] readbackTransforms = GetLivePhysicsReadbackTransforms();
            var result = new MmdLivePhysicsBodyDiagnostics[count];
            for (int i = 0; i < count; i++)
            {
                MmdRigidbodyDefinition body = model.physics.rigidbodies[i];
                LivePhysicsBodyMetadata bodyMetadata = metadata[i];
                MmdUnityPhysicsBody? physicsBody = bodyMetadata.physicsBody;
                MmdPhysicsBodyTransform bodyTransform = readbackTransforms[i];
                bool hasBone = body.boneIndex >= 0 && body.boneIndex < playbackInstance.BoneTransforms.Length;
                Transform? bone = hasBone ? playbackInstance.BoneTransforms[body.boneIndex] : null;
                Vector3 boneWorldPos = bone != null ? bone.position : Vector3.zero;
                Vector3 boneModelPos = bone != null
                    ? ToMmdModelPosition(root.InverseTransformPoint(bone.position), importScale)
                    : Vector3.zero;
                Vector3 readbackMmdPos = ToMmdVector3(bodyTransform.position);
                Quaternion readbackMmdRot = ToMmdQuaternion(bodyTransform.rotation);
                Vector3 readbackWorldPos = root.TransformPoint(ToUnityModelPosition(bodyTransform.position, importScale));
                Quaternion readbackWorldRot = root.rotation * ToUnityModelRotation(bodyTransform.rotation);
                Vector3 debugWorldPos = physicsBody != null ? physicsBody.transform.position : Vector3.zero;
                Quaternion debugWorldRot = physicsBody != null ? physicsBody.transform.rotation : Quaternion.identity;
                float debugToReadback = physicsBody != null
                    ? Vector3.Distance(debugWorldPos, readbackWorldPos) : 0f;
                float boneToDebug = (hasBone && physicsBody != null)
                    ? Vector3.Distance(boneWorldPos, debugWorldPos) : 0f;
                float boneToReadback = hasBone
                    ? Vector3.Distance(boneWorldPos, readbackWorldPos) : 0f;
                result[i] = new MmdLivePhysicsBodyDiagnostics
                {
                    bodyIndex = body.index,
                    bodyName = body.name ?? string.Empty,
                    boneIndex = body.boneIndex,
                    boneName = body.boneName ?? string.Empty,
                    physicsKind = body.physicsKind ?? string.Empty,
                    shapeType = body.shapeType ?? string.Empty,
                    nativeShapeType = bodyMetadata.nativeShapeType,
                    mass = body.mass,
                    descriptorSize = body.size != null && body.size.Length >= 3
                        ? new Vector3(body.size[0], body.size[1], body.size[2]) : Vector3.zero,
                    descriptorPosition = body.position != null && body.position.Length >= 3
                        ? new Vector3(body.position[0], body.position[1], body.position[2]) : Vector3.zero,
                    descriptorRotation = body.rotation != null && body.rotation.Length >= 3
                        ? new Vector3(body.rotation[0], body.rotation[1], body.rotation[2]) : Vector3.zero,
                    debugColliderType = bodyMetadata.colliderType,
                    debugColliderSize = bodyMetadata.colliderSize,
                    boneWorldPosition = boneWorldPos,
                    boneModelPosition = boneModelPos,
                    readbackMmdPosition = readbackMmdPos,
                    readbackMmdRotation = readbackMmdRot,
                    readbackWorldPosition = readbackWorldPos,
                    readbackWorldRotation = readbackWorldRot,
                    debugColliderWorldPosition = debugWorldPos,
                    debugColliderWorldRotation = debugWorldRot,
                    debugToReadbackWorldDistance = debugToReadback,
                    boneToDebugWorldDistance = boneToDebug,
                    boneToReadbackWorldDistance = boneToReadback
                };
            }

            return result;
        }

        private MmdPhysicsBodyTransform[] GetLivePhysicsReadbackTransforms()
        {
            if (livePhysicsReadbackTransforms == null)
            {
                throw new InvalidOperationException("Live physics readback was not captured for the current frame.");
            }

            return livePhysicsReadbackTransforms;
        }

        private bool ShouldSampleLivePhysicsBodyDiagnostics(int frame)
        {
            return livePhysicsBodyDiagnosticsSampleInterval > 0 &&
                   frame % livePhysicsBodyDiagnosticsSampleInterval == 0;
        }

        private void ClearLivePhysicsBodyDiagnostics()
        {
            lastLivePhysicsBodyDiagnostics = Array.Empty<MmdLivePhysicsBodyDiagnostics>();
            lastLivePhysicsBodyDiagnosticsFrame = -1;
        }

        private LivePhysicsBodyMetadata[] GetLivePhysicsBodyMetadata(IMmdLivePhysicsBackend backend)
        {
            if (ReferenceEquals(livePhysicsMetadataInstance, playbackInstance) &&
                ReferenceEquals(livePhysicsMetadataBackend, backend) &&
                livePhysicsBodyMetadata != null)
            {
                return livePhysicsBodyMetadata;
            }

            MmdUnityPhysicsBody[] physicsBodies = playbackInstance.PhysicsBodies;
            var physicsBodiesByIndex = new Dictionary<int, MmdUnityPhysicsBody>(physicsBodies.Length);
            foreach (MmdUnityPhysicsBody physicsBody in physicsBodies)
            {
                if (physicsBody != null && physicsBody.BodyIndex >= 0)
                {
                    physicsBodiesByIndex[physicsBody.BodyIndex] = physicsBody;
                }
            }

            int count = model.physics.rigidbodies.Count;
            var metadata = new LivePhysicsBodyMetadata[count];
            for (int i = 0; i < count; i++)
            {
                physicsBodiesByIndex.TryGetValue(model.physics.rigidbodies[i].index, out MmdUnityPhysicsBody? physicsBody);
                metadata[i] = new LivePhysicsBodyMetadata(
                    physicsBody,
                    backend.GetRigidbodyShapeType(i),
                    ResolveColliderType(physicsBody),
                    ResolveColliderSize(physicsBody));
                livePhysicsReadbackShapeTypeCount++;
            }

            livePhysicsMetadataInstance = playbackInstance;
            livePhysicsMetadataBackend = backend;
            livePhysicsBodyMetadata = metadata;
            return metadata;
        }

        private readonly struct LivePhysicsBodyMetadata
        {
            public LivePhysicsBodyMetadata(
                MmdUnityPhysicsBody? physicsBody,
                string nativeShapeType,
                string colliderType,
                Vector3 colliderSize)
            {
                this.physicsBody = physicsBody;
                this.nativeShapeType = nativeShapeType;
                this.colliderType = colliderType;
                this.colliderSize = colliderSize;
            }

            public readonly MmdUnityPhysicsBody? physicsBody;
            public readonly string nativeShapeType;
            public readonly string colliderType;
            public readonly Vector3 colliderSize;
        }

        private static string ResolveColliderType(MmdUnityPhysicsBody? physicsBody)
        {
            if (physicsBody == null)
            {
                return string.Empty;
            }

            if (physicsBody.GetComponent<SphereCollider>() != null)
            {
                return "sphere";
            }

            if (physicsBody.GetComponent<BoxCollider>() != null)
            {
                return "box";
            }

            if (physicsBody.GetComponent<CapsuleCollider>() != null)
            {
                return "capsule";
            }

            return string.Empty;
        }

        private static Vector3 ResolveColliderSize(MmdUnityPhysicsBody? physicsBody)
        {
            if (physicsBody == null)
            {
                return Vector3.zero;
            }

            SphereCollider sphere = physicsBody.GetComponent<SphereCollider>();
            if (sphere != null)
            {
                return new Vector3(sphere.radius, sphere.radius, sphere.radius);
            }

            BoxCollider box = physicsBody.GetComponent<BoxCollider>();
            if (box != null)
            {
                return box.size;
            }

            CapsuleCollider capsule = physicsBody.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                return new Vector3(capsule.radius, capsule.height, capsule.direction);
            }

            return Vector3.zero;
        }

        private static bool IsStaticPhysicsKind(string? physicsKind)
        {
            return string.Equals(physicsKind, "static", StringComparison.Ordinal);
        }

        private static bool IsDynamicWithBonePhysicsKind(string? physicsKind)
        {
            return string.Equals(physicsKind, "dynamicBone", StringComparison.Ordinal) ||
                   string.Equals(physicsKind, "dynamic-orientation", StringComparison.Ordinal);
        }

        private static bool IsDynamicPhysicsKind(string? physicsKind)
        {
            return string.Equals(physicsKind, "dynamic", StringComparison.Ordinal);
        }
    }
}
