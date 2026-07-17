#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Mmd.Native;
using Mmd.Parser;

namespace Mmd.Physics
{
    internal interface IMmdLivePhysicsBackend : IDisposable
    {
        string Name { get; }

        int SkippedWorldAnchorJointCount { get; }

        void InitializeWorld(MmdModelDefinition model);

        void SetAnimationFrame(int frame);

        void SetRigidbodyTransform(int bodyIndex, float[] position, float[] rotation);

        MmdPhysicsBodyTransform GetRigidbodyTransform(int bodyIndex);

        string GetRigidbodyShapeType(int bodyIndex);

        void SyncInterpolationAndZeroVelocity();

        void Reset();

        void Step(int frame, float deltaTime);
    }

    /// <summary>
    /// Unity-side adapter for the feature-gated mmd-anim Bullet runtime ABI.
    /// The adapter owns a runtime instance and a PMX-created physics world and
    /// evaluates one VMD clip frame through mmd-anim's before-physics boundary and
    /// advances the native Bullet world without re-evaluating an already-applied
    /// Unity pose.
    /// </summary>
    internal sealed class MmdAnimPhysicsBackend : IMmdLivePhysicsBackend
    {
        private const int TransformFloatCount = 7;
        private readonly string modelId;
        private readonly string motionId;
        private readonly IntPtr model;
        private readonly IntPtr clip;
        private readonly IntPtr instance;
        private readonly IntPtr world;
        private float[] rigidbodyStates;
        private string[] rigidbodyShapeTypes = Array.Empty<string>();
        private bool seededSinceReset;
        private int pendingAnimationFrame = -1;
        private bool disposed;
        private int skippedWorldAnchorJointCount;

        private MmdAnimPhysicsBackend(byte[] pmxBytes, byte[] vmdBytes, string modelId, string motionId)
        {
            this.modelId = modelId ?? string.Empty;
            this.motionId = motionId ?? string.Empty;

            IntPtr createdModel = IntPtr.Zero;
            IntPtr createdClip = IntPtr.Zero;
            IntPtr createdInstance = IntPtr.Zero;
            IntPtr createdWorld = IntPtr.Zero;
            try
            {
                createdModel = MmdRuntimeFfiMethods.ModelCreateFromPmxBytes(pmxBytes, new IntPtr(pmxBytes.Length));
                if (createdModel == IntPtr.Zero)
                {
                    throw CreateNativeException("ModelCreateFromPmxBytes", 4);
                }

                createdClip = MmdRuntimeFfiMethods.ClipCreateFromVmdBytesForModel(
                    createdModel,
                    vmdBytes,
                    new IntPtr(vmdBytes.Length));
                if (createdClip == IntPtr.Zero)
                {
                    throw CreateNativeException("ClipCreateFromVmdBytesForModel", 4);
                }

                createdInstance = MmdRuntimeFfiMethods.InstanceCreateForModel(createdModel);
                if (createdInstance == IntPtr.Zero)
                {
                    throw CreateNativeException("InstanceCreateForModel", 4);
                }

                int createStatus = MmdRuntimeFfiMethods.PhysicsWorldCreateFromPmxBytes(
                    pmxBytes,
                    new IntPtr(pmxBytes.Length),
                    out createdWorld);
                ThrowIfFailed(createStatus, "PhysicsWorldCreateFromPmxBytes", this.modelId, this.motionId);
                if (createdWorld == IntPtr.Zero)
                {
                    throw CreateNativeException("PhysicsWorldCreateFromPmxBytes", 4);
                }

                int modeStatus = MmdRuntimeFfiMethods.InstanceSetPhysicsMode(
                    createdInstance,
                    MmdRuntimeFfiMethods.PhysicsModeLive);
                ThrowIfFailed(modeStatus, "InstanceSetPhysicsMode", this.modelId, this.motionId);

                model = createdModel;
                clip = createdClip;
                instance = createdInstance;
                world = createdWorld;
                rigidbodyStates = Array.Empty<float>();
                createdModel = IntPtr.Zero;
                createdClip = IntPtr.Zero;
                createdInstance = IntPtr.Zero;
                createdWorld = IntPtr.Zero;
            }
            finally
            {
                if (createdWorld != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.PhysicsWorldFree(createdWorld);
                }

                if (createdClip != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ClipFree(createdClip);
                }

                if (createdInstance != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.InstanceFree(createdInstance);
                }

                if (createdModel != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.ModelFree(createdModel);
                }
            }
        }

        public string Name => "mmd-anim-bullet-native";

        public int SkippedWorldAnchorJointCount => skippedWorldAnchorJointCount;

