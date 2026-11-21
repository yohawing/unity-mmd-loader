#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mmd.UnityIntegration.Cinemachine;
using NUnit.Framework;

namespace Mmd.Tests.Cinemachine
{
    /// <summary>
    /// Pins the serialized fields and MonoScript GUIDs of the optional
    /// Mmd.Cinemachine assembly. Compiles only when Cinemachine 3 is installed
    /// (MMD_CINEMACHINE_3), so it is absent rather than skipped elsewhere.
    /// See <see cref="PublicApiBaselineAssert"/> for the baseline update workflow.
    /// </summary>
    [TestFixture]
    public sealed class MmdCinemachineSerializedSurfaceSnapshotContractTests
    {
        private static Assembly CinemachineAssembly => typeof(MmdCinemachineCameraBinding).Assembly;

        private static string BaselineRoot =>
            Path.Combine(MmdTestFixtures.PackageRoot, "Tests", "EditMode", "Cinemachine", "PublicApi");

        [Test]
        public void CinemachineSerializedFieldsMatchCommittedBaseline()
        {
            List<string> actual = SerializedFieldSnapshot.Build(new[] { CinemachineAssembly });
            Assert.That(actual, Is.Not.Empty, "No serialized types were found in Mmd.Cinemachine.");
            PublicApiBaselineAssert.AssertLinesMatchBaseline(
                "Serialized fields of Mmd.Cinemachine",
                actual,
                Path.Combine(BaselineRoot, "Mmd.Cinemachine.serialized-fields.txt"),
                "Mmd.Cinemachine.serialized-fields.txt");
        }

        [Test]
        public void CinemachineMonoScriptGuidsMatchCommittedBaseline()
        {
            List<string> actual = MonoScriptGuidSnapshot.Build(new[] { CinemachineAssembly.GetName().Name });
            Assert.That(actual, Is.Not.Empty, "No MonoScript resolved to a class in Mmd.Cinemachine.");
            PublicApiBaselineAssert.WriteCandidateIfRequested(
                "Mmd.Cinemachine.monoscript-unmapped-types.txt",
                MonoScriptGuidSnapshot.FindUnityObjectTypesWithoutScript(new[] { CinemachineAssembly }));
            PublicApiBaselineAssert.AssertLinesMatchBaseline(
                "MonoScript GUIDs of Mmd.Cinemachine",
                actual,
                Path.Combine(BaselineRoot, "Mmd.Cinemachine.monoscript-guids.txt"),
                "Mmd.Cinemachine.monoscript-guids.txt");
        }
    }
}
