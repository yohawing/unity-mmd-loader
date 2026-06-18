#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mmd;
using Mmd.Native;
using Mmd.Parser;
using Mmd.Physics;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed class MmdUnityPlaybackBinding : IDisposable
    {
        private readonly MmdRuntimeSession session;
        private readonly MmdModelDefinition model;
        private readonly string modelId;
        private readonly string motionId;
        private BulletMmdPhysicsBackend? livePhysicsBackend;
        private int lastLiveFrame = -1;
        // Tracks the last frame seen by forward Timeline playback so a backward jump (scrub/seek) can be
        // distinguished from continuous forward play. Persists across ResetLivePhysics during a scrub so
        // a continuous backward drag keeps being treated as a scrub.
        private int lastForwardPlaybackFrame = -1;
        private MmdPlaybackSnapshot? lastLiveSnapshot;
        private MmdLivePhysicsFrameDiagnostics? lastLivePhysicsDiagnostics;
        private MmdPhysicsMode physicsMode = MmdPhysicsMode.Off;
        private MmdRuntimeFfiPlaybackSession? fastSession;
        private float[]? fastWorldMatrices;
        private float[]? fastMorphWeights;
        private byte[]? fastIkEnabled;
        private MmdEvaluatedFrame? fastMorphFrame;
        private float[]? fastLastAppliedMorphWeights;
        private bool fastMorphApplied;
        private bool fastMorphCacheValid;
        private MmdPlaybackSnapshot? fastSnapshot;

        private MmdUnityPlaybackBinding(
            MmdUnityModelInstance instance,
            MmdRuntimeSession session,
            MmdModelDefinition model,
            string modelId,
            string motionId)
        {
            Instance = instance;
            this.session = session;
            this.model = model;
            this.modelId = modelId;
            this.motionId = motionId;
        }

        public MmdUnityModelInstance Instance { get; }

        public string MotionId => motionId;

        public MmdPhysicsMode PhysicsMode => physicsMode;

        public MmdLivePhysicsFrameDiagnostics? LastLivePhysicsDiagnostics => lastLivePhysicsDiagnostics;

        public bool IsFastRuntimeEnabled => fastSession != null;

        public static MmdUnityPlaybackBinding CreateSkinned(
            MmdPmxAsset modelAsset,
            MmdVmdAsset motionAsset)
        {
            if (modelAsset == null)
            {
                throw new ArgumentNullException(nameof(modelAsset));
            }

            if (motionAsset == null)
            {
                throw new ArgumentNullException(nameof(motionAsset));
            }

            var parser = new NativeMmdParser();
            MmdModelDefinition model = modelAsset.LoadModel(parser);
            MmdModelValidator.ThrowIfInvalid(model);
            MmdMotionDefinition motion = motionAsset.LoadMotion(parser);
            MmdMotionValidator.ThrowIfInvalid(motion);
            float importScale = modelAsset.ImportScale;
            return CreateSkinned(
                model,
                motion,
                string.IsNullOrWhiteSpace(modelAsset.SourceId) ? modelAsset.name : modelAsset.SourceId,
                string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId,
                string.IsNullOrWhiteSpace(modelAsset.SourcePath) ? null : modelAsset.SourcePath,
                importScale);
        }

        public static MmdUnityPlaybackBinding CreateSkinned(
            MmdUnityModelInstance instance,
            MmdPmxAsset modelAsset,
            MmdVmdAsset motionAsset)
        {
            if (motionAsset == null)
            {
                throw new ArgumentNullException(nameof(motionAsset));
            }

            var parser = new NativeMmdParser();
            MmdMotionDefinition motion = motionAsset.LoadMotion(parser);
            MmdMotionValidator.ThrowIfInvalid(motion);
            return CreateSkinned(instance, modelAsset, motionAsset, motion);
        }

        public static MmdUnityPlaybackBinding CreateSkinned(
            MmdUnityModelInstance instance,
            MmdPmxAsset modelAsset,
            MmdVmdAsset motionAsset,
            MmdMotionDefinition motion)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (modelAsset == null)
            {
                throw new ArgumentNullException(nameof(modelAsset));
            }

            if (motionAsset == null)
            {
                throw new ArgumentNullException(nameof(motionAsset));
            }

            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            var parser = new NativeMmdParser();
            MmdModelDefinition model = modelAsset.LoadModel(parser);
            MmdModelValidator.ThrowIfInvalid(model);
            MmdMotionValidator.ThrowIfInvalid(motion);
            var session = new MmdRuntimeSession(
                model,
                motion,
                string.IsNullOrWhiteSpace(modelAsset.SourceId) ? modelAsset.name : modelAsset.SourceId,
                string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId);
            return new MmdUnityPlaybackBinding(
                instance,
                session,
                model,
                string.IsNullOrWhiteSpace(modelAsset.SourceId) ? modelAsset.name : modelAsset.SourceId,
                string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId);
        }

        public static MmdUnityPlaybackBinding CreateSkinned(
            MmdUnityModelInstance instance,
            MmdModelDefinition model,
            MmdVmdAsset motionAsset,
            MmdMotionDefinition motion,
            string modelId,
            string? sourcePath = null)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (motionAsset == null)
            {
                throw new ArgumentNullException(nameof(motionAsset));
            }

            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            MmdMotionValidator.ThrowIfInvalid(motion);
            string resolvedModelId = string.IsNullOrWhiteSpace(modelId) ? "PMX" : modelId;
            string resolvedMotionId = string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId;
            var session = new MmdRuntimeSession(model, motion, resolvedModelId, resolvedMotionId);
            return new MmdUnityPlaybackBinding(instance, session, model, resolvedModelId, resolvedMotionId);
        }

        public static MmdUnityPlaybackBinding CreateSkinnedFromExistingSceneModel(
            GameObject root,
            MmdPmxAsset modelAsset,
            MmdVmdAsset motionAsset,
            MmdMotionDefinition motion)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (modelAsset == null)
            {
                throw new ArgumentNullException(nameof(modelAsset));
            }

            if (motionAsset == null)
            {
                throw new ArgumentNullException(nameof(motionAsset));
            }

            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            var parser = new NativeMmdParser();
            MmdModelDefinition model = modelAsset.LoadModel(parser);
            MmdModelValidator.ThrowIfInvalid(model);
            MmdMotionValidator.ThrowIfInvalid(motion);
            float importScale = modelAsset.ImportScale;
            MmdUnityModelInstance instance = MmdUnityModelFactory.CreateExistingSkinnedModelInstance(
                root,
                model,
                string.IsNullOrWhiteSpace(modelAsset.SourcePath) ? null : modelAsset.SourcePath,
                importScale);
            var session = new MmdRuntimeSession(
                model,
                motion,
                string.IsNullOrWhiteSpace(modelAsset.SourceId) ? modelAsset.name : modelAsset.SourceId,
                string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId);
            return new MmdUnityPlaybackBinding(
                instance,
                session,
                model,
                string.IsNullOrWhiteSpace(modelAsset.SourceId) ? modelAsset.name : modelAsset.SourceId,
                string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId);
        }

        public static MmdUnityPlaybackBinding CreateSkinned(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            string modelId,
            string motionId,
            string? sourcePath = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            MmdUnityModelInstance instance = MmdUnityModelFactory.CreateSkinnedModel(model, sourcePath);
            var session = new MmdRuntimeSession(model, motion, modelId, motionId);
            return new MmdUnityPlaybackBinding(instance, session, model, modelId, motionId);
        }

        public static MmdUnityPlaybackBinding CreateSkinned(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            string modelId,
            string motionId,
            string? sourcePath,
            float importScale)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            float scale = (float.IsFinite(importScale) && importScale > 0.0f) ? importScale : 1.0f;
            MmdUnityModelInstance instance = MmdUnityModelFactory.CreateSkinnedModel(model, sourcePath, scale);
            var session = new MmdRuntimeSession(model, motion, modelId, motionId);
            return new MmdUnityPlaybackBinding(instance, session, model, modelId, motionId);
        }

        public void SetPhysicsMode(MmdPhysicsMode mode)
        {
            switch (mode)
            {
                case MmdPhysicsMode.Off:
                case MmdPhysicsMode.Live:
                    break;
                case MmdPhysicsMode.Cache:
                    throw new NotSupportedException("Physics Cache is not implemented yet.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown physics mode.");
            }

            physicsMode = mode;
            ResetLivePhysics();
        }

        public MmdPlaybackSnapshot ApplyFrame(int frame, float frameRate)
        {
            MmdPlaybackTime.ValidateFrame(frame);
            MmdPlaybackTime.ValidateFrameRate(frameRate);
            if (physicsMode == MmdPhysicsMode.Live)
            {
                return ApplyLivePhysicsFrame(frame, frameRate, allowArbitraryStart: false);
            }

            if (fastSession != null)
            {
                return ApplyFastFrame(frame, frameRate);
            }

            MmdEvaluatedFrame evaluatedFrame = session.EvaluateFrame(frame, MmdPlaybackTime.ToTime(frame, frameRate));
            MmdUnityFrameApplier.ApplyFrame(Instance, evaluatedFrame);
            return session.BuildSnapshotFromEvaluatedFrame(evaluatedFrame, Instance.RenderingDescriptor);
        }

        public MmdPlaybackSnapshot ApplyTime(float time, float frameRate)
        {
            MmdPlaybackTime.ValidateTime(time);
            MmdPlaybackTime.ValidateFrameRate(frameRate);
            if (physicsMode == MmdPhysicsMode.Live)
            {
                throw new InvalidOperationException("Physics Live does not support random-access time evaluation. Use forward playback Tick/ApplyFrame.");
            }

            if (fastSession != null)
            {
                return ApplyFastTime(time, frameRate);
            }

            MmdEvaluatedFrame evaluatedFrame = session.EvaluateFrameAtTime(time, frameRate);
            MmdUnityFrameApplier.ApplyFrame(Instance, evaluatedFrame);
            InvalidateFastMorphCache();
            return session.BuildSnapshotFromEvaluatedFrame(evaluatedFrame, Instance.RenderingDescriptor);
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

        public void Dispose()
        {
            DisableFastRuntime();
            ResetLivePhysics();
        }

        /// <summary>
        /// Opt-in fast runtime using the native mmd-runtime FFI library.
        /// Affects <see cref="ApplyFrame"/> in animation-only playback and the animation pose stage of Live physics playback.
        /// Returns true and clears <paramref name="reason"/> on success.
        /// Returns false with a diagnostic message in <paramref name="reason"/> when the native library is absent,
        /// ABI-incompatible, or the bone/morph counts do not match the managed model.
        /// </summary>
        public bool TryEnableFastRuntime(byte[] pmxBytes, byte[] vmdBytes, out string reason)
        {
            if (pmxBytes == null || pmxBytes.Length == 0)
            {
                throw new ArgumentException("PMX bytes are required.", nameof(pmxBytes));
            }

            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(vmdBytes));
            }

            DisposeFastRuntime();
            try
            {
                MmdRuntimeFfiPlaybackSession candidate = MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes);
                int candidateBoneCount = candidate.BoneCount;
                int candidateMorphCount = candidate.MorphCount;
                if (candidateBoneCount != model.bones.Count)
                {
                    candidate.Dispose();
                    reason = $"mmd-runtime bone count {candidateBoneCount} does not match managed model bone count {model.bones.Count}.";
                    return false;
                }

                if (candidateMorphCount != model.morphs.Count)
                {
                    candidate.Dispose();
                    reason = $"mmd-runtime morph count {candidateMorphCount} does not match managed model morph count {model.morphs.Count}.";
                    return false;
                }

                int expectedWorldMatrixFloatCount = model.bones.Count * 16;
                if (candidate.WorldMatrixFloatCount < expectedWorldMatrixFloatCount)
                {
                    candidate.Dispose();
                    reason = $"mmd-runtime world matrix float count {candidate.WorldMatrixFloatCount} is smaller than required {expectedWorldMatrixFloatCount}.";
                    return false;
                }

                if (candidate.MorphWeightCount != model.morphs.Count)
                {
                    candidate.Dispose();
                    reason = $"mmd-runtime morph weight count {candidate.MorphWeightCount} does not match managed model morph count {model.morphs.Count}.";
                    return false;
                }

                if (candidate.IkEnabledCount != model.ik.Count)
                {
                    candidate.Dispose();
                    reason = $"mmd-runtime IK enabled count {candidate.IkEnabledCount} does not match managed model IK count {model.ik.Count}.";
                    return false;
                }

                fastSession = candidate;
                fastWorldMatrices = new float[fastSession.WorldMatrixFloatCount];
                fastMorphWeights = new float[fastSession.MorphWeightCount];
                fastIkEnabled = new byte[fastSession.IkEnabledCount];
                fastLastAppliedMorphWeights = new float[fastSession.MorphWeightCount];
                fastMorphFrame = BuildFastMorphFrame(fastMorphWeights);
                fastMorphApplied = false;
                fastMorphCacheValid = false;
                reason = string.Empty;
                return true;
            }
            catch (DllNotFoundException ex)
            {
                DisposeFastRuntime();
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                DisposeFastRuntime();
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (BadImageFormatException ex)
            {
                DisposeFastRuntime();
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            catch (InvalidOperationException ex)
            {
                DisposeFastRuntime();
                reason = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public void DisableFastRuntime()
        {
            DisposeFastRuntime();
        }

        private void DisposeFastRuntime()
        {
            fastSession?.Dispose();
            fastSession = null;
            fastWorldMatrices = null;
            fastMorphWeights = null;
            fastIkEnabled = null;
            fastMorphFrame = null;
            fastLastAppliedMorphWeights = null;
            fastMorphApplied = false;
            fastMorphCacheValid = false;
            fastSnapshot = null;
        }

        private void InvalidateFastMorphCache()
        {
            if (fastSession == null)
            {
                return;
            }

            fastMorphApplied = false;
            fastMorphCacheValid = false;
        }

        private MmdPlaybackSnapshot ApplyFastFrame(int frame, float frameRate)
        {
            float time = MmdPlaybackTime.ToTime(frame, frameRate);
            fastSession!.EvaluateAndCopy(frame, fastWorldMatrices!, fastMorphWeights!, fastIkEnabled!);
            MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(Instance, fastWorldMatrices!);
            ApplyFastMorphWeights();
            // Lightweight snapshot: no managed session.EvaluateFrame call.
            // fastMorphFrame is reused in-place; frame/time are updated each call.
            // bones is empty because world matrices are applied directly to Unity transforms.
            // morphs reflects the last-applied fast weights (mutated on subsequent calls).
            // rendering is the stable Instance.RenderingDescriptor reference.
            // See runtime-session contract "fast-runtime binding snapshot mode".
            fastMorphFrame!.frame = frame;
            fastMorphFrame.time = time;
            fastSnapshot ??= new MmdPlaybackSnapshot
            {
                model = modelId,
                motion = motionId,
                frame = fastMorphFrame,
                rendering = Instance.RenderingDescriptor
            };
            return fastSnapshot;
        }

        private MmdPlaybackSnapshot ApplyFastTime(float time, float frameRate)
        {
            int frame = MmdPlaybackTime.ToFrame(time, frameRate);
            fastSession!.EvaluateAndCopy(frame, fastWorldMatrices!, fastMorphWeights!, fastIkEnabled!);
            MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(Instance, fastWorldMatrices!);
            ApplyFastMorphWeights();
            fastMorphFrame!.frame = frame;
            fastMorphFrame.time = time;
            fastSnapshot ??= new MmdPlaybackSnapshot
            {
                model = modelId,
                motion = motionId,
                frame = fastMorphFrame,
                rendering = Instance.RenderingDescriptor
            };
            return fastSnapshot;
        }

        private void ApplyFastMorphWeights()
        {
            bool hasNonZero = HasAnyNonZeroMorphWeight(fastMorphWeights!);
            if (fastMorphCacheValid && !hasNonZero && !fastMorphApplied)
            {
                return;
            }

            if (fastMorphCacheValid && hasNonZero && MorphWeightsEqual(fastMorphWeights!, fastLastAppliedMorphWeights!))
            {
                return;
            }

            RefreshFastMorphFrame(fastMorphWeights!);
            // The native mmd-runtime (RuntimeInstance::expand_group_morphs) has already expanded group
            // morph weights into their member morphs, while leaving each group morph's own weight in the
            // array. Re-running group resolution here would distribute that residual group weight a SECOND
            // time and over-drive (roughly double) the member blend shapes. Flip morphs are NOT expanded by
            // the native runtime, so the applier still resolves those.
            MmdUnityFrameApplier.ApplyMorphs(Instance, fastMorphFrame!, groupMorphsResolvedExternally: true);
            Array.Copy(fastMorphWeights!, fastLastAppliedMorphWeights!, fastMorphWeights!.Length);
            fastMorphApplied = hasNonZero;
            fastMorphCacheValid = true;
        }

        private void RefreshFastMorphFrame(float[] weights)
        {
            List<MmdEvaluatedMorphWeight> morphList = fastMorphFrame!.morphs;
            for (int i = 0; i < morphList.Count; i++)
            {
                morphList[i].weight = i < weights.Length ? weights[i] : 0.0f;
            }
        }

        private MmdEvaluatedFrame BuildFastMorphFrame(float[] weights)
        {
            var morphList = new List<MmdEvaluatedMorphWeight>(model.morphs.Count);
            for (int i = 0; i < model.morphs.Count; i++)
            {
                morphList.Add(new MmdEvaluatedMorphWeight
                {
                    name = string.IsNullOrWhiteSpace(model.morphs[i].name)
                        ? model.morphs[i].index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : model.morphs[i].name,
                    weight = i < weights.Length ? weights[i] : 0.0f
                });
            }

            return new MmdEvaluatedFrame { morphs = morphList };
        }

        private MmdEvaluatedFrame BuildFastLivePhysicsFrame(int frame, float time)
        {
            var bones = new List<MmdEvaluatedBonePose>(model.bones.Count);
            foreach (MmdBoneDefinition bone in model.bones)
            {
                bones.Add(new MmdEvaluatedBonePose
                {
                    index = bone.index,
                    name = string.IsNullOrWhiteSpace(bone.name)
                        ? bone.index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : bone.name,
                    localPosition = new[] { 0.0f, 0.0f, 0.0f },
                    localRotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                    localScale = new[] { 1.0f, 1.0f, 1.0f },
                    worldMatrix = new[]
                    {
                        1.0f, 0.0f, 0.0f, 0.0f,
                        0.0f, 1.0f, 0.0f, 0.0f,
                        0.0f, 0.0f, 1.0f, 0.0f,
                        0.0f, 0.0f, 0.0f, 1.0f
                    }
                });
            }

            return new MmdEvaluatedFrame
            {
                frame = frame,
                time = time,
                bones = bones,
                morphs = BuildFastMorphFrame(fastMorphWeights!).morphs
            };
        }

        private static bool HasAnyNonZeroMorphWeight(float[] weights)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] != 0.0f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MorphWeightsEqual(float[] a, float[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
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
                evaluatedFrame = session.EvaluateFrame(frame, time);
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
            float deltaTime;
            MmdLivePhysicsPinnedBodyDiagnostics pinnedBodyDiagnostics;
            double syncBoneDrivenBodiesMs;
            double stepPhysicsMs;
            if (initializeDynamicBodies)
            {
                // saba PMXModel::ResetPhysics-style seed, UNIFIED for both the fast (native FFI) and the
                // managed evaluation paths. The bones are already at the CURRENT motion pose (ApplyFastFrame
                // for the fast path, MmdUnityFrameApplier.ApplyFrame for the managed path). The first live
                // frame after a reset places every body (including the pure-dynamic mode-1 揺れもの) at that
                // CURRENT bone-derived pose, re-aligns the native interpolation transform with it, and zeroes
                // velocity so the first forward Step computes NO spurious kinematic velocity. This is a settle
                // (deltaTime 0), not a forward integration, so it cannot explode the chain.
                deltaTime = 0.0f;
                pinnedBodyDiagnostics = SeedLivePhysics(backend, frame);
                syncBoneDrivenBodiesMs = stageWatch.Elapsed.TotalMilliseconds;
                stepPhysicsMs = 0.0;
            }
            else
            {
                deltaTime = (frame - lastLiveFrame) / frameRate;
                pinnedBodyDiagnostics = SyncBoneDrivenPhysicsBodies(backend, includeDynamicBodies: false);
                syncBoneDrivenBodiesMs = stageWatch.Elapsed.TotalMilliseconds;
                stageWatch.Restart();
                backend.Step(frame, deltaTime);
                stepPhysicsMs = stageWatch.Elapsed.TotalMilliseconds;
            }

            stageWatch.Restart();
            ApplyPhysicsBodyTransforms(backend);
            double applyPhysicsBodiesMs = stageWatch.Elapsed.TotalMilliseconds;
            stageWatch.Restart();
            ApplyPhysicsBodyDebugTransforms(backend);
            MmdLivePhysicsBodyDiagnostics[] bodyDiagnostics = BuildBodyDiagnostics(backend);
            if (evaluatedFrame != null)
            {
                RefreshEvaluatedFrameFromUnityTransforms(evaluatedFrame);
            }

            double refreshSnapshotFrameMs = stageWatch.Elapsed.TotalMilliseconds;
            lastLiveFrame = frame;
            lastLiveSnapshot = session.BuildSnapshotFromEvaluatedFrame(evaluatedFrame!, Instance.RenderingDescriptor);
            totalWatch.Stop();
            lastLivePhysicsDiagnostics = new MmdLivePhysicsFrameDiagnostics
            {
                frame = frame,
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
            return lastLiveSnapshot;
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
        ///   4. Re-pin the bone-driven (kinematic / dynamic-orientation) bodies at the current pose.
        /// This is a settle, not a sweep from bind, so a pure-dynamic body never snaps toward the origin-space
        /// bind while the model is animated far away (the reported "揺れ骨が BindPose の場所に戻る" bug).
        /// </summary>
        private MmdLivePhysicsPinnedBodyDiagnostics SeedLivePhysics(
            BulletMmdPhysicsBackend backend, int frame)
        {
            // 1. saba MMDRigidBody::ResetTransform: place EVERY body (including pure-dynamic mode-1) at its
            //    current bone-derived model-space pose, overriding the origin-bind that native Reset() set.
            MmdLivePhysicsPinnedBodyDiagnostics diagnostics =
                SyncBoneDrivenPhysicsBodies(backend, includeDynamicBodies: true);

            // 2. Re-align the native interpolation transform with the just-placed world transform and zero
            //    velocity so the upcoming step (and the first forward Step) computes no spurious kinematic
            //    velocity from the stale origin-bind interpolation transform left by native Reset().
            backend.SyncInterpolationAndZeroVelocity();

            // 3. saba ResetPhysics' physics->Update settle: a SINGLE short step relaxes the joints at the
            //    current pose. The rig stays at the current pose, so the bone-driven bodies stay pinned and
            //    the dynamics settle in place instead of being dragged toward the origin.
            backend.Step(frame, LivePhysicsSeedSettleSeconds);

            // 4. Re-pin the bone-driven (kinematic / dynamic-orientation) bodies at the current pose.
            return SyncBoneDrivenPhysicsBodies(backend, includeDynamicBodies: false);
        }

        private MmdLivePhysicsPinnedBodyDiagnostics SyncBoneDrivenPhysicsBodies(
            BulletMmdPhysicsBackend backend,
            bool includeDynamicBodies)
        {
            Transform root = Instance.Root.transform;
            var diagnostics = new MmdLivePhysicsPinnedBodyDiagnostics();
            for (int i = 0; i < model.physics.rigidbodies.Count; i++)
            {
                MmdRigidbodyDefinition body = model.physics.rigidbodies[i];
                bool isStatic = IsStaticPhysicsKind(body.physicsKind);
                bool isDynamicOrientation = IsDynamicWithBonePhysicsKind(body.physicsKind);
                bool isDynamic = IsDynamicPhysicsKind(body.physicsKind);
                if (!isStatic && !isDynamicOrientation && !(includeDynamicBodies && isDynamic))
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
                Vector3 boneModelPosition = ToMmdModelPosition(root.InverseTransformPoint(bone.position));
                Vector3 bodyOffset = ToMmdVector3(body.position) - GetBoneOrigin(body.boneIndex);
                Quaternion boneModelRotation = ToMmdModelRotation(Quaternion.Inverse(root.rotation) * bone.rotation);
                Quaternion bodyLocalRotation = ToMmdEulerRotation(body.rotation);
                Quaternion bodyModelRotation = boneModelRotation * bodyLocalRotation;
                if (isDynamicOrientation && !includeDynamicBodies)
                {
                    bodyModelRotation = ToMmdQuaternion(backend.GetRigidbodyTransform(i).rotation);
                }

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
                    bone.position = root.TransformPoint(ToUnityModelPosition(boneModelPosition));
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
            for (int i = 0; i < model.physics.rigidbodies.Count; i++)
            {
                MmdRigidbodyDefinition body = model.physics.rigidbodies[i];
                if (!physicsBodiesByIndex.TryGetValue(body.index, out MmdUnityPhysicsBody physicsBody) ||
                    physicsBody == null)
                {
                    continue;
                }

                MmdPhysicsBodyTransform bodyTransform = backend.GetRigidbodyTransform(i);
                physicsBody.transform.position = root.TransformPoint(ToUnityModelPosition(bodyTransform.position));
                physicsBody.transform.rotation = root.rotation * ToUnityModelRotation(bodyTransform.rotation);
                physicsBody.RecordNativeTransform(bodyTransform.position, bodyTransform.rotation);
            }
        }

        private MmdLivePhysicsBodyDiagnostics[] BuildBodyDiagnostics(BulletMmdPhysicsBackend backend)
        {
            Transform root = Instance.Root.transform;
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
                    ? ToMmdModelPosition(root.InverseTransformPoint(bone.position))
                    : Vector3.zero;
                Vector3 readbackMmdPos = ToMmdVector3(bodyTransform.position);
                Quaternion readbackMmdRot = ToMmdQuaternion(bodyTransform.rotation);
                Vector3 readbackWorldPos = root.TransformPoint(ToUnityModelPosition(bodyTransform.position));
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

        private void RefreshEvaluatedFrameFromUnityTransforms(MmdEvaluatedFrame frame)
        {
            Transform root = Instance.Root.transform;
            foreach (MmdEvaluatedBonePose bonePose in frame.bones)
            {
                int index = bonePose.index;
                if (index < 0 || index >= Instance.BoneTransforms.Length)
                {
                    continue;
                }

                Transform bone = Instance.BoneTransforms[index];
                Vector3 localDelta = bone.localPosition - Instance.BindLocalPositions[index];
                Quaternion localRotation = Quaternion.Inverse(Instance.BindLocalRotations[index]) * bone.localRotation;
                bonePose.localPosition = ToArray(ToMmdModelPosition(localDelta));
                bonePose.localRotation = ToArray(ToMmdModelRotation(localRotation));
                bonePose.localScale = ToArray(bone.localScale);
                bonePose.worldMatrix = ToMmdModelMatrix(root, bone);
            }
        }

        private static Vector3 ToUnityModelPosition(float[] position)
        {
            return new Vector3(-position[0], position[1], -position[2]);
        }

        private static Vector3 ToUnityModelPosition(Vector3 position)
        {
            return new Vector3(-position.x, position.y, -position.z);
        }

        private static Quaternion ToUnityModelRotation(float[] rotation)
        {
            return new Quaternion(-rotation[0], rotation[1], -rotation[2], rotation[3]);
        }

        private static Quaternion ToUnityModelRotation(Quaternion rotation)
        {
            return new Quaternion(-rotation.x, rotation.y, -rotation.z, rotation.w);
        }

        private static Vector3 ToMmdModelPosition(Vector3 position)
        {
            return new Vector3(-position.x, position.y, -position.z);
        }

        private static Quaternion ToMmdModelRotation(Quaternion rotation)
        {
            return new Quaternion(-rotation.x, rotation.y, -rotation.z, rotation.w);
        }

        private static Quaternion ToMmdQuaternion(float[] rotation)
        {
            return new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
        }

        private static Quaternion ToMmdEulerRotation(float[] rotation)
        {
            if (rotation == null || rotation.Length < 3)
            {
                return Quaternion.identity;
            }

            Quaternion rotateX = Quaternion.AngleAxis(rotation[0] * Mathf.Rad2Deg, Vector3.right);
            Quaternion rotateY = Quaternion.AngleAxis(rotation[1] * Mathf.Rad2Deg, Vector3.up);
            Quaternion rotateZ = Quaternion.AngleAxis(rotation[2] * Mathf.Rad2Deg, Vector3.forward);
            return rotateZ * rotateY * rotateX;
        }

        private Vector3 GetBoneOrigin(int boneIndex)
        {
            for (int i = 0; i < model.bones.Count; i++)
            {
                MmdBoneDefinition bone = model.bones[i];
                if (bone.index == boneIndex)
                {
                    return ToMmdVector3(bone.origin);
                }
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

        private static Vector3 ToMmdVector3(float[] values)
        {
            if (values == null || values.Length < 3)
            {
                return Vector3.zero;
            }

            return new Vector3(values[0], values[1], values[2]);
        }

        private static float[] ToArray(Vector3 value)
        {
            return new[] { value.x, value.y, value.z };
        }

        private static float[] ToArray(Quaternion value)
        {
            return new[] { value.x, value.y, value.z, value.w };
        }

        private static float[] ToMmdModelMatrix(Transform root, Transform bone)
        {
            Vector3 position = ToMmdModelPosition(root.InverseTransformPoint(bone.position));
            Quaternion rotation = ToMmdModelRotation(Quaternion.Inverse(root.rotation) * bone.rotation);
            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            return new[]
            {
                matrix.m00, matrix.m01, matrix.m02, matrix.m03,
                matrix.m10, matrix.m11, matrix.m12, matrix.m13,
                matrix.m20, matrix.m21, matrix.m22, matrix.m23,
                matrix.m30, matrix.m31, matrix.m32, matrix.m33
            };
        }
    }

    [Serializable]
    public sealed class MmdLivePhysicsFrameDiagnostics
    {
        public int frame;
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
