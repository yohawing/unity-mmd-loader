#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Mmd.Native
{
    internal static class MmdRuntimeFfiMethods
    {
        internal const string LibraryName = "mmd_runtime_ffi";
        internal const uint ExpectedAbiVersion = 2;
        internal const uint FeatureSplitPhysicsEvaluation = 1u << 0;
        internal const uint FeaturePhysicsBulletNative = 1u << 1;
        internal const uint PhysicsModeLive = 2;

        [StructLayout(LayoutKind.Sequential)]
        internal struct PhysicsStepStats
        {
            internal float inputDtSeconds;
            internal float clampedDtSeconds;
            internal uint substeps;
            internal float accumulatorSeconds;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PhysicsWorldStepReport
        {
            internal PhysicsStepStats tick;
            internal IntPtr kinematicRigidbodiesFed;
            internal IntPtr bonesWrittenBack;
        }

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_abi_version", CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint AbiVersion();

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_feature_flags", CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint FeatureFlags();

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_last_error_message", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr LastErrorMessage();

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_create_from_pmx_bytes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ModelCreateFromPmxBytes(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_bone_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ModelBoneCount(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_morph_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ModelMorphCount(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_ik_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ModelIkCount(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_model_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ModelFree(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_create_from_vmd_bytes_for_model", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ClipCreateFromVmdBytesForModel(IntPtr model, byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_camera_track_create_from_vmd_bytes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdCameraTrackCreateFromVmdBytes(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_camera_track_frame_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdCameraTrackFrameCount(IntPtr track);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_camera_track_sample", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte VmdCameraTrackSample(IntPtr track, float frame, [Out] float[] outF32, IntPtr outF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_light_track_sample", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte VmdLightTrackSample(IntPtr track, float frame, [Out] float[] outF32, IntPtr outF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_light_track_create_from_vmd_bytes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdLightTrackCreateFromVmdBytes(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_light_track_frame_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdLightTrackFrameCount(IntPtr track);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_light_track_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void VmdLightTrackFree(IntPtr track);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_self_shadow_track_sample", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte VmdSelfShadowTrackSample(IntPtr track, float frame, [Out] float[] outF32, IntPtr outF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_self_shadow_track_create_from_vmd_bytes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdSelfShadowTrackCreateFromVmdBytes(byte[] data, IntPtr len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_self_shadow_track_frame_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr VmdSelfShadowTrackFrameCount(IntPtr track);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_self_shadow_track_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void VmdSelfShadowTrackFree(IntPtr track);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_vmd_camera_track_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void VmdCameraTrackFree(IntPtr track);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_frame_range", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte ClipFrameRange(IntPtr clip, out uint firstFrame, out uint lastFrame);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_clip_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ClipFree(IntPtr clip);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_create_for_model", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceCreateForModel(IntPtr model);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void InstanceFree(IntPtr instance);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_set_physics_mode", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int InstanceSetPhysicsMode(IntPtr instance, uint mode);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_physics_world_create_from_pmx_bytes", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PhysicsWorldCreateFromPmxBytes(byte[] data, IntPtr len, out IntPtr world);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_physics_world_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void PhysicsWorldFree(IntPtr world);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_physics_world_reset", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PhysicsWorldReset(IntPtr world, IntPtr instance, out IntPtr seededRigidbodyCount);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_evaluate_clip_frame_before_physics", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int InstanceEvaluateClipFrameBeforePhysics(IntPtr instance, IntPtr clip, float frame);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_physics_world_step_runtime", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PhysicsWorldStepRuntime(
            IntPtr world,
            IntPtr instance,
            float deltaTime,
            out PhysicsWorldStepReport outReport);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_physics_world_rigidbody_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PhysicsWorldRigidbodyCount(IntPtr world, out IntPtr rigidbodyCount);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_physics_world_copy_rigidbody_states", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int PhysicsWorldCopyRigidbodyStates(IntPtr world, [Out] float[] outTransformsF32, IntPtr outTransformsF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_evaluate_clip_frame", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceEvaluateClipFrame(IntPtr instance, IntPtr clip, float frame);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_evaluate_clip_frame_without_ik", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceEvaluateClipFrameWithoutIk(IntPtr instance, IntPtr clip, float frame);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_world_matrix_f32_len", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceWorldMatrixF32Len(IntPtr instance);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_copy_world_matrices", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceCopyWorldMatrices(IntPtr instance, [Out] float[] outF32, IntPtr outF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_morph_weight_len", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceMorphWeightLen(IntPtr instance);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_copy_morph_weights", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceCopyMorphWeights(IntPtr instance, [Out] float[] outF32, IntPtr outF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_clip_frame_batch_world_matrix_f32_len", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceClipFrameBatchWorldMatrixF32Len(IntPtr instance, IntPtr frameCount);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_clip_frame_batch_morph_weight_f32_len", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceClipFrameBatchMorphWeightF32Len(IntPtr instance, IntPtr frameCount);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_evaluate_clip_frame_batch", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceEvaluateClipFrameBatch(
            IntPtr instance,
            IntPtr clip,
            float startFrame,
            float frameStep,
            IntPtr frameCount,
            uint workerCount,
            [Out] float[] outWorldMatricesF32,
            IntPtr outWorldMatricesF32Len,
            [Out] float[] outMorphWeightsF32,
            IntPtr outMorphWeightsF32Len);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_ik_enabled_len", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr InstanceIkEnabledLen(IntPtr instance);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_instance_copy_ik_enabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte InstanceCopyIkEnabled(IntPtr instance, [Out] byte[] outU8, IntPtr outU8Len);

        internal static uint ValidateAbiVersion()
        {
            uint abiVersion = AbiVersion();
            if (abiVersion != ExpectedAbiVersion)
            {
                throw new InvalidOperationException(
                    $"mmd-runtime ABI version {abiVersion} is not supported. Expected {ExpectedAbiVersion}.");
            }

            return abiVersion;
        }

    }
    internal sealed class MmdRuntimeFfiPlaybackSession : IDisposable
    {
        private readonly IntPtr model;
        private readonly IntPtr clip;
        private readonly IntPtr instance;
        private bool disposed;

        private MmdRuntimeFfiPlaybackSession(IntPtr model, IntPtr clip, IntPtr instance)
        {
            this.model = model;
            this.clip = clip;
            this.instance = instance;
            AbiVersion = MmdRuntimeFfiMethods.ExpectedAbiVersion;
            BoneCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelBoneCount(model), "bone count");
            MorphCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelMorphCount(model), "morph count");
            IkCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelIkCount(model), "IK count");
            WorldMatrixFloatCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceWorldMatrixF32Len(instance), "world matrix float count");
            MorphWeightCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceMorphWeightLen(instance), "morph weight count");
            IkEnabledCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceIkEnabledLen(instance), "IK enabled count");
        }

        public uint AbiVersion { get; }
        public int BoneCount { get; }
        public int MorphCount { get; }
        public int IkCount { get; }
        public int WorldMatrixFloatCount { get; }
        public int MorphWeightCount { get; }
        public int IkEnabledCount { get; }

        public static MmdRuntimeFfiPlaybackSession Create(byte[] pmxBytes, byte[] vmdBytes)
        {
            if (pmxBytes == null || pmxBytes.Length == 0)
            {
                throw new ArgumentException("PMX bytes are required.", nameof(pmxBytes));
            }

            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(vmdBytes));
            }

            IntPtr model = IntPtr.Zero;
            IntPtr clip = IntPtr.Zero;
            IntPtr instance = IntPtr.Zero;
            try
            {
                MmdRuntimeFfiMethods.ValidateAbiVersion();
                model = MmdRuntimeFfiMethods.ModelCreateFromPmxBytes(pmxBytes, new IntPtr(pmxBytes.Length));
                if (model == IntPtr.Zero)
                {
                    throw new InvalidOperationException("mmd-runtime PMX import returned a null model.");
                }

                clip = MmdRuntimeFfiMethods.ClipCreateFromVmdBytesForModel(model, vmdBytes, new IntPtr(vmdBytes.Length));
                if (clip == IntPtr.Zero)
                {
                    throw new InvalidOperationException("mmd-runtime VMD import returned a null clip.");
                }

                instance = MmdRuntimeFfiMethods.InstanceCreateForModel(model);
                if (instance == IntPtr.Zero)
                {
                    throw new InvalidOperationException("mmd-runtime instance creation returned null.");
                }

                MmdRuntimeFfiPlaybackSession session = new MmdRuntimeFfiPlaybackSession(model, clip, instance);
                model = IntPtr.Zero;
                clip = IntPtr.Zero;
                instance = IntPtr.Zero;
                return session;
            }
            finally
            {
                if (instance != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.InstanceFree(instance);
                }

                if (clip != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ClipFree(clip);
                }

                if (model != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ModelFree(model);
                }
            }
        }

        public void EvaluateAndCopy(float frame, float[] worldMatrices, float[] morphWeights, byte[] ikEnabled)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeFfiPlaybackSession));
            }

            MmdRuntimeFfiSmoke.EvaluateAndCopy(instance, clip, frame, worldMatrices, morphWeights, ikEnabled);
        }

        public void EvaluateWithoutIkAndCopy(float frame, float[] worldMatrices, float[] morphWeights, byte[] ikEnabled)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeFfiPlaybackSession));
            }

            MmdRuntimeFfiSmoke.EvaluateWithoutIkAndCopy(instance, clip, frame, worldMatrices, morphWeights, ikEnabled);
        }

        public void EvaluateBatch(
            float startFrame,
            float frameStep,
            int frameCount,
            uint workerCount,
            float[] worldMatrices,
            float[] morphWeights)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeFfiPlaybackSession));
            }

            if (!float.IsFinite(startFrame) || !float.IsFinite(frameStep))
            {
                throw new ArgumentOutOfRangeException(nameof(startFrame), "Batch frame inputs must be finite.");
            }

            if (frameCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount));
            }

            if (worldMatrices == null)
            {
                throw new ArgumentNullException(nameof(worldMatrices));
            }

            if (morphWeights == null)
            {
                throw new ArgumentNullException(nameof(morphWeights));
            }

            int requiredWorldCount = MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.InstanceClipFrameBatchWorldMatrixF32Len(instance, new IntPtr(frameCount)),
                "batch world matrix float count");
            int requiredMorphCount = MmdFfiMarshal.CheckedIntPtrToInt(
                MmdRuntimeFfiMethods.InstanceClipFrameBatchMorphWeightF32Len(instance, new IntPtr(frameCount)),
                "batch morph weight float count");
            if (worldMatrices.Length < requiredWorldCount)
            {
                throw new ArgumentException(
                    $"Batch world matrix buffer requires {requiredWorldCount} floats.", nameof(worldMatrices));
            }

            if (morphWeights.Length < requiredMorphCount)
            {
                throw new ArgumentException(
                    $"Batch morph weight buffer requires {requiredMorphCount} floats.", nameof(morphWeights));
            }

            if (MmdRuntimeFfiMethods.InstanceEvaluateClipFrameBatch(
                    instance,
                    clip,
                    startFrame,
                    frameStep,
                    new IntPtr(frameCount),
                    workerCount,
                    worldMatrices,
                    new IntPtr(worldMatrices.Length),
                    morphWeights,
                    new IntPtr(morphWeights.Length)) == 0)
            {
                throw new InvalidOperationException("mmd-runtime batch clip frame evaluation returned false.");
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            MmdRuntimeFfiMethods.InstanceFree(instance);
            MmdRuntimeFfiMethods.ClipFree(clip);
            MmdRuntimeFfiMethods.ModelFree(model);
            disposed = true;
        }
    }

    internal static class MmdRuntimeFfiSmoke
    {
        internal static void EvaluateAndCopy(
            IntPtr instance,
            IntPtr clip,
            float frame,
            float[] worldMatrices,
            float[] morphWeights,
            byte[] ikEnabled)
            => EvaluateAndCopyCore(instance, clip, frame, worldMatrices, morphWeights, ikEnabled, useIk: true);

        internal static void EvaluateWithoutIkAndCopy(
            IntPtr instance,
            IntPtr clip,
            float frame,
            float[] worldMatrices,
            float[] morphWeights,
            byte[] ikEnabled)
            => EvaluateAndCopyCore(instance, clip, frame, worldMatrices, morphWeights, ikEnabled, useIk: false);

        private static void EvaluateAndCopyCore(
            IntPtr instance,
            IntPtr clip,
            float frame,
            float[] worldMatrices,
            float[] morphWeights,
            byte[] ikEnabled,
            bool useIk)
        {
            byte evaluated = useIk
                ? MmdRuntimeFfiMethods.InstanceEvaluateClipFrame(instance, clip, frame)
                : MmdRuntimeFfiMethods.InstanceEvaluateClipFrameWithoutIk(instance, clip, frame);
            if (evaluated == 0)
            {
                throw new InvalidOperationException(useIk
                    ? "mmd-runtime clip frame evaluation returned false."
                    : "mmd-runtime clip frame without IK evaluation returned false.");
            }

            if (worldMatrices.Length > 0 &&
                MmdRuntimeFfiMethods.InstanceCopyWorldMatrices(instance, worldMatrices, new IntPtr(worldMatrices.Length)) == 0)
            {
                throw new InvalidOperationException("mmd-runtime world matrix copy returned false.");
            }

            if (morphWeights.Length > 0 &&
                MmdRuntimeFfiMethods.InstanceCopyMorphWeights(instance, morphWeights, new IntPtr(morphWeights.Length)) == 0)
            {
                throw new InvalidOperationException("mmd-runtime morph weight copy returned false.");
            }

            if (ikEnabled.Length > 0 &&
                MmdRuntimeFfiMethods.InstanceCopyIkEnabled(instance, ikEnabled, new IntPtr(ikEnabled.Length)) == 0)
            {
                throw new InvalidOperationException("mmd-runtime IK enabled copy returned false.");
            }
        }

    }
}

