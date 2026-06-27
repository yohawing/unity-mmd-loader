#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Mmd.Native;
using Mmd.Parser;

namespace Mmd.Physics
{
    [Serializable]
    public sealed class MmdPhysicsBackendAvailability
    {
        public string backendName = string.Empty;
        public string wrapperLibraryName = string.Empty;
        public bool backendAvailable;
        public string status = string.Empty;
        public string unsupportedReason = string.Empty;
        public string nativeVersion = string.Empty;
    }

    public sealed class MmdPhysicsBodyTransform
    {
        public float[] position = Array.Empty<float>();
        public float[] rotation = Array.Empty<float>();
    }

    public sealed class MmdPhysicsCollisionFilter
    {
        public int group;
        public int mask;
    }

    public sealed class MmdPhysicsJointDescriptorReadback
    {
        public int bodyAIndex;
        public int bodyBIndex;
        public float[] position = Array.Empty<float>();
        public float[] rotation = Array.Empty<float>();
        public float[] linearLowerLimit = Array.Empty<float>();
        public float[] linearUpperLimit = Array.Empty<float>();
        public float[] angularLowerLimit = Array.Empty<float>();
        public float[] angularUpperLimit = Array.Empty<float>();
        public float[] linearSpring = Array.Empty<float>();
        public float[] angularSpring = Array.Empty<float>();
        public float[] frameAPosition = Array.Empty<float>();
        public float[] frameARotation = Array.Empty<float>();
        public float[] frameBPosition = Array.Empty<float>();
        public float[] frameBRotation = Array.Empty<float>();
    }

    public sealed class BulletMmdPhysicsBackend : IMmdPhysicsBackend, IDisposable
    {
        public const float FixedTimeStepSeconds = 1.0f / 60.0f;
        private static float maxSubStepEstimateFixedTimeStepSeconds = FixedTimeStepSeconds;

        // Faithful to saba (MMDPhysics m_maxSubStepCount = 10): a hard, small cap on the number of
        // Bullet substeps per Step. When a frame hitch, a Timeline seek, or a backward-scrub world
        // recreate hands the backend a large deltaTime, Bullet must DROP the excess simulation time
        // (clamp) instead of integrating seconds of motion in one step. Without this cap, a large
        // deltaTime let the 揺れもの simulate far too long in a single frame and fling apart
        // ("崩れる"/explosion on resume). The previous value (240) effectively defeated Bullet's
        // built-in clamp because it always allowed enough substeps to integrate the whole deltaTime.
        public const int MaxSubStepsLimit = 10;
        private readonly MmdPhysicsBackendAvailability availability;
        private readonly string modelId;
        private readonly string motionId;
        private IntPtr world;
        private bool disposed;
        private bool descriptorInitialized;
        private int skippedWorldAnchorJointCount;

        public BulletMmdPhysicsBackend(string modelId = "", string motionId = "")
        {
            availability = ProbeAvailability();
            this.modelId = modelId ?? string.Empty;
            this.motionId = motionId ?? string.Empty;
        }

        public int SkippedWorldAnchorJointCount => skippedWorldAnchorJointCount;

        public string Name => "Bullet";

        public bool IsDeterministic => false;

        public MmdPhysicsBackendAvailability Availability => availability;

        public int RigidbodyCount
        {
            get
            {
                ThrowIfUnavailable("RigidbodyCount");
                EnsureWorld();
                return MmdNativePhysicsMethods.WorldGetRigidbodyCount(world);
            }
        }

        public int JointCount
        {
            get
            {
                ThrowIfUnavailable("JointCount");
                EnsureWorld();
                return MmdNativePhysicsMethods.WorldGetJointCount(world);
            }
        }

