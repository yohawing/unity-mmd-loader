#nullable enable

using System;
using Mmd.Native;
using NUnit.Framework;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmdRuntimeNativeUnavailableBoundaryContractTests
    {
        private const string Operation = "standard VMD playback session";

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
