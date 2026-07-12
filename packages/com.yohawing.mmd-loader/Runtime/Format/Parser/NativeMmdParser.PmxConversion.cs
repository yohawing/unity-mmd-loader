#nullable enable
#pragma warning disable CS0649

using System;
using System.Collections.Generic;

namespace Mmd.Parser
{
    public sealed partial class NativeMmdParser
    {
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
    }
}