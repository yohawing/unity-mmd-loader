#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Mmd;
using Mmd.Motion;
using Mmd.Native;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Rendering;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    internal enum MmdUnityModelInstanceOwnership
    {
        Borrowed = 0,
        Owned = 1
    }

    public sealed partial class MmdUnityPlaybackBinding : IDisposable
    {
        private readonly MmdRuntimeSession session;
        private readonly MmdModelDefinition model;
        private readonly string modelId;
        private readonly string motionId;
        private IMmdLivePhysicsBackend? livePhysicsBackend;
        private int lastLiveFrame = -1;
        // Tracks the last frame seen by forward Timeline playback so a backward jump (scrub/seek) can be
        // distinguished from continuous forward play. Persists across ResetLivePhysics during a scrub so
        // a continuous backward drag keeps being treated as a scrub.
        private int lastForwardPlaybackFrame = -1;
        private MmdPlaybackSnapshot? lastLiveSnapshot;
        private MmdLivePhysicsFrameDiagnostics? lastLivePhysicsDiagnostics;
        private MmdLivePhysicsBodyDiagnostics[] lastLivePhysicsBodyDiagnostics = Array.Empty<MmdLivePhysicsBodyDiagnostics>();
        private int lastLivePhysicsBodyDiagnosticsFrame = -1;
        private int livePhysicsReadbackTransformCount;
        private int livePhysicsReadbackShapeTypeCount;
        private MmdPhysicsMode physicsMode = MmdPhysicsMode.Off;
        private int ikMaxIterationsCap;
        private MmdRuntimeFfiPlaybackSession? fastSession;
        private float[]? fastWorldMatrices;
        private float[]? fastMorphWeights;
        private byte[]? fastIkEnabled;
        private MmdEvaluatedFrame? fastMorphFrame;
        private float[]? fastLastAppliedMorphWeights;
        private MmdEvaluatedFrame? fastLivePhysicsFrame;
        private bool fastMorphApplied;
        private bool fastMorphCacheValid;
        private MmdPlaybackSnapshot? fastSnapshot;
        private readonly int[] fastPoseBoneIndices;
        private readonly int[] fastMorphIndices;
        private readonly int[] fastAfterPhysicsBoneIndices;
        private float[]? livePhysicsLocalPositionOffsets;
        private float[]? livePhysicsLocalRotations;
        private float[]? livePhysicsLocalScales;
        private float[]? livePhysicsMorphWeights;
        private byte[]? livePhysicsIkEnabled;
        private float[]? livePhysicsNativeWorldMatrices;
        private float[]? livePhysicsNativeMorphWeights;
        private byte[]? livePhysicsNativeIkEnabled;
        private float[]? livePhysicsRowMajorMatrices;
        private float[]? livePhysicsLocalMatrixScratch;
        private MmdEvaluatedFrame? livePhysicsFrame;
        private MmdEvaluatedMorphWeight[]? livePhysicsMorphEntries;
        private int[]? livePhysicsMorphOrder;
        private readonly MmdBoneDefinition[] livePhysicsOrderedBones;
        private readonly MmdUnityModelInstanceOwnership instanceOwnership;
        private readonly MmdBorrowedSceneMutationLease? borrowedMutationLease;
        private readonly IDisposable? borrowedSourceMutation;
        private MmdUnityModelInstance playbackInstance;
        private bool disposed;

        private MmdUnityPlaybackBinding(
            MmdUnityModelInstance instance,
            MmdRuntimeSession session,
            MmdModelDefinition model,
            string modelId,
            string motionId,
            MmdUnityModelInstanceOwnership instanceOwnership,
            MmdBorrowedSceneMutationLease? borrowedMutationLease = null,
            IDisposable? borrowedSourceMutation = null)
        {
            Instance = instance;
            playbackInstance = instance;
            this.session = session;
            this.model = model;
            this.modelId = modelId;
            this.motionId = motionId;
            fastPoseBoneIndices = MmdUnityPlaybackWorkset.BuildBoneIndices(model, instance);
            fastMorphIndices = MmdUnityPlaybackWorkset.BuildMorphIndices(model, instance.RenderingDescriptor);
            fastAfterPhysicsBoneIndices = MmdUnityPlaybackWorkset.BuildAfterPhysicsBoneIndices(model);
            var orderedBones = new List<MmdBoneDefinition>(model.bones);
            orderedBones.Sort((left, right) => left.index.CompareTo(right.index));
            livePhysicsOrderedBones = orderedBones.ToArray();
            this.instanceOwnership = instanceOwnership;
            this.borrowedMutationLease = borrowedMutationLease;
            this.borrowedSourceMutation = borrowedSourceMutation;
        }

        public MmdUnityModelInstance Instance { get; }

        internal MmdUnityModelInstance PlaybackInstance => playbackInstance;

        public string MotionId => motionId;

        public MmdPhysicsMode PhysicsMode => physicsMode;

        /// <summary>
        /// Optional per-chain IK iteration ceiling. Zero preserves authored PMX iterations.
        /// Positive values are supported for VMD Physics Off and Humanoid Live host-pose evaluation;
        /// VMD Live fails closed because its native seed and after-physics ABI has no IK-options variant.
        /// </summary>
        public int IkMaxIterationsCap
        {
            get => ikMaxIterationsCap;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "IK maximum iterations cap must be non-negative; zero preserves authored PMX iteration counts.");
                }

                ikMaxIterationsCap = value;
            }
        }

        public MmdLivePhysicsFrameDiagnostics? LastLivePhysicsDiagnostics => lastLivePhysicsDiagnostics;

        /// <summary>
        /// Controls detailed per-body Live physics diagnostics. Zero disables sampling; positive values sample
        /// only frames divisible by the interval. Summary diagnostics remain available every frame.
        /// </summary>
        public int LivePhysicsBodyDiagnosticsSampleInterval
        {
            get => livePhysicsBodyDiagnosticsSampleInterval;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Live physics body diagnostics sample interval must be non-negative.");
                }

                livePhysicsBodyDiagnosticsSampleInterval = value;
            }
        }

        public bool IsFastRuntimeEnabled => fastSession != null;

        internal int FastRuntimePoseBoneCount => fastPoseBoneIndices.Length;

        internal int FastRuntimeMorphCount => fastMorphIndices.Length;

        internal int FastRuntimeAfterPhysicsBoneCount => fastAfterPhysicsBoneIndices.Length;

        internal MmdEvaluatedFrame? FastLivePhysicsFrameForTests => fastLivePhysicsFrame;

        internal MmdEvaluatedFrame? LivePhysicsFrameForTests => livePhysicsFrame;

        internal float[]? LivePhysicsNativeWorldMatricesForTests => livePhysicsNativeWorldMatrices;

        public int MotionMaxFrame => session.MotionMaxFrame;

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
            MmdMotionDefinition motion = motionAsset.CreateNativeClipMotionHeader();
            MmdMotionValidator.ThrowIfInvalid(motion);
            float importScale = modelAsset.ImportScale;
            return CreateSkinned(
                model,
                motion,
                string.IsNullOrWhiteSpace(modelAsset.SourceId) ? modelAsset.name : modelAsset.SourceId,
                string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId,
                string.IsNullOrWhiteSpace(modelAsset.SourcePath) ? null : modelAsset.SourcePath,
                importScale,
                ResolveMaterialPreset(modelAsset),
                modelAsset.MaterialOverrideAsset);
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

            MmdMotionDefinition motion = motionAsset.CreateNativeClipMotionHeader();
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
            var lease = new MmdBorrowedSceneMutationLease(
                instance,
                modelAsset.MaterialOverrideAsset,
                modelAsset.MaterialRemaps,
                importedMaterials: modelAsset.ImportedMaterials);
            return new MmdUnityPlaybackBinding(
                instance,
                session,
                model,
                string.IsNullOrWhiteSpace(modelAsset.SourceId) ? modelAsset.name : modelAsset.SourceId,
                string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId,
                MmdUnityModelInstanceOwnership.Borrowed,
                lease);
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
            var lease = new MmdBorrowedSceneMutationLease(instance);
            return new MmdUnityPlaybackBinding(
                instance,
                session,
                model,
                resolvedModelId,
                resolvedMotionId,
                MmdUnityModelInstanceOwnership.Borrowed,
                lease);
        }

        internal static MmdUnityPlaybackBinding CreateSkinnedFromSuppliedInstance(
            MmdUnityModelInstance instance,
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            string modelId,
            string motionId)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            MmdMotionValidator.ThrowIfInvalid(motion);
            string resolvedModelId = string.IsNullOrWhiteSpace(modelId) ? "PMX" : modelId;
            string resolvedMotionId = string.IsNullOrWhiteSpace(motionId) ? "VMD" : motionId;
            var session = new MmdRuntimeSession(model, motion, resolvedModelId, resolvedMotionId);
            var lease = new MmdBorrowedSceneMutationLease(instance);
            return new MmdUnityPlaybackBinding(
                instance,
                session,
                model,
                resolvedModelId,
                resolvedMotionId,
                MmdUnityModelInstanceOwnership.Borrowed,
                lease);
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
            return CreateSkinnedFromExistingSceneModel(
                root,
                modelAsset,
                model,
                motionAsset,
                motion);
        }

        internal static MmdUnityPlaybackBinding CreateSkinnedFromExistingSceneModel(
            GameObject root,
            MmdPmxAsset modelAsset,
            MmdModelDefinition model,
            MmdVmdAsset motionAsset,
            MmdMotionDefinition motion)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            return CreateSkinnedFromExistingSceneModel(
                root,
                model,
                motion,
                string.IsNullOrWhiteSpace(modelAsset.SourceId) ? modelAsset.name : modelAsset.SourceId,
                string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId,
                string.IsNullOrWhiteSpace(modelAsset.SourcePath) ? null : modelAsset.SourcePath,
                modelAsset.ImportScale,
                MmdPmxModelPresetPolicy.AllowsAutomaticSelfShadowTarget(modelAsset.ModelPreset),
                modelAsset.MaterialOverrideAsset,
                modelAsset.MaterialRemaps,
                ResolveMaterialPreset(modelAsset),
                modelAsset.ImportedMaterials);
        }

        internal static MmdUnityPlaybackBinding CreateSkinnedFromExistingSceneModel(
            GameObject root,
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            string modelId,
            string motionId,
            string? sourcePath = null,
            float importScale = 1.0f,
            bool includeSelfShadowTarget = true,
            MmdMaterialOverrideAsset? materialOverride = null,
            Material[]? materialRemaps = null,
            MmdMaterialPreset materialPreset = MmdMaterialPreset.MmdToon,
            Material[]? importedMaterials = null)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            MmdMotionValidator.ThrowIfInvalid(motion);
            string resolvedModelId = string.IsNullOrWhiteSpace(modelId) ? "PMX" : modelId;
            string resolvedMotionId = string.IsNullOrWhiteSpace(motionId) ? "VMD" : motionId;
            var sourceMutation = new MmdExistingSceneRebindLease(root);
            MmdUnityModelInstance? instance = null;
            try
            {
                instance = MmdUnityModelFactory.CreateExistingSkinnedModelInstance(
                    root,
                    model,
                    sourcePath,
                    importScale,
                    includeSelfShadowTarget,
                    materialOverride: null,
                    preserveExistingSelfShadowTarget: true,
                    materialPreset: materialPreset);
                sourceMutation.AdoptFactoryResult(instance);
                var session = new MmdRuntimeSession(model, motion, resolvedModelId, resolvedMotionId);
                var playbackMutation = new MmdBorrowedSceneMutationLease(
                    instance,
                    materialOverride,
                    materialRemaps,
                    importedMaterials: importedMaterials,
                    reuseRenderingDescriptor: true);
                return new MmdUnityPlaybackBinding(
                    instance,
                    session,
                    model,
                    resolvedModelId,
                    resolvedMotionId,
                    MmdUnityModelInstanceOwnership.Borrowed,
                    playbackMutation,
                    sourceMutation);
            }
            catch
            {
                if (instance == null)
                {
                    sourceMutation.RollbackFactoryFailure();
                }
                else
                {
                    sourceMutation.Dispose();
                }

                throw;
            }
        }

        internal static MmdUnityPlaybackBinding CreateSkinnedForModelOnlyPhysicsFromExistingSceneModel(
            GameObject root,
            MmdPmxAsset modelAsset,
            string motionId)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (modelAsset == null)
            {
                throw new ArgumentNullException(nameof(modelAsset));
            }

            var parser = new NativeMmdParser();
            MmdModelDefinition model = modelAsset.LoadModel(parser);
            MmdModelValidator.ThrowIfInvalid(model);
            RemoveModelOnlyUnsupportedPureWorldAnchorJoints(model);
            MmdMotionDefinition restMotion = CreateModelOnlyRestMotion(model);
            MmdMotionValidator.ThrowIfInvalid(restMotion);
            float importScale = modelAsset.ImportScale;
            string resolvedModelId = string.IsNullOrWhiteSpace(modelAsset.SourceId) ? modelAsset.name : modelAsset.SourceId;
            string resolvedMotionId = string.IsNullOrWhiteSpace(motionId) ? "humanoid-physics" : motionId;
            var sourceMutation = new MmdExistingSceneRebindLease(root);
            MmdUnityModelInstance? instance = null;
            try
            {
                instance = MmdUnityModelFactory.CreateExistingSkinnedModelInstance(
                    root,
                    model,
                    string.IsNullOrWhiteSpace(modelAsset.SourcePath) ? null : modelAsset.SourcePath,
                    importScale,
                    MmdPmxModelPresetPolicy.AllowsAutomaticSelfShadowTarget(modelAsset.ModelPreset),
                    materialOverride: null,
                    preserveExistingSelfShadowTarget: true,
                    materialPreset: ResolveMaterialPreset(modelAsset));
                sourceMutation.AdoptFactoryResult(instance);
                var session = new MmdRuntimeSession(model, restMotion, resolvedModelId, resolvedMotionId);
                var playbackMutation = new MmdBorrowedSceneMutationLease(
                    instance,
                    modelAsset.MaterialOverrideAsset,
                    modelAsset.MaterialRemaps,
                    importedMaterials: modelAsset.ImportedMaterials,
                    reuseRenderingDescriptor: true);
                return new MmdUnityPlaybackBinding(
                    instance,
                    session,
                    model,
                    resolvedModelId,
                    resolvedMotionId,
                    MmdUnityModelInstanceOwnership.Borrowed,
                    playbackMutation,
                    sourceMutation);
            }
            catch
            {
                if (instance == null)
                {
                    sourceMutation.RollbackFactoryFailure();
                }
                else
                {
                    sourceMutation.Dispose();
                }

                throw;
            }
        }

        private static void RemoveModelOnlyUnsupportedPureWorldAnchorJoints(MmdModelDefinition model)
        {
            model.physics?.joints?.RemoveAll(joint => joint.rigidbodyAIndex < 0 && joint.rigidbodyBIndex < 0);
        }

        public static MmdUnityPlaybackBinding CreateSkinned(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            string modelId,
            string motionId,
            string? sourcePath = null,
            MmdMaterialPreset materialPreset = MmdMaterialPreset.MmdToon)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            MmdUnityModelInstance instance = MmdUnityModelFactory.CreateSkinnedModel(
                model,
                sourcePath,
                importScale: 1.0f,
                materialPreset);
            return CreateOwnedBinding(instance, model, motion, modelId, motionId);
        }

        private static MmdMotionDefinition CreateModelOnlyRestMotion(MmdModelDefinition model)
        {
            var motion = new MmdMotionDefinition
            {
                targetModelName = model.name ?? string.Empty,
                maxFrame = 0,
                boneKeyframes = new List<MmdBoneKeyframeDefinition>(),
                morphKeyframes = new List<MmdMorphKeyframeDefinition>(),
                modelKeyframes = new List<MmdModelKeyframeDefinition>()
            };
            motion.sourceBytes = BuildMinimalVmdBytes(model.name ?? string.Empty);
            return motion;
        }

        private static byte[] BuildMinimalVmdBytes(string modelName)
        {
            byte[] bytes = new byte[66];
            byte[] header = Encoding.ASCII.GetBytes("Vocaloid Motion Data 0002");
            Array.Copy(header, 0, bytes, 0, Math.Min(header.Length, 30));
            byte[] name = Encoding.GetEncoding(932).GetBytes(modelName);
            Array.Copy(name, 0, bytes, 30, Math.Min(name.Length, 20));
            return bytes;
        }

        public static MmdUnityPlaybackBinding CreateSkinned(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            string modelId,
            string motionId,
            string? sourcePath,
            float importScale,
            MmdMaterialOverrideAsset? materialOverride = null)
        {
            return CreateSkinned(
                model,
                motion,
                modelId,
                motionId,
                sourcePath,
                importScale,
                MmdMaterialPreset.MmdToon,
                materialOverride);
        }

        public static MmdUnityPlaybackBinding CreateSkinned(
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            string modelId,
            string motionId,
            string? sourcePath,
            float importScale,
            MmdMaterialPreset materialPreset,
            MmdMaterialOverrideAsset? materialOverride = null)
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
            MmdUnityModelInstance instance = MmdUnityModelFactory.CreateSkinnedModel(
                model,
                sourcePath,
                scale,
                materialPreset,
                materialOverride);
            return CreateOwnedBinding(instance, model, motion, modelId, motionId);
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

            EnsureBorrowedMutationActive();
            physicsMode = mode;
            ResetLivePhysics();
        }

        public MmdPlaybackSnapshot ApplyFrame(int frame, float frameRate)
        {
            MmdPlaybackTime.ValidateFrame(frame);
            MmdPlaybackTime.ValidateFrameRate(frameRate);
            EnsureBorrowedMutationActive();
            if (physicsMode == MmdPhysicsMode.Live)
            {
                return ApplyLivePhysicsFrame(frame, frameRate, allowArbitraryStart: false);
            }

            if (fastSession != null)
            {
                return ApplyFastFrame(frame, frameRate);
            }

            MmdEvaluatedFrame evaluatedFrame = session.EvaluateFrame(
                frame,
                MmdPlaybackTime.ToTime(frame, frameRate),
                (uint)ikMaxIterationsCap);
            MmdUnityFrameApplier.ApplyFrame(playbackInstance, evaluatedFrame);
            return session.BuildSnapshotFromEvaluatedFrame(evaluatedFrame, playbackInstance.RenderingDescriptor);
        }

        public MmdPlaybackSnapshot ApplyTime(float time, float frameRate)
        {
            MmdPlaybackTime.ValidateTime(time);
            MmdPlaybackTime.ValidateFrameRate(frameRate);
            if (physicsMode == MmdPhysicsMode.Live)
            {
                throw new InvalidOperationException("Physics Live does not support random-access time evaluation. Use forward playback Tick/ApplyFrame.");
            }

            EnsureBorrowedMutationActive();
            return ApplyTimeCore(time, frameRate);
        }

        internal MmdPlaybackSnapshot ApplyTimelinePreviewTime(float time, float frameRate)
        {
            MmdPlaybackTime.ValidateTime(time);
            MmdPlaybackTime.ValidateFrameRate(frameRate);
            EnsureBorrowedMutationActive();

            if (physicsMode == MmdPhysicsMode.Live)
            {
                PrepareLivePhysicsForRandomAccessEvaluation();
            }

            return ApplyTimeCore(time, frameRate);
        }

        private MmdPlaybackSnapshot ApplyTimeCore(float time, float frameRate)
        {
            if (fastSession != null)
            {
                return ApplyFastTime(time, frameRate);
            }

            MmdEvaluatedFrame evaluatedFrame = session.EvaluateFrameAtTime(
                time,
                frameRate,
                (uint)ikMaxIterationsCap);
            MmdUnityFrameApplier.ApplyFrame(playbackInstance, evaluatedFrame);
            InvalidateFastMorphCache();
            return session.BuildSnapshotFromEvaluatedFrame(evaluatedFrame, playbackInstance.RenderingDescriptor);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                DisableFastRuntime();
                ResetLivePhysics();
            }
            finally
            {
                try
                {
                    if (instanceOwnership == MmdUnityModelInstanceOwnership.Owned)
                    {
                        DestroyOwnedInstance(playbackInstance);
                    }
                    else
                    {
                        try
                        {
                            borrowedMutationLease?.Dispose();
                        }
                        finally
                        {
                            borrowedSourceMutation?.Dispose();
                            playbackInstance = Instance;
                        }
                    }
                }
                finally
                {
                    session.Dispose();
                }
            }
        }

        private void EnsureBorrowedMutationActive()
        {
            if (borrowedMutationLease != null && !borrowedMutationLease.IsActive)
            {
                playbackInstance = borrowedMutationLease.Activate();
            }
        }

        private static MmdMaterialPreset ResolveMaterialPreset(MmdPmxAsset modelAsset)
        {
            string shaderPreset = modelAsset.ShaderPreset ?? string.Empty;
            if (string.Equals(shaderPreset, "URP Lit", StringComparison.Ordinal) ||
                string.Equals(shaderPreset, "UrpLit", StringComparison.Ordinal))
            {
                return MmdMaterialPreset.UrpLit;
            }

            if (string.Equals(shaderPreset, "MMD URP Toon", StringComparison.Ordinal) ||
                string.Equals(shaderPreset, "Mmd Toon Lit", StringComparison.Ordinal) ||
                string.Equals(shaderPreset, "MmdToonLit", StringComparison.Ordinal))
            {
                return MmdMaterialPreset.MmdToonLit;
            }

            // MMD Basic Toon and custom profile summaries use the compatibility-safe built-in
            // preset when no runtime mapper set is available at this boundary.
            return MmdMaterialPreset.MmdToon;
        }

        private static MmdUnityPlaybackBinding CreateOwnedBinding(
            MmdUnityModelInstance instance,
            MmdModelDefinition model,
            MmdMotionDefinition motion,
            string modelId,
            string motionId)
        {
            try
            {
                var session = new MmdRuntimeSession(model, motion, modelId, motionId);
                return new MmdUnityPlaybackBinding(
                    instance,
                    session,
                    model,
                    modelId,
                    motionId,
                    MmdUnityModelInstanceOwnership.Owned);
            }
            catch
            {
                DestroyOwnedInstance(instance);
                throw;
            }
        }

        private static void DestroyOwnedInstance(MmdUnityModelInstance instance)
        {
            if (instance.SkinnedMeshRenderer != null)
            {
                instance.SkinnedMeshRenderer.sharedMesh = null;
                instance.SkinnedMeshRenderer.sharedMaterials = Array.Empty<Material>();
            }
            else if (instance.MeshRenderer != null)
            {
                instance.MeshRenderer.sharedMaterials = Array.Empty<Material>();
            }

            var destroyedIds = new HashSet<int>();
            foreach (Material material in instance.Materials)
            {
                DestroyOwnedObjectOnce(material, destroyedIds);
            }

            foreach (Texture2D texture in instance.OwnedTextures)
            {
                DestroyOwnedObjectOnce(texture, destroyedIds);
            }

            DestroyOwnedObjectOnce(instance.Mesh, destroyedIds);
            DestroyOwnedObjectOnce(instance.Root, destroyedIds);
        }

        private static void DestroyOwnedObjectOnce(UnityEngine.Object? value, HashSet<int> destroyedIds)
        {
            if (value == null || !destroyedIds.Add(value.GetInstanceID()))
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(value);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}