        public void InitializeWorld(MmdModelDefinition model)
        {
            ThrowIfUnavailable("InitializeWorld");
            MmdPhysicsDescriptorValidator.ThrowIfInvalid(model);
            EnsureWorld();
            if (descriptorInitialized)
            {
                throw new InvalidOperationException("Bullet physics world has already been initialized from a descriptor.");
            }

            var nativeBodyIndices = new Dictionary<int, int>();
            IReadOnlyList<MmdRigidbodyDefinition> rigidbodies = model.physics.rigidbodies;
            for (int i = 0; i < rigidbodies.Count; i++)
            {
                MmdRigidbodyDefinition body = rigidbodies[i];
                float mass = string.Equals(body.physicsKind, "static", StringComparison.Ordinal) ? 0.0f : body.mass;
                GuardStatus(MmdNativePhysicsMethods.WorldAddRigidbody(
                    world,
                    body.shapeType,
                    body.size[0],
                    body.size[1],
                    body.size[2],
                    body.position[0],
                    body.position[1],
                    body.position[2],
                    body.rotation[0],
                    body.rotation[1],
                    body.rotation[2],
                    mass,
                    body.linearDamping,
                    body.angularDamping,
                    body.friction,
                    body.restitution,
                    body.group,
                    body.mask,
                    out int nativeBodyIndex), "AddRigidbody");
                nativeBodyIndices.Add(body.index, nativeBodyIndex);
            }

            skippedWorldAnchorJointCount = 0;
            IReadOnlyList<MmdJointDefinition> joints = model.physics.joints;
            for (int i = 0; i < joints.Count; i++)
            {
                MmdJointDefinition joint = joints[i];
                // Skip one-sided world-anchored joints (one rigidbody index is -1) which
                // connect a body to the world ground rather than another rigidbody.
                // These are unsupported by the native Bullet 6-DoF spring joint wrapper.
                // Joints where both indices are -1 are rejected by the validator.
                if (joint.rigidbodyAIndex < 0 || joint.rigidbodyBIndex < 0)
                {
                    skippedWorldAnchorJointCount++;
                    continue;
                }

                GuardStatus(MmdNativePhysicsMethods.WorldAdd6DofSpringJoint(
                    world,
                    nativeBodyIndices[joint.rigidbodyAIndex],
                    nativeBodyIndices[joint.rigidbodyBIndex],
                    joint.position,
                    joint.rotation,
                    joint.linearLowerLimit,
                    joint.linearUpperLimit,
                    joint.angularLowerLimit,
                    joint.angularUpperLimit,
                    joint.linearSpring,
                    joint.angularSpring,
                    out _), "Add6DofSpringJoint");
            }

            descriptorInitialized = true;
        }

        public MmdPhysicsBodyTransform GetRigidbodyTransform(int bodyIndex)
        {
            ThrowIfUnavailable("GetRigidbodyTransform");
            EnsureWorld();
            if (bodyIndex < 0 || bodyIndex >= RigidbodyCount)
            {
                throw new ArgumentOutOfRangeException(nameof(bodyIndex), "Rigidbody index is out of range.");
            }

            float[] position = new float[3];
            float[] rotation = new float[4];
            GuardStatus(MmdNativePhysicsMethods.WorldGetRigidbodyTransform(world, bodyIndex, position, rotation), "GetRigidbodyTransform");
            ValidateVector(position, 3, "native position");
            ValidateQuaternion(rotation, "native rotation");
            return new MmdPhysicsBodyTransform
            {
                position = position,
                rotation = rotation
            };
        }

        public string GetRigidbodyShapeType(int bodyIndex)
        {
            ThrowIfUnavailable("GetRigidbodyShapeType");
            EnsureWorld();
            if (bodyIndex < 0 || bodyIndex >= RigidbodyCount)
            {
                throw new ArgumentOutOfRangeException(nameof(bodyIndex), "Rigidbody index is out of range.");
            }

            int shapeKind = MmdNativePhysicsMethods.WorldGetRigidbodyShapeKind(world, bodyIndex);
            return shapeKind switch
            {
                0 => "sphere",
                1 => "box",
                2 => "capsule",
                _ => "unknown"
            };
        }

        public MmdPhysicsCollisionFilter GetRigidbodyCollisionFilter(int bodyIndex)
        {
            ThrowIfUnavailable("GetRigidbodyCollisionFilter");
            EnsureWorld();
            if (bodyIndex < 0 || bodyIndex >= RigidbodyCount)
            {
                throw new ArgumentOutOfRangeException(nameof(bodyIndex), "Rigidbody index is out of range.");
            }

            GuardStatus(
                MmdNativePhysicsMethods.WorldGetRigidbodyCollisionFilter(world, bodyIndex, out int group, out int mask),
                "GetRigidbodyCollisionFilter");
            return new MmdPhysicsCollisionFilter
            {
                group = group,
                mask = mask
            };
        }

