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
        internal const int StatusOk = 0;
        internal const int StatusUnsupported = 2;
        internal const int StatusBufferTooSmall = 3;
        internal const uint ReductionTargetDccCubic = 2;

        internal const uint UnityCurveBoneLocalTranslation = 0;
        internal const uint UnityCurveBoneLocalEuler = 1;
        internal const uint UnityCurveMorphWeight = 2;
        internal const uint UnityCurveAxisNone = 3;

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

        [StructLayout(LayoutKind.Sequential)]
        internal struct ReductionTolerances
        {
            internal const float UnityPositionTolerance = 0.01f;
            internal const float RotationToleranceRadians = 0.005f;
            internal const float MorphWeightTolerance = 0.0001f;

            internal float localPosition;
            internal float localRotationRadians;
            internal float worldPosition;
            internal float worldRotationRadians;
            internal float morphWeight;

            internal static ReductionTolerances Default => new ReductionTolerances
            {
                localPosition = 1.0e-4f,
                localRotationRadians = 1.0e-4f,
                worldPosition = 1.0e-4f,
                worldRotationRadians = 1.0e-4f,
                morphWeight = 1.0e-4f
            };

            internal static ReductionTolerances ForUnityAnimationClip(float importScale)
            {
                if (!float.IsFinite(importScale) || importScale <= 0.0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(importScale));
                }

                float sourcePositionTolerance = UnityPositionTolerance / importScale;
                return new ReductionTolerances
                {
                    localPosition = sourcePositionTolerance,
                    localRotationRadians = RotationToleranceRadians,
                    worldPosition = sourcePositionTolerance,
                    worldRotationRadians = RotationToleranceRadians,
                    morphWeight = MorphWeightTolerance
                };
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct UnityCurveDescriptor
        {
            internal uint semantic;
            internal uint targetIndex;
            internal uint axis;
            internal IntPtr keyCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct UnityCurveKey
        {
            internal float timeSeconds;
            internal float value;
            internal float inTangent;
            internal float outTangent;
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

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_reduced_pose_create_from_dense", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReducedPoseCreateFromDense(
            IntPtr model,
            ulong modelIdentity,
            float[] worldMatricesF32,
            IntPtr worldMatricesF32Len,
            float[] morphWeightsF32,
            IntPtr morphWeightsF32Len,
            IntPtr frameCount,
            float startFrame,
            float frameStep,
            uint target,
            ReductionTolerances tolerances,
            out IntPtr reducedPose);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_reduced_pose_free", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ReducedPoseFree(IntPtr reducedPose);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_reduced_pose_unity_curve_count", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReducedPoseUnityCurveCount(
            IntPtr reducedPose,
            float framesPerSecond,
            [MarshalAs(UnmanagedType.I1)] bool flipZ,
            out IntPtr curveCount);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_reduced_pose_unity_curve_descriptor", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReducedPoseUnityCurveDescriptor(
            IntPtr reducedPose,
            float framesPerSecond,
            [MarshalAs(UnmanagedType.I1)] bool flipZ,
            IntPtr curveIndex,
            out UnityCurveDescriptor descriptor);

        [DllImport(LibraryName, EntryPoint = "mmd_runtime_reduced_pose_unity_curve_keys", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ReducedPoseUnityCurveKeys(
            IntPtr reducedPose,
            float framesPerSecond,
            [MarshalAs(UnmanagedType.I1)] bool flipZ,
            IntPtr curveIndex,
            [Out] UnityCurveKey[]? keys,
            IntPtr keyCapacity,
            out IntPtr requiredCount);

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
        internal const long MaxReductionInputBytes = 256L * 1024L * 1024L;

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

        internal MmdRuntimeReducedPose ReduceBatch(
            float batchStartFrame,
            int frameCount,
            uint workerCount,
            MmdRuntimeFfiMethods.ReductionTolerances tolerances)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeFfiPlaybackSession));
            }

            if (!float.IsFinite(batchStartFrame) || frameCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount));
            }

            ThrowIfReductionInputTooLarge(WorldMatrixFloatCount, MorphWeightCount, frameCount);
            var worldMatrices = new float[checked(WorldMatrixFloatCount * frameCount)];
            var morphWeights = new float[checked(MorphWeightCount * frameCount)];
            EvaluateBatch(batchStartFrame, 1.0f, frameCount, workerCount, worldMatrices, morphWeights);

            IntPtr reducedPose = IntPtr.Zero;
            int status = MmdRuntimeFfiMethods.ReducedPoseCreateFromDense(
                model,
                0,
                worldMatrices,
                new IntPtr(worldMatrices.Length),
                morphWeights,
                new IntPtr(morphWeights.Length),
                new IntPtr(frameCount),
                0.0f,
                1.0f,
                MmdRuntimeFfiMethods.ReductionTargetDccCubic,
                tolerances,
                out reducedPose);
            if (status != MmdRuntimeFfiMethods.StatusOk || reducedPose == IntPtr.Zero)
            {
                if (reducedPose != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ReducedPoseFree(reducedPose);
                }

                throw new InvalidOperationException(
                    "mmd-runtime reduced pose creation failed with status " + status + ": "
                    + MmdRuntimeFfiMarshal.LastErrorMessage());
            }

            return new MmdRuntimeReducedPose(reducedPose);
        }

        internal static void ThrowIfReductionInputTooLarge(
            int worldMatrixFloatCount,
            int morphWeightCount,
            int frameCount)
        {
            if (worldMatrixFloatCount < 0 || morphWeightCount < 0 || frameCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount));
            }

            long inputBytes = checked(
                ((long)worldMatrixFloatCount + morphWeightCount) * frameCount * sizeof(float));
            if (inputBytes > MaxReductionInputBytes)
            {
                throw new MmdRuntimeReductionInputTooLargeException(
                    "sparse reduction requires " + inputBytes
                    + " bytes of dense native input, exceeding the "
                    + MaxReductionInputBytes + " byte safety limit");
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

    internal sealed class MmdRuntimeReducedPose : IDisposable
    {
        private IntPtr handle;

        internal MmdRuntimeReducedPose(IntPtr handle)
        {
            this.handle = handle != IntPtr.Zero
                ? handle
                : throw new ArgumentException("Reduced pose handle is required.", nameof(handle));
        }

        internal int GetUnityCurveCount(float framesPerSecond, bool flipZ)
        {
            ThrowIfDisposed();
            int status = MmdRuntimeFfiMethods.ReducedPoseUnityCurveCount(
                handle, framesPerSecond, flipZ, out IntPtr count);
            ThrowForStatus(status, "curve count");
            return MmdFfiMarshal.CheckedIntPtrToInt(count, "reduced pose curve count");
        }

        internal MmdRuntimeFfiMethods.UnityCurveDescriptor GetUnityCurveDescriptor(
            float framesPerSecond,
            bool flipZ,
            int curveIndex)
        {
            ThrowIfDisposed();
            int status = MmdRuntimeFfiMethods.ReducedPoseUnityCurveDescriptor(
                handle,
                framesPerSecond,
                flipZ,
                new IntPtr(curveIndex),
                out MmdRuntimeFfiMethods.UnityCurveDescriptor descriptor);
            ThrowForStatus(status, "curve descriptor");
            return descriptor;
        }

        internal MmdRuntimeFfiMethods.UnityCurveKey[] GetUnityCurveKeys(
            float framesPerSecond,
            bool flipZ,
            int curveIndex)
        {
            ThrowIfDisposed();
            int status = MmdRuntimeFfiMethods.ReducedPoseUnityCurveKeys(
                handle,
                framesPerSecond,
                flipZ,
                new IntPtr(curveIndex),
                null,
                IntPtr.Zero,
                out IntPtr requiredCount);
            if (status != MmdRuntimeFfiMethods.StatusBufferTooSmall)
            {
                ThrowForStatus(status, "curve key count");
            }

            MmdRuntimeFfiMethods.UnityCurveKey[] keys = AllocateUnityCurveKeyBuffer(requiredCount);
            if (keys.Length == 0)
            {
                return keys;
            }

            status = MmdRuntimeFfiMethods.ReducedPoseUnityCurveKeys(
                handle,
                framesPerSecond,
                flipZ,
                new IntPtr(curveIndex),
                keys,
                new IntPtr(keys.Length),
                out IntPtr copiedCount);
            ThrowForStatus(status, "curve keys");
            if (copiedCount != requiredCount)
            {
                throw new InvalidOperationException("mmd-runtime reduced pose curve key count changed during enumeration.");
            }

            return keys;
        }

        internal static MmdRuntimeFfiMethods.UnityCurveKey[] AllocateUnityCurveKeyBuffer(
            IntPtr requiredCount)
        {
            int keyCount = MmdFfiMarshal.CheckedIntPtrToInt(
                requiredCount, "reduced pose curve key count");
            return keyCount == 0
                ? Array.Empty<MmdRuntimeFfiMethods.UnityCurveKey>()
                : new MmdRuntimeFfiMethods.UnityCurveKey[keyCount];
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            MmdRuntimeFfiMethods.ReducedPoseFree(handle);
            handle = IntPtr.Zero;
        }

        private void ThrowIfDisposed()
        {
            if (handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeReducedPose));
            }
        }

        private static void ThrowForStatus(int status, string operation)
        {
            if (status == MmdRuntimeFfiMethods.StatusOk)
            {
                return;
            }

            string message = "mmd-runtime reduced pose " + operation + " failed with status " + status + ": "
                             + MmdRuntimeFfiMarshal.LastErrorMessage();
            if (status == MmdRuntimeFfiMethods.StatusUnsupported)
            {
                throw new MmdRuntimeUnsupportedException(message);
            }

            throw new InvalidOperationException(message);
        }
    }

    internal sealed class MmdRuntimeUnsupportedException : Exception
    {
        internal MmdRuntimeUnsupportedException(string message) : base(message)
        {
        }
    }

    internal sealed class MmdRuntimeReductionInputTooLargeException : Exception
    {
        internal MmdRuntimeReductionInputTooLargeException(string message) : base(message)
        {
        }
    }

    internal static class MmdRuntimeFfiMarshal
    {
        internal static string LastErrorMessage()
        {
            IntPtr message = MmdRuntimeFfiMethods.LastErrorMessage();
            return message == IntPtr.Zero ? "no native diagnostic" : Marshal.PtrToStringAnsi(message) ?? "no native diagnostic";
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

