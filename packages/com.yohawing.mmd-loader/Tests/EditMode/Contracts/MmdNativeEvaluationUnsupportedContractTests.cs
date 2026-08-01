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
        public void SourceBackedPhaseOneFrameRejectsSuppliedPhysicsBackend()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadCubeFixturePair();

            NotSupportedException exception = Assert.Throws<NotSupportedException>(
                () => MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrame(
                    model,
                    motion,
                    frame: 0,
                    time: 0.0f,
                    physicsBackend: new NullMmdPhysicsBackend()))!;

            Assert.That(exception.Message, Does.Contain("source-backed PMX/VMD"));
            Assert.That(exception.Message, Does.Contain("custom physics backend or IK solver"));
            Assert.That(exception.Message, Does.Contain("native evaluation is required"));
        }

        [Test]
        public void SourceBackedPhaseOneFrameRejectsSuppliedIkSolver()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadCubeFixturePair();

            NotSupportedException exception = Assert.Throws<NotSupportedException>(
                () => MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrame(
                    model,
                    motion,
                    frame: 0,
                    time: 0.0f,
                    ikSolver: new PassthroughIkSolver()))!;

            Assert.That(exception.Message, Does.Contain("source-backed PMX/VMD"));
            Assert.That(exception.Message, Does.Contain("custom physics backend or IK solver"));
            Assert.That(exception.Message, Does.Contain("native evaluation is required"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MixedSourcePhaseOneFrameRejectsManagedFallback(bool modelRetainsSourceBytes)
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadCubeFixturePair();
            if (modelRetainsSourceBytes)
            {
                motion.sourceBytes = null;
            }
            else
            {
                model.sourceBytes = null;
            }

            NotSupportedException exception = Assert.Throws<NotSupportedException>(
                () => MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrame(
                    model,
                    motion,
                    frame: 0,
                    time: 0.0f))!;

            Assert.That(exception.Message, Does.Contain("source-less-only"));
            Assert.That(exception.Message, Does.Contain("mixed-source"));
            Assert.That(exception.Message, Does.Contain("native evaluation is required"));
        }

        [Test]
        public void SourceBackedBeforePhysicsRejectsDeformAfterPhysicsManagedFallback()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadCubeFixturePair();
            model.bones[0].deformAfterPhysics = true;

            Assert.That(model.HasDeformAfterPhysicsBones, Is.True,
                "the source-backed model must exercise the deform-after-physics contract.");

            NotSupportedException exception = Assert.Throws<NotSupportedException>(
                () => MmdRuntimeFrameEvaluator.EvaluateValidatedBeforePhysicsPlaybackFrame(
                    model,
                    motion,
                    frame: 0,
                    time: 0.0f))!;

            Assert.That(exception.Message, Does.Contain("source-backed PMX/VMD"));
            Assert.That(exception.Message, Does.Contain("before-physics"));
            Assert.That(exception.Message, Does.Contain("deform-after-physics bones"));
            Assert.That(exception.Message, Does.Contain("native evaluation is required"));
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

        private sealed class PassthroughIkSolver : IMmdIkSolver
        {
            public string Name => nameof(PassthroughIkSolver);

            public MmdSampledMotion Solve(MmdModelDefinition model, MmdSampledMotion? sampledMotion)
            {
                return sampledMotion ?? new MmdSampledMotion();
            }
        }
    }
}
