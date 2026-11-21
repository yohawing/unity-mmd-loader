#nullable enable

using System;
using Mmd.Physics;

namespace Mmd.UnityIntegration
{
    internal sealed class MmdTimelineLivePhysicsTransfer : IDisposable
    {
        private MmdAnimPhysicsBackend? backend;

        internal MmdTimelineLivePhysicsTransfer(byte[] modelSource, MmdAnimPhysicsBackend backend)
        {
            ModelSource = modelSource;
            this.backend = backend;
        }

        internal byte[] ModelSource { get; }

        internal MmdAnimPhysicsBackend Take()
        {
            MmdAnimPhysicsBackend result = backend
                ?? throw new InvalidOperationException("Timeline physics transfer was already consumed.");
            backend = null;
            return result;
        }

        public void Dispose()
        {
            backend?.Dispose();
            backend = null;
        }
    }
}
