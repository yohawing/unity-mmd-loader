#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Mmd.Editor
{
    public static class MmdPerformanceBaseline
    {
        public const int SchemaVersion = 3;
        public const int DefaultWarmupFrames = 5;
        public const int DefaultMeasurementFrames = 120;
        public const float DefaultFrameRate = 30.0f;
        public const string SchemaName = "mmd-performance-baseline";
        public const ulong ChecksumSeed = 14695981039346656037UL;

        public static readonly IReadOnlyList<string> LivePhysicsPhaseNames = new[]
        {
            "live-physics-total",
            "live-physics-evaluate",
            "live-physics-sync",
            "live-physics-step",
            "live-physics-apply",
        };

        public static readonly IReadOnlyList<string> RequiredPhaseNames = new[]
        {
            "pmx-load",
            "pmx-parse",
            "vmd-load-parse",
            "unity-asset-build",
            "native-evaluate-copy",
            "unity-pose-morph-apply",
        }.Concat(LivePhysicsPhaseNames).ToArray();

        public static double Percentile(IReadOnlyList<double> samples, double percentile)
        {
            if (samples == null || samples.Count == 0)
                throw new ArgumentException("At least one sample is required.", nameof(samples));
            if (double.IsNaN(percentile) || percentile < 0.0 || percentile > 1.0)
                throw new ArgumentOutOfRangeException(nameof(percentile));

            double[] sorted = samples.OrderBy(value => value).ToArray();
            if (sorted.Length == 1)
                return sorted[0];
            double position = percentile * (sorted.Length - 1);
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper)
                return sorted[lower];
            return sorted[lower] + ((sorted[upper] - sorted[lower]) * (position - lower));
        }

        public static MmdPerformancePhaseReport BuildPhase(
            string name,
            IReadOnlyList<double> samplesMs,
            long allocatedBytes,
            int frameCount,
            string reason = "")
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Phase name is required.", nameof(name));

            var phase = new MmdPerformancePhaseReport
            {
                name = name,
                status = MmdPerformanceStatus.Pass,
                reason = reason ?? string.Empty,
                samplesMs = samplesMs == null ? new List<double>() : new List<double>(samplesMs),
                sampleCount = samplesMs?.Count ?? 0,
                gcBytesPerFrame = frameCount > 0 ? Math.Max(0L, allocatedBytes) / (double)frameCount : 0.0,
            };
            if (phase.samplesMs.Count == 0)
            {
                phase.status = MmdPerformanceStatus.Unavailable;
                phase.reason = string.IsNullOrWhiteSpace(phase.reason) ? "No samples were recorded." : phase.reason;
                return phase;
            }

            phase.p50Ms = Percentile(phase.samplesMs, 0.50);
            phase.p95Ms = Percentile(phase.samplesMs, 0.95);
            phase.p99Ms = Percentile(phase.samplesMs, 0.99);
            return phase;
        }

        public static MmdPerformancePhaseReport BuildTimingPhase(
            string name,
            IReadOnlyList<double> samplesMs,
            int frameCount,
            string reason = "")
        {
            MmdPerformancePhaseReport phase = BuildPhase(name, samplesMs, 0, frameCount, reason);
            phase.gcBytesMeasured = false;
            return phase;
        }

        public static MmdPerformancePhaseReport UnavailablePhase(string name, string reason)
        {
            return new MmdPerformancePhaseReport
            {
                name = name,
                status = MmdPerformanceStatus.Unavailable,
                reason = string.IsNullOrWhiteSpace(reason) ? "Phase is not independently observable." : reason,
                samplesMs = new List<double>(),
            };
        }

        public static MmdPerformancePhaseReport SkipPhase(string name, string reason)
        {
            return new MmdPerformancePhaseReport
            {
                name = name,
                status = MmdPerformanceStatus.Skip,
                reason = string.IsNullOrWhiteSpace(reason) ? "Phase was skipped." : reason,
                samplesMs = new List<double>(),
            };
        }

        public static MmdPerformancePhaseReport ErrorPhase(string name, string reason)
        {
            return new MmdPerformancePhaseReport
            {
                name = name,
                status = MmdPerformanceStatus.Error,
                reason = string.IsNullOrWhiteSpace(reason) ? "Phase failed." : reason,
                samplesMs = new List<double>(),
            };
        }

        public static void MixChecksum(ref ulong state, int frame, float[] worldMatrices, float[] morphWeights, byte[] ikEnabled)
        {
            MixChecksum(ref state, frame);
            foreach (float value in worldMatrices)
                MixChecksum(ref state, BitConverter.SingleToInt32Bits(value));
            foreach (float value in morphWeights)
                MixChecksum(ref state, BitConverter.SingleToInt32Bits(value));
            foreach (byte value in ikEnabled)
                MixChecksum(ref state, value);
        }

        public static string FinishChecksum(ulong state) => state.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);

        private static void MixChecksum(ref ulong state, int value)
        {
            unchecked
            {
                state ^= (uint)value;
                state *= 1099511628211UL;
                state ^= (uint)(value >> 16);
                state *= 1099511628211UL;
            }
        }

        public static void ValidateOptions(MmdPerformanceBaselineOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (options.warmupFrames < DefaultWarmupFrames)
                throw new ArgumentOutOfRangeException(nameof(options.warmupFrames), "Warmup must be at least 5 frames.");
            if (options.measurementFrames != DefaultMeasurementFrames)
                throw new ArgumentOutOfRangeException(nameof(options.measurementFrames), "P0 measurement is fixed at 120 frames.");
            if (options.frameRate <= 0.0f || float.IsNaN(options.frameRate) || float.IsInfinity(options.frameRate))
                throw new ArgumentOutOfRangeException(nameof(options.frameRate));
        }

        /// <summary>
        /// Validates the serializable report contract before a report is used as a
        /// baseline or published as a performance result.  The comparer also runs
        /// this validation so malformed JSON cannot silently turn into a green gate.
        /// </summary>
        public static IReadOnlyList<string> ValidateReport(MmdPerformanceBaselineReport? report)
        {
            var errors = new List<string>();
            if (report == null)
            {
                errors.Add("report is null.");
                return errors;
            }

            if (report.schemaVersion != SchemaVersion)
                errors.Add("unsupported performance baseline schema version.");
            if (!string.Equals(report.schema, SchemaName, StringComparison.Ordinal))
                errors.Add("performance baseline schema name is missing or unsupported.");
            if (!IsKnownStatus(report.status))
                errors.Add("report status is missing or unsupported: " + report.status + ".");
            if (report.warmupFrames < DefaultWarmupFrames)
                errors.Add("warmupFrames must be at least " + DefaultWarmupFrames + ".");
            if (report.measurementFrames != DefaultMeasurementFrames)
                errors.Add("measurementFrames must be exactly " + DefaultMeasurementFrames + ".");
            if (report.frameRate <= 0.0f || float.IsNaN(report.frameRate) || float.IsInfinity(report.frameRate))
                errors.Add("frameRate must be finite and greater than zero.");

            if (string.Equals(report.status, MmdPerformanceStatus.Pass, StringComparison.OrdinalIgnoreCase))
            {
                RequireText(errors, report.fixtureSha256, "fixtureSha256");
                RequireText(errors, report.vmdFixtureSha256, "vmdFixtureSha256");
                RequireText(errors, report.physicsFixtureSha256, "physicsFixtureSha256");
                RequireText(errors, report.backend, "backend");
                RequireText(errors, report.deterministicResultChecksum, "deterministicResultChecksum");
            }

            if (report.phases == null)
            {
                errors.Add("phase list is missing.");
                return errors;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (MmdPerformancePhaseReport? phase in report.phases)
            {
                if (phase == null)
                {
                    errors.Add("phase list contains a null entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(phase.name))
                    errors.Add("phase name is missing.");
                else if (!names.Add(phase.name))
                    errors.Add("phase is duplicated: " + phase.name + ".");
                if (!IsKnownStatus(phase.status))
                    errors.Add("phase status is missing or unsupported: " + phase.name + ".");
                if (phase.sampleCount < 0)
                    errors.Add("phase sampleCount is negative: " + phase.name + ".");
                if (phase.samplesMs == null)
                {
                    errors.Add("phase samples are missing: " + phase.name + ".");
                    continue;
                }
                if (phase.sampleCount != phase.samplesMs.Count)
                    errors.Add("phase sampleCount does not match samplesMs: " + phase.name + ".");

                foreach (double sample in phase.samplesMs)
                    if (double.IsNaN(sample) || double.IsInfinity(sample) || sample < 0.0)
                        errors.Add("phase contains an invalid timing sample: " + phase.name + ".");

                if (string.Equals(phase.status, MmdPerformanceStatus.Pass, StringComparison.OrdinalIgnoreCase))
                {
                    if (phase.sampleCount <= 0)
                        errors.Add("PASS phase has no samples: " + phase.name + ".");
                    if (phase.sampleCount != report.measurementFrames)
                        errors.Add("PASS phase sampleCount does not match measurementFrames: " + phase.name + ".");
                    ValidateMetric(errors, phase.p50Ms, phase.name, "p50Ms");
                    ValidateMetric(errors, phase.p95Ms, phase.name, "p95Ms");
                    ValidateMetric(errors, phase.p99Ms, phase.name, "p99Ms");
                    if (phase.gcBytesMeasured)
                        ValidateMetric(errors, phase.gcBytesPerFrame, phase.name, "gcBytesPerFrame");
                    if (phase.p50Ms > phase.p95Ms || phase.p95Ms > phase.p99Ms)
                        errors.Add("phase percentile ordering is invalid: " + phase.name + ".");
                }
            }

            if (string.Equals(report.status, MmdPerformanceStatus.Pass, StringComparison.OrdinalIgnoreCase))
            {
                foreach (string requiredName in RequiredPhaseNames)
                {
                    MmdPerformancePhaseReport[] matches = report.phases.FindAll(phase => phase != null && phase.name == requiredName).ToArray();
                    if (matches.Length == 0)
                        errors.Add("required phase is missing: " + requiredName + ".");
                    else if (matches.Length == 1 && !string.Equals(matches[0].status, MmdPerformanceStatus.Pass, StringComparison.OrdinalIgnoreCase))
                        errors.Add("required phase is not PASS: " + requiredName + ".");
                }
            }

            return errors;
        }

        private static bool IsKnownStatus(string? status)
        {
            return string.Equals(status, MmdPerformanceStatus.Pass, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, MmdPerformanceStatus.Fail, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, MmdPerformanceStatus.Skip, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, MmdPerformanceStatus.Unavailable, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, MmdPerformanceStatus.Error, StringComparison.OrdinalIgnoreCase);
        }

        private static void RequireText(List<string> errors, string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                errors.Add(fieldName + " is required for a PASS report.");
        }

        private static void ValidateMetric(List<string> errors, double value, string phaseName, string metricName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
                errors.Add("phase metric is invalid: " + phaseName + "." + metricName + ".");
        }

    }

    public static class MmdPerformanceStatus
    {
        public const string Pass = "PASS";
        public const string Fail = "FAIL";
        public const string Skip = "SKIP";
        public const string Unavailable = "UNAVAILABLE";
        public const string Error = "ERROR";
    }

    [Serializable]
    public sealed class MmdPerformancePhaseReport
    {
        public string name = string.Empty;
        public string status = MmdPerformanceStatus.Unavailable;
        public string reason = string.Empty;
        public int sampleCount;
        public List<double> samplesMs = new();
        public double p50Ms;
        public double p95Ms;
        public double p99Ms;
        public double gcBytesPerFrame;
        public bool gcBytesMeasured = true;
    }

    [Serializable]
    public sealed class MmdPerformanceComparisonResult
    {
        public bool passed;
        public string reason = string.Empty;
        public List<string> violations = new();
        public double maxRegressionPercent;
        public double maxGcRegressionPercent;
    }

    [Serializable]
    public sealed class MmdPerformanceBaselineReport
    {
        public int schemaVersion = MmdPerformanceBaseline.SchemaVersion;
        public string schema = MmdPerformanceBaseline.SchemaName;
        public string status = MmdPerformanceStatus.Skip;
        public string skipReason = string.Empty;
        public string generatedUtc = string.Empty;
        public string fixtureSha256 = string.Empty;
        public string vmdFixtureSha256 = string.Empty;
        public string physicsFixtureSha256 = string.Empty;
        public string unityVersion = string.Empty;
        public string packageHead = string.Empty;
        public string mmdAnimRevision = string.Empty;
        public string mmdAnimAbi = string.Empty;
        public string backend = string.Empty;
        public string cpu = string.Empty;
        public int warmupFrames = MmdPerformanceBaseline.DefaultWarmupFrames;
        public int measurementFrames = MmdPerformanceBaseline.DefaultMeasurementFrames;
        public float frameRate = MmdPerformanceBaseline.DefaultFrameRate;
        public string deterministicResultChecksum = string.Empty;
        public List<MmdPerformancePhaseReport> phases = new();
        public MmdPerformanceComparisonResult? comparison;
    }

    public sealed class MmdPerformanceBaselineOptions
    {
        public string repoRoot = string.Empty;
        public string pmxPath = string.Empty;
        public string vmdPath = string.Empty;
        public string physicsPmxPath = string.Empty;
        public string baselinePath = string.Empty;
        public int warmupFrames = MmdPerformanceBaseline.DefaultWarmupFrames;
        public int measurementFrames = MmdPerformanceBaseline.DefaultMeasurementFrames;
        public float frameRate = MmdPerformanceBaseline.DefaultFrameRate;
    }
}
