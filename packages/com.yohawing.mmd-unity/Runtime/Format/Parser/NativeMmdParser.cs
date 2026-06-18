#nullable disable
#pragma warning disable CS0649

using System;
using System.Collections.Generic;
namespace Yohawing.MmdUnity.Parser
{
    public sealed class NativeMmdParser : IMmdParser
    {
        private readonly Func<byte[], MmdParserFfiMethods.IPmxSummaryAccessor> createPmxSummary;
        private readonly Func<byte[], MmdParserFfiMethods.IVmdSummaryAccessor> createVmdSummary;

        public NativeMmdParser()
            : this(MmdParserFfiMethods.CreatePmxSummary, MmdParserFfiMethods.CreateVmdSummary)
        {
        }

        internal NativeMmdParser(
            Func<byte[], MmdParserFfiMethods.IPmxSummaryAccessor> createPmxSummary,
            Func<byte[], MmdParserFfiMethods.IVmdSummaryAccessor> createVmdSummary)
        {
            this.createPmxSummary = createPmxSummary ?? throw new ArgumentNullException(nameof(createPmxSummary));
            this.createVmdSummary = createVmdSummary ?? throw new ArgumentNullException(nameof(createVmdSummary));
        }

        public MmdModelDefinition LoadModel(ReadOnlySpan<byte> data)
        {
            MmdParserInput.RequireNonEmpty(data, nameof(data));
            MmdParserFfiMethods.IPmxSummaryAccessor summary = createPmxSummary(data.ToArray());
            if (summary == null)
            {
                throw new InvalidOperationException("mmd-runtime PMX summary parser returned a null accessor.");
            }

            try
            {
                return BuildModelDefinition(CreateModelSnapshot(summary));
            }
            finally
            {
                (summary as IDisposable)?.Dispose();
            }
        }

        public MmdMotionDefinition LoadMotion(ReadOnlySpan<byte> data)
        {
            MmdParserInput.RequireNonEmpty(data, nameof(data));
            MmdParserFfiMethods.IVmdSummaryAccessor summary = createVmdSummary(data.ToArray());
            if (summary == null)
            {
                throw new InvalidOperationException("mmd-runtime VMD summary parser returned a null accessor.");
            }

            try
            {
                return BuildMotionDefinition(CreateMotionSnapshot(summary));
            }
            finally
            {
                (summary as IDisposable)?.Dispose();
            }
        }

        internal static MmdModelDefinition BuildModelDefinition(PmxModelSourceSnapshot source)
        {
            source ??= new PmxModelSourceSnapshot();
            var model = new MmdModelDefinition
            {
                name = source.metadata?.name ?? string.Empty
            };

            PmxModelSourceGeometry geometry = source.geometry ?? new PmxModelSourceGeometry();
            int vertexCount = (geometry.positions?.Length ?? 0) / 3;
            for (int i = 0; i < vertexCount; i++)
            {
                string skinningMode = GetString(geometry.skinningModes, i, "unknown");
                int boneCount = SkinningBoneCount(skinningMode);
                int[] boneIndices = new int[boneCount];
                float[] boneWeights = new float[boneCount];
                for (int j = 0; j < boneCount; j++)
                {
                    int boneIndex = GetUIntAsInt(geometry.skinIndices, i * 4 + j, 0);
                    float boneWeight = GetFloat(geometry.skinWeights, i * 4 + j, 0.0f);
                    if (boneIndex < 0)
                    {
                        boneIndex = 0;
                        boneWeight = 0.0f;
                    }

                    boneIndices[j] = boneIndex;
                    boneWeights[j] = boneWeight;
                }

                bool hasSdefParameters = GetBool(geometry.hasSdefParameters, i);
                model.vertices.Add(new MmdVertexDefinition
                {
                    index = i,
                    position = Vec3(geometry.positions, i * 3),
                    normal = SanitizedNormal(Vec3(geometry.normals, i * 3)),
                    uv = Vec2(geometry.uvs, i * 2),
                    skinningMode = skinningMode,
                    boneIndices = boneIndices,
                    boneWeights = boneWeights,
                    hasSdefParameters = hasSdefParameters,
                    sdefC = hasSdefParameters ? Vec3(geometry.sdefC, i * 3) : Array.Empty<float>(),
                    sdefR0 = hasSdefParameters ? Vec3(geometry.sdefR0, i * 3) : Array.Empty<float>(),
                    sdefR1 = hasSdefParameters ? Vec3(geometry.sdefR1, i * 3) : Array.Empty<float>()
                });
            }

            if (geometry.indices != null)
            {
                for (int i = 0; i < geometry.indices.Length; i++)
                {
                    model.indices.Add(CheckedUIntToInt(geometry.indices[i], "PMX index"));
                }
            }

            PmxModelSourceBone[] bones = source.skeleton?.bones ?? Array.Empty<PmxModelSourceBone>();
            for (int i = 0; i < bones.Length; i++)
            {
                PmxModelSourceBone bone = bones[i] ?? new PmxModelSourceBone();
                bool hasAppendTransform = HasAppendTransform(bone);
                model.bones.Add(new MmdBoneDefinition
                {
                    index = i,
                    name = bone.name ?? string.Empty,
                    parentIndex = bone.parentIndex,
                    transformOrder = bone.layer,
                    origin = CopyVec3(bone.position),
                    isMovable = bone.flags?.translatable ?? false,
                    isRotatable = bone.flags?.rotatable ?? false,
                    appendParentIndex = hasAppendTransform ? bone.appendTransform.parentIndex : -1,
                    appendRatio = hasAppendTransform ? bone.appendTransform.weight : 1.0f,
                    appendRotation = bone.flags?.appendRotate ?? false,
                    appendTranslation = bone.flags?.appendTranslate ?? false,
                    appendLocal = bone.flags?.appendLocal ?? false,
                    fixedAxis = HasVec3(bone.fixedAxis),
                    fixedAxisVector = CopyVec3(bone.fixedAxis),
                    localAxes = HasLocalAxis(bone.localAxis),
                    localXAxis = CopyVec3(bone.localAxis?.x),
                    localZAxis = CopyVec3(bone.localAxis?.z),
                    externalParentTransform = bone.externalParentKey >= 0 || (bone.flags?.externalParentTransform ?? false)
                });
            }

            for (int i = 0; i < bones.Length; i++)
            {
                PmxModelSourceIk ikJson = bones[i]?.ik;
                if (!HasIk(ikJson))
                {
                    continue;
                }

                var ik = new MmdIkDefinition
                {
                    boneIndex = i,
                    targetBoneIndex = ikJson.targetIndex,
                    iterationCount = ikJson.loopCount,
                    angleLimit = ikJson.limitAngle
                };
                PmxModelSourceIkLink[] links = ikJson.links ?? Array.Empty<PmxModelSourceIkLink>();
                for (int j = 0; j < links.Length; j++)
                {
                    PmxModelSourceIkLink link = links[j] ?? new PmxModelSourceIkLink();
                    ik.links.Add(new MmdIkLinkDefinition
                    {
                        boneIndex = link.boneIndex,
                        hasLimit = HasIkLimit(link.limits),
                        minimumAngle = CopyVec3(link.limits?.lower),
                        maximumAngle = CopyVec3(link.limits?.upper)
                    });
                }

                if (ik.targetBoneIndex == ik.boneIndex)
                {
                    ik.targetBoneIndex = InferIkEffectorIndex(model, ik);
                }

                model.ik.Add(ik);
            }

            PmxModelSourceMorph[] morphs = source.morphs ?? Array.Empty<PmxModelSourceMorph>();
            for (int i = 0; i < morphs.Length; i++)
            {
                PmxModelSourceMorph morphSource = morphs[i] ?? new PmxModelSourceMorph();
                var morph = new MmdMorphDefinition
                {
                    index = i,
                    name = morphSource.name ?? string.Empty,
                    type = morphSource.type ?? string.Empty,
                    panel = morphSource.panel ?? string.Empty
                };

                foreach (PmxModelSourceVertexMorphOffset offset in morphSource.vertexOffsets ?? Array.Empty<PmxModelSourceVertexMorphOffset>())
                {
                    morph.vertexOffsets.Add(new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = CheckedUIntToInt(offset.vertexIndex, "PMX vertex morph index"),
                        positionDelta = CopyVec3(offset.position)
                    });
                }

                foreach (PmxModelSourceGroupMorphOffset offset in morphSource.groupOffsets ?? Array.Empty<PmxModelSourceGroupMorphOffset>())
                {
                    morph.groupOffsets.Add(new MmdGroupMorphOffsetDefinition { morphIndex = offset.morphIndex, weight = offset.weight });
                }

                foreach (PmxModelSourceMaterialMorphOffset offset in morphSource.materialOffsets ?? Array.Empty<PmxModelSourceMaterialMorphOffset>())
                {
                    morph.materialOffsets.Add(new MmdMaterialMorphOffsetDefinition
                    {
                        materialIndex = offset.materialIndex,
                        operation = offset.operation ?? "unknown",
                        diffuseColor = Vec3From4(offset.diffuse),
                        diffuseOpacity = GetFloat(offset.diffuse, 3, 1.0f),
                        ambientColor = CopyVec3(offset.ambient),
                        specularColor = CopyVec3(offset.specular),
                        specularPower = offset.specularPower,
                        edgeColor = Vec3From4(offset.edgeColor),
                        edgeOpacity = GetFloat(offset.edgeColor, 3, 1.0f),
                        edgeSize = offset.edgeSize,
                        diffuseTextureBlend = CopyVec4(offset.textureFactor),
                        sphereTextureBlend = CopyVec4(offset.sphereTextureFactor),
                        toonTextureBlend = CopyVec4(offset.toonTextureFactor)
                    });
                }

                foreach (PmxModelSourceUvMorphOffset offset in morphSource.uvOffsets ?? Array.Empty<PmxModelSourceUvMorphOffset>())
                {
                    morph.uvOffsets.Add(new MmdUvMorphOffsetDefinition
                    {
                        vertexIndex = CheckedUIntToInt(offset.vertexIndex, "PMX UV morph index"),
                        positionDelta = CopyVec4(offset.uv)
                    });
                }

                foreach (PmxModelSourceAdditionalUvMorphOffset offset in morphSource.additionalUvOffsets ?? Array.Empty<PmxModelSourceAdditionalUvMorphOffset>())
                {
                    morph.uvOffsets.Add(new MmdUvMorphOffsetDefinition
                    {
                        vertexIndex = CheckedUIntToInt(offset.vertexIndex, "PMX additional UV morph index"),
                        positionDelta = CopyVec4(offset.uv)
                    });
                }

                foreach (PmxModelSourceBoneMorphOffset offset in morphSource.boneOffsets ?? Array.Empty<PmxModelSourceBoneMorphOffset>())
                {
                    morph.boneOffsets.Add(new MmdBoneMorphOffsetDefinition
                    {
                        boneIndex = offset.boneIndex,
                        translation = CopyVec3(offset.translation),
                        orientation = CopyVec4(offset.rotation)
                    });
                }

                foreach (PmxModelSourceGroupMorphOffset offset in morphSource.flipOffsets ?? Array.Empty<PmxModelSourceGroupMorphOffset>())
                {
                    morph.flipOffsets.Add(new MmdFlipMorphOffsetDefinition { morphIndex = offset.morphIndex, weight = offset.weight });
                }

                foreach (PmxModelSourceImpulseMorphOffset offset in morphSource.impulseOffsets ?? Array.Empty<PmxModelSourceImpulseMorphOffset>())
                {
                    morph.impulseOffsets.Add(new MmdImpulseMorphOffsetDefinition
                    {
                        rigidbodyIndex = offset.rigidBodyIndex,
                        velocity = CopyVec3(offset.velocity),
                        torque = CopyVec3(offset.torque),
                        local = offset.local
                    });
                }

                model.morphs.Add(morph);
            }

