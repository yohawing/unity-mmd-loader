#nullable enable

using System;
using NUnit.Framework;
using Mmd.Native;

namespace Mmd.Tests.Contracts
{
    [TestFixture]
    public sealed class MmdRuntimeFfiHostPoseSessionTests
    {
        [Test]
        public void SemiBasicBoneHostPoseRoundTripIsDeterministicWithoutPhysicsWorld()
        {
#if !UNITY_EDITOR_WIN
            Assert.Ignore("The distributed mmd-runtime host-pose gate is Windows Editor only.");
#endif
            using MmdRuntimeFfiHostPoseSession session = CreateSession();

            Assert.That(session.BoneCount, Is.GreaterThan(0));
            Assert.That(session.MorphCount, Is.EqualTo(0));
            Assert.That(session.IkCount, Is.GreaterThan(0));
            Assert.That(session.WorldMatrixFloatCount, Is.EqualTo(session.BoneCount * 16));

            var localPositionOffsetsXyz = new float[session.BoneCount * 3];
            var localRotationXyzw = new float[session.BoneCount * 4];
            var localScalesXyz = new float[session.BoneCount * 3];
            var morphWeights = new float[session.MorphCount];
            var ikEnabled = new byte[session.IkCount];
            for (int boneIndex = 0; boneIndex < session.BoneCount; boneIndex++)
            {
                localRotationXyzw[boneIndex * 4 + 3] = 1.0f;
                localScalesXyz[boneIndex * 3] = 1.0f;
                localScalesXyz[boneIndex * 3 + 1] = 1.0f;
                localScalesXyz[boneIndex * 3 + 2] = 1.0f;
            }
            for (int ikIndex = 0; ikIndex < ikEnabled.Length; ikIndex++)
            {
                ikEnabled[ikIndex] = 1;
            }

            var firstWorldMatrices = new float[session.WorldMatrixFloatCount];
            var secondWorldMatrices = new float[session.WorldMatrixFloatCount];
            session.EvaluateAndCopy(
                localPositionOffsetsXyz,
                localRotationXyzw,
                localScalesXyz,
                morphWeights,
                ikEnabled,
                firstWorldMatrices);
            session.EvaluateAndCopy(
                localPositionOffsetsXyz,
                localRotationXyzw,
                localScalesXyz,
                morphWeights,
                ikEnabled,
                secondWorldMatrices);

            for (int index = 0; index < firstWorldMatrices.Length; index++)
            {
                Assert.That(float.IsFinite(firstWorldMatrices[index]), Is.True,
                    "world matrix output must be finite at index " + index);
            }

            CollectionAssert.AreEqual(firstWorldMatrices, secondWorldMatrices,
                "repeating the identical physics-off host pose must not drift");

            var changedPositions = (float[])localPositionOffsetsXyz.Clone();
            var changedRotations = (float[])localRotationXyzw.Clone();
            changedPositions[0] = 0.25f;
            changedRotations[1] = (float)Math.Sin(0.25f);
            changedRotations[3] = (float)Math.Cos(0.25f);
            var changedWorldMatrices = new float[session.WorldMatrixFloatCount];
            session.EvaluateAndCopy(
                changedPositions,
                changedRotations,
                localScalesXyz,
                morphWeights,
                ikEnabled,
                changedWorldMatrices);
            Assert.That(Math.Abs(changedWorldMatrices[12] - firstWorldMatrices[12]), Is.GreaterThan(1.0e-5f));
            Assert.That(Math.Abs(changedWorldMatrices[0] - firstWorldMatrices[0]), Is.GreaterThan(1.0e-5f));

            Assert.Throws<ArgumentException>(() => session.EvaluateAndCopy(
                localPositionOffsetsXyz,
                localRotationXyzw,
                localScalesXyz,
                morphWeights,
                ikEnabled,
                new float[session.WorldMatrixFloatCount - 1]));
            session.Dispose();
            session.Dispose();
            Assert.Throws<ObjectDisposedException>(() => session.EvaluateAndCopy(
                Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(),
                Array.Empty<float>(), Array.Empty<byte>(), Array.Empty<float>()));
        }

        private static MmdRuntimeFfiHostPoseSession CreateSession()
        {
            return MmdRuntimeFfiHostPoseSession.Create(
                MmdTestFixtures.ReadFixtureAssetBytes("test_semi_basic_bone.pmx"));
        }

    }
}