        public MmdPhysicsJointDescriptorReadback Get6DofSpringJointDescriptor(int jointIndex)
        {
            ThrowIfUnavailable("Get6DofSpringJointDescriptor");
            EnsureWorld();
            if (jointIndex < 0 || jointIndex >= JointCount)
            {
                throw new ArgumentOutOfRangeException(nameof(jointIndex), "Joint index is out of range.");
            }

            float[] position = new float[3];
            float[] rotation = new float[3];
            float[] linearLowerLimit = new float[3];
            float[] linearUpperLimit = new float[3];
            float[] angularLowerLimit = new float[3];
            float[] angularUpperLimit = new float[3];
            float[] linearSpring = new float[3];
            float[] angularSpring = new float[3];
            float[] frameAPosition = new float[3];
            float[] frameARotation = new float[4];
            float[] frameBPosition = new float[3];
            float[] frameBRotation = new float[4];
            GuardStatus(
                MmdNativePhysicsMethods.WorldGet6DofSpringJointDescriptor(
                    world,
                    jointIndex,
                    out int bodyAIndex,
                    out int bodyBIndex,
                    position,
                    rotation,
                    linearLowerLimit,
                    linearUpperLimit,
                    angularLowerLimit,
                    angularUpperLimit,
                    linearSpring,
                    angularSpring,
                    frameAPosition,
                    frameARotation,
                    frameBPosition,
                    frameBRotation),
                "Get6DofSpringJointDescriptor");
            return new MmdPhysicsJointDescriptorReadback
            {
                bodyAIndex = bodyAIndex,
                bodyBIndex = bodyBIndex,
                position = position,
                rotation = rotation,
                linearLowerLimit = linearLowerLimit,
                linearUpperLimit = linearUpperLimit,
                angularLowerLimit = angularLowerLimit,
                angularUpperLimit = angularUpperLimit,
                linearSpring = linearSpring,
                angularSpring = angularSpring,
                frameAPosition = frameAPosition,
                frameARotation = frameARotation,
                frameBPosition = frameBPosition,
                frameBRotation = frameBRotation
            };
        }

        public void SetRigidbodyTransform(int bodyIndex, float[] position, float[] rotation)
        {
            ThrowIfUnavailable("SetRigidbodyTransform");
            EnsureWorld();
            if (bodyIndex < 0 || bodyIndex >= RigidbodyCount)
            {
                throw new ArgumentOutOfRangeException(nameof(bodyIndex), "Rigidbody index is out of range.");
            }

            ValidateVector(position, 3, nameof(position));
            ValidateVector(rotation, 4, nameof(rotation));
            ValidateQuaternion(rotation, nameof(rotation));
            GuardStatus(MmdNativePhysicsMethods.WorldSetRigidbodyTransform(world, bodyIndex, position, rotation), "SetRigidbodyTransform");
        }

        public void Reset()
        {
            ThrowIfUnavailable("Reset");
            EnsureWorld();
            GuardStatus(MmdNativePhysicsMethods.WorldReset(world), "Reset");
        }

        /// <summary>
        /// Seed-scoped settle (saba PMXModel::ResetPhysics): after the bodies have been teleported to their
        /// CURRENT bone-derived pose (<see cref="SetRigidbodyTransform"/>), re-aligns each body's interpolation
        /// world transform with its (already current) world transform and zeroes all velocities/forces. This
        /// does NOT change the world transform. It exists so the first forward <see cref="Step"/> computes no
        /// spurious kinematic velocity (currentWorld - staleOriginBindInterp)/dt that would explode the chain.
        /// Call ONLY at seed time; the per-frame forward re-pin relies on the live interpolation delta to drag
        /// the jointed dynamic bodies, so this must not run every frame.
        /// </summary>
        public void SyncInterpolationAndZeroVelocity()
        {
            ThrowIfUnavailable("SyncInterpolationAndZeroVelocity");
            EnsureWorld();
            GuardStatus(MmdNativePhysicsMethods.WorldSettleToCurrent(world), "SyncInterpolationAndZeroVelocity");
        }

