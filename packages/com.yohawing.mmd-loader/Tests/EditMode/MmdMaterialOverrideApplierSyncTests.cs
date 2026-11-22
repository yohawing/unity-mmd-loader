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
                "hasEmissionIntensity",
                "hasEmissionMap",
                "hasEmissionMask",
                "hasMetallic",
                "hasMetallicMap",
                "hasNormalMap",
                "hasNormalScale",
                "hasOcclusionMap",
                "hasOcclusionStrength",
                "hasOutlineColor",
                "hasOutlineWidth",
                "hasRimBoundary",
                "hasRimColor",
                "hasRimFeather",
                "hasRimLightFollow",
                "hasRoughnessMap",
                "hasSmoothness",
                "hasStylizedSpecularBoundary",
                "hasStylizedSpecularColor",
                "hasStylizedSpecularFeather",
                "hasSurfaceMode",
                "hasToonBandCount",
                "hasToonBoundary",
                "hasToonFeather"
            };
            string[] actual = typeof(MmdMaterialOverrideEntry)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => field.FieldType == typeof(bool) && field.Name.StartsWith("has", StringComparison.Ordinal))
                .Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(actual, Is.EqualTo(expected),
                "MmdMaterialOverrideEntry に has* flag を追加/削除したら、" +
                "MmdMaterialOverrideApplier の ApplyEntry / ApplyDescriptorEntry と " +
                "ApplyBindingSurfaceOverride を確認し、" +
                "このテストと下の 3 sink のテストを更新すること。");
        }

        [Test]
        public void AllFlagsApplyToMaterialSink()
        {
            Shader shader = Shader.Find(MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName)
                ?? throw new InvalidOperationException("MMD Toon Lit shader was not found.");
            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("Universal Render Pipeline/Lit shader was not found.");
            Material? toonMaterial = null;
            Material? urpLitMaterial = null;
            MmdMaterialOverrideAsset? asset = null;
            Texture2D? normalMap = null;
            Texture2D? metallicMap = null;
            Texture2D? roughnessMap = null;
            Texture2D? occlusionMap = null;
            Texture2D? emissionMap = null;
            Texture2D? emissionMask = null;
            try
            {
                toonMaterial = new Material(shader) { name = "sync-material-toon" };
                urpLitMaterial = new Material(urpLitShader) { name = "sync-material-urp-lit" };
                normalMap = NewTexture(new Color(0.2f, 0.3f, 0.9f));
                metallicMap = NewTexture(new Color(0.7f, 0.1f, 0.2f));
                roughnessMap = NewTexture(new Color(0.4f, 0.5f, 0.6f));
                occlusionMap = NewTexture(new Color(0.3f, 0.8f, 0.1f));
                emissionMap = NewTexture(new Color(0.8f, 0.7f, 0.6f));
                emissionMask = NewTexture(new Color(0.9f, 0.5f, 0.2f));
                MmdMaterialOverrideEntry entry = CreateAllFlagsEntry(
                    normalMap, metallicMap, roughnessMap, occlusionMap, emissionMap, emissionMask);
                asset = CreateAsset(entry);

                MmdMaterialOverrideApplier.Apply(asset, new[] { toonMaterial });
                MmdMaterialOverrideApplier.Apply(asset, new[] { urpLitMaterial });

                AssertHasProperties(toonMaterial,
                    MmdMaterialPropertyNames.BaseColor, MmdMaterialPropertyNames.Color, MmdMaterialPropertyNames.Alpha,
                    MmdMaterialPropertyNames.AmbientColor, MmdMaterialPropertyNames.ToonBandCount,
                    MmdMaterialPropertyNames.ToonBoundary, MmdMaterialPropertyNames.ToonFeather,
                    MmdMaterialPropertyNames.StylizedSpecularColor,
                    MmdMaterialPropertyNames.StylizedSpecularBoundary,
                    MmdMaterialPropertyNames.StylizedSpecularFeather,
                    MmdMaterialPropertyNames.EmissionColor, MmdMaterialPropertyNames.EmissionMap,
                    MmdMaterialPropertyNames.MmdEmissionIntensity, MmdMaterialPropertyNames.MmdEmissionMapBound,
                    MmdMaterialPropertyNames.MmdEmissionMask, MmdMaterialPropertyNames.MmdEmissionMaskBound,
                    MmdMaterialPropertyNames.RimColor,
                    MmdMaterialPropertyNames.RimBoundary,
                    MmdMaterialPropertyNames.RimFeather,
                    MmdMaterialPropertyNames.RimLightFollow,
                    MmdMaterialPropertyNames.OutlineColor,
                    MmdMaterialPropertyNames.OutlineWidth, MmdMaterialPropertyNames.OutlineVisible,
                    MmdMaterialPropertyNames.MmdNormalMap, MmdMaterialPropertyNames.MmdNormalMapBound,
                    MmdMaterialPropertyNames.AlphaClipThreshold, MmdMaterialPropertyNames.ShadowAlphaClipThreshold,
                    MmdMaterialPropertyNames.TextureAlphaOutputWeight, "_SrcBlend", "_DstBlend", "_ZWrite");
                AssertColor(toonMaterial.GetColor(MmdMaterialPropertyNames.BaseColor), new Color(0.11f, 0.22f, 0.33f, 0.42f));
                AssertColor(toonMaterial.GetColor(MmdMaterialPropertyNames.Color), new Color(0.44f, 0.55f, 0.66f, 0.42f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.Alpha), Is.EqualTo(0.42f).Within(0.00001f));
                AssertColor(toonMaterial.GetColor(MmdMaterialPropertyNames.AmbientColor), new Color(0.13f, 0.24f, 0.35f, 1.0f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.ToonBandCount), Is.EqualTo(3.0f).Within(0.00001f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.ToonBoundary), Is.EqualTo(0.42f).Within(0.00001f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.ToonFeather), Is.EqualTo(0.12f).Within(0.00001f));
                AssertColor(toonMaterial.GetColor(MmdMaterialPropertyNames.StylizedSpecularColor), new Color(0.91f, 0.81f, 0.71f, 0.61f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.StylizedSpecularBoundary), Is.EqualTo(0.90f).Within(0.00001f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.StylizedSpecularFeather), Is.EqualTo(0.03f).Within(0.00001f));
                AssertColor(toonMaterial.GetColor(MmdMaterialPropertyNames.RimColor), new Color(0.19f, 0.29f, 0.39f, 0.49f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.RimBoundary), Is.EqualTo(0.72f).Within(0.00001f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.RimFeather), Is.EqualTo(0.08f).Within(0.00001f));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.RimLightFollow), Is.EqualTo(0.65f).Within(0.00001f));
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
                AssertColor(urpLitMaterial.GetColor(MmdMaterialPropertyNames.EmissionColor), new Color(2.1f, 1.2f, 0.4f, 1.0f));
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
                Assert.That(toonMaterial.GetColor(MmdMaterialPropertyNames.EmissionColor), Is.EqualTo(new Color(2.1f, 1.2f, 0.4f, 1.0f)));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.MmdEmissionIntensity), Is.EqualTo(2.5f).Within(0.00001f));
                Assert.That(toonMaterial.GetTexture(MmdMaterialPropertyNames.EmissionMap), Is.SameAs(emissionMap));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.MmdEmissionMapBound), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(toonMaterial.GetTexture(MmdMaterialPropertyNames.MmdEmissionMask), Is.SameAs(emissionMask));
                Assert.That(toonMaterial.GetFloat(MmdMaterialPropertyNames.MmdEmissionMaskBound), Is.EqualTo(1.0f).Within(0.00001f));
                AssertKeywordUndeclaredAndDisabled(toonMaterial, "_EMISSION");
                Assert.That(toonMaterial.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
                Assert.That(urpLitMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
                AssertKeywordUndeclaredAndDisabled(urpLitMaterial, "_ALPHABLEND_ON");
                Assert.That(urpLitMaterial.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
            }
            finally
            {
                Destroy(asset, toonMaterial, urpLitMaterial, normalMap, metallicMap, roughnessMap, occlusionMap, emissionMap, emissionMask);
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
                Assert.That(material.emissionColor, Is.EqualTo(new[] { 2.1f, 1.2f, 0.4f }));
                Assert.That(material.emissionIntensity, Is.EqualTo(2.5f).Within(0.00001f));
                Assert.That(material.usesEmissionMap, Is.False);
                Assert.That(material.usesEmissionMask, Is.False);
                Assert.That(material.toonBandCount, Is.EqualTo(3.0f).Within(0.00001f));
                Assert.That(material.toonBoundary, Is.EqualTo(0.42f).Within(0.00001f));
                Assert.That(material.toonFeather, Is.EqualTo(0.12f).Within(0.00001f));
                Assert.That(material.stylizedSpecularColor, Is.EqualTo(new[] { 0.91f, 0.81f, 0.71f }));
                Assert.That(material.stylizedSpecularBoundary, Is.EqualTo(0.90f).Within(0.00001f));
                Assert.That(material.stylizedSpecularFeather, Is.EqualTo(0.03f).Within(0.00001f));
                Assert.That(material.rimColor, Is.EqualTo(new[] { 0.19f, 0.29f, 0.39f }));
                Assert.That(material.rimBoundary, Is.EqualTo(0.72f).Within(0.00001f));
                Assert.That(material.rimFeather, Is.EqualTo(0.08f).Within(0.00001f));
                Assert.That(material.rimLightFollow, Is.EqualTo(0.65f).Within(0.00001f));
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
                Assert.That(binding.emissionColor, Is.EqualTo(new[] { 2.1f, 1.2f, 0.4f }));
                Assert.That(binding.emissionIntensity, Is.EqualTo(2.5f).Within(0.00001f));
                Assert.That(binding.usesEmissionMap, Is.False);
                Assert.That(binding.usesEmissionMask, Is.False);
                Assert.That(binding.toonBandCount, Is.EqualTo(3.0f).Within(0.00001f));
                Assert.That(binding.toonBoundary, Is.EqualTo(0.42f).Within(0.00001f));
                Assert.That(binding.toonFeather, Is.EqualTo(0.12f).Within(0.00001f));
                Assert.That(binding.stylizedSpecularColor, Is.EqualTo(new[] { 0.91f, 0.81f, 0.71f }));
                Assert.That(binding.stylizedSpecularBoundary, Is.EqualTo(0.90f).Within(0.00001f));
                Assert.That(binding.stylizedSpecularFeather, Is.EqualTo(0.03f).Within(0.00001f));
                Assert.That(binding.rimColor, Is.EqualTo(new[] { 0.19f, 0.29f, 0.39f }));
                Assert.That(binding.rimBoundary, Is.EqualTo(0.72f).Within(0.00001f));
                Assert.That(binding.rimFeather, Is.EqualTo(0.08f).Within(0.00001f));
                Assert.That(binding.rimLightFollow, Is.EqualTo(0.65f).Within(0.00001f));
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

        [Test]
        public void PartialOverridePreservesEachDescriptorSinksUnspecifiedValues()
        {
            MmdMaterialOverrideAsset asset = CreateAsset(new MmdMaterialOverrideEntry
            {
                materialIndex = 0,
                hasAlpha = true,
                alpha = 0.45f
            });
            try
            {
                MmdRenderingDescriptor descriptor = CreateDescriptor();
                MmdMaterialDescriptor material = descriptor.materials[0];
                MmdUrpMaterialBindingDescriptor binding = descriptor.urpMaterialBindings[0];
                float[] materialAmbient = { 0.1f, 0.2f, 0.3f };
                float[] bindingAmbient = { 0.4f, 0.5f, 0.6f };
                material.ambientColor = materialAmbient;
                binding.ambientColor = bindingAmbient;
                material.toonBoundary = 0.2f;
                binding.toonBoundary = 0.8f;
                material.edgeSize = 1.0f;
                binding.edgeSize = 2.0f;

                MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(asset, descriptor);

                Assert.That(material.alpha, Is.EqualTo(0.45f));
                Assert.That(binding.alpha, Is.EqualTo(0.45f));
                Assert.That(material.ambientColor, Is.SameAs(materialAmbient));
                Assert.That(binding.ambientColor, Is.SameAs(bindingAmbient));
                Assert.That(materialAmbient, Is.EqualTo(new[] { 0.1f, 0.2f, 0.3f }));
                Assert.That(bindingAmbient, Is.EqualTo(new[] { 0.4f, 0.5f, 0.6f }));
                Assert.That(material.toonBoundary, Is.EqualTo(0.2f));
                Assert.That(binding.toonBoundary, Is.EqualTo(0.8f));
                Assert.That(material.edgeSize, Is.EqualTo(1.0f));
                Assert.That(binding.edgeSize, Is.EqualTo(2.0f));
            }
            finally
            {
                Destroy(asset);
            }
        }

        [Test]
        public void OverrideColorArraysRemainIndependentBetweenDescriptorSinks()
        {
            MmdMaterialOverrideAsset asset = CreateAsset(CreateAllFlagsEntry(null, null, null, null));
            try
            {
                MmdRenderingDescriptor descriptor = CreateDescriptor();
                MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(asset, descriptor);
                MmdMaterialDescriptor material = descriptor.materials[0];
                MmdUrpMaterialBindingDescriptor binding = descriptor.urpMaterialBindings[0];
                float[][] materialColors =
                {
                    material.diffuseColor, material.ambientColor, material.emissionColor,
                    material.stylizedSpecularColor, material.rimColor, material.edgeColor
                };
                float[][] bindingColors =
                {
                    binding.diffuseColor, binding.ambientColor, binding.emissionColor,
                    binding.stylizedSpecularColor, binding.rimColor, binding.edgeColor
                };
                for (int i = 0; i < materialColors.Length; i++)
                {
                    Assert.That(bindingColors[i], Is.EqualTo(materialColors[i]));
                    Assert.That(bindingColors[i], Is.Not.SameAs(materialColors[i]));
                    float[] originalBindingColor = (float[])bindingColors[i].Clone();
                    materialColors[i][0] = -10.0f;
                    Assert.That(bindingColors[i], Is.EqualTo(originalBindingColor));
                }
            }
            finally
            {
                Destroy(asset);
            }
        }

        [Test]
        public void OverrideAppliesToMaterialDescriptorWithoutUrpBinding()
        {
            MmdMaterialOverrideAsset asset = CreateAsset(new MmdMaterialOverrideEntry
            {
                materialIndex = 0,
                hasBaseColor = true,
                baseColor = new Color(0.1f, 0.2f, 0.3f, 0.8f),
                hasAlpha = true,
                alpha = 0.45f,
                hasOutlineWidth = true,
                outlineWidth = 1.75f
            });
            try
            {
                MmdRenderingDescriptor descriptor = CreateDescriptor();
                descriptor.urpMaterialBindings.Clear();

                MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(asset, descriptor);

                MmdMaterialDescriptor material = descriptor.materials[0];
                Assert.That(material.diffuseColor, Is.EqualTo(new[] { 0.1f, 0.2f, 0.3f }));
                Assert.That(material.alpha, Is.EqualTo(0.45f));
                Assert.That(material.edgeSize, Is.EqualTo(1.75f));
                Assert.That(material.drawEdgeFlag, Is.True);
                Assert.That(descriptor.urpMaterialBindings, Is.Empty);
            }
            finally
            {
                Destroy(asset);
            }
        }

        [Test]
        public void NullEmissionTexturesClearMaterialBindings()
        {
            Shader shader = Shader.Find(MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName)
                ?? throw new InvalidOperationException("MMD Toon Lit shader was not found.");
            Material? material = null;
            MmdMaterialOverrideAsset? asset = null;
            Texture2D? map = null;
            Texture2D? mask = null;
            try
            {
                material = new Material(shader);
                map = NewTexture(Color.white);
                mask = NewTexture(Color.white);
                material.SetTexture(MmdMaterialPropertyNames.EmissionMap, map);
                material.SetTexture(MmdMaterialPropertyNames.MmdEmissionMask, mask);
                material.SetFloat(MmdMaterialPropertyNames.MmdEmissionMapBound, 1.0f);
                material.SetFloat(MmdMaterialPropertyNames.MmdEmissionMaskBound, 1.0f);
                asset = CreateAsset(new MmdMaterialOverrideEntry
                {
                    materialIndex = 0,
                    hasEmissionMap = true,
                    emissionMap = null,
                    hasEmissionMask = true,
                    emissionMask = null
                });

                MmdMaterialOverrideApplier.Apply(asset, new[] { material });

                Assert.That(material.GetTexture(MmdMaterialPropertyNames.EmissionMap), Is.Null);
                Assert.That(material.GetTexture(MmdMaterialPropertyNames.MmdEmissionMask), Is.Null);
                Assert.That(material.GetFloat(MmdMaterialPropertyNames.MmdEmissionMapBound), Is.EqualTo(0.0f));
                Assert.That(material.GetFloat(MmdMaterialPropertyNames.MmdEmissionMaskBound), Is.EqualTo(0.0f));
            }
            finally
            {
                Destroy(asset, material, map, mask);
            }
        }

        // Surface decision table shared by the Material sink and the URP binding sink.
        // Columns: surface override (null = no hasSurfaceMode), alpha override (null = no hasAlpha),
        // hasAlphaClipThreshold, initial descriptor binding alpha, expected Material outcome,
        // expected binding outcome. Material outcomes: "unchanged", "thresholdOnly" (clip
        // threshold written without a surface change), "opaque", "alphaTest", "alphaBlend"
        // (alphaBlend never clips). Binding outcomes are the transparencyMode, or "unchanged".
        // Decision 2026-11-22: alpha < 1 plus an alpha clip threshold without a surface mode is
        // transparent without clipping (alphaBlend) on both sinks.
        private static readonly object?[][] SurfaceDecisionTable =
        {
            new object?[] { null, null, false, 1.0f, "unchanged", "unchanged" },
            new object?[] { null, null, true, 1.0f, "alphaTest", "alphaTest" },
            new object?[] { null, 0.5f, false, 1.0f, "alphaBlend", "alphaBlend" },
            new object?[] { null, 0.5f, true, 1.0f, "alphaBlend", "alphaBlend" },
            new object?[] { null, 1.0f, false, 1.0f, "unchanged", "unchanged" },
            new object?[] { null, 1.0f, true, 1.0f, "alphaTest", "alphaTest" },
            new object?[] { null, null, false, 0.5f, "unchanged", "alphaBlend" },
            new object?[] { null, null, true, 0.5f, "alphaTest", "alphaTest" },
            new object?[] { MmdMaterialOverrideSurfaceMode.Preserve, null, false, 1.0f, "unchanged", "unchanged" },
            new object?[] { MmdMaterialOverrideSurfaceMode.Preserve, null, true, 1.0f, "thresholdOnly", "unchanged" },
            new object?[] { MmdMaterialOverrideSurfaceMode.Preserve, 0.5f, false, 1.0f, "unchanged", "unchanged" },
            new object?[] { MmdMaterialOverrideSurfaceMode.Preserve, 0.5f, true, 1.0f, "thresholdOnly", "unchanged" },
            new object?[] { MmdMaterialOverrideSurfaceMode.Preserve, 1.0f, false, 1.0f, "unchanged", "unchanged" },
            new object?[] { MmdMaterialOverrideSurfaceMode.Preserve, 1.0f, true, 1.0f, "thresholdOnly", "unchanged" },
            new object?[] { MmdMaterialOverrideSurfaceMode.Opaque, null, false, 1.0f, "opaque", "opaque" },
            new object?[] { MmdMaterialOverrideSurfaceMode.Opaque, null, true, 1.0f, "opaque", "opaque" },
            new object?[] { MmdMaterialOverrideSurfaceMode.Opaque, 0.5f, false, 1.0f, "opaque", "opaque" },
            new object?[] { MmdMaterialOverrideSurfaceMode.Opaque, 0.5f, true, 1.0f, "opaque", "opaque" },
            new object?[] { MmdMaterialOverrideSurfaceMode.Opaque, 1.0f, false, 1.0f, "opaque", "opaque" },
            new object?[] { MmdMaterialOverrideSurfaceMode.Opaque, 1.0f, true, 1.0f, "opaque", "opaque" },
            new object?[] { MmdMaterialOverrideSurfaceMode.AlphaTest, null, false, 1.0f, "alphaTest", "alphaTest" },
            new object?[] { MmdMaterialOverrideSurfaceMode.AlphaTest, null, true, 1.0f, "alphaTest", "alphaTest" },
            new object?[] { MmdMaterialOverrideSurfaceMode.AlphaTest, 0.5f, false, 1.0f, "alphaTest", "alphaTest" },
            new object?[] { MmdMaterialOverrideSurfaceMode.AlphaTest, 0.5f, true, 1.0f, "alphaTest", "alphaTest" },
            new object?[] { MmdMaterialOverrideSurfaceMode.AlphaTest, 1.0f, false, 1.0f, "alphaTest", "alphaTest" },
            new object?[] { MmdMaterialOverrideSurfaceMode.AlphaTest, 1.0f, true, 1.0f, "alphaTest", "alphaTest" },
            new object?[] { MmdMaterialOverrideSurfaceMode.AlphaBlend, null, false, 1.0f, "alphaBlend", "alphaBlend" },
            new object?[] { MmdMaterialOverrideSurfaceMode.AlphaBlend, null, true, 1.0f, "alphaBlend", "alphaBlend" },
            new object?[] { MmdMaterialOverrideSurfaceMode.AlphaBlend, 0.5f, false, 1.0f, "alphaBlend", "alphaBlend" },
            new object?[] { MmdMaterialOverrideSurfaceMode.AlphaBlend, 0.5f, true, 1.0f, "alphaBlend", "alphaBlend" },
            new object?[] { MmdMaterialOverrideSurfaceMode.AlphaBlend, 1.0f, false, 1.0f, "alphaBlend", "alphaBlend" },
            new object?[] { MmdMaterialOverrideSurfaceMode.AlphaBlend, 1.0f, true, 1.0f, "alphaBlend", "alphaBlend" }
        };

        private const int SentinelRenderQueue = 2001;
        private const float SentinelClipThreshold = 0.5f;
        private const float EntryClipThreshold = 0.27f;
        private const float DefaultClipThreshold = 0.01f;

        [TestCaseSource(nameof(SurfaceDecisionTable))]
        public void SurfaceDecisionTableIsSharedByMaterialAndBindingSinks(
            MmdMaterialOverrideSurfaceMode? surfaceMode,
            float? alpha,
            bool hasAlphaClipThreshold,
            float descriptorAlpha,
            string expectedMaterial,
            string expectedBinding)
        {
            Shader shader = Shader.Find(MmdUrpMaterialBindingDescriptorBuilder.MmdToonLitShaderName)
                ?? throw new InvalidOperationException("MMD Toon Lit shader was not found.");
            Material? material = null;
            MmdMaterialOverrideAsset? asset = null;
            try
            {
                var entry = new MmdMaterialOverrideEntry { materialIndex = 0 };
                if (surfaceMode.HasValue)
                {
                    entry.hasSurfaceMode = true;
                    entry.surfaceMode = surfaceMode.Value;
                }

                if (alpha.HasValue)
                {
                    entry.hasAlpha = true;
                    entry.alpha = alpha.Value;
                }

                if (hasAlphaClipThreshold)
                {
                    entry.hasAlphaClipThreshold = true;
                    entry.alphaClipThreshold = EntryClipThreshold;
                }

                asset = CreateAsset(entry);

                material = new Material(shader) { name = "surface-table" };
                AssertHasProperties(material,
                    MmdMaterialPropertyNames.AlphaClipThreshold, MmdMaterialPropertyNames.ShadowAlphaClipThreshold);
                material.renderQueue = SentinelRenderQueue;
                material.SetFloat(MmdMaterialPropertyNames.AlphaClipThreshold, SentinelClipThreshold);
                material.SetFloat(MmdMaterialPropertyNames.ShadowAlphaClipThreshold, SentinelClipThreshold);
                MmdMaterialOverrideApplier.Apply(asset, new[] { material });

                MmdRenderingDescriptor descriptor = CreateDescriptor();
                MmdUrpMaterialBindingDescriptor binding = descriptor.urpMaterialBindings[0];
                binding.alpha = descriptorAlpha;
                binding.isTransparent = false;
                binding.transparencyMode = "unchanged";
                binding.renderOrderBucket = "unchanged";
                MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(asset, descriptor);

                string actualMaterial = ObserveMaterialSurface(material);
                string actualBinding = binding.transparencyMode;
                Assert.That(
                    new[] { actualMaterial, actualBinding },
                    Is.EqualTo(new[] { expectedMaterial, expectedBinding }),
                    "[material, binding] surface outcome");

                float expectedShadowThreshold = hasAlphaClipThreshold ? EntryClipThreshold : DefaultClipThreshold;
                switch (expectedMaterial)
                {
                    case "alphaBlend":
                        Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
                        Assert.That(material.GetFloat(MmdMaterialPropertyNames.AlphaClipThreshold), Is.EqualTo(0.0f));
                        Assert.That(material.GetFloat(MmdMaterialPropertyNames.ShadowAlphaClipThreshold),
                            Is.EqualTo(expectedShadowThreshold).Within(0.00001f));
                        break;
                    case "alphaTest":
                        Assert.That(material.GetFloat(MmdMaterialPropertyNames.AlphaClipThreshold),
                            Is.EqualTo(expectedShadowThreshold).Within(0.00001f));
                        Assert.That(material.GetFloat(MmdMaterialPropertyNames.ShadowAlphaClipThreshold),
                            Is.EqualTo(expectedShadowThreshold).Within(0.00001f));
                        break;
                }

                switch (expectedBinding)
                {
                    case "alphaBlend":
                        Assert.That(binding.isTransparent, Is.True);
                        Assert.That(binding.renderOrderBucket, Is.EqualTo("alphaBlend"));
                        break;
                    case "alphaTest":
                    case "opaque":
                        Assert.That(binding.isTransparent, Is.False);
                        Assert.That(binding.renderOrderBucket, Is.EqualTo("opaque"));
                        break;
                    default:
                        Assert.That(binding.isTransparent, Is.False);
                        Assert.That(binding.renderOrderBucket, Is.EqualTo("unchanged"));
                        break;
                }
            }
            finally
            {
                Destroy(asset, material);
            }
        }

        // Descriptor -> URP binding projection table. Each row applies one entry (one has* flag, or a
        // documented combination) and names every field shared by MmdMaterialDescriptor and
        // MmdUrpMaterialBindingDescriptor that the entry writes. After the apply, those fields must
        // hold the same (normalized) value on both sinks, and every other shared field must keep its
        // sentinel on both sinks. Surface fields that only exist on the binding are covered by
        // SurfaceDecisionTable.
        private static readonly object[][] DescriptorProjectionTable =
        {
            new object[] { new[] { "hasBaseColor" }, new[] { "alpha", "diffuseColor" } },
            new object[] { new[] { "hasAlpha" }, new[] { "alpha" } },
            new object[] { new[] { "hasBaseColor", "hasAlpha" }, new[] { "alpha", "diffuseColor" } },
            new object[] { new[] { "hasAmbientColor" }, new[] { "ambientColor" } },
            new object[] { new[] { "hasEmissionColor" }, new[] { "emissionColor" } },
            new object[] { new[] { "hasEmissionIntensity" }, new[] { "emissionIntensity" } },
            new object[] { new[] { "hasEmissionMap" }, new[] { "usesEmissionMap" } },
            new object[] { new[] { "hasEmissionMask" }, new[] { "usesEmissionMask" } },
            new object[] { new[] { "hasToonBoundary" }, new[] { "toonBoundary" } },
            new object[] { new[] { "hasToonFeather" }, new[] { "toonFeather" } },
            new object[] { new[] { "hasToonBandCount" }, new[] { "toonBandCount" } },
            new object[] { new[] { "hasStylizedSpecularColor" }, new[] { "stylizedSpecularColor" } },
            new object[] { new[] { "hasStylizedSpecularBoundary" }, new[] { "stylizedSpecularBoundary" } },
            new object[] { new[] { "hasStylizedSpecularFeather" }, new[] { "stylizedSpecularFeather" } },
            new object[] { new[] { "hasRimColor" }, new[] { "rimColor" } },
            new object[] { new[] { "hasRimBoundary" }, new[] { "rimBoundary" } },
            new object[] { new[] { "hasRimFeather" }, new[] { "rimFeather" } },
            new object[] { new[] { "hasRimLightFollow" }, new[] { "rimLightFollow" } },
            new object[] { new[] { "hasOutlineColor" }, new[] { "edgeColor" } },
            new object[] { new[] { "hasOutlineWidth" }, new[] { "drawEdgeFlag", "edgeSize" } },
            new object[] { new[] { "hasAlphaClipThreshold" }, Array.Empty<string>() },
            new object[] { new[] { "hasColor" }, Array.Empty<string>() },
            new object[] { new[] { "hasMetallic" }, Array.Empty<string>() },
            new object[] { new[] { "hasMetallicMap" }, Array.Empty<string>() },
            new object[] { new[] { "hasNormalMap" }, Array.Empty<string>() },
            new object[] { new[] { "hasNormalScale" }, Array.Empty<string>() },
            new object[] { new[] { "hasOcclusionMap" }, Array.Empty<string>() },
            new object[] { new[] { "hasOcclusionStrength" }, Array.Empty<string>() },
            new object[] { new[] { "hasRoughnessMap" }, Array.Empty<string>() },
            new object[] { new[] { "hasSmoothness" }, Array.Empty<string>() },
            new object[] { new[] { "hasSurfaceMode" }, Array.Empty<string>() }
        };

        [Test]
        public void DescriptorProjectionTableCoversEveryHasFlag()
        {
            string[] covered = DescriptorProjectionTable
                .SelectMany(row => (string[])row[0])
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            string[] flags = typeof(MmdMaterialOverrideEntry)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => field.FieldType == typeof(bool) && field.Name.StartsWith("has", StringComparison.Ordinal))
                .Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.That(covered, Is.EqualTo(flags));
        }

        [TestCaseSource(nameof(DescriptorProjectionTable))]
        public void DescriptorEntryProjectsWrittenFieldsOntoUrpBinding(string[] flags, string[] expectedWritten)
        {
            MmdMaterialOverrideEntry entry = CreateProjectionEntry(flags);
            MmdMaterialOverrideAsset asset = CreateAsset(entry);
            try
            {
                MmdRenderingDescriptor descriptor = CreateDescriptor();
                MmdMaterialDescriptor material = descriptor.materials[0];
                MmdUrpMaterialBindingDescriptor binding = descriptor.urpMaterialBindings[0];
                FieldInfo[] sharedMaterialFields = SharedDescriptorFields();
                FillProjectionSentinels(material, binding, sharedMaterialFields);
                object?[] materialBefore = sharedMaterialFields.Select(field => CloneValue(field.GetValue(material))).ToArray();
                object?[] bindingBefore = sharedMaterialFields
                    .Select(field => CloneValue(BindingField(field).GetValue(binding)))
                    .ToArray();

                MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(asset, descriptor);

                var materialWritten = new System.Collections.Generic.List<string>();
                var bindingWritten = new System.Collections.Generic.List<string>();
                for (int i = 0; i < sharedMaterialFields.Length; i++)
                {
                    FieldInfo materialField = sharedMaterialFields[i];
                    object? materialAfter = materialField.GetValue(material);
                    object? bindingAfter = BindingField(materialField).GetValue(binding);
                    if (!ValueEquals(materialBefore[i], materialAfter))
                    {
                        materialWritten.Add(materialField.Name);
                    }

                    if (!ValueEquals(bindingBefore[i], bindingAfter))
                    {
                        bindingWritten.Add(materialField.Name);
                        Assert.That(ValueEquals(materialAfter, bindingAfter), Is.True,
                            $"binding.{materialField.Name} must equal the material descriptor value.");
                        if (materialAfter is float[] materialArray)
                        {
                            Assert.That(bindingAfter, Is.Not.SameAs(materialArray),
                                $"binding.{materialField.Name} must not alias the material descriptor array.");
                        }
                    }
                }

                string[] expected = expectedWritten.OrderBy(name => name, StringComparer.Ordinal).ToArray();
                Assert.That(materialWritten.OrderBy(name => name, StringComparer.Ordinal).ToArray(), Is.EqualTo(expected),
                    "material descriptor fields written");
                Assert.That(bindingWritten.OrderBy(name => name, StringComparer.Ordinal).ToArray(), Is.EqualTo(expected),
                    "URP binding fields written");
            }
            finally
            {
                Destroy(asset);
            }
        }

        // Values are chosen so the normalized result differs from every sentinel; several are out
        // of range so the binding has to carry the normalized (not the raw entry) value.
        private static MmdMaterialOverrideEntry CreateProjectionEntry(string[] flags)
        {
            var entry = new MmdMaterialOverrideEntry
            {
                materialIndex = 0,
                baseColor = new Color(1.5f, 0.22f, -0.3f, 0.66f),
                color = new Color(0.44f, 0.55f, 0.66f, 0.88f),
                alpha = 0.42f,
                ambientColor = new Color(0.13f, 2.0f, 0.35f, 1.0f),
                emissionColor = new Color(2.1f, 1.2f, 0.4f, 1.0f),
                emissionIntensity = 12.0f,
                emissionMap = null,
                emissionMask = null,
                toonBoundary = 3.0f,
                toonFeather = 0.12f,
                toonBandCount = 3.0f,
                stylizedSpecularColor = new Color(0.91f, 0.81f, 0.71f, 0.61f),
                stylizedSpecularBoundary = 0.90f,
                stylizedSpecularFeather = -4.0f,
                rimColor = new Color(0.19f, 0.29f, 0.39f, 0.49f),
                rimBoundary = 0.72f,
                rimFeather = 0.08f,
                rimLightFollow = 1.65f,
                outlineColor = new Color(0.72f, 0.61f, 0.5f, 0.39f),
                outlineWidth = 1.75f,
                metallic = 0.73f,
                smoothness = 0.64f,
                normalScale = 0.87f,
                occlusionStrength = 0.58f,
                surfaceMode = MmdMaterialOverrideSurfaceMode.AlphaTest,
                alphaClipThreshold = 0.27f
            };
            foreach (string flag in flags)
            {
                FieldInfo field = typeof(MmdMaterialOverrideEntry).GetField(flag, BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new InvalidOperationException($"Unknown override flag {flag}.");
                field.SetValue(entry, true);
            }

            return entry;
        }

        private static FieldInfo[] SharedDescriptorFields()
        {
            return typeof(MmdMaterialDescriptor)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => typeof(MmdUrpMaterialBindingDescriptor).GetField(field.Name, BindingFlags.Public | BindingFlags.Instance) != null)
                .ToArray();
        }

        private static FieldInfo BindingField(FieldInfo materialField)
        {
            return typeof(MmdUrpMaterialBindingDescriptor).GetField(materialField.Name, BindingFlags.Public | BindingFlags.Instance)!;
        }

        // Material and binding sentinels differ from each other and from every override result, so a
        // field written on one sink only, or copied from the wrong source, is visible.
        private static void FillProjectionSentinels(
            MmdMaterialDescriptor material,
            MmdUrpMaterialBindingDescriptor binding,
            FieldInfo[] sharedMaterialFields)
        {
            for (int i = 0; i < sharedMaterialFields.Length; i++)
            {
                FieldInfo materialField = sharedMaterialFields[i];
                FieldInfo bindingField = BindingField(materialField);
                Type type = materialField.FieldType;
                if (materialField.Name == nameof(MmdMaterialDescriptor.materialIndex) ||
                    materialField.Name == nameof(MmdMaterialDescriptor.name))
                {
                    continue;
                }

                if (type == typeof(float))
                {
                    materialField.SetValue(material, 0.301f + (i * 0.001f));
                    bindingField.SetValue(binding, 0.601f + (i * 0.001f));
                }
                else if (type == typeof(float[]))
                {
                    int length = ((float[])materialField.GetValue(material)!).Length;
                    materialField.SetValue(material, Enumerable.Repeat(0.301f + (i * 0.001f), length).ToArray());
                    bindingField.SetValue(binding, Enumerable.Repeat(0.601f + (i * 0.001f), length).ToArray());
                }
                else if (type == typeof(bool))
                {
                    bool sentinel = materialField.Name != nameof(MmdMaterialDescriptor.drawEdgeFlag);
                    materialField.SetValue(material, sentinel);
                    bindingField.SetValue(binding, sentinel);
                }
                else if (type == typeof(int))
                {
                    materialField.SetValue(material, 100 + i);
                    bindingField.SetValue(binding, 200 + i);
                }
                else if (type == typeof(string))
                {
                    materialField.SetValue(material, "material-" + materialField.Name);
                    bindingField.SetValue(binding, "binding-" + materialField.Name);
                }
                else
                {
                    Assert.Fail($"Unsupported shared descriptor field type {type} on {materialField.Name}.");
                }
            }
        }

        private static object? CloneValue(object? value)
        {
            return value is float[] array ? array.Clone() : value;
        }

        private static bool ValueEquals(object? left, object? right)
        {
            if (left is float[] leftArray && right is float[] rightArray)
            {
                return leftArray.SequenceEqual(rightArray);
            }

            return Equals(left, right);
        }

        private static string ObserveMaterialSurface(Material material)
        {
            float clip = material.GetFloat(MmdMaterialPropertyNames.AlphaClipThreshold);
            if (material.renderQueue == SentinelRenderQueue)
            {
                if (Mathf.Approximately(clip, SentinelClipThreshold))
                {
                    return "unchanged";
                }

                return Mathf.Approximately(clip, EntryClipThreshold) ? "thresholdOnly" : $"unexpected(queue={material.renderQueue}, clip={clip})";
            }

            if (material.renderQueue >= (int)RenderQueue.Transparent)
            {
                return "alphaBlend";
            }

            if (material.renderQueue == (int)RenderQueue.Geometry)
            {
                return clip > 0.0f ? "alphaTest" : "opaque";
            }

            return $"unexpected(queue={material.renderQueue}, clip={clip})";
        }

        private static MmdMaterialOverrideEntry CreateAllFlagsEntry(
            Texture2D? normalMap,
            Texture2D? metallicMap,
            Texture2D? roughnessMap,
            Texture2D? occlusionMap,
            Texture2D? emissionMap = null,
            Texture2D? emissionMask = null)
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
                hasToonBandCount = true,
                toonBandCount = 3.0f,
                hasToonBoundary = true,
                toonBoundary = 0.42f,
                hasToonFeather = true,
                toonFeather = 0.12f,
                hasStylizedSpecularColor = true,
                stylizedSpecularColor = new Color(0.91f, 0.81f, 0.71f, 0.61f),
                hasStylizedSpecularBoundary = true,
                stylizedSpecularBoundary = 0.90f,
                hasStylizedSpecularFeather = true,
                stylizedSpecularFeather = 0.03f,
                hasRimColor = true,
                rimColor = new Color(0.19f, 0.29f, 0.39f, 0.49f),
                hasRimBoundary = true,
                rimBoundary = 0.72f,
                hasRimFeather = true,
                rimFeather = 0.08f,
                hasRimLightFollow = true,
                rimLightFollow = 0.65f,
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
                emissionColor = new Color(2.1f, 1.2f, 0.4f, 1.0f),
                hasEmissionIntensity = true,
                emissionIntensity = 2.5f,
                hasEmissionMap = true,
                emissionMap = emissionMap,
                hasEmissionMask = true,
                emissionMask = emissionMask,
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
