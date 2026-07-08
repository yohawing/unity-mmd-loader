#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Mmd;
using Mmd.Motion;
using Mmd.Native;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Pose;
using Mmd.Rendering;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackBinding : IDisposable
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
            MmdMotionDefinition motion = motionAsset.LoadMotion(parser);
            MmdMotionValidator.ThrowIfInvalid(motion);
            float importScale = modelAsset.ImportScale;
            return CreateSkinned(
                model,
                motion,
                string.IsNullOrWhiteSpace(modelAsset.SourceId) ? modelAsset.name : modelAsset.SourceId,
                string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId,
                string.IsNullOrWhiteSpace(modelAsset.SourcePath) ? null : modelAsset.SourcePath,
                importScale,
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
            ApplyMaterialOverrideToSuppliedInstance(instance, modelAsset.MaterialOverrideAsset, modelAsset.MaterialRemaps);
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

        private static void ApplyMaterialOverrideToSuppliedInstance(
            MmdUnityModelInstance instance,
            MmdMaterialOverrideAsset? materialOverride,
            Material[]? materialRemaps)
        {
            if (materialOverride == null)
            {
                return;
            }

            bool[] excludedMaterialSlots = BuildMaterialOverrideExclusionSlots(materialRemaps, instance.Materials.Length);
            Material[] sceneMaterials = CloneMaterialsForOverride(instance.Materials, excludedMaterialSlots);
            for (int i = 0; i < sceneMaterials.Length; i++)
            {
                instance.Materials[i] = sceneMaterials[i];
            }

            MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(materialOverride, instance.RenderingDescriptor, excludedMaterialSlots);
            MmdMaterialOverrideApplier.Apply(materialOverride, sceneMaterials, excludedMaterialSlots);
            if (instance.SkinnedMeshRenderer != null)
            {
                instance.SkinnedMeshRenderer.sharedMaterials = sceneMaterials;
            }
            else if (instance.MeshRenderer != null)
            {
                instance.MeshRenderer.sharedMaterials = sceneMaterials;
            }

            instance.RefreshMaterialBindingDiagnostics();
        }

        private static Material[] CloneMaterialsForOverride(Material[] materials, bool[] excludedMaterialSlots)
        {
            if (materials == null || materials.Length == 0)
            {
                return Array.Empty<Material>();
            }

            var clones = new Material[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null)
                {
                    continue;
                }

                if (IsExcludedMaterialSlot(i, excludedMaterialSlots))
                {
                    clones[i] = source;
                    continue;
                }

                Material clone = new Material(source)
                {
                    name = source.name
                };
                clones[i] = clone;
            }

            return clones;
        }

        private static bool[] BuildMaterialOverrideExclusionSlots(Material[]? materialRemaps, int materialSlotCount)
        {
            var excluded = new bool[Math.Max(0, materialSlotCount)];
            if (materialRemaps == null)
            {
                return excluded;
            }

            int count = Math.Min(excluded.Length, materialRemaps.Length);
            for (int i = 0; i < count; i++)
            {
                excluded[i] = materialRemaps[i] != null;
            }

            return excluded;
        }

        private static bool IsExcludedMaterialSlot(int materialSlot, bool[] excludedMaterialSlots)
        {
            return materialSlot >= 0 &&
                materialSlot < excludedMaterialSlots.Length &&
                excludedMaterialSlots[materialSlot];
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
                importScale,
                MmdPmxModelPresetPolicy.AllowsAutomaticSelfShadowTarget(modelAsset.ModelPreset),
                modelAsset.MaterialOverrideAsset);
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
            MmdUnityModelInstance instance = MmdUnityModelFactory.CreateExistingSkinnedModelInstance(
                root,
                model,
                string.IsNullOrWhiteSpace(modelAsset.SourcePath) ? null : modelAsset.SourcePath,
                importScale,
                MmdPmxModelPresetPolicy.AllowsAutomaticSelfShadowTarget(modelAsset.ModelPreset),
                modelAsset.MaterialOverrideAsset);
            string resolvedModelId = string.IsNullOrWhiteSpace(modelAsset.SourceId) ? modelAsset.name : modelAsset.SourceId;
            string resolvedMotionId = string.IsNullOrWhiteSpace(motionId) ? "humanoid-physics" : motionId;
            var session = new MmdRuntimeSession(model, restMotion, resolvedModelId, resolvedMotionId);
            return new MmdUnityPlaybackBinding(instance, session, model, resolvedModelId, resolvedMotionId);
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
            var session = new MmdRuntimeSession(model, motion, modelId, motionId);
            return new MmdUnityPlaybackBinding(instance, session, model, modelId, motionId);
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

        public void Dispose()
        {
            DisableFastRuntime();
            ResetLivePhysics();
        }
    }
}