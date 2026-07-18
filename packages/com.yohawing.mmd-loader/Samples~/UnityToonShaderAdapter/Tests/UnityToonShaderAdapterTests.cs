#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mmd.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mmd.Samples.UnityToonShader.Tests
{
    public sealed class UnityToonShaderAdapterTests
    {
        [Test]
        public void SchemaCanaryAcceptsUts0141WhenPresent()
        {
            Shader shader = RequireInstalledUtsShader();

            var diagnostics = new List<UnityToonShaderDiagnostic>();
            Assert.That(UnityToonShaderAdapter.TryValidateSchema(shader, diagnostics), Is.True,
                string.Join("\n", diagnostics));
            Assert.That(diagnostics.Select(item => item.Code), Does.Contain("UTS_SCHEMA_OK"));
        }

        [Test]
        public void InjectedNullFallsBackWithoutTouchingOriginalSlots()
        {
            Material original = CreateSourceMaterial();
            var originals = new[] { original };
            var diagnostics = new List<UnityToonShaderDiagnostic>();
            try
            {
                bool converted = UnityToonShaderAdapter.TryConvertMaterialsWithShader(
                    new[] { CreateDescriptor() },
                    originals,
                    null,
                    containsMaterialMorphs: false,
                    out Material[] result,
                    diagnostics);

                Assert.That(converted, Is.False);
                Assert.That(result, Is.SameAs(originals));
                Assert.That(result[0], Is.SameAs(original));
                Assert.That(diagnostics.Select(item => item.Code), Does.Contain("UTS_SHADER_ABSENT"));
                Assert.That(diagnostics.Select(item => item.Code), Does.Contain("UTS_FALLBACK_MMD_TOON"));
            }
            finally
            {
                Object.DestroyImmediate(original);
            }
        }

        [Test]
        public void ConversionMapsConservativeStatesAndReportsApproximations()
        {
            Shader shader = RequireInstalledUtsShader();

            Material original = CreateSourceMaterial();
            var texture = new Texture2D(1, 1);
            var sphere = new Texture2D(1, 1);
            original.SetTexture("_BaseMap", texture);
            original.SetTexture("_SphereMap", sphere);
            original.renderQueue = 3007;
            var diagnostics = new List<UnityToonShaderDiagnostic>();
            Material[] converted = new Material[0];
            try
            {
                bool success = UnityToonShaderAdapter.TryConvertMaterialsWithShader(
                    new[] { CreateDescriptor() },
                    new[] { original },
                    shader,
                    containsMaterialMorphs: true,
                    out converted,
                    diagnostics);

                Assert.That(success, Is.True, string.Join("\n", diagnostics));
                Assert.That(converted, Has.Length.EqualTo(1));
                Assert.That(converted[0], Is.Not.SameAs(original));
                Assert.That(converted[0].shader, Is.SameAs(shader));
                Assert.That(converted[0].GetTexture("_BaseMap"), Is.SameAs(texture));
                Assert.That(converted[0].GetTexture("_MatCap_Sampler"), Is.SameAs(sphere));
                Assert.That(converted[0].GetFloat("_MatCap"), Is.EqualTo(1.0f));
                Assert.That(converted[0].GetFloat("_CullMode"), Is.EqualTo(0.0f));
                Assert.That(converted[0].GetFloat("_TransparentEnabled"), Is.EqualTo(1.0f));
                Assert.That(converted[0].IsKeywordEnabled("_IS_CLIPPING_TRANSMODE"), Is.True);
                Assert.That(converted[0].GetFloat("_Clipping_Level"), Is.EqualTo(0.5f));
                Assert.That(converted[0].GetTag("RenderType", false), Is.EqualTo("Transparent"));
                Assert.That(converted[0].renderQueue, Is.EqualTo(3007));
                Assert.That(converted[0].GetFloat("_Outline_Width"), Is.GreaterThan(0.0f));
                Assert.That(converted[0].GetFloat("_OUTLINE"), Is.EqualTo(0.0f));
                Assert.That(converted[0].IsKeywordEnabled("_OUTLINE_NML"), Is.True);
                Assert.That(converted[0].GetShaderPassEnabled("SRPDefaultUnlit"), Is.True);
                Assert.That(converted[0].GetFloat("_SPRDefaultUnlitColorMask"), Is.EqualTo(0.0f));
                Assert.That(converted[0].GetFloat("_SRPDefaultUnlitColMode"), Is.EqualTo(1.0f));
                Assert.That(diagnostics.Select(item => item.Code), Does.Contain("UTS_TOON_RAMP_APPROXIMATION"));
                Assert.That(diagnostics.Select(item => item.Code), Does.Contain("UTS_SELF_SHADOW_INCOMPATIBLE"));
                Assert.That(diagnostics.Select(item => item.Code), Does.Contain("UTS_MATERIAL_MORPH_INCOMPATIBLE"));
                Assert.That(diagnostics.Select(item => item.Code), Does.Contain("UTS_SPH_MATCAP_APPROXIMATION"));
                Assert.That(diagnostics.Select(item => item.Code), Does.Contain("UTS_OUTLINE_WIDTH_APPROXIMATION"));
                Assert.That(diagnostics.Select(item => item.Code), Does.Contain("UTS_TRANSPARENT_OUTLINE_DISABLED"));
            }
            finally
            {
                UnityToonShaderAdapter.DestroyMaterials(converted);
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(sphere);
            }
        }

        [Test]
        public void AlphaClipThresholdIsConvertedToUtsClippingOffset()
        {
            Shader shader = RequireInstalledUtsShader();

            Material original = CreateSourceMaterial();
            MmdMaterialDescriptor descriptor = CreateDescriptor();
            descriptor.alpha = 1.0f;
            original.SetFloat("_AlphaClipThreshold", 0.2f);
            var diagnostics = new List<UnityToonShaderDiagnostic>();
            Material[] converted = new Material[0];
            try
            {
                bool success = UnityToonShaderAdapter.TryConvertMaterialsWithShader(
                    new[] { descriptor },
                    new[] { original },
                    shader,
                    containsMaterialMorphs: false,
                    out converted,
                    diagnostics);

                Assert.That(success, Is.True, string.Join("\n", diagnostics));
                Assert.That(converted[0].GetFloat("_ClippingMode"), Is.EqualTo(1.0f));
                Assert.That(converted[0].GetFloat("_Clipping_Level"), Is.EqualTo(0.3f).Within(0.0001f));
                Assert.That(converted[0].IsKeywordEnabled("_IS_CLIPPING_MODE"), Is.True);
                Assert.That(converted[0].GetFloat("_SPRDefaultUnlitColorMask"), Is.EqualTo(15.0f));
            }
            finally
            {
                UnityToonShaderAdapter.DestroyMaterials(converted);
                Object.DestroyImmediate(original);
            }
        }

        [Test]
        public void UnrepresentableAlphaClipThresholdIsClampedWithDiagnostic()
        {
            Shader shader = RequireInstalledUtsShader();

            Material original = CreateSourceMaterial();
            MmdMaterialDescriptor descriptor = CreateDescriptor();
            descriptor.alpha = 1.0f;
            original.SetFloat("_AlphaClipThreshold", 0.8f);
            var diagnostics = new List<UnityToonShaderDiagnostic>();
            Material[] converted = Array.Empty<Material>();
            try
            {
                bool success = UnityToonShaderAdapter.TryConvertMaterialsWithShader(
                    new[] { descriptor }, new[] { original }, shader, false, out converted, diagnostics);

                Assert.That(success, Is.True, string.Join("\n", diagnostics));
                Assert.That(converted[0].GetFloat("_Clipping_Level"), Is.EqualTo(0.0f));
                Assert.That(diagnostics.Select(item => item.Code), Does.Contain("UTS_ALPHA_CLIP_CLAMPED"));
            }
            finally
            {
                UnityToonShaderAdapter.DestroyMaterials(converted);
                Object.DestroyImmediate(original);
            }
        }

        [Test]
        public void RepresentativeMaterialRendersVisualCanaryPng()
        {
            Shader shader = RequireInstalledUtsShader();

            Material original = CreateSourceMaterial();
            var baseTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var sphereTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var renderTexture = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var cameraObject = new GameObject("UTS Adapter Canary Camera");
            var lightObject = new GameObject("UTS Adapter Canary Light");
            GameObject geometry = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Material[] converted = Array.Empty<Material>();
            RenderTexture? previousActive = RenderTexture.active;
            try
            {
                baseTexture.SetPixels(new[]
                {
                    new Color(0.95f, 0.35f, 0.20f, 1.0f),
                    new Color(0.25f, 0.75f, 0.95f, 1.0f),
                    new Color(0.90f, 0.85f, 0.25f, 1.0f),
                    new Color(0.45f, 0.25f, 0.80f, 1.0f)
                });
                baseTexture.Apply();
                sphereTexture.SetPixels(new[]
                {
                    Color.white,
                    new Color(0.65f, 0.75f, 1.0f, 1.0f),
                    new Color(1.0f, 0.65f, 0.45f, 1.0f),
                    new Color(0.35f, 0.45f, 0.70f, 1.0f)
                });
                sphereTexture.Apply();
                original.SetTexture("_BaseMap", baseTexture);
                original.SetTexture("_SphereMap", sphereTexture);
                original.renderQueue = 2000;

                MmdMaterialDescriptor descriptor = CreateDescriptor();
                descriptor.alpha = 1.0f;
                var diagnostics = new List<UnityToonShaderDiagnostic>();
                bool success = UnityToonShaderAdapter.TryConvertMaterialsWithShader(
                    new[] { descriptor },
                    new[] { original },
                    shader,
                    containsMaterialMorphs: false,
                    out converted,
                    diagnostics);
                Assert.That(success, Is.True, string.Join("\n", diagnostics));

                const int canaryLayer = 31;
                geometry.name = "UTS Adapter Canary Geometry";
                geometry.layer = canaryLayer;
                geometry.GetComponent<Renderer>().sharedMaterial = converted[0];

                var camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = new Vector3(0.0f, 0.0f, -3.0f);
                cameraObject.transform.LookAt(Vector3.zero);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f, 1.0f);
                camera.fieldOfView = 35.0f;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.cullingMask = 1 << canaryLayer;
                camera.targetTexture = renderTexture;

                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1.0f, 0.94f, 0.86f, 1.0f);
                light.intensity = 1.2f;
                light.cullingMask = 1 << canaryLayer;
                lightObject.transform.rotation = Quaternion.Euler(35.0f, -30.0f, 0.0f);

                renderTexture.Create();
                camera.Render();
                AssertUtsShaderHealthy(shader);
                RenderTexture.active = renderTexture;
                readback.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                readback.Apply();

                Color[] pixels = readback.GetPixels();
                var distinctColors = new HashSet<int>();
                float minLuminance = float.PositiveInfinity;
                float maxLuminance = float.NegativeInfinity;
                foreach (Color pixel in pixels)
                {
                    Assert.That(float.IsNaN(pixel.r) || float.IsInfinity(pixel.r), Is.False);
                    Assert.That(float.IsNaN(pixel.g) || float.IsInfinity(pixel.g), Is.False);
                    Assert.That(float.IsNaN(pixel.b) || float.IsInfinity(pixel.b), Is.False);
                    float luminance = pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f;
                    minLuminance = Mathf.Min(minLuminance, luminance);
                    maxLuminance = Mathf.Max(maxLuminance, luminance);
                    int packed = Mathf.RoundToInt(pixel.r * 255.0f) |
                        (Mathf.RoundToInt(pixel.g * 255.0f) << 8) |
                        (Mathf.RoundToInt(pixel.b * 255.0f) << 16);
                    distinctColors.Add(packed);
                }

                Assert.That(maxLuminance - minLuminance, Is.GreaterThan(0.05f),
                    "visual canary must contain rendered geometry distinct from its background");
                Assert.That(distinctColors.Count, Is.GreaterThan(1),
                    "visual canary must contain both rendered geometry and background pixels");

                string projectRoot = Path.GetDirectoryName(Application.dataPath)!;
                string repositoryRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));
                string artifactPath = Path.Combine(repositoryRoot, "artifacts", "visual", "uts-adapter-canary.png");
                Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
                File.WriteAllBytes(artifactPath, readback.EncodeToPNG());
                Assert.That(new FileInfo(artifactPath).Length, Is.GreaterThan(0));
                Debug.Log($"UTS adapter visual canary: {artifactPath}");
            }
            finally
            {
                RenderTexture.active = previousActive;
                UnityToonShaderAdapter.DestroyMaterials(converted);
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(baseTexture);
                Object.DestroyImmediate(sphereTexture);
                Object.DestroyImmediate(readback);
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(geometry);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static Material CreateSourceMaterial()
        {
            Shader shader = Shader.Find("MMD Basic URP Toon");
            Assert.That(shader, Is.Not.Null, "MMD Toon shader must be available to the imported sample tests.");
            return new Material(shader);
        }

        private static Shader RequireInstalledUtsShader()
        {
            Shader shader = Shader.Find(UnityToonShaderAdapter.ExpectedShaderName);
            if (shader == null)
            {
                Assert.Ignore("Optional Unity Toon Shader is not installed.");
            }

            AssertUtsShaderHealthy(shader);
            return shader;
        }

        private static void AssertUtsShaderHealthy(Shader shader)
        {
            Assert.That(shader.isSupported, Is.True,
                "Installed Unity Toon Shader must be supported by the active render pipeline.");
            ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
            string errors = string.Join("\n", messages
                .Where(message => message.severity == ShaderCompilerMessageSeverity.Error)
                .Select(message => message.message));
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False,
                "Installed Unity Toon Shader has compiler errors:\n" + errors);
        }

        private static MmdMaterialDescriptor CreateDescriptor()
        {
            return new MmdMaterialDescriptor
            {
                materialIndex = 0,
                name = "sample",
                texture = "diffuse.png",
                sphereTexture = "effect.sph",
                sphereTextureMode = "multiply-sphere",
                alpha = 0.5f,
                diffuseColor = new[] { 0.8f, 0.6f, 0.4f },
                edgeColor = new[] { 0.1f, 0.2f, 0.3f, 1.0f },
                edgeSize = 2.0f,
                drawEdgeFlag = true,
                cullingPolicy = "double-sided",
                toonShared = true,
                sharedToonIndex = 0
            };
        }
    }
}
