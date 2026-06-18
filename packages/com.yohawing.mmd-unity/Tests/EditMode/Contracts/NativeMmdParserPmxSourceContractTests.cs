using System;
using NUnit.Framework;
using Yohawing.MmdUnity.Parser;

namespace Yohawing.MmdUnity.Tests
{
    [TestFixture]
    public sealed class NativeMmdParserPmxSourceContractTests
    {
        [Test]
        public void BuildModelDefinitionUsesNonJsonSnapshotWithoutNativeInvocation()
        {
            var snapshot = new NativeMmdParser.PmxModelSourceSnapshot
            {
                metadata = new NativeMmdParser.PmxModelSourceMetadata { name = "pmx-source" },
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
                                externalParentTransform = true
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
        public void CreateModelSnapshotUsesPmxSummaryAccessorWithoutNativeInvocation()
        {
            var accessor = new FakePmxSummaryAccessor();

            NativeMmdParser.PmxModelSourceSnapshot snapshot = NativeMmdParser.CreateModelSnapshot(accessor);
            MmdModelDefinition model = NativeMmdParser.BuildModelDefinition(snapshot);

            Assert.That(snapshot.metadata.name, Is.EqualTo("pmx-summary"));
            Assert.That(snapshot.geometry.skinningModes[0], Is.EqualTo("qdef"));
            Assert.That(snapshot.geometry.skinningModes[1], Is.EqualTo("sdef"));
            CollectionAssert.AreEqual(new uint[] { 0, 1, 0 }, snapshot.geometry.indices);

            Assert.That(model.name, Is.EqualTo("pmx-summary"));
            Assert.That(model.vertices, Has.Count.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 1.0f, 2.0f, 3.0f }, model.vertices[0].position);
            Assert.That(model.vertices[0].skinningMode, Is.EqualTo("qdef"));
            CollectionAssert.AreEqual(new[] { 1, 0 }, model.vertices[1].boneIndices);
            Assert.That(model.vertices[1].hasSdefParameters, Is.True);
            CollectionAssert.AreEqual(new[] { 0.1f, 0.2f, 0.3f }, model.vertices[1].sdefC);

            Assert.That(model.bones, Has.Count.EqualTo(2));
            Assert.That(model.bones[1].appendParentIndex, Is.EqualTo(0));
            Assert.That(model.bones[1].fixedAxis, Is.True);
            Assert.That(model.ik, Has.Count.EqualTo(1));
            Assert.That(model.ik[0].targetBoneIndex, Is.EqualTo(0));
            Assert.That(model.ik[0].links[0].hasLimit, Is.True);

            Assert.That(model.morphs, Has.Count.EqualTo(1));
            Assert.That(model.morphs[0].name, Is.EqualTo("summary_morph"));
            Assert.That(model.morphs[0].type, Is.EqualTo("vertex"));
            Assert.That(model.morphs[0].panel, Is.EqualTo("eye"));
            Assert.That(model.morphs[0].materialOffsets[0].operation, Is.EqualTo("add"));
            Assert.That(model.morphs[0].uvOffsets, Has.Count.EqualTo(2));
            Assert.That(model.morphs[0].impulseOffsets[0].local, Is.True);

            Assert.That(model.materials, Has.Count.EqualTo(1));
            Assert.That(model.materials[0].name, Is.EqualTo("summary_mat"));
            Assert.That(model.materials[0].sphereTextureMode, Is.EqualTo("multiply-sphere"));
            Assert.That(model.materials[0].vertexCount, Is.EqualTo(3));

            Assert.That(model.physics.rigidbodies[0].boneName, Is.EqualTo("append"));
            Assert.That(model.physics.rigidbodies[0].mask, Is.EqualTo(65535));
            Assert.That(model.physics.joints[0].rigidbodyAIndex, Is.EqualTo(0));
            CollectionAssert.AreEqual(new[] { 7.0f, 8.0f, 9.0f }, model.physics.joints[0].linearSpring);
        }

        [Test]
        public void LoadModelUsesPmxSummaryFactoryAndDisposesAccessorWithoutJsonEntryPoint()
        {
            var accessor = new FakePmxSummaryAccessor();
            byte[] observedBytes = null;
            var parser = new NativeMmdParser(
                bytes =>
                {
                    observedBytes = bytes;
                    return accessor;
                },
                _ => throw new AssertionException("VMD summary factory must not be used when loading PMX."));

            MmdModelDefinition model = parser.LoadModel(new byte[] { 9, 8, 7 });

            CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, observedBytes);
            Assert.That(model.name, Is.EqualTo("pmx-summary"));
            Assert.That(model.vertices, Has.Count.EqualTo(2));
            Assert.That(accessor.Disposed, Is.True);
        }

