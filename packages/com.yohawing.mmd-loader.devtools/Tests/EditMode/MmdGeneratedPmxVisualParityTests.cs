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
using Mmd.Rendering;
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
            Assert.That(mean, Is.LessThanOrEqualTo(ceiling),
                $"{visualCase.name}: measured {mean:F6} > baseline {entry.maxMean:F6} + tol {tolerance:F6}");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [Explicit("Run the S1a main-light visual delta explicitly; FLIP artifacts still require human review.")]
        [Category("VisualShadingTier")]
        public void ToonRampToonLit_TracksUnityMainLightWhileLegacyStaysInvariant()
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

            string fixtureDirectory = ResolveGeneratedPmxFixtureDirectory();
            string pmxPath = Path.Combine(fixtureDirectory, visualCase.modelFileName);
            if (!File.Exists(pmxPath))
            {
                RequireOrOptOut(optedOut, "Generated PMX fixture missing: " + pmxPath);
            }

            string artifactsDir = ResolveArtifactsDir();
            Directory.CreateDirectory(artifactsDir);
            Color changedLightColor = new Color(0.18f, 0.82f, 0.31f, 1.0f);
            const float changedLightIntensity = 0.35f;
            string legacyReference = Path.Combine(artifactsDir, visualCase.name + ".legacy-main-light-default.png");
            string legacyCandidate = Path.Combine(artifactsDir, visualCase.name + ".legacy-main-light-changed.png");
            string toonLitReference = Path.Combine(artifactsDir, visualCase.name + ".toon-lit-main-light-default.png");
            string toonLitCandidate = Path.Combine(artifactsDir, visualCase.name + ".toon-lit-main-light-changed.png");
            string legacyShadowReference = Path.Combine(artifactsDir, visualCase.name + ".legacy-realtime-shadow-off.png");
            string legacyShadowCandidate = Path.Combine(artifactsDir, visualCase.name + ".legacy-realtime-shadow-on.png");
            string toonLitShadowReference = Path.Combine(artifactsDir, visualCase.name + ".toon-lit-realtime-shadow-off.png");
            string toonLitShadowCandidate = Path.Combine(artifactsDir, visualCase.name + ".toon-lit-realtime-shadow-on.png");

            MmdGeneratedPmxVisualCaseReport legacyReferenceReport = MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                visualCase, fixtureDirectory, legacyReference,
                backgroundEnabled: true, postProcessingEnabled: false,
                materialPreset: MmdMaterialPreset.MmdToon,
                ambientLightColorOverride: Color.black,
                ambientLightIntensityOverride: 0.0f);
            MmdGeneratedPmxVisualCaseReport legacyCandidateReport = MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                visualCase, fixtureDirectory, legacyCandidate,
                backgroundEnabled: true, postProcessingEnabled: false,
                materialPreset: MmdMaterialPreset.MmdToon,
                directionalLightColorOverride: changedLightColor,
                directionalLightIntensityOverride: changedLightIntensity,
                ambientLightColorOverride: Color.black,
                ambientLightIntensityOverride: 0.0f);
            MmdGeneratedPmxVisualCaseReport toonLitReferenceReport = MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                visualCase, fixtureDirectory, toonLitReference,
                backgroundEnabled: true, postProcessingEnabled: false,
                materialPreset: MmdMaterialPreset.MmdToonLit,
                ambientLightColorOverride: Color.black,
                ambientLightIntensityOverride: 0.0f);
            MmdGeneratedPmxVisualCaseReport toonLitCandidateReport = MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                visualCase, fixtureDirectory, toonLitCandidate,
                backgroundEnabled: true, postProcessingEnabled: false,
                materialPreset: MmdMaterialPreset.MmdToonLit,
                directionalLightColorOverride: changedLightColor,
                directionalLightIntensityOverride: changedLightIntensity,
                ambientLightColorOverride: Color.black,
                ambientLightIntensityOverride: 0.0f);
            MmdGeneratedPmxVisualCaseReport legacyShadowReferenceReport = MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                visualCase, fixtureDirectory, legacyShadowReference,
                backgroundEnabled: true, postProcessingEnabled: false,
                materialPreset: MmdMaterialPreset.MmdToon,
                ambientLightColorOverride: Color.black,
                ambientLightIntensityOverride: 0.0f);
            MmdGeneratedPmxVisualCaseReport legacyShadowCandidateReport = MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                visualCase, fixtureDirectory, legacyShadowCandidate,
                backgroundEnabled: true, postProcessingEnabled: false,
                materialPreset: MmdMaterialPreset.MmdToon,
                ambientLightColorOverride: Color.black,
                ambientLightIntensityOverride: 0.0f,
                realtimeShadowOccluderEnabled: true);
            MmdGeneratedPmxVisualCaseReport toonLitShadowReferenceReport = MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                visualCase, fixtureDirectory, toonLitShadowReference,
                backgroundEnabled: true, postProcessingEnabled: false,
                materialPreset: MmdMaterialPreset.MmdToonLit,
                ambientLightColorOverride: Color.black,
                ambientLightIntensityOverride: 0.0f);
            MmdGeneratedPmxVisualCaseReport toonLitShadowCandidateReport =
                MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                    visualCase, fixtureDirectory, toonLitShadowCandidate,
                    backgroundEnabled: true, postProcessingEnabled: false,
                    materialPreset: MmdMaterialPreset.MmdToonLit,
                    ambientLightColorOverride: Color.black,
                    ambientLightIntensityOverride: 0.0f,
                    realtimeShadowOccluderEnabled: true);

            Assert.That(legacyShadowReferenceReport.status, Is.EqualTo("passed"));
            Assert.That(legacyShadowCandidateReport.status, Is.EqualTo("passed"));
            Assert.That(toonLitShadowReferenceReport.status, Is.EqualTo("passed"));
            Assert.That(toonLitShadowCandidateReport.status, Is.EqualTo("passed"));

            Assert.That(legacyReferenceReport.shaderName,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName));
            Assert.That(legacyCandidateReport.shaderName,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName));
            Assert.That(legacyReferenceReport.captureUsedStandardRequest,
                Is.True,
                "Legacy capture must use the same active SRP StandardRequest path as the Toon Lit comparison.");
            Assert.That(legacyCandidateReport.captureUsedStandardRequest,
                Is.True,
                "Legacy capture must use the same active SRP StandardRequest path as the Toon Lit comparison.");
            Assert.That(legacyReferenceReport.selectedMaterialPassValid,
                Is.True,
                "Legacy capture must select an enabled ForwardLit pass.");
            Assert.That(legacyCandidateReport.selectedMaterialPassValid,
                Is.True,
                "Legacy capture must select an enabled ForwardLit pass.");
            Assert.That(legacyReferenceReport.selectedMaterialLightMode,
                Is.EqualTo("UniversalForward"));
            Assert.That(legacyCandidateReport.selectedMaterialLightMode,
                Is.EqualTo("UniversalForward"));
            Assert.That(toonLitReferenceReport.shaderName,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName));
            Assert.That(toonLitCandidateReport.shaderName,
                Is.EqualTo(MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName));
            Assert.That(toonLitReferenceReport.captureUsedStandardRequest,
                Is.True,
                "S1a requires the active SRP StandardRequest path; Camera.Render fallback is not evidence of URP lighting.");
            Assert.That(toonLitCandidateReport.captureUsedStandardRequest,
                Is.True,
                "S1a requires the active SRP StandardRequest path; Camera.Render fallback is not evidence of URP lighting.");
            Assert.That(toonLitReferenceReport.selectedMaterialPassValid,
                Is.True,
                "S1a capture must select an enabled ForwardLit pass with a UniversalForward/UniversalForwardOnly LightMode tag.");
            Assert.That(toonLitCandidateReport.selectedMaterialPassValid,
                Is.True,
                "S1a capture must select an enabled ForwardLit pass with a UniversalForward/UniversalForwardOnly LightMode tag.");
            Assert.That(toonLitReferenceReport.selectedMaterialPassName,
                Is.EqualTo("ForwardLit"));
            Assert.That(toonLitCandidateReport.selectedMaterialPassName,
                Is.EqualTo("ForwardLit"));
            Assert.That(toonLitReferenceReport.selectedMaterialLightMode,
                Is.EqualTo("UniversalForwardOnly"));
            Assert.That(toonLitCandidateReport.selectedMaterialLightMode,
                Is.EqualTo("UniversalForwardOnly"));
            Assert.That(toonLitReferenceReport.loadedToonTextures, Is.GreaterThan(0));
            Assert.That(toonLitReferenceReport.outlinePixelCount, Is.GreaterThan(0));

            float legacyDelta = MmdFlipHelper.ComputeMeanError(legacyReference, legacyCandidate, artifactsDir);
            float toonLitDelta = MmdFlipHelper.ComputeMeanError(toonLitReference, toonLitCandidate, artifactsDir);
            float legacyShadowDelta = MmdFlipHelper.ComputeMeanError(legacyShadowReference, legacyShadowCandidate, artifactsDir);
            float toonLitShadowDelta = MmdFlipHelper.ComputeMeanError(toonLitShadowReference, toonLitShadowCandidate, artifactsDir);
            const float minimumVisibleDelta = 0.01f;
            Assert.That(legacyDelta, Is.LessThanOrEqualTo(0.0001f),
                "Legacy MMD Toon must remain invariant when only the Unity main light changes.");
            Assert.That(toonLitDelta, Is.GreaterThan(legacyDelta + minimumVisibleDelta),
                "MMD Toon Lit must visibly follow Unity main-light color and intensity. "
                + $"Captured URP main-light default=({string.Join(",", toonLitReferenceReport.mainLightColor.Select(value => value.ToString("F3")))}) "
                + $"changed=({string.Join(",", toonLitCandidateReport.mainLightColor.Select(value => value.ToString("F3")))}).");
            Assert.That(legacyShadowDelta, Is.LessThanOrEqualTo(0.0001f),
                "Legacy MMD Toon must remain invariant when only Unity realtime shadowing changes.");
            Assert.That(toonLitShadowDelta, Is.GreaterThan(legacyShadowDelta + minimumVisibleDelta),
                "MMD Toon Lit must visibly follow Unity main-light realtime shadow attenuation.");
            TestContext.WriteLine(
                $"[toon-lit-main-light] legacy={legacyDelta:F6} toon-lit={toonLitDelta:F6} "
                + $"legacy-shadow={legacyShadowDelta:F6} toon-lit-shadow={toonLitShadowDelta:F6}");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [Explicit("Run the S1b Ambient SH and fog visual deltas explicitly; FLIP artifacts still require human review.")]
        [Category("VisualShadingTier")]
        public void ToonRampToonLit_TracksAmbientShAndFogWhileLegacyStaysInvariant()
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

            string fixtureDirectory = ResolveGeneratedPmxFixtureDirectory();
            string pmxPath = Path.Combine(fixtureDirectory, visualCase.modelFileName);
            if (!File.Exists(pmxPath))
            {
                RequireOrOptOut(optedOut, "Generated PMX fixture missing: " + pmxPath);
            }

            string artifactsDir = ResolveArtifactsDir();
            Directory.CreateDirectory(artifactsDir);
            string ambientLegacyOffPath = Path.Combine(artifactsDir, visualCase.name + ".legacy-ambient-sh-off.png");
            string ambientLegacyOnPath = Path.Combine(artifactsDir, visualCase.name + ".legacy-ambient-sh-on.png");
            string ambientToonLitOffPath = Path.Combine(artifactsDir, visualCase.name + ".toon-lit-ambient-sh-off.png");
            string ambientToonLitOnPath = Path.Combine(artifactsDir, visualCase.name + ".toon-lit-ambient-sh-on.png");
            string fogLegacyOffPath = Path.Combine(artifactsDir, visualCase.name + ".legacy-fog-off.png");
            string fogLegacyOnPath = Path.Combine(artifactsDir, visualCase.name + ".legacy-fog-on.png");
            string fogToonLitOffPath = Path.Combine(artifactsDir, visualCase.name + ".toon-lit-fog-off.png");
            string fogToonLitOnPath = Path.Combine(artifactsDir, visualCase.name + ".toon-lit-fog-on.png");

            MmdGeneratedPmxVisualCaseReport ambientLegacyOff = RenderAmbientFogCase(
                visualCase, fixtureDirectory, ambientLegacyOffPath, MmdMaterialPreset.MmdToon,
                ambientShEnabled: false, fogEnabled: false);
            MmdGeneratedPmxVisualCaseReport ambientLegacyOn = RenderAmbientFogCase(
                visualCase, fixtureDirectory, ambientLegacyOnPath, MmdMaterialPreset.MmdToon,
                ambientShEnabled: true, fogEnabled: false);
            MmdGeneratedPmxVisualCaseReport ambientToonLitOff = RenderAmbientFogCase(
                visualCase, fixtureDirectory, ambientToonLitOffPath, MmdMaterialPreset.MmdToonLit,
                ambientShEnabled: false, fogEnabled: false);
            MmdGeneratedPmxVisualCaseReport ambientToonLitOn = RenderAmbientFogCase(
                visualCase, fixtureDirectory, ambientToonLitOnPath, MmdMaterialPreset.MmdToonLit,
                ambientShEnabled: true, fogEnabled: false);

            MmdGeneratedPmxVisualCaseReport fogLegacyOff = RenderAmbientFogCase(
                visualCase, fixtureDirectory, fogLegacyOffPath, MmdMaterialPreset.MmdToon,
                ambientShEnabled: false, fogEnabled: false);
            MmdGeneratedPmxVisualCaseReport fogLegacyOn = RenderAmbientFogCase(
                visualCase, fixtureDirectory, fogLegacyOnPath, MmdMaterialPreset.MmdToon,
                ambientShEnabled: false, fogEnabled: true);
            MmdGeneratedPmxVisualCaseReport fogToonLitOff = RenderAmbientFogCase(
                visualCase, fixtureDirectory, fogToonLitOffPath, MmdMaterialPreset.MmdToonLit,
                ambientShEnabled: false, fogEnabled: false);
            MmdGeneratedPmxVisualCaseReport fogToonLitOn = RenderAmbientFogCase(
                visualCase, fixtureDirectory, fogToonLitOnPath, MmdMaterialPreset.MmdToonLit,
                ambientShEnabled: false, fogEnabled: true);

            AssertCaptureEvidence(ambientLegacyOff, MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName, "ambient Legacy off");
            AssertCaptureEvidence(ambientLegacyOn, MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName, "ambient Legacy on");
            AssertCaptureEvidence(ambientToonLitOff, MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName, "ambient Toon Lit off");
            AssertCaptureEvidence(ambientToonLitOn, MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName, "ambient Toon Lit on");
            AssertCaptureEvidence(fogLegacyOff, MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName, "fog Legacy off");
            AssertCaptureEvidence(fogLegacyOn, MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName, "fog Legacy on");
            AssertCaptureEvidence(fogToonLitOff, MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName, "fog Toon Lit off");
            AssertCaptureEvidence(fogToonLitOn, MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName, "fog Toon Lit on");

            float ambientLegacyDelta = MmdFlipHelper.ComputeMeanError(ambientLegacyOffPath, ambientLegacyOnPath, artifactsDir);
            float ambientToonLitDelta = MmdFlipHelper.ComputeMeanError(ambientToonLitOffPath, ambientToonLitOnPath, artifactsDir);
            float fogLegacyDelta = MmdFlipHelper.ComputeMeanError(fogLegacyOffPath, fogLegacyOnPath, artifactsDir);
            float fogToonLitDelta = MmdFlipHelper.ComputeMeanError(fogToonLitOffPath, fogToonLitOnPath, artifactsDir);
            const float minimumVisibleDelta = 0.01f;
            Assert.That(ambientLegacyDelta, Is.LessThanOrEqualTo(0.0001f),
                "Legacy MMD Toon must remain invariant when only Ambient SH changes.");
            Assert.That(ambientToonLitDelta, Is.GreaterThan(ambientLegacyDelta + minimumVisibleDelta),
                "MMD Toon Lit must visibly follow Ambient SH while Legacy stays invariant.");
            Assert.That(fogLegacyDelta, Is.LessThanOrEqualTo(0.0001f),
                "Legacy MMD Toon must remain invariant when only fog changes.");
            Assert.That(fogToonLitDelta, Is.GreaterThan(fogLegacyDelta + minimumVisibleDelta),
                "MMD Toon Lit must visibly follow fog while Legacy stays invariant.");
            TestContext.WriteLine(
                $"[toon-lit-ambient-fog] ambient legacy={ambientLegacyDelta:F6} toon-lit={ambientToonLitDelta:F6} "
                + $"fog legacy={fogLegacyDelta:F6} toon-lit={fogToonLitDelta:F6}");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [Explicit("Run the S1b URP SSAO visual delta explicitly; FLIP artifacts still require human review.")]
        [Category("VisualShadingTier")]
        public void ToonRampToonLit_TracksSsaoWhileLegacyStaysInvariant()
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

            string fixtureDirectory = ResolveGeneratedPmxFixtureDirectory();
            string pmxPath = Path.Combine(fixtureDirectory, visualCase.modelFileName);
            if (!File.Exists(pmxPath))
            {
                RequireOrOptOut(optedOut, "Generated PMX fixture missing: " + pmxPath);
            }

            string artifactsDir = ResolveArtifactsDir();
            Directory.CreateDirectory(artifactsDir);
            string legacyOffPath = Path.Combine(artifactsDir, visualCase.name + ".legacy-ssao-off.png");
            string legacyOnPath = Path.Combine(artifactsDir, visualCase.name + ".legacy-ssao-on.png");
            string toonLitOffPath = Path.Combine(artifactsDir, visualCase.name + ".toon-lit-ssao-off.png");
            string toonLitOnPath = Path.Combine(artifactsDir, visualCase.name + ".toon-lit-ssao-on.png");

            // The first renderer-feature toggle may allocate SSAO intermediates or warm shader
            // variants. Keep that infrastructure work out of the measured Legacy invariant.
            MmdGeneratedPmxVisualCaseReport warmupOff = RenderSsaoCase(
                visualCase,
                fixtureDirectory,
                Path.Combine(artifactsDir, visualCase.name + ".ssao-warmup-off.png"),
                MmdMaterialPreset.MmdToon,
                ssaoEnabled: false);
            MmdGeneratedPmxVisualCaseReport warmupOn = RenderSsaoCase(
                visualCase,
                fixtureDirectory,
                Path.Combine(artifactsDir, visualCase.name + ".ssao-warmup-on.png"),
                MmdMaterialPreset.MmdToon,
                ssaoEnabled: true);
            AssertCaptureEvidence(warmupOff, MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName, "SSAO warm-up off");
            AssertCaptureEvidence(warmupOn, MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName, "SSAO warm-up on");

            MmdGeneratedPmxVisualCaseReport legacyOff = RenderSsaoCase(
                visualCase, fixtureDirectory, legacyOffPath, MmdMaterialPreset.MmdToon, ssaoEnabled: false);
            MmdGeneratedPmxVisualCaseReport legacyOn = RenderSsaoCase(
                visualCase, fixtureDirectory, legacyOnPath, MmdMaterialPreset.MmdToon, ssaoEnabled: true);
            MmdGeneratedPmxVisualCaseReport toonLitOff = RenderSsaoCase(
                visualCase, fixtureDirectory, toonLitOffPath, MmdMaterialPreset.MmdToonLit, ssaoEnabled: false);
            MmdGeneratedPmxVisualCaseReport toonLitOn = RenderSsaoCase(
                visualCase, fixtureDirectory, toonLitOnPath, MmdMaterialPreset.MmdToonLit, ssaoEnabled: true);

            AssertCaptureEvidence(legacyOff, MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName, "SSAO Legacy off");
            AssertCaptureEvidence(legacyOn, MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName, "SSAO Legacy on");
            AssertCaptureEvidence(toonLitOff, MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName, "SSAO Toon Lit off");
            AssertCaptureEvidence(toonLitOn, MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName, "SSAO Toon Lit on");
            Assert.That(legacyOff.ssaoAvailable, Is.True, "SSAO off capture must find the configured URP feature.");
            Assert.That(legacyOn.ssaoAvailable, Is.True, "SSAO on capture must find the configured URP feature.");
            Assert.That(toonLitOff.ssaoConfigured, Is.True, "SSAO off capture must configure the renderer feature.");
            Assert.That(toonLitOn.ssaoConfigured, Is.True, "SSAO on capture must configure the renderer feature.");

            float legacyDelta = MmdFlipHelper.ComputeMeanError(legacyOffPath, legacyOnPath, artifactsDir);
            float toonLitDelta = MmdFlipHelper.ComputeMeanError(toonLitOffPath, toonLitOnPath, artifactsDir);
            const float minimumVisibleDelta = 0.01f;
            Assert.That(legacyDelta, Is.LessThanOrEqualTo(0.0001f),
                "Legacy MMD Toon must remain invariant when only the URP SSAO feature changes.");
            Assert.That(toonLitDelta, Is.GreaterThan(legacyDelta + minimumVisibleDelta),
                "MMD Toon Lit must visibly consume URP SSAO indirectAmbientOcclusion.");
            TestContext.WriteLine(
                $"[toon-lit-ssao] legacy={legacyDelta:F6} toon-lit={toonLitDelta:F6}");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [Explicit("Run the S1b URP reflection-probe visual delta explicitly; FLIP artifacts still require human review.")]
        [Category("VisualShadingTier")]
        public void ToonRampToonLit_TracksReflectionProbeWhileLegacyStaysInvariant()
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

            string fixtureDirectory = ResolveGeneratedPmxFixtureDirectory();
            string pmxPath = Path.Combine(fixtureDirectory, visualCase.modelFileName);
            if (!File.Exists(pmxPath))
            {
                RequireOrOptOut(optedOut, "Generated PMX fixture missing: " + pmxPath);
            }

            string artifactsDir = ResolveArtifactsDir();
            Directory.CreateDirectory(artifactsDir);
            string legacyOffPath = Path.Combine(artifactsDir, visualCase.name + ".legacy-reflection-probe-off.png");
            string legacyOnPath = Path.Combine(artifactsDir, visualCase.name + ".legacy-reflection-probe-on.png");
            string toonLitOffPath = Path.Combine(artifactsDir, visualCase.name + ".toon-lit-reflection-probe-off.png");
            string toonLitOnPath = Path.Combine(artifactsDir, visualCase.name + ".toon-lit-reflection-probe-on.png");

            MmdGeneratedPmxVisualCaseReport legacyOff = RenderReflectionProbeCase(
                visualCase, fixtureDirectory, legacyOffPath, MmdMaterialPreset.MmdToon, enabled: false);
            MmdGeneratedPmxVisualCaseReport legacyOn = RenderReflectionProbeCase(
                visualCase, fixtureDirectory, legacyOnPath, MmdMaterialPreset.MmdToon, enabled: true);
            MmdGeneratedPmxVisualCaseReport toonLitOff = RenderReflectionProbeCase(
                visualCase, fixtureDirectory, toonLitOffPath, MmdMaterialPreset.MmdToonLit, enabled: false);
            MmdGeneratedPmxVisualCaseReport toonLitOn = RenderReflectionProbeCase(
                visualCase, fixtureDirectory, toonLitOnPath, MmdMaterialPreset.MmdToonLit, enabled: true);

            AssertCaptureEvidence(legacyOff, MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName, "Reflection Probe Legacy off");
            AssertCaptureEvidence(legacyOn, MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName, "Reflection Probe Legacy on");
            AssertCaptureEvidence(toonLitOff, MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName, "Reflection Probe Toon Lit off");
            AssertCaptureEvidence(toonLitOn, MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName, "Reflection Probe Toon Lit on");
            Assert.That(legacyOff.reflectionProbeAvailable, Is.True, "Reflection Probe off capture must configure the local probe.");
            Assert.That(legacyOn.reflectionProbeAvailable, Is.True, "Reflection Probe on capture must configure the local probe.");
            Assert.That(toonLitOff.reflectionProbeConfigured, Is.True, "Reflection Probe off capture must configure material binding.");
            Assert.That(toonLitOn.reflectionProbeConfigured, Is.True, "Reflection Probe on capture must configure material binding.");

            float legacyDelta = MmdFlipHelper.ComputeMeanError(legacyOffPath, legacyOnPath, artifactsDir);
            float toonLitDelta = MmdFlipHelper.ComputeMeanError(toonLitOffPath, toonLitOnPath, artifactsDir);
            const float minimumVisibleDelta = 0.01f;
            Assert.That(legacyDelta, Is.LessThanOrEqualTo(0.0001f),
                "Legacy MMD Toon must remain invariant when only the shared custom reflection cubemap changes.");
            Assert.That(toonLitDelta, Is.GreaterThan(legacyDelta + minimumVisibleDelta),
                "MMD Toon Lit must visibly consume URP GlossyEnvironmentReflection.");
            TestContext.WriteLine(
                $"[toon-lit-reflection-probe] legacy={legacyDelta:F6} toon-lit={toonLitDelta:F6}");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [Explicit("Run the S1c Toon boundary/feather visual delta explicitly; FLIP artifacts still require human review.")]
        [Category("VisualShadingTier")]
        public void ToonRampToonLit_TracksToonBoundaryAndFeatherWhileLegacyStaysInvariant()
        {
            RunToonAuthoringDeltaCase(
                "toon-boundary",
                offBoundary: -1.0f,
                offFeather: -1.0f,
                offBandCount: -1.0f,
                onBoundary: 0.55f,
                onFeather: 0.12f,
                onBandCount: -1.0f,
                feature: "toon-boundary-feather");
        }

        [Test]
        [Explicit("Run the S1c Toon band-count visual delta explicitly; FLIP artifacts still require human review.")]
        [Category("VisualShadingTier")]
        public void ToonRampToonLit_TracksToonBandCountWhileLegacyStaysInvariant()
        {
            RunToonAuthoringDeltaCase(
                "band-count",
                offBoundary: 0.55f,
                offFeather: 0.12f,
                offBandCount: -1.0f,
                onBoundary: 0.55f,
                onFeather: 0.12f,
                onBandCount: 3.0f,
                feature: "toon-band-count");
        }

        [Test]
        [Explicit("Run the S1c stylized-specular visual delta explicitly; FLIP artifacts still require human review.")]
        [Category("VisualShadingTier")]
        public void ToonRampToonLit_TracksStylizedSpecularWhileLegacyStaysInvariant()
        {
            RunToonAuthoringDeltaCase(
                "stylized-specular",
                offBoundary: -1.0f,
                offFeather: -1.0f,
                offBandCount: -1.0f,
                onBoundary: -1.0f,
                onFeather: -1.0f,
                onBandCount: -1.0f,
                feature: "stylized-specular",
                offStylizedSpecularColor: Color.white,
                offStylizedSpecularBoundary: -1.0f,
                offStylizedSpecularFeather: -1.0f,
                onStylizedSpecularColor: new Color(1.0f, 0.35f, 0.1f, 1.0f),
                onStylizedSpecularBoundary: 0.90f,
                onStylizedSpecularFeather: 0.03f);
        }

        [Test]
        [Explicit("Run the S1c rim-light visual delta explicitly; FLIP artifacts still require human review.")]
        [Category("VisualShadingTier")]
        public void ToonRampToonLit_TracksRimLightWhileLegacyStaysInvariant()
        {
            RunToonAuthoringDeltaCase(
                "rim-light",
                offBoundary: -1.0f,
                offFeather: -1.0f,
                offBandCount: -1.0f,
                onBoundary: -1.0f,
                onFeather: -1.0f,
                onBandCount: -1.0f,
                feature: "rim-light-fixed",
                offRimColor: Color.white,
                offRimBoundary: -1.0f,
                offRimFeather: -1.0f,
                offRimLightFollow: 0.0f,
                onRimColor: new Color(1.0f, 0.75f, 0.35f, 1.0f),
                onRimBoundary: 0.58f,
                onRimFeather: 0.08f,
                onRimLightFollow: 0.0f);
        }

        [Test]
        [Explicit("Run the S1c rim-light follow visual delta explicitly; FLIP artifacts still require human review.")]
        [Category("VisualShadingTier")]
        public void ToonRampToonLit_TracksRimLightFollowWhileLegacyStaysInvariant()
        {
            RunToonAuthoringDeltaCase(
                "rim-light-follow",
                offBoundary: -1.0f,
                offFeather: -1.0f,
                offBandCount: -1.0f,
                onBoundary: -1.0f,
                onFeather: -1.0f,
                onBandCount: -1.0f,
                feature: "rim-light-follow",
                offRimColor: Color.white,
                offRimBoundary: 0.58f,
                offRimFeather: 0.08f,
                offRimLightFollow: 0.0f,
                onRimColor: Color.white,
                onRimBoundary: 0.58f,
                onRimFeather: 0.08f,
                onRimLightFollow: 1.0f);
        }

        [Test]
        [Explicit("Run the S1c HDR emission visual delta explicitly; FLIP artifacts still require human review.")]
        [Category("VisualShadingTier")]
        public void ToonRampToonLit_TracksHdrEmissionWhileLegacyStaysInvariant()
        {
            RunToonAuthoringDeltaCase(
                "hdr-emission",
                offBoundary: -1.0f,
                offFeather: -1.0f,
                offBandCount: -1.0f,
                onBoundary: -1.0f,
                onFeather: -1.0f,
                onBandCount: -1.0f,
                feature: "hdr-emission",
                offEmissionColor: Color.white,
                offEmissionIntensity: -1.0f,
                onEmissionColor: new Color(3.0f, 1.2f, 0.25f, 1.0f),
                onEmissionIntensity: 1.0f);
        }

        private static void RunToonAuthoringDeltaCase(
            string artifactStem,
            float offBoundary,
            float offFeather,
            float offBandCount,
            float onBoundary,
            float onFeather,
            float onBandCount,
            string feature,
            Color? offStylizedSpecularColor = null,
            float? offStylizedSpecularBoundary = null,
            float? offStylizedSpecularFeather = null,
            Color? onStylizedSpecularColor = null,
            float? onStylizedSpecularBoundary = null,
            float? onStylizedSpecularFeather = null,
            Color? offRimColor = null,
            float? offRimBoundary = null,
            float? offRimFeather = null,
            float? offRimLightFollow = null,
            Color? onRimColor = null,
            float? onRimBoundary = null,
            float? onRimFeather = null,
            float? onRimLightFollow = null,
            Color? offEmissionColor = null,
            float? offEmissionIntensity = null,
            Color? onEmissionColor = null,
            float? onEmissionIntensity = null)
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

            string fixtureDirectory = ResolveGeneratedPmxFixtureDirectory();
            string pmxPath = Path.Combine(fixtureDirectory, visualCase.modelFileName);
            if (!File.Exists(pmxPath))
            {
                RequireOrOptOut(optedOut, "Generated PMX fixture missing: " + pmxPath);
            }

            string artifactsDir = ResolveArtifactsDir();
            Directory.CreateDirectory(artifactsDir);
            string legacyOffPath = Path.Combine(artifactsDir, visualCase.name + ".legacy-" + artifactStem + "-off.png");
            string legacyOnPath = Path.Combine(artifactsDir, visualCase.name + ".legacy-" + artifactStem + "-on.png");
            string toonLitOffPath = Path.Combine(artifactsDir, visualCase.name + ".toon-lit-" + artifactStem + "-off.png");
            string toonLitOnPath = Path.Combine(artifactsDir, visualCase.name + ".toon-lit-" + artifactStem + "-on.png");

            MmdGeneratedPmxVisualCaseReport legacyOff = RenderToonBoundaryCase(
                visualCase, fixtureDirectory, legacyOffPath, MmdMaterialPreset.MmdToon, offBoundary, offFeather, offBandCount,
                offStylizedSpecularColor, offStylizedSpecularBoundary, offStylizedSpecularFeather,
                offRimColor, offRimBoundary, offRimFeather, offRimLightFollow,
                offEmissionColor, offEmissionIntensity);
            MmdGeneratedPmxVisualCaseReport legacyOn = RenderToonBoundaryCase(
                visualCase, fixtureDirectory, legacyOnPath, MmdMaterialPreset.MmdToon, onBoundary, onFeather, onBandCount,
                onStylizedSpecularColor, onStylizedSpecularBoundary, onStylizedSpecularFeather,
                onRimColor, onRimBoundary, onRimFeather, onRimLightFollow,
                onEmissionColor, onEmissionIntensity);
            MmdGeneratedPmxVisualCaseReport toonLitOff = RenderToonBoundaryCase(
                visualCase, fixtureDirectory, toonLitOffPath, MmdMaterialPreset.MmdToonLit, offBoundary, offFeather, offBandCount,
                offStylizedSpecularColor, offStylizedSpecularBoundary, offStylizedSpecularFeather,
                offRimColor, offRimBoundary, offRimFeather, offRimLightFollow,
                offEmissionColor, offEmissionIntensity);
            MmdGeneratedPmxVisualCaseReport toonLitOn = RenderToonBoundaryCase(
                visualCase, fixtureDirectory, toonLitOnPath, MmdMaterialPreset.MmdToonLit, onBoundary, onFeather, onBandCount,
                onStylizedSpecularColor, onStylizedSpecularBoundary, onStylizedSpecularFeather,
                onRimColor, onRimBoundary, onRimFeather, onRimLightFollow,
                onEmissionColor, onEmissionIntensity);

            AssertCaptureEvidence(legacyOff, MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName, "Toon boundary Legacy off");
            AssertCaptureEvidence(legacyOn, MmdUrpMaterialBindingDescriptorBuilder.DefaultShaderName, "Toon boundary Legacy on");
            AssertCaptureEvidence(toonLitOff, MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName, "Toon boundary Toon Lit off");
            AssertCaptureEvidence(toonLitOn, MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName, "Toon boundary Toon Lit on");
            Assert.That(toonLitOff.toonBoundaryConfigured, Is.True, "Toon Lit off capture must expose both authoring properties.");
            Assert.That(toonLitOn.toonBoundaryConfigured, Is.True, "Toon Lit on capture must expose both authoring properties.");
            Assert.That(toonLitOff.stylizedSpecularConfigured, Is.True, "Toon Lit off capture must expose stylized specular properties.");
            Assert.That(toonLitOn.stylizedSpecularConfigured, Is.True, "Toon Lit on capture must expose stylized specular properties.");
            Assert.That(toonLitOff.rimConfigured, Is.True, "Toon Lit off capture must expose rim properties.");
            Assert.That(toonLitOn.rimConfigured, Is.True, "Toon Lit on capture must expose rim properties.");
            Assert.That(toonLitOff.emissionConfigured, Is.True, "Toon Lit off capture must expose HDR emission properties.");
            Assert.That(toonLitOn.emissionConfigured, Is.True, "Toon Lit on capture must expose HDR emission properties.");

            float legacyDelta = MmdFlipHelper.ComputeMeanError(legacyOffPath, legacyOnPath, artifactsDir);
            float toonLitDelta = MmdFlipHelper.ComputeMeanError(toonLitOffPath, toonLitOnPath, artifactsDir);
            const float minimumVisibleDelta = 0.01f;
            Assert.That(legacyDelta, Is.LessThanOrEqualTo(0.0001f),
                "Legacy MMD Toon must remain invariant when only Toon Lit authoring properties change.");
            Assert.That(toonLitDelta, Is.GreaterThan(legacyDelta + minimumVisibleDelta),
                "MMD Toon Lit must visibly consume the isolated " + feature + " authoring change.");
            TestContext.WriteLine(
                $"[toon-lit-{feature}] legacy={legacyDelta:F6} toon-lit={toonLitDelta:F6}");
            LogAssert.NoUnexpectedReceived();
        }

        private static MmdGeneratedPmxVisualCaseReport RenderToonBoundaryCase(
            MmdGeneratedPmxVisualCase visualCase,
            string fixtureDirectory,
            string capturePath,
            MmdMaterialPreset materialPreset,
            float? toonBoundary,
            float? toonFeather,
            float? toonBandCount,
            Color? stylizedSpecularColor = null,
            float? stylizedSpecularBoundary = null,
            float? stylizedSpecularFeather = null,
            Color? rimColor = null,
            float? rimBoundary = null,
            float? rimFeather = null,
            float? rimLightFollow = null,
            Color? emissionColor = null,
            float? emissionIntensity = null)
        {
            return MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                visualCase,
                fixtureDirectory,
                capturePath,
                backgroundEnabled: true,
                postProcessingEnabled: false,
                materialPreset: materialPreset,
                ambientShEnabledOverride: false,
                fogEnabledOverride: false,
                toonBoundaryOverride: toonBoundary,
                toonFeatherOverride: toonFeather,
                toonBandCountOverride: toonBandCount,
                stylizedSpecularColorOverride: stylizedSpecularColor,
                stylizedSpecularBoundaryOverride: stylizedSpecularBoundary,
                stylizedSpecularFeatherOverride: stylizedSpecularFeather,
                rimColorOverride: rimColor,
                rimBoundaryOverride: rimBoundary,
                rimFeatherOverride: rimFeather,
                rimLightFollowOverride: rimLightFollow,
                emissionColorOverride: emissionColor,
                emissionIntensityOverride: emissionIntensity);
        }

        private static MmdGeneratedPmxVisualCaseReport RenderReflectionProbeCase(
            MmdGeneratedPmxVisualCase visualCase,
            string fixtureDirectory,
            string capturePath,
            MmdMaterialPreset materialPreset,
            bool enabled)
        {
            return MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                visualCase,
                fixtureDirectory,
                capturePath,
                backgroundEnabled: true,
                postProcessingEnabled: false,
                materialPreset: materialPreset,
                directionalLightColorOverride: Color.black,
                directionalLightIntensityOverride: 0.0f,
                ambientLightColorOverride: Color.black,
                ambientLightIntensityOverride: 0.0f,
                ambientShEnabledOverride: false,
                fogEnabledOverride: false,
                reflectionProbeEnabledOverride: enabled);
        }

        private static MmdGeneratedPmxVisualCaseReport RenderSsaoCase(
            MmdGeneratedPmxVisualCase visualCase,
            string fixtureDirectory,
            string capturePath,
            MmdMaterialPreset materialPreset,
            bool ssaoEnabled)
        {
            return MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                visualCase,
                fixtureDirectory,
                capturePath,
                backgroundEnabled: true,
                postProcessingEnabled: false,
                materialPreset: materialPreset,
                directionalLightColorOverride: Color.black,
                directionalLightIntensityOverride: 0.0f,
                ambientLightColorOverride: Color.black,
                ambientLightIntensityOverride: 0.0f,
                ambientShEnabledOverride: true,
                fogEnabledOverride: false,
                ssaoEnabledOverride: ssaoEnabled);
        }

        private static MmdGeneratedPmxVisualCaseReport RenderAmbientFogCase(
            MmdGeneratedPmxVisualCase visualCase,
            string fixtureDirectory,
            string capturePath,
            MmdMaterialPreset materialPreset,
            bool ambientShEnabled,
            bool fogEnabled)
        {
            return MmdEditorRenderingDiagnostics.RenderGeneratedPmxVisualCase(
                visualCase,
                fixtureDirectory,
                capturePath,
                backgroundEnabled: true,
                postProcessingEnabled: false,
                materialPreset: materialPreset,
                directionalLightColorOverride: Color.black,
                directionalLightIntensityOverride: 0.0f,
                ambientLightColorOverride: Color.black,
                ambientLightIntensityOverride: 0.0f,
                ambientShEnabledOverride: ambientShEnabled,
                fogEnabledOverride: fogEnabled);
        }

        private static void AssertCaptureEvidence(
            MmdGeneratedPmxVisualCaseReport report,
            string expectedShader,
            string label)
        {
            Assert.That(report.status, Is.EqualTo("passed"), label + ": capture status must be passed.");
            Assert.That(report.shaderName, Is.EqualTo(expectedShader), label + ": shader profile mismatch.");
            Assert.That(report.captureUsedStandardRequest, Is.True, label + ": StandardRequest is required.");
            Assert.That(report.selectedMaterialPassValid, Is.True, label + ": selected ForwardLit pass is invalid.");
            Assert.That(report.selectedMaterialPassName, Is.EqualTo("ForwardLit"), label + ": pass name mismatch.");
            string expectedLightMode = expectedShader == MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName
                ? "UniversalForwardOnly"
                : "UniversalForward";
            Assert.That(report.selectedMaterialLightMode, Is.EqualTo(expectedLightMode), label + ": LightMode mismatch.");
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
    }
}
