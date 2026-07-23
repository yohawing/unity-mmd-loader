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
            Color background = new(0.055f, 0.065f, 0.09f, 1.0f);
            try
            {
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

                renderTexture.Create();
                camera.Render();
                RenderTexture.active = renderTexture;
                readback.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                readback.Apply(updateMipmaps: false, makeNoLongerReadable: false);

                float maximumBackgroundDelta = 0.0f;
                foreach (Color pixel in readback.GetPixels())
                {
                    Assert.That(float.IsNaN(pixel.r) || float.IsInfinity(pixel.r) ||
                        float.IsNaN(pixel.g) || float.IsInfinity(pixel.g) ||
                        float.IsNaN(pixel.b) || float.IsInfinity(pixel.b), Is.False,
                        feature + " UTS profile capture contains invalid pixels.");
                    maximumBackgroundDelta = Mathf.Max(
                        maximumBackgroundDelta,
                        Mathf.Abs(pixel.r - background.r),
                        Mathf.Abs(pixel.g - background.g),
                        Mathf.Abs(pixel.b - background.b));
                }

                Assert.That(maximumBackgroundDelta, Is.GreaterThan(0.01f),
                    feature + " UTS profile capture must contain visible imported geometry.");
                string artifactPath = Path.Combine(
                    ProjectRoot,
                    "artifacts",
                    "visual",
                    "uts-profile-import",
                    feature + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
                File.WriteAllBytes(artifactPath, readback.EncodeToPNG());
                Assert.That(new FileInfo(artifactPath).Length, Is.GreaterThan(0));
                Debug.Log($"UTS profile visual smoke ({feature}): {artifactPath}");
            }
            finally
            {
                RenderTexture.active = previousActive;
                Camera? camera = cameraObject.GetComponent<Camera>();
                if (camera != null)
                {
                    camera.targetTexture = null;
                }

                renderTexture.Release();
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(instance);
            }
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
                requiredKeywords = new[] { "_OUTLINE_NML" },
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
