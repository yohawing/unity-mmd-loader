#nullable enable

using System;
using System.IO;
using Mmd.Native;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmdRuntimeNativeUnavailableBoundaryContractTests
    {
        private const string Operation = "standard VMD playback session";
        private const string PhysicalUnavailableGateEnvironmentVariable =
            "MMD_NATIVE_PHYSICAL_UNAVAILABLE_GATE";
        private const string PhysicalProbeOperation = "physical native runtime probe";

        [Test]
        public void MissingNativeDllIsClassifiedAsUnavailableWithStableOperation()
        {
            AssertUnavailable(new DllNotFoundException("missing native DLL"));
        }

        [Test]
        public void MissingNativeEntryPointIsClassifiedAsUnavailable()
        {
            AssertUnavailable(new EntryPointNotFoundException("missing native entry point"));
        }

        [Test]
        public void BadNativeImageIsClassifiedAsUnavailable()
        {
            AssertUnavailable(new BadImageFormatException("wrong native image"));
        }

        [Test]
        public void InvalidAndUnsupportedFailuresPassThroughTheBoundary()
        {
            var invalid = new InvalidOperationException("invalid native parse");
            InvalidOperationException invalidResult = Assert.Throws<InvalidOperationException>(
                () => MmdRuntimeNativeBoundary.Invoke<object>(Operation, () => throw invalid))!;
            Assert.That(invalidResult, Is.SameAs(invalid));

            var unsupported = new MmdRuntimeUnsupportedException("feature unsupported");
            MmdRuntimeUnsupportedException unsupportedResult = Assert.Throws<MmdRuntimeUnsupportedException>(
                () => MmdRuntimeNativeBoundary.Invoke<object>(Operation, () => throw unsupported))!;
            Assert.That(unsupportedResult, Is.SameAs(unsupported));
        }

        [Test]
        public void PhysicalMissingNativeDllProbeClassifiesUnavailableRuntime()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable(PhysicalUnavailableGateEnvironmentVariable),
                    "1",
                    StringComparison.Ordinal))
            {
                Assert.Ignore(
                    "Set " + PhysicalUnavailableGateEnvironmentVariable + "=1 to run the physical native unavailable gate.");
            }

#if !UNITY_EDITOR_WIN
            Assert.Fail("The physical native unavailable gate is Windows Editor only.");
#else
            PackageInfo? package = PackageInfo.FindForAssembly(typeof(MmdRuntimeNativeUnavailableBoundaryContractTests).Assembly);
            Assert.That(package, Is.Not.Null, "The loader package must be resolvable for the physical native unavailable gate.");
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            Assert.That(projectRoot, Is.Not.Empty, "The Unity project root must be resolvable for the physical native unavailable gate.");
            string packagePath = package!.assetPath;
            if (!Path.IsPathRooted(packagePath))
            {
                packagePath = Path.Combine(projectRoot, packagePath);
            }
            string nativeDllPath = Path.GetFullPath(Path.Combine(
                packagePath,
                "Runtime",
                "Plugins",
                "x86_64",
                "mmd_runtime_ffi.dll"));
            Assert.That(
                File.Exists(nativeDllPath),
                Is.False,
                "The physical missing-DLL gate requires the copied package DLL to be absent. " +
                "Use scripts/run-native-unavailable-gate.ps1 before enabling " +
                PhysicalUnavailableGateEnvironmentVariable + ". Path=" + nativeDllPath);

            MmdRuntimeNativeUnavailableException unavailable = Assert.Throws<MmdRuntimeNativeUnavailableException>(
                () => MmdRuntimeNativeBoundary.Invoke(
                    PhysicalProbeOperation,
                    MmdRuntimeFfiMethods.ValidateAbiVersion))!;

            Assert.That(unavailable.InnerException, Is.Not.Null);
            Assert.That(unavailable.InnerException!.Message, Is.Not.Null.And.Not.Empty);
            Assert.That(
                unavailable.InnerException,
                    Is.TypeOf<DllNotFoundException>()
                    .Or.TypeOf<EntryPointNotFoundException>()
                    .Or.TypeOf<BadImageFormatException>());
#endif
        }

        private static void AssertUnavailable(Exception nativeException)
        {
            MmdRuntimeNativeUnavailableException unavailable = Assert.Throws<MmdRuntimeNativeUnavailableException>(
                () => MmdRuntimeNativeBoundary.Invoke<object>(Operation, () => throw nativeException))!;

            Assert.That(unavailable, Is.TypeOf<MmdRuntimeNativeUnavailableException>());
            Assert.That(
                unavailable.Message,
                Is.EqualTo("mmd-runtime native is unavailable for " + Operation + ": " + nativeException.Message));
            Assert.That(unavailable.InnerException, Is.SameAs(nativeException));
        }
    }
}
