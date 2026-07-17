#nullable enable

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Mmd.Motion;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Rendering;
using Mmd.Tracing;

namespace Mmd.Tests
{
    public sealed class PackageImportSmokeTests
    {
        [Test]
        public void RuntimeAssemblyExposesPackageIdentity()
        {
            Assert.That(MmdRuntimeInfo.PackageName, Is.EqualTo("com.yohawing.mmd-loader"));
            Assert.That(MmdRuntimeInfo.RuntimeBaseline, Is.EqualTo("Phase1"));
        }

        [Test]
        public void BasicPlaybackSampleAssetsParse()
        {
            string sampleRoot = Path.Combine(MmdTestFixtures.PackageRoot, "Samples~", "BasicPlayback", "Assets");
            string pmxPath = Path.Combine(sampleRoot, "mmt_test_model.pmx");
            string vmdPath = Path.Combine(sampleRoot, "mmt_test_model_test_motion.vmd");
            string timelinePath = Path.Combine(sampleRoot, "BasicSampleTimeline.playable");
            string scenePath = Path.Combine(sampleRoot, "BasicPlayback.unity");
            Assert.That(pmxPath, Does.Exist, "BasicPlayback sample PMX must be bundled.");
            Assert.That(vmdPath, Does.Exist, "BasicPlayback sample VMD must be bundled.");
            Assert.That(timelinePath, Does.Exist, "BasicPlayback sample Timeline must be bundled.");
            Assert.That(scenePath, Does.Exist, "ready-to-play BasicPlayback scene must be bundled.");
            Assert.That(File.ReadAllText(timelinePath), Does.Contain("MMT Test Motion (VMD)"),
                "BasicPlayback Timeline must contain the bundled VMD motion.");
            Assert.That(File.ReadAllText(scenePath), Does.Contain("Basic Timeline"),
                "BasicPlayback scene must retain the Timeline director object.");
            Assert.That(File.ReadAllText(scenePath), Does.Contain("m_InitialState: 1"),
                "BasicPlayback Timeline must play automatically when the scene starts.");

            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(File.ReadAllBytes(pmxPath));
            MmdMotionDefinition motion = parser.LoadMotion(File.ReadAllBytes(vmdPath));

            Assert.That(model.vertices.Count, Is.GreaterThan(0), "sample PMX vertices");
            Assert.That(model.bones.Count, Is.GreaterThan(0), "sample PMX bones");
            Assert.That(model.materials.Count, Is.GreaterThan(0), "sample PMX materials");
            Assert.That(motion.boneKeyframes.Count, Is.GreaterThan(0), "sample VMD bone keyframes");
        }

