#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mmd.Parser;
using Mmd.Rendering;
using Mmd.Samples.UnityToonShader;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Mmd.Samples.UnityToonShader.Tests
{
    public sealed class GeneratedPmxUnityToonShaderVisualTests
    {
        private static readonly string[] FixtureNames = { "mmd-toon-ramp-lit-box", "mmd-sphere-texture-multiply", "mmd-texture-alpha-used-uv-cutout" };

        private static readonly Vector3 CameraPosition = new(-0.06f, 0.6f, 3.3f);
        private static readonly Vector3 CameraTarget = new(-0.06f, 0.6f, 0.0f);
        private static readonly Color BackgroundColor = new(0.72f, 0.75f, 0.80f, 1.0f);
        private const int CaptureLayer = 31;

        [Test]
        public void GeneratedPmxFixturesRenderLegacyAndUtsAdapterPngs()
        {
            Shader utsShader = RequireInstalledUtsShader();
            string fixtureDirectory = ResolveGeneratedPmxFixtureDirectory();
            string artifactDirectory = ResolveArtifactDirectory();
            Directory.CreateDirectory(artifactDirectory);
            foreach (string fixtureName in FixtureNames)
            {
                string pmxPath = Path.Combine(fixtureDirectory, fixtureName + ".pmx");
                Assert.That(File.Exists(pmxPath), Is.True, "Generated PMX fixture not found: " + pmxPath);
                RenderFixture(fixtureName, pmxPath, artifactDirectory, utsShader);
            }
        }

        private static void RenderFixture(string fixtureName, string pmxPath, string artifactDirectory, Shader utsShader)
        {
            GameObject cameraObject = new("generated-pmx-uts-camera");
            GameObject lightObject = new("generated-pmx-uts-light");
            RenderTexture renderTexture = new(512, 512, 24, RenderTextureFormat.ARGB32);
            Texture2D readback = new(512, 512, TextureFormat.RGBA32, mipChain: false);
            MmdUnityModelInstance? instance = null;
            Material[] convertedMaterials = Array.Empty<Material>();
            bool convertedMaterialsOwned = false;
            RenderTexture? previousActive = RenderTexture.active;
            AmbientMode previousAmbientMode = RenderSettings.ambientMode;
            Color previousAmbientLight = RenderSettings.ambientLight;
            Light? previousSun = RenderSettings.sun;
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.78f, 0.80f, 0.84f, 1.0f);

                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = CameraPosition;
                camera.transform.LookAt(CameraTarget, Vector3.up);
                camera.fieldOfView = 27.0f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 20.0f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = BackgroundColor;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.useOcclusionCulling = false;
                camera.cullingMask = 1 << CaptureLayer;
                camera.targetTexture = renderTexture;
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(154.0f / 255.0f, 154.0f / 255.0f, 154.0f / 255.0f, 1.0f);
                light.intensity = 1.8f;
                light.shadows = LightShadows.None;
                light.cullingMask = 1 << CaptureLayer;
                lightObject.transform.SetPositionAndRotation(
                    new Vector3(2.5f, 5.0f, 4.0f),
                    Quaternion.LookRotation(new Vector3(0.5f, -1.0f, -0.5f).normalized, Vector3.up));
                RenderSettings.sun = light;
                var parser = new NativeMmdParser();
                MmdModelDefinition model = parser.LoadModel(File.ReadAllBytes(pmxPath));
                instance = MmdUnityModelFactory.CreateSkinnedModel(model, pmxPath, 1.0f, MmdMaterialPreset.MmdToon);
                instance.Root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.Root.transform.localScale = Vector3.one;
                SetLayerRecursively(instance.Root.transform, CaptureLayer);
                Renderer[] renderers = instance.Root.GetComponentsInChildren<Renderer>(includeInactive: true);
                Assert.That(renderers, Is.Not.Empty, fixtureName + " must have a renderer.");
                Material[][] originalRendererMaterials = renderers.Select(renderer => renderer.sharedMaterials).ToArray();
                CapturePng(camera, renderTexture, readback,
                    Path.Combine(artifactDirectory, fixtureName + "-legacy.png"), fixtureName + " Legacy MMD Toon");

                var diagnostics = new List<UnityToonShaderDiagnostic>();
                bool converted = UnityToonShaderAdapter.TryConvertMaterialsWithShader(
                    instance.RenderingDescriptor.materials, instance.Materials, utsShader,
                    instance.RenderingDescriptor.materialMorphs.Count > 0, out convertedMaterials, diagnostics);
                convertedMaterialsOwned = converted && !ReferenceEquals(convertedMaterials, instance.Materials);
                Assert.That(converted, Is.True, string.Join("\n", diagnostics));
                Assert.That(convertedMaterials, Has.Length.EqualTo(instance.Materials.Length));
                foreach (Renderer renderer in renderers)
                {
                    renderer.sharedMaterials = convertedMaterials;
                    Assert.That(renderer.sharedMaterials, Is.EqualTo(convertedMaterials),
                        fixtureName + " UTS material slots were not rebound to every renderer.");
                }
                CapturePng(camera, renderTexture, readback,
                    Path.Combine(artifactDirectory, fixtureName + "-uts.png"), fixtureName + " UTS adapter");
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sharedMaterials = originalRendererMaterials[i];
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
                Camera? cleanupCamera = cameraObject.GetComponent<Camera>();
                if (cleanupCamera != null)
                {
                    cleanupCamera.targetTexture = null;
                }
                DestroyInstance(instance);
                if (convertedMaterialsOwned)
                {
                    UnityToonShaderAdapter.DestroyMaterials(convertedMaterials);
                }
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(cameraObject);
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientLight = previousAmbientLight;
                RenderSettings.sun = previousSun;
            }
        }

        private static void CapturePng(Camera camera, RenderTexture renderTexture, Texture2D readback,
            string artifactPath, string label)
        {
            renderTexture.Create();
            camera.Render();
            RenderTexture.active = renderTexture;
            readback.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            readback.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            float maximumBackgroundDelta = 0.0f;
            foreach (Color pixel in readback.GetPixels())
            {
                maximumBackgroundDelta = Mathf.Max(maximumBackgroundDelta,
                    Mathf.Abs(pixel.r - BackgroundColor.r), Mathf.Abs(pixel.g - BackgroundColor.g),
                    Mathf.Abs(pixel.b - BackgroundColor.b));
                Assert.That(float.IsNaN(pixel.r) || float.IsInfinity(pixel.r) ||
                    float.IsNaN(pixel.g) || float.IsInfinity(pixel.g) ||
                    float.IsNaN(pixel.b) || float.IsInfinity(pixel.b), Is.False, label);
            }
            Assert.That(maximumBackgroundDelta, Is.GreaterThan(0.01f),
                label + " must contain visible PMX geometry distinct from the fixed background.");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            File.WriteAllBytes(artifactPath, readback.EncodeToPNG());
            Assert.That(new FileInfo(artifactPath).Length, Is.GreaterThan(0), label + " PNG is empty.");
            Debug.Log($"{label} visual evidence: {artifactPath}");
        }

        private static Shader RequireInstalledUtsShader()
        {
            Shader? shader = Shader.Find(UnityToonShaderAdapter.ExpectedShaderName);
            if (shader == null)
            {
                Assert.Ignore("Optional Unity Toon Shader is not installed; generated-PMX UTS visual evidence was skipped.");
            }

            var diagnostics = new List<UnityToonShaderDiagnostic>();
            Assert.That(UnityToonShaderAdapter.TryValidateSchema(shader, diagnostics), Is.True,
                "Installed UTS shader failed the adapter schema: " + string.Join("\n", diagnostics));
            Assert.That(shader.isSupported, Is.True, "Installed UTS shader is not supported by the active render pipeline.");
            string errors = string.Join("\n", ShaderUtil.GetShaderMessages(shader)
                .Where(message => message.severity == ShaderCompilerMessageSeverity.Error)
                .Select(message => message.message));
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False,
                "Installed UTS shader has compiler errors:\n" + errors);
            return shader!;
        }

        private static string ResolveGeneratedPmxFixtureDirectory()
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo
                .FindForAssetPath("Packages/com.yohawing.mmd-loader");
            Assert.That(package, Is.Not.Null, "The loader package must be installed for generated PMX visual tests.");
            return Path.Combine(package.resolvedPath, "Tests", "Fixtures", "Assets", "GeneratedPmx");
        }

        private static string ResolveArtifactDirectory()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath)!;
            string repositoryRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));
            return Path.Combine(repositoryRoot, "artifacts", "visual", "uts-adapter-generated-pmx");
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }

        private static void DestroyInstance(MmdUnityModelInstance? instance)
        {
            if (instance == null)
            {
                return;
            }
            if (instance.Root != null)
            {
                Object.DestroyImmediate(instance.Root);
            }
            if (instance.Mesh != null)
            {
                Object.DestroyImmediate(instance.Mesh);
            }
            DestroyOwned(instance.Materials);
            DestroyOwned(instance.OwnedTextures);
        }

        private static void DestroyOwned(IEnumerable<Object> objects)
        {
            foreach (Object obj in objects.Where(item => item != null).Distinct())
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
