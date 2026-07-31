#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Mmd.Samples.UnityToonShader.Tests
{
    [SetUpFixture]
    internal sealed class UnityToonShaderAdapterTestSetup
    {
        private string? previousShaderRenderPipeline;

        [OneTimeSetUp]
        public void SetUp()
        {
            previousShaderRenderPipeline = Shader.globalRenderPipeline;
            Shader.globalRenderPipeline = "UniversalPipeline";
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            Shader.globalRenderPipeline = previousShaderRenderPipeline ?? string.Empty;
        }
    }
}