        internal static bool TryCreate(
            byte[]? pmxBytes,
            byte[]? vmdBytes,
            string modelId,
            string motionId,
            out MmdAnimPhysicsBackend? backend,
            out string reason)
        {
            backend = null;
            reason = string.Empty;
            if (pmxBytes == null || pmxBytes.Length == 0)
            {
                reason = "Model source bytes are unavailable.";
                return false;
            }

            try
            {
                MmdRuntimeFfiMethods.ValidateAbiVersion();
                uint featureFlags = MmdRuntimeFfiMethods.FeatureFlags();
                uint requiredFlags = MmdRuntimeFfiMethods.FeatureSplitPhysicsEvaluation |
                    MmdRuntimeFfiMethods.FeaturePhysicsBulletNative;
                if ((featureFlags & requiredFlags) != requiredFlags)
                {
                    reason = $"mmd-runtime physics host features are unavailable (flags=0x{featureFlags:X8}).";
                    return false;
                }

                if (vmdBytes == null || vmdBytes.Length == 0)
                {
                    reason = "Motion source bytes are unavailable.";
                    return false;
                }

                backend = new MmdAnimPhysicsBackend(pmxBytes, vmdBytes, modelId, motionId);
                return true;
            }
            catch (DllNotFoundException ex)
            {
                reason = ex.Message;
            }
            catch (EntryPointNotFoundException ex)
            {
                reason = ex.Message;
            }
            catch (BadImageFormatException ex)
            {
                reason = ex.Message;
            }
            catch (InvalidOperationException ex)
            {
                reason = ex.Message;
            }

            backend?.Dispose();
            backend = null;
            return false;
        }

        public void InitializeWorld(MmdModelDefinition modelDefinition)
        {
            ThrowIfDisposed();
            if (modelDefinition == null)
            {
                throw new ArgumentNullException(nameof(modelDefinition));
            }

            if (modelDefinition.physics == null)
            {
                throw new ArgumentException("Managed model physics definition is required.", nameof(modelDefinition));
            }

            MmdPhysicsDescriptorValidator.ThrowIfInvalid(modelDefinition);
            MmdPhysicsDefinition physics = modelDefinition.physics;
            IReadOnlyList<MmdRigidbodyDefinition> rigidbodies = physics.rigidbodies;

            int nativeRigidbodyCount = ReadRigidbodyCount();
            int managedRigidbodyCount = rigidbodies.Count;
            if (nativeRigidbodyCount != managedRigidbodyCount)
            {
                throw new MmdPhysicsBackendException(
                    "InitializeWorld",
                    Name,
                    "binding-mismatch",
                    $"Native rigidbody count {nativeRigidbodyCount} does not match managed count {managedRigidbodyCount}.",
                    modelId,
                    motionId);
            }

            rigidbodyShapeTypes = new string[nativeRigidbodyCount];
            for (int i = 0; i < nativeRigidbodyCount; i++)
            {
                rigidbodyShapeTypes[i] = rigidbodies[i].shapeType ?? string.Empty;
            }

            skippedWorldAnchorJointCount = 0;
            if (physics.joints != null)
            {
                foreach (MmdJointDefinition joint in physics.joints)
                {
                    if (joint.rigidbodyAIndex < 0 || joint.rigidbodyBIndex < 0)
                    {
                        skippedWorldAnchorJointCount++;
                    }
                }
            }

            Array.Resize(ref rigidbodyStates, checked(nativeRigidbodyCount * TransformFloatCount));
        }

        public void SetAnimationFrame(int frame)
        {
            ThrowIfDisposed();
            MmdPlaybackTime.ValidateFrame(frame);
            pendingAnimationFrame = frame;
        }

        public void SetRigidbodyTransform(int bodyIndex, float[] position, float[] rotation)
        {
            ThrowIfDisposed();
            ValidateBodyIndex(bodyIndex);
            if (position == null || position.Length != 3)
            {
                throw new ArgumentException("Rigidbody position must contain three values.", nameof(position));
            }

            if (rotation == null || rotation.Length != 4)
            {
                throw new ArgumentException("Rigidbody rotation must contain four values.", nameof(rotation));
            }
        }

        public MmdPhysicsBodyTransform GetRigidbodyTransform(int bodyIndex)
        {
            ThrowIfDisposed();
            ValidateBodyIndex(bodyIndex);
            int offset = checked(bodyIndex * TransformFloatCount);
            return new MmdPhysicsBodyTransform
            {
                position = new[] { rigidbodyStates[offset], rigidbodyStates[offset + 1], rigidbodyStates[offset + 2] },
                rotation = new[] { rigidbodyStates[offset + 3], rigidbodyStates[offset + 4], rigidbodyStates[offset + 5], rigidbodyStates[offset + 6] }
            };
        }

        public string GetRigidbodyShapeType(int bodyIndex)
        {
            ThrowIfDisposed();
            ValidateBodyIndex(bodyIndex);
            return bodyIndex < rigidbodyShapeTypes.Length ? rigidbodyShapeTypes[bodyIndex] : string.Empty;
        }

        public void SyncInterpolationAndZeroVelocity()
        {
            ThrowIfDisposed();
            // The mmd-anim runtime owns interpolation and velocity state.
        }

        public void Reset()
        {
            ThrowIfDisposed();
            int status = MmdRuntimeFfiMethods.PhysicsWorldReset(world, instance, out _);
            ThrowIfFailed(status, "PhysicsWorldReset", modelId, motionId);
            CopyRigidbodyStates();
            seededSinceReset = false;
            pendingAnimationFrame = -1;
        }

