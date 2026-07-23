#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Mmd.Editor;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed partial class MmdAssetImporterTests
    {
        private const int UtsVisualLayer = 31;

        [Serializable]
        private sealed class UtsVisualManifest
        {
            public int schemaVersion = 1;
            public UtsVisualManifestEntry[] entries = Array.Empty<UtsVisualManifestEntry>();
        }

        [Serializable]
        private sealed class UtsVisualManifestEntry
        {
            public string fixture = string.Empty;
            public string feature = string.Empty;
            public string texture = string.Empty;
            public string artifact = string.Empty;
            public int nonBackgroundPixels;
            public float maximumBackgroundDelta;
            public int targetChangedPixels;
            public float targetMaximumDelta;
        }

        private readonly struct UtsVisualCapture
        {
            public UtsVisualCapture(Color[] pixels, int nonBackgroundPixels, float maximumBackgroundDelta)
            {
                Pixels = pixels;
                NonBackgroundPixels = nonBackgroundPixels;
                MaximumBackgroundDelta = maximumBackgroundDelta;
            }

            public Color[] Pixels { get; }
            public int NonBackgroundPixels { get; }
            public float MaximumBackgroundDelta { get; }
        }

        [TestCase("test_1bone_cube.pmx", "opaque", null)]
        [TestCase("mmd-sphere-texture-multiply.pmx", "sphere", "sphere-radial-multiply.png")]
        [TestCase("mmd-texture-alpha-used-uv-cutout.pmx", "alpha", "texture-alpha-cutout.png")]
        [TestCase("mmd-outline-normal-silhouette.pmx", "outline", null)]
        public void PmxImporterCustomProfileRendersUtsVisualSmoke(
            string fixtureName,
            string feature,
            string? textureName)
        {
            Shader? utsShader = Shader.Find("Toon/Toon");
            if (utsShader == null)
            {
                Assert.Ignore("Optional Unity Toon Shader is not installed.");
            }

            string pmxPath = TempDirectory + "/" + fixtureName;
            CopyFixtureWithOptionalTexture(fixtureName, textureName, pmxPath);
            CreateUtsProfileAsset(utsShader!);
            ConfigureCustomProfileImporter(pmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(pmxPath);
            Assert.That(pmxAsset.ImportedRoot, Is.Not.Null);
            Assert.That(pmxAsset.ImportedMaterials, Is.Not.Empty);
            Material material = pmxAsset.ImportedMaterials[0];
            Assert.That(material.shader, Is.SameAs(utsShader));
            Assert.That(material.IsKeywordEnabled("_OUTLINE_NML"), Is.True);
            Assert.That(material.GetShaderPassEnabled("SRPDefaultUnlit"), Is.True);

            switch (feature)
            {
                case "sphere":
                    Assert.That(material.GetTexture("_MatCap_Sampler"), Is.Not.Null);
                    Assert.That(material.GetFloat("_Is_BlendAddToMatCap"), Is.EqualTo(1.0f));
                    break;
                case "alpha":
                    Assert.That(material.renderQueue, Is.LessThan((int)RenderQueue.Transparent));
                    Assert.That(material.GetFloat("_Clipping_Level"), Is.GreaterThan(0.0f));
                    Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.True);
                    break;
                case "outline":
                    Assert.That(material.GetFloat("_Outline_Width"), Is.GreaterThan(0.0f));
                    break;
                default:
                    Assert.That(material.renderQueue, Is.LessThan((int)RenderQueue.Transparent));
                    break;
            }

            GameObject instance = Object.Instantiate(pmxAsset.ImportedRoot!);
            GameObject cameraObject = new("UTS profile visual camera");
            GameObject lightObject = new("UTS profile visual light");
            RenderTexture renderTexture = new(256, 256, 24, RenderTextureFormat.ARGB32);
            Texture2D readback = new(256, 256, TextureFormat.RGBA32, mipChain: false);
            RenderTexture? previousActive = RenderTexture.active;
            AmbientMode previousAmbientMode = RenderSettings.ambientMode;
            Color previousAmbientLight = RenderSettings.ambientLight;
            float previousAmbientIntensity = RenderSettings.ambientIntensity;
            Light? previousSun = RenderSettings.sun;
            Color background = new(0.055f, 0.065f, 0.09f, 1.0f);
            List<Material>? instanceMaterials = null;
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.16f, 1.0f);
                RenderSettings.ambientIntensity = 1.0f;
                SetLayerRecursively(instance.transform, UtsVisualLayer);
                Bounds bounds = ResolveWorldBounds(instance);
                Vector3 target = bounds.center;
                float distance = Mathf.Max(0.35f, bounds.extents.magnitude * 3.0f);

                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = target + new Vector3(0.0f, 0.0f, distance);
                camera.transform.LookAt(target, Vector3.up);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;
                camera.fieldOfView = 35.0f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = Mathf.Max(20.0f, distance * 8.0f);
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.useOcclusionCulling = false;
                camera.cullingMask = 1 << UtsVisualLayer;
                camera.targetTexture = renderTexture;

                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1.0f, 0.94f, 0.86f, 1.0f);
                light.intensity = 1.2f;
                light.cullingMask = 1 << UtsVisualLayer;
                lightObject.transform.rotation = Quaternion.Euler(35.0f, -30.0f, 0.0f);
                RenderSettings.sun = light;

                renderTexture.Create();
                instanceMaterials = ResolveInstanceMaterials(instance);
                UtsVisualCapture enabledCapture = CaptureFrame(
                    camera,
                    renderTexture,
                    readback,
                    background,
                    feature);
                byte[] enabledPng = readback.EncodeToPNG();

                DisableFeatureForComparison(feature, instanceMaterials);
                UtsVisualCapture disabledCapture = CaptureFrame(
                    camera,
                    renderTexture,
                    readback,
                    background,
                    feature + " disabled comparison");
                int targetChangedPixels = CountChangedPixels(
                    enabledCapture.Pixels,
                    disabledCapture.Pixels,
                    out float targetMaximumDelta);

                Assert.That(enabledCapture.MaximumBackgroundDelta, Is.GreaterThan(0.01f),
                    feature + " UTS profile capture must contain visible imported geometry.");
                if (feature != "opaque")
                {
                    Assert.That(targetChangedPixels, Is.GreaterThan(4),
                        feature + " target feature toggle must change rendered pixels.");
                    Assert.That(targetMaximumDelta, Is.GreaterThan(0.01f),
                        feature + " target feature toggle must change pixel values.");
                }

                string artifactPath = Path.Combine(
                    ProjectRoot,
                    "artifacts",
                    "visual",
                    "uts-profile-import",
                    feature + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
                File.WriteAllBytes(artifactPath, enabledPng);
                Assert.That(new FileInfo(artifactPath).Length, Is.GreaterThan(0));
                WriteVisualManifest(new UtsVisualManifestEntry
                {
                    fixture = fixtureName,
                    feature = feature,
                    texture = textureName ?? string.Empty,
                    artifact = "artifacts/visual/uts-profile-import/" + feature + ".png",
                    nonBackgroundPixels = enabledCapture.NonBackgroundPixels,
                    maximumBackgroundDelta = enabledCapture.MaximumBackgroundDelta,
                    targetChangedPixels = targetChangedPixels,
                    targetMaximumDelta = targetMaximumDelta
                });
                Debug.Log($"UTS profile visual smoke ({feature}): {artifactPath}");
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientLight = previousAmbientLight;
                RenderSettings.ambientIntensity = previousAmbientIntensity;
                RenderSettings.sun = previousSun;
                Camera? camera = cameraObject.GetComponent<Camera>();
                if (camera != null)
                {
                    camera.targetTexture = null;
                }

                renderTexture.Release();
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(renderTexture);
                if (instanceMaterials != null)
                {
                    foreach (Material comparisonMaterial in instanceMaterials)
                    {
                        Object.DestroyImmediate(comparisonMaterial);
                    }
                }

                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(instance);
            }
        }

        private static UtsVisualCapture CaptureFrame(
            Camera camera,
            RenderTexture renderTexture,
            Texture2D readback,
            Color background,
            string captureName)
        {
            camera.Render();
            RenderTexture.active = renderTexture;
            readback.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            readback.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            Color[] pixels = readback.GetPixels();
            int nonBackgroundPixels = 0;
            float maximumBackgroundDelta = 0.0f;
            foreach (Color pixel in pixels)
            {
                Assert.That(float.IsNaN(pixel.r) || float.IsInfinity(pixel.r) ||
                    float.IsNaN(pixel.g) || float.IsInfinity(pixel.g) ||
                    float.IsNaN(pixel.b) || float.IsInfinity(pixel.b), Is.False,
                    captureName + " contains invalid pixels.");
                float delta = Mathf.Max(
                    Mathf.Abs(pixel.r - background.r),
                    Mathf.Abs(pixel.g - background.g),
                    Mathf.Abs(pixel.b - background.b));
                maximumBackgroundDelta = Mathf.Max(maximumBackgroundDelta, delta);
                if (delta > 0.01f)
                {
                    nonBackgroundPixels++;
                }
            }

            return new UtsVisualCapture(pixels, nonBackgroundPixels, maximumBackgroundDelta);
        }

        private static List<Material> ResolveInstanceMaterials(GameObject instance)
        {
            var materials = new List<Material>();
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                Material[] sharedMaterials = renderer.sharedMaterials;
                var clonedMaterials = new Material[sharedMaterials.Length];
                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    Material? sharedMaterial = sharedMaterials[i];
                    if (sharedMaterial == null)
                    {
                        continue;
                    }

                    Material clone = new(sharedMaterial)
                    {
                        name = sharedMaterial.name + " (UTS visual comparison)"
                    };
                    clonedMaterials[i] = clone;
                    if (!materials.Contains(clone))
                    {
                        materials.Add(clone);
                    }
                }

                renderer.sharedMaterials = clonedMaterials;
            }

            Assert.That(materials, Is.Not.Empty);
            return materials;
        }

        private static void DisableFeatureForComparison(string feature, IReadOnlyList<Material> materials)
        {
            foreach (Material material in materials)
            {
                switch (feature)
                {
                    case "sphere":
                        if (material.HasProperty("_MatCap_Sampler"))
                        {
                            material.SetTexture("_MatCap_Sampler", null);
                        }

                        if (material.HasProperty("_MatCap"))
                        {
                            material.SetFloat("_MatCap", 0.0f);
                        }

                        material.DisableKeyword("_MatCap");
                        break;
                    case "alpha":
                        if (material.HasProperty("_Clipping_Level"))
                        {
                            // A zero threshold still discards exact-zero alpha samples in UTS
                            // (clip(0) is discarded). Use a negative threshold for a true
                            // no-clipping comparison render.
                            material.SetFloat("_Clipping_Level", -1.0f);
                        }

                        if (material.HasProperty("_IsBaseMapAlphaAsClippingMask"))
                        {
                            material.SetFloat("_IsBaseMapAlphaAsClippingMask", 0.0f);
                        }

                        if (material.HasProperty("_ClippingMode"))
                        {
                            material.SetFloat("_ClippingMode", 0.0f);
                        }

                        material.DisableKeyword("_ALPHATEST_ON");
                        material.DisableKeyword("_IS_CLIPPING_TRANSMODE");
                        material.DisableKeyword("_IS_TRANSCLIPPING_ON");
                        material.EnableKeyword("_IS_TRANSCLIPPING_OFF");
                        material.EnableKeyword("_IS_CLIPPING_OFF");
                        break;
                    case "outline":
                        if (material.HasProperty("_OUTLINE"))
                        {
                            material.SetFloat("_OUTLINE", 0.0f);
                        }

                        if (material.HasProperty("_Outline_Width"))
                        {
                            material.SetFloat("_Outline_Width", 0.0f);
                        }

                        material.SetShaderPassEnabled("SRPDefaultUnlit", false);
                        break;
                }
            }
        }

        private static int CountChangedPixels(
            IReadOnlyList<Color> enabled,
            IReadOnlyList<Color> disabled,
            out float maximumDelta)
        {
            Assert.That(enabled.Count, Is.EqualTo(disabled.Count));
            int changedPixels = 0;
            maximumDelta = 0.0f;
            for (int i = 0; i < enabled.Count; i++)
            {
                float delta = Mathf.Max(
                    Mathf.Abs(enabled[i].r - disabled[i].r),
                    Mathf.Abs(enabled[i].g - disabled[i].g),
                    Mathf.Abs(enabled[i].b - disabled[i].b));
                maximumDelta = Mathf.Max(maximumDelta, delta);
                if (delta > 0.01f)
                {
                    changedPixels++;
                }
            }

            return changedPixels;
        }

        private static void WriteVisualManifest(UtsVisualManifestEntry entry)
        {
            string directory = Path.Combine(ProjectRoot, "artifacts", "visual", "uts-profile-import");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "manifest.json");
            UtsVisualManifest manifest = File.Exists(path)
                ? JsonUtility.FromJson<UtsVisualManifest>(File.ReadAllText(path)) ?? new UtsVisualManifest()
                : new UtsVisualManifest();
            var entries = new List<UtsVisualManifestEntry>(manifest.entries ?? Array.Empty<UtsVisualManifestEntry>());
            entries.RemoveAll(existing => existing.feature == entry.feature);
            entries.Add(entry);
            entries.Sort((left, right) => string.CompareOrdinal(left.feature, right.feature));
            manifest.entries = entries.ToArray();
            File.WriteAllText(path, JsonUtility.ToJson(manifest, prettyPrint: true));
        }

        private static void CopyFixtureWithOptionalTexture(
            string fixtureName,
            string? textureName,
            string destinationPmxPath)
        {
            string sourcePmx = fixtureName == "test_1bone_cube.pmx"
                ? MmdTestFixtures.FixtureAssetPath(fixtureName)
                : MmdTestFixtures.FixtureAssetPath("GeneratedPmx/" + fixtureName);
            string destinationPmx = Path.Combine(ProjectRoot, destinationPmxPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPmx)!);
            File.Copy(sourcePmx, destinationPmx, overwrite: true);
            if (!string.IsNullOrWhiteSpace(textureName))
            {
                string sourceTexture = MmdTestFixtures.FixtureAssetPath("GeneratedPmx/" + textureName);
                string destinationTexture = Path.Combine(
                    ProjectRoot,
                    TempDirectory,
                    textureName);
                File.Copy(sourceTexture, destinationTexture, overwrite: true);
                AssetDatabase.ImportAsset(
                    TempDirectory + "/" + textureName,
                    ImportAssetOptions.ForceUpdate);
            }

            AssetDatabase.ImportAsset(destinationPmxPath, ImportAssetOptions.ForceUpdate);
        }

        private static void CreateUtsProfileAsset(Shader utsShader)
        {
            var profile = ScriptableObject.CreateInstance<MmdMaterialProfileAsset>();
            profile.shader = utsShader;
            profile.textureTargets = new MmdMaterialProfileTextureTargets
            {
                diffuseTextureProperties = new[] { "_BaseMap", "_MainTex" },
                sphereTextureProperty = "_MatCap_Sampler",
                sphereTextureBoundProperty = "_MatCap",
                sphereModeProperty = "_Is_BlendAddToMatCap"
            };
            profile.renderingTargets = new MmdMaterialProfileRenderingTargets
            {
                baseColorProperty = "_BaseColor",
                colorProperty = "_Color",
                ambientColorProperty = string.Empty,
                alphaProperty = string.Empty,
                alphaClipThresholdProperty = "_Clipping_Level",
                shadowAlphaClipThresholdProperty = string.Empty,
                textureAlphaOutputWeightProperty = string.Empty,
                textureAlphaClipMaskProperty = "_IsBaseMapAlphaAsClippingMask",
                alphaClipModeProperty = "_ClippingMode",
                cullProperty = "_CullMode",
                surfaceProperty = string.Empty,
                blendProperty = string.Empty,
                sourceBlendProperty = string.Empty,
                destinationBlendProperty = string.Empty,
                zWriteProperty = "_ZWrite",
                outlineColorProperty = "_Outline_Color",
                outlineWidthProperty = "_Outline_Width",
                outlineVisibleProperty = "_OUTLINE",
                outlineScreenSpaceWeightProperty = string.Empty,
                outlineZTestProperty = string.Empty,
                requiredKeywords = new[]
                {
                    "_OUTLINE_NML",
                    "_MatCap",
                    "_IS_CLIPPING_OFF",
                    "_IS_CLIPPING_TRANSMODE",
                    "_IS_TRANSCLIPPING_ON",
                    "_IS_OUTLINE_CLIPPING_NO"
                },
                requiredPasses = new[] { "SRPDefaultUnlit" },
                unsupportedFeatures = new[] { "toon-texture", "self-shadow", "material-morph" },
                supportsMaterialMorphs = false
            };
            AssetDatabase.CreateAsset(profile, TempMaterialProfilePath);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureCustomProfileImporter(string pmxPath)
        {
            var importer = AssetImporter.GetAtPath(pmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);
            var serializedImporter = new SerializedObject(importer!);
            serializedImporter.FindProperty("shaderPreset").enumValueIndex = (int)MmdPmxShaderPreset.CustomProfile;
            serializedImporter.FindProperty("materialProfileAsset").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<MmdMaterialProfileAsset>(TempMaterialProfilePath);
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer!.SaveAndReimport();
        }

        private static Bounds ResolveWorldBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            Assert.That(renderers, Is.Not.Empty);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }
    }
}
