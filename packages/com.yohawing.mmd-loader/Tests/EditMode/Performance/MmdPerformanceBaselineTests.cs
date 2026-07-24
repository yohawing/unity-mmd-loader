#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Mmd.Editor;

namespace Mmd.Tests.Performance
{
    public sealed class MmdPerformanceBaselineTests
    {
        [Test]
        public void DefaultsUseP0FrameRules()
        {
            var options = new MmdPerformanceBaselineOptions();

            Assert.That(options.warmupFrames, Is.EqualTo(5));
            Assert.That(options.measurementFrames, Is.EqualTo(120));
            Assert.That(options.frameRate, Is.EqualTo(30.0f));
            Assert.That(MmdPerformanceBaseline.SchemaVersion, Is.EqualTo(1));
            Assert.DoesNotThrow(() => MmdPerformanceBaseline.ValidateOptions(options));
        }

        [Test]
        public void RejectsShortWarmupAndNonP0Measurement()
        {
            var shortWarmup = new MmdPerformanceBaselineOptions { warmupFrames = 4 };
            Assert.Throws<ArgumentOutOfRangeException>(() => MmdPerformanceBaseline.ValidateOptions(shortWarmup));

            var variableFrameCount = new MmdPerformanceBaselineOptions { measurementFrames = 119 };
            Assert.Throws<ArgumentOutOfRangeException>(() => MmdPerformanceBaseline.ValidateOptions(variableFrameCount));
        }

        [Test]
        public void PercentileUsesSortedLinearInterpolation()
        {
            var samples = new List<double> { 4.0, 1.0, 3.0, 2.0 };

            Assert.That(MmdPerformanceBaseline.Percentile(samples, 0.50), Is.EqualTo(2.5).Within(1e-9));
            Assert.That(MmdPerformanceBaseline.Percentile(samples, 0.95), Is.EqualTo(3.85).Within(1e-9));
            Assert.That(MmdPerformanceBaseline.Percentile(samples, 0.99), Is.EqualTo(3.97).Within(1e-9));
        }

        [Test]
        public void ComparerRejectsIntentionalDelay()
        {
            var baseline = ReportWithAllRequiredPhases(10.0);
            var candidate = ReportWithAllRequiredPhases(10.0);
            ReplacePhase(candidate, "native-evaluate-copy", 13.0);

            MmdPerformanceComparisonResult result = MmdPerformanceBaselineComparer.Compare(
                baseline,
                candidate,
                new MmdPerformanceComparerOptions { MaxP95RegressionPercent = 10.0, MaxP99RegressionPercent = 10.0 });

            Assert.That(result.passed, Is.False);
            Assert.That(result.reason, Does.Contain("regression"));
        }

        [Test]
        public void ComparerRejectsChecksumMismatch()
        {
            var baseline = ReportWithAllRequiredPhases(10.0);
            var candidate = ReportWithAllRequiredPhases(10.0);
            baseline.deterministicResultChecksum = "aaa";
            candidate.deterministicResultChecksum = "bbb";

            MmdPerformanceComparisonResult result = MmdPerformanceBaselineComparer.Compare(baseline, candidate);

            Assert.That(result.passed, Is.False);
            Assert.That(result.reason, Does.Contain("checksum"));
        }

        [Test]
        public void ComparerAcceptsEquivalentCompleteReports()
        {
            var baseline = ReportWithAllRequiredPhases(10.0);
            var candidate = ReportWithAllRequiredPhases(10.0);

            Assert.That(MmdPerformanceBaselineComparer.Compare(baseline, candidate).passed, Is.True);
        }

        [Test]
        public void SkipPhaseIsNeverPass()
        {
            MmdPerformancePhaseReport phase = MmdPerformanceBaseline.SkipPhase("live-physics-total", "fixture missing");
            var candidate = new MmdPerformanceBaselineReport
            {
                status = MmdPerformanceStatus.Skip,
                phases = new List<MmdPerformancePhaseReport> { phase },
            };
            var baseline = ReportWithAllRequiredPhases(10.0);

            MmdPerformanceComparisonResult result = MmdPerformanceBaselineComparer.Compare(baseline, candidate);

            Assert.That(phase.status, Is.EqualTo(MmdPerformanceStatus.Skip));
            Assert.That(result.passed, Is.False);
            Assert.That(result.reason, Does.Contain("candidate status"));
        }

        [Test]
        public void ComparerRejectsOmittedRequiredPhase()
        {
            var baseline = ReportWithAllRequiredPhases(10.0);
            var candidate = ReportWithAllRequiredPhases(10.0);
            candidate.phases.RemoveAll(phase => phase.name == "vmd-load-parse");

            MmdPerformanceComparisonResult result = MmdPerformanceBaselineComparer.Compare(baseline, candidate);

            Assert.That(result.passed, Is.False);
            Assert.That(result.reason, Does.Contain("required phase is missing"));
        }

        [Test]
        public void ComparerRejectsSkippedRequiredPhase()
        {
            var baseline = ReportWithAllRequiredPhases(10.0);
            var candidate = ReportWithAllRequiredPhases(10.0);
            candidate.phases.Find(phase => phase.name == "live-physics-total")!.status = MmdPerformanceStatus.Skip;

            MmdPerformanceComparisonResult result = MmdPerformanceBaselineComparer.Compare(baseline, candidate);

            Assert.That(result.passed, Is.False);
            Assert.That(result.reason, Does.Contain("required phase is not measured"));
        }

        [Test]
        public void ChecksumIsDeterministicForEquivalentNativeBuffers()
        {
            ulong first = MmdPerformanceBaseline.ChecksumSeed;
            ulong second = MmdPerformanceBaseline.ChecksumSeed;
            var matrices = new[] { 1.0f, 0.0f, 0.0f, 1.0f };
            MmdPerformanceBaseline.MixChecksum(ref first, 4, matrices, new[] { 0.5f }, new byte[] { 1 });
            MmdPerformanceBaseline.MixChecksum(ref second, 4, matrices, new[] { 0.5f }, new byte[] { 1 });

            Assert.That(MmdPerformanceBaseline.FinishChecksum(first), Is.EqualTo(MmdPerformanceBaseline.FinishChecksum(second)));
        }

        private static MmdPerformanceBaselineReport ReportWithAllRequiredPhases(double value)
        {
            var report = new MmdPerformanceBaselineReport
            {
                status = MmdPerformanceStatus.Pass,
                deterministicResultChecksum = "same",
                fixtureSha256 = "pmx",
                vmdFixtureSha256 = "vmd",
                physicsFixtureSha256 = "physics",
                backend = "mmd-runtime-ffi",
            };
            foreach (string name in MmdPerformanceBaseline.RequiredPhaseNames)
                report.phases.Add(MmdPerformanceBaseline.BuildPhase(name, new[] { value, value, value }, 0, 3));
            return report;
        }

        private static void ReplacePhase(MmdPerformanceBaselineReport report, string name, double value)
        {
            int index = report.phases.FindIndex(phase => phase.name == name);
            report.phases[index] = MmdPerformanceBaseline.BuildPhase(name, new[] { value, value, value }, 0, 3);
        }
    }
}
