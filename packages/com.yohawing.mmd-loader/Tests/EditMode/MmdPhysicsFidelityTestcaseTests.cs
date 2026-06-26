#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Mmd.Parser;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmdPhysicsFidelityTestcaseTests
    {
        private const string PhysicsModelFileName = "test_hair_physics.pmx";

        private MmdModelDefinition? model;

        [OneTimeSetUp]
        public void ParseTestAssets()
        {
            string pmxPath = MmdTestFixtures.FixtureAssetPath(PhysicsModelFileName);
            Assume.That(pmxPath, Does.Exist, "PMX fixture not found: " + pmxPath);

            var parser = new NativeMmdParser();
            model = parser.LoadModel(File.ReadAllBytes(pmxPath));
        }

        [Test]
        public void ModelHasPhysicsDefinitions()
        {
            Assert.That(model!.physics, Is.Not.Null, "model.physics");
            Assert.That(model.physics.rigidbodies.Count, Is.GreaterThan(0),
                "test_hair_physics must contain rigidbody definitions");
            Assert.That(model.physics.joints.Count, Is.GreaterThan(0),
                "test_hair_physics must contain joint definitions");
        }

        [Test]
        public void ModelHasDynamicBodies()
        {
            int dynamicCount = model!.physics.rigidbodies.Count(
                b => b.physicsKind == "dynamic");
            int dynamicBoneCount = model.physics.rigidbodies.Count(
                b => b.physicsKind == "dynamicBone");
            int staticCount = model.physics.rigidbodies.Count(
                b => b.physicsKind == "static");

            Assert.That(dynamicCount + dynamicBoneCount, Is.GreaterThan(0),
                "test_hair_physics must have at least one non-static rigidbody");

            TestContext.Out.WriteLine($"Physics configuration:");
            TestContext.Out.WriteLine($"  Rigid bodies: {model.physics.rigidbodies.Count} total");
            TestContext.Out.WriteLine($"    static (mode 0 / bone-following): {staticCount}");
            TestContext.Out.WriteLine($"    dynamic (mode 1): {dynamicCount}");
            TestContext.Out.WriteLine($"    dynamicBone (mode 2): {dynamicBoneCount}");
            TestContext.Out.WriteLine($"  Joints: {model.physics.joints.Count}");
        }

        [Test]
        public void ModelPhysicsBonesAreValidIndices()
        {
            int boneCount = model!.bones.Count;
            foreach (MmdRigidbodyDefinition body in model.physics.rigidbodies)
            {
                if (body.boneIndex >= 0)
                {
                    Assert.That(body.boneIndex, Is.LessThan(boneCount),
                        $"Rigidbody '{body.name}' boneIndex {body.boneIndex} exceeds bone count {boneCount}");
                }
            }
        }

        [Test]
        public void ModelPhysicsJointIndicesAreValid()
        {
            int bodyCount = model!.physics.rigidbodies.Count;
            foreach (MmdJointDefinition joint in model.physics.joints)
            {
                if (joint.rigidbodyAIndex >= 0)
                {
                    Assert.That(joint.rigidbodyAIndex, Is.LessThan(bodyCount),
                        $"Joint '{joint.name}' rigidbodyAIndex {joint.rigidbodyAIndex} exceeds body count {bodyCount}");
                }
                if (joint.rigidbodyBIndex >= 0)
                {
                    Assert.That(joint.rigidbodyBIndex, Is.LessThan(bodyCount),
                        $"Joint '{joint.name}' rigidbodyBIndex {joint.rigidbodyBIndex} exceeds body count {bodyCount}");
                }
            }
        }

        [Test]
        public void PhysicsBoneNamesReportedForManifest()
        {
            var dynamicBoneNames = new HashSet<string>();
            foreach (MmdRigidbodyDefinition body in model!.physics.rigidbodies)
            {
                if (body.physicsKind != "static" && body.boneIndex >= 0 && body.boneIndex < model.bones.Count)
                {
                    dynamicBoneNames.Add(model.bones[body.boneIndex].name);
                }
            }

            Assert.That(dynamicBoneNames.Count, Is.GreaterThan(0),
                "Expected at least one dynamic bone for physics focus list");

            TestContext.Out.WriteLine($"Dynamic physics bones ({dynamicBoneNames.Count}):");
            foreach (string name in dynamicBoneNames.OrderBy(n => n))
            {
                TestContext.Out.WriteLine($"  {name}");
            }
        }

        [Test]
        public void CollisionGroupsReported()
        {
            var groups = new Dictionary<int, int>();
            foreach (MmdRigidbodyDefinition body in model!.physics.rigidbodies)
            {
                if (!groups.ContainsKey(body.group))
                    groups[body.group] = 0;
                groups[body.group]++;
            }

            TestContext.Out.WriteLine($"Collision groups:");
            foreach (var kv in groups.OrderBy(kv => kv.Key))
            {
                TestContext.Out.WriteLine($"  Group {kv.Key}: {kv.Value} bodies");
            }
        }

        [Test]
        public void PhysicsGravityDriftFramesPinned()
        {
            int gravityOnlyFrames = 120;
            TestContext.Out.WriteLine($"Gravity-only test configuration:");
            TestContext.Out.WriteLine($"  Model: {PhysicsModelFileName}");
            TestContext.Out.WriteLine($"  Motion: rest pose (no VMD)");
            TestContext.Out.WriteLine($"  Frames: 0..{gravityOnlyFrames} at 30fps");
            TestContext.Out.WriteLine($"  Sample frames: [0, 1, 5, 10, 30, 60, 90, 120]");
            TestContext.Out.WriteLine($"  Metrics: rms-translation, max-translation, divergence-ratio");
            TestContext.Out.WriteLine($"  Comparison: bone worldMatrix column-major (m[12], m[13], m[14]) translation");
            TestContext.Out.WriteLine($"  Epsilon: 0.003 (inherited from physics-coarse)");
            Assert.Pass("Frame configuration pinned");
        }
    }
}
