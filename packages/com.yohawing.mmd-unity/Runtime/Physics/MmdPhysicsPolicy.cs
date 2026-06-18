#nullable enable

using System;

namespace Yohawing.MmdUnity.Physics
{
    [Serializable]
    public sealed class MmdPhysicsModePolicyRecord
    {
        public string physicsMode = string.Empty;
        public string backendName = string.Empty;
        public bool supportsRandomAccess;
        public bool isDeterministic;
        public string status = string.Empty;
        public string unsupportedReason = string.Empty;
    }

    public static class MmdPhysicsPolicy
    {
        public const string StatusAvailable = "available";
        public const string StatusUnsupported = "unsupported";

        public static MmdPhysicsModePolicyRecord Describe(MmdPhysicsMode mode, IMmdPhysicsBackend? backend = null)
        {
            backend ??= new NullMmdPhysicsBackend();
            ValidateBackendName(backend.Name);

            return mode switch
            {
                MmdPhysicsMode.Off => new MmdPhysicsModePolicyRecord
                {
                    physicsMode = "off",
                    backendName = backend.Name,
                    supportsRandomAccess = true,
                    isDeterministic = backend.IsDeterministic,
                    status = StatusAvailable
                },
                MmdPhysicsMode.Live => new MmdPhysicsModePolicyRecord
                {
                    physicsMode = "live",
                    backendName = backend.Name,
                    supportsRandomAccess = false,
                    isDeterministic = backend.IsDeterministic,
                    status = StatusAvailable
                },
                MmdPhysicsMode.Cache => new MmdPhysicsModePolicyRecord
                {
                    physicsMode = "cache",
                    backendName = backend.Name,
                    supportsRandomAccess = true,
                    isDeterministic = true,
                    status = StatusUnsupported,
                    unsupportedReason = "physics cache playback is not implemented"
                },
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown physics mode.")
            };
        }

        public static void ThrowIfRandomAccessUnsupported(MmdPhysicsMode mode)
        {
            MmdPhysicsModePolicyRecord policy = Describe(mode);
            if (!policy.supportsRandomAccess)
            {
                throw new InvalidOperationException($"Physics mode '{policy.physicsMode}' does not support random access evaluation.");
            }
        }

        public static void ValidateLiveStepInput(int frame, float deltaTime)
        {
            MmdPlaybackTime.ValidateFrame(frame);
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Physics live deltaTime must be a non-negative finite value.");
            }
        }

        private static void ValidateBackendName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Physics backend name must be a non-empty string.", nameof(name));
            }
        }
    }
}
