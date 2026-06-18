using System;
using System.Collections.Generic;
using System.Linq;
using Yohawing.MmdUnity.Parser;

namespace Yohawing.MmdUnity.Rendering
{
    [Serializable]
    public sealed class MmdVertexMorphDescriptor
    {
        public int morphIndex;
        public string morphName = string.Empty;
        public List<MmdVertexMorphOffsetDescriptor> offsets = new();
    }

    [Serializable]
    public sealed class MmdVertexMorphOffsetDescriptor
    {
        public int vertexIndex;
        public float[] positionDelta = Array.Empty<float>();
    }

    [Serializable]
    public sealed class MmdGroupMorphDescriptor
    {
        public int morphIndex;
        public string morphName = string.Empty;
        public List<MmdGroupMorphOffsetDescriptor> offsets = new();
    }

    [Serializable]
    public sealed class MmdGroupMorphOffsetDescriptor
    {
        public int targetMorphIndex;
        public string targetMorphName = string.Empty;
        public float weight;
    }

    [Serializable]
    public sealed class MmdMorphTypeInventoryDescriptor
    {
        public int morphIndex;
        public string morphName = string.Empty;
        public string morphType = string.Empty;
        public string panel = string.Empty;
        public string normalizedFamily = string.Empty;
        public int vertexOffsetCount;
        public int groupOffsetCount;
        public int materialOffsetCount;
        public int uvOffsetCount;
        public int boneOffsetCount;
        public int flipOffsetCount;
        public int impulseOffsetCount;
        public string supportStatus = string.Empty;
        public string supportReason = string.Empty;
        public bool evaluatedByVertexMorphEvaluator;
    }

    public static class MmdMorphDescriptorBuilder
    {
        private static readonly HashSet<string> KnownMorphTypes = new(StringComparer.Ordinal)
        {
            "vertex",
            "group",
            "bone",
            "texture",
            "uva1",
            "uva2",
            "uva3",
            "uva4",
            "material",
            "flip",
            "impulse",
            "unknown"
        };

        public const string SupportedStatus = "supported";
        public const string NoOpSupportedStatus = "no-op-supported";
        public const string InventoryOnlyStatus = "inventory-only";
        public const string FutureStatus = "future";
        public const string NotSupportedStatus = "not-supported";
        public const string VertexMorphEvaluatorReason = "vertex-morph-evaluator";
        public const string VertexMorphNoOffsetsReason = "vertex-morph-no-offsets";
        public const string CompositeMorphInventoryOnlyReason = "composite-morph-inventory-only";
        public const string FlipMorphEvaluatorReason = "flip-morph-weight-resolver";
        public const string BoneMorphEvaluatorReason = "bone-morph-evaluator";
        public const string TextureUvMorphEvaluatorReason = "texture-uv-morph-evaluator";
        public const string MaterialMorphEvaluatorReason = "material-morph-evaluator";
        public const string FutureRuntimePathReason = "future-runtime-path";
        public const string PhysicsImpulseOutOfScopeReason = "physics-impulse-out-of-scope";
        public const string UnknownMorphTypeReason = "unknown-morph-type";

        public static IReadOnlyList<MmdVertexMorphDescriptor> BuildVertexMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            return model.morphs
                .Where(morph => string.Equals(NormalizeMorphType(morph.type), "vertex", StringComparison.Ordinal))
                .OrderBy(morph => morph.index)
                .Select(morph => new MmdVertexMorphDescriptor
                {
                    morphIndex = morph.index,
                    morphName = morph.name,
                    offsets = morph.vertexOffsets
                        .OrderBy(offset => offset.vertexIndex)
                        .Select(offset => new MmdVertexMorphOffsetDescriptor
                        {
                            vertexIndex = offset.vertexIndex,
                            positionDelta = offset.positionDelta.ToArray()
                        })
                        .ToList()
                })
                .ToList();
        }

