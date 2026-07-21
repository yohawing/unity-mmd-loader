#nullable enable

using System;
using System.Collections.Generic;

namespace Mmd.Tests
{
    /// <summary>
    /// Canonical schema (schemaVersion 1) for the visual review manifest consumed by
    /// tools/new-visual-review.ps1 and produced by MmdGeneratedPmxVisualParityTests.
    ///
    /// This is the single source of truth for the manifest shape. Any other tool or
    /// script that reads/writes a manifest.json with this schema (for example
    /// scripts/verify-unity-toon-shader-sample.ps1, which cannot reference this
    /// assembly because it lives outside the devtools asmdef graph) MUST treat the
    /// field list below as authoritative and keep field names/types in sync by hand.
    ///
    /// Backward compatibility note: <see cref="VisualReviewCase.passed"/> is a
    /// machine-computed FLIP-metric verdict only. It is NOT a human review decision.
    /// Consumers (notably tools/new-visual-review.ps1) must render it as a "metric"
    /// indicator distinct from the human Accept/Reject/Needs follow-up decision that
    /// is recorded separately (in the reviewer's browser localStorage / review.json
    /// export), never as an overall "pass" badge.
    /// </summary>
    [Serializable]
    public sealed class VisualReviewManifest
    {
        public int schemaVersion = 1;
        public string artifactKind = "legacy-parity";
        public string runId = string.Empty;
        public string unityVersion = string.Empty;
        public string urpVersion = string.Empty;
        public string gpu = string.Empty;
        public string humanSignoff = string.Empty;
        public List<VisualReviewCase> cases = new();
    }

    /// <summary>
    /// One reference/candidate/heatmap comparison case inside a
    /// <see cref="VisualReviewManifest"/>. See the manifest-level doc comment for the
    /// machine-metric vs. human-decision distinction that <see cref="passed"/> is
    /// part of.
    /// </summary>
    [Serializable]
    public sealed class VisualReviewCase
    {
        public string id = string.Empty;
        public string feature = string.Empty;
        public string reference = string.Empty;
        public string candidate = string.Empty;
        public string heatmap = string.Empty;
        public float flipMean;
        public float flipCeiling;
        public float expectedDeltaFloor;

        /// <summary>
        /// Machine-computed FLIP-metric verdict (within the configured floor/ceiling).
        /// This is NOT a human review decision; render/consume it as a "metric" or
        /// "machine" indicator only. The authoritative pass/fail for release purposes
        /// is the human decision recorded in the reviewer's review.json export.
        /// </summary>
        public bool passed;
        public string shaderProfile = string.Empty;
        public string selectedMaterialPassName = string.Empty;
        public string selectedMaterialLightMode = string.Empty;
        public int selectedMaterialPassIndex = -1;
        public bool selectedMaterialPassEnabled;
        public bool selectedMaterialPassValid;
        public string captureRequestType = string.Empty;
        public string captureRenderPath = string.Empty;
        public bool captureUsedStandardRequest;
        public string renderPipelineName = string.Empty;
        public float[] mainLightColor = Array.Empty<float>();
        public bool ambientShEnabled;
        public string ambientShMode = string.Empty;
        public float[] ambientShColor = Array.Empty<float>();
        public float ambientShIntensity;
        public bool fogEnabled;
        public string fogMode = string.Empty;
        public float[] fogColor = Array.Empty<float>();
        public float fogDensity;
        public float fogStartDistance;
        public float fogEndDistance;
        public bool ssaoEnabled;
        public bool ssaoAvailable;
        public bool ssaoConfigured;
        public string ssaoMode = string.Empty;
        public bool reflectionProbeEnabled;
        public bool reflectionProbeAvailable;
        public bool reflectionProbeConfigured;
        public string reflectionProbeMode = string.Empty;
        public float toonBoundary = -1.0f;
        public float toonFeather = -1.0f;
        public float toonBandCount = -1.0f;
        public bool toonBoundaryConfigured;
        public string toonBoundaryMode = string.Empty;
        public float[] stylizedSpecularColor = Array.Empty<float>();
        public float stylizedSpecularBoundary = -1.0f;
        public float stylizedSpecularFeather = -1.0f;
        public bool stylizedSpecularConfigured;
        public string stylizedSpecularMode = string.Empty;
        public float[] rimColor = Array.Empty<float>();
        public float rimBoundary = -1.0f;
        public float rimFeather = -1.0f;
        public float rimLightFollow;
        public bool rimConfigured;
        public string rimMode = string.Empty;
        public float[] emissionColor = Array.Empty<float>();
        public float emissionIntensity = -1.0f;
        public bool emissionConfigured;
        public string emissionMode = string.Empty;
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
        public string legacyReference = string.Empty;
        public string legacyCandidate = string.Empty;
        public float legacyFlipMean;
        public string toonLitLightReference = string.Empty;
        public string toonLitLightCandidate = string.Empty;
        public float toonLitLightFlipMean;
        public string toonLitLightHeatmap = string.Empty;
        public string featureReference = string.Empty;
        public string featureCandidate = string.Empty;
        public float featureFlipMean;
        public string featureHeatmap = string.Empty;
        public string legacyFeatureHeatmap = string.Empty;
        public string profileReference = string.Empty;
        public string profileCandidate = string.Empty;
        public float profileFlipMean;
        public string profileHeatmap = string.Empty;
        public string legacyShadowReference = string.Empty;
        public string legacyShadowCandidate = string.Empty;
        public float legacyShadowFlipMean;
        public string toonLitShadowReference = string.Empty;
        public string toonLitShadowCandidate = string.Empty;
        public float toonLitShadowFlipMean;
        public string toonLitShadowHeatmap = string.Empty;
    }
}
