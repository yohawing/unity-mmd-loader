#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mmd;
using Mmd.Motion;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Pose;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackBinding
    {
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

            if (resetOnFirstStep)
            {
                SoftResetLivePhysicsSimulation();
            }

            var totalWatch = Stopwatch.StartNew();
            var stageWatch = Stopwatch.StartNew();
            BulletMmdPhysicsBackend backend = EnsureLivePhysicsBackend();
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
            livePhysicsBackend?.Dispose();
            livePhysicsBackend = null;
            lastLiveFrame = -1;
            lastForwardPlaybackFrame = -1;
            lastLiveSnapshot = null;
            lastLivePhysicsDiagnostics = null;
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
            BulletMmdPhysicsBackend backend = EnsureLivePhysicsBackend();
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
                MmdUnityFrameApplier.ApplyFrame(Instance, evaluatedFrame!);
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
            lastLiveSnapshot = session.BuildSnapshotFromEvaluatedFrame(evaluatedFrame!, Instance.RenderingDescriptor);
            totalWatch.Stop();
            diagnostics.refreshSnapshotFrameMs = refreshSnapshotFrameMs;
            diagnostics.totalMs = totalWatch.Elapsed.TotalMilliseconds;
            lastLivePhysicsDiagnostics = diagnostics;
            return lastLiveSnapshot;
        }

        private MmdLivePhysicsFrameDiagnostics StepLivePhysicsCore(
            BulletMmdPhysicsBackend backend,
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
            var stageWatch = Stopwatch.StartNew();
            MmdLivePhysicsPinnedBodyDiagnostics pinnedBodyDiagnostics;
            double syncBoneDrivenBodiesMs;
            double stepPhysicsMs;
            if (resetSeed)
            {
                // saba PMXModel::ResetPhysics-style seed. The bones are already at the CURRENT driving pose
                // (VMD animation or Humanoid retarget), so seed from those Unity transforms without replaying VMD.
                pinnedBodyDiagnostics = SeedLivePhysics(backend, sequenceFrame);
                syncBoneDrivenBodiesMs = stageWatch.Elapsed.TotalMilliseconds;
                stepPhysicsMs = 0.0;
                deltaTime = 0.0f;
            }
            else
            {
                pinnedBodyDiagnostics = SyncBoneDrivenPhysicsBodies(backend, includeDynamicBodies: false);
                syncBoneDrivenBodiesMs = stageWatch.Elapsed.TotalMilliseconds;
                stageWatch.Restart();
                backend.Step(sequenceFrame, deltaTime);
                stepPhysicsMs = stageWatch.Elapsed.TotalMilliseconds;
            }

            stageWatch.Restart();
            ApplyPhysicsBodyTransforms(backend);
            double applyPhysicsBodiesMs = stageWatch.Elapsed.TotalMilliseconds;
            stageWatch.Restart();
            ApplyAfterPhysicsBoneEvaluationFromUnityTransforms();
            ApplyPhysicsBodyDebugTransforms(backend);
            MmdLivePhysicsBodyDiagnostics[] bodyDiagnostics = BuildBodyDiagnostics(backend);
            if (evaluatedFrame != null)
            {
                RefreshEvaluatedFrameFromUnityTransforms(evaluatedFrame);
            }

            refreshSnapshotFrameMs = stageWatch.Elapsed.TotalMilliseconds;
            lastLivePhysicsDiagnostics = new MmdLivePhysicsFrameDiagnostics
            {
                frame = sequenceFrame,
                deltaTime = deltaTime,
                totalMs = totalWatch.Elapsed.TotalMilliseconds,
                ensureBackendMs = ensureBackendMs,
                evaluateFrameMs = evaluateFrameMs,
                applyAnimationFrameMs = applyAnimationFrameMs,
                syncBoneDrivenBodiesMs = syncBoneDrivenBodiesMs,
                stepPhysicsMs = stepPhysicsMs,
                applyPhysicsBodiesMs = applyPhysicsBodiesMs,
                refreshSnapshotFrameMs = refreshSnapshotFrameMs,
                pinnedBodies = pinnedBodyDiagnostics,
                unsupportedWorldAnchorJointCount = backend.SkippedWorldAnchorJointCount,
                comparisonSpace = "runtime-forward-playback-diagnostics",
                importScale = Instance.ImportScale,
                bodyDiagnostics = bodyDiagnostics
            };
            return lastLivePhysicsDiagnostics;
        }

        private void ApplyAfterPhysicsBoneEvaluationFromUnityTransforms()
        {
            if (!model.HasDeformAfterPhysicsBones)
            {
                return;
            }

            MmdSampledMotion postPhysicsPose = CaptureCurrentBonePoseFromUnityTransforms();
            MmdSampledMotion afterAppend = MmdAppendTransformEvaluator.ApplyAppendTransforms(
                model,
                postPhysicsPose,
                MmdBoneEvaluationPass.AfterPhysics);
            MmdSampledMotion afterIk = new MmdIkSolver().Solve(
                model,
                postPhysicsPose,
                afterAppend,
                MmdBoneEvaluationPass.AfterPhysics);
            ApplyBoneTransformsOnly(afterIk, bone => bone.deformAfterPhysics);
        }

        private MmdSampledMotion CaptureCurrentBonePoseFromUnityTransforms()
        {
            var motion = new MmdSampledMotion();
            float importScale = NormalizeImportScale(Instance.ImportScale);
            for (int i = 0; i < model.bones.Count; i++)
            {
                MmdBoneDefinition bone = model.bones[i];
                int index = bone.index;
                if (index < 0 || index >= Instance.BoneTransforms.Length)
                {
                    continue;
                }

                Transform boneTransform = Instance.BoneTransforms[index];
                Vector3 localDelta = boneTransform.localPosition - Instance.BindLocalPositions[index];
                Quaternion localRotation = Quaternion.Inverse(Instance.BindLocalRotations[index]) * boneTransform.localRotation;
                motion.Bones[bone.name] = new MmdBonePoseSample(
                    ToArray(ToMmdModelPosition(localDelta, importScale)),
                    ToArray(ToMmdModelRotation(localRotation)));
            }

            return motion;
        }

        private void ApplyBoneTransformsOnly(MmdSampledMotion motion, Func<MmdBoneDefinition, bool> predicate)
        {
            for (int i = 0; i < model.bones.Count; i++)
            {
                MmdBoneDefinition bone = model.bones[i];
                if (!predicate(bone))
                {
                    continue;
                }

                int index = bone.index;
                if (index < 0 || index >= Instance.BoneTransforms.Length)
                {
                    continue;
                }

                if (!motion.Bones.TryGetValue(bone.name, out MmdBonePoseSample pose))
                {
                    continue;
                }

                Transform boneTransform = Instance.BoneTransforms[index];
                boneTransform.localPosition = Instance.BindLocalPositions[index] + ToUnityModelPosition(pose.Translation, Instance.ImportScale);
                boneTransform.localRotation = Instance.BindLocalRotations[index] * ToUnityModelRotation(pose.Rotation);
            }
        }

        private BulletMmdPhysicsBackend EnsureLivePhysicsBackend()
        {
            if (livePhysicsBackend != null)
            {
                return livePhysicsBackend;
            }

            var backend = new BulletMmdPhysicsBackend(modelId, motionId);
            backend.InitializeWorld(model);
            backend.Reset();
            livePhysicsBackend = backend;
            return backend;
        }

        // saba PMXModel::ResetPhysics settles the bodies at the current pose over a SINGLE short fixed step
        // (physics->Update(1/60)), not a long ease-in. A multi-step settle injects oscillation energy and is
        // not saba-faithful, so the seed uses exactly one short step.
        private const float LivePhysicsSeedSettleSeconds = 1.0f / 60.0f;

        /// <summary>
        /// Seeds the live simulation at (or after) a reset, mirroring saba PMXModel::ResetPhysics. The native
        /// Reset() returned every body to its origin-space descriptor (bind) transform; saba's ResetPhysics
        /// instead re-syncs each body to its CURRENT node global transform (MMDRigidBody::ResetTransform ->
        /// DynamicMotionState::Reset), runs a single short physics Update, then cleans contact pairs and zeroes
        /// velocity (MMDRigidBody::Reset). We replicate that here for BOTH the fast (native FFI) and managed
        /// evaluation paths — the bones are ALREADY at the current motion pose before this runs:
        ///   1. Place EVERY body (INCLUDING pure-dynamic mode-1) at the CURRENT bone-derived pose
        ///      (SyncBoneDrivenPhysicsBodies -> SetRigidbodyTransform sets world + motion-state transform).
        ///   2. Re-align the native interpolation transform with the just-placed world transform and zero all
        ///      velocities (backend.SyncInterpolationAndZeroVelocity). Without this, native Reset() left the
        ///      interpolation transform at the ORIGIN-bind, so the first forward Step would compute a kinematic
        ///      velocity of (currentPose - originBind)/dt and fling the jointed dynamic chain apart.
        ///   3. ONE short settle step at the current pose so the joints relax (saba physics->Update(1/60)).
        ///   4. Re-pin static kinematic bodies at the current pose. Mode-2 dynamic-orientation bodies remain
        ///      active dynamic bodies after the reset seed.
        /// This is a settle, not a sweep from bind, so a pure-dynamic body never snaps toward the origin-space
        /// bind while the model is animated far away (the reported "揺れ骨が BindPose の場所に戻る" bug).
        /// </summary>
        private MmdLivePhysicsPinnedBodyDiagnostics SeedLivePhysics(
            BulletMmdPhysicsBackend backend, int frame)
        {
            // 1. saba MMDRigidBody::ResetTransform: place EVERY body (including pure-dynamic mode-1) at its
            //    current bone-derived model-space pose, overriding the origin-bind that native Reset() set.
            MmdLivePhysicsPinnedBodyDiagnostics seedDiagnostics =
                SyncBoneDrivenPhysicsBodies(backend, includeDynamicBodies: true);

            // 2. Re-align the native interpolation transform with the just-placed world transform and zero
            //    velocity so the upcoming step (and the first forward Step) computes no spurious kinematic
            //    velocity from the stale origin-bind interpolation transform left by native Reset().
            backend.SyncInterpolationAndZeroVelocity();

            // 3. saba ResetPhysics' physics->Update settle: a SINGLE short step relaxes the joints at the
            //    current pose. The rig stays at the current pose, so the bone-driven bodies stay pinned and
            //    the dynamics settle in place instead of being dragged toward the origin.
            backend.Step(frame, LivePhysicsSeedSettleSeconds);

            // 4. Re-pin only static bodies at the current pose. Return the seed diagnostics so reset-frame
            //    reports still show that mode-1 and mode-2 dynamic bodies were initialized and zeroed.
            SyncBoneDrivenPhysicsBodies(backend, includeDynamicBodies: false);
            return seedDiagnostics;
        }

        private MmdLivePhysicsPinnedBodyDiagnostics SyncBoneDrivenPhysicsBodies(
            BulletMmdPhysicsBackend backend,
            bool includeDynamicBodies)
        {
            Transform root = Instance.Root.transform;
            float importScale = NormalizeImportScale(Instance.ImportScale);
            var diagnostics = new MmdLivePhysicsPinnedBodyDiagnostics();
            for (int i = 0; i < model.physics.rigidbodies.Count; i++)
            {
                MmdRigidbodyDefinition body = model.physics.rigidbodies[i];
                bool isStatic = IsStaticPhysicsKind(body.physicsKind);
                bool isDynamicOrientation = IsDynamicWithBonePhysicsKind(body.physicsKind);
                bool isDynamic = IsDynamicPhysicsKind(body.physicsKind);
                bool shouldSyncBody = isStatic || (includeDynamicBodies && (isDynamicOrientation || isDynamic));
                if (!shouldSyncBody)
                {
                    continue;
                }

                if (body.boneIndex < 0 || body.boneIndex >= Instance.BoneTransforms.Length)
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

                Transform bone = Instance.BoneTransforms[body.boneIndex];
                Vector3 boneModelPosition = ToMmdModelPosition(root.InverseTransformPoint(bone.position), importScale);
                Vector3 bodyOffset = ToMmdVector3(body.position) - GetBoneOrigin(body.boneIndex);
                Quaternion boneModelRotation = ToMmdModelRotation(Quaternion.Inverse(root.rotation) * bone.rotation);
                Quaternion bodyLocalRotation = ToMmdEulerRotation(body.rotation);
                Quaternion bodyModelRotation = boneModelRotation * bodyLocalRotation;

                Vector3 rotatedBodyOffset = boneModelRotation * bodyOffset;
                backend.SetRigidbodyTransform(
                    i,
                    ToArray(boneModelPosition + rotatedBodyOffset),
                    ToArray(bodyModelRotation));
                MmdPhysicsBodyTransform syncedTransform = backend.GetRigidbodyTransform(i);
                Vector3 expectedPosition = boneModelPosition + rotatedBodyOffset;
                Vector3 actualPosition = ToMmdVector3(syncedTransform.position);
                float distance = Vector3.Distance(expectedPosition, actualPosition);
                Quaternion actualRotation = ToMmdQuaternion(syncedTransform.rotation);
                float rotationAngle = Quaternion.Angle(bodyModelRotation, actualRotation);
                diagnostics.maxPinnedBodySyncDistance = Math.Max(diagnostics.maxPinnedBodySyncDistance, distance);
                diagnostics.maxPinnedBodyRotationAngle = Math.Max(diagnostics.maxPinnedBodyRotationAngle, rotationAngle);
                if (distance > diagnostics.worstPinnedBodySyncDistance || diagnostics.worstPinnedBodyIndex < 0)
                {
                    diagnostics.worstPinnedBodySyncDistance = distance;
                    diagnostics.worstPinnedBodyIndex = i;
                    diagnostics.worstPinnedBodyName = body.name;
                    diagnostics.worstPinnedBodyBoneIndex = body.boneIndex;
                    diagnostics.worstPinnedBodyBoneName = body.boneName;
                    diagnostics.worstPinnedBodyPhysicsKind = body.physicsKind;
                }

                if (rotationAngle > diagnostics.worstPinnedBodyRotationAngle || diagnostics.worstPinnedBodyRotationIndex < 0)
                {
                    diagnostics.worstPinnedBodyRotationAngle = rotationAngle;
                    diagnostics.worstPinnedBodyRotationIndex = i;
                    diagnostics.worstPinnedBodyRotationName = body.name;
                    diagnostics.worstPinnedBodyRotationBoneIndex = body.boneIndex;
                    diagnostics.worstPinnedBodyRotationBoneName = body.boneName;
                    diagnostics.worstPinnedBodyRotationPhysicsKind = body.physicsKind;
                }
            }

            return diagnostics;
        }

        private void ApplyPhysicsBodyTransforms(BulletMmdPhysicsBackend backend)
        {
            float importScale = NormalizeImportScale(Instance.ImportScale);
            for (int i = 0; i < model.physics.rigidbodies.Count; i++)
            {
                MmdRigidbodyDefinition body = model.physics.rigidbodies[i];
                if (body.boneIndex < 0 || body.boneIndex >= Instance.BoneTransforms.Length)
                {
                    continue;
                }

                if (IsStaticPhysicsKind(body.physicsKind))
                {
                    continue;
                }

                MmdPhysicsBodyTransform bodyTransform = backend.GetRigidbodyTransform(i);
                Transform bone = Instance.BoneTransforms[body.boneIndex];
                Transform root = Instance.Root.transform;
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

        private void ApplyPhysicsBodyDebugTransforms(BulletMmdPhysicsBackend backend)
        {
            Dictionary<int, MmdUnityPhysicsBody> physicsBodiesByIndex = BuildPhysicsBodyIndexMap();
            if (physicsBodiesByIndex.Count == 0)
            {
                return;
            }

            Transform root = Instance.Root.transform;
            float importScale = NormalizeImportScale(Instance.ImportScale);
            for (int i = 0; i < model.physics.rigidbodies.Count; i++)
            {
                MmdRigidbodyDefinition body = model.physics.rigidbodies[i];
                if (!physicsBodiesByIndex.TryGetValue(body.index, out MmdUnityPhysicsBody physicsBody) ||
                    physicsBody == null)
                {
                    continue;
                }

                MmdPhysicsBodyTransform bodyTransform = backend.GetRigidbodyTransform(i);
                physicsBody.transform.position = root.TransformPoint(ToUnityModelPosition(bodyTransform.position, importScale));
                physicsBody.transform.rotation = root.rotation * ToUnityModelRotation(bodyTransform.rotation);
                physicsBody.RecordNativeTransform(bodyTransform.position, bodyTransform.rotation);
            }
        }

        private MmdLivePhysicsBodyDiagnostics[] BuildBodyDiagnostics(BulletMmdPhysicsBackend backend)
        {
            Transform root = Instance.Root.transform;
            float importScale = NormalizeImportScale(Instance.ImportScale);
            Dictionary<int, MmdUnityPhysicsBody> physicsBodiesByIndex = BuildPhysicsBodyIndexMap();
            int count = model.physics.rigidbodies.Count;
            var result = new MmdLivePhysicsBodyDiagnostics[count];
            for (int i = 0; i < count; i++)
            {
                MmdRigidbodyDefinition body = model.physics.rigidbodies[i];
                physicsBodiesByIndex.TryGetValue(body.index, out MmdUnityPhysicsBody? physicsBody);
                MmdPhysicsBodyTransform bodyTransform = backend.GetRigidbodyTransform(i);
                bool hasBone = body.boneIndex >= 0 && body.boneIndex < Instance.BoneTransforms.Length;
                Transform? bone = hasBone ? Instance.BoneTransforms[body.boneIndex] : null;
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
                    nativeShapeType = backend.GetRigidbodyShapeType(i),
                    mass = body.mass,
                    descriptorSize = body.size != null && body.size.Length >= 3
                        ? new Vector3(body.size[0], body.size[1], body.size[2]) : Vector3.zero,
                    descriptorPosition = body.position != null && body.position.Length >= 3
                        ? new Vector3(body.position[0], body.position[1], body.position[2]) : Vector3.zero,
                    descriptorRotation = body.rotation != null && body.rotation.Length >= 3
                        ? new Vector3(body.rotation[0], body.rotation[1], body.rotation[2]) : Vector3.zero,
                    debugColliderType = ResolveColliderType(physicsBody),
                    debugColliderSize = ResolveColliderSize(physicsBody),
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

        private Dictionary<int, MmdUnityPhysicsBody> BuildPhysicsBodyIndexMap()
        {
            MmdUnityPhysicsBody[] physicsBodies = Instance.PhysicsBodies;
            var result = new Dictionary<int, MmdUnityPhysicsBody>(physicsBodies.Length);
            foreach (MmdUnityPhysicsBody physicsBody in physicsBodies)
            {
                if (physicsBody == null || physicsBody.BodyIndex < 0)
                {
                    continue;
                }

                result[physicsBody.BodyIndex] = physicsBody;
            }

            return result;
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