#nullable enable

using System;
using System.Collections.Generic;

namespace Mmd.Editor
{
    public static class MmdPerformanceBaseline
    {
        public const int SchemaVersion = 1;
        public const int DefaultWarmupFrames = 5;
        public const int DefaultMeasurementFrames = 120;
        public const float DefaultFrameRate = 30.0f;
        public const string SchemaName = "mmd-performance-baseline";
        public const ulong ChecksumSeed = 14695981039346656037UL;

        public static readonly IReadOnlyList<string> RequiredPhaseNames = new[]
        {
            "pmx-load",
            "pmx-parse",
            "vmd-load-parse",
            "unity-asset-build",
            "native-evaluate-copy",
            "unity-pose-morph-apply",
            "live-physics-total",
        };

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
