using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Yohawing.MmdUnity.Motion;
using Yohawing.MmdUnity.Parser;
using Yohawing.MmdUnity.Physics;
using Yohawing.MmdUnity.Rendering;
using Yohawing.MmdUnity.Tracing;

namespace Yohawing.MmdUnity.Tests
{
    public sealed class PackageImportSmokeTests
    {
        [Test]
        public void RuntimeAssemblyExposesPackageIdentity()
        {
            Assert.That(MmdRuntimeInfo.PackageName, Is.EqualTo("com.yohawing.mmd-unity"));
            Assert.That(MmdRuntimeInfo.RuntimeBaseline, Is.EqualTo("Phase1"));
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
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                origin = new[] { 0.0f, 0.0f, 0.0f }
            });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "mat",
                texture = "mat.png",
                vertexCount = 3
            });

            var motion = new MmdMotionDefinition();
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = 0,
                translation = new[] { 1.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = LinearInterpolation()
            });

            MmdEvaluatedFrame frame = MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrame(model, motion, frame: 0, time: 0.0f);

            Assert.That(frame.bones, Has.Count.EqualTo(1));
            Assert.That(frame.bones[0].worldMatrix[3], Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(frame.materials, Has.Count.EqualTo(1));
            Assert.That(frame.materials[0].texture, Is.EqualTo("mat.png"));
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

            var motion = new MmdMotionDefinition();
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = 0,
                translation = new[] { 1.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = LinearInterpolation()
            });

            MmdPlaybackSnapshot snapshot = MmdPlaybackSnapshotBuilder.BuildPhaseOneSnapshot(model, motion, frame: 0, time: 0.0f);

            Assert.That(snapshot.frame.bones, Has.Count.EqualTo(1));
            Assert.That(snapshot.rendering.vertices, Has.Count.EqualTo(1));
            Assert.That(snapshot.rendering.indices, Has.Count.EqualTo(3));
            Assert.That(snapshot.rendering.materials, Has.Count.EqualTo(1));
            Assert.That(snapshot.rendering.urpMaterialBindings, Has.Count.EqualTo(1));
        }

        [Test]
        public void RuntimeSessionEvaluatesTraceAndSnapshotFromNeutralIr()
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

            var motion = new MmdMotionDefinition();
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = 0,
                translation = new[] { 1.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = LinearInterpolation()
            });

            var session = new MmdRuntimeSession(model, motion, "model.pmx", "motion.vmd");

            Assert.That(session.EvaluateTrace(frame: 0, time: 0.0f).frames, Has.Count.EqualTo(5));
            Assert.That(session.BuildSnapshot(frame: 0, time: 0.0f).rendering.indices, Has.Count.EqualTo(3));
            Assert.That(
                session.BuildSnapshot(
                    frame: 0,
                    time: 0.0f,
                    physicsBackend: null,
                    ikSolver: new TestOffsetIkSolver("root", 1.0f)).frame.bones[0].localPosition[0],
                Is.EqualTo(2.0f).Within(0.00001f));
        }

        private static MmdBoneInterpolationDefinition LinearInterpolation()
        {
            byte[] linear = { 20, 20, 107, 107 };
            return new MmdBoneInterpolationDefinition
            {
                translationX = linear,
                translationY = linear,
                translationZ = linear,
                rotation = linear
            };
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

        private sealed class TestOffsetIkSolver : IMmdIkSolver
        {
            private readonly string boneName;
            private readonly float xOffset;

            public TestOffsetIkSolver(string boneName, float xOffset)
            {
                this.boneName = boneName;
                this.xOffset = xOffset;
            }

            public string Name => "TestOffset";

            public MmdSampledMotion Solve(MmdModelDefinition model, MmdSampledMotion? sampledMotion)
            {
                if (model == null)
                {
                    throw new ArgumentNullException(nameof(model));
                }

                var result = new MmdSampledMotion();
                if (sampledMotion != null)
                {
                    foreach (var bone in sampledMotion.Bones.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    {
                        result.Bones[bone.Key] = bone.Value;
                    }

                    foreach (var morph in sampledMotion.Morphs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    {
                        result.Morphs[morph.Key] = morph.Value;
                    }

                    foreach (var ikState in sampledMotion.IkStates.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    {
                        result.IkStates[ikState.Key] = ikState.Value;
                    }
                }

                if (result.Bones.TryGetValue(boneName, out MmdBonePoseSample pose))
                {
                    result.Bones[boneName] = new MmdBonePoseSample(
                        new[] { pose.Translation[0] + xOffset, pose.Translation[1], pose.Translation[2] },
                        pose.Rotation);
                }

                return result;
            }
        }
    }
}
