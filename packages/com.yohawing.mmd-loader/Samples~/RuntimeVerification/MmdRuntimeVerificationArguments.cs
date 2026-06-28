#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Mmd.Samples.RuntimeVerification
{
    public enum MmdRuntimeVerificationDrive
    {
        Controller = 0,
        Timeline = 1
    }

    public sealed class MmdRuntimeVerificationArguments
    {
        public string PmxPath { get; private set; } = string.Empty;
        public string VmdPath { get; private set; } = string.Empty;
        public string DirectoryPath { get; private set; } = string.Empty;
        public string FixtureManifestPath { get; private set; } = string.Empty;
        public string OutputPath { get; private set; } = string.Empty;
        public float DurationSeconds { get; private set; } = 3.0f;
        public float FrameRate { get; private set; } = 30.0f;
        public int[] SampleFrames { get; private set; } = Array.Empty<int>();
        public bool DumpBones { get; private set; }
        public float PhysicsMaxSubStepFixedStepSeconds { get; private set; }
        public MmdRuntimeVerificationDrive Drive { get; private set; } =
            MmdRuntimeVerificationDrive.Timeline;
        public bool FastRuntimeEnabled { get; private set; } = true;
        public bool HelpRequested { get; private set; }
        public List<string> Errors { get; } = new();

        public static MmdRuntimeVerificationArguments Parse(string[] commandLineArgs)
        {
            var parsed = new MmdRuntimeVerificationArguments();
            string[] args = commandLineArgs ?? Array.Empty<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i] ?? string.Empty;
                if (!arg.StartsWith("--", StringComparison.Ordinal) &&
                    !string.Equals(arg, "-h", StringComparison.Ordinal))
                {
                    continue;
                }

                string name = arg;
                string? inlineValue = null;
                int equals = arg.IndexOf('=');
                if (equals >= 0)
                {
                    name = arg.Substring(0, equals);
                    inlineValue = arg.Substring(equals + 1);
                }

                string? value = inlineValue;
                bool requiresValue = name is "--pmx" or "--vmd" or "--dir" or "--out" or
                    "--duration" or "--frame-rate" or "--drive" or "--fast-runtime" or
                    "--fixture-manifest" or
                    "--sample-frames" or "--physics-max-substep-fixed-step";
                if (requiresValue && value == null)
                {
                    if (i + 1 >= args.Length)
                    {
                        parsed.Errors.Add("Missing value for " + name + ".");
                        continue;
                    }

                    value = args[++i];
                }

                parsed.Apply(name, value ?? string.Empty);
            }

            parsed.NormalizeAndValidate();
            return parsed;
        }

        public MmdRuntimeVerificationCase[] CreateCases()
        {
            var cases = new List<MmdRuntimeVerificationCase>();
            bool hasPmx = !string.IsNullOrWhiteSpace(PmxPath);
            bool hasVmd = !string.IsNullOrWhiteSpace(VmdPath);
            bool hasDir = !string.IsNullOrWhiteSpace(DirectoryPath);
            if (!string.IsNullOrWhiteSpace(FixtureManifestPath))
            {
                return MmdRuntimeViewerFixtureManifest.LoadPlaybackCases(FixtureManifestPath, Errors);
            }

            if (!hasDir)
            {
                cases.Add(new MmdRuntimeVerificationCase(
                    CaseName(PmxPath, VmdPath, "single"),
                    PmxPath,
                    VmdPath,
                    parseOnly: !(hasPmx && hasVmd)));
                return cases.ToArray();
            }

            if (hasVmd && !hasPmx)
            {
                foreach (string pmx in EnumerateFiles(DirectoryPath, "*.pmx"))
                {
                    cases.Add(new MmdRuntimeVerificationCase(
                        CaseName(pmx, VmdPath, Path.GetFileNameWithoutExtension(pmx)),
                        pmx,
                        VmdPath,
                        parseOnly: false));
                }

                return cases.ToArray();
            }

            if (hasPmx && !hasVmd)
            {
                foreach (string vmd in EnumerateFiles(DirectoryPath, "*.vmd"))
                {
                    cases.Add(new MmdRuntimeVerificationCase(
                        CaseName(PmxPath, vmd, Path.GetFileNameWithoutExtension(vmd)),
                        PmxPath,
                        vmd,
                        parseOnly: false));
                }

                return cases.ToArray();
            }

            if (hasPmx && hasVmd)
            {
                cases.Add(new MmdRuntimeVerificationCase(
                    CaseName(PmxPath, VmdPath, "single"),
                    PmxPath,
                    VmdPath,
                    parseOnly: false));
                return cases.ToArray();
            }

            foreach (string pmx in EnumerateFiles(DirectoryPath, "*.pmx"))
            {
                cases.Add(new MmdRuntimeVerificationCase(
                    "parse-pmx:" + Path.GetFileName(pmx),
                    pmx,
                    string.Empty,
                    parseOnly: true));
            }

            foreach (string vmd in EnumerateFiles(DirectoryPath, "*.vmd"))
            {
                cases.Add(new MmdRuntimeVerificationCase(
                    "parse-vmd:" + Path.GetFileName(vmd),
                    string.Empty,
                    vmd,
                    parseOnly: true));
            }

            return cases.ToArray();
        }

        private void Apply(string name, string value)
        {
            switch (name)
            {
                case "--pmx":
                    PmxPath = value;
                    break;
                case "--vmd":
                    VmdPath = value;
                    break;
                case "--dir":
                    DirectoryPath = value;
                    break;
                case "--fixture-manifest":
                    FixtureManifestPath = value;
                    break;
                case "--out":
                    OutputPath = value;
                    break;
                case "--duration":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float duration))
                    {
                        DurationSeconds = duration;
                    }
                    else
                    {
                        Errors.Add("Invalid --duration value: " + value);
                    }
                    break;
                case "--frame-rate":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float frameRate))
                    {
                        FrameRate = frameRate;
                    }
                    else
                    {
                        Errors.Add("Invalid --frame-rate value: " + value);
                    }
                    break;
                case "--sample-frames":
                    SampleFrames = ParseSampleFrames(value, Errors);
                    break;
                case "--physics-max-substep-fixed-step":
                    if (TryParsePositiveFloatOrFraction(value, out float fixedStepSeconds))
                    {
                        PhysicsMaxSubStepFixedStepSeconds = fixedStepSeconds;
                    }
                    else
                    {
                        Errors.Add("Invalid --physics-max-substep-fixed-step value: " + value + ". Expected positive seconds or a fraction such as 1/120.");
                    }
                    break;
                case "--dump-bones":
                    DumpBones = true;
                    break;
                case "--drive":
                    if (string.Equals(value, "timeline", StringComparison.OrdinalIgnoreCase))
                    {
                        Drive = MmdRuntimeVerificationDrive.Timeline;
                    }
                    else if (string.Equals(value, "controller", StringComparison.OrdinalIgnoreCase))
                    {
                        Drive = MmdRuntimeVerificationDrive.Controller;
                    }
                    else
                    {
                        Errors.Add("Invalid --drive value: " + value + ". Expected timeline or controller.");
                    }
                    break;
                case "--fast-runtime":
                    if (string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "0", StringComparison.Ordinal))
                    {
                        FastRuntimeEnabled = false;
                    }
                    else if (string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "1", StringComparison.Ordinal))
                    {
                        FastRuntimeEnabled = true;
                    }
                    else
                    {
                        Errors.Add("Invalid --fast-runtime value: " + value + ". Expected on or off.");
                    }
                    break;
                case "--help":
                case "-h":
                    HelpRequested = true;
                    break;
                default:
                    Errors.Add("Unknown argument: " + name);
                    break;
            }
        }

        private void NormalizeAndValidate()
        {
            PmxPath = ResolveInputPath(PmxPath);
            VmdPath = ResolveInputPath(VmdPath);
            DirectoryPath = ResolveInputPath(DirectoryPath);
            FixtureManifestPath = ResolveInputPath(ResolveFixtureManifestPath(
                FixtureManifestPath,
                PmxPath,
                VmdPath,
                DirectoryPath));
            OutputPath = ResolveInputPath(OutputPath);

            if (DurationSeconds < 0.0f || float.IsNaN(DurationSeconds) || float.IsInfinity(DurationSeconds))
            {
                Errors.Add("--duration must be a non-negative finite number.");
            }

            if (FrameRate <= 0.0f || float.IsNaN(FrameRate) || float.IsInfinity(FrameRate))
            {
                Errors.Add("--frame-rate must be a positive finite number.");
            }

            if (!string.IsNullOrWhiteSpace(DirectoryPath) && !Directory.Exists(DirectoryPath))
            {
                Errors.Add("--dir does not exist: " + DirectoryPath);
            }

            if (!string.IsNullOrWhiteSpace(FixtureManifestPath) && !File.Exists(FixtureManifestPath))
            {
                Errors.Add("--fixture-manifest does not exist: " + FixtureManifestPath);
            }

            if (string.IsNullOrWhiteSpace(DirectoryPath) &&
                string.IsNullOrWhiteSpace(PmxPath) &&
                string.IsNullOrWhiteSpace(VmdPath) &&
                string.IsNullOrWhiteSpace(FixtureManifestPath) &&
                !HelpRequested)
            {
                Errors.Add("Provide --pmx/--vmd, --fixture-manifest, or --dir for a parse-only sweep.");
            }
        }

        private static string ResolveFixtureManifestPath(
            string path,
            string pmxPath,
            string vmdPath,
            string directoryPath)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            if (!string.IsNullOrWhiteSpace(pmxPath) ||
                !string.IsNullOrWhiteSpace(vmdPath) ||
                !string.IsNullOrWhiteSpace(directoryPath))
            {
                return string.Empty;
            }

            string? environmentPath = Environment.GetEnvironmentVariable("MMD_RUNTIME_VIEWER_FIXTURES");
            return environmentPath ?? string.Empty;
        }

        private static string ResolveInputPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.GetFullPath(path);
        }

        private static int[] ParseSampleFrames(string value, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add("--sample-frames must contain at least one frame index.");
                return Array.Empty<int>();
            }

            var frames = new List<int>();
            string[] parts = value.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (string.IsNullOrWhiteSpace(part))
                {
                    errors.Add("Invalid --sample-frames value: empty frame entry.");
                    continue;
                }

                if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int frame))
                {
                    errors.Add("Invalid --sample-frames frame: " + part);
                    continue;
                }

                if (frame < 0)
                {
                    errors.Add("--sample-frames entries must be non-negative: " + part);
                    continue;
                }

                if (!frames.Contains(frame))
                {
                    frames.Add(frame);
                }
            }

            frames.Sort();
            return frames.ToArray();
        }

        private static bool TryParsePositiveFloatOrFraction(string value, out float result)
        {
            result = 0.0f;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            int slash = trimmed.IndexOf('/');
            if (slash > 0 && slash < trimmed.Length - 1)
            {
                string numeratorText = trimmed.Substring(0, slash);
                string denominatorText = trimmed.Substring(slash + 1);
                if (float.TryParse(numeratorText, NumberStyles.Float, CultureInfo.InvariantCulture, out float numerator) &&
                    float.TryParse(denominatorText, NumberStyles.Float, CultureInfo.InvariantCulture, out float denominator) &&
                    denominator > 0.0f)
                {
                    result = numerator / denominator;
                    return float.IsFinite(result) && result > 0.0f;
                }

                return false;
            }

            return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
                float.IsFinite(result) &&
                result > 0.0f;
        }

        private static IEnumerable<string> EnumerateFiles(string directory, string pattern)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                yield break;
            }

            foreach (string file in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
            {
                yield return Path.GetFullPath(file);
            }
        }

        private static string CaseName(string pmxPath, string vmdPath, string fallback)
        {
            string pmxName = string.IsNullOrWhiteSpace(pmxPath)
                ? string.Empty
                : Path.GetFileNameWithoutExtension(pmxPath);
            string vmdName = string.IsNullOrWhiteSpace(vmdPath)
                ? string.Empty
                : Path.GetFileNameWithoutExtension(vmdPath);
            if (!string.IsNullOrWhiteSpace(pmxName) && !string.IsNullOrWhiteSpace(vmdName))
            {
                return pmxName + "__" + vmdName;
            }

            return string.IsNullOrWhiteSpace(fallback) ? "case" : fallback;
        }
    }

    public readonly struct MmdRuntimeVerificationCase
    {
        public MmdRuntimeVerificationCase(
            string name,
            string pmxPath,
            string vmdPath,
            bool parseOnly)
        {
            Name = name ?? string.Empty;
            PmxPath = pmxPath ?? string.Empty;
            VmdPath = vmdPath ?? string.Empty;
            ParseOnly = parseOnly;
        }

        public string Name { get; }
        public string PmxPath { get; }
        public string VmdPath { get; }
        public bool ParseOnly { get; }
    }
}
