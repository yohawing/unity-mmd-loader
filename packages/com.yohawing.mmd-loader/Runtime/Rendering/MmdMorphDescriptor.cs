#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd.Parser;

namespace Mmd.Rendering
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

    public static partial class MmdMorphDescriptorBuilder
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
