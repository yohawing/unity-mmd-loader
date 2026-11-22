#nullable enable

using Mmd.Physics;
using NUnit.Framework;

namespace Mmd.Tests
{
    /// <summary>
    /// Skips a test when the mmd-anim Bullet live physics backend cannot be loaded.
    /// MmdAnimPhysicsBackend is internal to Mmd.Runtime and is visible only to the test
    /// assemblies (not to Mmd.EditModeTestKit), so each test assembly keeps this copy.
    /// </summary>
    internal static class MmdBulletBackendTestGuard
    {
        internal static void RequireBulletBackend()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }
        }
    }
}
