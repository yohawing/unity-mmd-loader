#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.TestTools;
using Mmd.Editor;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdGeneratedPmxVisualParityTests
    {
        [Test]
        [Explicit("Run the tracked shading gate explicitly; prerequisites are fail-closed.")]
        [Category("VisualShadingTier")]
        public void ToonRampOpaqueOutline_IsDeterministicAndMatchesGolden()
        {
            bool optedOut = string.Equals(
                Environment.GetEnvironmentVariable("YMU_VISUAL_TIER_OPT_OUT"), "1",
                StringComparison.Ordinal);
            var visualCase = new MmdGeneratedPmxVisualCase(
                "mmd-toon-ramp-lit-box",
                "mmd-toon-ramp-lit-box.pmx",
                new Vector3(-0.06f, 0.6f, 3.3f),
                new Vector3(-0.06f, 0.6f, 0.0f),
                27.0f,
                0.03f);

            var availability = MmdFlipHelper.ProbeAvailability();
            if (!availability.available)
            {
                RequireOrOptOut(optedOut, "FLIP not available: " + availability.unsupportedReason);
            }

            string? goldenPath = ResolveGoldenPath(visualCase.name);
            if (goldenPath == null)
            {
                RequireOrOptOut(optedOut, "Golden reference missing for " + visualCase.name);
            }

            string fixtureDirectory = ResolveGeneratedPmxFixtureDirectory();
            string pmxPath = Path.Combine(fixtureDirectory, visualCase.modelFileName);
            if (!File.Exists(pmxPath))
            {
                RequireOrOptOut(optedOut, "Generated PMX fixture missing: " + pmxPath);
            }

            string artifactsDir = ResolveArtifactsDir();
            Directory.CreateDirectory(artifactsDir);
            string firstPng = Path.Combine(artifactsDir, visualCase.name + ".determinism-a.png");
            string secondPng = Path.Combine(artifactsDir, visualCase.name + ".determinism-b.png");
            MmdGeneratedPmxVisualCaseReport firstReport = MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                visualCase, fixtureDirectory, firstPng,
                backgroundEnabled: true, postProcessingEnabled: false);
            MmdGeneratedPmxVisualCaseReport secondReport = MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                visualCase, fixtureDirectory, secondPng,
                backgroundEnabled: true, postProcessingEnabled: false);
            CollectionAssert.AreEqual(File.ReadAllBytes(firstPng), File.ReadAllBytes(secondPng),
                "Identical camera/light captures must be byte-deterministic.");
            Assert.That(firstReport.loadedToonTextures, Is.GreaterThan(0), "The gate must exercise toon texture binding.");
            Assert.That(firstReport.outlinePixelCount, Is.GreaterThan(0), "The gate must exercise an opaque outline.");
            Assert.That(secondReport.status, Is.EqualTo("passed"));

            bool perturb = string.Equals(
                Environment.GetEnvironmentVariable("YMU_VISUAL_TIER_PERTURB"), "1",
                StringComparison.Ordinal);
            string candidatePng = secondPng;
            if (perturb)
            {
                candidatePng = Path.Combine(artifactsDir, visualCase.name + ".candidate-perturbed.png");
                MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                    visualCase, fixtureDirectory, candidatePng,
                    backgroundEnabled: true, postProcessingEnabled: false,
                    perturbShaderOutput: true);
            }

            File.Copy(goldenPath, Path.Combine(artifactsDir, visualCase.name + ".golden.png"), overwrite: true);

            float mean = MmdFlipHelper.ComputeMeanError(goldenPath!, candidatePng, artifactsDir);
            TestContext.WriteLine($"[visual-shading-tier] {visualCase.name} mean={mean:F6} perturb={perturb}");
            string baselineJsonPath = GetBaselineJsonPath();
            BaselineList baseline = LoadBaseline(baselineJsonPath);
            BaselineEntry? entry = baseline.FindEntry(visualCase.name);
            Assert.That(entry, Is.Not.Null, "Tracked baseline entry is required for the explicit visual tier.");
            float tolerance = Mathf.Max(0.03f, entry!.maxMean * 0.10f);
            float ceiling = entry.maxMean + tolerance;
            WriteReviewManifest(
                artifactsDir,
                visualCase,
                firstReport,
                Path.Combine(artifactsDir, visualCase.name + ".golden.png"),
                candidatePng,
                mean,
                ceiling);
            Assert.That(mean, Is.LessThanOrEqualTo(ceiling),
                $"{visualCase.name}: measured {mean:F6} > baseline {entry.maxMean:F6} + tol {tolerance:F6}");
            LogAssert.NoUnexpectedReceived();
        }

        private static void WriteReviewManifest(
            string artifactsDir,
            MmdGeneratedPmxVisualCase visualCase,
            MmdGeneratedPmxVisualCaseReport report,
            string referencePath,
            string candidatePath,
            float flipMean,
            float ceiling)
        {
            PackageInfo? urp = PackageInfo.GetAllRegisteredPackages()
                .FirstOrDefault(package => package.name == "com.unity.render-pipelines.universal");
            string? heatmap = Directory.GetFiles(artifactsDir, "flip*.png")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            var manifest = new VisualReviewManifest
            {
                runId = new DirectoryInfo(artifactsDir).Name,
                unityVersion = Application.unityVersion,
                urpVersion = urp?.version ?? "unknown",
                gpu = SystemInfo.graphicsDeviceName,
                humanSignoff = "pending",
                cases = new List<VisualReviewCase>
                {
                    new VisualReviewCase
                    {
                        id = visualCase.name,
                        reference = Path.GetFileName(referencePath),
                        candidate = Path.GetFileName(candidatePath),
                        heatmap = heatmap == null ? string.Empty : Path.GetFileName(heatmap),
                        flipMean = flipMean,
                        flipCeiling = ceiling,
                        passed = flipMean <= ceiling,
                        shaderProfile = report.shaderName,
                        cameraPosition = report.cameraPosition,
                        cameraTarget = report.cameraTarget,
                        cameraFieldOfView = report.cameraFieldOfView,
                        ambientLightColor = report.ambientLightColor,
                        ambientLightIntensity = report.ambientLightIntensity,
                        directionalLightColor = report.directionalLightColor,
                        directionalLightIntensity = report.directionalLightIntensity,
                        directionalLightPosition = report.directionalLightPosition,
                        directionalLightTarget = report.directionalLightTarget,
                        directionalLightMode = report.directionalLightMode,
                        volume = "disabled",
                        intendedChange = "Legacy parity gate; no intended visual delta"
                    }
                }
            };
            File.WriteAllText(
                Path.Combine(artifactsDir, "manifest.json"),
                JsonUtility.ToJson(manifest, prettyPrint: true));
        }

        private static void RequireOrOptOut(bool optedOut, string reason)
        {
            if (optedOut)
                Assert.Ignore("Explicit visual tier opt-out: " + reason);
            Assert.Fail(reason + ". Set YMU_VISUAL_TIER_OPT_OUT=1 only for an intentional opt-out.");
        }

        private static string? ResolveGoldenPath(string caseName)
        {
            string? envOverride = Environment.GetEnvironmentVariable("YMU_GOLDEN_ORACLE_ROOT");
            if (!string.IsNullOrEmpty(envOverride))
            {
                string external = Path.Combine(envOverride!, "runs", "fixture-render",
                    $"fixture-render-generated-visual-{caseName}", "frame-0.png");
                if (File.Exists(external))
                    return external;
            }

            string base64Path = Path.Combine(GetDevToolsPackageRoot(), "Tests", "EditMode",
                "mmd-toon-ramp-lit-box.golden.base64");
            if (!File.Exists(base64Path))
                return null;
            string outputDirectory = ResolveArtifactsDir();
            Directory.CreateDirectory(outputDirectory);
            string decodedPath = Path.Combine(outputDirectory, caseName + ".tracked-golden.png");
            File.WriteAllBytes(decodedPath, Convert.FromBase64String(File.ReadAllText(base64Path).Trim()));
            return decodedPath;
        }

        private static string ResolveArtifactsDir()
        {
            string? envOverride = Environment.GetEnvironmentVariable("YMU_VISUAL_PARITY_ARTIFACTS");
            if (!string.IsNullOrEmpty(envOverride))
                return envOverride!;
            string projectRoot = Path.GetDirectoryName(Application.dataPath)!;
            string repoRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));
            return Path.Combine(repoRoot, "artifacts", "visual-parity");
        }

        private static string ResolveGeneratedPmxFixtureDirectory()
        {
            string packageRoot = PackageInfo.FindForAssembly(typeof(MmdUnityModelInstance).Assembly).resolvedPath;
            return Path.Combine(packageRoot, "Tests", "Fixtures", "Assets", "GeneratedPmx");
        }

        private static string GetBaselineJsonPath()
        {
            return Path.Combine(GetDevToolsPackageRoot(), "Tests", "EditMode", "visual-parity-baseline.json");
        }

        private static string GetDevToolsPackageRoot()
        {
            return PackageInfo.FindForAssembly(typeof(MmdGeneratedPmxVisualParityTests).Assembly).resolvedPath;
        }

        private static BaselineList LoadBaseline(string path)
        {
            if (!File.Exists(path))
                return new BaselineList();
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<BaselineList>(json) ?? new BaselineList();
        }

        [Serializable]
        private class BaselineEntry
        {
            public string name = "";
            public float maxMean;
        }

        [Serializable]
        private class BaselineList
        {
            public List<BaselineEntry> entries = new List<BaselineEntry>();

            public BaselineEntry? FindEntry(string caseName)
            {
                foreach (var e in entries)
                {
                    if (e.name == caseName)
                        return e;
                }
                return null;
            }
        }

        [Serializable]
        private sealed class VisualReviewManifest
        {
            public int schemaVersion = 1;
            public string runId = string.Empty;
            public string unityVersion = string.Empty;
            public string urpVersion = string.Empty;
            public string gpu = string.Empty;
            public string humanSignoff = string.Empty;
            public List<VisualReviewCase> cases = new();
        }

        [Serializable]
        private sealed class VisualReviewCase
        {
            public string id = string.Empty;
            public string reference = string.Empty;
            public string candidate = string.Empty;
            public string heatmap = string.Empty;
            public float flipMean;
            public float flipCeiling;
            public bool passed;
            public string shaderProfile = string.Empty;
            public float[] cameraPosition = Array.Empty<float>();
            public float[] cameraTarget = Array.Empty<float>();
            public float cameraFieldOfView;
            public float[] ambientLightColor = Array.Empty<float>();
            public float ambientLightIntensity;
            public float[] directionalLightColor = Array.Empty<float>();
            public float directionalLightIntensity;
            public float[] directionalLightPosition = Array.Empty<float>();
            public float[] directionalLightTarget = Array.Empty<float>();
            public string directionalLightMode = string.Empty;
            public string volume = string.Empty;
            public string intendedChange = string.Empty;
        }
    }
}
