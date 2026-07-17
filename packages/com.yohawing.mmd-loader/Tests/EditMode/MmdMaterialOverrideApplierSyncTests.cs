#nullable enable

using System;
using System.Linq;
using System.Reflection;
using Mmd.Rendering;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mmd.EditModeTests
{
    public sealed class MmdMaterialOverrideApplierSyncTests
    {
        private static readonly string[] DescriptorExcludedFlags =
        {
            "hasAlphaClipThreshold",
            "hasColor",
            "hasEmissionColor",
            "hasMetallic",
            "hasMetallicMap",
            "hasNormalMap",
            "hasNormalScale",
            "hasOcclusionMap",
            "hasOcclusionStrength",
            "hasRoughnessMap",
            "hasSmoothness",
            "hasSurfaceMode"
        };

        private static readonly string[] UrpBindingExcludedFlags =
        {
            "hasColor",
            "hasEmissionColor",
            "hasMetallic",
            "hasMetallicMap",
            "hasNormalMap",
            "hasNormalScale",
            "hasOcclusionMap",
            "hasOcclusionStrength",
            "hasRoughnessMap",
            "hasSmoothness"
        };

        [Test]
        public void OverrideEntryHasFlagInventoryIsPinned()
        {
            string[] expected =
            {
                "hasAlpha",
                "hasAlphaClipThreshold",
                "hasAmbientColor",
                "hasBaseColor",
                "hasColor",
                "hasEmissionColor",
                "hasMetallic",
                "hasMetallicMap",
                "hasNormalMap",
                "hasNormalScale",
                "hasOcclusionMap",
                "hasOcclusionStrength",
                "hasOutlineColor",
                "hasOutlineWidth",
                "hasRoughnessMap",
                "hasSmoothness",
                "hasSurfaceMode"
            };
            string[] actual = typeof(MmdMaterialOverrideEntry)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => field.FieldType == typeof(bool) && field.Name.StartsWith("has", StringComparison.Ordinal))
                .Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(actual, Is.EqualTo(expected),
                "MmdMaterialOverrideEntry に has* flag を追加/削除したら、" +
                "MmdMaterialOverrideApplier の ApplyEntry / ApplyDescriptorEntry / " +
                "ApplyUrpMaterialBindingEntry の 3 経路すべてを確認し、" +
                "このテストと下の三経路テストを更新すること。");
        }

        [Test]
        public void AllFlagsApplyToMaterialSink()
        {
            Shader shader = Shader.Find("MMD Basic URP Toon")
                ?? throw new InvalidOperationException("MMD Basic URP Toon shader was not found.");
            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("Universal Render Pipeline/Lit shader was not found.");
            Material? toonMaterial = null;
            Material? urpLitMaterial = null;
            MmdMaterialOverrideAsset? asset = null;
            Texture2D? normalMap = null;
            Texture2D? metallicMap = null;
            Texture2D? roughnessMap = null;
            Texture2D? occlusionMap = null;
            try
            {
                toonMaterial = new Material(shader) { name = "sync-material-toon" };
                urpLitMaterial = new Material(urpLitShader) { name = "sync-material-urp-lit" };
                normalMap = NewTexture(new Color(0.2f, 0.3f, 0.9f));
                metallicMap = NewTexture(new Color(0.7f, 0.1f, 0.2f));
                roughnessMap = NewTexture(new Color(0.4f, 0.5f, 0.6f));
                occlusionMap = NewTexture(new Color(0.3f, 0.8f, 0.1f));
                MmdMaterialOverrideEntry entry = CreateAllFlagsEntry(normalMap, metallicMap, roughnessMap, occlusionMap);
                asset = CreateAsset(entry);

                MmdMaterialOverrideApplier.Apply(asset, new[] { toonMaterial });
                MmdMaterialOverrideApplier.Apply(asset, new[] { urpLitMaterial });

                AssertHasProperties(toonMaterial,
                    MmdMaterialPropertyNames.BaseColor, MmdMaterialPropertyNames.Color, MmdMaterialPropertyNames.Alpha,
                    MmdMaterialPropertyNames.AmbientColor, MmdMaterialPropertyNames.OutlineColor,
                    MmdMaterialPropertyNames.OutlineWidth, MmdMaterialPropertyNames.OutlineVisible,
                    MmdMaterialPropertyNames.MmdNormalMap, MmdMaterialPropertyNames.MmdNormalMapBound,
                    MmdMaterialPropertyNames.AlphaClipThreshold, MmdMaterialPropertyNames.ShadowAlphaClipThreshold,
                    MmdMaterialPropertyNames.TextureAlphaOutputWeight, "_SrcBlend", "_DstBlend", "_ZWrite");
                AssertColor(toonMaterial.GetColor(MmdMaterialPropertyNames.BaseColor), new Color(0.11f, 0.22f, 0.33f, 0.42f));
                AssertColor(toonMaterial.GetColor(MmdMaterialPropertyNames.Color), new Color(0.44f, 0.55f, 0.66f, 0.42f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.Alpha), Is.EqualTo(0.42f).Within(0.00001f));
                AssertColor(toonMaterial.GetColor(MmdMaterialPropertyNames.AmbientColor), new Color(0.13f, 0.24f, 0.35f, 1.0f));
                AssertColor(toonMaterial.GetColor(MmdMaterialPropertyNames.OutlineColor), new Color(0.72f, 0.61f, 0.5f, 0.39f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.OutlineWidth), Is.EqualTo(1.75f).Within(0.00001f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.OutlineVisible), Is.EqualTo(1.0f));
                Assert.That(toonMaterial.GetTexture(MmdMaterialPropertyNames.MmdNormalMap), Is.SameAs(normalMap));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.MmdNormalMapBound), Is.EqualTo(1.0f));

                AssertHasProperties(urpLitMaterial,
                    MmdMaterialPropertyNames.BaseColor, MmdMaterialPropertyNames.Metallic,
                    MmdMaterialPropertyNames.Smoothness, MmdMaterialPropertyNames.OcclusionStrength,
                    MmdMaterialPropertyNames.EmissionColor, MmdMaterialPropertyNames.BumpMap,
                    MmdMaterialPropertyNames.BumpScale, MmdMaterialPropertyNames.MetallicGlossMap,
                    MmdMaterialPropertyNames.OcclusionMap, "_Surface", "_SrcBlend", "_DstBlend", "_ZWrite");
                AssertColor(urpLitMaterial.GetColor(MmdMaterialPropertyNames.BaseColor), new Color(0.11f, 0.22f, 0.33f, 0.42f));
                Assert.That(urpLitMaterial.GetFloat(MmdMaterialPropertyNames.Metallic), Is.EqualTo(0.73f).Within(0.00001f));
                Assert.That(urpLitMaterial.GetFloat(MmdMaterialPropertyNames.Smoothness), Is.EqualTo(0.64f).Within(0.00001f));
                Assert.That(urpLitMaterial.GetFloat(MmdMaterialPropertyNames.OcclusionStrength), Is.EqualTo(0.58f).Within(0.00001f));
                AssertColor(urpLitMaterial.GetColor(MmdMaterialPropertyNames.EmissionColor), new Color(0.31f, 0.21f, 0.11f, 1.0f));
                Assert.That(urpLitMaterial.GetFloat(MmdMaterialPropertyNames.BumpScale), Is.EqualTo(0.87f).Within(0.00001f));
                Assert.That(urpLitMaterial.GetTexture(MmdMaterialPropertyNames.BumpMap), Is.SameAs(normalMap));
                Assert.That(urpLitMaterial.GetTexture(MmdMaterialPropertyNames.MetallicGlossMap), Is.SameAs(metallicMap));
                Assert.That(urpLitMaterial.GetTexture(MmdMaterialPropertyNames.OcclusionMap), Is.SameAs(occlusionMap));
                Assert.That(urpLitMaterial.IsKeywordEnabled("_NORMALMAP"), Is.True);
                Assert.That(urpLitMaterial.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
                Assert.That(urpLitMaterial.IsKeywordEnabled("_OCCLUSIONMAP"), Is.True);
                Assert.That(urpLitMaterial.IsKeywordEnabled("_EMISSION"), Is.True);

                Assert.That(toonMaterial.HasProperty(MmdMaterialPropertyNames.MmdMetallicMap), Is.False);
                Assert.That(urpLitMaterial.HasProperty(MmdMaterialPropertyNames.MmdMetallicMap), Is.False);
                Assert.That(toonMaterial.HasProperty(MmdMaterialPropertyNames.MmdRoughnessMap), Is.False);
                Assert.That(urpLitMaterial.HasProperty(MmdMaterialPropertyNames.MmdRoughnessMap), Is.False);
                Assert.That(toonMaterial.HasProperty(MmdMaterialPropertyNames.MmdOcclusionMap), Is.False);
                Assert.That(urpLitMaterial.HasProperty(MmdMaterialPropertyNames.MmdOcclusionMap), Is.False);
                Assert.That(toonMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
                AssertKeywordUndeclaredAndDisabled(toonMaterial, "_ALPHABLEND_ON");
                Assert.That(toonMaterial.GetFloat("_SrcBlend"), Is.EqualTo((float)BlendMode.SrcAlpha).Within(0.00001f));
                Assert.That(toonMaterial.GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.OneMinusSrcAlpha).Within(0.00001f));
                Assert.That(toonMaterial.GetFloat("_ZWrite"), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.AlphaClipThreshold), Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.ShadowAlphaClipThreshold), Is.EqualTo(0.27f).Within(0.00001f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.TextureAlphaOutputWeight), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(toonMaterial.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
                Assert.That(urpLitMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
                AssertKeywordUndeclaredAndDisabled(urpLitMaterial, "_ALPHABLEND_ON");
                Assert.That(urpLitMaterial.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
            }
            finally
            {
                Destroy(asset, toonMaterial, urpLitMaterial, normalMap, metallicMap, roughnessMap, occlusionMap);
            }
        }

        [Test]
        public void AllFlagsApplyToMaterialDescriptorSinkWithExplicitExclusions()
        {
            Assert.That(DescriptorExcludedFlags, Is.Ordered.Using<string>(StringComparer.Ordinal));
            MmdMaterialOverrideAsset? asset = null;
            try
            {
                MmdMaterialOverrideEntry entry = CreateAllFlagsEntry(null, null, null, null);
                asset = CreateAsset(entry);
                MmdRenderingDescriptor descriptor = CreateDescriptor();

                MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(asset, descriptor);

                MmdMaterialDescriptor material = descriptor.materials[0];
                Assert.That(material.diffuseColor, Is.EqualTo(new[] { 0.11f, 0.22f, 0.33f }));
                Assert.That(material.alpha, Is.EqualTo(0.42f).Within(0.00001f));
                Assert.That(material.ambientColor, Is.EqualTo(new[] { 0.13f, 0.24f, 0.35f }));
                Assert.That(material.edgeColor, Is.EqualTo(new[] { 0.72f, 0.61f, 0.5f, 0.39f }));
                Assert.That(material.edgeSize, Is.EqualTo(1.75f).Within(0.00001f));
                Assert.That(material.drawEdgeFlag, Is.True);
            }
            finally
            {
                Destroy(asset);
            }
        }

        [Test]
        public void AllFlagsApplyToUrpBindingSinkWithExplicitExclusions()
        {
            Assert.That(UrpBindingExcludedFlags, Is.Ordered.Using<string>(StringComparer.Ordinal));
            MmdMaterialOverrideAsset? asset = null;
            try
            {
                MmdMaterialOverrideEntry entry = CreateAllFlagsEntry(null, null, null, null);
                asset = CreateAsset(entry);
                MmdRenderingDescriptor descriptor = CreateDescriptor();

                MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(asset, descriptor);

                MmdUrpMaterialBindingDescriptor binding = descriptor.urpMaterialBindings[0];
                Assert.That(binding.diffuseColor, Is.EqualTo(new[] { 0.11f, 0.22f, 0.33f }));
                Assert.That(binding.alpha, Is.EqualTo(0.42f).Within(0.00001f));
                Assert.That(binding.ambientColor, Is.EqualTo(new[] { 0.13f, 0.24f, 0.35f }));
                Assert.That(binding.edgeColor, Is.EqualTo(new[] { 0.72f, 0.61f, 0.5f, 0.39f }));
                Assert.That(binding.edgeSize, Is.EqualTo(1.75f).Within(0.00001f));
                Assert.That(binding.drawEdgeFlag, Is.True);
                Assert.That(binding.isTransparent, Is.True);
                Assert.That(binding.transparencyMode, Is.EqualTo("alphaBlend"));
                Assert.That(binding.renderOrderBucket, Is.EqualTo("alphaBlend"));
            }
            finally
            {
                Destroy(asset);
            }
        }

        private static MmdMaterialOverrideEntry CreateAllFlagsEntry(
            Texture2D? normalMap,
            Texture2D? metallicMap,
            Texture2D? roughnessMap,
            Texture2D? occlusionMap)
            => new()
            {
                materialIndex = 0,
                hasBaseColor = true,
                baseColor = new Color(0.11f, 0.22f, 0.33f, 0.77f),
                hasColor = true,
                color = new Color(0.44f, 0.55f, 0.66f, 0.88f),
                hasAlpha = true,
                alpha = 0.42f,
                hasAmbientColor = true,
                ambientColor = new Color(0.13f, 0.24f, 0.35f, 1.0f),
                hasOutlineColor = true,
                outlineColor = new Color(0.72f, 0.61f, 0.5f, 0.39f),
                hasOutlineWidth = true,
                outlineWidth = 1.75f,
                hasMetallic = true,
                metallic = 0.73f,
                hasSmoothness = true,
                smoothness = 0.64f,
                hasNormalMap = true,
                normalMap = normalMap,
                hasOcclusionStrength = true,
                occlusionStrength = 0.58f,
                hasEmissionColor = true,
                emissionColor = new Color(0.31f, 0.21f, 0.11f, 1.0f),
                hasNormalScale = true,
                normalScale = 0.87f,
                hasMetallicMap = true,
                metallicMap = metallicMap,
                metallicMapIncludesSmoothness = true,
                hasRoughnessMap = true,
                roughnessMap = roughnessMap,
                hasOcclusionMap = true,
                occlusionMap = occlusionMap,
                hasSurfaceMode = true,
                surfaceMode = MmdMaterialOverrideSurfaceMode.AlphaBlend,
                hasAlphaClipThreshold = true,
                alphaClipThreshold = 0.27f
            };

        private static MmdMaterialOverrideAsset CreateAsset(MmdMaterialOverrideEntry entry)
        {
            MmdMaterialOverrideAsset asset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
            asset.entries = new[] { entry };
            return asset;
        }

        private static MmdRenderingDescriptor CreateDescriptor()
            => new()
            {
                materials = { new MmdMaterialDescriptor { materialIndex = 0, name = "sync-material" } },
                urpMaterialBindings = { new MmdUrpMaterialBindingDescriptor { materialIndex = 0, name = "sync-material" } }
            };

        private static Texture2D NewTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.00001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.00001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.00001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.00001f));
        }

        private static void AssertHasProperties(Material material, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                Assert.That(material.HasProperty(propertyName), Is.True,
                    $"Shader '{material.shader.name}' must expose '{propertyName}' for this synchronization test.");
            }
        }

        private static void AssertKeywordUndeclaredAndDisabled(Material material, string keywordName)
        {
            LocalKeyword keyword = material.shader.keywordSpace.FindKeyword(keywordName);
            Assert.That(keyword.isValid, Is.False,
                $"Shader '{material.shader.name}' unexpectedly declares '{keywordName}'.");
            Assert.That(material.IsKeywordEnabled(keywordName), Is.False,
                $"Undeclared keyword '{keywordName}' must not be retained by shader '{material.shader.name}'.");
        }

        private static void Destroy(params UnityEngine.Object?[] objects)
        {
            foreach (UnityEngine.Object? value in objects)
            {
                if (value != null)
                {
                    UnityEngine.Object.DestroyImmediate(value);
                }
            }
        }
    }
}
