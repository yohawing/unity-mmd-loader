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
            var session = new MmdRuntimeSession(model, motion, "model", "motion");
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
            MmdRuntimeSession session = CreateSession(out MmdModelDefinition model);
            _ = session.TopologyPlan;
            model.bones[0].origin[0] += 0.25f;

            InvalidOperationException? error = Assert.Throws<InvalidOperationException>(() =>
                session.EvaluateFrame(frame: 0, time: 0.0f, ikSolver: new MmdIkSolver()));

            Assert.That(error!.Message, Does.Contain("topology changed"));
        }

        [Test]
        public void RuntimeSessionNativeAndSnapshotPathsKeepTopologyLazy()
        {
            MmdRuntimeSession session = CreateSession(out _);
            FieldInfo? field = typeof(MmdRuntimeSession).GetField(
                "topologyPlan",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            Assert.That(field!.GetValue(session), Is.Null);
            _ = session.EvaluateFrame(frame: 0, time: 0.0f);
            _ = session.BuildSnapshot(frame: 0, time: 0.0f);
            Assert.That(field.GetValue(session), Is.Null);
        }

        private static MmdRuntimeSession CreateSession(out MmdModelDefinition model)
        {
            var parser = new NativeMmdParser();
            model = parser.LoadModel(MmdTestFixtures.ReadFixtureAssetBytes("test_append_bone.pmx"));
            MmdMotionDefinition motion = parser.LoadMotion(
                MmdTestFixtures.ReadFixtureAssetBytes("test_append_bone.vmd"));
            return new MmdRuntimeSession(model, motion, "model", "motion");
        }
    }
}
