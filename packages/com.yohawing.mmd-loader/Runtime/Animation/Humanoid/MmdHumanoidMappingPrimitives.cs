#nullable enable

using System;
using UnityEngine;

namespace Mmd
{
    public static class MmdHumanoidMappingReadiness
    {
        public const string NotEvaluated = "NotEvaluated";
        public const string Ready = "Ready";
        public const string MissingRequired = "MissingRequired";
        public const string Ambiguous = "Ambiguous";
        public const string NoBones = "NoBones";
        public const string HierarchyNotReady = "HierarchyNotReady";
    }

    [Serializable]
    public sealed class MmdSerializableBoneMappingEntry
    {
        [SerializeField] private HumanBodyBones humanBone;
        [SerializeField] private string mmdBoneName = string.Empty;
        [SerializeField] private int mmdBoneIndex;
        [SerializeField] private bool required;
        [SerializeField] private string category = string.Empty;

        public MmdSerializableBoneMappingEntry() { }

        public MmdSerializableBoneMappingEntry(
            HumanBodyBones humanBone,
            string mmdBoneName,
            int mmdBoneIndex,
            bool required,
            string category)
        {
            this.humanBone = humanBone;
            this.mmdBoneName = mmdBoneName ?? string.Empty;
            this.mmdBoneIndex = mmdBoneIndex;
            this.required = required;
            this.category = category ?? string.Empty;
        }

        public HumanBodyBones HumanBone => humanBone;

        public string MmdBoneName => mmdBoneName;

        public int MmdBoneIndex => mmdBoneIndex;

        public bool Required => required;

        public string Category => category;
    }
}
