#nullable enable

using System;
using System.Reflection;
using Mmd.Motion;
using Mmd.Parser;
using NUnit.Framework;

namespace Mmd.Tests
{
    public sealed class MmdTopologyPlanTests
    {
        [Test]
        public void RuntimeSessionReusesTopologyAndPreservesManagedWorldMatrices()
        {
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(
                MmdTestFixtures.ReadFixtureAssetBytes("test_append_bone.pmx"));
            MmdMotionDefinition motion = parser.LoadMotion(
                MmdTestFixtures.ReadFixtureAssetBytes("test_append_bone.vmd"));
            using var session = new MmdRuntimeSession(model, motion, "model", "motion");
            var topologyPlan = session.TopologyPlan;

            MmdEvaluatedFrame expected = MmdRuntimeFrameEvaluator.EvaluatePhaseOnePlaybackFrame(
                model, motion, frame: 0, time: 0.0f, ikSolver: new MmdIkSolver());
            MmdEvaluatedFrame actual = session.EvaluateFrame(
                frame: 0, time: 0.0f, ikSolver: new MmdIkSolver());

            Assert.That(session.TopologyPlan, Is.SameAs(topologyPlan));
            Assert.That(actual.bones.Count, Is.EqualTo(expected.bones.Count));
            for (int bone = 0; bone < expected.bones.Count; bone++)
            {
                Assert.That(actual.bones[bone].worldMatrix, Is.EqualTo(expected.bones[bone].worldMatrix));
            }
        }

        [Test]
        public void RuntimeSessionManagedPipelineRejectsStaleTopologyBeforeEvaluation()
        {
            using MmdRuntimeSession session = CreateSession(out MmdModelDefinition model);
            _ = session.TopologyPlan;
            model.bones[0].origin[0] += 0.25f;

            InvalidOperationException? error = Assert.Throws<InvalidOperationException>(() =>
                session.EvaluateFrame(frame: 0, time: 0.0f, ikSolver: new MmdIkSolver()));

            Assert.That(error!.Message, Does.Contain("topology changed"));
        }

