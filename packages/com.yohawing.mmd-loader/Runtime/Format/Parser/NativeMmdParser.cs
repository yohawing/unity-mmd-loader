#nullable enable
#pragma warning disable CS0649

using System;
using System.Collections.Generic;
namespace Mmd.Parser
{
    public sealed class NativeMmdParser : IMmdParser
    {
        private readonly Func<byte[], string> parseVmdJson;
        private readonly Func<byte[], string> parsePmxNonGeometryJson;
        private readonly Func<byte[], PmxModelSourceGeometry> createPmxGeometry;

        public NativeMmdParser()
            : this(MmdParserFfiMethods.ParseVmdJson,
                   MmdParserFfiMethods.ParsePmxNonGeometryJson, CreatePmxGeometryFromNativeBuffers)
        {
        }

        internal NativeMmdParser(
            Func<byte[], string> parseVmdJson)
            : this(parseVmdJson,
                   MmdParserFfiMethods.ParsePmxNonGeometryJson, CreatePmxGeometryFromNativeBuffers)
        {
        }

        internal NativeMmdParser(
            Func<byte[], string> parseVmdJson,
            Func<byte[], string> parsePmxNonGeometryJson,
            Func<byte[], PmxModelSourceGeometry> createPmxGeometry)
        {
            this.parseVmdJson = parseVmdJson ?? throw new ArgumentNullException(nameof(parseVmdJson));
            this.parsePmxNonGeometryJson = parsePmxNonGeometryJson ?? throw new ArgumentNullException(nameof(parsePmxNonGeometryJson));
            this.createPmxGeometry = createPmxGeometry ?? throw new ArgumentNullException(nameof(createPmxGeometry));
        }

        public MmdModelDefinition LoadModel(ReadOnlySpan<byte> data)
        {
            MmdParserInput.RequireNonEmpty(data, nameof(data));
            byte[] bytes = data.ToArray();
            string json = parsePmxNonGeometryJson(bytes);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("mmd-runtime PMX non-geometry JSON parser returned empty JSON.");
            }

            PmxModelSourceSnapshot snapshot = UnityEngine.JsonUtility.FromJson<PmxModelSourceSnapshot>(json)
                ?? new PmxModelSourceSnapshot();
            snapshot.geometry = createPmxGeometry(bytes);
            return BuildModelDefinition(snapshot);
        }

        public MmdMotionDefinition LoadMotion(ReadOnlySpan<byte> data)
        {
            MmdParserInput.RequireNonEmpty(data, nameof(data));
            string json = parseVmdJson(data.ToArray());
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("mmd-runtime VMD JSON parser returned empty JSON.");
            }

