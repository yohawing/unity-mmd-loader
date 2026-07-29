#nullable enable

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
    }
}
