#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Mmd.Tests
{
    public sealed class MmdToonLitShaderGUITests
    {
        [Test]
        public void SectionsExposeRequiredAuthoringGroups()
        {
            string[] expected =
            {
                "Surface Rendering",
                "Surface Maps",
                "Toon Lighting",
                "Shadow / MMD Self Shadow",
                "Normal",
                "Sphere / MatCap",
                "Stylized Specular",
                "Rim Light",
                "Emission",
                "Outline",
                "Advanced / Diagnostics",
            };

            Assert.That(Mmd.Editor.MmdToonLitShaderGUIState.Sections.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(Mmd.Editor.MmdToonLitShaderGUIState.Sections[i].DisplayName, Is.EqualTo(expected[i]));
            }
        }

        [Test]
        public void SurfaceRenderingAndToonLightingAreTheOnlyDefaultOpenSections()
        {
            bool[] expanded = Mmd.Editor.MmdToonLitShaderGUIState.CreateDefaultExpandedState();

            Assert.That(expanded[(int)Mmd.Editor.MmdToonLitInspectorSection.SurfaceRendering], Is.True);
            Assert.That(expanded[(int)Mmd.Editor.MmdToonLitInspectorSection.ToonLighting], Is.True);
            for (int i = 0; i < expanded.Length; i++)
            {
                if (i == (int)Mmd.Editor.MmdToonLitInspectorSection.SurfaceRendering ||
                    i == (int)Mmd.Editor.MmdToonLitInspectorSection.ToonLighting)
                {
                    continue;
                }

                Assert.That(expanded[i], Is.False, $"Section index {i} must start closed.");
            }
        }

        [Test]
        public void InactiveFeatureDetailsCanBeHiddenWithoutBlockingMixedSelections()
        {
            Assert.That(
                Mmd.Editor.MmdToonLitShaderGUIState.ShouldDrawFeatureDetails(Mmd.Editor.MmdToonLitSectionState.Off),
                Is.False);
            Assert.That(
                Mmd.Editor.MmdToonLitShaderGUIState.ShouldDrawFeatureDetails(Mmd.Editor.MmdToonLitSectionState.On),
                Is.True);
            Assert.That(
                Mmd.Editor.MmdToonLitShaderGUIState.ShouldDrawFeatureDetails(Mmd.Editor.MmdToonLitSectionState.Mixed),
                Is.True);
        }

        [Test]
        public void MmdToonLitShaderNameIsExplicitAndDoesNotClaimLegacyShader()
        {
            Assert.That(Mmd.Editor.MmdToonLitShaderGUIState.ShaderName, Is.EqualTo("MMD Toon Lit"));

            Shader? toonLit = Shader.Find("MMD Toon Lit");
            Shader? legacy = Shader.Find("MMD Basic URP Toon");
            Assert.That(Mmd.Editor.MmdToonLitShaderGUIState.IsMmdToonLitShader(toonLit), Is.True);
            Assert.That(Mmd.Editor.MmdToonLitShaderGUIState.IsMmdToonLitShader(legacy), Is.False);
        }
    }
}