        private sealed class FakePmxSummaryAccessor : MmdParserFfiMethods.IPmxSummaryAccessor, IDisposable
        {
            public bool Disposed { get; private set; }
            public string ModelName => "pmx-summary";
            public int VertexCount => 2;
            public int IndexCount => 3;
            public int MaterialCount => 1;
            public int BoneCount => 2;
            public int MorphCount => 1;
            public int RigidbodyCount => 1;
            public int JointCount => 1;

            public uint GetIndex(int index) => new uint[] { 0, 1, 0 }[index];
            public float GetVertexPosition(int vertexIndex, int component) => Read(new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f }, vertexIndex * 3 + component);
            public float GetVertexNormal(int vertexIndex, int component) => Read(new[] { 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f }, vertexIndex * 3 + component);
            public float GetVertexUv(int vertexIndex, int component) => Read(new[] { 0.25f, 0.5f, 0.75f, 1.0f }, vertexIndex * 2 + component);
            public int GetVertexSkinBoneIndex(int vertexIndex, int subIndex) => Read(new[] { 0, 1, -1, -1, 1, 0, -1, -1 }, vertexIndex * 4 + subIndex);
            public float GetVertexSkinWeight(int vertexIndex, int subIndex) => Read(new[] { 0.75f, 0.25f, 0.0f, 0.0f, 0.6f, 0.4f, 0.0f, 0.0f }, vertexIndex * 4 + subIndex);
            public string GetVertexSkinningKind(int vertexIndex) => vertexIndex == 0 ? "qdef" : "sdef";
            public bool GetVertexSdefEnabled(int vertexIndex) => vertexIndex == 1;
            public float GetVertexSdef(int vertexIndex, int which, int component) => vertexIndex == 1 ? 0.1f + (which * 0.3f) + (component * 0.1f) : 0.0f;

            public string GetMaterialName(int materialIndex) => "summary_mat";
            public string GetMaterialTexturePath(int materialIndex) => "summary.png";
            public string GetMaterialSphereTexturePath(int materialIndex) => "summary.sph";
            public string GetMaterialToonTexturePath(int materialIndex) => "toon.bmp";
            public string GetMaterialSphereMode(int materialIndex) => "multiply";
            public int GetMaterialSharedToonIndex(int materialIndex) => 1;
            public float GetMaterialDiffuse(int materialIndex, int component) => Read(new[] { 0.8f, 0.7f, 0.6f, 0.5f }, component);
            public float GetMaterialAmbient(int materialIndex, int component) => Read(new[] { 0.1f, 0.2f, 0.3f }, component);
            public float GetMaterialEdgeColor(int materialIndex, int component) => Read(new[] { 0.2f, 0.3f, 0.4f, 0.5f }, component);
            public float GetMaterialEdgeSize(int materialIndex) => 0.25f;
            public int GetMaterialFaceCount(int materialIndex) => 1;
            public bool GetMaterialDoubleSided(int materialIndex) => true;
            public bool GetMaterialEdgeFlag(int materialIndex) => true;

            public string GetBoneName(int boneIndex) => boneIndex == 0 ? "root" : "append";
            public int GetBoneParentIndex(int boneIndex) => boneIndex == 0 ? -1 : 0;
            public int GetBoneLayer(int boneIndex) => boneIndex;
            public float GetBonePosition(int boneIndex, int component) => Read(new[] { 0.0f, 1.0f, 2.0f, 1.0f, 2.0f, 3.0f }, boneIndex * 3 + component);
            public bool GetBoneRotatable(int boneIndex) => true;
            public bool GetBoneTranslatable(int boneIndex) => boneIndex == 0;
            public bool GetBoneAppendRotate(int boneIndex) => boneIndex == 1;
            public bool GetBoneAppendTranslate(int boneIndex) => false;
            public bool GetBoneAppendLocal(int boneIndex) => boneIndex == 1;
            public int GetBoneAppendParentIndex(int boneIndex) => 0;
            public float GetBoneAppendWeight(int boneIndex) => 0.5f;
            public bool GetBoneFixedAxisPresent(int boneIndex) => boneIndex == 1;
            public float GetBoneFixedAxis(int boneIndex, int component) => Read(new[] { 1.0f, 0.0f, 0.0f }, component);
            public bool GetBoneLocalAxisPresent(int boneIndex) => boneIndex == 1;
            public float GetBoneLocalAxisX(int boneIndex, int component) => Read(new[] { 1.0f, 0.0f, 0.0f }, component);
            public float GetBoneLocalAxisZ(int boneIndex, int component) => Read(new[] { 0.0f, 0.0f, 1.0f }, component);
            public bool GetBoneExternalParentPresent(int boneIndex) => boneIndex == 1;
            public int GetBoneExternalParentKey(int boneIndex) => 12;
            public bool GetBoneIkPresent(int boneIndex) => boneIndex == 1;
            public int GetBoneIkTargetIndex(int boneIndex) => 0;
            public int GetBoneIkLoopCount(int boneIndex) => 8;
            public float GetBoneIkLimitAngle(int boneIndex) => 0.25f;
            public int GetBoneIkLinkCount(int boneIndex) => boneIndex == 1 ? 1 : 0;
            public int GetBoneIkLinkBoneIndex(int boneIndex, int linkIndex) => 0;
            public bool GetBoneIkLinkLimitPresent(int boneIndex, int linkIndex) => true;
            public float GetBoneIkLinkLimitLower(int boneIndex, int linkIndex, int component) => Read(new[] { -0.1f, -0.2f, -0.3f }, component);
            public float GetBoneIkLinkLimitUpper(int boneIndex, int linkIndex, int component) => Read(new[] { 0.1f, 0.2f, 0.3f }, component);

