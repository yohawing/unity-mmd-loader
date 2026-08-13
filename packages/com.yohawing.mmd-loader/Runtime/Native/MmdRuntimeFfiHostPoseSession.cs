#nullable enable
using System;
using System.Runtime.InteropServices;

namespace Mmd.Native
{
    internal sealed class MmdRuntimeFfiHostPoseSession : IDisposable
    {
        private IntPtr model;
        private IntPtr instance;
        private bool disposed;
        private MmdRuntimeFfiHostPoseSession(
            IntPtr model,
            IntPtr instance,
            int boneCount,
            int morphCount,
            int ikCount,
            int worldMatrixFloatCount)
        {
            this.model = model;
            this.instance = instance;
            BoneCount = boneCount;
            MorphCount = morphCount;
            IkCount = ikCount;
            WorldMatrixFloatCount = worldMatrixFloatCount;
        }

        internal int BoneCount { get; }
        internal int MorphCount { get; }
        internal int IkCount { get; }
        internal int WorldMatrixFloatCount { get; }
        internal static MmdRuntimeFfiHostPoseSession Create(byte[] pmxBytes)
        {
            if (pmxBytes == null || pmxBytes.Length == 0)
            {
                throw new ArgumentException("PMX bytes are required.", nameof(pmxBytes));
            }
            return MmdRuntimeNativeBoundary.Invoke(
                "native host-pose session",
                () => CreateNative(pmxBytes));
        }
        private static MmdRuntimeFfiHostPoseSession CreateNative(byte[] pmxBytes)
        {
            IntPtr model = IntPtr.Zero;
            IntPtr instance = IntPtr.Zero;
            try
            {
                MmdRuntimeFfiMethods.ValidateHostPoseCapability();
                model = MmdRuntimeFfiMethods.ModelCreateFromPmxBytes(
                    pmxBytes,
                    new IntPtr(pmxBytes.Length));
                if (model == IntPtr.Zero)
                {
                    throw new InvalidOperationException(
                        "mmd-runtime host-pose PMX import returned a null model: " +
                        MmdRuntimeFfiMarshal.LastErrorMessage());
                }
                int boneCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelBoneCount(model), "host-pose bone count");
                int morphCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelMorphCount(model), "host-pose morph count");
                int ikCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.ModelIkCount(model), "host-pose IK count");

