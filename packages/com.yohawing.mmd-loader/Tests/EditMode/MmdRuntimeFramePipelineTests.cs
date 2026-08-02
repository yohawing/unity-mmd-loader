#nullable enable

using Mmd.Motion;
using Mmd.Parser;
using Mmd.Physics;
using NUnit.Framework;

namespace Mmd.Tests
{
    public sealed class MmdRuntimeFramePipelineTests
    {
        [Test]
        public void NormalEvaluationOnlyMaterializesFinalWorldMatrices()
        {
            MmdRuntimeFrameEvaluation evaluation = Evaluate(captureCheckpoints: false);

            Assert.That(evaluation.SampledWorldMatrices, Is.Null);
            Assert.That(evaluation.AppendedWorldMatrices, Is.Null);
            Assert.That(evaluation.IkWorldMatrices, Is.Null);
            Assert.That(evaluation.WorldMatrices, Is.Not.Empty);
        }

        [Test]
        public void TraceEvaluationMaterializesEveryCheckpoint()
        {
            MmdRuntimeFrameEvaluation evaluation = Evaluate(captureCheckpoints: true);

            Assert.That(evaluation.SampledWorldMatrices, Is.Not.Empty);
            Assert.That(evaluation.AppendedWorldMatrices, Is.Not.Empty);
            Assert.That(evaluation.IkWorldMatrices, Is.Not.Empty);
            Assert.That(evaluation.WorldMatrices, Is.Not.Empty);
        }

        [Test]
        public void BeforePhysicsLeavesDeformAfterPhysicsAppendForFinalPass()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f }
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1,
                name = "append",
                parentIndex = 0,
                origin = new[] { 0.0f, 0.0f, 0.0f },
                appendParentIndex = 0,
                appendRatio = 1.0f,
                appendTranslation = true,
                deformAfterPhysics = true
            });
            var motion = new MmdMotionDefinition();
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = 0,
                translation = new[] { 1.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f }
            });

            MmdRuntimeFrameEvaluation before = MmdRuntimeFramePipeline.EvaluateWithOptions(
                model, motion, frame: 0, physicsBackend: new NullMmdPhysicsBackend(), stopBeforePhysics: true);
            MmdRuntimeFrameEvaluation final = MmdRuntimeFramePipeline.EvaluateWithOptions(
                model, motion, frame: 0, physicsBackend: new NullMmdPhysicsBackend());

            Assert.That(before.WorldMatrices, Does.ContainKey(1));
            Assert.That(final.WorldMatrices, Does.ContainKey(1));
            Assert.That(before.WorldMatrices[1][3], Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(final.WorldMatrices[1][3], Is.EqualTo(2.0f).Within(0.00001f));
        }

        private static MmdRuntimeFrameEvaluation Evaluate(bool captureCheckpoints)
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadFixture();

            return MmdRuntimeFramePipeline.EvaluateWithOptions(
                model,
                motion,
                frame: 0,
                physicsBackend: new NullMmdPhysicsBackend(),
                ikSolver: new MmdIkSolver(),
                captureCheckpoints: captureCheckpoints);
        }

        private static (MmdModelDefinition Model, MmdMotionDefinition Motion) LoadFixture()
        {
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(
                MmdTestFixtures.ReadFixtureAssetBytes("test_append_bone.pmx"));
            MmdMotionDefinition motion = parser.LoadMotion(
                MmdTestFixtures.ReadFixtureAssetBytes("test_append_bone.vmd"));
            model.sourceBytes = null;
            motion.sourceBytes = null;
            return (
                model,
                motion);
        }
    }
}