            PmxModelSourceMaterial[] materials = source.materials ?? Array.Empty<PmxModelSourceMaterial>();
            for (int i = 0; i < materials.Length; i++)
            {
                PmxModelSourceMaterial material = materials[i] ?? new PmxModelSourceMaterial();
                model.materials.Add(new MmdMaterialDefinition
                {
                    index = i,
                    name = NonEmptyMaterialName(material.name, i),
                    texture = material.texturePath ?? string.Empty,
                    sphereTexture = material.sphereTexturePath ?? string.Empty,
                    toonTexture = material.toonTexturePath ?? string.Empty,
                    alpha = Clamp01(GetFloat(material.diffuse, 3, 1.0f)),
                    diffuseColor = ClampColor3(Vec3From4(material.diffuse), new[] { 1.0f, 1.0f, 1.0f }),
                    ambientColor = ClampColor3(material.ambient, new[] { 0.25f, 0.25f, 0.25f }),
                    edgeColor = ClampColor4(material.edgeColor, new[] { 0.0f, 0.0f, 0.0f, 1.0f }),
                    edgeSize = ClampNonNegative(material.edgeSize),
                    sphereTextureMode = SphereTextureModeName(material.sphereMode),
                    toonShared = material.sharedToonIndex >= 0,
                    sharedToonIndex = material.sharedToonIndex,
                    cullingPolicy = (material.flags?.doubleSided ?? false) ? "double-sided" : "backface-culling",
                    drawEdgeFlag = material.flags?.edge ?? false,
                    vertexCount = Math.Max(0, material.faceCount * 3)
                });
            }

            PmxModelSourceRigidBody[] rigidBodies = source.rigidBodies ?? Array.Empty<PmxModelSourceRigidBody>();
            for (int i = 0; i < rigidBodies.Length; i++)
            {
                PmxModelSourceRigidBody body = rigidBodies[i] ?? new PmxModelSourceRigidBody();
                model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
                {
                    index = i,
                    name = NonEmptyPhysicsName(body.name, "rigidbody", i),
                    boneIndex = body.boneIndex,
                    boneName = BoneNameOrEmpty(model, body.boneIndex),
                    shapeType = body.shape ?? string.Empty,
                    size = CopyVec3(body.size),
                    position = CopyVec3(body.position),
                    rotation = CopyVec3(body.rotation),
                    mass = NonNegativeFiniteOrZero(body.mass),
                    linearDamping = NonNegativeFiniteOrZero(body.linearDamping),
                    angularDamping = NonNegativeFiniteOrZero(body.angularDamping),
                    friction = NonNegativeFiniteOrZero(body.friction),
                    restitution = NonNegativeFiniteOrZero(body.restitution),
                    group = Math.Max(0, body.group),
                    mask = Math.Max(0, body.mask),
                    physicsKind = body.mode ?? string.Empty
                });
            }

            PmxModelSourceJoint[] joints = source.joints ?? Array.Empty<PmxModelSourceJoint>();
            for (int i = 0; i < joints.Length; i++)
            {
                PmxModelSourceJoint joint = joints[i] ?? new PmxModelSourceJoint();
                model.physics.joints.Add(new MmdJointDefinition
                {
                    index = i,
                    name = NonEmptyPhysicsName(joint.name, "joint", i),
                    rigidbodyAIndex = joint.rigidBodyIndexA,
                    rigidbodyBIndex = joint.rigidBodyIndexB,
                    position = CopyVec3(joint.position),
                    rotation = CopyVec3(joint.rotation),
                    linearLowerLimit = CopyVec3(joint.translationLowerLimit),
                    linearUpperLimit = CopyVec3(joint.translationUpperLimit),
                    angularLowerLimit = CopyVec3(joint.rotationLowerLimit),
                    angularUpperLimit = CopyVec3(joint.rotationUpperLimit),
                    linearSpring = CopyVec3(joint.springTranslationFactor),
                    angularSpring = CopyVec3(joint.springRotationFactor)
                });
            }