        public void Step(int frame, float deltaTime)
        {
            MmdPhysicsPolicy.ValidateLiveStepInput(frame, deltaTime);
            ThrowIfDisposed();
            if (pendingAnimationFrame < 0)
            {
                throw new InvalidOperationException("mmd-anim physics requires an animation frame before Step.");
            }

            int animationFrame = pendingAnimationFrame;
            pendingAnimationFrame = -1;
            int animationStatus = MmdRuntimeFfiMethods.InstanceEvaluateClipFrameBeforePhysics(
                instance,
                clip,
                animationFrame);
            ThrowIfFailed(animationStatus, "InstanceEvaluateClipFrameBeforePhysics", modelId, motionId);

            if (!seededSinceReset)
            {
                int resetStatus = MmdRuntimeFfiMethods.PhysicsWorldReset(world, instance, out _);
                ThrowIfFailed(resetStatus, "PhysicsWorldReset", modelId, motionId);
                CopyRigidbodyStates();
                seededSinceReset = true;
                return;
            }

            int stepStatus = MmdRuntimeFfiMethods.PhysicsWorldStepRuntime(
                world,
                instance,
                deltaTime,
                out _);
            ThrowIfFailed(stepStatus, "PhysicsWorldStepRuntime", modelId, motionId);
            CopyRigidbodyStates();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (world != IntPtr.Zero)
            {
                MmdRuntimeFfiMethods.PhysicsWorldFree(world);
            }

            if (clip != IntPtr.Zero)
            {
                MmdRuntimeFfiMethods.ClipFree(clip);
            }

            if (instance != IntPtr.Zero)
            {
                MmdRuntimeFfiMethods.InstanceFree(instance);
            }

            if (model != IntPtr.Zero)
            {
                MmdRuntimeFfiMethods.ModelFree(model);
            }

            GC.SuppressFinalize(this);
        }

        private void CopyRigidbodyStates()
        {
            if (rigidbodyStates.Length == 0)
            {
                return;
            }

            int status = MmdRuntimeFfiMethods.PhysicsWorldCopyRigidbodyStates(
                world,
                rigidbodyStates,
                new IntPtr(rigidbodyStates.Length));
            ThrowIfFailed(status, "PhysicsWorldCopyRigidbodyStates", modelId, motionId);
        }

        private int ReadRigidbodyCount()
        {
            int status = MmdRuntimeFfiMethods.PhysicsWorldRigidbodyCount(world, out IntPtr count);
            ThrowIfFailed(status, "PhysicsWorldRigidbodyCount", modelId, motionId);
            return MmdFfiMarshal.CheckedIntPtrToInt(count, "native rigidbody count");
        }

        private void ValidateBodyIndex(int bodyIndex)
        {
            int bodyCount = rigidbodyStates.Length / TransformFloatCount;
            if (bodyIndex < 0 || bodyIndex >= bodyCount)
            {
                throw new ArgumentOutOfRangeException(nameof(bodyIndex));
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(MmdAnimPhysicsBackend));
            }
        }

        private static MmdPhysicsBackendException CreateNativeException(string operation, int status)
        {
            return new MmdPhysicsBackendException(
                operation,
                "mmd-anim-bullet-native",
                $"status-{status}",
                LastErrorMessage());
        }

        private static void ThrowIfFailed(int status, string operation, string modelId, string motionId)
        {
            if (status == 0)
            {
                return;
            }

            throw new MmdPhysicsBackendException(
                operation,
                "mmd-anim-bullet-native",
                $"status-{status}",
                LastErrorMessage(),
                modelId,
                motionId);
        }

        private static string LastErrorMessage()
        {
            return Marshal.PtrToStringAnsi(MmdRuntimeFfiMethods.LastErrorMessage()) ?? "Native mmd-anim physics call failed.";
        }
    }

    internal sealed class MmdLegacyBulletPhysicsBackendAdapter : IMmdLivePhysicsBackend
    {
        private readonly BulletMmdPhysicsBackend backend;

        internal MmdLegacyBulletPhysicsBackendAdapter(string modelId, string motionId)
        {
            backend = new BulletMmdPhysicsBackend(modelId, motionId);
        }

        public string Name => backend.Name;

        public int SkippedWorldAnchorJointCount => backend.SkippedWorldAnchorJointCount;

        public void InitializeWorld(MmdModelDefinition model) => backend.InitializeWorld(model);

        public void SetAnimationFrame(int frame)
        {
            MmdPlaybackTime.ValidateFrame(frame);
        }

        public void SetRigidbodyTransform(int bodyIndex, float[] position, float[] rotation) => backend.SetRigidbodyTransform(bodyIndex, position, rotation);

        public MmdPhysicsBodyTransform GetRigidbodyTransform(int bodyIndex) => backend.GetRigidbodyTransform(bodyIndex);

        public string GetRigidbodyShapeType(int bodyIndex) => backend.GetRigidbodyShapeType(bodyIndex);

        public void SyncInterpolationAndZeroVelocity() => backend.SyncInterpolationAndZeroVelocity();

        public void Reset() => backend.Reset();

        public void Step(int frame, float deltaTime) => backend.Step(frame, deltaTime);

        public void Dispose() => backend.Dispose();
    }
}
