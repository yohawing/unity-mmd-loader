#nullable enable

using System;
using NUnit.Framework;
using Mmd.Parser;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class NativeMmdParserPmxSourceContractTests
    {
        [Test]
        public void BuildModelDefinitionUsesNonJsonSnapshotWithoutNativeInvocation()
        {
            var snapshot = new NativeMmdParser.PmxModelSourceSnapshot
            {
                metadata = new NativeMmdParser.PmxModelSourceMetadata
                {
                    name = "pmx-source",
                    englishName = "pmx-source-en",
                    comment = "日本語コメント",
                    englishComment = "English comment"
                },
                geometry = new NativeMmdParser.PmxModelSourceGeometry
                {
                    positions = new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f },
                    normals = new[] { 0.0f, 1.0f, 0.0f, float.NaN, 0.0f, 1.0f },
                    uvs = new[] { 0.25f, 0.5f, 0.75f, 1.0f },
                    indices = new uint[] { 0, 1, 0 },
                    skinningModes = new[] { "bdef2", "sdef" },
                    skinIndices = new uint[] { 0, 1, 0, 0, 1, 2, 0, 0 },
                    skinWeights = new[] { 0.65f, 0.35f, 0.0f, 0.0f, 0.7f, 0.3f, 0.0f, 0.0f },
                    hasSdefParameters = new[] { false, true },
                    sdefC = new[] { 0.0f, 0.0f, 0.0f, 0.1f, 0.2f, 0.3f },
                    sdefR0 = new[] { 0.0f, 0.0f, 0.0f, 0.4f, 0.5f, 0.6f },
                    sdefR1 = new[] { 0.0f, 0.0f, 0.0f, 0.7f, 0.8f, 0.9f }
                },
                skeleton = new NativeMmdParser.PmxModelSourceSkeleton
                {
                    bones = new[]
                    {
                        new NativeMmdParser.PmxModelSourceBone
                        {
                            name = "root",
                            parentIndex = -1,
                            layer = 0,
                            position = new[] { 0.0f, 1.0f, 2.0f },
                            flags = new NativeMmdParser.PmxModelSourceBoneFlags
                            {
                                rotatable = true,
                                translatable = true
                            }
                        },
                        new NativeMmdParser.PmxModelSourceBone
                        {
                            name = "append",
                            parentIndex = 0,
                            layer = 2,
                            position = new[] { 1.0f, 2.0f, 3.0f },
                            flags = new NativeMmdParser.PmxModelSourceBoneFlags
                            {
                                appendRotate = true,
                                appendLocal = true,
                                externalParentTransform = true,
                                transformAfterPhysics = true
                            },
                            appendTransform = new NativeMmdParser.PmxModelSourceAppendTransform
                            {
                                parentIndex = 0,
                                weight = 0.5f
                            },
                            fixedAxis = new[] { 1.0f, 0.0f, 0.0f },
                            localAxis = new NativeMmdParser.PmxModelSourceLocalAxis
                            {
                                x = new[] { 1.0f, 0.0f, 0.0f },
                                z = new[] { 0.0f, 0.0f, 1.0f }
                            },
                            externalParentKey = 12
                        },
                        new NativeMmdParser.PmxModelSourceBone
                        {
                            name = "leg_ik",
                            parentIndex = 0,
                            layer = 3,
                            position = new[] { 2.0f, 3.0f, 4.0f },
                            ik = new NativeMmdParser.PmxModelSourceIk
                            {
                                targetIndex = 1,
                                loopCount = 8,
                                limitAngle = 0.25f,
                                links = new[]
                                {
                                    new NativeMmdParser.PmxModelSourceIkLink
                                    {
                                        boneIndex = 0,
                                        limits = new NativeMmdParser.PmxModelSourceIkLimit
                                        {
                                            lower = new[] { -0.1f, -0.2f, -0.3f },
                                            upper = new[] { 0.1f, 0.2f, 0.3f }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                morphs = new[]
                {
                    new NativeMmdParser.PmxModelSourceMorph
                    {
                        name = "smile",
                        type = "vertex",
                        panel = "brow",
                        vertexOffsets = new[]
                        {
                            new NativeMmdParser.PmxModelSourceVertexMorphOffset
                            {
                                vertexIndex = 1,
                                position = new[] { 0.01f, 0.02f, 0.03f }
                            }
                        },
                        groupOffsets = new[]
                        {
                            new NativeMmdParser.PmxModelSourceGroupMorphOffset { morphIndex = 0, weight = 0.5f }
                        },
                        boneOffsets = new[]
                        {
                            new NativeMmdParser.PmxModelSourceBoneMorphOffset
                            {
                                boneIndex = 1,
                                translation = new[] { 0.1f, 0.2f, 0.3f },
                                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f }
                            }
                        },
                        uvOffsets = new[]
                        {
                            new NativeMmdParser.PmxModelSourceUvMorphOffset
                            {
                                vertexIndex = 0,
                                uv = new[] { 0.1f, 0.2f, 0.3f, 0.4f }
                            }
                        },
                        materialOffsets = new[]
                        {
                            new NativeMmdParser.PmxModelSourceMaterialMorphOffset
                            {
                                materialIndex = 0,
                                operation = "multiply",
                                diffuse = new[] { 0.8f, 0.7f, 0.6f, 0.5f },
                                ambient = new[] { 0.1f, 0.2f, 0.3f },
                                specular = new[] { 0.4f, 0.5f, 0.6f },
                                specularPower = 7.0f,
                                edgeColor = new[] { 0.2f, 0.3f, 0.4f, 0.5f },
                                edgeSize = 0.15f,
                                textureFactor = new[] { 0.1f, 0.2f, 0.3f, 0.4f },
                                sphereTextureFactor = new[] { 0.5f, 0.6f, 0.7f, 0.8f },
                                toonTextureFactor = new[] { 0.9f, 1.0f, 0.8f, 0.7f }
                            }
                        },
                        flipOffsets = new[]
                        {
                            new NativeMmdParser.PmxModelSourceGroupMorphOffset { morphIndex = 0, weight = 0.25f }
                        },
                        impulseOffsets = new[]
                        {
                            new NativeMmdParser.PmxModelSourceImpulseMorphOffset
                            {
                                rigidBodyIndex = 0,
                                local = true,
                                velocity = new[] { 1.0f, 2.0f, 3.0f },
                                torque = new[] { 4.0f, 5.0f, 6.0f }
                            }
                        }
                    }
                },
                materials = new[]
                {
                    new NativeMmdParser.PmxModelSourceMaterial
                    {
                        name = "mat",
                        texturePath = "diffuse.png",
                        sphereTexturePath = "sphere.spa",
                        sphereMode = "add",
                        toonTexturePath = "toon.bmp",
                        sharedToonIndex = 3,
                        diffuse = new[] { 1.2f, -0.1f, 0.5f, 0.75f },
                        ambient = new[] { 0.1f, 0.2f, 0.3f },
                        edgeColor = new[] { 0.1f, 0.2f, 0.3f, 2.0f },
                        edgeSize = 0.25f,
                        flags = new NativeMmdParser.PmxModelSourceMaterialFlags
                        {
                            doubleSided = true,
                            edge = true
                        },
                        faceCount = 2
                    }
                },
                rigidBodies = new[]
                {
                    new NativeMmdParser.PmxModelSourceRigidBody
                    {
                        name = "body",
                        boneIndex = 1,
                        group = 2,
                        mask = 65535,
                        shape = "sphere",
                        size = new[] { 0.1f, 0.2f, 0.3f },
                        position = new[] { 1.0f, 2.0f, 3.0f },
                        rotation = new[] { 4.0f, 5.0f, 6.0f },
                        mass = 1.5f,
                        linearDamping = 0.1f,
                        angularDamping = 0.2f,
                        restitution = 0.3f,
                        friction = 0.4f,
                        mode = "dynamic"
                    }
                },
                joints = new[]
                {
                    new NativeMmdParser.PmxModelSourceJoint
                    {
                        name = "joint",
                        rigidBodyIndexA = 0,
                        rigidBodyIndexB = 0,
                        position = new[] { 0.1f, 0.2f, 0.3f },
                        rotation = new[] { 0.4f, 0.5f, 0.6f },
                        translationLowerLimit = new[] { -1.0f, -2.0f, -3.0f },
                        translationUpperLimit = new[] { 1.0f, 2.0f, 3.0f },
                        rotationLowerLimit = new[] { -0.1f, -0.2f, -0.3f },
                        rotationUpperLimit = new[] { 0.1f, 0.2f, 0.3f },
                        springTranslationFactor = new[] { 7.0f, 8.0f, 9.0f },
                        springRotationFactor = new[] { 10.0f, 11.0f, 12.0f }
                    }
                }
            };

            MmdModelDefinition model = NativeMmdParser.BuildModelDefinition(snapshot);

            Assert.That(model.name, Is.EqualTo("pmx-source"));
            Assert.That(model.englishName, Is.EqualTo("pmx-source-en"));
            Assert.That(model.comment, Is.EqualTo("日本語コメント"));
            Assert.That(model.englishComment, Is.EqualTo("English comment"));
            Assert.That(model.vertices, Has.Count.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 1.0f, 2.0f, 3.0f }, model.vertices[0].position);
            CollectionAssert.AreEqual(new[] { 0.0f, 1.0f, 0.0f }, model.vertices[1].normal);
            Assert.That(model.vertices[1].skinningMode, Is.EqualTo("sdef"));
            CollectionAssert.AreEqual(new[] { 1, 2 }, model.vertices[1].boneIndices);
            Assert.That(model.vertices[1].hasSdefParameters, Is.True);
            CollectionAssert.AreEqual(new[] { 0.1f, 0.2f, 0.3f }, model.vertices[1].sdefC);
            CollectionAssert.AreEqual(new[] { 0, 1, 0 }, model.indices);

            Assert.That(model.bones, Has.Count.EqualTo(3));
            Assert.That(model.bones[0].isMovable, Is.True);
            Assert.That(model.bones[0].isRotatable, Is.True);
            Assert.That(model.bones[1].appendParentIndex, Is.EqualTo(0));
            Assert.That(model.bones[1].appendRatio, Is.EqualTo(0.5f));
            Assert.That(model.bones[1].appendRotation, Is.True);
            Assert.That(model.bones[1].appendLocal, Is.True);
            Assert.That(model.bones[1].fixedAxis, Is.True);
            Assert.That(model.bones[1].localAxes, Is.True);
            Assert.That(model.bones[1].externalParentTransform, Is.True);
            Assert.That(model.bones[1].deformAfterPhysics, Is.True);
            Assert.That(model.HasDeformAfterPhysicsBones, Is.True);
            Assert.That(model.ik, Has.Count.EqualTo(1));
            Assert.That(model.ik[0].boneIndex, Is.EqualTo(2));
            Assert.That(model.ik[0].targetBoneIndex, Is.EqualTo(1));
            Assert.That(model.ik[0].links, Has.Count.EqualTo(1));
            Assert.That(model.ik[0].links[0].hasLimit, Is.True);

            Assert.That(model.morphs, Has.Count.EqualTo(1));
            Assert.That(model.morphs[0].name, Is.EqualTo("smile"));
            Assert.That(model.morphs[0].panel, Is.EqualTo("brow"));
            Assert.That(model.morphs[0].vertexOffsets[0].vertexIndex, Is.EqualTo(1));
            Assert.That(model.morphs[0].materialOffsets[0].operation, Is.EqualTo("multiply"));
            CollectionAssert.AreEqual(new[] { 0.1f, 0.2f, 0.3f, 0.4f }, model.morphs[0].uvOffsets[0].positionDelta);
            Assert.That(model.morphs[0].impulseOffsets[0].local, Is.True);

            Assert.That(model.materials, Has.Count.EqualTo(1));
            Assert.That(model.materials[0].name, Is.EqualTo("mat"));
            Assert.That(model.materials[0].alpha, Is.EqualTo(0.75f));
            CollectionAssert.AreEqual(new[] { 1.0f, 0.0f, 0.5f }, model.materials[0].diffuseColor);
            Assert.That(model.materials[0].sphereTextureMode, Is.EqualTo("additive-sphere"));
            Assert.That(model.materials[0].toonShared, Is.True);
            Assert.That(model.materials[0].sharedToonIndex, Is.EqualTo(3));
            Assert.That(model.materials[0].cullingPolicy, Is.EqualTo("double-sided"));
            Assert.That(model.materials[0].drawEdgeFlag, Is.True);
            Assert.That(model.materials[0].vertexCount, Is.EqualTo(6));

            Assert.That(model.physics.rigidbodies, Has.Count.EqualTo(1));
            Assert.That(model.physics.rigidbodies[0].boneName, Is.EqualTo("append"));
            Assert.That(model.physics.rigidbodies[0].physicsKind, Is.EqualTo("dynamic"));
            Assert.That(model.physics.joints, Has.Count.EqualTo(1));
            CollectionAssert.AreEqual(new[] { 10.0f, 11.0f, 12.0f }, model.physics.joints[0].angularSpring);
        }

        [Test]
        public void BuildModelDefinitionAllowsNullSnapshot()
        {
            MmdModelDefinition model = NativeMmdParser.BuildModelDefinition(null);

            Assert.That(model.name, Is.Empty);
            Assert.That(model.vertices, Is.Empty);
            Assert.That(model.materials, Is.Empty);
            Assert.That(model.physics.rigidbodies, Is.Empty);
        }

        [Test]
        public void LoadModelUsesJsonAndGeometryDelegatesWithoutPmxSummaryAccessor()
        {
            byte[] expectedBytes = { 9, 8, 7 };
            byte[]? observedJsonBytes = null;
            byte[]? observedGeometryBytes = null;

            var parser = new NativeMmdParser(
                _ => throw new AssertionException("VMD JSON parser must not be used when loading PMX."),
                bytes =>
                {
                    observedJsonBytes = bytes;
                    // Minimal valid JSON: JsonUtility leaves all PmxModelSourceSnapshot fields at defaults.
                    return "{}";
                },
                bytes =>
                {
                    observedGeometryBytes = bytes;
                    return new NativeMmdParser.PmxModelSourceGeometry();
                });

            MmdModelDefinition model = parser.LoadModel(expectedBytes);

            CollectionAssert.AreEqual(expectedBytes, observedJsonBytes,
                "parsePmxNonGeometryJson must receive the original PMX bytes.");
            CollectionAssert.AreEqual(expectedBytes, observedGeometryBytes,
                "createPmxGeometry must receive the original PMX bytes.");
            Assert.IsNotNull(model, "LoadModel must return a non-null definition.");
        }

    }
}