                instance = MmdRuntimeFfiMethods.InstanceCreateForModel(model);
                if (instance == IntPtr.Zero)
                {
                    throw new InvalidOperationException(
                        "mmd-runtime host-pose instance creation returned null: " +
                        MmdRuntimeFfiMarshal.LastErrorMessage());
                }
                int instanceMorphCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceMorphWeightLen(instance), "host-pose instance morph count");
                int instanceIkCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceIkEnabledLen(instance), "host-pose instance IK count");
                if (instanceMorphCount != morphCount)
                {
                    throw new InvalidOperationException(
                        $"mmd-runtime host-pose morph count mismatch: model {morphCount}, instance {instanceMorphCount}.");
                }
                if (instanceIkCount != ikCount)
                {
                    throw new InvalidOperationException(
                        $"mmd-runtime host-pose IK count mismatch: model {ikCount}, instance {instanceIkCount}.");
                }

                int worldMatrixFloatCount = MmdFfiMarshal.CheckedIntPtrToInt(MmdRuntimeFfiMethods.InstanceWorldMatrixF32Len(instance), "host-pose world matrix float count");
                long expectedWorldMatrixFloatCount = checked((long)boneCount * 16L);
                if (expectedWorldMatrixFloatCount > int.MaxValue ||
                    worldMatrixFloatCount != expectedWorldMatrixFloatCount)
                {
                    throw new InvalidOperationException(
                        $"mmd-runtime host-pose world matrix count mismatch: expected {expectedWorldMatrixFloatCount}, native {worldMatrixFloatCount}.");
                }

                MmdRuntimeFfiHostPoseSession session = new MmdRuntimeFfiHostPoseSession(
                    model, instance, boneCount, morphCount, ikCount, worldMatrixFloatCount);
                model = IntPtr.Zero;
                instance = IntPtr.Zero;
                return session;
            }
            finally
            {
                if (instance != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.InstanceFree(instance);
                }

                if (model != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ModelFree(model);
                }
            }
        }

        internal void EvaluateAndCopy(
            float[] localPositionOffsetsXyz,
            float[] localRotationXyzw,
            float[] localScalesXyz,
            float[] morphWeights,
            byte[] ikEnabled,
            float[] worldMatrices)
        {
            ThrowIfDisposed();
            ValidateInputArrays(
                localPositionOffsetsXyz,
                localRotationXyzw,
                localScalesXyz,
                morphWeights,
                ikEnabled,
                worldMatrices);

            GCHandle positionHandle = default;
            GCHandle rotationHandle = default;
            GCHandle scaleHandle = default;
            GCHandle morphHandle = default;
            GCHandle ikHandle = default;
            try
            {
                var pose = new MmdRuntimeFfiMethods.PhysicsHostPoseView
                {
                    localPositionOffsetsXyz = Pin(localPositionOffsetsXyz, ref positionHandle),
                    localRotationXyzw = Pin(localRotationXyzw, ref rotationHandle),
                    localScalesXyz = Pin(localScalesXyz, ref scaleHandle),
                    boneCount = new IntPtr(BoneCount),
                    morphWeights = Pin(morphWeights, ref morphHandle),
                    morphCount = new IntPtr(MorphCount),
                    ikEnabled = Pin(ikEnabled, ref ikHandle),
                    ikCount = new IntPtr(IkCount)
                };

                int status = MmdRuntimeFfiMethods.InstanceApplyHostPoseAndEvaluateBeforePhysics(instance, ref pose);
                ThrowForStatus(status, "apply host pose and evaluate before physics");

                status = MmdRuntimeFfiMethods.InstanceEvaluateCurrentPoseAfterPhysics(instance);
                ThrowForStatus(status, "evaluate current pose after physics");

                if (worldMatrices.Length > 0 &&
                    MmdRuntimeFfiMethods.InstanceCopyWorldMatrices(instance, worldMatrices, new IntPtr(worldMatrices.Length)) == 0)
                {
                    throw new InvalidOperationException(
                        "mmd-runtime host-pose world matrix copy returned false: " + MmdRuntimeFfiMarshal.LastErrorMessage());
                }
            }
            finally
            {
                Free(ref ikHandle);
                Free(ref morphHandle);
                Free(ref scaleHandle);
                Free(ref rotationHandle);
                Free(ref positionHandle);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            IntPtr instanceHandle = instance;
            instance = IntPtr.Zero;
            IntPtr modelHandle = model;
            model = IntPtr.Zero;
            try
            {
                if (instanceHandle != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.InstanceFree(instanceHandle);
                }
            }
            finally
            {
                if (modelHandle != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ModelFree(modelHandle);
                }
            }
        }

        private void ValidateInputArrays(
            float[] localPositionOffsetsXyz,
            float[] localRotationXyzw,
            float[] localScalesXyz,
            float[] morphWeights,
            byte[] ikEnabled,
            float[] worldMatrices)
        {
            int expectedPositionCount = checked(BoneCount * 3);
            int expectedRotationCount = checked(BoneCount * 4);
            int expectedScaleCount = checked(BoneCount * 3);
            RequireExactLength(localPositionOffsetsXyz, expectedPositionCount, nameof(localPositionOffsetsXyz));
            RequireExactLength(localRotationXyzw, expectedRotationCount, nameof(localRotationXyzw));
            RequireExactLength(localScalesXyz, expectedScaleCount, nameof(localScalesXyz));
            RequireExactLength(morphWeights, MorphCount, nameof(morphWeights));
            RequireExactLength(ikEnabled, IkCount, nameof(ikEnabled));
            RequireExactLength(worldMatrices, WorldMatrixFloatCount, nameof(worldMatrices));

        }

        private static IntPtr Pin(Array values, ref GCHandle handle)
        {
            if (values.Length == 0)
            {
                return IntPtr.Zero;
            }

            handle = GCHandle.Alloc(values, GCHandleType.Pinned);
            return handle.AddrOfPinnedObject();
        }

        private static void Free(ref GCHandle handle)
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }

        private static void RequireExactLength<T>(T[]? values, int expected, string label)
        {
            if (values == null)
            {
                throw new ArgumentNullException(label);
            }

            if (values.Length != expected)
            {
                throw new ArgumentException(
                    label + " requires exactly " + expected + " elements, got " + values.Length + ".",
                    label);
            }
        }

        private static void ThrowForStatus(int status, string operation)
        {
            if (status == MmdRuntimeFfiMethods.StatusOk)
            {
                return;
            }

            throw new InvalidOperationException(
                "mmd-runtime host-pose " + operation + " failed with status " + status + ": " +
                MmdRuntimeFfiMarshal.LastErrorMessage());
        }

        private void ThrowIfDisposed()
        {
            if (disposed || instance == IntPtr.Zero || model == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(MmdRuntimeFfiHostPoseSession));
            }
        }
    }
}