        [Test]
        public void RuntimeSessionNativeAndSnapshotPathsKeepTopologyLazy()
        {
            using MmdRuntimeSession session = CreateSession(out _);
            FieldInfo? field = typeof(MmdRuntimeSession).GetField(
                "topologyPlan",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            Assert.That(field!.GetValue(session), Is.Null);
            _ = session.EvaluateFrame(frame: 0, time: 0.0f);
            _ = session.BuildSnapshot(frame: 0, time: 0.0f);
            Assert.That(field.GetValue(session), Is.Null);
        }

        [Test]
        public void RuntimeSessionNativePlaybackIsLazyAndReusesHandleAndScratchBuffers()
        {
            using MmdRuntimeSession session = CreateSession(out _);
            FieldInfo playbackField = RequirePrivateField("nativePlaybackSession");
            FieldInfo worldField = RequirePrivateField("nativeWorldMatrices");
            FieldInfo morphField = RequirePrivateField("nativeMorphWeights");
            FieldInfo ikField = RequirePrivateField("nativeIkEnabled");

            Assert.That(playbackField.GetValue(session), Is.Null);
            Assert.That(worldField.GetValue(session), Is.Null);
            Assert.That(morphField.GetValue(session), Is.Null);
            Assert.That(ikField.GetValue(session), Is.Null);

            _ = session.EvaluateFrame(frame: 0, time: 0.0f);
            object playback = playbackField.GetValue(session)!;
            object world = worldField.GetValue(session)!;
            object morph = morphField.GetValue(session)!;
            object ik = ikField.GetValue(session)!;
            _ = session.EvaluateBeforePhysicsFrame(frame: 1, time: 1.0f / 30.0f);

            Assert.That(playbackField.GetValue(session), Is.SameAs(playback));
            Assert.That(worldField.GetValue(session), Is.SameAs(world));
            Assert.That(morphField.GetValue(session), Is.SameAs(morph));
            Assert.That(ikField.GetValue(session), Is.SameAs(ik));
        }

        [Test]
        public void RuntimeSessionNativePlaybackPreservesSeekAndRepeatParity()
        {
            using MmdRuntimeSession session = CreateSession(out MmdModelDefinition model, out MmdMotionDefinition motion);
            MmdEvaluatedFrame expectedLater = MmdRuntimeFrameEvaluator.EvaluatePhaseOnePlaybackFrame(
                model, motion, frame: 1, time: 1.0f / 30.0f);
            MmdEvaluatedFrame expectedEarlier = MmdRuntimeFrameEvaluator.EvaluatePhaseOnePlaybackFrame(
                model, motion, frame: 0, time: 0.0f);

            MmdEvaluatedFrame firstLater = session.EvaluateFrame(frame: 1, time: 1.0f / 30.0f);
            MmdEvaluatedFrame earlier = session.EvaluateFrame(frame: 0, time: 0.0f);
            MmdEvaluatedFrame repeatedLater = session.EvaluateFrame(frame: 1, time: 1.0f / 30.0f);

            AssertFrameEqual(expectedLater, firstLater);
            AssertFrameEqual(expectedEarlier, earlier);
            AssertFrameEqual(expectedLater, repeatedLater);
        }

        [Test]
        public void RuntimeSessionNativePlaybackRejectsSourceMutationBeforeCompileAndReplacementAfterCompile()
        {
            using MmdRuntimeSession changedBeforeCompile = CreateSession(out MmdModelDefinition firstModel);
            firstModel.sourceBytes![0] ^= 0xff;
            InvalidOperationException? fingerprintError = Assert.Throws<InvalidOperationException>(() =>
                changedBeforeCompile.EvaluateFrame(frame: 0, time: 0.0f));
            Assert.That(fingerprintError!.Message, Does.Contain("source identity changed"));

            using MmdRuntimeSession replacedAfterCompile = CreateSession(out _, out MmdMotionDefinition secondMotion);
            _ = replacedAfterCompile.EvaluateFrame(frame: 0, time: 0.0f);
            secondMotion.sourceBytes![0] ^= 0xff;
            InvalidOperationException? postCompileFingerprintError = Assert.Throws<InvalidOperationException>(() =>
                replacedAfterCompile.EvaluateFrame(frame: 0, time: 0.0f));
            Assert.That(postCompileFingerprintError!.Message, Does.Contain("source identity changed"));
            secondMotion.sourceBytes[0] ^= 0xff;
            secondMotion.sourceBytes = (byte[])secondMotion.sourceBytes!.Clone();
            InvalidOperationException? identityError = Assert.Throws<InvalidOperationException>(() =>
                replacedAfterCompile.EvaluateFrame(frame: 0, time: 0.0f));
            Assert.That(identityError!.Message, Does.Contain("source identity changed"));
        }

        [Test]
        public void RuntimeSessionDisposeIsIdempotentReleasesNativeStateAndRejectsOperations()
        {
            MmdRuntimeSession session = CreateSession(out _);
            _ = session.EvaluateFrame(frame: 0, time: 0.0f);
            FieldInfo playbackField = RequirePrivateField("nativePlaybackSession");
            FieldInfo worldField = RequirePrivateField("nativeWorldMatrices");

            session.Dispose();
            session.Dispose();

            Assert.That(playbackField.GetValue(session), Is.Null);
            Assert.That(worldField.GetValue(session), Is.Null);
            Assert.Throws<ObjectDisposedException>(() => session.EvaluateFrame(frame: 0, time: 0.0f));
            Assert.Throws<ObjectDisposedException>(() => session.BuildSnapshot(frame: 0, time: 0.0f));
        }

        private static MmdRuntimeSession CreateSession(out MmdModelDefinition model)
        {
            return CreateSession(out model, out _);
        }

        private static MmdRuntimeSession CreateSession(
            out MmdModelDefinition model,
            out MmdMotionDefinition motion)
        {
            var parser = new NativeMmdParser();
            model = parser.LoadModel(MmdTestFixtures.ReadFixtureAssetBytes("test_append_bone.pmx"));
            motion = parser.LoadMotion(
                MmdTestFixtures.ReadFixtureAssetBytes("test_append_bone.vmd"));
            return new MmdRuntimeSession(model, motion, "model", "motion");
        }

        private static FieldInfo RequirePrivateField(string name)
        {
            FieldInfo? field = typeof(MmdRuntimeSession).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field!;
        }

        private static void AssertFrameEqual(MmdEvaluatedFrame expected, MmdEvaluatedFrame actual)
        {
            Assert.That(actual.frame, Is.EqualTo(expected.frame));
            Assert.That(actual.time, Is.EqualTo(expected.time));
            Assert.That(actual.bones.Count, Is.EqualTo(expected.bones.Count));
            for (int bone = 0; bone < expected.bones.Count; bone++)
            {
                Assert.That(actual.bones[bone].worldMatrix, Is.EqualTo(expected.bones[bone].worldMatrix));
            }

            Assert.That(actual.morphs.Count, Is.EqualTo(expected.morphs.Count));
            for (int morph = 0; morph < expected.morphs.Count; morph++)
            {
                Assert.That(actual.morphs[morph].weight, Is.EqualTo(expected.morphs[morph].weight));
            }
        }
    }
}
