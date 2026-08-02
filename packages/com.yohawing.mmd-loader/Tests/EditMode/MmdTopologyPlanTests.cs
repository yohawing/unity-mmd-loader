#nullable enable

using System;
using System.Reflection;
using Mmd.Motion;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Pose;
using NUnit.Framework;

namespace Mmd.Tests
{
    public sealed class MmdTopologyPlanTests
    {
        [Test]
        public void RuntimeSessionReusesTopologyAndPreservesManagedWorldMatrices()
        {
            MmdModelDefinition model = CreateIkTopologyModel();
            var motion = new MmdMotionDefinition();
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
            MmdModelDefinition model = CreateIkTopologyModel();
            using MmdRuntimeSession session = new(
                model,
                new MmdMotionDefinition(),
                "synthetic.pmx",
                "synthetic.vmd");
            _ = session.TopologyPlan;
            model.bones[0].origin[0] += 0.25f;

            InvalidOperationException? error = Assert.Throws<InvalidOperationException>(() =>
                session.EvaluateFrame(frame: 0, time: 0.0f, ikSolver: new MmdIkSolver()));

            Assert.That(error!.Message, Does.Contain("topology changed"));
        }

        [Test]
        public void RuntimeSessionPublicEvaluationSupportsSyntheticIrAndUsesRequestedPhysicsBackend()
        {
            MmdModelDefinition model = CreateIkTopologyModel();
            var motion = new MmdMotionDefinition();
            using var session = new MmdRuntimeSession(model, motion, "synthetic.pmx", "synthetic.vmd");
            var physics = new RecordingPhysicsBackend();

            MmdEvaluatedFrame frame = session.EvaluateFrame(
                frame: 0,
                time: 0.0f,
                physicsBackend: physics);

            Assert.That(frame.bones, Has.Count.EqualTo(model.bones.Count));
            Assert.That(physics.StepCount, Is.EqualTo(1));
            Assert.That(RequirePrivateField("nativePlaybackSession").GetValue(session), Is.Null);
        }

