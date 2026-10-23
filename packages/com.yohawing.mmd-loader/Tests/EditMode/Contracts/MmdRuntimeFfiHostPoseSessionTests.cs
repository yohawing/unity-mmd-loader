#nullable enable

using System;
using System.Linq;
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

        [Test]
        public void HostRigEvaluationRecoversAfterInvalidPose()
        {
            using MmdRuntimeFfiHostPoseSession session = MmdRuntimeFfiHostPoseSession.CreateWithRig(
                MmdTestFixtures.ReadFixtureAssetBytes("test_semi_basic_bone.pmx"),
                new[] { 0u },
                Array.Empty<uint>());

            var localPositionOffsetsXyz = new float[session.BoneCount * 3];
            var localRotationXyzw = new float[session.BoneCount * 4];
            var localScalesXyz = new float[session.BoneCount * 3];
            var morphWeights = new float[session.MorphCount];
            var ikEnabled = new byte[session.IkCount];
            InitializeIdentityPose(
                session,
                localRotationXyzw,
                localScalesXyz);
            localPositionOffsetsXyz[0] = 0.25f;
            localRotationXyzw[1] = (float)Math.Sin(0.25f);
            localRotationXyzw[3] = (float)Math.Cos(0.25f);

            var firstWorldMatrices = new float[session.WorldMatrixFloatCount];
            session.EvaluateAndCopy(
                localPositionOffsetsXyz,
                localRotationXyzw,
                localScalesXyz,
                morphWeights,
                ikEnabled,
                firstWorldMatrices);
            Assert.That(firstWorldMatrices.All(float.IsFinite), Is.True,
                "host-rig world matrix output must remain finite");

            var invalidScales = (float[])localScalesXyz.Clone();
            invalidScales[1] = 2.0f;
            Assert.Throws<InvalidOperationException>(() => session.EvaluateAndCopy(
                localPositionOffsetsXyz,
                localRotationXyzw,
                invalidScales,
                morphWeights,
                ikEnabled,
                new float[session.WorldMatrixFloatCount]));

            var recoveredWorldMatrices = new float[session.WorldMatrixFloatCount];
            session.EvaluateAndCopy(
                localPositionOffsetsXyz,
                localRotationXyzw,
                localScalesXyz,
                morphWeights,
                ikEnabled,
                recoveredWorldMatrices);
            CollectionAssert.AreEqual(
                firstWorldMatrices,
                recoveredWorldMatrices,
                "an invalid host-rig pose must not poison the next valid evaluation");

            session.Dispose();
            session.Dispose();
            Assert.Throws<ObjectDisposedException>(() => session.EvaluateAndCopy(
                localPositionOffsetsXyz,
                localRotationXyzw,
                localScalesXyz,
                morphWeights,
                ikEnabled,
                recoveredWorldMatrices));
        }

        [Test]
        public void HostRigRejectsInvalidDrivenAndGoalIndices()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_semi_basic_bone.pmx");
            Assert.Throws<InvalidOperationException>(() => MmdRuntimeFfiHostPoseSession.CreateWithRig(
                pmxBytes,
                new[] { uint.MaxValue },
                Array.Empty<uint>()));
            Assert.Throws<InvalidOperationException>(() => MmdRuntimeFfiHostPoseSession.CreateWithRig(
                pmxBytes,
                Array.Empty<uint>(),
                new[] { uint.MaxValue }));
        }

        [Test]
        public void HostRigProtectsIncomingAppendTargetAndUsesItsInputForEvaluation()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_append_bone.pmx");
            using MmdRuntimeFfiHostPoseSession ordinary = MmdRuntimeFfiHostPoseSession.CreateWithRig(
                pmxBytes,
                Array.Empty<uint>(),
                Array.Empty<uint>());
            using MmdRuntimeFfiHostPoseSession hostRig = MmdRuntimeFfiHostPoseSession.CreateWithRig(
                pmxBytes,
                new[] { 5u },
                Array.Empty<uint>());

            var ordinaryPositions = new float[ordinary.BoneCount * 3];
            var ordinaryRotations = new float[ordinary.BoneCount * 4];
            var ordinaryScales = new float[ordinary.BoneCount * 3];
            var ordinaryMorphs = new float[ordinary.MorphCount];
            var ordinaryIk = new byte[ordinary.IkCount];
            var rigPositions = new float[hostRig.BoneCount * 3];
            var rigRotations = new float[hostRig.BoneCount * 4];
            var rigScales = new float[hostRig.BoneCount * 3];
            var rigMorphs = new float[hostRig.MorphCount];
            var rigIk = new byte[hostRig.IkCount];
            InitializeIdentityPose(ordinary, ordinaryRotations, ordinaryScales);
            InitializeIdentityPose(hostRig, rigRotations, rigScales);
            SetRotationZ(ordinaryRotations, 3, 0.4f);
            SetRotationZ(ordinaryRotations, 5, -0.2f);
            SetRotationZ(rigRotations, 3, 0.4f);
            SetRotationZ(rigRotations, 5, -0.2f);

            var ordinaryWorldMatrices = new float[ordinary.WorldMatrixFloatCount];
            var rigWorldMatrices = new float[hostRig.WorldMatrixFloatCount];
            ordinary.EvaluateAndCopy(
                ordinaryPositions,
                ordinaryRotations,
                ordinaryScales,
                ordinaryMorphs,
                ordinaryIk,
                ordinaryWorldMatrices);
            hostRig.EvaluateAndCopy(
                rigPositions,
                rigRotations,
                rigScales,
                rigMorphs,
                rigIk,
                rigWorldMatrices);

            const float targetAngle = -0.2f;
            int targetMatrixOffset = 5 * 16;
            Assert.That(rigWorldMatrices[targetMatrixOffset + 12], Is.EqualTo(2.0f).Within(1.0e-5f));
            Assert.That(rigWorldMatrices[targetMatrixOffset + 13], Is.EqualTo(2.0f).Within(1.0e-5f));
            Assert.That(rigWorldMatrices[targetMatrixOffset + 14], Is.EqualTo(2.0f).Within(1.0e-5f));
            Assert.That(rigWorldMatrices[targetMatrixOffset], Is.EqualTo(Math.Cos(targetAngle)).Within(1.0e-5));
            Assert.That(rigWorldMatrices[targetMatrixOffset + 1], Is.EqualTo(Math.Sin(targetAngle)).Within(1.0e-5));
            Assert.That(ordinaryWorldMatrices[targetMatrixOffset + 1], Is.EqualTo(Math.Sin(0.2f)).Within(1.0e-5));
            Assert.That(
                Math.Abs(rigWorldMatrices[targetMatrixOffset + 1] - ordinaryWorldMatrices[targetMatrixOffset + 1]),
                Is.GreaterThan(0.1f),
                "the legacy incoming Append result must differ from the protected FK target");
        }

        [Test]
        public void AppendDependentIkHostSamplesRemainIndependent()
        {
#if !UNITY_EDITOR_WIN
            Assert.Ignore("The distributed mmd-runtime host-pose gate is Windows Editor only.");
#endif
            using var session = MmdRuntimeFfiHostPoseSession.CreateWithRig(
                MmdTestFixtures.ReadFixtureAssetBytes("ik_append_history.pmx"),
                Array.Empty<uint>(), new uint[] { 3, 7 });
            var positions = new float[session.BoneCount * 3];
            var rotations = new float[session.BoneCount * 4];
            var scales = new float[session.BoneCount * 3];
            var morphs = new float[session.MorphCount];
            var ik = Enumerable.Repeat((byte)1, session.IkCount).ToArray();
            InitializeIdentityPose(session, rotations, scales);
            var first = new float[session.WorldMatrixFloatCount];
            var repeated = new float[session.WorldMatrixFloatCount];
            session.EvaluateAndCopy(positions, rotations, scales, morphs, ik, first);
            session.EvaluateAndCopy(positions, rotations, scales, morphs, ik, repeated);
            CollectionAssert.AreEqual(first, repeated);
        }

        private static MmdRuntimeFfiHostPoseSession CreateSession()
        {
            return MmdRuntimeFfiHostPoseSession.CreateWithRig(
                MmdTestFixtures.ReadFixtureAssetBytes("test_semi_basic_bone.pmx"),
                Array.Empty<uint>(),
                Array.Empty<uint>());
        }

        private static void InitializeIdentityPose(
            MmdRuntimeFfiHostPoseSession session,
            float[] localRotationXyzw,
            float[] localScalesXyz)
        {
            for (int boneIndex = 0; boneIndex < session.BoneCount; boneIndex++)
            {
                localRotationXyzw[boneIndex * 4 + 3] = 1.0f;
                localScalesXyz[boneIndex * 3] = 1.0f;
                localScalesXyz[boneIndex * 3 + 1] = 1.0f;
                localScalesXyz[boneIndex * 3 + 2] = 1.0f;
            }
        }

        private static void SetRotationZ(float[] localRotationXyzw, int boneIndex, float angle)
        {
            localRotationXyzw[boneIndex * 4 + 2] = (float)Math.Sin(angle * 0.5f);
            localRotationXyzw[boneIndex * 4 + 3] = (float)Math.Cos(angle * 0.5f);
        }

    }
}