        [Test]
        public void HumanoidPlaybackSampleBundlesSourceAndGeneratedWorkflow()
        {
            string sampleRoot = Path.Combine(MmdTestFixtures.PackageRoot, "Samples~", "HumanoidPlayback");
            string assetsRoot = Path.Combine(sampleRoot, "Assets");
            string pmxPath = Path.Combine(assetsRoot, "HumanoidSampleModel.pmx");
            string fbxPath = Path.Combine(assetsRoot, "TaisouMocap.fbx");
            string fbxMetaPath = fbxPath + ".meta";
            string pmxMetaPath = pmxPath + ".meta";
            string timelinePath = Path.Combine(assetsRoot, "HumanoidSampleTimeline.playable");
            string scenePath = Path.Combine(assetsRoot, "HumanoidPlayback.unity");
            string provenancePath = Path.Combine(sampleRoot, "ASSET_PROVENANCE.md");

            Assert.That(pmxPath, Does.Exist, "Humanoid sample PMX source");
            Assert.That(fbxPath, Does.Exist, "Humanoid sample FBX motion");
            Assert.That(timelinePath, Does.Exist, "Humanoid Timeline Asset");
            Assert.That(scenePath, Does.Exist, "ready-to-play Humanoid scene");
            Assert.That(provenancePath, Does.Exist, "redistribution provenance");
            const string expectedFbxSha256 = "972E72E6AF8B0C7D32B4762EB7180395BCF6BAC03ADCF5BE27EA95AE8B655753";
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(fbxPath))
            {
                string actualHash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
                Assert.That(actualHash, Is.EqualTo(expectedFbxSha256), "provenance must identify the bundled FBX bytes");
            }
            string provenance = Regex.Replace(File.ReadAllText(provenancePath), @"\s+", " ");
            Assert.That(provenance, Does.Contain(expectedFbxSha256));
            Assert.That(provenance, Does.Contain("39.733-second"));
            Assert.That(provenance, Does.Contain("only for conversion-contract checks"));
            Assert.That(File.ReadAllText(pmxMetaPath), Does.Contain("animationType: 2"),
                "sample PMX importer metadata must request Humanoid");
            Assert.That(File.ReadAllText(fbxMetaPath), Does.Contain("animationType: 3"),
                "sample FBX importer metadata must request Humanoid animation");
            string timeline = File.ReadAllText(timelinePath);
            Assert.That(timeline, Does.Contain("m_Duration: 39.73333740234375"),
                "Timeline must retain the full practical motion duration");
            Assert.That(timeline, Does.Contain("Taisou Mocap (FBX)"),
                "Timeline must use the practical FBX motion rather than the short VMD fixture bake");
            Assert.That(timeline, Does.Contain("guid: f63f3dd549969bd4c89c90a26054020c"),
                "Timeline clip must reference TaisouMocap.fbx");
            Assert.That(timeline, Does.Not.Contain("proxyAnimator:"),
                "Humanoid Timeline clips must derive their Animator from the track-bound playback controller");
            string scene = File.ReadAllText(scenePath);
            Assert.That(scene, Does.Contain("Humanoid Timeline"),
                "scene must retain the Timeline director object");
            Assert.That(scene, Does.Contain("m_PlayableAsset: {fileID: 11400000, guid: 14448a896038bd649a6f42098dcdb42d, type: 2}"),
                "scene director must reference the bundled Timeline");
            Assert.That(scene, Does.Contain("m_InitialState: 1"),
                "ready-to-play Timeline must start automatically");

            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(File.ReadAllBytes(pmxPath));
            Assert.That(model.vertices.Count, Is.GreaterThan(0), "Humanoid sample PMX vertices");
            Assert.That(model.bones.Count, Is.GreaterThan(0), "Humanoid sample PMX bones");
        }

        [Test]
        public void ActualTraceDumperCreatesSchemaVersionOneJson()
        {
            MmdTrace trace = MmdActualTraceDumper.CreateTrace("minimal.pmx", "minimal.vmd");

            string json = MmdActualTraceDumper.ToJson(trace, prettyPrint: false);

            Assert.That(json, Does.Contain("\"schemaVersion\":1"));
            Assert.That(json, Does.Contain("\"model\":\"minimal.pmx\""));
            Assert.That(json, Does.Contain("\"motion\":\"minimal.vmd\""));
            Assert.That(json, Does.Contain("\"space\":\"mmd\""));
        }

        [Test]
        public void NullPhysicsBackendIsDeterministicNoOp()
        {
            IMmdPhysicsBackend backend = new NullMmdPhysicsBackend();

            backend.Reset();
            backend.Step(frame: 0, deltaTime: 0.0f);

            Assert.That(backend.Name, Is.EqualTo("Null"));
            Assert.That(backend.IsDeterministic, Is.True);
        }

        [Test]
        public void PhysicsDescriptorValidatorRejectsUnsupportedRigidbodyShape()
        {
            var model = new MmdModelDefinition();
            model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
            {
                index = 0,
                name = "bad shape",
                shapeType = "cube",
                size = new[] { 1.0f, 1.0f, 1.0f },
                position = new[] { 0.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f },
                mass = 1.0f,
                linearDamping = 0.0f,
                angularDamping = 0.0f,
                friction = 0.5f,
                restitution = 0.0f,
                group = 0,
                mask = 0xffff,
                physicsKind = "dynamic"
            });

