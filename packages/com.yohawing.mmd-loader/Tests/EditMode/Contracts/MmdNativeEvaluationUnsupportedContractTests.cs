#nullable enable

using System;
using NUnit.Framework;
using Mmd.Motion;
using Mmd.Parser;
using Mmd.Physics;

namespace Mmd.Tests.Contracts
{
    [TestFixture]
    public sealed class MmdNativeEvaluationUnsupportedContractTests
    {
        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public void NonSourceLessPhaseOneFrameRejectsManagedFallback(bool mixedSource)
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadCubeFixturePair();
            IMmdPhysicsBackend? physicsBackend = null;
            if (mixedSource)
            {
                motion.sourceBytes = null;
            }
            else
            {
                physicsBackend = new NullMmdPhysicsBackend();
            }

            NotSupportedException exception = Assert.Throws<NotSupportedException>(
                () => MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrame(
                    model,
                    motion,
                    frame: 0,
                    time: 0.0f,
                    physicsBackend: physicsBackend))!;

            Assert.That(exception.Message, Does.Contain("Managed fallback evaluation"));
            Assert.That(exception.Message, Does.Contain("native evaluation is required"));
            Assert.That(exception.Message, Does.Contain("source-backed or mixed-source PMX/VMD"));
        }

        private static (MmdModelDefinition Model, MmdMotionDefinition Motion) LoadCubeFixturePair()
        {
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(
                MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx"));
            MmdMotionDefinition motion = parser.LoadMotion(
                MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd"));
            return (model, motion);
        }

    }
}
