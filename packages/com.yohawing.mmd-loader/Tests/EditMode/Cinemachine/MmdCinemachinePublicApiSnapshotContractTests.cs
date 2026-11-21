#nullable enable

using System.IO;
using Mmd.UnityIntegration.Cinemachine;
using NUnit.Framework;

namespace Mmd.Tests.Cinemachine
{
    /// <summary>
    /// Pins the public/protected API surface of the optional Mmd.Cinemachine
    /// assembly. This assembly only compiles when Cinemachine 3 is installed
    /// (MMD_CINEMACHINE_3), so the fixture is absent rather than skipped in
    /// projects without Cinemachine. See <see cref="PublicApiBaselineAssert"/>
    /// for the baseline update workflow.
    /// </summary>
    [TestFixture]
    public sealed class MmdCinemachinePublicApiSnapshotContractTests
    {
        private static string BaselinePath =>
            Path.Combine(MmdTestFixtures.PackageRoot, "Tests", "EditMode", "Cinemachine", "PublicApi", "Mmd.Cinemachine.api.txt");

        [Test]
        public void CinemachinePublicApiMatchesCommittedBaseline()
        {
            PublicApiBaselineAssert.AssertMatchesBaseline(typeof(MmdCinemachineCameraBinding).Assembly, BaselinePath);
        }

        [Test]
        public void CinemachineAssemblyGrantsNoInternalsVisibleTo()
        {
            object[] grants = typeof(MmdCinemachineCameraBinding).Assembly
                .GetCustomAttributes(typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute), false);
            Assert.That(grants, Is.Empty, "Mmd.Cinemachine must not widen its API through InternalsVisibleTo.");
        }
    }
}