        public static IReadOnlyList<MmdMorphTypeInventoryDescriptor> BuildTypeInventory(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            return model.morphs
                .OrderBy(morph => morph.index)
                .Select(morph =>
                {
                    string morphType = NormalizeMorphType(morph.type);
                    int vertexOffsetCount = morph.vertexOffsets.Count;
                    int groupOffsetCount = morph.groupOffsets.Count;
                    int materialOffsetCount = morph.materialOffsets.Count;
                    int uvOffsetCount = morph.uvOffsets.Count;
                    int boneOffsetCount = morph.boneOffsets.Count;
                    int flipOffsetCount = morph.flipOffsets != null ? morph.flipOffsets.Count : 0;
                    int impulseOffsetCount = morph.impulseOffsets != null ? morph.impulseOffsets.Count : 0;
                    bool evaluatedByVertexMorphEvaluator = string.Equals(morphType, "vertex", StringComparison.Ordinal)
                        && vertexOffsetCount > 0;
                    return new MmdMorphTypeInventoryDescriptor
                    {
                        morphIndex = morph.index,
                        morphName = morph.name,
                        morphType = morphType,
                        panel = morph.panel ?? string.Empty,
                        normalizedFamily = GetNormalizedFamily(morphType),
                        vertexOffsetCount = vertexOffsetCount,
                        groupOffsetCount = groupOffsetCount,
                        materialOffsetCount = materialOffsetCount,
                        uvOffsetCount = uvOffsetCount,
                        boneOffsetCount = boneOffsetCount,
                        flipOffsetCount = flipOffsetCount,
                        impulseOffsetCount = impulseOffsetCount,
                        supportStatus = GetSupportStatus(morphType, vertexOffsetCount),
                        supportReason = GetSupportReason(morphType, vertexOffsetCount),
                        evaluatedByVertexMorphEvaluator = evaluatedByVertexMorphEvaluator
                    };
                })
                .ToList();
        }