            public string GetMorphName(int morphIndex) => "summary_morph";
            public string GetMorphKind(int morphIndex) => "vertex";
            public string GetMorphPanel(int morphIndex) => "eye";
            public int GetMorphVertexOffsetCount(int morphIndex) => 1;
            public int GetMorphGroupOffsetCount(int morphIndex) => 1;
            public int GetMorphBoneOffsetCount(int morphIndex) => 1;
            public int GetMorphUvOffsetCount(int morphIndex) => 1;
            public int GetMorphAdditionalUvOffsetCount(int morphIndex) => 1;
            public int GetMorphMaterialOffsetCount(int morphIndex) => 1;
            public int GetMorphFlipOffsetCount(int morphIndex) => 1;
            public int GetMorphImpulseOffsetCount(int morphIndex) => 1;
            public uint GetMorphVertexOffsetVertexIndex(int morphIndex, int offsetIndex) => 1;
            public float GetMorphVertexOffsetPosition(int morphIndex, int offsetIndex, int component) => Read(new[] { 0.01f, 0.02f, 0.03f }, component);
            public int GetMorphGroupOffsetMorphIndex(int morphIndex, int offsetIndex) => 0;
            public float GetMorphGroupOffsetWeight(int morphIndex, int offsetIndex) => 0.5f;
            public int GetMorphBoneOffsetBoneIndex(int morphIndex, int offsetIndex) => 1;
            public float GetMorphBoneOffsetTranslation(int morphIndex, int offsetIndex, int component) => Read(new[] { 0.1f, 0.2f, 0.3f }, component);
            public float GetMorphBoneOffsetRotation(int morphIndex, int offsetIndex, int component) => Read(new[] { 0.0f, 0.0f, 0.0f, 1.0f }, component);
            public uint GetMorphUvOffsetVertexIndex(int morphIndex, int offsetIndex) => 0;
            public float GetMorphUvOffsetValue(int morphIndex, int offsetIndex, int component) => Read(new[] { 0.1f, 0.2f, 0.3f, 0.4f }, component);
            public uint GetMorphAdditionalUvOffsetVertexIndex(int morphIndex, int offsetIndex) => 1;
            public byte GetMorphAdditionalUvOffsetUvIndex(int morphIndex, int offsetIndex) => 0;
            public float GetMorphAdditionalUvOffsetValue(int morphIndex, int offsetIndex, int component) => Read(new[] { 0.5f, 0.6f, 0.7f, 0.8f }, component);
            public int GetMorphMaterialOffsetMaterialIndex(int morphIndex, int offsetIndex) => 0;
            public string GetMorphMaterialOffsetOperation(int morphIndex, int offsetIndex) => "add";
            public float GetMorphMaterialOffsetDiffuse(int morphIndex, int offsetIndex, int component) => Read(new[] { 0.8f, 0.7f, 0.6f, 0.5f }, component);
            public float GetMorphMaterialOffsetSpecular(int morphIndex, int offsetIndex, int component) => Read(new[] { 0.4f, 0.5f, 0.6f }, component);
            public float GetMorphMaterialOffsetSpecularPower(int morphIndex, int offsetIndex) => 7.0f;
            public float GetMorphMaterialOffsetAmbient(int morphIndex, int offsetIndex, int component) => Read(new[] { 0.1f, 0.2f, 0.3f }, component);
            public float GetMorphMaterialOffsetEdgeColor(int morphIndex, int offsetIndex, int component) => Read(new[] { 0.2f, 0.3f, 0.4f, 0.5f }, component);
            public float GetMorphMaterialOffsetEdgeSize(int morphIndex, int offsetIndex) => 0.15f;
            public float GetMorphMaterialOffsetTextureFactor(int morphIndex, int offsetIndex, int component) => Read(new[] { 0.1f, 0.2f, 0.3f, 0.4f }, component);
            public float GetMorphMaterialOffsetSphereTextureFactor(int morphIndex, int offsetIndex, int component) => Read(new[] { 0.5f, 0.6f, 0.7f, 0.8f }, component);
            public float GetMorphMaterialOffsetToonTextureFactor(int morphIndex, int offsetIndex, int component) => Read(new[] { 0.9f, 1.0f, 0.8f, 0.7f }, component);
            public int GetMorphFlipOffsetMorphIndex(int morphIndex, int offsetIndex) => 0;
            public float GetMorphFlipOffsetWeight(int morphIndex, int offsetIndex) => 0.25f;
            public int GetMorphImpulseOffsetRigidbodyIndex(int morphIndex, int offsetIndex) => 0;
            public bool GetMorphImpulseOffsetLocal(int morphIndex, int offsetIndex) => true;
            public float GetMorphImpulseOffsetVelocity(int morphIndex, int offsetIndex, int component) => Read(new[] { 1.0f, 2.0f, 3.0f }, component);
            public float GetMorphImpulseOffsetTorque(int morphIndex, int offsetIndex, int component) => Read(new[] { 4.0f, 5.0f, 6.0f }, component);

