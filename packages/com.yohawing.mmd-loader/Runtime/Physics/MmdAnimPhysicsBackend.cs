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

        MmdPhysicsBodyTransform GetRigidbodyTransform(int bodyIndex);

        void CopyRigidbodyTransform(int bodyIndex, float[] position, float[] rotation);

        string GetRigidbodyShapeType(int bodyIndex);

        void Reset();
    }

    /// <summary>
    /// Unity-side adapter for the feature-gated mmd-anim Bullet runtime ABI.
    /// The adapter owns a runtime instance and a mmd-anim physics world and
    /// evaluates host-provided Unity pose arrays through the native
    /// before/after-physics boundary.
    /// </summary>
    internal sealed class MmdAnimPhysicsBackend : IMmdLivePhysicsBackend
    {
        private const int TransformFloatCount = 7;
        private readonly string modelId;
        private readonly string motionId;
        private readonly IntPtr model;
        private readonly IntPtr instance;
        private IntPtr world;
        private readonly int boneCount;
        private readonly int morphCount;
        private readonly int ikCount;
        private float[] rigidbodyStates;
        private string[] rigidbodyShapeTypes = Array.Empty<string>();
        private bool seededSinceReset;
        private bool disposed;
        private int skippedWorldAnchorJointCount;

        private MmdAnimPhysicsBackend(byte[] pmxBytes, string modelId, string motionId)
        {
            this.modelId = modelId ?? string.Empty;
            this.motionId = motionId ?? string.Empty;

            IntPtr createdModel = IntPtr.Zero;
            IntPtr createdInstance = IntPtr.Zero;
            IntPtr createdWorld = IntPtr.Zero;
            try
            {
                createdModel = MmdRuntimeFfiMethods.ModelCreateFromPmxBytes(pmxBytes, new IntPtr(pmxBytes.Length));
                if (createdModel == IntPtr.Zero)
                {
                    throw CreateNativeException("ModelCreateFromPmxBytes", 4);
                }

                createdInstance = MmdRuntimeFfiMethods.InstanceCreateForModel(createdModel);
                if (createdInstance == IntPtr.Zero)
                {
                    throw CreateNativeException("InstanceCreateForModel", 4);
                }

                boneCount = MmdFfiMarshal.CheckedIntPtrToInt(
                    MmdRuntimeFfiMethods.ModelBoneCount(createdModel), "native bone count");
                morphCount = MmdFfiMarshal.CheckedIntPtrToInt(
                    MmdRuntimeFfiMethods.ModelMorphCount(createdModel), "native morph count");
                ikCount = MmdFfiMarshal.CheckedIntPtrToInt(
                    MmdRuntimeFfiMethods.ModelIkCount(createdModel), "native IK count");

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
                instance = createdInstance;
                world = createdWorld;
                rigidbodyStates = Array.Empty<float>();
                createdModel = IntPtr.Zero;
                createdInstance = IntPtr.Zero;
                createdWorld = IntPtr.Zero;
            }
            finally
            {
                if (createdWorld != IntPtr.Zero)
                {
                    MmdRuntimeFfiMethods.PhysicsWorldFree(createdWorld);
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

        internal static MmdPhysicsBackendAvailability ProbeAvailability()
        {
            try
            {
                uint abiVersion = MmdRuntimeFfiMethods.ValidateAbiVersion();
                uint featureFlags = MmdRuntimeFfiMethods.FeatureFlags();
                uint requiredFlags = MmdRuntimeFfiMethods.FeatureSplitPhysicsEvaluation |
                    MmdRuntimeFfiMethods.FeaturePhysicsBulletNative |
                    MmdRuntimeFfiMethods.FeatureHostPoseNativeMorphs;
                if ((featureFlags & requiredFlags) != requiredFlags)
                {
                    return Unavailable($"mmd-anim physics features are unavailable (flags=0x{featureFlags:X8}).");
                }

                return new MmdPhysicsBackendAvailability
                {
                    backendName = "mmd-anim-bullet-native",
                    wrapperLibraryName = MmdRuntimeFfiMethods.LibraryName,
                    backendAvailable = true,
                    status = "available",
                    nativeVersion = $"abi-{abiVersion}"
                };
            }
            catch (DllNotFoundException ex)
            {
                return Unavailable(ex.Message);
            }
            catch (EntryPointNotFoundException ex)
            {
                return Unavailable(ex.Message);
            }
            catch (BadImageFormatException ex)
            {
                return Unavailable(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Unavailable(ex.Message);
            }
        }

        private static MmdPhysicsBackendAvailability Unavailable(string reason)
        {
            return new MmdPhysicsBackendAvailability
            {
                backendName = "mmd-anim-bullet-native",
                wrapperLibraryName = MmdRuntimeFfiMethods.LibraryName,
                backendAvailable = false,
                status = "backend-unavailable",
                unsupportedReason = string.IsNullOrWhiteSpace(reason)
                    ? "mmd_runtime_ffi is not available."
                    : reason
            };
        }

        internal static bool TryCreate(
            byte[]? pmxBytes,
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
                    MmdRuntimeFfiMethods.FeaturePhysicsBulletNative |
                    MmdRuntimeFfiMethods.FeatureHostPoseNativeMorphs;
                if ((featureFlags & requiredFlags) != requiredFlags)
                {
                    reason = $"mmd-runtime physics host features are unavailable (flags=0x{featureFlags:X8}).";
                    return false;
                }

                backend = new MmdAnimPhysicsBackend(pmxBytes, modelId, motionId);
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
            if (nativeRigidbodyCount == 0 && managedRigidbodyCount > 0)
            {
                InitializeDescriptorWorld(modelDefinition);
                nativeRigidbodyCount = ReadRigidbodyCount();
            }

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

        private void InitializeDescriptorWorld(MmdModelDefinition modelDefinition)
        {
            IReadOnlyList<MmdRigidbodyDefinition> managedBodies = modelDefinition.physics!.rigidbodies;
            var nativeBodies = new MmdRuntimeFfiMethods.PhysicsRigidbodyDescriptor[managedBodies.Count];
            for (int i = 0; i < managedBodies.Count; i++)
            {
                nativeBodies[i] = CreateRigidbodyDescriptor(modelDefinition, managedBodies[i]);
            }

            IReadOnlyList<MmdJointDefinition> managedJoints = modelDefinition.physics.joints;
            var nativeJoints = new List<MmdRuntimeFfiMethods.PhysicsJointDescriptor>(managedJoints.Count);
            foreach (MmdJointDefinition joint in managedJoints)
            {
                if (joint.rigidbodyAIndex < 0 || joint.rigidbodyBIndex < 0)
                {
                    continue;
                }

                nativeJoints.Add(CreateJointDescriptor(managedBodies, joint));
            }

            int status = MmdRuntimeFfiMethods.PhysicsWorldCreate(
                nativeBodies,
                new IntPtr(nativeBodies.Length),
                nativeJoints.ToArray(),
                new IntPtr(nativeJoints.Count),
                out IntPtr descriptorWorld);
            ThrowIfFailed(status, "PhysicsWorldCreate", modelId, motionId);
            if (descriptorWorld == IntPtr.Zero)
            {
                throw CreateNativeException("PhysicsWorldCreate", 4);
            }

            MmdRuntimeFfiMethods.PhysicsWorldFree(world);
            world = descriptorWorld;
        }

        private static MmdRuntimeFfiMethods.PhysicsRigidbodyDescriptor CreateRigidbodyDescriptor(
            MmdModelDefinition modelDefinition,
            MmdRigidbodyDefinition body)
        {
            uint shape = body.shapeType switch
            {
                "sphere" => MmdRuntimeFfiMethods.PhysicsShapeSphere,
                "box" => MmdRuntimeFfiMethods.PhysicsShapeBox,
                "capsule" => MmdRuntimeFfiMethods.PhysicsShapeCapsule,
                _ => throw new ArgumentException($"Unsupported rigidbody shape: {body.shapeType}", nameof(modelDefinition))
            };
            uint mode = body.physicsKind switch
            {
                "static" => MmdRuntimeFfiMethods.PhysicsBodyModeStatic,
                "dynamic" => MmdRuntimeFfiMethods.PhysicsBodyModeDynamic,
                "dynamicBone" => MmdRuntimeFfiMethods.PhysicsBodyModeDynamicBone,
                "dynamic-orientation" => MmdRuntimeFfiMethods.PhysicsBodyModeDynamicBone,
                _ => throw new ArgumentException($"Unsupported rigidbody physics kind: {body.physicsKind}", nameof(modelDefinition))
            };

            float[] bodyPosition = body.position;
            float[] bodyRotation = body.rotation;
            float[] boneOrigin = GetBoneOrigin(modelDefinition, body.boneIndex);
            float[] bodyFromBonePosition = new[]
            {
                bodyPosition[0] - boneOrigin[0],
                bodyPosition[1] - boneOrigin[1],
                bodyPosition[2] - boneOrigin[2]
            };
            float[] bodyFromBoneRotation = EulerXyzToQuaternion(bodyRotation[0], bodyRotation[1], bodyRotation[2]);
            float[] boneFromBodyRotation = QuaternionInverse(bodyFromBoneRotation);
            float[] boneFromBodyPosition = Negate(RotateVector(boneFromBodyRotation, bodyFromBonePosition));

            return new MmdRuntimeFfiMethods.PhysicsRigidbodyDescriptor
            {
                shape = shape,
                shapeSizeX = body.size[0],
                shapeSizeY = body.size[1],
                shapeSizeZ = body.size[2],
                positionX = bodyPosition[0],
                positionY = bodyPosition[1],
                positionZ = bodyPosition[2],
                rotationX = bodyRotation[0],
                rotationY = bodyRotation[1],
                rotationZ = bodyRotation[2],
                mass = body.mass,
                linearDamping = body.linearDamping,
                angularDamping = body.angularDamping,
                friction = body.friction,
                restitution = body.restitution,
                collisionGroup = checked((ushort)body.group),
                collisionMask = checked((ushort)body.mask),
                boneIndex = body.boneIndex,
                mode = mode,
                bodyFromBonePositionX = bodyFromBonePosition[0],
                bodyFromBonePositionY = bodyFromBonePosition[1],
                bodyFromBonePositionZ = bodyFromBonePosition[2],
                bodyFromBoneRotationX = bodyFromBoneRotation[0],
                bodyFromBoneRotationY = bodyFromBoneRotation[1],
                bodyFromBoneRotationZ = bodyFromBoneRotation[2],
                bodyFromBoneRotationW = bodyFromBoneRotation[3],
                boneFromBodyPositionX = boneFromBodyPosition[0],
                boneFromBodyPositionY = boneFromBodyPosition[1],
                boneFromBodyPositionZ = boneFromBodyPosition[2],
                boneFromBodyRotationX = boneFromBodyRotation[0],
                boneFromBodyRotationY = boneFromBodyRotation[1],
                boneFromBodyRotationZ = boneFromBodyRotation[2],
                boneFromBodyRotationW = boneFromBodyRotation[3]
            };
        }

        private static MmdRuntimeFfiMethods.PhysicsJointDescriptor CreateJointDescriptor(
            IReadOnlyList<MmdRigidbodyDefinition> managedBodies,
            MmdJointDefinition joint)
        {
            return new MmdRuntimeFfiMethods.PhysicsJointDescriptor
            {
                kind = MmdRuntimeFfiMethods.PhysicsJointKindGeneric6DofSpring,
                rigidbodyA = new IntPtr(FindRigidbodyOrdinal(managedBodies, joint.rigidbodyAIndex)),
                rigidbodyB = new IntPtr(FindRigidbodyOrdinal(managedBodies, joint.rigidbodyBIndex)),
                positionX = joint.position[0],
                positionY = joint.position[1],
                positionZ = joint.position[2],
                rotationX = joint.rotation[0],
                rotationY = joint.rotation[1],
                rotationZ = joint.rotation[2],
                translationLowerX = joint.linearLowerLimit[0],
                translationLowerY = joint.linearLowerLimit[1],
                translationLowerZ = joint.linearLowerLimit[2],
                translationUpperX = joint.linearUpperLimit[0],
                translationUpperY = joint.linearUpperLimit[1],
                translationUpperZ = joint.linearUpperLimit[2],
                rotationLowerX = joint.angularLowerLimit[0],
                rotationLowerY = joint.angularLowerLimit[1],
                rotationLowerZ = joint.angularLowerLimit[2],
                rotationUpperX = joint.angularUpperLimit[0],
                rotationUpperY = joint.angularUpperLimit[1],
                rotationUpperZ = joint.angularUpperLimit[2],
                springTranslationX = joint.linearSpring[0],
                springTranslationY = joint.linearSpring[1],
                springTranslationZ = joint.linearSpring[2],
                springRotationX = joint.angularSpring[0],
                springRotationY = joint.angularSpring[1],
                springRotationZ = joint.angularSpring[2]
            };
        }

        private static int FindRigidbodyOrdinal(IReadOnlyList<MmdRigidbodyDefinition> bodies, int bodyIndex)
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i].index == bodyIndex)
                {
                    return i;
                }
            }

            throw new ArgumentException($"Rigidbody index is not present: {bodyIndex}");
        }

        private static float[] GetBoneOrigin(MmdModelDefinition modelDefinition, int boneIndex)
        {
            if (boneIndex < 0 || boneIndex >= modelDefinition.bones.Count)
            {
                return new[] { 0.0f, 0.0f, 0.0f };
            }

            float[] origin = modelDefinition.bones[boneIndex].origin;
            return origin.Length == 3 ? origin : new[] { 0.0f, 0.0f, 0.0f };
        }

        private static float[] EulerXyzToQuaternion(float x, float y, float z)
        {
            float c1 = MathF.Cos(x * 0.5f);
            float c2 = MathF.Cos(y * 0.5f);
            float c3 = MathF.Cos(z * 0.5f);
            float s1 = MathF.Sin(x * 0.5f);
            float s2 = MathF.Sin(y * 0.5f);
            float s3 = MathF.Sin(z * 0.5f);
            return new[]
            {
                s1 * c2 * c3 + c1 * s2 * s3,
                c1 * s2 * c3 - s1 * c2 * s3,
                c1 * c2 * s3 + s1 * s2 * c3,
                c1 * c2 * c3 - s1 * s2 * s3
            };
        }

        private static float[] QuaternionInverse(float[] quaternion)
        {
            float lengthSquared = quaternion[0] * quaternion[0]
                + quaternion[1] * quaternion[1]
                + quaternion[2] * quaternion[2]
                + quaternion[3] * quaternion[3];
            if (lengthSquared < 1.0e-12f)
            {
                return new[] { 0.0f, 0.0f, 0.0f, 1.0f };
            }

            float scale = 1.0f / lengthSquared;
            return new[]
            {
                -quaternion[0] * scale,
                -quaternion[1] * scale,
                -quaternion[2] * scale,
                quaternion[3] * scale
            };
        }

        private static float[] RotateVector(float[] quaternion, float[] vector)
        {
            float x = quaternion[0];
            float y = quaternion[1];
            float z = quaternion[2];
            float w = quaternion[3];
            float tx = 2.0f * (y * vector[2] - z * vector[1]);
            float ty = 2.0f * (z * vector[0] - x * vector[2]);
            float tz = 2.0f * (x * vector[1] - y * vector[0]);
            return new[]
            {
                vector[0] + w * tx + y * tz - z * ty,
                vector[1] + w * ty + z * tx - x * tz,
                vector[2] + w * tz + x * ty - y * tx
            };
        }

        private static float[] Negate(float[] vector)
        {
            return new[] { -vector[0], -vector[1], -vector[2] };
        }

        public MmdPhysicsBodyTransform GetRigidbodyTransform(int bodyIndex)
        {
            var result = new MmdPhysicsBodyTransform
            {
                position = new float[3],
                rotation = new float[4]
            };
            CopyRigidbodyTransform(bodyIndex, result.position, result.rotation);
            return result;
        }

        public void CopyRigidbodyTransform(int bodyIndex, float[] position, float[] rotation)
        {
            ThrowIfDisposed();
            ValidateBodyIndex(bodyIndex);
            ValidateTransformDestination(position, rotation);
            int offset = checked(bodyIndex * TransformFloatCount);
            Array.Copy(rigidbodyStates, offset, position, 0, 3);
            Array.Copy(rigidbodyStates, offset + 3, rotation, 0, 4);
        }

        public string GetRigidbodyShapeType(int bodyIndex)
        {
            ThrowIfDisposed();
            ValidateBodyIndex(bodyIndex);
            return bodyIndex < rigidbodyShapeTypes.Length ? rigidbodyShapeTypes[bodyIndex] : string.Empty;
        }

        public void Reset()
        {
            ThrowIfDisposed();
            int status = MmdRuntimeFfiMethods.PhysicsWorldReset(world, instance, out _);
            ThrowIfFailed(status, "PhysicsWorldReset", modelId, motionId);
            CopyRigidbodyStates();
            seededSinceReset = false;
        }

        public void StepFromHostPose(
            int frame,
            float[] localPositionOffsets,
            float[] localRotations,
            float[] localScales,
            float[] morphWeights,
            byte[] ikEnabled,
            bool seed,
            float deltaTime)
        {
            MmdPhysicsPolicy.ValidateLiveStepInput(frame, deltaTime);
            ThrowIfDisposed();
            ValidateHostPoseArray(localPositionOffsets, checked(boneCount * 3), nameof(localPositionOffsets));
            ValidateHostPoseArray(localRotations, checked(boneCount * 4), nameof(localRotations));
            ValidateHostPoseArray(localScales, checked(boneCount * 3), nameof(localScales));
            ValidateHostPoseArray(morphWeights, morphCount, nameof(morphWeights));
            ValidateHostPoseArray(ikEnabled, ikCount, nameof(ikEnabled));

            GCHandle positionHandle = default;
            GCHandle rotationHandle = default;
            GCHandle scaleHandle = default;
            GCHandle morphHandle = default;
            GCHandle ikHandle = default;
            try
            {
                var pose = new MmdRuntimeFfiMethods.PhysicsHostPoseView
                {
                    localPositionOffsetsXyz = Pin(localPositionOffsets, ref positionHandle),
                    localRotationXyzw = Pin(localRotations, ref rotationHandle),
                    localScalesXyz = Pin(localScales, ref scaleHandle),
                    boneCount = new IntPtr(boneCount),
                    morphWeights = Pin(morphWeights, ref morphHandle),
                    morphCount = new IntPtr(morphCount),
                    ikEnabled = Pin(ikEnabled, ref ikHandle),
                    ikCount = new IntPtr(ikCount)
                };
                int status = MmdRuntimeFfiMethods.EvaluateHostFrame(
                    instance,
                    world,
                    ref pose,
                    seed || !seededSinceReset
                        ? MmdRuntimeFfiMethods.PhysicsFrameActionSeed
                        : MmdRuntimeFfiMethods.PhysicsFrameActionStep,
                    deltaTime,
                    ikTolerance: 0.0001f,
                    ikMaxIterationsCap: 0,
                    out _);
                ThrowIfFailed(status, "EvaluateHostFrame", modelId, motionId);
                CopyRigidbodyStates();
                seededSinceReset = true;
            }
            finally
            {
                Free(ref positionHandle);
                Free(ref rotationHandle);
                Free(ref scaleHandle);
                Free(ref morphHandle);
                Free(ref ikHandle);
            }
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

        private static void ValidateTransformDestination(float[] position, float[] rotation)
        {
            if (position == null || position.Length != 3)
            {
                throw new ArgumentException("Rigidbody position destination must contain three values.", nameof(position));
            }

            if (rotation == null || rotation.Length != 4)
            {
                throw new ArgumentException("Rigidbody rotation destination must contain four values.", nameof(rotation));
            }
        }

        private static void ValidateHostPoseArray<T>(T[] values, int expectedLength, string name)
        {
            if (values == null || values.Length != expectedLength)
            {
                throw new ArgumentException(
                    $"Host pose {name} must contain exactly {expectedLength} values.", name);
            }
        }

        private static IntPtr Pin<T>(T[] values, ref GCHandle handle) where T : struct
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

}
