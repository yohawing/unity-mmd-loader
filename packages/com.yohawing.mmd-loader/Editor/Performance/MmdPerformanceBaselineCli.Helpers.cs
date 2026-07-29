#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEngine;
using Mmd.Native;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Editor
{
    public static partial class MmdPerformanceBaselineCli
    {
        private static void MeasureLivePhysics(
            MmdPerformanceBaselineReport report,
            MmdPerformanceBaselineOptions options,
            string physicsPath,
            byte[] vmdBytes)
        {
            MmdUnityPlaybackBinding? binding = null;
            MmdUnityModelInstance? instance = null;
            try
            {
                var parser = new NativeMmdParser();
                MmdModelDefinition physicsModel = parser.LoadModel(File.ReadAllBytes(physicsPath));
                int removedPureWorldAnchors = physicsModel.physics.joints.RemoveAll(
                    joint => joint.rigidbodyAIndex < 0 && joint.rigidbodyBIndex < 0);
                MmdMotionDefinition motion = parser.LoadMotion(vmdBytes);
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    physicsModel,
                    motion,
                    physicsPath,
                    "performance-vmd",
                    physicsPath);
                instance = binding.Instance;
                binding.SetPhysicsMode(MmdPhysicsMode.Live);
                for (int i = 0; i < options.warmupFrames; i++)
                    binding.ApplyFrame(i, options.frameRate);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var samples = new List<double>(options.measurementFrames);
                var evaluateSamples = new List<double>(options.measurementFrames);
                var syncSamples = new List<double>(options.measurementFrames);
                var stepSamples = new List<double>(options.measurementFrames);
                var applySamples = new List<double>(options.measurementFrames);
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < options.measurementFrames; i++)
                {
                    long start = Stopwatch.GetTimestamp();
                    binding.ApplyFrame(options.warmupFrames + i, options.frameRate);
                    samples.Add(ElapsedMs(start));
                    MmdLivePhysicsFrameDiagnostics? diagnostics = binding.LastLivePhysicsDiagnostics;
                    if (diagnostics == null)
                        throw new InvalidOperationException("Live physics frame did not produce diagnostics.");
                    evaluateSamples.Add(diagnostics.evaluateFrameMs);
                    syncSamples.Add(diagnostics.syncBoneDrivenBodiesMs);
                    stepSamples.Add(diagnostics.stepPhysicsMs);
                    applySamples.Add(diagnostics.applyPhysicsBodiesMs);
                }
                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                report.phases.Add(MmdPerformanceBaseline.BuildPhase(
                    "live-physics-total",
                    samples,
                    allocatedBytes,
                    options.measurementFrames,
                    "Binding ApplyFrame total; " +
                    removedPureWorldAnchors + " unsupported pure world-anchor joints were excluded."));
                report.phases.Add(MmdPerformanceBaseline.BuildTimingPhase("live-physics-evaluate", evaluateSamples, options.measurementFrames));
                report.phases.Add(MmdPerformanceBaseline.BuildTimingPhase("live-physics-sync", syncSamples, options.measurementFrames));
                report.phases.Add(MmdPerformanceBaseline.BuildTimingPhase("live-physics-step", stepSamples, options.measurementFrames));
                report.phases.Add(MmdPerformanceBaseline.BuildTimingPhase("live-physics-apply", applySamples, options.measurementFrames));
            }
            catch (Exception exception)
            {
                bool unavailable = IsNativeCapabilityUnavailable(exception);
                foreach (string phaseName in MmdPerformanceBaseline.LivePhysicsPhaseNames)
                {
                    report.phases.Add(unavailable
                        ? MmdPerformanceBaseline.SkipPhase(phaseName, exception.Message)
                        : MmdPerformanceBaseline.ErrorPhase(phaseName, exception.Message));
                }
                report.status = unavailable ? MmdPerformanceStatus.Skip : MmdPerformanceStatus.Error;
                report.skipReason = (unavailable ? "Live Physics unavailable: " : "Live Physics failed: ") + exception.Message;
            }
            finally
            {
                binding?.Dispose();
                DestroyInstance(instance);
            }

        }

        private static MmdPerformanceBaselineOptions CreateOptions()
        {
            string repoRoot = GetArgument("-repoRoot", DiscoverRepoRoot());
            return new MmdPerformanceBaselineOptions
            {
                repoRoot = repoRoot,
                pmxPath = GetArgument("-pmxPath", Path.Combine(repoRoot, "packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube.pmx")),
                vmdPath = GetArgument("-vmdPath", Path.Combine(repoRoot, "packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube_motion.vmd")),
                physicsPmxPath = GetArgument("-physicsPmxPath", Path.Combine(repoRoot, "packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_hair_physics.pmx")),
                baselinePath = GetArgument("-baseline", string.Empty),
                warmupFrames = GetOptionalInt("-warmupFrames", MmdPerformanceBaseline.DefaultWarmupFrames),
                measurementFrames = GetOptionalInt("-frameCount", MmdPerformanceBaseline.DefaultMeasurementFrames),
                frameRate = GetOptionalFloat("-frameRate", MmdPerformanceBaseline.DefaultFrameRate),
            };
        }

        private static MmdPerformanceBaselineReport CreateReport(MmdPerformanceBaselineOptions options, string status, string reason)
        {
            return new MmdPerformanceBaselineReport
            {
                status = status,
                skipReason = reason,
                generatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                packageHead = GitHead(options.repoRoot),
                mmdAnimRevision = GitHead(Path.Combine(options.repoRoot, "native", "mmd-anim")),
                mmdAnimAbi = DiscoverAbiVersion(),
                backend = "unavailable",
                cpu = SystemInfo.processorType + "; logicalProcessors=" + Environment.ProcessorCount,
                warmupFrames = options.warmupFrames,
                measurementFrames = options.measurementFrames,
                frameRate = options.frameRate,
            };
        }

        private static MmdPerformanceBaselineReport LoadBaseline(string path)
        {
            if (!File.Exists(path))
                throw new InvalidDataException("Baseline file was not found: " + path);
            MmdPerformanceBaselineReport? baseline = JsonUtility.FromJson<MmdPerformanceBaselineReport>(File.ReadAllText(path));
            if (baseline == null || baseline.schemaVersion != MmdPerformanceBaseline.SchemaVersion || baseline.schema != MmdPerformanceBaseline.SchemaName)
                throw new InvalidDataException("Baseline schema is missing or unsupported.");
            IReadOnlyList<string> validationErrors = MmdPerformanceBaseline.ValidateReport(baseline);
            if (validationErrors.Count > 0)
                throw new InvalidDataException("Baseline report is malformed: " + string.Join(" ", validationErrors));
            return baseline;
        }

        private static string DiscoverAbiVersion()
        {
            try
            {
                Type? type = typeof(NativeMmdParser).Assembly.GetType("Mmd.Native.MmdRuntimeFfiMethods");
                FieldInfo? field = type?.GetField("ExpectedAbiVersion", BindingFlags.Static | BindingFlags.NonPublic);
                return field?.GetValue(null)?.ToString() ?? "unavailable";
            }
            catch
            {
                return "unavailable";
            }
        }

        private static string GitHead(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    return "unavailable";
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = "-C \"" + path + "\" rev-parse HEAD",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);
                return string.IsNullOrWhiteSpace(output) ? "unavailable" : output;
            }
            catch
            {
                return "unavailable";
            }
        }

        private static string Sha256File(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool IsNativeCapabilityUnavailable(Exception exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is DllNotFoundException || current is EntryPointNotFoundException || current is BadImageFormatException)
                    return true;
            }
            return false;
        }

        private static void WriteReport(MmdPerformanceBaselineReport report, string outputPath)
        {
            string fullPath = Path.GetFullPath(outputPath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, JsonUtility.ToJson(report, true));
            UnityEngine.Debug.Log("MMD performance baseline report: " + fullPath + " (" + report.status + ")");
        }

        private static string DefaultOutputPath() => Path.Combine(DiscoverRepoRoot(), "artifacts", "performance", "performance-baseline.json");

        private static string DiscoverRepoRoot()
        {
            DirectoryInfo? unityProject = Directory.GetParent(Application.dataPath);
            return unityProject?.Parent?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static string GetArgument(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return fallback;
        }

        private static int GetOptionalInt(string name, int fallback) => int.TryParse(GetArgument(name, fallback.ToString()), out int value) ? value : fallback;

        private static float GetOptionalFloat(string name, float fallback)
        {
            return float.TryParse(GetArgument(name, fallback.ToString(System.Globalization.CultureInfo.InvariantCulture)), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;
        }

        private static double ElapsedMs(long startTimestamp) => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;

        private static void DestroyInstance(MmdUnityModelInstance? instance)
        {
            if (instance == null)
                return;
            if (instance.Root != null)
                Object.DestroyImmediate(instance.Root);
            if (instance.Mesh != null)
                Object.DestroyImmediate(instance.Mesh);
            foreach (Material material in instance.Materials ?? Array.Empty<Material>())
                if (material != null)
                    Object.DestroyImmediate(material);
            foreach (Texture2D texture in instance.OwnedTextures ?? Array.Empty<Texture2D>())
                if (texture != null)
                    Object.DestroyImmediate(texture);
        }
    }
}
