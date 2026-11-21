#nullable enable

using System;

namespace Mmd.Physics
{
    [Serializable]
    internal sealed class MmdPhysicsBackendAvailability
    {
        public string backendName = string.Empty;
        public string wrapperLibraryName = string.Empty;
        public bool backendAvailable;
        public string status = string.Empty;
        public string unsupportedReason = string.Empty;
        public string nativeVersion = string.Empty;
    }

    internal sealed class MmdPhysicsBodyTransform
    {
        public float[] position = Array.Empty<float>();
        public float[] rotation = Array.Empty<float>();
    }
}