            public string GetRigidbodyName(int rigidbodyIndex) => "body";
            public int GetRigidbodyBoneIndex(int rigidbodyIndex) => 1;
            public int GetRigidbodyGroup(int rigidbodyIndex) => 2;
            public int GetRigidbodyMask(int rigidbodyIndex) => 65535;
            public string GetRigidbodyShape(int rigidbodyIndex) => "sphere";
            public float GetRigidbodySize(int rigidbodyIndex, int component) => Read(new[] { 0.1f, 0.2f, 0.3f }, component);
            public float GetRigidbodyPosition(int rigidbodyIndex, int component) => Read(new[] { 1.0f, 2.0f, 3.0f }, component);
            public float GetRigidbodyRotation(int rigidbodyIndex, int component) => Read(new[] { 4.0f, 5.0f, 6.0f }, component);
            public float GetRigidbodyMass(int rigidbodyIndex) => 1.5f;
            public float GetRigidbodyLinearDamping(int rigidbodyIndex) => 0.1f;
            public float GetRigidbodyAngularDamping(int rigidbodyIndex) => 0.2f;
            public float GetRigidbodyRestitution(int rigidbodyIndex) => 0.3f;
            public float GetRigidbodyFriction(int rigidbodyIndex) => 0.4f;
            public string GetRigidbodyMode(int rigidbodyIndex) => "dynamic";

            public string GetJointName(int jointIndex) => "joint";
            public int GetJointRigidbodyAIndex(int jointIndex) => 0;
            public int GetJointRigidbodyBIndex(int jointIndex) => 0;
            public float GetJointPosition(int jointIndex, int component) => Read(new[] { 0.1f, 0.2f, 0.3f }, component);
            public float GetJointRotation(int jointIndex, int component) => Read(new[] { 0.4f, 0.5f, 0.6f }, component);
            public float GetJointTranslationLowerLimit(int jointIndex, int component) => Read(new[] { -1.0f, -2.0f, -3.0f }, component);
            public float GetJointTranslationUpperLimit(int jointIndex, int component) => Read(new[] { 1.0f, 2.0f, 3.0f }, component);
            public float GetJointRotationLowerLimit(int jointIndex, int component) => Read(new[] { -0.1f, -0.2f, -0.3f }, component);
            public float GetJointRotationUpperLimit(int jointIndex, int component) => Read(new[] { 0.1f, 0.2f, 0.3f }, component);
            public float GetJointSpringTranslationFactor(int jointIndex, int component) => Read(new[] { 7.0f, 8.0f, 9.0f }, component);
            public float GetJointSpringRotationFactor(int jointIndex, int component) => Read(new[] { 10.0f, 11.0f, 12.0f }, component);

            private static T Read<T>(T[] values, int index)
            {
                return values[index];
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
