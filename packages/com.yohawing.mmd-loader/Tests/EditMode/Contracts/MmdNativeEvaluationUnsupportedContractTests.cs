#nullable enable

using System;
using System.Collections.Generic;
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
        public void SourceBackedRuntimeSessionUsesNativeBeforePhysicsWithPostPhysicsBones()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadCubeFixturePair();
            model.bones[0].deformAfterPhysics = true;
            using var session = new MmdRuntimeSession(
                model,
                motion,
                "test_1bone_cube.pmx",
                "test_1bone_cube_motion.vmd");

            MmdEvaluatedFrame frame = session.EvaluateBeforePhysicsFrame(frame: 0, time: 0.0f);

            Assert.That(frame.bones, Is.Not.Empty);
        }

        [Test]
        public void SourceBackedBeforePhysicsPreservesFrameAndTimeValidation()
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
        public void RuntimeSessionValidatesModelAtConstruction()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadCubeFixturePair();
            model.bones[0].parentIndex = model.bones[0].index;

            Assert.Throws<InvalidOperationException>(
                () => new MmdRuntimeSession(
                    model,
                    motion,
                    "test_1bone_cube.pmx",
                    "test_1bone_cube_motion.vmd"));
        }

        [Test]
        public void SourceBackedBeforePhysicsSteadyFramesDoNotRevalidateMutableInputs()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadCubeFixturePair();
            model.bones[0].deformAfterPhysics = true;
            using var session = new MmdRuntimeSession(
                model,
                motion,
                "test_1bone_cube.pmx",
                "test_1bone_cube_motion.vmd");

            MmdEvaluatedFrame initialFrame = session.EvaluateBeforePhysicsFrame(frame: 0, time: 0.0f);
            Assert.That(initialFrame.bones, Is.Not.Empty);

            model.bones[0].parentIndex = model.bones[0].index;
            motion.maxFrame = -1;

            for (int i = 0; i < 3; i++)
            {
                MmdEvaluatedFrame steadyFrame = session.EvaluateBeforePhysicsFrame(frame: 0, time: 0.0f);
                Assert.That(steadyFrame.bones, Is.Not.Empty);
            }
        }

        [Test]
        public void InPlaceNativeFrameBuilderPreservesLegacyPoseBitsAndFrameIdentity()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadAppendFixturePair();
            using var session = new MmdRuntimeSession(model, motion, "test_append_bone.pmx", "test_append_bone.vmd");
            session.GetNativeOutputBufferLengths(out int worldMatrixFloatCount, out int morphWeightCount, out int ikEnabledCount);
            var nativeWorldMatrices = new float[worldMatrixFloatCount];
            var nativeMorphWeights = new float[morphWeightCount];
            var nativeIkEnabled = new byte[ikEnabledCount];
            Assert.Throws<ArgumentException>(() => session.EvaluateBeforePhysicsFrameInto(0, 0.0f,
                new float[nativeWorldMatrices.Length - 1], nativeMorphWeights, nativeIkEnabled));
            session.EvaluateBeforePhysicsFrameInto(0, 0.0f, nativeWorldMatrices, nativeMorphWeights, nativeIkEnabled);
            MmdEvaluatedFrame legacyFrame = MmdRuntimeFrameEvaluator.BuildFrameFromNative(
                model, 0, 0.0f, nativeWorldMatrices, nativeMorphWeights, includeMaterials: false);
            var orderedBones = new List<MmdBoneDefinition>(model.bones);
            orderedBones.Sort((left, right) => left.index.CompareTo(right.index));
            var morphEntries = Array.Empty<MmdEvaluatedMorphWeight>();
            var morphOrder = Array.Empty<int>();
            var reusableFrame = new MmdEvaluatedFrame();
            MmdEvaluatedFrame inPlaceFrame = MmdRuntimeFrameEvaluator.BuildFrameFromNativeInPlace(
                model, 0, 0.0f, nativeWorldMatrices, nativeMorphWeights, reusableFrame,
                new float[orderedBones.Count * 16], new float[16], orderedBones, morphEntries, morphOrder, false);

            Assert.That(inPlaceFrame, Is.SameAs(reusableFrame));
            Assert.That(inPlaceFrame.bones.Count, Is.EqualTo(legacyFrame.bones.Count));
            for (int i = 0; i < legacyFrame.bones.Count; i++)
            {
                MmdEvaluatedBonePose expected = legacyFrame.bones[i];
                MmdEvaluatedBonePose actual = inPlaceFrame.bones[i];
                AssertFloatBitsEqual(expected.localPosition, actual.localPosition);
                AssertFloatBitsEqual(expected.localRotation, actual.localRotation);
                AssertFloatBitsEqual(expected.worldMatrix, actual.worldMatrix);
            }
            MmdEvaluatedFrame secondFrame = MmdRuntimeFrameEvaluator.BuildFrameFromNativeInPlace(
                model, 1, 1.0f / 30.0f, nativeWorldMatrices, nativeMorphWeights, reusableFrame,
                new float[orderedBones.Count * 16], new float[16], orderedBones, morphEntries, morphOrder, false);
            Assert.That(secondFrame, Is.SameAs(reusableFrame));
        }

        private static void AssertFloatBitsEqual(float[] expected, float[] actual)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
                Assert.That(BitConverter.SingleToInt32Bits(actual[i]),
                    Is.EqualTo(BitConverter.SingleToInt32Bits(expected[i])));
        }

        private static (MmdModelDefinition Model, MmdMotionDefinition Motion) LoadAppendFixturePair()
        {
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(
                MmdTestFixtures.ReadFixtureAssetBytes("test_append_bone.pmx"));
            MmdMotionDefinition motion = parser.LoadMotion(
                MmdTestFixtures.ReadFixtureAssetBytes("test_append_bone.vmd"));
            return (model, motion);
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