        private static string NormalizeMorphType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            string trimmed = value.Trim();
            return KnownMorphTypes.Contains(trimmed) ? trimmed : "unknown";
        }

        private static string GetNormalizedFamily(string morphType)
        {
            return morphType switch
            {
                "vertex" => "vertex",
                "group" => "composite",
                "flip" => "composite",
                "bone" => "bone",
                "texture" => "uv",
                "uva1" => "uv",
                "uva2" => "uv",
                "uva3" => "uv",
                "uva4" => "uv",
                "material" => "material",
                "impulse" => "physics",
                _ => "unknown"
            };
        }

        private static string GetSupportStatus(string morphType, int vertexOffsetCount)
        {
            return morphType switch
            {
                "vertex" => vertexOffsetCount > 0 ? SupportedStatus : NoOpSupportedStatus,
                "group" => InventoryOnlyStatus,
                "flip" => SupportedStatus,
                "bone" => SupportedStatus,
                "texture" => SupportedStatus,
                "uva1" => FutureStatus,
                "uva2" => FutureStatus,
                "uva3" => FutureStatus,
                "uva4" => FutureStatus,
                "material" => SupportedStatus,
                "impulse" => NotSupportedStatus,
                _ => NotSupportedStatus
            };
        }

        public static IReadOnlyList<MmdGroupMorphDescriptor> BuildGroupMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);

            var morphIndexToName = new Dictionary<int, string>(model.morphs.Count);
            foreach (MmdMorphDefinition morph in model.morphs)
            {
                morphIndexToName[morph.index] = morph.name;
            }

            return model.morphs
                .Where(morph => string.Equals(NormalizeMorphType(morph.type), "group", StringComparison.Ordinal))
                .OrderBy(morph => morph.index)
                .Select(morph => new MmdGroupMorphDescriptor
                {
                    morphIndex = morph.index,
                    morphName = morph.name,
                    offsets = (morph.groupOffsets ?? new List<MmdGroupMorphOffsetDefinition>())
                        .OrderBy(offset => offset.morphIndex)
                        .Select(offset =>
                        {
                            string targetName = morphIndexToName.TryGetValue(offset.morphIndex, out string? name)
                                ? name
                                : string.Empty;
                            return new MmdGroupMorphOffsetDescriptor
                            {
                                targetMorphIndex = offset.morphIndex,
                                targetMorphName = targetName,
                                weight = offset.weight
                            };
                        })
                        .ToList()
                })
                .ToList();
        }

        [Serializable]
        public sealed class MmdUvMorphDescriptor
        {
            public int morphIndex;
            public string morphName = string.Empty;
            public string morphType = string.Empty;
            public int uvOffsetCount;
            public List<MmdUvMorphOffsetDescriptor> offsets = new();
        }

        [Serializable]
        public sealed class MmdUvMorphOffsetDescriptor
        {
            public int vertexIndex;
            public float[] positionDelta = Array.Empty<float>();
            public bool targetVertexExists;
            public bool allPayloadFinite;
        }

        public static IReadOnlyList<MmdUvMorphDescriptor> BuildUvMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);

            var knownUvTypes = new HashSet<string>(StringComparer.Ordinal)
            {
                "texture", "uva1", "uva2", "uva3", "uva4"
            };

            var vertexIndices = new HashSet<int>(model.vertices.Count);
            foreach (MmdVertexDefinition vertex in model.vertices)
            {
                vertexIndices.Add(vertex.index);
            }

            return model.morphs
                .Where(morph => knownUvTypes.Contains(NormalizeMorphType(morph.type)))
                .OrderBy(morph => morph.index)
                .Select(morph =>
                {
                    string morphType = NormalizeMorphType(morph.type);
                    IReadOnlyList<MmdUvMorphOffsetDefinition> offsets = morph.uvOffsets ?? new List<MmdUvMorphOffsetDefinition>();
                    return new MmdUvMorphDescriptor
                    {
                        morphIndex = morph.index,
                        morphName = morph.name,
                        morphType = morphType,
                        uvOffsetCount = offsets.Count,
                        offsets = offsets
                            .OrderBy(offset => offset.vertexIndex)
                            .Select(offset =>
                            {
                                bool targetExists = vertexIndices.Contains(offset.vertexIndex);
                                float[]? positionDelta = offset.positionDelta;
                                bool allFinite = positionDelta != null &&
                                    positionDelta.Length == 4 &&
                                    positionDelta.All(float.IsFinite);
                                return new MmdUvMorphOffsetDescriptor
                                {
                                    vertexIndex = offset.vertexIndex,
                                    positionDelta = positionDelta != null && positionDelta.Length == 4
                                        ? new[] { positionDelta[0], positionDelta[1], positionDelta[2], positionDelta[3] }
                                        : new float[] { 0, 0, 0, 0 },
                                    targetVertexExists = targetExists,
                                    allPayloadFinite = allFinite
                                };
                            })
                            .ToList()
                    };
                })
                .ToList();
        }

        [Serializable]
        public sealed class MmdBoneMorphDescriptor
        {
            public int morphIndex;
            public string morphName = string.Empty;
            public int boneOffsetCount;
            public List<MmdBoneMorphOffsetDescriptor> offsets = new();
        }

        [Serializable]
        public sealed class MmdBoneMorphOffsetDescriptor
        {
            public int boneIndex;
            public string targetBoneName = string.Empty;
            public bool targetBoneExists;
            public float[] translation = Array.Empty<float>();
            public float[] orientation = Array.Empty<float>();
            public bool allPayloadFinite;
        }

        public static IReadOnlyList<MmdBoneMorphDescriptor> BuildBoneMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);

            var boneIndexToName = new Dictionary<int, string>(model.bones.Count);
            foreach (MmdBoneDefinition bone in model.bones)
            {
                boneIndexToName[bone.index] = bone.name;
            }

            var boneIndices = new HashSet<int>();
            foreach (MmdBoneDefinition bone in model.bones)
            {
                boneIndices.Add(bone.index);
            }

            return model.morphs
                .Where(morph => string.Equals(NormalizeMorphType(morph.type), "bone", StringComparison.Ordinal))
                .OrderBy(morph => morph.index)
                .Select(morph =>
                {
                    IReadOnlyList<MmdBoneMorphOffsetDefinition> offsets = morph.boneOffsets ?? new List<MmdBoneMorphOffsetDefinition>();
                    return new MmdBoneMorphDescriptor
                    {
                        morphIndex = morph.index,
                        morphName = morph.name,
                        boneOffsetCount = offsets.Count,
                        offsets = offsets
                            .Select(offset =>
                            {
                                string targetName = boneIndexToName.TryGetValue(offset.boneIndex, out string? name)
                                    ? name : string.Empty;
                                bool targetExists = boneIndices.Contains(offset.boneIndex);
                                float[]? translation = offset.translation;
                                float[]? orientation = offset.orientation;
                                bool allFinite = translation != null &&
                                    translation.Length == 3 &&
                                    translation.All(float.IsFinite) &&
                                    orientation != null &&
                                    orientation.Length == 4 &&
                                    orientation.All(float.IsFinite);
                                return new MmdBoneMorphOffsetDescriptor
                                {
                                    boneIndex = offset.boneIndex,
                                    targetBoneName = targetName,
                                    targetBoneExists = targetExists,
                                    translation = translation != null && translation.Length == 3
                                        ? new[] { translation[0], translation[1], translation[2] }
                                        : new float[] { 0, 0, 0 },
                                    orientation = orientation != null && orientation.Length == 4
                                        ? new[] { orientation[0], orientation[1], orientation[2], orientation[3] }
                                        : new float[] { 0, 0, 0, 1 },
                                    allPayloadFinite = allFinite
                                };
                            })
                            .ToList()
                    };
                })
                .ToList();
        }

        [Serializable]
        public sealed class MmdFlipMorphDescriptor
        {
            public int morphIndex;
            public string morphName = string.Empty;
            public int flipOffsetCount;
            public List<MmdFlipMorphOffsetDescriptor> offsets = new();
        }

        [Serializable]
        public sealed class MmdFlipMorphOffsetDescriptor
        {
            public int targetMorphIndex;
            public string targetMorphName = string.Empty;
            public string targetMorphType = string.Empty;
            public float weight;
            public bool finiteWeight;
        }

        public static IReadOnlyList<MmdFlipMorphDescriptor> BuildFlipMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);

            var morphIndexToName = new Dictionary<int, string>(model.morphs.Count);
            var morphIndexToType = new Dictionary<int, string>(model.morphs.Count);
            foreach (MmdMorphDefinition morph in model.morphs)
            {
                morphIndexToName[morph.index] = morph.name;
                morphIndexToType[morph.index] = NormalizeMorphType(morph.type);
            }

            return model.morphs
                .Where(morph => string.Equals(NormalizeMorphType(morph.type), "flip", StringComparison.Ordinal))
                .OrderBy(morph => morph.index)
                .Select(morph =>
                {
                    IReadOnlyList<MmdFlipMorphOffsetDefinition> offsets = morph.flipOffsets ?? new List<MmdFlipMorphOffsetDefinition>();
                    return new MmdFlipMorphDescriptor
                    {
                        morphIndex = morph.index,
                        morphName = morph.name,
                        flipOffsetCount = offsets.Count,
                        offsets = offsets
                            .Select(offset =>
                            {
                                string targetName = morphIndexToName.TryGetValue(offset.morphIndex, out string? name)
                                    ? name : string.Empty;
                                string targetType = morphIndexToType.TryGetValue(offset.morphIndex, out string? type)
                                    ? type : string.Empty;
                                return new MmdFlipMorphOffsetDescriptor
                                {
                                    targetMorphIndex = offset.morphIndex,
                                    targetMorphName = targetName,
                                    targetMorphType = targetType,
                                    weight = offset.weight,
                                    finiteWeight = float.IsFinite(offset.weight)
                                };
                            })
                            .ToList()
                    };
                })
                .ToList();
        }

        [Serializable]
        public sealed class MmdImpulseMorphDescriptor
        {
            public int morphIndex;
            public string morphName = string.Empty;
            public int impulseOffsetCount;
            public List<MmdImpulseMorphOffsetDescriptor> offsets = new();
        }

        [Serializable]
        public sealed class MmdImpulseMorphOffsetDescriptor
        {
            public int rigidbodyIndex = -1;
            public string rigidbodyName = string.Empty;
            public bool targetRigidbodyExists;
            public float[] velocity = Array.Empty<float>();
            public float[] torque = Array.Empty<float>();
            public bool local;
            public bool allPayloadFinite;
        }

        [Serializable]
        public sealed class MmdMaterialMorphDescriptor
        {
            public int morphIndex;
            public string morphName = string.Empty;
            public List<MmdMaterialMorphOffsetDescriptor> offsets = new();
        }

        [Serializable]
        public sealed class MmdMaterialMorphOffsetDescriptor
        {
            public int materialIndex = -1;
            public string operation = "unknown";
            public float[] diffuseColor = Array.Empty<float>();
            public float diffuseOpacity = 1.0f;
            public float[] ambientColor = Array.Empty<float>();
            public float[] edgeColor = Array.Empty<float>();
            public float edgeOpacity = 1.0f;
            public float edgeSize;
            public bool allOffsetsFinite;
        }

        public static IReadOnlyList<MmdMaterialMorphDescriptor> BuildMaterialMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);

            return model.morphs
                .Where(morph => string.Equals(NormalizeMorphType(morph.type), "material", StringComparison.Ordinal))
                .OrderBy(morph => morph.index)
                .Select(morph =>
                {
                    IReadOnlyList<MmdMaterialMorphOffsetDefinition> offsets = morph.materialOffsets ?? new List<MmdMaterialMorphOffsetDefinition>();
                    return new MmdMaterialMorphDescriptor
                    {
                        morphIndex = morph.index,
                        morphName = morph.name,
                        offsets = offsets
                            .OrderBy(offset => offset.materialIndex)
                            .Select(offset =>
                            {
                                float[] diffuseColor = offset.diffuseColor ?? Array.Empty<float>();
                                float[] ambientColor = offset.ambientColor ?? Array.Empty<float>();
                                float[] edgeColor = offset.edgeColor ?? Array.Empty<float>();
                                bool allFinite =
                                    diffuseColor.Length == 3 &&
                                    diffuseColor.All(float.IsFinite) &&
                                    float.IsFinite(offset.diffuseOpacity) &&
                                    ambientColor.Length == 3 &&
                                    ambientColor.All(float.IsFinite) &&
                                    edgeColor.Length == 3 &&
                                    edgeColor.All(float.IsFinite) &&
                                    float.IsFinite(offset.edgeOpacity) &&
                                    float.IsFinite(offset.edgeSize);
                                return new MmdMaterialMorphOffsetDescriptor
                                {
                                    materialIndex = offset.materialIndex,
                                    operation = offset.operation,
                                    diffuseColor = diffuseColor.Length == 3
                                        ? new[] { diffuseColor[0], diffuseColor[1], diffuseColor[2] }
                                        : Array.Empty<float>(),
                                    diffuseOpacity = offset.diffuseOpacity,
                                    ambientColor = ambientColor.Length == 3
                                        ? new[] { ambientColor[0], ambientColor[1], ambientColor[2] }
                                        : Array.Empty<float>(),
                                    edgeColor = edgeColor.Length >= 3
                                        ? new[] { edgeColor[0], edgeColor[1], edgeColor[2] }
                                        : Array.Empty<float>(),
                                    edgeOpacity = offset.edgeOpacity,
                                    edgeSize = offset.edgeSize,
                                    allOffsetsFinite = allFinite
                                };
                            })
                            .ToList()
                    };
                })
                .ToList();
        }

        public static IReadOnlyList<MmdImpulseMorphDescriptor> BuildImpulseMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);

            var rigidbodyIndexToName = new Dictionary<int, string>(model.physics.rigidbodies.Count);
            var rigidbodyIndices = new HashSet<int>();
            foreach (MmdRigidbodyDefinition rb in model.physics.rigidbodies)
            {
                rigidbodyIndexToName[rb.index] = rb.name;
                rigidbodyIndices.Add(rb.index);
            }

            return model.morphs
                .Where(morph => string.Equals(NormalizeMorphType(morph.type), "impulse", StringComparison.Ordinal))
                .OrderBy(morph => morph.index)
                .Select(morph =>
                {
                    IReadOnlyList<MmdImpulseMorphOffsetDefinition> offsets = morph.impulseOffsets ?? new List<MmdImpulseMorphOffsetDefinition>();
                    return new MmdImpulseMorphDescriptor
                    {
                        morphIndex = morph.index,
                        morphName = morph.name,
                        impulseOffsetCount = offsets.Count,
                        offsets = offsets
                            .Select(offset =>
                            {
                                string targetName = rigidbodyIndexToName.TryGetValue(offset.rigidbodyIndex, out string? name)
                                    ? name : string.Empty;
                                bool targetExists = rigidbodyIndices.Contains(offset.rigidbodyIndex);
                                float[]? velocity = offset.velocity;
                                float[]? torque = offset.torque;
                                bool allFinite = velocity != null &&
                                    velocity.Length == 3 &&
                                    velocity.All(float.IsFinite) &&
                                    torque != null &&
                                    torque.Length == 3 &&
                                    torque.All(float.IsFinite);
                                return new MmdImpulseMorphOffsetDescriptor
                                {
                                    rigidbodyIndex = offset.rigidbodyIndex,
                                    rigidbodyName = targetName,
                                    targetRigidbodyExists = targetExists,
                                    velocity = velocity != null && velocity.Length == 3
                                        ? new[] { velocity[0], velocity[1], velocity[2] }
                                        : new float[] { 0, 0, 0 },
                                    torque = torque != null && torque.Length == 3
                                        ? new[] { torque[0], torque[1], torque[2] }
                                        : new float[] { 0, 0, 0 },
                                    local = offset.local,
                                    allPayloadFinite = allFinite
                                };
                            })
                            .ToList()
                    };
                })
                .ToList();
        }

        private static string GetSupportReason(string morphType, int vertexOffsetCount)
        {
            return morphType switch
            {
                "vertex" => vertexOffsetCount > 0 ? VertexMorphEvaluatorReason : VertexMorphNoOffsetsReason,
                "group" => CompositeMorphInventoryOnlyReason,
                "flip" => FlipMorphEvaluatorReason,
                "bone" => BoneMorphEvaluatorReason,
                "texture" => TextureUvMorphEvaluatorReason,
                "uva1" => FutureRuntimePathReason,
                "uva2" => FutureRuntimePathReason,
                "uva3" => FutureRuntimePathReason,
                "uva4" => FutureRuntimePathReason,
                "material" => MaterialMorphEvaluatorReason,
                "impulse" => PhysicsImpulseOutOfScopeReason,
                _ => UnknownMorphTypeReason
            };
        }
    }
}
