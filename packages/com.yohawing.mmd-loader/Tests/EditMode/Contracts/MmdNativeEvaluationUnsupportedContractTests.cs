#nullable enable

using System;
using NUnit.Framework;
using Mmd.Parser;

namespace Mmd.Tests.Contracts
{
    [TestFixture]
    public sealed class MmdNativeEvaluationUnsupportedContractTests
    {
        [Test]
        public void SourceLessPhaseOneFrameRequiresNativeSourceBytes()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadCubeFixturePair();
            model.sourceBytes = null;
            motion.sourceBytes = null;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrame(
                    model,
                    motion,
                    frame: 0,
                    time: 0.0f))!;

            Assert.That(exception.Message, Does.Contain("Model sourceBytes are required for native evaluation"));
        }

        [Test]
        public void SourceBackedRuntimeSessionUsesNativeBeforePhysicsWhenNoPostPhysicsBones()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadCubeFixturePair();
            using var session = new MmdRuntimeSession(
                model,
                motion,
                "test_1bone_cube.pmx",
                "test_1bone_cube_motion.vmd");

            MmdEvaluatedFrame frame = session.EvaluateBeforePhysicsFrame(frame: 0, time: 0.0f);

            Assert.That(frame.bones, Is.Not.Empty);
        }

        [Test]
        public void SourceBackedRuntimeSessionRejectsDeformAfterPhysicsBeforePhysicsNativeBoundary()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadCubeFixturePair();
            model.bones[0].deformAfterPhysics = true;
            using var session = new MmdRuntimeSession(
                model,
                motion,
                "test_1bone_cube.pmx",
                "test_1bone_cube_motion.vmd");

            NotSupportedException exception = Assert.Throws<NotSupportedException>(
                () => session.EvaluateBeforePhysicsFrame(frame: 0, time: 0.0f))!;

            Assert.That(exception.Message, Does.Contain("source-backed PMX/VMD"));
            Assert.That(exception.Message, Does.Contain("before-physics"));
            Assert.That(exception.Message, Does.Contain("deform-after-physics bones"));
        }

        [Test]
        public void SourceBackedUnsupportedBeforePhysicsPreservesFrameAndTimeValidation()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadCubeFixturePair();
            model.bones[0].deformAfterPhysics = true;
            using var session = new MmdRuntimeSession(
                model,
                motion,
                "test_1bone_cube.pmx",
                "test_1bone_cube_motion.vmd");

            Assert.Throws<ArgumentOutOfRangeException>(
                () => session.EvaluateBeforePhysicsFrame(frame: -1, time: 0.0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => session.EvaluateBeforePhysicsFrame(frame: 0, time: -1.0f));
        }

        [Test]
        public void SourceBackedUnsupportedBeforePhysicsRevalidatesMutableTopology()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadCubeFixturePair();
            using var session = new MmdRuntimeSession(
                model,
                motion,
                "test_1bone_cube.pmx",
                "test_1bone_cube_motion.vmd");

            _ = session.TopologyPlan;
            model.bones[0].deformAfterPhysics = true;
            model.bones[0].parentIndex = model.bones[0].index;

            Assert.Throws<InvalidOperationException>(
                () => session.EvaluateBeforePhysicsFrame(frame: 0, time: 0.0f));
        }

        [Test]
        public void SourceBackedUnsupportedBeforePhysicsRevalidatesMutableMotion()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadCubeFixturePair();
            using var session = new MmdRuntimeSession(
                model,
                motion,
                "test_1bone_cube.pmx",
                "test_1bone_cube_motion.vmd");

            model.bones[0].deformAfterPhysics = true;
            motion.maxFrame = -1;

            Assert.Throws<InvalidOperationException>(
                () => session.EvaluateBeforePhysicsFrame(frame: 0, time: 0.0f));
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
