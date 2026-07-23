#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Mmd.Tests
{
    [SetUpFixture]
    internal sealed class MmdEditModePipelineSetup
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