        [Test]
        public void RuntimeSessionNativePlaybackIsLazyAndReusesStateWithoutManagedTopology()
        {
            using MmdRuntimeSession session = CreateSession(out _);
            FieldInfo topologyField = RequirePrivateField("topologyPlan");
            FieldInfo playbackField = RequirePrivateField("nativePlaybackSession");
            FieldInfo worldField = RequirePrivateField("nativeWorldMatrices");
            FieldInfo morphField = RequirePrivateField("nativeMorphWeights");
            FieldInfo ikField = RequirePrivateField("nativeIkEnabled");

            Assert.That(topologyField.GetValue(session), Is.Null);
            Assert.That(playbackField.GetValue(session), Is.Null);
            Assert.That(worldField.GetValue(session), Is.Null);
            Assert.That(morphField.GetValue(session), Is.Null);
            Assert.That(ikField.GetValue(session), Is.Null);

            _ = session.EvaluateFrame(frame: 0, time: 0.0f);
            object playback = playbackField.GetValue(session)!;
            object world = worldField.GetValue(session)!;
            object morph = morphField.GetValue(session)!;
            object ik = ikField.GetValue(session)!;
            _ = session.EvaluateFrame(frame: 1, time: 1.0f / 30.0f);
            _ = session.BuildSnapshot(frame: 0, time: 0.0f);

            Assert.That(topologyField.GetValue(session), Is.Null);
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
        public void RuntimeSessionNativePlaybackRejectsMutationBeforeCompileButUsesCompiledStateAfterSourceReplacement()
        {
            using MmdRuntimeSession changedBeforeCompile = CreateSession(out MmdModelDefinition firstModel);
            firstModel.sourceBytes![0] ^= 0xff;
            InvalidOperationException? preCompileError = Assert.Throws<InvalidOperationException>(() =>
                changedBeforeCompile.EvaluateFrame(frame: 0, time: 0.0f));
            Assert.That(preCompileError!.Message, Does.Contain("changed before session compilation"));

            using MmdRuntimeSession replacedAfterCompile = CreateSession(out _, out MmdMotionDefinition secondMotion);
            _ = replacedAfterCompile.EvaluateFrame(frame: 0, time: 0.0f);
            secondMotion.sourceBytes = (byte[])secondMotion.sourceBytes!.Clone();
            secondMotion.sourceBytes[0] ^= 0xff;
            Assert.DoesNotThrow(() =>
            {
                _ = replacedAfterCompile.EvaluateFrame(frame: 0, time: 0.0f);
            });

            InvalidOperationException? snapshotError = Assert.Throws<InvalidOperationException>(() =>
                replacedAfterCompile.BuildSnapshots(new[] { 0 }, frameRate: 30.0f));
            Assert.That(snapshotError!.Message, Does.Contain("changed after session compilation"));
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
            Assert.Throws<ObjectDisposedException>(() => session.EvaluateNativeFrame(frame: 0, time: 0.0f));
            Assert.Throws<ObjectDisposedException>(() => session.BuildSnapshot(frame: 0, time: 0.0f));
        }

        [Test]
        public void TopologyPlanCompilesIkChainsAndLinksAsImmutableSourceOrderedValues()
        {
            MmdModelDefinition model = CreateIkTopologyModel();
            MmdTopologyPlan plan = MmdTopologyPlan.CreateFromValidatedModel(model);

            Assert.That(plan.IkChains, Has.Count.EqualTo(2));
            Assert.That(plan.IkChains[0].BoneIndex, Is.EqualTo(2));
            Assert.That(plan.IkChains[0].TargetBoneIndex, Is.EqualTo(1));
            Assert.That(plan.IkChains[0].IterationCount, Is.EqualTo(8));
            Assert.That(plan.IkChains[0].AngleLimit, Is.EqualTo(0.5f));
            Assert.That(plan.IkChains[0].Links, Has.Count.EqualTo(2));
            Assert.That(plan.IkChains[0].Links[0].BoneIndex, Is.EqualTo(0));
            Assert.That(plan.IkChains[0].Links[1].BoneIndex, Is.EqualTo(1));
            Assert.That(plan.IkChains[1].BoneIndex, Is.EqualTo(1));
            Assert.That(plan.IkChains[0].Links[0].MinimumX, Is.EqualTo(-0.25f));
            Assert.That(plan.IkChains[0].Links[0].MaximumZ, Is.EqualTo(0.75f));

            model.ik[0].boneIndex = 0;
            model.ik[0].links[0].minimumAngle[0] = -9.0f;
            Assert.That(plan.IkChains[0].BoneIndex, Is.EqualTo(2));
            Assert.That(plan.IkChains[0].Links[0].MinimumX, Is.EqualTo(-0.25f));
        }

        [TestCase("ik-definition")]
        [TestCase("ik-link")]
        [TestCase("ik-link-limit")]
        [TestCase("fixed-axis")]
        [TestCase("deform-after")]
        [TestCase("name")]
        public void TopologyPlanRejectsIkRelevantSourceMutation(string mutation)
        {
            MmdModelDefinition model = CreateIkTopologyModel();
            MmdTopologyPlan plan = MmdTopologyPlan.CreateFromValidatedModel(model);

            switch (mutation)
            {
                case "ik-definition":
                    model.ik[0].iterationCount++;
                    break;
                case "ik-link":
                    model.ik[0].links[0].boneIndex = 1;
                    break;
                case "ik-link-limit":
                    model.ik[0].links[0].maximumAngle[2] += 0.125f;
                    break;
                case "fixed-axis":
                    model.bones[0].fixedAxisVector[1] = 1.0f;
                    break;
                case "deform-after":
                    model.bones[2].deformAfterPhysics = !model.bones[2].deformAfterPhysics;
                    break;
                case "name":
                    model.bones[2].name = "renamed-goal";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation));
            }

            InvalidOperationException? error = Assert.Throws<InvalidOperationException>(() => plan.EnsureModel(model));
            Assert.That(error!.Message, Does.Contain("topology changed"));
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

        private static MmdModelDefinition CreateIkTopologyModel()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f },
                fixedAxis = true,
                fixedAxisVector = new[] { 1.0f, 0.0f, 0.0f }
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1,
                name = "effector",
                parentIndex = 0,
                origin = new[] { 0.0f, 1.0f, 0.0f }
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 2,
                name = "goal",
                parentIndex = 0,
                origin = new[] { 1.0f, 1.0f, 0.0f },
                deformAfterPhysics = true
            });

            model.ik.Add(new MmdIkDefinition
            {
                boneIndex = 2,
                targetBoneIndex = 1,
                iterationCount = 8,
                angleLimit = 0.5f,
                links = new System.Collections.Generic.List<MmdIkLinkDefinition>
                {
                    new MmdIkLinkDefinition
                    {
                        boneIndex = 0,
                        hasLimit = true,
                        minimumAngle = new[] { -0.25f, -0.5f, -0.75f },
                        maximumAngle = new[] { 0.25f, 0.5f, 0.75f }
                    },
                    new MmdIkLinkDefinition { boneIndex = 1 }
                }
            });
            model.ik.Add(new MmdIkDefinition
            {
                boneIndex = 1,
                targetBoneIndex = 0,
                iterationCount = 2,
                angleLimit = 0.25f,
                links = new System.Collections.Generic.List<MmdIkLinkDefinition>
                {
                    new MmdIkLinkDefinition { boneIndex = 2 }
                }
            });
            return model;
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

        private sealed class RecordingPhysicsBackend : IMmdPhysicsBackend
        {
            public string Name => "recording";

            public bool IsDeterministic => true;

            public int StepCount { get; private set; }

            public void Reset()
            {
            }

            public void Step(int frame, float deltaTime)
            {
                StepCount++;
            }
        }
    }
}
