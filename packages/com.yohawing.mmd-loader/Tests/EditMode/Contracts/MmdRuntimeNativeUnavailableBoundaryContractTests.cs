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
        private const string PhysicalGateModeEnvironmentVariable = "MMD_NATIVE_PHYSICAL_GATE_MODE";
        private const string MissingDllMode = "MissingDll";
        private const string MissingEntryPointMode = "MissingEntryPoint";
        private const string AbiMismatchMode = "AbiMismatch";
        private const string PhysicalProbeOperation = "physical native runtime probe";
        private const string PhysicalAbiMismatchOperation = "physical native ABI mismatch probe";

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
            string nativeDllPath = GetPhysicalNativeDllPath(MissingDllMode);
            Assert.That(
                File.Exists(nativeDllPath),
                Is.False,
                "The physical missing-DLL gate requires the copied package DLL to be absent. " +
                "Use scripts/run-native-unavailable-gate.ps1 before enabling the physical gate. Path=" + nativeDllPath);

            MmdRuntimeNativeUnavailableException unavailable = Assert.Throws<MmdRuntimeNativeUnavailableException>(
                () => MmdRuntimeNativeBoundary.Invoke(
                    PhysicalProbeOperation,
                    MmdRuntimeFfiMethods.ValidateAbiVersion))!;

            Assert.That(unavailable.InnerException, Is.Not.Null);
            Assert.That(unavailable.InnerException!.Message, Is.Not.Null.And.Not.Empty);
            Assert.That(unavailable.InnerException, Is.TypeOf<DllNotFoundException>());
        }

        [Test]
        public void PhysicalMissingNativeEntryPointProbeClassifiesUnavailableRuntime()
        {
            string nativeDllPath = GetPhysicalNativeDllPath(MissingEntryPointMode);
            Assert.That(File.Exists(nativeDllPath), Is.True, "The physical entry-point gate requires a replacement DLL.");

            MmdRuntimeNativeUnavailableException unavailable = Assert.Throws<MmdRuntimeNativeUnavailableException>(
                () => MmdRuntimeNativeBoundary.Invoke(
                    PhysicalProbeOperation,
                    MmdRuntimeFfiMethods.ValidateAbiVersion))!;

            Assert.That(unavailable.InnerException, Is.TypeOf<EntryPointNotFoundException>());
            Assert.That(unavailable.Message, Does.Contain(PhysicalProbeOperation));
        }

        [Test]
        public void PhysicalAbiMismatchProbeClassifiesUnsupportedRuntime()
        {
            string nativeDllPath = GetPhysicalNativeDllPath(AbiMismatchMode);
            Assert.That(File.Exists(nativeDllPath), Is.True, "The physical ABI gate requires a replacement DLL.");

            uint observedAbiVersion = MmdRuntimeNativeBoundary.Invoke(
                PhysicalAbiMismatchOperation,
                MmdRuntimeFfiMethods.AbiVersion);
            Assert.That(observedAbiVersion, Is.Not.EqualTo(MmdRuntimeFfiMethods.ExpectedAbiVersion));

            MmdRuntimeUnsupportedException unsupported = Assert.Throws<MmdRuntimeUnsupportedException>(
                () => MmdRuntimeNativeBoundary.Invoke(
                    PhysicalAbiMismatchOperation,
                    MmdRuntimeFfiMethods.ValidateVmdSharedContextCapability))!;
            Assert.That(unsupported.Message, Does.Contain("ABI version"));
            Assert.That(unsupported.Message, Does.Contain("Expected"));
        }

        private static string GetPhysicalNativeDllPath(string expectedMode)
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable(PhysicalGateModeEnvironmentVariable),
                    expectedMode,
                    StringComparison.Ordinal))
            {
                Assert.Ignore(
                    "Set " + PhysicalGateModeEnvironmentVariable + "=" + expectedMode +
                    " to run the physical native gate.");
            }

#if !UNITY_EDITOR_WIN
            Assert.Fail("The physical native gate is Windows Editor only.");
            return string.Empty;
#else
            PackageInfo? package = PackageInfo.FindForAssembly(typeof(MmdRuntimeNativeUnavailableBoundaryContractTests).Assembly);
            Assert.That(package, Is.Not.Null, "The loader package must be resolvable for the physical native gate.");
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            Assert.That(projectRoot, Is.Not.Empty, "The Unity project root must be resolvable for the physical native gate.");
            string packagePath = package!.assetPath;
            if (!Path.IsPathRooted(packagePath))
            {
                packagePath = Path.Combine(projectRoot, packagePath);
            }
            packagePath = Path.GetFullPath(packagePath);
            Assert.That(Directory.Exists(packagePath), Is.True, "The resolved loader package directory must exist for the physical native gate.");
            string nativePluginDirectory = Path.GetFullPath(Path.Combine(
                packagePath,
                "Runtime",
                "Plugins",
                "x86_64"));
            Assert.That(nativePluginDirectory, Does.Exist, "The copied native plugin directory must exist for the physical native gate.");

            return Path.GetFullPath(Path.Combine(
                nativePluginDirectory,
                "mmd_runtime_ffi.dll"));
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
