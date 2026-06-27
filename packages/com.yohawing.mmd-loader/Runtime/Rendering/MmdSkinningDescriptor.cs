#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd.Parser;

namespace Mmd.Rendering
{
    [Serializable]
    public sealed class MmdSkinningDescriptor
    {
        public int vertexIndex;
        public string skinningMode = string.Empty;
        public string normalizedFamily = string.Empty;
        public int[] boneIndices = Array.Empty<int>();
        public float[] boneWeights = Array.Empty<float>();
        public string supportStatus = string.Empty;
        public string supportReason = string.Empty;
        public bool exactRuntimeSkinning;
        public bool linearFallbackToBoneWeights;
    }

    [Serializable]
    public sealed class MmdSkinningModeInventoryDescriptor
    {
        public int vertexIndex;
        public string skinningMode = string.Empty;
        public string normalizedFamily = string.Empty;
        public int boneInfluenceCount;
        public int expectedBoneInfluenceCount;
        public bool boneInfluenceCountMatchesMode;
        public string supportStatus = string.Empty;
        public string supportReason = string.Empty;
        public bool handedOffToSkinningDescriptor;
        public bool exactRuntimeSkinning;
        public bool linearFallbackToBoneWeights;
        public bool hasSdefParameters;
        public string sdefParameterStatus = string.Empty;
    }

    public static class MmdSkinningDescriptorBuilder
    {
        private static readonly HashSet<string> KnownSkinningModes = new(StringComparer.Ordinal)
        {
            "bdef1",
            "bdef2",
            "bdef4",
            "sdef",
            "qdef",
            "unknown"
        };

        public const string CurrentRuntimeSupportedStatus = "current-runtime-supported";
        public const string LinearFallbackStatus = "linear-fallback";
        public const string NotSupportedStatus = "not-supported";
        public const string BoneWeightHandoffReason = "bone-index-weight-handoff";
        public const string SdefExactDeformationFutureReason = "sdef-exact-deformation-future";
        public const string QdefDualQuaternionFutureReason = "qdef-dual-quaternion-future";
        public const string UnknownSkinningModeReason = "unknown-skinning-mode";

        public static IReadOnlyList<MmdSkinningDescriptor> Build(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            var descriptors = new List<MmdSkinningDescriptor>(model.vertices.Count);
            foreach (MmdVertexDefinition vertex in model.vertices.OrderBy(vertex => vertex.index))
            {
                string skinningMode = NormalizeSkinningMode(vertex.skinningMode);
                descriptors.Add(new MmdSkinningDescriptor
                {
                    vertexIndex = vertex.index,
                    skinningMode = skinningMode,
                    normalizedFamily = GetNormalizedFamily(skinningMode),
                    boneIndices = vertex.boneIndices.ToArray(),
                    boneWeights = vertex.boneWeights.ToArray(),
                    supportStatus = GetSupportStatus(skinningMode),
                    supportReason = GetSupportReason(skinningMode),
                    exactRuntimeSkinning = IsCurrentRuntimeSupported(skinningMode),
                    linearFallbackToBoneWeights = IsLinearFallback(skinningMode)
                });
            }

            return descriptors;
        }

        public static IReadOnlyList<MmdSkinningModeInventoryDescriptor> BuildModeInventory(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            var inventory = new List<MmdSkinningModeInventoryDescriptor>(model.vertices.Count);
            foreach (MmdVertexDefinition vertex in model.vertices.OrderBy(vertex => vertex.index))
            {
                string skinningMode = NormalizeSkinningMode(vertex.skinningMode);
                int boneInfluenceCount = vertex.boneIndices.Length;
                int expectedBoneInfluenceCount = GetExpectedBoneInfluenceCount(skinningMode);
                inventory.Add(new MmdSkinningModeInventoryDescriptor
                {
                    vertexIndex = vertex.index,
                    skinningMode = skinningMode,
                    normalizedFamily = GetNormalizedFamily(skinningMode),
                    boneInfluenceCount = boneInfluenceCount,
                    expectedBoneInfluenceCount = expectedBoneInfluenceCount,
                    boneInfluenceCountMatchesMode = expectedBoneInfluenceCount >= 0 && boneInfluenceCount == expectedBoneInfluenceCount,
                    supportStatus = GetSupportStatus(skinningMode),
                    supportReason = GetSupportReason(skinningMode),
                    handedOffToSkinningDescriptor = IsCurrentRuntimeSupported(skinningMode) || IsLinearFallback(skinningMode),
                    exactRuntimeSkinning = IsCurrentRuntimeSupported(skinningMode),
                    linearFallbackToBoneWeights = IsLinearFallback(skinningMode),
                    hasSdefParameters = vertex.hasSdefParameters,
                    sdefParameterStatus = GetSdefParameterStatus(skinningMode, vertex.hasSdefParameters)
                });
            }

            return inventory;
        }

        private static string NormalizeSkinningMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            string trimmed = value.Trim().ToLowerInvariant();
            return KnownSkinningModes.Contains(trimmed) ? trimmed : "unknown";
        }

        private static int GetExpectedBoneInfluenceCount(string skinningMode)
        {
            return skinningMode switch
            {
                "bdef1" => 1,
                "bdef2" => 2,
                "bdef4" => 4,
                "sdef" => 2,
                "qdef" => 4,
                _ => -1
            };
        }

        private static string GetNormalizedFamily(string skinningMode)
        {
            return skinningMode switch
            {
                "bdef1" => "bdef",
                "bdef2" => "bdef",
                "bdef4" => "bdef",
                "sdef" => "sdef",
                "qdef" => "qdef",
                _ => "unknown"
            };
        }

        private static string GetSupportStatus(string skinningMode)
        {
            if (IsCurrentRuntimeSupported(skinningMode))
            {
                return CurrentRuntimeSupportedStatus;
            }

            return skinningMode switch
            {
                "sdef" => LinearFallbackStatus,
                "qdef" => LinearFallbackStatus,
                _ => NotSupportedStatus
            };
        }

        private static string GetSupportReason(string skinningMode)
        {
            if (IsCurrentRuntimeSupported(skinningMode))
            {
                return BoneWeightHandoffReason;
            }

            return skinningMode switch
            {
                "sdef" => SdefExactDeformationFutureReason,
                "qdef" => QdefDualQuaternionFutureReason,
                _ => UnknownSkinningModeReason
            };
        }

        private static bool IsCurrentRuntimeSupported(string skinningMode)
        {
            return skinningMode is "bdef1" or "bdef2" or "bdef4";
        }

        private static bool IsLinearFallback(string skinningMode)
        {
            return skinningMode is "sdef" or "qdef";
        }

        private static string GetSdefParameterStatus(string skinningMode, bool hasSdefParameters)
        {
            if (skinningMode == "sdef")
            {
                return hasSdefParameters ? "available" : "missing";
            }

            return hasSdefParameters ? "unexpected" : "not-applicable";
        }
    }
}
