#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Mmd.Editor
{
    public sealed class MmdPerformanceComparerOptions
    {
        public double MaxP95RegressionPercent { get; set; } = 10.0;
        public double MaxP99RegressionPercent { get; set; } = 10.0;
        public double MaxGcRegressionPercent { get; set; } = 10.0;
        public bool RequireChecksumMatch { get; set; } = true;
    }

    public static class MmdPerformanceBaselineComparer
    {
        public static MmdPerformanceComparisonResult Compare(
            MmdPerformanceBaselineReport baseline,
            MmdPerformanceBaselineReport candidate,
            MmdPerformanceComparerOptions? options = null)
        {
            if (baseline == null)
                throw new ArgumentNullException(nameof(baseline));
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));

            options ??= new MmdPerformanceComparerOptions();
            var result = new MmdPerformanceComparisonResult { passed = true };
            foreach (string error in MmdPerformanceBaseline.ValidateReport(baseline))
                AddViolation(result, "baseline report invalid: " + error);
            foreach (string error in MmdPerformanceBaseline.ValidateReport(candidate))
                AddViolation(result, "candidate report invalid: " + error);
            CheckTopLevel(result, baseline, candidate);
            CheckRequiredPhases(result, baseline, "baseline");
            CheckRequiredPhases(result, candidate, "candidate");

            if (options.RequireChecksumMatch &&
                (string.IsNullOrWhiteSpace(baseline.deterministicResultChecksum) ||
                 string.IsNullOrWhiteSpace(candidate.deterministicResultChecksum) ||
                 !string.Equals(baseline.deterministicResultChecksum, candidate.deterministicResultChecksum, StringComparison.OrdinalIgnoreCase)))
            {
                AddViolation(result, "deterministic result checksum mismatch.");
            }

            Dictionary<string, MmdPerformancePhaseReport> baselinePhases = UniquePhases(baseline);
            Dictionary<string, MmdPerformancePhaseReport> candidatePhases = UniquePhases(candidate);
            foreach (string name in MmdPerformanceBaseline.RequiredPhaseNames)
            {
                if (!baselinePhases.TryGetValue(name, out MmdPerformancePhaseReport? before) ||
                    !candidatePhases.TryGetValue(name, out MmdPerformancePhaseReport? after))
                {
                    continue;
                }

                double p95Regression = RegressionPercent(before.p95Ms, after.p95Ms);
                double p99Regression = RegressionPercent(before.p99Ms, after.p99Ms);
                double gcRegression = RegressionPercent(before.gcBytesPerFrame, after.gcBytesPerFrame);
                result.maxRegressionPercent = Math.Max(result.maxRegressionPercent, Math.Max(p95Regression, p99Regression));
                result.maxGcRegressionPercent = Math.Max(result.maxGcRegressionPercent, gcRegression);
                if (p95Regression > options.MaxP95RegressionPercent)
                    AddViolation(result, $"{name} p95 regression {p95Regression:F2}% exceeds {options.MaxP95RegressionPercent:F2}%.");
                if (p99Regression > options.MaxP99RegressionPercent)
                    AddViolation(result, $"{name} p99 regression {p99Regression:F2}% exceeds {options.MaxP99RegressionPercent:F2}%.");
                if (gcRegression > options.MaxGcRegressionPercent)
                    AddViolation(result, $"{name} GC regression {gcRegression:F2}% exceeds {options.MaxGcRegressionPercent:F2}%.");
            }

            result.reason = result.passed ? "baseline thresholds and checksum passed." : string.Join(" ", result.violations);
            return result;
        }

        private static void CheckTopLevel(
            MmdPerformanceComparisonResult result,
            MmdPerformanceBaselineReport baseline,
            MmdPerformanceBaselineReport candidate)
        {
            if (baseline.schemaVersion != MmdPerformanceBaseline.SchemaVersion ||
                candidate.schemaVersion != MmdPerformanceBaseline.SchemaVersion ||
                !string.Equals(baseline.schema, MmdPerformanceBaseline.SchemaName, StringComparison.Ordinal) ||
                !string.Equals(candidate.schema, MmdPerformanceBaseline.SchemaName, StringComparison.Ordinal))
                AddViolation(result, "unsupported performance baseline schema version.");
            if (!string.Equals(baseline.status, MmdPerformanceStatus.Pass, StringComparison.OrdinalIgnoreCase))
                AddViolation(result, "baseline status is " + baseline.status + ".");
            if (!string.Equals(candidate.status, MmdPerformanceStatus.Pass, StringComparison.OrdinalIgnoreCase))
                AddViolation(result, "candidate status is " + candidate.status + ".");
            if (baseline.warmupFrames != candidate.warmupFrames ||
                baseline.measurementFrames != candidate.measurementFrames ||
                Math.Abs(baseline.frameRate - candidate.frameRate) > 0.0001f)
                AddViolation(result, "frame configuration mismatch.");
            CheckChecksum(result, baseline.fixtureSha256, candidate.fixtureSha256, "PMX fixture");
            CheckChecksum(result, baseline.vmdFixtureSha256, candidate.vmdFixtureSha256, "VMD fixture");
            CheckChecksum(result, baseline.physicsFixtureSha256, candidate.physicsFixtureSha256, "physics fixture");
            if (string.IsNullOrWhiteSpace(baseline.backend) ||
                string.IsNullOrWhiteSpace(candidate.backend) ||
                !string.Equals(baseline.backend, candidate.backend, StringComparison.OrdinalIgnoreCase))
                AddViolation(result, "backend mismatch.");
        }

        private static void CheckRequiredPhases(MmdPerformanceComparisonResult result, MmdPerformanceBaselineReport report, string label)
        {
            if (report.phases == null)
            {
                AddViolation(result, label + " required phase list is missing.");
                return;
            }

            foreach (string name in MmdPerformanceBaseline.RequiredPhaseNames)
            {
                MmdPerformancePhaseReport[] matches = report.phases.Where(phase => phase != null && phase.name == name).ToArray();
                if (matches.Length == 0)
                    AddViolation(result, label + " required phase is missing: " + name + ".");
                else if (matches.Length > 1)
                    AddViolation(result, label + " required phase is duplicated: " + name + ".");
                else if (matches[0].status != MmdPerformanceStatus.Pass || matches[0].sampleCount <= 0 || matches[0].samplesMs == null || matches[0].samplesMs.Count == 0)
                    AddViolation(result, label + " required phase is not measured: " + name + ".");
            }
        }

        private static Dictionary<string, MmdPerformancePhaseReport> UniquePhases(MmdPerformanceBaselineReport report)
        {
            return (report.phases ?? new List<MmdPerformancePhaseReport>())
                .Where(phase => phase != null)
                .GroupBy(phase => phase.name, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        }

        private static void CheckChecksum(MmdPerformanceComparisonResult result, string baseline, string candidate, string label)
        {
            if (string.IsNullOrWhiteSpace(baseline) ||
                string.IsNullOrWhiteSpace(candidate) ||
                !string.Equals(baseline, candidate, StringComparison.OrdinalIgnoreCase))
                AddViolation(result, label + " checksum mismatch.");
        }

        private static double RegressionPercent(double baseline, double candidate)
        {
            if (baseline <= 0.0)
                return candidate > baseline ? 100.0 : 0.0;
            return Math.Max(0.0, ((candidate - baseline) / baseline) * 100.0);
        }

        private static void AddViolation(MmdPerformanceComparisonResult result, string violation)
        {
            result.passed = false;
            result.violations.Add(violation);
        }
    }
}
