#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mmd.Tests
{
    public sealed class MmdToonLitShaderGUITests
    {
        [Test]
        public void SectionsMatchTheUrpLitLayout()
        {
            string[] expected =
            {
                "Surface Options",
                "Surface Inputs",
                "Toon Map",
                "Normal Map",
                "Sphere / MatCap",
                "Toon Lighting",
                "Stylized Specular",
                "Rim Light",
                "Emission",
                "Outline",
                "Advanced Options",
            };

            Assert.That(Mmd.Editor.MmdToonLitShaderGUIState.Sections.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(Mmd.Editor.MmdToonLitShaderGUIState.Sections[i].DisplayName, Is.EqualTo(expected[i]));
            }
        }

        [Test]
        public void SurfaceOptionsAndInputsAreTheOnlyDefaultOpenSections()
        {
            bool[] expanded = Mmd.Editor.MmdToonLitShaderGUIState.CreateDefaultExpandedState();

            Assert.That(expanded[(int)Mmd.Editor.MmdToonLitInspectorSection.SurfaceOptions], Is.True);
            Assert.That(expanded[(int)Mmd.Editor.MmdToonLitInspectorSection.SurfaceInputs], Is.True);
            for (int i = 0; i < expanded.Length; i++)
            {
                if (i == (int)Mmd.Editor.MmdToonLitInspectorSection.SurfaceOptions ||
                    i == (int)Mmd.Editor.MmdToonLitInspectorSection.SurfaceInputs)
                {
                    continue;
                }

                Assert.That(expanded[i], Is.False, $"Section index {i} must start closed.");
            }
        }

        [Test]
        public void PublicAndLegacyShaderNamesMapToFriendlyProfileNames()
        {
            Shader? publicToon = Shader.Find("MMD URP Toon");
            Shader? legacyToon = Shader.Find("MMD Toon Lit");
            Shader? publicBasic = Shader.Find("MMD Basic Toon");
            Shader? legacyBasic = Shader.Find("MMD Basic URP Toon");

            Assert.That(Mmd.Editor.MmdToonLitShaderGUIState.TryGetProfile(publicToon, out var publicToonProfile), Is.True);
            Assert.That(Mmd.Editor.MmdToonLitShaderGUIState.TryGetProfile(legacyToon, out var legacyToonProfile), Is.True);
            Assert.That(Mmd.Editor.MmdToonLitShaderGUIState.TryGetProfile(publicBasic, out var publicBasicProfile), Is.True);
            Assert.That(Mmd.Editor.MmdToonLitShaderGUIState.TryGetProfile(legacyBasic, out var legacyBasicProfile), Is.True);
            Assert.That(
                Mmd.Editor.MmdToonLitShaderGUIState.GetDisplayName(publicToonProfile),
                Is.EqualTo("MMD URP Toon"));
            Assert.That(
                Mmd.Editor.MmdToonLitShaderGUIState.GetDisplayName(legacyToonProfile),
                Is.EqualTo("MMD URP Toon"));
            Assert.That(
                Mmd.Editor.MmdToonLitShaderGUIState.GetDisplayName(publicBasicProfile),
                Is.EqualTo("MMD Basic Toon"));
            Assert.That(
                Mmd.Editor.MmdToonLitShaderGUIState.GetDisplayName(legacyBasicProfile),
                Is.EqualTo("MMD Basic Toon"));
        }

        [Test]
        public void ProfileSupportHidesMmdOnlyFeatureSectionsForBasicToon()
        {
            Assert.That(
                Mmd.Editor.MmdToonLitShaderGUIState.SupportsSection(
                    Mmd.Editor.MmdToonInspectorProfile.BasicToon,
                    Mmd.Editor.MmdToonLitInspectorSection.StylizedSpecular),
                Is.False);
            Assert.That(
                Mmd.Editor.MmdToonLitShaderGUIState.SupportsSection(
                    Mmd.Editor.MmdToonInspectorProfile.BasicToon,
                    Mmd.Editor.MmdToonLitInspectorSection.RimLight),
                Is.False);
            Assert.That(
                Mmd.Editor.MmdToonLitShaderGUIState.SupportsSection(
                    Mmd.Editor.MmdToonInspectorProfile.BasicToon,
                    Mmd.Editor.MmdToonLitInspectorSection.Emission),
                Is.False);
            Assert.That(
                Mmd.Editor.MmdToonLitShaderGUIState.SupportsSection(
                    Mmd.Editor.MmdToonInspectorProfile.BasicToon,
                    Mmd.Editor.MmdToonLitInspectorSection.Outline),
                Is.True);
            Assert.That(
                Mmd.Editor.MmdToonLitShaderGUIState.SupportsSection(
                    Mmd.Editor.MmdToonInspectorProfile.MmdToon,
                    Mmd.Editor.MmdToonLitInspectorSection.Emission),
                Is.True);
        }

        [Test]
        public void SurfaceTypeRecognizesOpaqueCutoutTransparentAndMixedSelections()
        {
            Material opaque = CreateMaterial("MMD Basic Toon");
            Material cutout = CreateMaterial("MMD URP Toon");
            try
            {
                cutout.SetFloat("_AlphaClipThreshold", 0.35f);
                Assert.That(Mmd.Editor.MmdToonMaterialStateSync.GetSurfaceType(opaque),
                    Is.EqualTo(Mmd.Editor.MmdToonSurfaceType.Opaque));
                Assert.That(Mmd.Editor.MmdToonMaterialStateSync.GetSurfaceType(cutout),
                    Is.EqualTo(Mmd.Editor.MmdToonSurfaceType.Cutout));

                cutout.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                Assert.That(Mmd.Editor.MmdToonMaterialStateSync.GetSurfaceType(cutout),
                    Is.EqualTo(Mmd.Editor.MmdToonSurfaceType.Transparent));
                Mmd.Editor.MmdToonMaterialStateSync.GetSurfaceType(new[] { opaque, cutout }, out bool mixed);
                Assert.That(mixed, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(opaque);
                Object.DestroyImmediate(cutout);
            }
        }

        [Test]
        public void SurfaceTypeRoundTripPreservesCutoutThresholdsAndManualQueue()
        {
            Material material = CreateMaterial("MMD URP Toon");
            try
            {
                material.SetFloat("_AlphaClipThreshold", 0.35f);
                material.SetFloat("_ShadowAlphaClipThreshold", 0.21f);
                material.renderQueue = 2100;

                Mmd.Editor.MmdToonMaterialStateSync.ApplySurfaceType(
                    new[] { material }, Mmd.Editor.MmdToonSurfaceType.Opaque);
                Assert.That(material.GetFloat("_AlphaClipThreshold"), Is.Zero);
                Assert.That(material.GetFloat("_ShadowAlphaClipThreshold"), Is.Zero);
                Assert.That(material.renderQueue, Is.EqualTo(2100));

                Mmd.Editor.MmdToonMaterialStateSync.ApplySurfaceType(
                    new[] { material }, Mmd.Editor.MmdToonSurfaceType.Cutout);
                Assert.That(material.GetFloat("_AlphaClipThreshold"), Is.EqualTo(0.35f).Within(0.00001f));
                Assert.That(material.GetFloat("_ShadowAlphaClipThreshold"), Is.EqualTo(0.21f).Within(0.00001f));
                Assert.That(material.renderQueue, Is.EqualTo(2100));

                Mmd.Editor.MmdToonMaterialStateSync.ApplySurfaceType(
                    new[] { material }, Mmd.Editor.MmdToonSurfaceType.Transparent);
                Assert.That(material.GetFloat("_AlphaClipThreshold"), Is.Zero);
                Assert.That(material.GetFloat("_ShadowAlphaClipThreshold"), Is.EqualTo(0.21f).Within(0.00001f));
                Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
                Assert.That(material.renderQueue, Is.EqualTo(2100));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SurfaceTypeMovesOnlyDefaultRenderQueuesAndKeepsMmdCutoutInGeometry()
        {
            Material material = CreateMaterial("MMD URP Toon");
            try
            {
                Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Geometry));

                Mmd.Editor.MmdToonMaterialStateSync.ApplySurfaceType(
                    new[] { material }, Mmd.Editor.MmdToonSurfaceType.Cutout);
                Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Geometry));

                Mmd.Editor.MmdToonMaterialStateSync.ApplySurfaceType(
                    new[] { material }, Mmd.Editor.MmdToonSurfaceType.Transparent);
                Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));

                Mmd.Editor.MmdToonMaterialStateSync.ApplySurfaceType(
                    new[] { material }, Mmd.Editor.MmdToonSurfaceType.Opaque);
                Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Geometry));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void FeatureSentinelsAndDirectFlagsRoundTripWithoutLosingValues()
        {
            Material material = CreateMaterial("MMD URP Toon");
            Mmd.Editor.MmdToonFeature[] features =
            {
                Mmd.Editor.MmdToonFeature.ToonMap,
                Mmd.Editor.MmdToonFeature.NormalMap,
                Mmd.Editor.MmdToonFeature.SphereMatCap,
                Mmd.Editor.MmdToonFeature.StylizedSpecular,
                Mmd.Editor.MmdToonFeature.RimLight,
                Mmd.Editor.MmdToonFeature.Emission,
                Mmd.Editor.MmdToonFeature.Outline,
            };
            try
            {
                material.SetFloat("_ToonMapBound", 1.0f);
                material.SetFloat("_MmdNormalMapBound", 1.0f);
                material.SetFloat("_SphereMode", 2.0f);
                material.SetFloat("_StylizedSpecularBoundary", 0.72f);
                material.SetFloat("_RimBoundary", 0.28f);
                material.SetFloat("_MmdEmissionIntensity", 2.5f);
                material.SetFloat("_OutlineVisible", 1.0f);

                for (int i = 0; i < features.Length; i++)
                {
                    Mmd.Editor.MmdToonMaterialStateSync.SetFeatureEnabled(
                        new[] { material }, features[i], false);
                    Assert.That(
                        Mmd.Editor.MmdToonMaterialStateSync.GetFeatureState(new[] { material }, features[i]),
                        Is.EqualTo(Mmd.Editor.MmdToonFeatureState.Off));
                    Mmd.Editor.MmdToonMaterialStateSync.SetFeatureEnabled(
                        new[] { material }, features[i], true);
                    Assert.That(
                        Mmd.Editor.MmdToonMaterialStateSync.GetFeatureState(new[] { material }, features[i]),
                        Is.EqualTo(Mmd.Editor.MmdToonFeatureState.On));
                }

                Assert.That(material.GetFloat("_SphereMode"), Is.EqualTo(2.0f).Within(0.00001f));
                Assert.That(material.GetFloat("_StylizedSpecularBoundary"), Is.EqualTo(0.72f).Within(0.00001f));
                Assert.That(material.GetFloat("_RimBoundary"), Is.EqualTo(0.28f).Within(0.00001f));
                Assert.That(material.GetFloat("_MmdEmissionIntensity"), Is.EqualTo(2.5f).Within(0.00001f));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void FeatureMixedSelectionCanBeEnabledTogetherAndUndoRestoresPreviousState()
        {
            Material first = CreateMaterial("MMD URP Toon");
            Material second = CreateMaterial("MMD URP Toon");
            try
            {
                first.SetFloat("_RimBoundary", 0.63f);
                second.SetFloat("_RimBoundary", -1.0f);
                Material[] materials = { first, second };

                Assert.That(
                    Mmd.Editor.MmdToonMaterialStateSync.GetFeatureState(
                        materials, Mmd.Editor.MmdToonFeature.RimLight),
                    Is.EqualTo(Mmd.Editor.MmdToonFeatureState.Mixed));

                Mmd.Editor.MmdToonMaterialStateSync.SetFeatureEnabled(
                    materials, Mmd.Editor.MmdToonFeature.RimLight, true);
                Assert.That(first.GetFloat("_RimBoundary"), Is.EqualTo(0.63f).Within(0.00001f));
                Assert.That(second.GetFloat("_RimBoundary"), Is.EqualTo(0.5f).Within(0.00001f));

                Undo.PerformUndo();
                Assert.That(first.GetFloat("_RimBoundary"), Is.EqualTo(0.63f).Within(0.00001f));
                Assert.That(second.GetFloat("_RimBoundary"), Is.EqualTo(-1.0f).Within(0.00001f));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        private static Material CreateMaterial(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null, $"Shader '{shaderName}' must be available for the Inspector test.");
            return new Material(shader);
        }
    }
}