            string[] errors = MmdPhysicsDescriptorValidator.Validate(model).ToArray();

            Assert.That(errors, Does.Contain("rigidbody shapeType is unsupported: 0 -> cube"));
        }

        [Test]
        public void PhysicsDescriptorValidatorRejectsJointWithBothWorldAnchorEndpoints()
        {
            var model = new MmdModelDefinition();
            model.physics.joints.Add(new MmdJointDefinition
            {
                index = 7,
                name = "bad-world-anchor",
                rigidbodyAIndex = -1,
                rigidbodyBIndex = -1,
                position = new[] { 0.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f },
                linearLowerLimit = new[] { 0.0f, 0.0f, 0.0f },
                linearUpperLimit = new[] { 0.0f, 0.0f, 0.0f },
                angularLowerLimit = new[] { 0.0f, 0.0f, 0.0f },
                angularUpperLimit = new[] { 0.0f, 0.0f, 0.0f },
                linearSpring = new[] { 0.0f, 0.0f, 0.0f },
                angularSpring = new[] { 0.0f, 0.0f, 0.0f }
            });

            string[] errors = MmdPhysicsDescriptorValidator.Validate(model).ToArray();

            Assert.That(errors, Does.Contain("joint has both rigidbody endpoints set to -1 (unsupported world-anchored joint): 7"));
        }

        [Test]
        public void VmdBoneSamplerUsesNextKeyframeInterpolationCurve()
        {
            var keyframes = new[]
            {
                new MmdBoneKeyframeDefinition
                {
                    boneName = "root",
                    frame = 0,
                    rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                    interpolation = new MmdBoneInterpolationDefinition
                    {
                        rotation = new byte[] { 0, 20, 107, 107 }
                    }
                },
                new MmdBoneKeyframeDefinition
                {
                    boneName = "root",
                    frame = 9,
                    rotation = new[] { -0.38268337f, 0.0f, 0.0f, 0.92387956f },
                    interpolation = new MmdBoneInterpolationDefinition
                    {
                        rotation = new byte[] { 0, 0, 85, 127 }
                    }
                }
            };

            MmdBonePoseSample sample = VmdBoneSampler.SamplePose(keyframes, "root", frame: 1.0f);

            Assert.That(sample.Rotation[0], Is.EqualTo(-0.06206015f).Within(0.00001f));
            Assert.That(sample.Rotation[3], Is.EqualTo(0.99807227f).Within(0.00001f));
        }

        [Test]
        public void RuntimePipelineRunsCcdIkByDefault()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f }
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1,
                name = "link",
                parentIndex = 0,
                origin = new[] { 1.0f, 0.0f, 0.0f }
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 2,
                name = "effector",
                parentIndex = 1,
                origin = new[] { 2.0f, 0.0f, 0.0f }
            });
            model.bones.Add(new MmdBoneDefinition
            {
                index = 3,
                name = "target",
                parentIndex = 0,
                origin = new[] { 1.0f, 1.0f, 0.0f }
            });
            model.ik.Add(new MmdIkDefinition
            {
                boneIndex = 3,
                targetBoneIndex = 2,
                iterationCount = 1,
                angleLimit = 1.0f,
                links =
                {
                    new MmdIkLinkDefinition { boneIndex = 1 }
                }
            });

            MmdTrace trace = MmdRuntimeTraceEvaluator.EvaluatePhaseOneTrace(
                model,
                new MmdMotionDefinition(),
                frame: 0,
                time: 0.0f,
                modelId: "ik-model.pmx",
                motionId: "empty.vmd");

            MmdTraceBone appendLink = trace.frames.Single(frame => frame.checkpoint == MmdTraceCheckpoints.AfterAppendTransform).bones.Single(bone => bone.name == "link");
            MmdTraceBone ikLink = trace.frames.Single(frame => frame.checkpoint == MmdTraceCheckpoints.AfterIk).bones.Single(bone => bone.name == "link");
            Assert.That(ikLink.localRotation[2], Is.Not.EqualTo(appendLink.localRotation[2]).Within(0.00001f));
        }

        [Test]
        public void FrameEvaluatorEmitsRenderingFacingSnapshot()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            byte[] vmdBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");

            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(pmxBytes);
            MmdMotionDefinition motion = parser.LoadMotion(vmdBytes);

            MmdEvaluatedFrame frame = MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrame(model, motion, frame: 0, time: 0.0f);

            Assert.That(frame.bones, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(frame.bones[0].worldMatrix, Has.Length.EqualTo(16));
            Assert.That(frame.bones[0].localPosition, Has.Length.GreaterThanOrEqualTo(3));
            Assert.That(frame.bones[0].localRotation, Has.Length.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void RenderingDescriptorAllowsDuplicateMorphNamesForLocalCompatibility()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f }
            });
            model.vertices.Add(new MmdVertexDefinition
            {
                index = 0,
                position = new[] { 0.0f, 0.0f, 0.0f },
                normal = new[] { 0.0f, 1.0f, 0.0f },
                uv = new[] { 0.0f, 0.0f },
                boneIndices = new[] { 0 },
                boneWeights = new[] { 1.0f }
            });
            model.indices.AddRange(new[] { 0, 0, 0 });
            model.materials.Add(new MmdMaterialDefinition { index = 0, name = "mat", vertexCount = 3 });
            model.morphs.Add(new MmdMorphDefinition { index = 0, name = "duplicate", type = "vertex" });
            model.morphs.Add(new MmdMorphDefinition { index = 1, name = "duplicate", type = "vertex" });

            MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);

            Assert.That(descriptor.vertexMorphs, Has.Count.EqualTo(2));
            Assert.That(descriptor.vertexMorphs[0].morphName, Is.EqualTo("duplicate"));
            Assert.That(descriptor.vertexMorphs[1].morphName, Is.EqualTo("duplicate"));
        }

        [Test]
        public void PlaybackSnapshotCombinesFrameAndRenderingInputs()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadPlaybackFixturePair();

            MmdPlaybackSnapshot snapshot = MmdPlaybackSnapshotBuilder.BuildPhaseOneSnapshot(model, motion, frame: 0, time: 0.0f);

            Assert.That(snapshot.frame.bones, Has.Count.GreaterThan(0));
            Assert.That(snapshot.frame.bones[0].worldMatrix, Has.Length.EqualTo(16));
            Assert.That(snapshot.rendering.vertices, Has.Count.GreaterThan(0));
            Assert.That(snapshot.rendering.indices, Has.Count.GreaterThan(0));
            Assert.That(snapshot.rendering.materials, Has.Count.GreaterThan(0));
            Assert.That(snapshot.rendering.urpMaterialBindings, Has.Count.EqualTo(snapshot.rendering.materials.Count));
        }

        [Test]
        public void RuntimeSessionEvaluatesTraceAndSnapshotFromNeutralIr()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadPlaybackFixturePair();
            var session = new MmdRuntimeSession(
                model,
                motion,
                "test_1bone_cube.pmx",
                "test_1bone_cube_motion.vmd");

            Assert.That(session.EvaluateTrace(frame: 0, time: 0.0f).frames, Has.Count.EqualTo(5));
            MmdPlaybackSnapshot snapshot = session.BuildSnapshot(frame: 0, time: 0.0f);
            Assert.That(snapshot.rendering.indices, Has.Count.GreaterThan(0));
            Assert.That(snapshot.frame.bones, Has.Count.GreaterThan(0));
            Assert.That(snapshot.frame.bones[0].worldMatrix, Has.Length.EqualTo(16));
        }

        [Test]
        public void RuntimeSessionBuildSnapshotAppliesCustomIkSolver()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadPlaybackFixturePair();
            string targetBoneName = model.bones[0].name;
            var session = new MmdRuntimeSession(
                model,
                motion,
                "test_1bone_cube.pmx",
                "test_1bone_cube_motion.vmd");

            MmdPlaybackSnapshot baseline = session.BuildSnapshot(frame: 0, time: 0.0f);
            MmdPlaybackSnapshot adjusted = session.BuildSnapshot(
                frame: 0,
                time: 0.0f,
                ikSolver: new TestOffsetIkSolver(targetBoneName, 2.0f));

            Assert.That(adjusted.frame.bones[0].localPosition[0], Is.EqualTo(baseline.frame.bones[0].localPosition[0] + 2.0f).Within(0.00001f));
            Assert.That(adjusted.frame.bones[0].worldMatrix[3], Is.EqualTo(baseline.frame.bones[0].worldMatrix[3] + 2.0f).Within(0.00001f));
        }

        [Test]
        public void RuntimeSessionBuildSnapshotsWithCustomIkSolverDoesNotRequireNativeSourceBytes()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f }
            });
            var motion = new MmdMotionDefinition();
            var session = new MmdRuntimeSession(model, motion, "synthetic.pmx", "synthetic.vmd");

            IReadOnlyList<MmdPlaybackSnapshot> snapshots = session.BuildSnapshots(
                new[] { 0, 1 },
                frameRate: 30.0f,
                ikSolver: new TestOffsetIkSolver("root", 2.0f));

            Assert.That(snapshots, Has.Count.EqualTo(2));
            Assert.That(snapshots[0].frame.bones[0].localPosition[0], Is.EqualTo(2.0f).Within(0.00001f));
            Assert.That(snapshots[1].frame.bones[0].localPosition[0], Is.EqualTo(2.0f).Within(0.00001f));
        }

        private static (MmdModelDefinition Model, MmdMotionDefinition Motion) LoadPlaybackFixturePair()
        {
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx"));
            MmdMotionDefinition motion = parser.LoadMotion(MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd"));
            return (model, motion);
        }

        private sealed class TestOffsetIkSolver : IMmdIkSolver
        {
            private readonly string boneName;
            private readonly float offsetX;

            public TestOffsetIkSolver(string boneName, float offsetX)
            {
                this.boneName = boneName;
                this.offsetX = offsetX;
            }

            public string Name => "TestOffsetIkSolver";

            public MmdSampledMotion Solve(MmdModelDefinition model, MmdSampledMotion? sampledMotion)
            {
                MmdSampledMotion result = CopyMotion(sampledMotion);
                MmdBonePoseSample source = result.Bones.TryGetValue(boneName, out MmdBonePoseSample found)
                    ? found
                    : MmdBonePoseSample.Identity;
                result.Bones[boneName] = new MmdBonePoseSample(
                    new[] { source.Translation[0] + offsetX, source.Translation[1], source.Translation[2] },
                    source.Rotation);
                return result;
            }

            private static MmdSampledMotion CopyMotion(MmdSampledMotion? source)
            {
                var result = new MmdSampledMotion();
                if (source == null)
                {
                    return result;
                }

                foreach (KeyValuePair<string, MmdBonePoseSample> bone in source.Bones)
                {
                    result.Bones[bone.Key] = bone.Value;
                }

                foreach (KeyValuePair<string, float> morph in source.Morphs)
                {
                    result.Morphs[morph.Key] = morph.Value;
                }

                foreach (KeyValuePair<string, bool> ikState in source.IkStates)
                {
                    result.IkStates[ikState.Key] = ikState.Value;
                }

                return result;
            }
        }

        [Test]
        public void GroupMorphOffsetsWithValidTargetsPassValidation()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f }
            });
            model.vertices.Add(new MmdVertexDefinition
            {
                index = 0,
                position = new[] { 0.0f, 0.0f, 0.0f },
                normal = new[] { 0.0f, 1.0f, 0.0f },
                uv = new[] { 0.0f, 0.0f },
                boneIndices = new[] { 0 },
                boneWeights = new[] { 1.0f }
            });
            model.indices.AddRange(new[] { 0, 0, 0 });
            model.materials.Add(new MmdMaterialDefinition { index = 0, name = "mat", vertexCount = 3 });

            // Create a vertex morph target
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "target-morph",
                type = "vertex",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition { vertexIndex = 0, positionDelta = new[] { 0.0f, 0.1f, 0.0f } }
                }
            });

            // Create a group morph that references target-morph
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "group-morph",
                type = "group",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 0, weight = 0.5f }
                }
            });

            // Should not throw
            Assert.DoesNotThrow(() => MmdModelValidator.ThrowIfInvalid(model));
        }

        [Test]
        public void GroupMorphOffsetsSelfReferenceRejected()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f }
            });
            model.vertices.Add(new MmdVertexDefinition
            {
                index = 0,
                position = new[] { 0.0f, 0.0f, 0.0f },
                normal = new[] { 0.0f, 1.0f, 0.0f },
                uv = new[] { 0.0f, 0.0f },
                boneIndices = new[] { 0 },
                boneWeights = new[] { 1.0f }
            });
            model.indices.AddRange(new[] { 0, 0, 0 });
            model.materials.Add(new MmdMaterialDefinition { index = 0, name = "mat", vertexCount = 3 });

            // Group morph referencing itself
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "self-ref-group",
                type = "group",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 0, weight = 1.0f }
                }
            });

            var errors = MmdModelValidator.ValidateStructuralModel(model);
            Assert.That(errors, Has.Some.Matches<string>(msg => msg.Contains("self-reference")));
        }

        [Test]
        public void GroupMorphOffsetsNonExistentTargetRejected()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f }
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "bad-group",
                type = "group",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 99, weight = 0.5f }
                }
            });

            var errors = MmdModelValidator.ValidateStructuralModel(model);
            Assert.That(errors, Has.Some.Matches<string>(msg => msg.Contains("does not exist")));
        }

        [Test]
        public void GroupMorphOffsetsNonFiniteWeightRejected()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f }
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "target-morph",
                type = "vertex"
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "group-morph",
                type = "group",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 0, weight = float.NaN }
                }
            });

            var errors = MmdModelValidator.ValidateStructuralModel(model);
            Assert.That(errors, Has.Some.Matches<string>(msg => msg.Contains("weight must be finite")));
        }

        [Test]
        public void MorphTypeInventoryExposesGroupOffsetCount()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f }
            });
            model.vertices.Add(new MmdVertexDefinition
            {
                index = 0,
                position = new[] { 0.0f, 0.0f, 0.0f },
                normal = new[] { 0.0f, 1.0f, 0.0f },
                uv = new[] { 0.0f, 0.0f },
                boneIndices = new[] { 0 },
                boneWeights = new[] { 1.0f }
            });
            model.indices.AddRange(new[] { 0, 0, 0 });
            model.materials.Add(new MmdMaterialDefinition { index = 0, name = "mat", vertexCount = 3 });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "target",
                type = "vertex",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition { vertexIndex = 0, positionDelta = new[] { 0.0f, 0.1f, 0.0f } }
                }
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "group",
                type = "group",
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 0, weight = 0.5f },
                    new MmdGroupMorphOffsetDefinition { morphIndex = 0, weight = 1.0f }
                }
            });

            IReadOnlyList<MmdMorphTypeInventoryDescriptor> inventory = MmdMorphDescriptorBuilder.BuildTypeInventory(model);

            MmdMorphTypeInventoryDescriptor groupRecord = inventory.Single(record => record.morphType == "group");
            Assert.That(groupRecord.groupOffsetCount, Is.EqualTo(2));
            Assert.That(groupRecord.vertexOffsetCount, Is.EqualTo(0));
            Assert.That(groupRecord.normalizedFamily, Is.EqualTo("composite"));
            Assert.That(groupRecord.supportStatus, Is.EqualTo(MmdMorphDescriptorBuilder.InventoryOnlyStatus));
        }

    }
}