        public void Step(int frame, float deltaTime)
        {
            MmdPhysicsPolicy.ValidateLiveStepInput(frame, deltaTime);
            ThrowIfUnavailable("Step");
            EnsureWorld();
            GuardStatus(MmdNativePhysicsMethods.WorldStep(world, deltaTime, EstimateMaxSubSteps(deltaTime)), "Step");
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            if (world != IntPtr.Zero)
            {
                MmdNativePhysicsMethods.WorldDestroy(world);
                world = IntPtr.Zero;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~BulletMmdPhysicsBackend()
        {
            Dispose();
        }

        public static MmdPhysicsBackendAvailability ProbeAvailability()
        {
            try
            {
                string version = Marshal.PtrToStringAnsi(MmdNativePhysicsMethods.GetVersion()) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(version))
                {
                    return Unavailable("wrapper returned a blank native version");
                }

                return new MmdPhysicsBackendAvailability
                {
                    backendName = "Bullet",
                    wrapperLibraryName = MmdNativePhysicsMethods.LibraryName,
                    backendAvailable = true,
                    status = "available",
                    nativeVersion = version
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
        }

        private static MmdPhysicsBackendAvailability Unavailable(string reason)
        {
            return new MmdPhysicsBackendAvailability
            {
                backendName = "Bullet",
                wrapperLibraryName = MmdNativePhysicsMethods.LibraryName,
                backendAvailable = false,
                status = "backend-unavailable",
                unsupportedReason = string.IsNullOrWhiteSpace(reason) ? "Bullet wrapper DLL is not available." : reason
            };
        }

        private void ThrowIfUnavailable(string operation)
        {
            if (availability.backendAvailable)
            {
                return;
            }

            throw new MmdPhysicsBackendException(
                operation,
                "Bullet",
                availability.status,
                string.IsNullOrWhiteSpace(availability.unsupportedReason)
                    ? $"wrapper={availability.wrapperLibraryName}"
                    : availability.unsupportedReason,
                modelId,
                motionId);
        }

        private void EnsureWorld()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(BulletMmdPhysicsBackend));
            }

            if (world != IntPtr.Zero)
            {
                return;
            }

            GuardStatus(MmdNativePhysicsMethods.WorldCreate(out world), "WorldCreate");
            if (world == IntPtr.Zero)
            {
                throw new MmdPhysicsBackendException(
                    "WorldCreate",
                    "Bullet",
                    "native-error",
                    "Bullet physics world creation returned a null handle.",
                    modelId,
                    motionId);
            }
        }

        private void GuardStatus(int status, string operation)
        {
            if (status == 0)
            {
                return;
            }

            string message = Marshal.PtrToStringAnsi(MmdNativePhysicsMethods.GetLastError()) ?? string.Empty;
            throw new MmdPhysicsBackendException(
                operation,
                "Bullet",
                $"native-status-{status}",
                string.IsNullOrWhiteSpace(message) ? "no native error message" : message,
                modelId,
                motionId);
        }

        public static int EstimateMaxSubSteps(float deltaTime)
        {
            if (deltaTime <= 0.0f)
            {
                return 0;
            }

            return Math.Min(MaxSubStepsLimit, Math.Max(1, (int)Math.Ceiling(deltaTime / maxSubStepEstimateFixedTimeStepSeconds)));
        }

        internal static float MaxSubStepEstimateFixedTimeStepSecondsForDiagnostics =>
            maxSubStepEstimateFixedTimeStepSeconds;

        internal static void SetMaxSubStepEstimateFixedTimeStepSecondsForDiagnostics(float fixedTimeStepSeconds)
        {
            maxSubStepEstimateFixedTimeStepSeconds =
                float.IsFinite(fixedTimeStepSeconds) && fixedTimeStepSeconds > 0.0f
                    ? fixedTimeStepSeconds
                    : FixedTimeStepSeconds;
        }

        internal static void ResetMaxSubStepEstimateFixedTimeStepSecondsForDiagnostics()
        {
            maxSubStepEstimateFixedTimeStepSeconds = FixedTimeStepSeconds;
        }

        private static void ValidateVector(float[] values, int length, string name)
        {
            if (values == null || values.Length < length)
            {
                throw new ArgumentException($"{name} must contain at least {length} values.", name);
            }

            for (int i = 0; i < length; i++)
            {
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                {
                    throw new ArgumentException($"{name} must contain only finite values.", name);
                }
            }
        }

        private static void ValidateQuaternion(float[] values, string name)
        {
            ValidateVector(values, 4, name);
            float lengthSquared =
                values[0] * values[0]
                + values[1] * values[1]
                + values[2] * values[2]
                + values[3] * values[3];
            if (!float.IsFinite(lengthSquared) || lengthSquared < 0.000001f)
            {
                throw new ArgumentException($"{name} must be a finite non-zero quaternion.", name);
            }
        }
    }
}
