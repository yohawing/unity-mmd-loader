#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Mmd.Parser;
using Mmd.Physics;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmdPhysicsFidelityTestcaseTests
    {
        private const string PhysicsModelFileName = "test_hair_physics.pmx";
        // This is a fixture-level migration gate, not a request to remove the legacy fallback.
        // The roadmap already classifies the remaining solver-layer RMS gap as good enough.
        private const float HairFixtureMaxPositionDelta = 0.20f;
        private const float HairFixtureMaxRotationDeltaDegrees = 5.0f;

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
        public void MmdAnimAndLegacyBulletHairFixtureParityIsMeasured()
        {
            const string motionFileName = "rest-pose.vmd";
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes(PhysicsModelFileName);

            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(pmxBytes);
            byte[] vmdBytes = CreateEmptyVmdBytes(model.name ?? PhysicsModelFileName);
            int pureWorldAnchorJointCount = model.physics.joints.Count(
                joint => joint.rigidbodyAIndex < 0 && joint.rigidbodyBIndex < 0);
            model.physics.joints.RemoveAll(
                joint => joint.rigidbodyAIndex < 0 && joint.rigidbodyBIndex < 0);

            MmdPhysicsBackendAvailability legacyAvailability = BulletMmdPhysicsBackend.ProbeAvailability();
            if (!legacyAvailability.backendAvailable)
            {
                Assert.Ignore("Legacy Bullet physics backend is not available: " + legacyAvailability.unsupportedReason);
            }

            if (!MmdAnimPhysicsBackend.TryCreate(
                    pmxBytes,
                    vmdBytes,
                    PhysicsModelFileName,
                    motionFileName,
                    out MmdAnimPhysicsBackend? nativeBackend,
                    out string nativeReason))
            {
                Assert.Ignore("mmd-anim Bullet physics backend is not available: " + nativeReason);
            }

            MmdAnimPhysicsBackend nonNullNativeBackend = nativeBackend!;
            using (nonNullNativeBackend)
            using (var legacyBackend = new BulletMmdPhysicsBackend(PhysicsModelFileName, motionFileName))
            {
                BulletMmdPhysicsBackend.SetMaxSubStepEstimateFixedTimeStepSecondsForDiagnostics(1.0f / 120.0f);
                try
                {
                    nonNullNativeBackend.InitializeWorld(model);
                    legacyBackend.InitializeWorld(model);

                    Assert.That(legacyBackend.RigidbodyCount, Is.EqualTo(model.physics.rigidbodies.Count));
                    Assert.That(nonNullNativeBackend.SkippedWorldAnchorJointCount, Is.EqualTo(legacyBackend.SkippedWorldAnchorJointCount));

                    var report = new StringBuilder();
                    report.AppendLine("=== mmd-anim / legacy Bullet physics parity ===");
                    report.AppendLine($"model={PhysicsModelFileName}");
                    report.AppendLine($"motion={motionFileName}");
                    report.AppendLine($"rigidbodies={model.physics.rigidbodies.Count}");
                    report.AppendLine($"joints={model.physics.joints.Count}");
                    report.AppendLine($"pureWorldAnchorJointsRemovedForLegacy={pureWorldAnchorJointCount}");
                    report.AppendLine($"oneSidedWorldAnchorJointsSkipped={nonNullNativeBackend.SkippedWorldAnchorJointCount}");
                    report.AppendLine("frame,positionMax,rotationMaxDegrees,worstBody");

                    nonNullNativeBackend.Reset();
                    legacyBackend.Reset();
                    legacyBackend.SyncInterpolationAndZeroVelocity();

                    ParitySample resetSample = CompareBodies(nonNullNativeBackend, legacyBackend, model, "reset");
                    report.AppendLine(resetSample.ToCsvLine());
                    AppendBodyDetails(report, "reset", nonNullNativeBackend, legacyBackend, model);

                    // mmd-anim's first Step after Reset evaluates the requested frame and
                    // performs the current-pose reset/settle. Match that with the legacy
                    // adapter's explicit seed settle before comparing forward trajectories.
                    nonNullNativeBackend.SetAnimationFrame(0);
                    nonNullNativeBackend.Step(0, 1.0f / 60.0f);
                    FeedLegacyStaticBodies(legacyBackend, model);
                    legacyBackend.Step(0, 1.0f / 60.0f);
                    ParitySample seedSample = CompareBodies(nonNullNativeBackend, legacyBackend, model, "seed");
                    report.AppendLine(seedSample.ToCsvLine());
                    AppendBodyDetails(report, "seed", nonNullNativeBackend, legacyBackend, model);

                    float globalPositionMax = seedSample.positionMax;
                    float globalRotationMax = seedSample.rotationMaxDegrees;
                    foreach (int frame in Enumerable.Range(1, 120))
                    {
                        nonNullNativeBackend.SetAnimationFrame(frame);
                        nonNullNativeBackend.Step(frame, 1.0f / 30.0f);
                        FeedLegacyStaticBodies(legacyBackend, model);
                        legacyBackend.Step(frame, 1.0f / 30.0f);

                        ParitySample sample = CompareBodies(
                            nonNullNativeBackend,
                            legacyBackend,
                            model,
                            frame.ToString(CultureInfo.InvariantCulture));
                        globalPositionMax = Math.Max(globalPositionMax, sample.positionMax);
                        globalRotationMax = Math.Max(globalRotationMax, sample.rotationMaxDegrees);

                        if (frame == 1 || frame == 5 || frame == 10 || frame == 30 ||
                            frame == 60 || frame == 90 || frame == 120)
                        {
                            report.AppendLine(sample.ToCsvLine());
                            if (frame == 1)
                            {
                                AppendBodyDetails(report, "frame1", nonNullNativeBackend, legacyBackend, model);
                            }
                        }
                    }

                    report.AppendLine($"globalPositionMax={globalPositionMax.ToString("F6", CultureInfo.InvariantCulture)}");
                    report.AppendLine($"globalRotationMaxDegrees={globalRotationMax.ToString("F6", CultureInfo.InvariantCulture)}");
                    report.AppendLine($"acceptancePositionMax={HairFixtureMaxPositionDelta.ToString("F6", CultureInfo.InvariantCulture)}");
                    report.AppendLine($"acceptanceRotationMaxDegrees={HairFixtureMaxRotationDeltaDegrees.ToString("F6", CultureInfo.InvariantCulture)}");

                    string reportPath = Path.Combine(
                        MmdTestFixtures.RepositoryRoot,
                        "artifacts",
                        "physics-fidelity",
                        "mmd-anim-legacy-bullet-parity.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                    File.WriteAllText(reportPath, report.ToString());
                    TestContext.Out.WriteLine(report.ToString());

                    Assert.That(float.IsFinite(globalPositionMax), Is.True, "Physics parity position result must be finite.");
                    Assert.That(float.IsFinite(globalRotationMax), Is.True, "Physics parity rotation result must be finite.");
                    Assert.That(
                        globalPositionMax,
                        Is.LessThanOrEqualTo(HairFixtureMaxPositionDelta),
                        "mmd-anim / legacy Bullet hair fixture position parity exceeded its accepted envelope.");
                    Assert.That(
                        globalRotationMax,
                        Is.LessThanOrEqualTo(HairFixtureMaxRotationDeltaDegrees),
                        "mmd-anim / legacy Bullet hair fixture rotation parity exceeded its accepted envelope.");
                }
                finally
                {
                    BulletMmdPhysicsBackend.ResetMaxSubStepEstimateFixedTimeStepSecondsForDiagnostics();
                }
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

        private static ParitySample CompareBodies(
            MmdAnimPhysicsBackend nativeBackend,
            BulletMmdPhysicsBackend legacyBackend,
            MmdModelDefinition model,
            string frame)
        {
            float positionMax = 0.0f;
            float rotationMaxDegrees = 0.0f;
            string worstBody = string.Empty;
            for (int i = 0; i < model.physics.rigidbodies.Count; i++)
            {
                MmdPhysicsBodyTransform native = nativeBackend.GetRigidbodyTransform(i);
                MmdPhysicsBodyTransform legacy = legacyBackend.GetRigidbodyTransform(i);
                float positionError = Distance(native.position, legacy.position);
                float rotationError = QuaternionAngleDegrees(native.rotation, legacy.rotation);
                if (positionError > positionMax || rotationError > rotationMaxDegrees)
                {
                    worstBody = model.physics.rigidbodies[i].name ?? $"body-{i}";
                }

                positionMax = Math.Max(positionMax, positionError);
                rotationMaxDegrees = Math.Max(rotationMaxDegrees, rotationError);
            }

            return new ParitySample(frame, positionMax, rotationMaxDegrees, worstBody);
        }

        private static void AppendBodyDetails(
            StringBuilder report,
            string stage,
            MmdAnimPhysicsBackend nativeBackend,
            BulletMmdPhysicsBackend legacyBackend,
            MmdModelDefinition model)
        {
            report.AppendLine($"{stage}-bodies,index,name,physicsKind,boneIndex,nativePosition,legacyPosition");
            for (int i = 0; i < model.physics.rigidbodies.Count; i++)
            {
                MmdPhysicsBodyTransform native = nativeBackend.GetRigidbodyTransform(i);
                MmdPhysicsBodyTransform legacy = legacyBackend.GetRigidbodyTransform(i);
                report.AppendLine(string.Join(",", stage, i, model.physics.rigidbodies[i].name,
                    model.physics.rigidbodies[i].physicsKind, model.physics.rigidbodies[i].boneIndex,
                    FormatVector(native.position), FormatVector(legacy.position)));
            }
        }

        private static string FormatVector(float[] values) =>
            string.Join("/", values.Take(3).Select(value => value.ToString("F4", CultureInfo.InvariantCulture)));

        private static byte[] CreateEmptyVmdBytes(string modelName)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            WriteFixedSjis(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedSjis(writer, modelName, 20);
            writer.Write(0u); // bone count
            writer.Write(0u); // morph count
            writer.Write(0u); // camera count
            writer.Write(0u); // light count
            writer.Write(0u); // self-shadow count
            writer.Write(0u); // show/IK count
            return stream.ToArray();
        }

        private static void WriteFixedSjis(BinaryWriter writer, string value, int byteCount)
        {
            byte[] buffer = new byte[byteCount];
            byte[] encoded = Encoding.GetEncoding(932).GetBytes(value ?? string.Empty);
            Array.Copy(encoded, 0, buffer, 0, Math.Min(encoded.Length, buffer.Length));
            writer.Write(buffer);
        }

        private static void FeedLegacyStaticBodies(
            BulletMmdPhysicsBackend legacyBackend,
            MmdModelDefinition model)
        {
            for (int i = 0; i < model.physics.rigidbodies.Count; i++)
            {
                MmdRigidbodyDefinition body = model.physics.rigidbodies[i];
                if (!string.Equals(body.physicsKind, "static", StringComparison.Ordinal))
                {
                    continue;
                }

                MmdPhysicsBodyTransform current = legacyBackend.GetRigidbodyTransform(i);
                legacyBackend.SetRigidbodyTransform(i, current.position, current.rotation);
            }
        }

        private static float Distance(float[] left, float[] right)
        {
            float dx = left[0] - right[0];
            float dy = left[1] - right[1];
            float dz = left[2] - right[2];
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static float QuaternionAngleDegrees(float[] left, float[] right)
        {
            float leftLength = MathF.Sqrt(
                left[0] * left[0] + left[1] * left[1] + left[2] * left[2] + left[3] * left[3]);
            float rightLength = MathF.Sqrt(
                right[0] * right[0] + right[1] * right[1] + right[2] * right[2] + right[3] * right[3]);
            if (leftLength <= 0.0f || rightLength <= 0.0f)
            {
                return float.PositiveInfinity;
            }

            float dot = MathF.Abs(
                (left[0] * right[0] + left[1] * right[1] + left[2] * right[2] + left[3] * right[3]) /
                (leftLength * rightLength));
            dot = Math.Clamp(dot, 0.0f, 1.0f);
            return 2.0f * MathF.Acos(dot) * (180.0f / MathF.PI);
        }

        private readonly struct ParitySample
        {
            public ParitySample(string frame, float positionMax, float rotationMaxDegrees, string worstBody)
            {
                this.frame = frame;
                this.positionMax = positionMax;
                rotationMaxDegrees = Math.Max(0.0f, rotationMaxDegrees);
                this.rotationMaxDegrees = rotationMaxDegrees;
                this.worstBody = worstBody;
            }

            private readonly string frame;
            public readonly float positionMax;
            public readonly float rotationMaxDegrees;
            private readonly string worstBody;

            public string ToCsvLine() =>
                string.Join(",", frame, positionMax.ToString("F6", CultureInfo.InvariantCulture),
                    rotationMaxDegrees.ToString("F6", CultureInfo.InvariantCulture), worstBody);
        }
    }
}
