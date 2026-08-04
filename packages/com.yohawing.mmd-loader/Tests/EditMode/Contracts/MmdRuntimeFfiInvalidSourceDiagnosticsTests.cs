#nullable enable

using System;
using System.Text;
using Mmd.Native;
using NUnit.Framework;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmdRuntimeFfiInvalidSourceDiagnosticsTests
    {
        [Test]
        public void InvalidVmdReportsNativeDiagnosticInsteadOfBareNullClip()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            byte[] invalidVmdBytes = Encoding.ASCII.GetBytes("not a VMD");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => MmdRuntimeFfiPlaybackSession.Create(pmxBytes, invalidVmdBytes))!;

            Assert.That(exception.Message, Does.Contain("mmd-runtime VMD import returned a null clip:"));
            Assert.That(exception.Message, Does.Not.EndWith("null clip."));
        }
    }
}
