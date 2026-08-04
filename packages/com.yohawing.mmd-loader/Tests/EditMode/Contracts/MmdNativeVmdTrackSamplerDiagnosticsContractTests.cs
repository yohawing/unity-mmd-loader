#nullable enable

using System;
using NUnit.Framework;
using Mmd.Native;
using Mmd.Motion;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmdNativeVmdTrackSamplerDiagnosticsContractTests
    {
        [Test]
        public void InvalidBytesReturnNonEmptyStableReasonsForAllSceneTrackSamplers()
        {
            byte[] invalidBytes = { 0, 1, 2, 3 };

            Assert.That(
                NativeVmdCameraTrackSampler.TryCreate(invalidBytes, out NativeVmdCameraTrackSampler? camera, out string cameraReason),
                Is.False);
            Assert.That(camera, Is.Null);
            Assert.That(cameraReason, Is.Not.Null.And.Not.Empty);

            Assert.That(
                NativeVmdLightTrackSampler.TryCreate(invalidBytes, out NativeVmdLightTrackSampler? light, out string lightReason),
                Is.False);
            Assert.That(light, Is.Null);
            Assert.That(lightReason, Is.Not.Null.And.Not.Empty);

            Assert.That(
                NativeVmdSelfShadowTrackSampler.TryCreate(
                    invalidBytes,
                    out NativeVmdSelfShadowTrackSampler? selfShadow,
                    out string selfShadowReason),
                Is.False);
            Assert.That(selfShadow, Is.Null);
            Assert.That(selfShadowReason, Is.Not.Null.And.Not.Empty);

            Assert.That(
                NativeVmdCameraTrackSampler.TryCreate(
                    invalidBytes,
                    out NativeVmdCameraTrackSampler? secondCamera,
                    out string secondCameraReason),
                Is.False);
            Assert.That(secondCameraReason, Is.EqualTo(cameraReason));
        }

        [TestCase("dll", "native DLL is unavailable")]
        [TestCase("entry-point", "native entry point is unavailable")]
        [TestCase("image", "native DLL has an incompatible image format")]
        [TestCase("unsupported", "native runtime does not support this track")]
        [TestCase("operation", "native track sample operation failed")]
        public void TrySampleConvertsExpectedNativeBoundaryFailuresToStableDiagnostics(
            string failureKind,
            string expectedReason)
        {
            Exception exception = CreateExpectedBoundaryException(failureKind);
            var sampler = new TestSampler(
                _ => throw exception,
                _ => { });
            try
            {
                Assert.That(sampler.TrySample(0.0f, out int state), Is.False);
                Assert.That(state, Is.EqualTo(TestSampler.DefaultStateValue));
                Assert.That(sampler.LastFailureReason, Does.StartWith(expectedReason));
                if (failureKind == "operation")
                {
                    Assert.That(sampler.LastFailureReason, Does.Contain("machine-specific native operation detail"));
                }
            }
            finally
            {
                sampler.Dispose();
            }
        }

        [Test]
        public void TrySampleRejectsNonFiniteNativeValuesAndDoesNotExposeState()
        {
            var sampler = new TestSampler(
                values =>
                {
                    values[0] = float.NaN;
                    return 1;
                },
                _ => { });
            try
            {
                Assert.That(sampler.TrySample(0.0f, out int state), Is.False);
                Assert.That(state, Is.EqualTo(TestSampler.DefaultStateValue));
                Assert.That(sampler.LastFailureReason, Is.EqualTo("native track sample returned invalid data"));
            }
            finally
            {
                sampler.Dispose();
            }
        }

        [Test]
        public void TerminalNativeSampleFailureIsLatchedAndDoesNotRetryEveryFrame()
        {
            int sampleCount = 0;
            var sampler = new TestSampler(
                _ =>
                {
                    sampleCount++;
                    return 0;
                },
                _ => { });
            try
            {
                Assert.That(sampler.TrySample(0.0f, out _), Is.False);
                Assert.That(sampler.TrySample(1.0f, out _), Is.False);
                Assert.That(sampleCount, Is.EqualTo(1));
                Assert.That(sampler.LastFailureReason, Is.EqualTo("native track sample returned false"));
            }
            finally
            {
                sampler.Dispose();
            }
        }

        [Test]
        public void TrySampleDoesNotHideUnexpectedExceptions()
        {
            var sampler = new TestSampler(
                _ => throw new ArgumentException("programmer error"),
                _ => { });
            try
            {
                Assert.Throws<ArgumentException>(() => sampler.TrySample(0.0f, out _));
            }
            finally
            {
                sampler.Dispose();
            }
        }

        [Test]
        public void DisposeCallsFreeAtMostOnceAndSubsequentSampleFailsClosed()
        {
            int freeCount = 0;
            var sampler = new TestSampler(_ => 1, _ => freeCount++);

            sampler.Dispose();
            sampler.Dispose();

            Assert.That(freeCount, Is.EqualTo(1));
            Assert.That(sampler.TrySample(0.0f, out int state), Is.False);
            Assert.That(state, Is.EqualTo(TestSampler.DefaultStateValue));
            Assert.That(sampler.LastFailureReason, Is.EqualTo("sampler is disposed"));
        }

        [Test]
        public void DisposeRetainsNativeHandleWhenFreeFailsSoRetryCanSucceed()
        {
            int freeCount = 0;
            bool failFirstFree = true;
            var sampler = new TestSampler(
                _ => 1,
                _ =>
                {
                    freeCount++;
                    if (failFirstFree)
                    {
                        failFirstFree = false;
                        throw new InvalidOperationException("transient native cleanup failure");
                    }
                });

            Assert.Throws<InvalidOperationException>(() => sampler.Dispose());
            sampler.Dispose();

            Assert.That(freeCount, Is.EqualTo(2));
            Assert.That(sampler.TrySample(0.0f, out int state), Is.False);
            Assert.That(state, Is.EqualTo(TestSampler.DefaultStateValue));
            Assert.That(sampler.LastFailureReason, Is.EqualTo("sampler is disposed"));
        }

        private static Exception CreateExpectedBoundaryException(string failureKind)
        {
            switch (failureKind)
            {
                case "dll":
                    return new DllNotFoundException("machine-specific DLL path");
                case "entry-point":
                    return new EntryPointNotFoundException("machine-specific symbol");
                case "image":
                    return new BadImageFormatException("machine-specific image detail");
                case "unsupported":
                    return new MmdRuntimeUnsupportedException("machine-specific native detail");
                case "operation":
                    return new InvalidOperationException("machine-specific native operation detail");
                default:
                    throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, null);
            }
        }

        private sealed class TestSampler : NativeVmdTrackSampler<int>
        {
            internal const int DefaultStateValue = -1;

            private readonly Func<float[], byte> sample;

            internal TestSampler(Func<float[], byte> sample, Action<IntPtr> freeTrack)
                : base(new IntPtr(1), 1, 1, freeTrack)
            {
                this.sample = sample;
            }

            protected override int DefaultState => DefaultStateValue;

            protected override byte SampleTrack(IntPtr track, float frame, float[] values, IntPtr valueCount)
            {
                return sample(values);
            }

            protected override int ToState(float[] values)
            {
                return (int)values[0];
            }
        }
    }
}
