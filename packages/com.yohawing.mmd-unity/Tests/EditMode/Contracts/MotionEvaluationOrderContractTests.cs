#nullable enable

using System.Linq;
using NUnit.Framework;
using Yohawing.MmdUnity.Parser;
using Yohawing.MmdUnity.Tracing;

namespace Yohawing.MmdUnity.Tests.Contracts
{
    public sealed class MotionEvaluationOrderContractTests
    {
        private static readonly string[] PhaseOneCheckpoints =
        {
            MmdTraceCheckpoints.AfterMotionSampling,
            MmdTraceCheckpoints.AfterAppendTransform,
            MmdTraceCheckpoints.AfterIk,
            MmdTraceCheckpoints.AfterMorphEvaluation,
            MmdTraceCheckpoints.FinalWorldUpdate
        };

        [Test]
        public void TraceCheckpointConstantsMatchMotionEvaluationContract()
        {
            Assert.That(MmdTraceCheckpoints.AfterMotionSampling, Is.EqualTo("afterMotionSampling"));
            Assert.That(MmdTraceCheckpoints.AfterAppendTransform, Is.EqualTo("afterAppendTransform"));
            Assert.That(MmdTraceCheckpoints.AfterIk, Is.EqualTo("afterIK"));
            Assert.That(MmdTraceCheckpoints.AfterMorphEvaluation, Is.EqualTo("afterMorphEvaluation"));
            Assert.That(MmdTraceCheckpoints.FinalWorldUpdate, Is.EqualTo("finalWorldUpdate"));
        }

        [Test]
        public void PhaseOneTraceEvaluatorEmitsRequiredCheckpointsInOrder()
        {
            MmdTrace trace = MmdRuntimeTraceEvaluator.EvaluatePhaseOneTrace(
                CreateSingleBoneModel(),
                CreateTranslatedRootMotion(),
                frame: 0,
                time: 0.0f,
                modelId: "minimal.pmx",
                motionId: "minimal.vmd");

            Assert.That(trace.frames, Has.Count.EqualTo(PhaseOneCheckpoints.Length));
            Assert.That(trace.frames.Select(frame => frame.checkpoint), Is.EqualTo(PhaseOneCheckpoints));
            Assert.That(trace.frames.Any(frame => frame.checkpoint == "afterPhysics"), Is.False);
            Assert.That(trace.frames.All(frame => frame.frame == 0), Is.True);
            Assert.That(trace.frames[4].bones[0].worldMatrix[3], Is.EqualTo(1.0f).Within(0.00001f));
        }

        [Test]
        public void PhaseOneTraceFramesEvaluateRequestedFramesInSortedCheckpointGroups()
        {
            MmdTrace trace = MmdRuntimeTraceEvaluator.EvaluatePhaseOneTraceFrames(
                CreateSingleBoneModel(),
                CreateTranslatedRootMotion(),
                new[] { 5, 0 },
                frameRate: 30.0f,
                modelId: "sequence.pmx",
                motionId: "sequence.vmd");

            Assert.That(trace.frames, Has.Count.EqualTo(10));
            Assert.That(trace.frames.Take(5).Select(frame => frame.frame), Is.All.EqualTo(0));
            Assert.That(trace.frames.Take(5).Select(frame => frame.checkpoint), Is.EqualTo(PhaseOneCheckpoints));
            Assert.That(trace.frames.Skip(5).Take(5).Select(frame => frame.frame), Is.All.EqualTo(5));
            Assert.That(trace.frames.Skip(5).Take(5).Select(frame => frame.checkpoint), Is.EqualTo(PhaseOneCheckpoints));
        }

        private static MmdModelDefinition CreateSingleBoneModel()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f }
            });
            return model;
        }

        private static MmdMotionDefinition CreateTranslatedRootMotion()
        {
            var motion = new MmdMotionDefinition();
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = 0,
                translation = new[] { 1.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = LinearInterpolation()
            });
            return motion;
        }

        private static MmdBoneInterpolationDefinition LinearInterpolation()
        {
            byte[] linear = { 20, 20, 107, 107 };
            return new MmdBoneInterpolationDefinition
            {
                translationX = linear,
                translationY = linear,
                translationZ = linear,
                rotation = linear
            };
        }
    }
}