            return model;
        }

        internal static MmdMotionDefinition BuildMotionDefinition(VmdMotionSourceSnapshot source)
        {
            source ??= new VmdMotionSourceSnapshot();
            var motion = new MmdMotionDefinition
            {
                targetModelName = source.TargetModelName ?? string.Empty,
                maxFrame = CheckedUIntToInt(source.MaxFrame, "VMD maxFrame")
            };

            motion.cameraKeyframeCount = UIntCountToInt(source.CameraKeyframeCount);
            motion.lightKeyframeCount = UIntCountToInt(source.LightKeyframeCount);
            motion.selfShadowKeyframeCount = UIntCountToInt(source.SelfShadowKeyframeCount);

            foreach (VmdMotionSourceBoneFrame frame in source.BoneFrames ?? Array.Empty<VmdMotionSourceBoneFrame>())
            {
                motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
                {
                    boneName = frame?.BoneName ?? string.Empty,
                    frame = CheckedUIntToInt(frame?.Frame ?? 0u, "VMD bone frame"),
                    translation = CopyVec3(frame?.Translation),
                    rotation = CopyVec4(frame?.Rotation),
                    interpolation = new MmdBoneInterpolationDefinition
                    {
                        translationX = CopyInterpolation(frame?.TranslationXInterpolation),
                        translationY = CopyInterpolation(frame?.TranslationYInterpolation),
                        translationZ = CopyInterpolation(frame?.TranslationZInterpolation),
                        rotation = CopyInterpolation(frame?.RotationInterpolation)
                    },
                    physicsEnabled = false
                });
            }

            foreach (VmdMotionSourceMorphFrame frame in source.MorphFrames ?? Array.Empty<VmdMotionSourceMorphFrame>())
            {
                motion.morphKeyframes.Add(new MmdMorphKeyframeDefinition
                {
                    morphName = frame?.MorphName ?? string.Empty,
                    frame = CheckedUIntToInt(frame?.Frame ?? 0u, "VMD morph frame"),
                    weight = frame?.Weight ?? 0.0f
                });
            }

            foreach (VmdMotionSourcePropertyFrame frame in source.PropertyFrames ?? Array.Empty<VmdMotionSourcePropertyFrame>())
            {
                var keyframe = new MmdModelKeyframeDefinition
                {
                    frame = CheckedUIntToInt(frame?.Frame ?? 0u, "VMD property frame"),
                    visible = frame?.Visible ?? true
                };
                foreach (VmdMotionSourceIkState state in frame?.IkStates ?? Array.Empty<VmdMotionSourceIkState>())
                {
                    keyframe.constraintStates.Add(new MmdModelConstraintStateDefinition
                    {
                        boneName = state?.BoneName ?? string.Empty,
                        enabled = state?.Enabled ?? true
                    });
                }

                motion.modelKeyframes.Add(keyframe);
            }

            foreach (VmdMotionSourceCameraFrame frame in source.CameraFrames ?? Array.Empty<VmdMotionSourceCameraFrame>())
            {
                motion.cameraKeyframes.Add(new MmdCameraKeyframeDefinition
                {
                    frame = CheckedUIntToInt(frame?.Frame ?? 0u, "VMD camera frame"),
                    distance = frame?.Distance ?? 0.0f,
                    position = CopyVec3(frame?.Position),
                    rotation = CopyVec3(frame?.Rotation),
                    viewAngle = CheckedUIntToInt(frame?.ViewAngle ?? 0u, "VMD camera view angle"),
                    perspective = frame?.Perspective ?? true,
                    interpolation = CopyCameraInterpolation(frame?.Interpolation)
                });
            }

            foreach (VmdMotionSourceLightFrame frame in source.LightFrames ?? Array.Empty<VmdMotionSourceLightFrame>())
            {
                motion.lightKeyframes.Add(new MmdLightKeyframeDefinition
                {
                    frame = CheckedUIntToInt(frame?.Frame ?? 0u, "VMD light frame"),
                    color = CopyVec3(frame?.Color),
                    direction = CopyVec3(frame?.Direction),
                });
            }

            return motion;
        }

        internal static VmdMotionSourceSnapshot CreateMotionSnapshot(MmdParserFfiMethods.IVmdSummaryAccessor accessor)
        {
            if (accessor == null)
            {
                return new VmdMotionSourceSnapshot();
            }

            var source = new VmdMotionSourceSnapshot
            {
                TargetModelName = accessor.TargetModelName ?? string.Empty,
                MaxFrame = accessor.MaxFrame,
                CameraKeyframeCount = CountToUInt(accessor.CameraFrameCount),
                LightKeyframeCount = CountToUInt(accessor.LightFrameCount),
                SelfShadowKeyframeCount = CountToUInt(accessor.SelfShadowFrameCount),
                BoneFrames = new VmdMotionSourceBoneFrame[CountToArrayLength(accessor.BoneFrameCount, "VMD bone frame count")],
                MorphFrames = new VmdMotionSourceMorphFrame[CountToArrayLength(accessor.MorphFrameCount, "VMD morph frame count")],
                PropertyFrames = new VmdMotionSourcePropertyFrame[CountToArrayLength(accessor.PropertyFrameCount, "VMD property frame count")],
                CameraFrames = new VmdMotionSourceCameraFrame[CountToArrayLength(accessor.CameraFrameCount, "VMD camera frame count")],
                LightFrames = new VmdMotionSourceLightFrame[CountToArrayLength(accessor.LightFrameCount, "VMD light frame count")]
            };

            for (int i = 0; i < source.BoneFrames.Length; i++)
            {
                source.BoneFrames[i] = new VmdMotionSourceBoneFrame
                {
                    BoneName = accessor.GetBoneFrameName(i) ?? string.Empty,
                    Frame = accessor.GetBoneFrameFrame(i),
                    Translation = new[]
                    {
                        accessor.GetBoneFrameTranslation(i, 0),
                        accessor.GetBoneFrameTranslation(i, 1),
                        accessor.GetBoneFrameTranslation(i, 2)
                    },
                    Rotation = new[]
                    {
                        accessor.GetBoneFrameRotation(i, 0),
                        accessor.GetBoneFrameRotation(i, 1),
                        accessor.GetBoneFrameRotation(i, 2),
                        accessor.GetBoneFrameRotation(i, 3)
                    },
                    TranslationXInterpolation = VmdSummaryInterpolation(accessor, i, 0),
                    TranslationYInterpolation = VmdSummaryInterpolation(accessor, i, 1),
                    TranslationZInterpolation = VmdSummaryInterpolation(accessor, i, 2),
                    RotationInterpolation = VmdSummaryInterpolation(accessor, i, 3)
                };
            }

            for (int i = 0; i < source.MorphFrames.Length; i++)
            {
                source.MorphFrames[i] = new VmdMotionSourceMorphFrame
                {
                    MorphName = accessor.GetMorphFrameName(i) ?? string.Empty,
                    Frame = accessor.GetMorphFrameFrame(i),
                    Weight = accessor.GetMorphFrameWeight(i)
                };
            }

            for (int i = 0; i < source.PropertyFrames.Length; i++)
            {
                int ikStateCount = CountToArrayLength(accessor.GetPropertyFrameIkStateCount(i), "VMD property IK state count");
                var propertyFrame = new VmdMotionSourcePropertyFrame
                {
                    Frame = accessor.GetPropertyFrameFrame(i),
                    Visible = accessor.GetPropertyFrameVisible(i),
                    IkStates = new VmdMotionSourceIkState[ikStateCount]
                };

                for (int j = 0; j < propertyFrame.IkStates.Length; j++)
                {
                    propertyFrame.IkStates[j] = new VmdMotionSourceIkState
                    {
                        BoneName = accessor.GetPropertyFrameIkStateName(i, j) ?? string.Empty,
                        Enabled = accessor.GetPropertyFrameIkStateEnabled(i, j)
                    };
                }

                source.PropertyFrames[i] = propertyFrame;
            }

            for (int i = 0; i < source.CameraFrames.Length; i++)
            {
                var interpolation = new byte[24];
                for (int b = 0; b < interpolation.Length; b++)
                {
                    interpolation[b] = accessor.GetCameraFrameInterpolationByte(i, b);
                }

                source.CameraFrames[i] = new VmdMotionSourceCameraFrame
                {
                    Frame = accessor.GetCameraFrameFrame(i),
                    Distance = accessor.GetCameraFrameDistance(i),
                    Position = new[]
                    {
                        accessor.GetCameraFramePosition(i, 0),
                        accessor.GetCameraFramePosition(i, 1),
                        accessor.GetCameraFramePosition(i, 2)
                    },
                    Rotation = new[]
                    {
                        accessor.GetCameraFrameRotation(i, 0),
                        accessor.GetCameraFrameRotation(i, 1),
                        accessor.GetCameraFrameRotation(i, 2)
                    },
                    ViewAngle = accessor.GetCameraFrameFov(i),
                    Perspective = accessor.GetCameraFramePerspective(i),
                    Interpolation = interpolation
                };
            }

            for (int i = 0; i < source.LightFrames.Length; i++)
            {
                source.LightFrames[i] = new VmdMotionSourceLightFrame
                {
                    Frame = accessor.GetLightFrameFrame(i),
                    Color = new[]
                    {
                        accessor.GetLightFrameColor(i, 0),
                        accessor.GetLightFrameColor(i, 1),
                        accessor.GetLightFrameColor(i, 2)
                    },
                    Direction = new[]
                    {
                        accessor.GetLightFrameDirection(i, 0),
                        accessor.GetLightFrameDirection(i, 1),
                        accessor.GetLightFrameDirection(i, 2)
                    }
                };
            }

            return source;
        }

        internal static PmxModelSourceSnapshot CreateModelSnapshot(MmdParserFfiMethods.IPmxSummaryAccessor accessor)
        {
            if (accessor == null)
            {
                return new PmxModelSourceSnapshot();
            }

            return new PmxModelSourceSnapshot
            {
                metadata = new PmxModelSourceMetadata { name = accessor.ModelName ?? string.Empty },
                geometry = CreatePmxGeometry(accessor),
                materials = CreatePmxMaterials(accessor),
                skeleton = new PmxModelSourceSkeleton { bones = CreatePmxBones(accessor) },
                morphs = CreatePmxMorphs(accessor),
                rigidBodies = CreatePmxRigidBodies(accessor),
                joints = CreatePmxJoints(accessor)
            };
        }

        private static PmxModelSourceGeometry CreatePmxGeometry(MmdParserFfiMethods.IPmxSummaryAccessor accessor)
        {
            int vertexCount = CountToArrayLength(accessor.VertexCount, "PMX vertex count");
            int indexCount = CountToArrayLength(accessor.IndexCount, "PMX index count");
            var geometry = new PmxModelSourceGeometry
            {
                positions = new float[vertexCount * 3],
                normals = new float[vertexCount * 3],
                uvs = new float[vertexCount * 2],
                indices = new uint[indexCount],
                skinningModes = new string[vertexCount],
                skinIndices = new uint[vertexCount * 4],
                skinWeights = new float[vertexCount * 4],
                hasSdefParameters = new bool[vertexCount],
                sdefC = new float[vertexCount * 3],
                sdefR0 = new float[vertexCount * 3],
                sdefR1 = new float[vertexCount * 3]
            };

            for (int i = 0; i < indexCount; i++)
            {
                geometry.indices[i] = accessor.GetIndex(i);
            }

            for (int i = 0; i < vertexCount; i++)
            {
                for (int component = 0; component < 3; component++)
                {
                    geometry.positions[i * 3 + component] = accessor.GetVertexPosition(i, component);
                    geometry.normals[i * 3 + component] = accessor.GetVertexNormal(i, component);
                    geometry.sdefC[i * 3 + component] = accessor.GetVertexSdef(i, 0, component);
                    geometry.sdefR0[i * 3 + component] = accessor.GetVertexSdef(i, 1, component);
                    geometry.sdefR1[i * 3 + component] = accessor.GetVertexSdef(i, 2, component);
                }

                for (int component = 0; component < 2; component++)
                {
                    geometry.uvs[i * 2 + component] = accessor.GetVertexUv(i, component);
                }

                for (int slot = 0; slot < 4; slot++)
                {
                    int boneIndex = accessor.GetVertexSkinBoneIndex(i, slot);
                    int offset = i * 4 + slot;
                    if (boneIndex < 0)
                    {
                        geometry.skinIndices[offset] = 0u;
                        geometry.skinWeights[offset] = 0.0f;
                    }
                    else
                    {
                        geometry.skinIndices[offset] = (uint)boneIndex;
                        geometry.skinWeights[offset] = accessor.GetVertexSkinWeight(i, slot);
                    }
                }

                geometry.hasSdefParameters[i] = accessor.GetVertexSdefEnabled(i);
                geometry.skinningModes[i] = ExactOrInferredPmxSkinningMode(accessor, i);
            }

            return geometry;
        }

        private static string ExactOrInferredPmxSkinningMode(MmdParserFfiMethods.IPmxSummaryAccessor accessor, int vertexIndex)
        {
            string exact = accessor.GetVertexSkinningKind(vertexIndex);
            return string.IsNullOrWhiteSpace(exact) ? InferPmxSkinningMode(accessor, vertexIndex) : exact;
        }

        private static string InferPmxSkinningMode(MmdParserFfiMethods.IPmxSummaryAccessor accessor, int vertexIndex)
        {
            if (accessor.GetVertexSdefEnabled(vertexIndex))
            {
                return "sdef";
            }

            int weightedSlotCount = 0;
            for (int slot = 0; slot < 4; slot++)
            {
                if (accessor.GetVertexSkinBoneIndex(vertexIndex, slot) >= 0 &&
                    Math.Abs(accessor.GetVertexSkinWeight(vertexIndex, slot)) > 0.000001f)
                {
                    weightedSlotCount++;
                }
            }

            return weightedSlotCount switch
            {
                <= 1 => "bdef1",
                2 => "bdef2",
                _ => "bdef4"
            };
        }

        private static PmxModelSourceMaterial[] CreatePmxMaterials(MmdParserFfiMethods.IPmxSummaryAccessor accessor)
        {
            int count = CountToArrayLength(accessor.MaterialCount, "PMX material count");
            var materials = new PmxModelSourceMaterial[count];
            for (int i = 0; i < count; i++)
            {
                materials[i] = new PmxModelSourceMaterial
                {
                    name = accessor.GetMaterialName(i) ?? string.Empty,
                    texturePath = accessor.GetMaterialTexturePath(i) ?? string.Empty,
                    sphereTexturePath = accessor.GetMaterialSphereTexturePath(i) ?? string.Empty,
                    sphereMode = accessor.GetMaterialSphereMode(i) ?? string.Empty,
                    toonTexturePath = accessor.GetMaterialToonTexturePath(i) ?? string.Empty,
                    sharedToonIndex = accessor.GetMaterialSharedToonIndex(i),
                    diffuse = ReadVec4(component => accessor.GetMaterialDiffuse(i, component)),
                    ambient = ReadVec3(component => accessor.GetMaterialAmbient(i, component)),
                    edgeColor = ReadVec4(component => accessor.GetMaterialEdgeColor(i, component)),
                    edgeSize = accessor.GetMaterialEdgeSize(i),
                    flags = new PmxModelSourceMaterialFlags
                    {
                        doubleSided = accessor.GetMaterialDoubleSided(i),
                        edge = accessor.GetMaterialEdgeFlag(i)
                    },
                    faceCount = accessor.GetMaterialFaceCount(i)
                };
            }

            return materials;
        }

        private static PmxModelSourceBone[] CreatePmxBones(MmdParserFfiMethods.IPmxSummaryAccessor accessor)
        {
            int count = CountToArrayLength(accessor.BoneCount, "PMX bone count");
            var bones = new PmxModelSourceBone[count];
            for (int i = 0; i < count; i++)
            {
                bool appendRotate = accessor.GetBoneAppendRotate(i);
                bool appendTranslate = accessor.GetBoneAppendTranslate(i);
                bool fixedAxisPresent = accessor.GetBoneFixedAxisPresent(i);
                bool localAxisPresent = accessor.GetBoneLocalAxisPresent(i);
                bool externalParentPresent = accessor.GetBoneExternalParentPresent(i);
                bool ikPresent = accessor.GetBoneIkPresent(i);
                bones[i] = new PmxModelSourceBone
                {
                    name = accessor.GetBoneName(i) ?? string.Empty,
                    parentIndex = accessor.GetBoneParentIndex(i),
                    layer = accessor.GetBoneLayer(i),
                    position = ReadVec3(component => accessor.GetBonePosition(i, component)),
                    flags = new PmxModelSourceBoneFlags
                    {
                        rotatable = accessor.GetBoneRotatable(i),
                        translatable = accessor.GetBoneTranslatable(i),
                        appendRotate = appendRotate,
                        appendTranslate = appendTranslate,
                        appendLocal = accessor.GetBoneAppendLocal(i),
                        externalParentTransform = externalParentPresent
                    },
                    appendTransform = (appendRotate || appendTranslate)
                        ? new PmxModelSourceAppendTransform
                        {
                            parentIndex = accessor.GetBoneAppendParentIndex(i),
                            weight = accessor.GetBoneAppendWeight(i)
                        }
                        : null,
                    fixedAxis = fixedAxisPresent ? ReadVec3(component => accessor.GetBoneFixedAxis(i, component)) : null,
                    localAxis = localAxisPresent
                        ? new PmxModelSourceLocalAxis
                        {
                            x = ReadVec3(component => accessor.GetBoneLocalAxisX(i, component)),
                            z = ReadVec3(component => accessor.GetBoneLocalAxisZ(i, component))
                        }
                        : null,
                    externalParentKey = externalParentPresent ? accessor.GetBoneExternalParentKey(i) : -1,
                    ik = ikPresent ? CreatePmxIk(accessor, i) : null
                };
            }

            return bones;
        }

        private static PmxModelSourceIk CreatePmxIk(MmdParserFfiMethods.IPmxSummaryAccessor accessor, int boneIndex)
        {
            int linkCount = CountToArrayLength(accessor.GetBoneIkLinkCount(boneIndex), "PMX IK link count");
            var links = new PmxModelSourceIkLink[linkCount];
            for (int i = 0; i < linkCount; i++)
            {
                bool limitPresent = accessor.GetBoneIkLinkLimitPresent(boneIndex, i);
                links[i] = new PmxModelSourceIkLink
                {
                    boneIndex = accessor.GetBoneIkLinkBoneIndex(boneIndex, i),
                    limits = limitPresent
                        ? new PmxModelSourceIkLimit
                        {
                            lower = ReadVec3(component => accessor.GetBoneIkLinkLimitLower(boneIndex, i, component)),
                            upper = ReadVec3(component => accessor.GetBoneIkLinkLimitUpper(boneIndex, i, component))
                        }
                        : null
                };
            }

            return new PmxModelSourceIk
            {
                targetIndex = accessor.GetBoneIkTargetIndex(boneIndex),
                loopCount = accessor.GetBoneIkLoopCount(boneIndex),
                limitAngle = accessor.GetBoneIkLimitAngle(boneIndex),
                links = links
            };
        }

        private static PmxModelSourceMorph[] CreatePmxMorphs(MmdParserFfiMethods.IPmxSummaryAccessor accessor)
        {
            int count = CountToArrayLength(accessor.MorphCount, "PMX morph count");
            var morphs = new PmxModelSourceMorph[count];
            for (int i = 0; i < count; i++)
            {
                morphs[i] = new PmxModelSourceMorph
                {
                    name = accessor.GetMorphName(i) ?? string.Empty,
                    type = accessor.GetMorphKind(i) ?? string.Empty,
                    panel = accessor.GetMorphPanel(i) ?? string.Empty,
                    vertexOffsets = CreatePmxVertexMorphOffsets(accessor, i),
                    groupOffsets = CreatePmxGroupMorphOffsets(accessor, i),
                    boneOffsets = CreatePmxBoneMorphOffsets(accessor, i),
                    uvOffsets = CreatePmxUvMorphOffsets(accessor, i),
                    additionalUvOffsets = CreatePmxAdditionalUvMorphOffsets(accessor, i),
                    materialOffsets = CreatePmxMaterialMorphOffsets(accessor, i),
                    flipOffsets = CreatePmxFlipMorphOffsets(accessor, i),
                    impulseOffsets = CreatePmxImpulseMorphOffsets(accessor, i)
                };
            }

            return morphs;
        }

        private static PmxModelSourceVertexMorphOffset[] CreatePmxVertexMorphOffsets(MmdParserFfiMethods.IPmxSummaryAccessor accessor, int morphIndex)
        {
            int count = CountToArrayLength(accessor.GetMorphVertexOffsetCount(morphIndex), "PMX vertex morph offset count");
            var offsets = new PmxModelSourceVertexMorphOffset[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = new PmxModelSourceVertexMorphOffset
                {
                    vertexIndex = accessor.GetMorphVertexOffsetVertexIndex(morphIndex, i),
                    position = ReadVec3(component => accessor.GetMorphVertexOffsetPosition(morphIndex, i, component))
                };
            }

            return offsets;
        }

        private static PmxModelSourceGroupMorphOffset[] CreatePmxGroupMorphOffsets(MmdParserFfiMethods.IPmxSummaryAccessor accessor, int morphIndex)
        {
            int count = CountToArrayLength(accessor.GetMorphGroupOffsetCount(morphIndex), "PMX group morph offset count");
            var offsets = new PmxModelSourceGroupMorphOffset[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = new PmxModelSourceGroupMorphOffset
                {
                    morphIndex = accessor.GetMorphGroupOffsetMorphIndex(morphIndex, i),
                    weight = accessor.GetMorphGroupOffsetWeight(morphIndex, i)
                };
            }

            return offsets;
        }

        private static PmxModelSourceBoneMorphOffset[] CreatePmxBoneMorphOffsets(MmdParserFfiMethods.IPmxSummaryAccessor accessor, int morphIndex)
        {
            int count = CountToArrayLength(accessor.GetMorphBoneOffsetCount(morphIndex), "PMX bone morph offset count");
            var offsets = new PmxModelSourceBoneMorphOffset[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = new PmxModelSourceBoneMorphOffset
                {
                    boneIndex = accessor.GetMorphBoneOffsetBoneIndex(morphIndex, i),
                    translation = ReadVec3(component => accessor.GetMorphBoneOffsetTranslation(morphIndex, i, component)),
                    rotation = ReadVec4(component => accessor.GetMorphBoneOffsetRotation(morphIndex, i, component))
                };
            }

            return offsets;
        }

        private static PmxModelSourceUvMorphOffset[] CreatePmxUvMorphOffsets(MmdParserFfiMethods.IPmxSummaryAccessor accessor, int morphIndex)
        {
            int count = CountToArrayLength(accessor.GetMorphUvOffsetCount(morphIndex), "PMX UV morph offset count");
            var offsets = new PmxModelSourceUvMorphOffset[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = new PmxModelSourceUvMorphOffset
                {
                    vertexIndex = accessor.GetMorphUvOffsetVertexIndex(morphIndex, i),
                    uv = ReadVec4(component => accessor.GetMorphUvOffsetValue(morphIndex, i, component))
                };
            }

            return offsets;
        }

        private static PmxModelSourceAdditionalUvMorphOffset[] CreatePmxAdditionalUvMorphOffsets(MmdParserFfiMethods.IPmxSummaryAccessor accessor, int morphIndex)
        {
            int count = CountToArrayLength(accessor.GetMorphAdditionalUvOffsetCount(morphIndex), "PMX additional UV morph offset count");
            var offsets = new PmxModelSourceAdditionalUvMorphOffset[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = new PmxModelSourceAdditionalUvMorphOffset
                {
                    vertexIndex = accessor.GetMorphAdditionalUvOffsetVertexIndex(morphIndex, i),
                    uvIndex = accessor.GetMorphAdditionalUvOffsetUvIndex(morphIndex, i),
                    uv = ReadVec4(component => accessor.GetMorphAdditionalUvOffsetValue(morphIndex, i, component))
                };
            }

            return offsets;
        }

        private static PmxModelSourceMaterialMorphOffset[] CreatePmxMaterialMorphOffsets(MmdParserFfiMethods.IPmxSummaryAccessor accessor, int morphIndex)
        {
            int count = CountToArrayLength(accessor.GetMorphMaterialOffsetCount(morphIndex), "PMX material morph offset count");
            var offsets = new PmxModelSourceMaterialMorphOffset[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = new PmxModelSourceMaterialMorphOffset
                {
                    materialIndex = accessor.GetMorphMaterialOffsetMaterialIndex(morphIndex, i),
                    operation = accessor.GetMorphMaterialOffsetOperation(morphIndex, i) ?? "unknown",
                    diffuse = ReadVec4(component => accessor.GetMorphMaterialOffsetDiffuse(morphIndex, i, component)),
                    specular = ReadVec3(component => accessor.GetMorphMaterialOffsetSpecular(morphIndex, i, component)),
                    specularPower = accessor.GetMorphMaterialOffsetSpecularPower(morphIndex, i),
                    ambient = ReadVec3(component => accessor.GetMorphMaterialOffsetAmbient(morphIndex, i, component)),
                    edgeColor = ReadVec4(component => accessor.GetMorphMaterialOffsetEdgeColor(morphIndex, i, component)),
                    edgeSize = accessor.GetMorphMaterialOffsetEdgeSize(morphIndex, i),
                    textureFactor = ReadVec4(component => accessor.GetMorphMaterialOffsetTextureFactor(morphIndex, i, component)),
                    sphereTextureFactor = ReadVec4(component => accessor.GetMorphMaterialOffsetSphereTextureFactor(morphIndex, i, component)),
                    toonTextureFactor = ReadVec4(component => accessor.GetMorphMaterialOffsetToonTextureFactor(morphIndex, i, component))
                };
            }

            return offsets;
        }

        private static PmxModelSourceGroupMorphOffset[] CreatePmxFlipMorphOffsets(MmdParserFfiMethods.IPmxSummaryAccessor accessor, int morphIndex)
        {
            int count = CountToArrayLength(accessor.GetMorphFlipOffsetCount(morphIndex), "PMX flip morph offset count");
            var offsets = new PmxModelSourceGroupMorphOffset[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = new PmxModelSourceGroupMorphOffset
                {
                    morphIndex = accessor.GetMorphFlipOffsetMorphIndex(morphIndex, i),
                    weight = accessor.GetMorphFlipOffsetWeight(morphIndex, i)
                };
            }

            return offsets;
        }

        private static PmxModelSourceImpulseMorphOffset[] CreatePmxImpulseMorphOffsets(MmdParserFfiMethods.IPmxSummaryAccessor accessor, int morphIndex)
        {
            int count = CountToArrayLength(accessor.GetMorphImpulseOffsetCount(morphIndex), "PMX impulse morph offset count");
            var offsets = new PmxModelSourceImpulseMorphOffset[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = new PmxModelSourceImpulseMorphOffset
                {
                    rigidBodyIndex = accessor.GetMorphImpulseOffsetRigidbodyIndex(morphIndex, i),
                    local = accessor.GetMorphImpulseOffsetLocal(morphIndex, i),
                    velocity = ReadVec3(component => accessor.GetMorphImpulseOffsetVelocity(morphIndex, i, component)),
                    torque = ReadVec3(component => accessor.GetMorphImpulseOffsetTorque(morphIndex, i, component))
                };
            }

            return offsets;
        }

        private static PmxModelSourceRigidBody[] CreatePmxRigidBodies(MmdParserFfiMethods.IPmxSummaryAccessor accessor)
        {
            int count = CountToArrayLength(accessor.RigidbodyCount, "PMX rigidbody count");
            var bodies = new PmxModelSourceRigidBody[count];
            for (int i = 0; i < count; i++)
            {
                bodies[i] = new PmxModelSourceRigidBody
                {
                    name = accessor.GetRigidbodyName(i) ?? string.Empty,
                    boneIndex = accessor.GetRigidbodyBoneIndex(i),
                    group = accessor.GetRigidbodyGroup(i),
                    mask = accessor.GetRigidbodyMask(i),
                    shape = accessor.GetRigidbodyShape(i) ?? string.Empty,
                    size = ReadVec3(component => accessor.GetRigidbodySize(i, component)),
                    position = ReadVec3(component => accessor.GetRigidbodyPosition(i, component)),
                    rotation = ReadVec3(component => accessor.GetRigidbodyRotation(i, component)),
                    mass = accessor.GetRigidbodyMass(i),
                    linearDamping = accessor.GetRigidbodyLinearDamping(i),
                    angularDamping = accessor.GetRigidbodyAngularDamping(i),
                    restitution = accessor.GetRigidbodyRestitution(i),
                    friction = accessor.GetRigidbodyFriction(i),
                    mode = accessor.GetRigidbodyMode(i) ?? string.Empty
                };
            }

            return bodies;
        }

        private static PmxModelSourceJoint[] CreatePmxJoints(MmdParserFfiMethods.IPmxSummaryAccessor accessor)
        {
            int count = CountToArrayLength(accessor.JointCount, "PMX joint count");
            var joints = new PmxModelSourceJoint[count];
            for (int i = 0; i < count; i++)
            {
                joints[i] = new PmxModelSourceJoint
                {
                    name = accessor.GetJointName(i) ?? string.Empty,
                    rigidBodyIndexA = accessor.GetJointRigidbodyAIndex(i),
                    rigidBodyIndexB = accessor.GetJointRigidbodyBIndex(i),
                    position = ReadVec3(component => accessor.GetJointPosition(i, component)),
                    rotation = ReadVec3(component => accessor.GetJointRotation(i, component)),
                    translationLowerLimit = ReadVec3(component => accessor.GetJointTranslationLowerLimit(i, component)),
                    translationUpperLimit = ReadVec3(component => accessor.GetJointTranslationUpperLimit(i, component)),
                    rotationLowerLimit = ReadVec3(component => accessor.GetJointRotationLowerLimit(i, component)),
                    rotationUpperLimit = ReadVec3(component => accessor.GetJointRotationUpperLimit(i, component)),
                    springTranslationFactor = ReadVec3(component => accessor.GetJointSpringTranslationFactor(i, component)),
                    springRotationFactor = ReadVec3(component => accessor.GetJointSpringRotationFactor(i, component))
                };
            }

            return joints;
        }

        private static float[] ReadVec3(Func<int, float> readComponent)
        {
            return new[] { readComponent(0), readComponent(1), readComponent(2) };
        }

        private static float[] ReadVec4(Func<int, float> readComponent)
        {
            return new[] { readComponent(0), readComponent(1), readComponent(2), readComponent(3) };
        }

        private static int InferIkEffectorIndex(MmdModelDefinition model, MmdIkDefinition ik)
        {
            if (ik.links.Count == 0)
            {
                return ik.targetBoneIndex;
            }

            int firstLinkIndex = ik.links[0].boneIndex;
            var linkIndices = new HashSet<int>();
            for (int i = 0; i < ik.links.Count; i++)
            {
                linkIndices.Add(ik.links[i].boneIndex);
            }

            for (int i = 0; i < model.bones.Count; i++)
            {
                MmdBoneDefinition bone = model.bones[i];
                if (bone.parentIndex == firstLinkIndex && !linkIndices.Contains(bone.index))
                {
                    return bone.index;
                }
            }

            return firstLinkIndex;
        }

        private static int SkinningBoneCount(string skinningMode)
        {
            return skinningMode switch
            {
                "bdef1" => 1,
                "bdef2" or "sdef" => 2,
                "bdef4" or "qdef" => 4,
                _ => 0
            };
        }

        private static byte[] VmdSummaryInterpolation(MmdParserFfiMethods.IVmdSummaryAccessor accessor, int frameIndex, int component)
        {
            return new[]
            {
                accessor.GetBoneFrameInterpolationByte(frameIndex, component),
                accessor.GetBoneFrameInterpolationByte(frameIndex, component + 4),
                accessor.GetBoneFrameInterpolationByte(frameIndex, component + 8),
                accessor.GetBoneFrameInterpolationByte(frameIndex, component + 12)
            };
        }

        private static int CountToArrayLength(int value, string label)
        {
            if (value < 0)
            {
                throw new InvalidOperationException(label + " is out of range: " + value);
            }

            return value;
        }

        private static uint CountToUInt(int value)
        {
            return value < 0 ? 0u : (uint)value;
        }

        private static string GetString(string[] values, int index, string fallback)
        {
            return values != null && index >= 0 && index < values.Length && !string.IsNullOrEmpty(values[index])
                ? values[index]
                : fallback;
        }

        private static bool GetBool(bool[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length && values[index];
        }

        private static int GetUIntAsInt(uint[] values, int index, int fallback)
        {
            return values != null && index >= 0 && index < values.Length
                ? CheckedUIntToInt(values[index], "PMX skin index")
                : fallback;
        }

        private static int CheckedUIntToInt(uint value, string label)
        {
            if (value > int.MaxValue)
            {
                throw new InvalidOperationException(label + " is out of range: " + value);
            }

            return (int)value;
        }

        private static int UIntCountToInt(uint value)
        {
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static float GetFloat(float[] values, int index, float fallback)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : fallback;
        }

        private static float[] Vec2(float[] values, int offset)
        {
            return new[] { GetFloat(values, offset, 0.0f), GetFloat(values, offset + 1, 0.0f) };
        }

        private static float[] Vec3(float[] values, int offset)
        {
            return new[] { GetFloat(values, offset, 0.0f), GetFloat(values, offset + 1, 0.0f), GetFloat(values, offset + 2, 0.0f) };
        }

        private static float[] CopyVec3(float[] values)
        {
            return new[] { GetFloat(values, 0, 0.0f), GetFloat(values, 1, 0.0f), GetFloat(values, 2, 0.0f) };
        }

        private static bool HasVec3(float[] values)
        {
            return values != null && values.Length >= 3;
        }

        private static bool HasLocalAxis(PmxModelSourceLocalAxis axis)
        {
            return axis != null && HasVec3(axis.x) && HasVec3(axis.z);
        }

        private static bool HasAppendTransform(PmxModelSourceBone bone)
        {
            return bone.appendTransform != null &&
                ((bone.flags?.appendRotate ?? false) || (bone.flags?.appendTranslate ?? false));
        }

        private static bool HasIk(PmxModelSourceIk ik)
        {
            return ik != null && ik.links != null && ik.links.Length > 0;
        }

        private static bool HasIkLimit(PmxModelSourceIkLimit limit)
        {
            return limit != null && HasVec3(limit.lower) && HasVec3(limit.upper);
        }

        private static float[] CopyVec4(float[] values)
        {
            return new[] { GetFloat(values, 0, 0.0f), GetFloat(values, 1, 0.0f), GetFloat(values, 2, 0.0f), GetFloat(values, 3, 0.0f) };
        }

        private static byte[] CopyInterpolation(byte[] values)
        {
            return new[]
            {
                values != null && values.Length > 0 ? values[0] : (byte)0,
                values != null && values.Length > 1 ? values[1] : (byte)0,
                values != null && values.Length > 2 ? values[2] : (byte)0,
                values != null && values.Length > 3 ? values[3] : (byte)0
            };
        }

        // MMD camera interpolation is a flat 24-byte block (6 curves x 4 control points:
        // X, Y, Z, rotation, distance, view-angle). Pad/truncate defensively to a fixed 24.
        private static byte[] CopyCameraInterpolation(byte[] values)
        {
            var result = new byte[24];
            if (values != null)
            {
                int count = Math.Min(values.Length, result.Length);
                for (int i = 0; i < count; i++)
                {
                    result[i] = values[i];
                }
            }

            return result;
        }

        private static float[] Vec3From4(float[] values)
        {
            return CopyVec3(values);
        }

        private static float[] SanitizedNormal(float[] normal)
        {
            if (IsFinite(normal[0]) && IsFinite(normal[1]) && IsFinite(normal[2]))
            {
                return normal;
            }

            return new[] { 0.0f, 1.0f, 0.0f };
        }

        private static string NonEmptyMaterialName(string value, int index)
        {
            return string.IsNullOrWhiteSpace(value) ? $"material_{index}" : value;
        }

        private static string NonEmptyPhysicsName(string value, string prefix, int index)
        {
            return string.IsNullOrWhiteSpace(value) ? $"{prefix}_{index}" : value;
        }

        private static string BoneNameOrEmpty(MmdModelDefinition model, int boneIndex)
        {
            return boneIndex >= 0 && boneIndex < model.bones.Count ? model.bones[boneIndex].name : string.Empty;
        }

        private static float NonNegativeFiniteOrZero(float value)
        {
            return IsFinite(value) && value >= 0.0f ? value : 0.0f;
        }

        private static float Clamp01(float value)
        {
            if (!IsFinite(value))
            {
                return 1.0f;
            }

            return value < 0.0f ? 0.0f : value > 1.0f ? 1.0f : value;
        }

        private static float ClampNonNegative(float value)
        {
            return IsFinite(value) && value >= 0.0f ? value : 0.0f;
        }

        private static float[] ClampColor3(float[] values, float[] fallback)
        {
            return new[]
            {
                ClampColorComponent(values, 0, fallback[0]),
                ClampColorComponent(values, 1, fallback[1]),
                ClampColorComponent(values, 2, fallback[2])
            };
        }

        private static float[] ClampColor4(float[] values, float[] fallback)
        {
            return new[]
            {
                ClampColorComponent(values, 0, fallback[0]),
                ClampColorComponent(values, 1, fallback[1]),
                ClampColorComponent(values, 2, fallback[2]),
                ClampColorComponent(values, 3, fallback[3])
            };
        }

        private static float ClampColorComponent(float[] values, int index, float fallback)
        {
            float value = GetFloat(values, index, fallback);
            if (!IsFinite(value))
            {
                return fallback;
            }

            return value < 0.0f ? 0.0f : value > 1.0f ? 1.0f : value;
        }

        private static string SphereTextureModeName(string value)
        {
            return value switch
            {
                "multiply" => "multiply-sphere",
                "add" => "additive-sphere",
                "subTexture" => "sub-texture-sphere",
                "none" or null => "none",
                _ => "unknown-sphere"
            };
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal sealed class VmdMotionSourceSnapshot
        {
            public string TargetModelName = string.Empty;
            public uint MaxFrame;
            public uint CameraKeyframeCount;
            public uint LightKeyframeCount;
            public uint SelfShadowKeyframeCount;
            public VmdMotionSourceBoneFrame[] BoneFrames = Array.Empty<VmdMotionSourceBoneFrame>();
            public VmdMotionSourceMorphFrame[] MorphFrames = Array.Empty<VmdMotionSourceMorphFrame>();
            public VmdMotionSourcePropertyFrame[] PropertyFrames = Array.Empty<VmdMotionSourcePropertyFrame>();
            public VmdMotionSourceCameraFrame[] CameraFrames = Array.Empty<VmdMotionSourceCameraFrame>();
            public VmdMotionSourceLightFrame[] LightFrames = Array.Empty<VmdMotionSourceLightFrame>();
        }

        internal sealed class VmdMotionSourceCameraFrame
        {
            public uint Frame;
            public float Distance;
            public float[] Position = Array.Empty<float>();
            public float[] Rotation = Array.Empty<float>();
            public uint ViewAngle;
            public bool Perspective = true;
            public byte[] Interpolation = Array.Empty<byte>();
        }

        internal sealed class VmdMotionSourceLightFrame
        {
            public uint Frame;
            public float[] Color = Array.Empty<float>();
            public float[] Direction = Array.Empty<float>();
        }

        internal sealed class VmdMotionSourceBoneFrame
        {
            public string BoneName = string.Empty;
            public uint Frame;
            public float[] Translation = Array.Empty<float>();
            public float[] Rotation = Array.Empty<float>();
            public byte[] TranslationXInterpolation = Array.Empty<byte>();
            public byte[] TranslationYInterpolation = Array.Empty<byte>();
            public byte[] TranslationZInterpolation = Array.Empty<byte>();
            public byte[] RotationInterpolation = Array.Empty<byte>();
        }

        internal sealed class VmdMotionSourceMorphFrame
        {
            public string MorphName = string.Empty;
            public uint Frame;
            public float Weight;
        }

        internal sealed class VmdMotionSourcePropertyFrame
        {
            public uint Frame;
            public bool Visible = true;
            public VmdMotionSourceIkState[] IkStates = Array.Empty<VmdMotionSourceIkState>();
        }

        internal sealed class VmdMotionSourceIkState
        {
            public string BoneName = string.Empty;
            public bool Enabled = true;
        }

        [Serializable]
        internal sealed class PmxModelSourceSnapshot
        {
            public PmxModelSourceMetadata metadata = new();
            public PmxModelSourceGeometry geometry = new();
            public PmxModelSourceMaterial[] materials = Array.Empty<PmxModelSourceMaterial>();
            public PmxModelSourceSkeleton skeleton = new();
            public PmxModelSourceMorph[] morphs = Array.Empty<PmxModelSourceMorph>();
            public PmxModelSourceRigidBody[] rigidBodies = Array.Empty<PmxModelSourceRigidBody>();
            public PmxModelSourceJoint[] joints = Array.Empty<PmxModelSourceJoint>();
        }

        [Serializable]
        internal sealed class PmxModelSourceMetadata { public string name = string.Empty; }

        [Serializable]
        internal sealed class PmxModelSourceGeometry
        {
            public float[] positions = Array.Empty<float>();
            public float[] normals = Array.Empty<float>();
            public float[] uvs = Array.Empty<float>();
            public uint[] indices = Array.Empty<uint>();
            public string[] skinningModes = Array.Empty<string>();
            public uint[] skinIndices = Array.Empty<uint>();
            public float[] skinWeights = Array.Empty<float>();
            public bool[] hasSdefParameters = Array.Empty<bool>();
            public float[] sdefC = Array.Empty<float>();
            public float[] sdefR0 = Array.Empty<float>();
            public float[] sdefR1 = Array.Empty<float>();
        }

        [Serializable]
        internal sealed class PmxModelSourceSkeleton { public PmxModelSourceBone[] bones = Array.Empty<PmxModelSourceBone>(); }

        [Serializable]
        internal sealed class PmxModelSourceBone
        {
            public string name = string.Empty;
            public int parentIndex = -1;
            public int layer;
            public float[] position = Array.Empty<float>();
            public PmxModelSourceBoneFlags flags = new();
            public PmxModelSourceAppendTransform appendTransform;
            public float[] fixedAxis;
            public PmxModelSourceLocalAxis localAxis;
            public int externalParentKey = -1;
            public PmxModelSourceIk ik;
        }

        [Serializable]
        internal sealed class PmxModelSourceBoneFlags
        {
            public bool rotatable;
            public bool translatable;
            public bool appendLocal;
            public bool appendRotate;
            public bool appendTranslate;
            public bool externalParentTransform;
        }

        [Serializable]
        internal sealed class PmxModelSourceAppendTransform { public int parentIndex = -1; public float weight; }
        [Serializable]
        internal sealed class PmxModelSourceLocalAxis { public float[] x = Array.Empty<float>(); public float[] z = Array.Empty<float>(); }
        [Serializable]
        internal sealed class PmxModelSourceIk { public int targetIndex; public int loopCount; public float limitAngle; public PmxModelSourceIkLink[] links = Array.Empty<PmxModelSourceIkLink>(); }
        [Serializable]
        internal sealed class PmxModelSourceIkLink { public int boneIndex; public PmxModelSourceIkLimit limits; }
        [Serializable]
        internal sealed class PmxModelSourceIkLimit { public float[] lower = Array.Empty<float>(); public float[] upper = Array.Empty<float>(); }

        [Serializable]
        internal sealed class PmxModelSourceMorph
        {
            public string name = string.Empty;
            public string type = string.Empty;
            public string panel = string.Empty;
            public PmxModelSourceVertexMorphOffset[] vertexOffsets = Array.Empty<PmxModelSourceVertexMorphOffset>();
            public PmxModelSourceGroupMorphOffset[] groupOffsets = Array.Empty<PmxModelSourceGroupMorphOffset>();
            public PmxModelSourceBoneMorphOffset[] boneOffsets = Array.Empty<PmxModelSourceBoneMorphOffset>();
            public PmxModelSourceUvMorphOffset[] uvOffsets = Array.Empty<PmxModelSourceUvMorphOffset>();
            public PmxModelSourceAdditionalUvMorphOffset[] additionalUvOffsets = Array.Empty<PmxModelSourceAdditionalUvMorphOffset>();
            public PmxModelSourceMaterialMorphOffset[] materialOffsets = Array.Empty<PmxModelSourceMaterialMorphOffset>();
            public PmxModelSourceGroupMorphOffset[] flipOffsets = Array.Empty<PmxModelSourceGroupMorphOffset>();
            public PmxModelSourceImpulseMorphOffset[] impulseOffsets = Array.Empty<PmxModelSourceImpulseMorphOffset>();
        }

        [Serializable]
        internal sealed class PmxModelSourceVertexMorphOffset { public uint vertexIndex; public float[] position = Array.Empty<float>(); }
        [Serializable]
        internal sealed class PmxModelSourceGroupMorphOffset { public int morphIndex; public float weight; }
        [Serializable]
        internal sealed class PmxModelSourceBoneMorphOffset { public int boneIndex; public float[] translation = Array.Empty<float>(); public float[] rotation = Array.Empty<float>(); }
        [Serializable]
        internal class PmxModelSourceUvMorphOffset { public uint vertexIndex; public float[] uv = Array.Empty<float>(); }
        [Serializable]
        internal sealed class PmxModelSourceAdditionalUvMorphOffset : PmxModelSourceUvMorphOffset { public byte uvIndex; }
        [Serializable]
        internal sealed class PmxModelSourceMaterialMorphOffset
        {
            public int materialIndex = -1;
            public string operation = "unknown";
            public float[] diffuse = Array.Empty<float>();
            public float[] specular = Array.Empty<float>();
            public float specularPower;
            public float[] ambient = Array.Empty<float>();
            public float[] edgeColor = Array.Empty<float>();
            public float edgeSize;
            public float[] textureFactor = Array.Empty<float>();
            public float[] sphereTextureFactor = Array.Empty<float>();
            public float[] toonTextureFactor = Array.Empty<float>();
        }

        [Serializable]
        internal sealed class PmxModelSourceImpulseMorphOffset { public int rigidBodyIndex = -1; public bool local; public float[] velocity = Array.Empty<float>(); public float[] torque = Array.Empty<float>(); }

        [Serializable]
        internal sealed class PmxModelSourceMaterial
        {
            public string name = string.Empty;
            public string texturePath = string.Empty;
            public string sphereTexturePath = string.Empty;
            public string sphereMode = string.Empty;
            public string toonTexturePath = string.Empty;
            public int sharedToonIndex = -1;
            public float[] diffuse = Array.Empty<float>();
            public float[] ambient = Array.Empty<float>();
            public float[] edgeColor = Array.Empty<float>();
            public float edgeSize;
            public PmxModelSourceMaterialFlags flags = new();
            public int faceCount;
        }

        [Serializable]
        internal sealed class PmxModelSourceMaterialFlags { public bool doubleSided; public bool edge; }
        [Serializable]
        internal sealed class PmxModelSourceRigidBody
        {
            public string name = string.Empty;
            public int boneIndex = -1;
            public int group;
            public int mask;
            public string shape = string.Empty;
            public float[] size = Array.Empty<float>();
            public float[] position = Array.Empty<float>();
            public float[] rotation = Array.Empty<float>();
            public float mass;
            public float linearDamping;
            public float angularDamping;
            public float restitution;
            public float friction;
            public string mode = string.Empty;
        }

        [Serializable]
        internal sealed class PmxModelSourceJoint
        {
            public string name = string.Empty;
            public int rigidBodyIndexA = -1;
            public int rigidBodyIndexB = -1;
            public float[] position = Array.Empty<float>();
            public float[] rotation = Array.Empty<float>();
            public float[] translationLowerLimit = Array.Empty<float>();
            public float[] translationUpperLimit = Array.Empty<float>();
            public float[] rotationLowerLimit = Array.Empty<float>();
            public float[] rotationUpperLimit = Array.Empty<float>();
            public float[] springTranslationFactor = Array.Empty<float>();
            public float[] springRotationFactor = Array.Empty<float>();
        }

    }
}
