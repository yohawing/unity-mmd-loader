#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Mmd.Parser;

namespace Mmd
{
    public readonly struct MmdHumanoidBoneMappingReport
    {
        public MmdHumanoidBoneMappingReport(
            string readiness,
            int totalBoneCount,
            int requiredMappedBoneCount,
            int optionalMappedBoneCount,
            int missingRequiredBoneCount,
            int ambiguousMappingCount,
            int ignoredHelperBoneCount,
            string[] diagnostics,
            MmdSerializableBoneMappingEntry[]? mappingEntries = null)
        {
            Readiness = readiness ?? MmdHumanoidSetupAsset.NotEvaluatedReadiness;
            TotalBoneCount = Math.Max(0, totalBoneCount);
            RequiredMappedBoneCount = Math.Max(0, requiredMappedBoneCount);
            OptionalMappedBoneCount = Math.Max(0, optionalMappedBoneCount);
            MissingRequiredBoneCount = Math.Max(0, missingRequiredBoneCount);
            AmbiguousMappingCount = Math.Max(0, ambiguousMappingCount);
            IgnoredHelperBoneCount = Math.Max(0, ignoredHelperBoneCount);
            Diagnostics = diagnostics != null ? (string[])diagnostics.Clone() : Array.Empty<string>();
            MappingEntries = mappingEntries ?? Array.Empty<MmdSerializableBoneMappingEntry>();
        }

        public string Readiness { get; }

        public int TotalBoneCount { get; }

        public int RequiredMappedBoneCount { get; }

        public int OptionalMappedBoneCount { get; }

        public int MissingRequiredBoneCount { get; }

        public int AmbiguousMappingCount { get; }

        public int IgnoredHelperBoneCount { get; }

        public string[] Diagnostics { get; }

        public MmdSerializableBoneMappingEntry[] MappingEntries { get; }
    }

    public readonly struct MmdHumanoidBoneMappingMatch
    {
        public MmdHumanoidBoneMappingMatch(
            HumanBodyBones humanBone,
            string mmdBoneName,
            int mmdBoneIndex,
            bool required)
        {
            HumanBone = humanBone;
            MmdBoneName = mmdBoneName ?? string.Empty;
            MmdBoneIndex = mmdBoneIndex;
            Required = required;
        }

        public HumanBodyBones HumanBone { get; }

        public string MmdBoneName { get; }

        public int MmdBoneIndex { get; }

        public bool Required { get; }
    }

    public readonly struct MmdHumanoidRequiredBoneInfo
    {
        public MmdHumanoidRequiredBoneInfo(HumanBodyBones humanBone, string mmdBoneName)
        {
            HumanBone = humanBone;
            MmdBoneName = mmdBoneName ?? string.Empty;
        }

        public HumanBodyBones HumanBone { get; }

        public string MmdBoneName { get; }
    }

    [Serializable]
    public sealed class MmdHumanoidBoneMappingOverride
    {
        [SerializeField] private string mmdBoneName = string.Empty;
        [SerializeField] private HumanBodyBones humanBone = HumanBodyBones.LastBone;

        public MmdHumanoidBoneMappingOverride() { }

        public MmdHumanoidBoneMappingOverride(string mmdBoneName, HumanBodyBones humanBone)
        {
            this.mmdBoneName = mmdBoneName ?? string.Empty;
            this.humanBone = humanBone;
        }

        public string MmdBoneName => mmdBoneName;

        public HumanBodyBones HumanBone => humanBone;
    }

}
