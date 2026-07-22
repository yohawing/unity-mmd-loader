#nullable enable

using NUnit.Framework;
using UnityEngine;

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
                "Detail Inputs",
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
    }
}
