#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Mmd.Parser;
using Mmd.Rendering;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    public static class MmdEditorRenderingDiagnostics
    {
        public static readonly MmdGeneratedPmxVisualCase[] GeneratedPmxVisualCases = new[]
        {
            new MmdGeneratedPmxVisualCase("mmd-toon-ramp-lit-box", "mmd-toon-ramp-lit-box.pmx", new Vector3(-0.06f, 0.6f, 3.3f), new Vector3(-0.06f, 0.6f, 0.0f), 27.0f, 0.03f)
        };
        private static readonly Color GeneratedPmxAmbientLightColor = Color.white;
        private const float GeneratedPmxAmbientLightIntensity = 1.2f;
        // MMD's default directional light color is (154,154,154) ≈ 0.604 (not white).
        private static readonly Color GeneratedPmxDirectionalLightColor = new Color(154f / 255f, 154f / 255f, 154f / 255f, 1.0f);
        private const float GeneratedPmxDirectionalLightIntensity = 1.8f;
        private static readonly Vector3 GeneratedPmxDirectionalLightPosition = new Vector3(2.5f, 5.0f, 4.0f);
        private static readonly Vector3 GeneratedPmxMmdLightTravelDirection = new Vector3(0.5f, -1.0f, -0.5f);
        private const string GeneratedPmxCameraCoordinatePolicy = "camera-x-mirrored-for-unity-handedness";
        private static readonly Color GeneratedPmxAmbientShColor = new Color(0.35f, 0.08f, 0.18f, 1.0f);
        private const float GeneratedPmxAmbientShIntensity = 1.0f;
        private static readonly Color GeneratedPmxFogColor = new Color(0.20f, 0.35f, 0.60f, 1.0f);
        private const float GeneratedPmxFogStartDistance = 0.5f;
        private const float GeneratedPmxFogEndDistance = 3.5f;
        private static readonly int MainLightColorId = Shader.PropertyToID("_MainLightColor");

        public static MmdGeneratedPmxVisualCaseReport RenderGeneratedPmxVisualCase(
            MmdGeneratedPmxVisualCase visualCase,
            string fixtureDirectory,
            string capturePath,
            bool backgroundEnabled,
            bool postProcessingEnabled,
            bool perturbShaderOutput = false,
            MmdMaterialPreset materialPreset = MmdMaterialPreset.MmdToon,
            Color? directionalLightColorOverride = null,
            float? directionalLightIntensityOverride = null,
            Color? ambientLightColorOverride = null,
            float? ambientLightIntensityOverride = null,
            bool realtimeShadowOccluderEnabled = false,
            bool? ambientShEnabledOverride = null,
            bool? fogEnabledOverride = null,
            bool? ssaoEnabledOverride = null,
            bool? reflectionProbeEnabledOverride = null,
            float? toonBoundaryOverride = null,
            float? toonFeatherOverride = null)
        {
            const int width = 1024;
            const int height = 1024;
            string pmxPath = Path.Combine(fixtureDirectory, visualCase.modelFileName);
            if (!File.Exists(pmxPath))
            {
                throw new FileNotFoundException("Generated PMX fixture was not found.", pmxPath);
            }

            var cameraObject = new GameObject("phase17-generated-pmx-camera");
            var lightObject = new GameObject("phase17-generated-pmx-light");
            GameObject? shadowOccluder = null;
            GameObject? ssaoOccluder = null;
            Material? ssaoOccluderMaterial = null;
            GameObject? reflectionProbeObject = null;
            ReflectionProbe? reflectionProbe = null;
            Cubemap? reflectionProbeCubemap = null;
            RenderTexture? renderTexture = null;
            Texture2D? pixels = null;
            MmdUnityModelInstance? instance = null;
            MmdGeneratedPmxMaterialProxyStats? proxyStats = null;
            string selectedMaterialPassName = string.Empty;
            string selectedMaterialLightMode = string.Empty;
            int selectedMaterialPassIndex = -1;
            bool selectedMaterialPassEnabled = false;
            bool selectedMaterialPassValid = false;
            bool usedStandardRequest = false;
            bool ssaoAvailable = false;
            bool ssaoConfigured = !ssaoEnabledOverride.HasValue;
            bool ssaoEnabled = ssaoEnabledOverride == true;
            bool reflectionProbeAvailable = false;
            bool reflectionProbeConfigured = !reflectionProbeEnabledOverride.HasValue;
            bool reflectionProbeEnabled = reflectionProbeEnabledOverride == true;
            bool toonBoundaryConfigured = !toonBoundaryOverride.HasValue && !toonFeatherOverride.HasValue;
            float toonBoundary = toonBoundaryOverride ?? -1.0f;
            float toonFeather = toonFeatherOverride ?? -1.0f;
            List<UnityEngine.Object>? configuredSsaoFeatures = null;
            List<bool>? previousSsaoFeatureStates = null;
            Color previousAmbientLight = RenderSettings.ambientLight;
            AmbientMode previousAmbientMode = RenderSettings.ambientMode;
            SphericalHarmonicsL2 previousAmbientProbe = RenderSettings.ambientProbe;
            bool previousFog = RenderSettings.fog;
            FogMode previousFogMode = RenderSettings.fogMode;
            Color previousFogColor = RenderSettings.fogColor;
            float previousFogDensity = RenderSettings.fogDensity;
            float previousFogStartDistance = RenderSettings.fogStartDistance;
            float previousFogEndDistance = RenderSettings.fogEndDistance;
            DefaultReflectionMode previousDefaultReflectionMode = RenderSettings.defaultReflectionMode;
            Texture? previousCustomReflectionTexture = RenderSettings.customReflectionTexture;
            float previousReflectionIntensity = RenderSettings.reflectionIntensity;
            Light? previousSun = RenderSettings.sun;
            Color captureAmbientLightColor = ambientLightColorOverride ?? GeneratedPmxAmbientLightColor;
            float captureAmbientLightIntensity = ambientLightIntensityOverride ?? GeneratedPmxAmbientLightIntensity;
            bool ambientShConfigured = ambientShEnabledOverride.HasValue;
            bool ambientShEnabled = ambientShEnabledOverride == true;
            bool fogConfigured = fogEnabledOverride.HasValue;
            bool fogEnabled = fogEnabledOverride == true;
            try
            {
                RenderSettings.ambientMode = ambientShConfigured
                    ? AmbientMode.Custom
                    : AmbientMode.Flat;
                RenderSettings.ambientLight = captureAmbientLightColor * captureAmbientLightIntensity;
                RenderSettings.ambientProbe = ambientShConfigured
                    ? BuildAmbientProbe(ambientShEnabled ? GeneratedPmxAmbientShColor : Color.black, GeneratedPmxAmbientShIntensity)
                    : previousAmbientProbe;
                if (fogConfigured)
                {
                    RenderSettings.fog = fogEnabled;
                    RenderSettings.fogMode = FogMode.Linear;
                    RenderSettings.fogColor = GeneratedPmxFogColor;
                    RenderSettings.fogStartDistance = GeneratedPmxFogStartDistance;
                    RenderSettings.fogEndDistance = GeneratedPmxFogEndDistance;
                    RenderSettings.fogDensity = 0.0f;
                }

                if (ssaoEnabledOverride.HasValue)
                {
                    List<UnityEngine.Object> ssaoFeatures = FindSsaoFeatures();
                    ssaoAvailable = ssaoFeatures.Count > 0;
                    if (ssaoAvailable)
                    {
                        configuredSsaoFeatures = new List<UnityEngine.Object>(ssaoFeatures.Count);
                        previousSsaoFeatureStates = new List<bool>(ssaoFeatures.Count);
                        bool allFeaturesConfigured = true;
                        foreach (UnityEngine.Object feature in ssaoFeatures)
                        {
                            if (TryGetSsaoFeatureActive(feature, out bool previousActive) &&
                                SetSsaoFeatureActive(feature, ssaoEnabled))
                            {
                                configuredSsaoFeatures.Add(feature);
                                previousSsaoFeatureStates.Add(previousActive);
                            }
                            else
                            {
                                allFeaturesConfigured = false;
                            }
                        }

                        ssaoConfigured = allFeaturesConfigured;
                    }
                }

                if (reflectionProbeEnabledOverride.HasValue)
                {
                    reflectionProbeCubemap = BuildReflectionProbeCubemap(reflectionProbeEnabled);
                    reflectionProbeObject = new GameObject("phase17-generated-pmx-reflection-probe");
                    reflectionProbeObject.transform.position = visualCase.cameraTarget;
                    reflectionProbe = reflectionProbeObject.AddComponent<ReflectionProbe>();
                    reflectionProbe.enabled = false;
                    reflectionProbe.mode = ReflectionProbeMode.Custom;
                    reflectionProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                    reflectionProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                    reflectionProbe.boxProjection = true;
                    reflectionProbe.size = new Vector3(4.0f, 4.0f, 4.0f);
                    reflectionProbe.center = Vector3.zero;
                    reflectionProbe.blendDistance = 0.0f;
                    reflectionProbe.importance = 100;
                    reflectionProbe.intensity = 1.0f;
                    reflectionProbe.customBakedTexture = reflectionProbeCubemap;
                    reflectionProbe.enabled = true;
                    RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                    RenderSettings.customReflectionTexture = reflectionProbeCubemap;
                    RenderSettings.reflectionIntensity = 1.0f;
                    reflectionProbeAvailable = reflectionProbe.customBakedTexture != null;
                    reflectionProbeConfigured = reflectionProbeAvailable;
                }

                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = false;
                camera.fieldOfView = visualCase.fieldOfView;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 20.0f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = backgroundEnabled
                    ? new Color(1.0f, 1.0f, 1.0f, 1.0f)
                    : new Color(0.0f, 0.0f, 0.0f, 0.0f);
                camera.transform.position = visualCase.cameraPosition;
                camera.transform.LookAt(visualCase.cameraTarget, Vector3.up);
                bool actualPostProcessingEnabled = ConfigurePostProcessing(cameraObject, postProcessingEnabled);

                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = directionalLightColorOverride ?? GeneratedPmxDirectionalLightColor;
                light.intensity = directionalLightIntensityOverride ?? GeneratedPmxDirectionalLightIntensity;
                light.shadows = realtimeShadowOccluderEnabled ? LightShadows.Hard : LightShadows.None;
                // Orient the directional light to the MMD reference direction so the toon
                // shading matches the GoldenOracle render's lit side.
                Vector3 unityLightTravel = GeneratedPmxMmdLightTravelDirection.normalized;
                lightObject.transform.position = GeneratedPmxDirectionalLightPosition;
                lightObject.transform.rotation = Quaternion.LookRotation(unityLightTravel, Vector3.up);
                // An off-screen StandardRequest does not reliably select a directional light as
                // URP's main light unless the scene declares its sun explicitly. Preserve the
                // user's scene setting and restore it in finally.
                RenderSettings.sun = light;
                // Keep the MMD direction override so Legacy remains byte-stable while this harness
                // changes only Unity's main-light color/intensity for the S1a delta assertion.
                Vector3 generatedPmxDirectionToLight = -lightObject.transform.forward;

                if (realtimeShadowOccluderEnabled)
                {
                    shadowOccluder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    shadowOccluder.name = "phase17-generated-pmx-shadow-occluder";
                    shadowOccluder.transform.position = visualCase.cameraTarget + generatedPmxDirectionToLight * 0.55f;
                    shadowOccluder.transform.rotation = Quaternion.LookRotation(lightObject.transform.forward, Vector3.up);
                    shadowOccluder.transform.localScale = new Vector3(0.75f, 0.75f, 0.04f);
                    Renderer occluderRenderer = shadowOccluder.GetComponent<Renderer>();
                    occluderRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    occluderRenderer.receiveShadows = false;
                }

                if (ssaoEnabledOverride.HasValue)
                {
                    Shader? ssaoShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (ssaoShader != null)
                    {
                        ssaoOccluder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        ssaoOccluder.name = "phase17-generated-pmx-ssao-occluder";
                        ssaoOccluder.transform.position = visualCase.cameraTarget + new Vector3(0.32f, 0.0f, 0.02f);
                        ssaoOccluder.transform.localScale = new Vector3(0.28f, 0.42f, 0.28f);
                        ssaoOccluderMaterial = new Material(ssaoShader)
                        {
                            name = "phase17-generated-pmx-ssao-occluder-material",
                            color = new Color(0.18f, 0.18f, 0.18f, 1.0f)
                        };
                        ssaoOccluder.GetComponent<Renderer>().sharedMaterial = ssaoOccluderMaterial;
                    }
                }

                var parser = new NativeMmdParser();
                MmdModelDefinition model = parser.LoadModel(File.ReadAllBytes(pmxPath));
                instance = MmdUnityModelFactory.CreateSkinnedModel(
                    model,
                    pmxPath,
                    importScale: 1.0f,
                    preset: materialPreset);
                instance.Root.transform.position = Vector3.zero;
                instance.Root.transform.rotation = Quaternion.identity;
                instance.Root.transform.localScale = Vector3.one;
                proxyStats = EnableGeneratedPmxMaterialOrderProxies(instance, generatedPmxDirectionToLight);
                if (instance.Materials.Length > 0 && instance.Materials[0] != null)
                {
                    Material captureMaterial = instance.Materials[0];
                    selectedMaterialPassIndex = captureMaterial.FindPass("ForwardLit");
                    if (selectedMaterialPassIndex >= 0)
                    {
                        selectedMaterialPassName = captureMaterial.GetPassName(selectedMaterialPassIndex);
                        selectedMaterialLightMode = captureMaterial.shader.FindPassTagValue(
                            selectedMaterialPassIndex,
                            new ShaderTagId("LightMode")).name;
                        selectedMaterialPassEnabled = !string.IsNullOrEmpty(selectedMaterialLightMode) &&
                            captureMaterial.GetShaderPassEnabled(selectedMaterialLightMode);
                        selectedMaterialPassValid = string.Equals(
                                selectedMaterialPassName,
                                "ForwardLit",
                                StringComparison.Ordinal) &&
                            (string.Equals(selectedMaterialLightMode, "UniversalForward", StringComparison.Ordinal) ||
                                string.Equals(selectedMaterialLightMode, "UniversalForwardOnly", StringComparison.Ordinal)) &&
                            selectedMaterialPassEnabled;
                    }
                }
                if (reflectionProbeEnabledOverride.HasValue)
                {
                    foreach (Renderer renderer in instance.Root.GetComponentsInChildren<Renderer>(includeInactive: true))
                    {
                        renderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
                        renderer.probeAnchor = reflectionProbe != null ? reflectionProbe.transform : null;
                    }

                    bool anyReflectionMaterial = false;
                    foreach (Material material in instance.Materials)
                    {
                        if (material == null || !material.HasProperty("_ReflectionProbeWeight"))
                        {
                            continue;
                        }

                        // Keep material response fixed so the visual delta proves that enabling the
                        // local custom probe changes URP's bound environment, not merely that this
                        // harness changed a shader property between captures.
                        material.SetFloat("_ReflectionProbeWeight", 1.0f);
                        anyReflectionMaterial = true;
                    }

                    reflectionProbeConfigured &= materialPreset != MmdMaterialPreset.MmdToonLit || anyReflectionMaterial;
                }
                if (toonBoundaryOverride.HasValue || toonFeatherOverride.HasValue)
                {
                    bool anyToonBoundaryMaterial = false;
                    bool allToonBoundaryMaterialsConfigured = true;
                    foreach (Material material in instance.Materials)
                    {
                        if (material == null)
                        {
                            continue;
                        }

                        bool hasBoundary = material.HasProperty(MmdMaterialPropertyNames.ToonBoundary);
                        bool hasFeather = material.HasProperty(MmdMaterialPropertyNames.ToonFeather);
                        if (hasBoundary && toonBoundaryOverride.HasValue)
                        {
                            material.SetFloat(MmdMaterialPropertyNames.ToonBoundary, toonBoundary);
                        }
                        if (hasFeather && toonFeatherOverride.HasValue)
                        {
                            material.SetFloat(MmdMaterialPropertyNames.ToonFeather, toonFeather);
                        }

                        anyToonBoundaryMaterial |= hasBoundary || hasFeather;
                        if (materialPreset == MmdMaterialPreset.MmdToonLit && (!hasBoundary || !hasFeather))
                        {
                            allToonBoundaryMaterialsConfigured = false;
                        }
                    }

                    toonBoundaryConfigured = materialPreset != MmdMaterialPreset.MmdToonLit ||
                        (anyToonBoundaryMaterial && allToonBoundaryMaterialsConfigured);
                }
                if (perturbShaderOutput)
                {
                    foreach (Material material in instance.Materials)
                    {
                        if (material == null)
                            continue;
                        if (material.HasProperty("_BaseColor"))
                            material.SetColor("_BaseColor", Color.magenta);
                        if (material.HasProperty("_Color"))
                            material.SetColor("_Color", Color.magenta);
                    }
                }

                // Real MMD composites transparency in gamma (sRGB) space. A default sRGB target
                // makes the GPU decode the destination to linear before blending, so alpha-over
                // happens in linear space and semi-transparent overlays read too light/desaturated
                // versus the golden. Render into a non-sRGB (linear-stored) target instead: paired
                // with the shader's _GammaTarget=1 path (which emits sRGB-encoded values verbatim),
                // the fixed-function blend then operates on sRGB values == gamma-space alpha-over.
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                camera.targetTexture = renderTexture;
                // The project renders with URP, so camera.Render() (a built-in-pipeline
                // API) would skip URP lighting/shading and produce a non-GameView result.
                // Submit a render request so the active SRP runs the full pipeline.
                var renderRequest = new UnityEngine.Rendering.RenderPipeline.StandardRequest
                {
                    destination = renderTexture
                };
                bool standardRequestSupported = UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, renderRequest);
                if (standardRequestSupported)
                {
                    usedStandardRequest = true;
                    if (reflectionProbeEnabledOverride.HasValue)
                    {
                        // Give the culling/probe system one complete SRP submission after the
                        // temporary probe and explicit anchors are registered. The second submit
                        // is the measured frame.
                        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, renderRequest);
                    }
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, renderRequest);
                }
                else
                {
                    camera.Render();
                }
                Vector4 renderedMainLightColor = Shader.GetGlobalVector(MainLightColorId);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                pixels = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                RenderTexture.active = previous;
                camera.targetTexture = null;

                CountPixels(pixels, camera.backgroundColor, out int nonBlank, out int alpha, out int outline, out int transparent);
                if (backgroundEnabled)
                {
                    FlattenToOpaqueBackground(pixels, camera.backgroundColor);
                }

                File.WriteAllBytes(Path.GetFullPath(capturePath), pixels.EncodeToPNG());

                bool passed = nonBlank > 0 && alpha > 0 &&
                    selectedMaterialPassValid &&
                    usedStandardRequest &&
                    (!ssaoEnabledOverride.HasValue || (ssaoAvailable && ssaoConfigured)) &&
                    (!reflectionProbeEnabledOverride.HasValue ||
                        (reflectionProbeAvailable && reflectionProbeConfigured)) &&
                    ((!toonBoundaryOverride.HasValue && !toonFeatherOverride.HasValue) || toonBoundaryConfigured);
                return new MmdGeneratedPmxVisualCaseReport
                {
                    caseName = "phase17-generated-pmx-" + visualCase.name,
                    sourceCaseId = visualCase.name,
                    status = passed ? "passed" : "failed",
                    modelFileName = visualCase.modelFileName,
                    modelPath = Path.GetFullPath(pmxPath),
                    actualImage = Path.GetFullPath(capturePath),
                    width = width,
                    height = height,
                    shaderName = instance.Materials.Length > 0 && instance.Materials[0] != null
                        ? instance.Materials[0].shader.name
                        : string.Empty,
                    renderTextureFormat = renderTexture.format.ToString(),
                    renderTextureGraphicsFormat = renderTexture.graphicsFormat.ToString(),
                    renderTextureSrgb = renderTexture.sRGB,
                    readbackTextureFormat = pixels.format.ToString(),
                    pngEncodingSource = "Texture2D.EncodeToPNG-after-ReadPixels",
                    materialCount = instance.Materials.Length,
                    materialProxyMode = proxyStats.mode,
                    materialProxyPolicy = proxyStats.policy,
                    materialProxyRendererCount = proxyStats.rendererCount,
                    materialProxyMeshVertexCount = proxyStats.meshVertexCount,
                    materialProxyBindings = proxyStats.materials,
                    materialBindings = instance.MaterialBindingDiagnostics,
                    vertexCount = instance.VertexCount,
                    indexCount = instance.IndexCount,
                    submeshCount = instance.SubmeshCount,
                    loadedDiffuseTextures = instance.LoadedDiffuseTextureCount,
                    loadedSphereTextures = instance.LoadedSphereTextureCount,
                    loadedToonTextures = instance.LoadedToonTextureCount,
                    missingTextures = instance.MissingTextureReferenceCount,
                    unsupportedTextures = instance.UnsupportedTextureReferenceCount,
                    skippedSphereTextures = instance.SkippedSphereTextureReferenceCount,
                    skippedToonTextures = instance.SkippedToonTextureReferenceCount,
                    thresholdMean = visualCase.thresholdMean,
                    cameraPosition = ToVector3Array(camera.transform.position),
                    cameraTarget = ToVector3Array(visualCase.cameraTarget),
                    cameraFieldOfView = camera.fieldOfView,
                    cameraForward = ToVector3Array(camera.transform.forward),
                    cameraUp = ToVector3Array(camera.transform.up),
                    cameraCoordinatePolicy = GeneratedPmxCameraCoordinatePolicy,
                    ambientLightColor = ToColorArray(captureAmbientLightColor),
                    ambientLightIntensity = captureAmbientLightIntensity,
                    directionalLightColor = ToColorArray(light.color),
                    directionalLightIntensity = light.intensity,
                    directionalLightPosition = ToVector3Array(GeneratedPmxDirectionalLightPosition),
                    directionalLightTarget = ToVector3Array(Vector3.zero),
                    directionalLightMode = "position-to-origin-urp-main-light",
                    mainLightColor = ToVector4Array(renderedMainLightColor),
                    ambientShEnabled = ambientShEnabled,
                    ambientShMode = ambientShConfigured ? AmbientMode.Custom.ToString() : RenderSettings.ambientMode.ToString(),
                    ambientShColor = ToColorArray(ambientShEnabled ? GeneratedPmxAmbientShColor : Color.black),
                    ambientShIntensity = ambientShEnabled ? GeneratedPmxAmbientShIntensity : 0.0f,
                    fogEnabled = fogEnabled,
                    fogMode = fogConfigured ? RenderSettings.fogMode.ToString() : string.Empty,
                    fogColor = ToColorArray(fogConfigured ? RenderSettings.fogColor : Color.clear),
                    fogDensity = fogConfigured ? RenderSettings.fogDensity : 0.0f,
                    fogStartDistance = fogConfigured ? RenderSettings.fogStartDistance : 0.0f,
                    fogEndDistance = fogConfigured ? RenderSettings.fogEndDistance : 0.0f,
                    ssaoEnabled = ssaoEnabled,
                    ssaoAvailable = ssaoAvailable,
                    ssaoConfigured = ssaoConfigured,
                    ssaoMode = ssaoEnabledOverride.HasValue
                        ? (ssaoAvailable ? "renderer-feature" : "unavailable")
                        : "unchanged",
                    reflectionProbeEnabled = reflectionProbeEnabled,
                    reflectionProbeAvailable = reflectionProbeAvailable,
                    reflectionProbeConfigured = reflectionProbeConfigured,
                    reflectionProbeMode = reflectionProbeEnabledOverride.HasValue
                        ? (reflectionProbeAvailable ? "custom-cubemap-probe-and-environment" : "unavailable")
                        : "unchanged",
                    toonBoundary = toonBoundary,
                    toonFeather = toonFeather,
                    toonBoundaryConfigured = toonBoundaryConfigured,
                    toonBoundaryMode = toonBoundaryOverride.HasValue || toonFeatherOverride.HasValue
                        ? (toonBoundaryConfigured ? "material-properties" : "unavailable")
                        : "unchanged",
                    selectedMaterialPassName = selectedMaterialPassName,
                    selectedMaterialLightMode = selectedMaterialLightMode,
                    selectedMaterialPassIndex = selectedMaterialPassIndex,
                    selectedMaterialPassEnabled = selectedMaterialPassEnabled,
                    selectedMaterialPassValid = selectedMaterialPassValid,
                    captureRequestType = usedStandardRequest
                        ? "RenderPipeline.StandardRequest"
                        : "Camera.Render",
                    captureRenderPath = usedStandardRequest
                        ? "RenderPipeline.StandardRequest"
                        : "Camera.Render-fallback",
                    captureUsedStandardRequest = usedStandardRequest,
                    renderPipelineName = GraphicsSettings.currentRenderPipeline != null
                        ? GraphicsSettings.currentRenderPipeline.name
                        : "BuiltInRenderPipeline",
                    nonBlankPixelCount = nonBlank,
                    alphaPixelCount = alpha,
                    transparentPixelCount = transparent,
                    outlinePixelCount = outline,
                    backgroundEnabled = backgroundEnabled,
                    backgroundMode = backgroundEnabled ? "solid-color" : "transparent",
                    postProcessingEnabled = actualPostProcessingEnabled,
                    postProcessingMode = actualPostProcessingEnabled ? "camera-enabled" : "disabled"
                };
            }
            finally
            {
                RenderSettings.ambientLight = previousAmbientLight;
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientProbe = previousAmbientProbe;
                RenderSettings.fog = previousFog;
                RenderSettings.fogMode = previousFogMode;
                RenderSettings.fogColor = previousFogColor;
                RenderSettings.fogDensity = previousFogDensity;
                RenderSettings.fogStartDistance = previousFogStartDistance;
                RenderSettings.fogEndDistance = previousFogEndDistance;
                RenderSettings.defaultReflectionMode = previousDefaultReflectionMode;
                RenderSettings.customReflectionTexture = previousCustomReflectionTexture;
                RenderSettings.reflectionIntensity = previousReflectionIntensity;
                if (configuredSsaoFeatures != null && previousSsaoFeatureStates != null)
                {
                    for (int i = 0; i < configuredSsaoFeatures.Count && i < previousSsaoFeatureStates.Count; i++)
                    {
                        if (configuredSsaoFeatures[i] != null)
                        {
                            SetSsaoFeatureActive(configuredSsaoFeatures[i], previousSsaoFeatureStates[i]);
                        }
                    }
                }
                RenderSettings.sun = previousSun;
                if (proxyStats != null)
                {
                    foreach (Material clonedEdgeMaterial in proxyStats.clonedEdgeMaterials)
                    {
                        if (clonedEdgeMaterial != null)
                        {
                            UnityEngine.Object.DestroyImmediate(clonedEdgeMaterial);
                        }
                    }
                }
                DestroyInstance(instance);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(lightObject);
                if (shadowOccluder != null)
                {
                    UnityEngine.Object.DestroyImmediate(shadowOccluder);
                }
                if (ssaoOccluder != null)
                {
                    UnityEngine.Object.DestroyImmediate(ssaoOccluder);
                }
                if (ssaoOccluderMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(ssaoOccluderMaterial);
                }
                if (reflectionProbeObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(reflectionProbeObject);
                }
                if (reflectionProbeCubemap != null)
                {
                    UnityEngine.Object.DestroyImmediate(reflectionProbeCubemap);
                }
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }

                if (pixels != null)
                {
                    UnityEngine.Object.DestroyImmediate(pixels);
                }
            }
        }

        private static List<UnityEngine.Object> FindSsaoFeatures()
        {
            var features = new List<UnityEngine.Object>();
            RenderPipelineAsset? pipelineAsset = GraphicsSettings.currentRenderPipeline;
            if (pipelineAsset == null)
            {
                return features;
            }

            PropertyInfo? rendererDataProperty = pipelineAsset.GetType().GetProperty(
                "scriptableRendererData",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object? rendererData = rendererDataProperty?.GetValue(pipelineAsset);
            if (rendererData == null)
            {
                return features;
            }

            PropertyInfo? featuresProperty = rendererData.GetType().GetProperty(
                "rendererFeatures",
                BindingFlags.Instance | BindingFlags.Public);
            if (!(featuresProperty?.GetValue(rendererData) is IEnumerable rendererFeatures))
            {
                return features;
            }

            foreach (object? feature in rendererFeatures)
            {
                if (feature is UnityEngine.Object featureObject &&
                    string.Equals(
                        featureObject.GetType().FullName,
                        "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion",
                        StringComparison.Ordinal))
                {
                    features.Add(featureObject);
                }
            }

            return features;
        }

        private static bool TryGetSsaoFeatureActive(UnityEngine.Object feature, out bool active)
        {
            PropertyInfo? property = feature.GetType().GetProperty(
                "isActive",
                BindingFlags.Instance | BindingFlags.Public);
            if (property?.PropertyType == typeof(bool) && property.CanRead)
            {
                active = (bool)property.GetValue(feature);
                return true;
            }

            active = false;
            return false;
        }

        private static bool SetSsaoFeatureActive(UnityEngine.Object feature, bool active)
        {
            MethodInfo? method = feature.GetType().GetMethod(
                "SetActive",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(bool) },
                modifiers: null);
            if (method == null)
            {
                return false;
            }

            method.Invoke(feature, new object[] { active });
            return true;
        }

        private static SphericalHarmonicsL2 BuildAmbientProbe(Color color, float intensity)
        {
            var probe = new SphericalHarmonicsL2();
            probe.Clear();
            const float sphericalHarmonicsL0 = 0.2820947918f;
            probe[0, 0] = color.r * intensity / sphericalHarmonicsL0;
            probe[1, 0] = color.g * intensity / sphericalHarmonicsL0;
            probe[2, 0] = color.b * intensity / sphericalHarmonicsL0;
            return probe;
        }

        private static Cubemap BuildReflectionProbeCubemap(bool colored)
        {
            const int size = 16;
            var cubemap = new Cubemap(size, TextureFormat.RGBA32, mipChain: true);
            var faceColors = colored ? new[]
            {
                new Color(0.95f, 0.12f, 0.08f, 1.0f),
                new Color(0.08f, 0.22f, 0.95f, 1.0f),
                new Color(0.10f, 0.85f, 0.25f, 1.0f),
                new Color(0.90f, 0.75f, 0.08f, 1.0f),
                new Color(0.75f, 0.12f, 0.90f, 1.0f),
                new Color(0.08f, 0.85f, 0.85f, 1.0f)
            } : new[]
            {
                Color.black, Color.black, Color.black,
                Color.black, Color.black, Color.black
            };
            var pixels = new Color[size * size];
            for (int faceIndex = 0; faceIndex < faceColors.Length; faceIndex++)
            {
                for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
                {
                    pixels[pixelIndex] = faceColors[faceIndex];
                }

                cubemap.SetPixels(pixels, (CubemapFace)faceIndex);
            }

            cubemap.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return cubemap;
        }

        private static void CountPixels(Texture2D pixels, Color backgroundColor, out int nonBlank, out int alpha, out int outline, out int transparent)
        {
            Color32 background = Quantize(backgroundColor);
            Color32[] colors = pixels.GetPixels32();
            nonBlank = 0;
            alpha = 0;
            outline = 0;
            transparent = 0;
            for (int i = 0; i < colors.Length; i++)
            {
                Color32 color = colors[i];
                if (ColorDistance(color, background) > 8)
                {
                    nonBlank++;
                }

                if (color.a > 0)
                {
                    alpha++;
                }

                if (color.a > 0 && color.a < 255)
                {
                    transparent++;
                }

                if (color.r < 28 && color.g < 32 && color.b < 48 && ColorDistance(color, background) > 16)
                {
                    outline++;
                }
            }
        }

        private static void FlattenToOpaqueBackground(Texture2D pixels, Color backgroundColor)
        {
            // The render target is cleared to an OPAQUE background and every fragment composites
            // over it (opaque body replaces it; transparent overlays alpha-blend over the body).
            // So the readback RGB is already the final color over the background — only the alpha
            // channel still carries the last transparent layer's coverage (e.g. 0.84 for an overlay
            // at texture-alpha 0.8 over the opaque body). Re-compositing that RGB against the
            // background again (the previous premultiplied add) double-counted the background,
            // adding ~background*(1-alpha) and washing transparent overlays out toward the bg color.
            // The correct flatten here is simply to force the image opaque, leaving RGB untouched.
            Color32[] colors = pixels.GetPixels32();
            bool changed = false;
            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i].a != byte.MaxValue)
                {
                    colors[i].a = byte.MaxValue;
                    changed = true;
                }
            }

            if (changed)
            {
                pixels.SetPixels32(colors);
                pixels.Apply();
            }
        }


        private static float[] ToColorArray(Color color)
        {
            return new[] { color.r, color.g, color.b, color.a };
        }

        private static float[] ToVector3Array(Vector3 vector)
        {
            return new[] { vector.x, vector.y, vector.z };
        }

        private static float[] ToVector4Array(Vector4 vector)
        {
            return new[] { vector.x, vector.y, vector.z, vector.w };
        }

        private static MmdGeneratedPmxMaterialProxyStats EnableGeneratedPmxMaterialOrderProxies(MmdUnityModelInstance instance, Vector3 directionToLight)
        {
            Renderer? sourceRenderer = instance.MeshRenderer != null
                ? instance.MeshRenderer
                : instance.SkinnedMeshRenderer;
            if (sourceRenderer == null)
            {
                return new MmdGeneratedPmxMaterialProxyStats
                {
                    mode = "disabled",
                    policy = "renderer-required"
                };
            }

            Transform parent = sourceRenderer.transform;
            sourceRenderer.enabled = false;
            int rendererCount = 0;
            int meshVertexCount = 0;
            var proxyMaterials = new List<MmdGeneratedPmxProxyMaterialDiagnostic>();
            var stats = new MmdGeneratedPmxMaterialProxyStats();
            var edgeProxyQueue = new List<(Mesh mesh, Material bodyMaterial, int slot, MmdMaterialDescriptor descriptor)>();
            for (int materialSlot = 0; materialSlot < instance.RenderingDescriptor.materials.Count; materialSlot++)
            {
                MmdMaterialDescriptor material = instance.RenderingDescriptor.materials[materialSlot];
                MmdSubmeshDescriptor? submesh = FindGeneratedPmxSubmesh(instance.RenderingDescriptor, material.materialIndex);
                if (submesh == null || materialSlot >= instance.Materials.Length)
                {
                    continue;
                }

                Material proxyMaterial = instance.Materials[materialSlot];
                // The proxy takes every material parameter from the PMX descriptor via the loader
                // (MmdUnityMaterialBuilder): diffuse color, alpha, ambient, outline color/width,
                // transparency mode, alpha clip threshold, texture-alpha output weight, blend, and
                // culling. There are no per-material-name golden-fitting overrides and no harness-side
                // texture routing. (Removed 2026-06-15: _order/silhouette color+outline, alpha_morph/
                // depth_back color+outline, tga 2.12 / cutout 2.2 flat-lighting brightening, and the
                // _AlphaClipThreshold=0 / _TextureAlphaOutputWeight PNG overrides — the loader's
                // descriptor-derived values are correct. Morphs 'foreground_hide'/'png_hairshadow_hide'
                // render at weight 0 in their goldens, so morph evaluation needs a golden re-rendered
                // at a non-zero weight to validate.)
                //
                // The loader is the single source of truth for transparency: read back its resolved
                // alpha-blend decision instead of re-deriving a parallel (and divergent) classifier.
                bool loaderAlphaBlend = IsLoaderResolvedAlphaBlendProxy(proxyMaterial);
                if (proxyMaterial.HasProperty("_MmdLightDirection"))
                {
                    proxyMaterial.SetVector("_MmdLightDirection",
                        new Vector4(directionToLight.x, directionToLight.y, directionToLight.z, 0.0f));
                }
                if (proxyMaterial.HasProperty("_MmdLightColor"))
                {
                    proxyMaterial.SetColor("_MmdLightColor", GeneratedPmxDirectionalLightColor);
                }
                // The parity target is non-sRGB so transparency composites in gamma space; tell
                // the shader to emit sRGB-encoded values verbatim instead of converting to linear.
                if (proxyMaterial.HasProperty("_GammaTarget"))
                {
                    proxyMaterial.SetFloat("_GammaTarget", 1.0f);
                }
                if (loaderAlphaBlend)
                {
                    // saba draws transparent geometry under GL_LESS depth (the GL default), so a
                    // double-sided sheet's coincident back face is depth-rejected rather than blended
                    // twice. Unity's transparent default is LEqual, which double-draws coplanar faces;
                    // match saba so a flat both-face overlay stays single-layer while genuinely
                    // separated layers (the alpha-blend-overlap stack) still blend.
                    if (proxyMaterial.HasProperty("_ZTest"))
                    {
                        proxyMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Less);
                    }
                }
                // The body proxy draws the body only. The silhouette edge is rendered by a separate
                // edge proxy queued after every body (below): URP sorts a single material's
                // SRPDefaultUnlit outline pass BEFORE its UniversalForward body pass, so an inline
                // outline would fill the whole silhouette before the body writes depth and bleed black
                // through every translucent body. Drawing the edge after all bodies lets it depth-test
                // against the completed body depth, matching MMD/saba (body depth-write, edge GL_LESS).
                if (proxyMaterial.HasProperty("_OutlineVisible"))
                {
                    proxyMaterial.SetFloat("_OutlineVisible", 0.0f);
                }
                proxyMaterial.renderQueue = loaderAlphaBlend
                    ? (int)RenderQueue.Transparent + materialSlot
                    : (int)RenderQueue.Geometry + materialSlot;
                // saba renders every submesh as the FULL mesh under depth-tested alpha blend (Cull Off
                // for both-face, depth-write ON, drawn in mesh order); it never builds a camera-facing
                // shell. The depth test reproduces saba's partial layering: the first face drawn per
                // pixel wins the depth, so a nearer face drawn afterwards blends over it while a farther
                // one is depth-rejected. (saba's diffuse is flat — N.L only routes through toon/shadow —
                // so a both-face cube's far faces shade the same color as its near faces, no normal flip
                // needed.)
                Mesh proxyMesh = BuildGeneratedPmxSubmeshProxyMesh(instance.Mesh, submesh);
                meshVertexCount += proxyMesh.vertexCount;
                CreateGeneratedPmxProxyRenderer(instance, parent, proxyMesh, proxyMaterial, materialSlot, material.name + " material proxy");
                proxyMaterials.Add(BuildGeneratedPmxProxyMaterialDiagnostic("body", materialSlot, material, proxyMaterial, proxyMesh.vertexCount));
                rendererCount++;

                // Queue the MMD silhouette edge when the material draws one (PMX draw-edge flag).
                if (material.drawEdgeFlag && material.edgeSize > 0.0f && proxyMaterial.HasProperty("_OutlineWidth"))
                {
                    edgeProxyQueue.Add((proxyMesh, proxyMaterial, materialSlot, material));
                }
            }

            // Edge proxies: clone each body material, draw only the outline pass, and queue them after
            // every body so the screen-space inverted hull depth-tests against the fully written body
            // depth. The interior hull is rejected (it sits behind the body front), leaving only the
            // edgeSize-pixel silhouette ring — the faithful MMD edge that does not bleed through the
            // translucent bodies.
            foreach ((Mesh mesh, Material bodyMaterial, int slot, MmdMaterialDescriptor descriptor) in edgeProxyQueue)
            {
                var edgeMaterial = new Material(bodyMaterial)
                {
                    name = bodyMaterial.name + " edge",
                    hideFlags = HideFlags.HideAndDontSave
                };
                edgeMaterial.SetFloat("_BodyVisible", 0.0f);
                edgeMaterial.SetFloat("_OutlineVisible", 1.0f);
                // GL_LESS (saba's edge depth func): the inverted hull only survives where it expands
                // beyond the body silhouette (over the cleared background), giving the edgeSize-pixel
                // ring. Coplanar hull fragments — the interior, and a camera-facing flat face whose
                // screen-space normal is ~0 so the hull collapses onto the body — are rejected, so a
                // flat quad keeps its fill instead of being painted black by its own edge.
                if (edgeMaterial.HasProperty("_OutlineZTest"))
                {
                    edgeMaterial.SetFloat("_OutlineZTest", (float)UnityEngine.Rendering.CompareFunction.Less);
                }
                edgeMaterial.renderQueue = (int)RenderQueue.Overlay + slot;
                stats.clonedEdgeMaterials.Add(edgeMaterial);
                CreateGeneratedPmxProxyRenderer(instance, parent, mesh, edgeMaterial, 1000 + slot, descriptor.name + " edge proxy");
                proxyMaterials.Add(BuildGeneratedPmxProxyMaterialDiagnostic("edge", slot, descriptor, edgeMaterial, mesh.vertexCount));
                rendererCount++;
            }

            stats.mode = "enabled";
            stats.policy = "phase17-debug-material-proxies-tga-front-facing";
            stats.rendererCount = rendererCount;
            stats.meshVertexCount = meshVertexCount;
            stats.materials = proxyMaterials.ToArray();
            return stats;
        }

        private static MmdGeneratedPmxProxyMaterialDiagnostic BuildGeneratedPmxProxyMaterialDiagnostic(
            string proxyKind,
            int materialSlot,
            MmdMaterialDescriptor material,
            Material proxyMaterial,
            int proxyMeshVertexCount)
        {
            return new MmdGeneratedPmxProxyMaterialDiagnostic
            {
                proxyKind = proxyKind,
                materialIndex = material.materialIndex,
                materialSlot = materialSlot,
                name = material.name,
                texture = material.texture,
                sphereTexture = material.sphereTexture,
                sphereTextureModeHint = material.sphereTextureMode,
                renderQueue = proxyMaterial.renderQueue,
                proxyMeshVertexCount = proxyMeshVertexCount,
                baseColorProperty = GetMaterialColorArray(proxyMaterial, "_BaseColor"),
                colorProperty = GetMaterialColorArray(proxyMaterial, "_Color"),
                outlineColorProperty = GetMaterialColorArray(proxyMaterial, "_OutlineColor"),
                sphereMapBound = GetMaterialTextureBound(proxyMaterial, "_SphereMap"),
                sphereMode = GetMaterialFloat(proxyMaterial, "_SphereMode"),
                alpha = GetMaterialFloat(proxyMaterial, "_Alpha"),
                outlineWidth = GetMaterialFloat(proxyMaterial, "_OutlineWidth"),
                outlineScreenSpaceWeight = GetMaterialFloat(proxyMaterial, "_OutlineScreenSpaceWeight"),
                textureAlphaOutputWeight = GetMaterialFloat(proxyMaterial, "_TextureAlphaOutputWeight"),
                textureFlatLightingWeight = GetMaterialFloat(proxyMaterial, "_TextureFlatLightingWeight"),
                textureFlatLightingValue = GetMaterialFloat(proxyMaterial, "_TextureFlatLightingValue"),
                alphaClipThreshold = GetMaterialFloat(proxyMaterial, "_AlphaClipThreshold"),
                zWrite = GetMaterialFloat(proxyMaterial, "_ZWrite"),
                outlineZWrite = GetMaterialFloat(proxyMaterial, "_OutlineZWrite"),
                srcBlend = GetMaterialFloat(proxyMaterial, "_SrcBlend"),
                dstBlend = GetMaterialFloat(proxyMaterial, "_DstBlend")
            };
        }

        private static float[] GetMaterialColorArray(Material material, string propertyName)
        {
            if (!material.HasProperty(propertyName))
            {
                return Array.Empty<float>();
            }

            return ToColorArray(material.GetColor(propertyName));
        }

        private static float GetMaterialFloat(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) ? material.GetFloat(propertyName) : -1.0f;
        }

        private static bool GetMaterialTextureBound(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) && material.GetTexture(propertyName) != null;
        }

        private static bool IsLoaderResolvedAlphaBlendProxy(Material proxyMaterial)
        {
            // The loader (MmdUnityMaterialBuilder) is the single source of truth for transparency:
            // it resolves opaque / alphaTest / alphaBlend from the PMX descriptor + UV-covered
            // texture-alpha scan (MmdMaterialTransparencyPolicy) and configures the blend. alphaBlend
            // is the only mode that sets a OneMinusSrcAlpha destination blend (opaque/alphaTest stay
            // SrcBlend=One, DstBlend=Zero), so read that back instead of re-deriving a parallel — and
            // divergent — classifier in the harness. This is what lets a regular opaque-PMX TGA whose
            // texture alpha is a meaningful mask (mmd-tga-regular-hair-alpha-opaque) flow through the
            // transparent path the loader chose, matching real MMD's always-alpha-blend behavior.
            return proxyMaterial.HasProperty("_DstBlend")
                && Mathf.Approximately(proxyMaterial.GetFloat("_DstBlend"), (float)BlendMode.OneMinusSrcAlpha);
        }

        private static MmdSubmeshDescriptor? FindGeneratedPmxSubmesh(MmdRenderingDescriptor descriptor, int materialIndex)
        {
            foreach (MmdSubmeshDescriptor submesh in descriptor.submeshes)
            {
                if (submesh.materialIndex == materialIndex)
                {
                    return submesh;
                }
            }

            return null;
        }

        private static Mesh BuildGeneratedPmxSubmeshProxyMesh(
            Mesh sourceMesh,
            MmdSubmeshDescriptor submesh)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            sourceMesh.GetVertices(vertices);
            sourceMesh.GetNormals(normals);
            sourceMesh.GetUVs(0, uvs);

            var mesh = new Mesh
            {
                name = sourceMesh.name + " material proxy " + submesh.materialIndex,
                indexFormat = sourceMesh.indexFormat
            };
            mesh.SetVertices(vertices);
            if (normals.Count == vertices.Count)
            {
                mesh.SetNormals(normals);
            }

            if (uvs.Count == vertices.Count)
            {
                mesh.SetUVs(0, uvs);
            }

            mesh.boneWeights = sourceMesh.boneWeights;
            mesh.bindposes = sourceMesh.bindposes;
            mesh.subMeshCount = 1;
            // Full submesh, every triangle — saba never builds a camera-facing shell; the depth test
            // (depth-write ON, GL_LESS) handles partial layering for the transparent both-face cubes.
            int[] triangles = sourceMesh.GetTriangles(submesh.submeshIndex);
            mesh.SetTriangles(triangles, 0, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateGeneratedPmxProxyRenderer(
            MmdUnityModelInstance instance,
            Transform parent,
            Mesh mesh,
            Material material,
            int renderOrder,
            string name)
        {
            var proxyObject = new GameObject(name);
            proxyObject.transform.SetParent(parent, worldPositionStays: false);
            proxyObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = proxyObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = renderOrder;
        }

        private static void DestroyInstance(MmdUnityModelInstance? instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.Root != null)
            {
                UnityEngine.Object.DestroyImmediate(instance.Root);
            }

            if (instance.Mesh != null)
            {
                UnityEngine.Object.DestroyImmediate(instance.Mesh);
            }

            for (int i = 0; i < instance.Materials.Length; i++)
            {
                if (instance.Materials[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance.Materials[i]);
                }
            }

            for (int i = 0; i < instance.OwnedTextures.Length; i++)
            {
                if (instance.OwnedTextures[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance.OwnedTextures[i]);
                }
            }
        }

        private static Color32 Quantize(Color color)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255.0f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255.0f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255.0f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.a) * 255.0f));
        }

        private static int ColorDistance(Color32 left, Color32 right)
        {
            return Math.Abs(left.r - right.r)
                + Math.Abs(left.g - right.g)
                + Math.Abs(left.b - right.b)
                + Math.Abs(left.a - right.a);
        }

        private static bool ConfigurePostProcessing(GameObject cameraObject, bool enabled)
        {
            Type? cameraDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (cameraDataType == null)
            {
                return false;
            }

            Component cameraData = cameraObject.GetComponent(cameraDataType) ?? cameraObject.AddComponent(cameraDataType);
            System.Reflection.PropertyInfo? property = cameraDataType.GetProperty("renderPostProcessing");
            if (property != null && property.CanWrite)
            {
                property.SetValue(cameraData, enabled);
                return enabled;
            }

            return false;
        }
    }

    public sealed class MmdGeneratedPmxVisualCase
    {
        public MmdGeneratedPmxVisualCase(
            string name,
            string modelFileName,
            Vector3 cameraPosition,
            Vector3 cameraTarget,
            float fieldOfView,
            float thresholdMean)
        {
            this.name = name;
            this.modelFileName = modelFileName;
            this.cameraPosition = cameraPosition;
            this.cameraTarget = cameraTarget;
            this.fieldOfView = fieldOfView;
            this.thresholdMean = thresholdMean;
        }

        public readonly string name;
        public readonly string modelFileName;
        public readonly Vector3 cameraPosition;
        public readonly Vector3 cameraTarget;
        public readonly float fieldOfView;
        public readonly float thresholdMean;
    }

    [Serializable]
    public sealed class MmdGeneratedPmxMaterialProxyStats
    {
        public string mode = string.Empty;
        public string policy = string.Empty;
        public int rendererCount;
        public int meshVertexCount;
        public MmdGeneratedPmxProxyMaterialDiagnostic[] materials = Array.Empty<MmdGeneratedPmxProxyMaterialDiagnostic>();

        // Edge proxies clone the body material to render the silhouette edge after the body
        // (with a higher render queue). The clones are not owned by the instance, so the caller
        // destroys them once the render completes.
        [NonSerialized]
        public List<Material> clonedEdgeMaterials = new List<Material>();
    }

    [Serializable]
    public sealed class MmdGeneratedPmxProxyMaterialDiagnostic
    {
        public string proxyKind = string.Empty;
        public int materialIndex;
        public int materialSlot;
        public string name = string.Empty;
        public string texture = string.Empty;
        public string sphereTexture = string.Empty;
        public string sphereTextureModeHint = string.Empty;
        public int renderQueue;
        public int proxyMeshVertexCount;
        public float[] baseColorProperty = Array.Empty<float>();
        public float[] colorProperty = Array.Empty<float>();
        public float[] outlineColorProperty = Array.Empty<float>();
        public bool sphereMapBound;
        public float sphereMode = -1.0f;
        public float alpha = -1.0f;
        public float outlineWidth = -1.0f;
        public float outlineScreenSpaceWeight = -1.0f;
        public float textureAlphaOutputWeight = -1.0f;
        public float textureFlatLightingWeight = -1.0f;
        public float textureFlatLightingValue = -1.0f;
        public float alphaClipThreshold = -1.0f;
        public float zWrite = -1.0f;
        public float outlineZWrite = -1.0f;
        public float srcBlend = -1.0f;
        public float dstBlend = -1.0f;
    }
    [Serializable]
    public sealed class MmdGeneratedPmxVisualCaseReport
    {
        public string caseName = string.Empty;
        public string sourceCaseId = string.Empty;
        public string status = string.Empty;
        public string modelFileName = string.Empty;
        public string modelPath = string.Empty;
        public string actualImage = string.Empty;
        public int width;
        public int height;
        public string shaderName = string.Empty;
        public string renderTextureFormat = string.Empty;
        public string renderTextureGraphicsFormat = string.Empty;
        public bool renderTextureSrgb;
        public string readbackTextureFormat = string.Empty;
        public string pngEncodingSource = string.Empty;
        public int materialCount;
        public string materialProxyMode = string.Empty;
        public string materialProxyPolicy = string.Empty;
        public int materialProxyRendererCount;
        public int materialProxyMeshVertexCount;
        public MmdGeneratedPmxProxyMaterialDiagnostic[] materialProxyBindings = Array.Empty<MmdGeneratedPmxProxyMaterialDiagnostic>();
        public MmdUnityMaterialBindingDiagnostic[] materialBindings = Array.Empty<MmdUnityMaterialBindingDiagnostic>();
        public int vertexCount;
        public int indexCount;
        public int submeshCount;
        public int loadedDiffuseTextures;
        public int loadedSphereTextures;
        public int loadedToonTextures;
        public int missingTextures;
        public int unsupportedTextures;
        public int skippedSphereTextures;
        public int skippedToonTextures;
        public float thresholdMean;
        public float[] cameraPosition = Array.Empty<float>();
        public float[] cameraTarget = Array.Empty<float>();
        public float cameraFieldOfView;
        public float[] cameraForward = Array.Empty<float>();
        public float[] cameraUp = Array.Empty<float>();
        public string cameraCoordinatePolicy = string.Empty;
        public float[] ambientLightColor = Array.Empty<float>();
        public float ambientLightIntensity;
        public float[] directionalLightColor = Array.Empty<float>();
        public float directionalLightIntensity;
        public float[] directionalLightPosition = Array.Empty<float>();
        public float[] directionalLightTarget = Array.Empty<float>();
        public string directionalLightMode = string.Empty;
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
        public bool toonBoundaryConfigured;
        public string toonBoundaryMode = string.Empty;
        public string selectedMaterialPassName = string.Empty;
        public string selectedMaterialLightMode = string.Empty;
        public int selectedMaterialPassIndex = -1;
        public bool selectedMaterialPassEnabled;
        public bool selectedMaterialPassValid;
        public string captureRequestType = string.Empty;
        public string captureRenderPath = string.Empty;
        public bool captureUsedStandardRequest;
        public string renderPipelineName = string.Empty;
        public int nonBlankPixelCount;
        public int alphaPixelCount;
        public int transparentPixelCount;
        public int outlinePixelCount;
        public bool backgroundEnabled;
        public string backgroundMode = string.Empty;
        public bool postProcessingEnabled;
        public string postProcessingMode = string.Empty;
    }
}