            VmdParsedAnimationJson? parsed = UnityEngine.JsonUtility.FromJson<VmdParsedAnimationJson>(json);
            return BuildMotionDefinition(CreateMotionSnapshot(parsed));
        }

        internal static MmdModelDefinition BuildModelDefinition(PmxModelSourceSnapshot? source)
        {
            source ??= new PmxModelSourceSnapshot();
            var model = new MmdModelDefinition
            {
                name = source.metadata?.name ?? string.Empty,
                englishName = source.metadata?.englishName ?? string.Empty,
                comment = source.metadata?.comment ?? string.Empty,
                englishComment = source.metadata?.englishComment ?? string.Empty
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
                    edgeScale = GetOptionalEdgeScale(geometry.edgeScale, i),
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
                PmxModelSourceAppendTransform? appendTransform = hasAppendTransform ? bone.appendTransform : null;
                model.bones.Add(new MmdBoneDefinition
                {
                    index = i,
                    name = bone.name ?? string.Empty,
                    parentIndex = bone.parentIndex,
                    transformOrder = bone.layer,
                    origin = CopyVec3(bone.position),
                    isMovable = bone.flags?.translatable ?? false,
                    isRotatable = bone.flags?.rotatable ?? false,
                    appendParentIndex = appendTransform != null ? appendTransform.parentIndex : -1,
                    appendRatio = appendTransform != null ? appendTransform.weight : 1.0f,
                    appendRotation = bone.flags?.appendRotate ?? false,
                    appendTranslation = bone.flags?.appendTranslate ?? false,
                    appendLocal = bone.flags?.appendLocal ?? false,
                    fixedAxis = HasVec3(bone.fixedAxis),
                    fixedAxisVector = CopyVec3(bone.fixedAxis),
                    localAxes = HasLocalAxis(bone.localAxis),
                    localXAxis = CopyVec3(bone.localAxis?.x),
                    localZAxis = CopyVec3(bone.localAxis?.z),
                    externalParentTransform = bone.externalParentKey >= 0 || (bone.flags?.externalParentTransform ?? false),
                    deformAfterPhysics = bone.flags?.transformAfterPhysics ?? false
                });
            }

            for (int i = 0; i < bones.Length; i++)
            {
                PmxModelSourceIk? ikJson = bones[i]?.ik;
                if (!HasIk(ikJson))
                {
                    continue;
                }

                PmxModelSourceIk sourceIk = ikJson!;
                var ik = new MmdIkDefinition
                {
                    boneIndex = i,
                    targetBoneIndex = sourceIk.targetIndex,
                    iterationCount = sourceIk.loopCount,
                    angleLimit = sourceIk.limitAngle
                };
                PmxModelSourceIkLink[] links = sourceIk.links ?? Array.Empty<PmxModelSourceIkLink>();
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

        internal static MmdMotionDefinition BuildMotionDefinition(VmdMotionSourceSnapshot? source)
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

            foreach (VmdMotionSourceSelfShadowFrame frame in source.SelfShadowFrames ?? Array.Empty<VmdMotionSourceSelfShadowFrame>())
            {
                motion.selfShadowKeyframes.Add(new MmdSelfShadowKeyframeDefinition
                {
                    frame = CheckedUIntToInt(frame?.Frame ?? 0u, "VMD self-shadow frame"),
                    mode = frame?.Mode ?? 0,
                    distance = frame?.Distance ?? 0.0f
                });
            }

            return motion;
        }

        internal static VmdMotionSourceSnapshot CreateMotionSnapshot(VmdParsedAnimationJson? parsed)
        {
            if (parsed == null)
            {
                return new VmdMotionSourceSnapshot();
            }

            VmdParsedMetadataJson metadata = parsed.metadata ?? new VmdParsedMetadataJson();
            VmdParsedBoneFrameJson[] boneFrames = parsed.boneFrames ?? Array.Empty<VmdParsedBoneFrameJson>();
            VmdParsedMorphFrameJson[] morphFrames = parsed.morphFrames ?? Array.Empty<VmdParsedMorphFrameJson>();
            VmdParsedPropertyFrameJson[] propertyFrames = parsed.propertyFrames ?? Array.Empty<VmdParsedPropertyFrameJson>();
            VmdParsedCameraFrameJson[] cameraFrames = parsed.cameraFrames ?? Array.Empty<VmdParsedCameraFrameJson>();
            VmdParsedLightFrameJson[] lightFrames = parsed.lightFrames ?? Array.Empty<VmdParsedLightFrameJson>();
            VmdParsedSelfShadowFrameJson[] selfShadowFrames = parsed.selfShadowFrames ?? Array.Empty<VmdParsedSelfShadowFrameJson>();

            var source = new VmdMotionSourceSnapshot
            {
                TargetModelName = metadata.modelName ?? string.Empty,
                MaxFrame = metadata.maxFrame,
                CameraKeyframeCount = CountToUInt(cameraFrames.Length),
                LightKeyframeCount = CountToUInt(lightFrames.Length),
                SelfShadowKeyframeCount = CountToUInt(selfShadowFrames.Length),
                BoneFrames = new VmdMotionSourceBoneFrame[boneFrames.Length],
                MorphFrames = new VmdMotionSourceMorphFrame[morphFrames.Length],
                PropertyFrames = new VmdMotionSourcePropertyFrame[propertyFrames.Length],
                CameraFrames = new VmdMotionSourceCameraFrame[cameraFrames.Length],
                LightFrames = new VmdMotionSourceLightFrame[lightFrames.Length],
                SelfShadowFrames = new VmdMotionSourceSelfShadowFrame[selfShadowFrames.Length]
            };

            for (int i = 0; i < source.BoneFrames.Length; i++)
            {
                VmdParsedBoneFrameJson frame = boneFrames[i] ?? new VmdParsedBoneFrameJson();
                source.BoneFrames[i] = new VmdMotionSourceBoneFrame
                {
                    BoneName = frame.boneName ?? string.Empty,
                    Frame = frame.frame,
                    Translation = CopyVec3(frame.translation),
                    Rotation = CopyVec4(frame.rotation),
                    TranslationXInterpolation = VmdJsonBoneInterpolation(frame.interpolation, 0),
                    TranslationYInterpolation = VmdJsonBoneInterpolation(frame.interpolation, 1),
                    TranslationZInterpolation = VmdJsonBoneInterpolation(frame.interpolation, 2),
                    RotationInterpolation = VmdJsonBoneInterpolation(frame.interpolation, 3)
                };
            }

            for (int i = 0; i < source.MorphFrames.Length; i++)
            {
                VmdParsedMorphFrameJson frame = morphFrames[i] ?? new VmdParsedMorphFrameJson();
                source.MorphFrames[i] = new VmdMotionSourceMorphFrame
                {
                    MorphName = frame.morphName ?? string.Empty,
                    Frame = frame.frame,
                    Weight = frame.weight
                };
            }

            for (int i = 0; i < source.PropertyFrames.Length; i++)
            {
                VmdParsedPropertyFrameJson frame = propertyFrames[i] ?? new VmdParsedPropertyFrameJson();
                VmdParsedIkStateJson[] ikStates = frame.ikStates ?? Array.Empty<VmdParsedIkStateJson>();
                var propertyFrame = new VmdMotionSourcePropertyFrame
                {
                    Frame = frame.frame,
                    Visible = frame.visible,
                    IkStates = new VmdMotionSourceIkState[ikStates.Length]
                };

                for (int j = 0; j < propertyFrame.IkStates.Length; j++)
                {
                    VmdParsedIkStateJson state = ikStates[j] ?? new VmdParsedIkStateJson();
                    propertyFrame.IkStates[j] = new VmdMotionSourceIkState
                    {
                        BoneName = state.boneName ?? string.Empty,
                        Enabled = state.enabled
                    };
                }

                source.PropertyFrames[i] = propertyFrame;
            }

            for (int i = 0; i < source.CameraFrames.Length; i++)
            {
                VmdParsedCameraFrameJson frame = cameraFrames[i] ?? new VmdParsedCameraFrameJson();
                source.CameraFrames[i] = new VmdMotionSourceCameraFrame
                {
                    Frame = frame.frame,
                    Distance = frame.distance,
                    Position = CopyVec3(frame.position),
                    Rotation = CopyVec3(frame.rotation),
                    ViewAngle = frame.ViewAngle,
                    Perspective = frame.perspective,
                    Interpolation = CopyByteArray(frame.interpolation)
                };
            }

            for (int i = 0; i < source.LightFrames.Length; i++)
            {
                VmdParsedLightFrameJson frame = lightFrames[i] ?? new VmdParsedLightFrameJson();
                source.LightFrames[i] = new VmdMotionSourceLightFrame
                {
                    Frame = frame.frame,
                    Color = CopyVec3(frame.color),
                    Direction = CopyVec3(frame.direction)
                };
            }

            for (int i = 0; i < source.SelfShadowFrames.Length; i++)
            {
                VmdParsedSelfShadowFrameJson frame = selfShadowFrames[i] ?? new VmdParsedSelfShadowFrameJson();
                source.SelfShadowFrames[i] = new VmdMotionSourceSelfShadowFrame
                {
                    Frame = frame.frame,
                    Mode = frame.mode,
                    Distance = frame.distance
                };
            }

            return source;
        }

        internal static PmxModelSourceGeometry CreatePmxGeometryFromNativeBuffers(byte[] data)
        {
            string modesJson = MmdParserFfiMethods.ParsePmxSkinningModesJson(data);
            SkinningModesWrapper modesWrapper = string.IsNullOrWhiteSpace(modesJson)
                ? new SkinningModesWrapper()
                : (UnityEngine.JsonUtility.FromJson<SkinningModesWrapper>(modesJson) ?? new SkinningModesWrapper());

            return new PmxModelSourceGeometry
            {
                positions = MmdParserFfiMethods.ParsePmxPositions(data),
                normals = MmdParserFfiMethods.ParsePmxNormals(data),
                uvs = MmdParserFfiMethods.ParsePmxUvs(data),
                edgeScale = MmdParserFfiMethods.ParsePmxEdgeScale(data),
                indices = MmdParserFfiMethods.ParsePmxIndices(data),
                skinningModes = modesWrapper.skinningModes,
                skinIndices = MmdParserFfiMethods.ParsePmxSkinIndices(data),
                skinWeights = MmdParserFfiMethods.ParsePmxSkinWeights(data),
                hasSdefParameters = MmdParserFfiMethods.ParsePmxSdefEnabled(data),
                sdefC = MmdParserFfiMethods.ParsePmxSdefC(data),
                sdefR0 = MmdParserFfiMethods.ParsePmxSdefR0(data),
                sdefR1 = MmdParserFfiMethods.ParsePmxSdefR1(data),
            };
        }

        [Serializable]
        private sealed class SkinningModesWrapper
        {
            public string[] skinningModes = Array.Empty<string>();
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

        private static int SkinningBoneCount(string? skinningMode)
        {
            return skinningMode switch
            {
                "bdef1" => 1,
                "bdef2" or "sdef" => 2,
                "bdef4" or "qdef" => 4,
                _ => 0
            };
        }

        private static byte[] VmdJsonBoneInterpolation(int[] values, int component)
        {
            return new[]
            {
                JsonByteAt(values, component),
                JsonByteAt(values, component + 4),
                JsonByteAt(values, component + 8),
                JsonByteAt(values, component + 12)
            };
        }

        private static byte JsonByteAt(int[]? values, int index)
        {
            if (values == null || index < 0 || index >= values.Length)
            {
                return 0;
            }

            int value = values[index];
            return value < 0 ? (byte)0 : value > byte.MaxValue ? byte.MaxValue : (byte)value;
        }

        private static byte[] CopyByteArray(int[]? values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var result = new byte[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                result[i] = JsonByteAt(values, i);
            }

            return result;
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

        private static string GetString(string[]? values, int index, string fallback)
        {
            return values != null && index >= 0 && index < values.Length && !string.IsNullOrEmpty(values[index])
                ? values[index]
                : fallback;
        }

        private static bool GetBool(bool[]? values, int index)
        {
            return values != null && index >= 0 && index < values.Length && values[index];
        }

        private static int GetUIntAsInt(uint[]? values, int index, int fallback)
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

        private static float GetFloat(float[]? values, int index, float fallback)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : fallback;
        }

        private static float GetOptionalEdgeScale(float[]? values, int index)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : float.NaN;
        }

        private static float[] Vec2(float[]? values, int offset)
        {
            return new[] { GetFloat(values, offset, 0.0f), GetFloat(values, offset + 1, 0.0f) };
        }

        private static float[] Vec3(float[]? values, int offset)
        {
            return new[] { GetFloat(values, offset, 0.0f), GetFloat(values, offset + 1, 0.0f), GetFloat(values, offset + 2, 0.0f) };
        }

        private static float[] CopyVec3(float[]? values)
        {
            return new[] { GetFloat(values, 0, 0.0f), GetFloat(values, 1, 0.0f), GetFloat(values, 2, 0.0f) };
        }

        private static bool HasVec3(float[]? values)
        {
            return values != null && values.Length >= 3;
        }

        private static bool HasLocalAxis(PmxModelSourceLocalAxis? axis)
        {
            return axis != null && HasVec3(axis.x) && HasVec3(axis.z);
        }

        private static bool HasAppendTransform(PmxModelSourceBone? bone)
        {
            return bone != null &&
                bone.appendTransform != null &&
                ((bone.flags?.appendRotate ?? false) || (bone.flags?.appendTranslate ?? false));
        }

        private static bool HasIk(PmxModelSourceIk? ik)
        {
            return ik != null && ik.links != null && ik.links.Length > 0;
        }

        private static bool HasIkLimit(PmxModelSourceIkLimit? limit)
        {
            return limit != null && HasVec3(limit.lower) && HasVec3(limit.upper);
        }

        private static float[] CopyVec4(float[]? values)
        {
            return new[] { GetFloat(values, 0, 0.0f), GetFloat(values, 1, 0.0f), GetFloat(values, 2, 0.0f), GetFloat(values, 3, 0.0f) };
        }

        private static byte[] CopyInterpolation(byte[]? values)
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
        private static byte[] CopyCameraInterpolation(byte[]? values)
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

        private static float[] Vec3From4(float[]? values)
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

        private static string NonEmptyMaterialName(string? value, int index)
        {
            return string.IsNullOrWhiteSpace(value) ? $"material_{index}" : value;
        }

        private static string NonEmptyPhysicsName(string? value, string prefix, int index)
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

        private static float[] ClampColor3(float[]? values, float[] fallback)
        {
            return new[]
            {
                ClampColorComponent(values, 0, fallback[0]),
                ClampColorComponent(values, 1, fallback[1]),
                ClampColorComponent(values, 2, fallback[2])
            };
        }

        private static float[] ClampColor4(float[]? values, float[] fallback)
        {
            return new[]
            {
                ClampColorComponent(values, 0, fallback[0]),
                ClampColorComponent(values, 1, fallback[1]),
                ClampColorComponent(values, 2, fallback[2]),
                ClampColorComponent(values, 3, fallback[3])
            };
        }

        private static float ClampColorComponent(float[]? values, int index, float fallback)
        {
            float value = GetFloat(values, index, fallback);
            if (!IsFinite(value))
            {
                return fallback;
            }

            return value < 0.0f ? 0.0f : value > 1.0f ? 1.0f : value;
        }

        private static string SphereTextureModeName(string? value)
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
            public string? TargetModelName = string.Empty;
            public uint MaxFrame;
            public uint CameraKeyframeCount;
            public uint LightKeyframeCount;
            public uint SelfShadowKeyframeCount;
            public VmdMotionSourceBoneFrame[] BoneFrames = Array.Empty<VmdMotionSourceBoneFrame>();
            public VmdMotionSourceMorphFrame[] MorphFrames = Array.Empty<VmdMotionSourceMorphFrame>();
            public VmdMotionSourcePropertyFrame[] PropertyFrames = Array.Empty<VmdMotionSourcePropertyFrame>();
            public VmdMotionSourceCameraFrame[] CameraFrames = Array.Empty<VmdMotionSourceCameraFrame>();
            public VmdMotionSourceLightFrame[] LightFrames = Array.Empty<VmdMotionSourceLightFrame>();
            public VmdMotionSourceSelfShadowFrame[] SelfShadowFrames = Array.Empty<VmdMotionSourceSelfShadowFrame>();
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

        internal sealed class VmdMotionSourceSelfShadowFrame
        {
            public uint Frame;
            public byte Mode;
            public float Distance;
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
        internal sealed class VmdParsedAnimationJson
        {
            public VmdParsedMetadataJson metadata = new();
            public VmdParsedBoneFrameJson[] boneFrames = Array.Empty<VmdParsedBoneFrameJson>();
            public VmdParsedMorphFrameJson[] morphFrames = Array.Empty<VmdParsedMorphFrameJson>();
            public VmdParsedCameraFrameJson[] cameraFrames = Array.Empty<VmdParsedCameraFrameJson>();
            public VmdParsedLightFrameJson[] lightFrames = Array.Empty<VmdParsedLightFrameJson>();
            public VmdParsedSelfShadowFrameJson[] selfShadowFrames = Array.Empty<VmdParsedSelfShadowFrameJson>();
            public VmdParsedPropertyFrameJson[] propertyFrames = Array.Empty<VmdParsedPropertyFrameJson>();
        }

        [Serializable]
        internal sealed class VmdParsedMetadataJson
        {
            public string modelName = string.Empty;
            public VmdParsedCountsJson counts = new();
            public uint maxFrame;
        }

        [Serializable]
        internal sealed class VmdParsedCountsJson
        {
            public int bones;
            public int morphs;
            public int cameras;
            public int lights;
            public int selfShadows;
            public int properties;
        }

        [Serializable]
        internal sealed class VmdParsedBoneFrameJson
        {
            public string boneName = string.Empty;
            public uint frame;
            public float[] translation = Array.Empty<float>();
            public float[] rotation = Array.Empty<float>();
            public int[] interpolation = Array.Empty<int>();
        }

        [Serializable]
        internal sealed class VmdParsedMorphFrameJson
        {
            public string morphName = string.Empty;
            public uint frame;
            public float weight;
        }

        [Serializable]
        internal sealed class VmdParsedPropertyFrameJson
        {
            public uint frame;
            public bool visible = true;
            public VmdParsedIkStateJson[] ikStates = Array.Empty<VmdParsedIkStateJson>();
        }

        [Serializable]
        internal sealed class VmdParsedIkStateJson
        {
            public string boneName = string.Empty;
            public bool enabled = true;
        }

        [Serializable]
        internal sealed class VmdParsedCameraFrameJson
        {
            public uint frame;
            public float distance;
            public float[] position = Array.Empty<float>();
            public float[] rotation = Array.Empty<float>();
            public int[] interpolation = Array.Empty<int>();
            public uint fov;
            public uint viewAngle;
            public bool perspective = true;

            public uint ViewAngle => fov != 0u || viewAngle == 0u ? fov : viewAngle;
        }

        [Serializable]
        internal sealed class VmdParsedLightFrameJson
        {
            public uint frame;
            public float[] color = Array.Empty<float>();
            public float[] direction = Array.Empty<float>();
        }

        [Serializable]
        internal sealed class VmdParsedSelfShadowFrameJson
        {
            public uint frame;
            public byte mode;
            public float distance;
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
        internal sealed class PmxModelSourceMetadata
        {
            public string name = string.Empty;
            public string englishName = string.Empty;
            public string comment = string.Empty;
            public string englishComment = string.Empty;
        }

        [Serializable]
        internal sealed class PmxModelSourceGeometry
        {
            public float[] positions = Array.Empty<float>();
            public float[] normals = Array.Empty<float>();
            public float[] uvs = Array.Empty<float>();
            public float[] edgeScale = Array.Empty<float>();
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
            public PmxModelSourceAppendTransform? appendTransform;
            public float[]? fixedAxis;
            public PmxModelSourceLocalAxis? localAxis;
            public int externalParentKey = -1;
            public PmxModelSourceIk? ik;
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
            public bool transformAfterPhysics;
        }

        [Serializable]
        internal sealed class PmxModelSourceAppendTransform { public int parentIndex = -1; public float weight; }
        [Serializable]
        internal sealed class PmxModelSourceLocalAxis { public float[] x = Array.Empty<float>(); public float[] z = Array.Empty<float>(); }
        [Serializable]
        internal sealed class PmxModelSourceIk { public int targetIndex; public int loopCount; public float limitAngle; public PmxModelSourceIkLink[] links = Array.Empty<PmxModelSourceIkLink>(); }
        [Serializable]
        internal sealed class PmxModelSourceIkLink { public int boneIndex; public PmxModelSourceIkLimit? limits; }
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
