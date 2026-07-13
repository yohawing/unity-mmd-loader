#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Mmd.Parser;

namespace Mmd
{
    public enum MmdHumanoidSetupPreset
    {
        MmdSemiStandard = 0,
        MmdStandard = 1,
        Custom = 2
    }

    public sealed class MmdHumanoidSetupAsset : ScriptableObject
    {
        public const string NotEvaluatedReadiness = "NotEvaluated";
        public const string ReadyReadiness = "Ready";
        public const string MissingRequiredReadiness = "MissingRequired";
        public const string AmbiguousReadiness = "Ambiguous";
        public const string NoBonesReadiness = "NoBones";
        public const string EvaluationFailedReadiness = "EvaluationFailed";
        public const string HierarchyNotReadyReadiness = "HierarchyNotReady";
        public const string NoNativePlaybackImpact = "None";
        public const string ImportedHierarchyInputSource = "ImportedHierarchy";
        public const string NoMappingInputSource = "None";

        [SerializeField] private MmdPmxAsset? pmxAsset;
        [SerializeField] private MmdHumanoidSetupPreset setupPreset = MmdHumanoidSetupPreset.MmdSemiStandard;
        [SerializeField] private int pmxBoneCount;
        [SerializeField] private string mappingReadiness = NotEvaluatedReadiness;
        [SerializeField] private string mappingInputSource = NoMappingInputSource;
        [SerializeField] private int requiredMappedBoneCount;
        [SerializeField] private int optionalMappedBoneCount;
        [SerializeField] private int missingRequiredBoneCount;
        [SerializeField] private int ambiguousMappingCount;
        [SerializeField] private int ignoredHelperBoneCount;
        [SerializeField] private string[] mappingDiagnostics = Array.Empty<string>();
        [SerializeField] private string nativePlaybackImpact = NoNativePlaybackImpact;
        [SerializeField] private MmdSerializableBoneMappingEntry[] mappingEntries =
            Array.Empty<MmdSerializableBoneMappingEntry>();

        public MmdPmxAsset? PmxAsset => pmxAsset;

        public MmdHumanoidSetupPreset SetupPreset => setupPreset;

        public int PmxBoneCount => pmxBoneCount;

        public string MappingReadiness => mappingReadiness;

        public string MappingInputSource => mappingInputSource;

        public int RequiredMappedBoneCount => requiredMappedBoneCount;

        public int OptionalMappedBoneCount => optionalMappedBoneCount;

        public int MissingRequiredBoneCount => missingRequiredBoneCount;

        public int AmbiguousMappingCount => ambiguousMappingCount;

        public int IgnoredHelperBoneCount => ignoredHelperBoneCount;

        public IReadOnlyList<string> MappingDiagnostics => mappingDiagnostics;

        public string NativePlaybackImpact => nativePlaybackImpact;

        public IReadOnlyList<MmdSerializableBoneMappingEntry> MappingEntries => Array.AsReadOnly(mappingEntries);

        public void Initialize(
            MmdPmxAsset sourceAsset,
            MmdHumanoidSetupPreset preset = MmdHumanoidSetupPreset.MmdSemiStandard)
        {
            if (sourceAsset == null)
            {
                throw new ArgumentNullException(nameof(sourceAsset));
            }

            pmxAsset = sourceAsset;
            setupPreset = preset;
            mappingInputSource = NoMappingInputSource;
            pmxBoneCount = Math.Max(0, sourceAsset.BoneCount);
            nativePlaybackImpact = NoNativePlaybackImpact;

            try
            {
                GameObject? importedRoot = sourceAsset.ImportedRoot;
                if (importedRoot == null)
                {
                    ApplyHierarchyNotReadyReport("hierarchy-not-ready: ImportedRoot is null. Reimport the .pmx asset.");
                    return;
                }

                SkinnedMeshRenderer? smr = importedRoot.GetComponentInChildren<SkinnedMeshRenderer>(
                    includeInactive: true);

                if (sourceAsset.BoneCount > 0)
                {
                    if (smr == null)
                    {
                        ApplyHierarchyNotReadyReport("hierarchy-not-ready: No SkinnedMeshRenderer found under ImportedRoot.");
                        return;
                    }

                    if (smr.bones == null)
                    {
                        ApplyHierarchyNotReadyReport(
                            "hierarchy-not-ready: SkinnedMeshRenderer.bones is null under ImportedRoot.");
                        return;
                    }

                    if (smr.bones.Length != sourceAsset.BoneCount)
                    {
                        ApplyHierarchyNotReadyReport(
                            "hierarchy-not-ready: SkinnedMeshRenderer.bones length does not match PMX BoneCount.");
                        return;
                    }

                    for (int i = 0; i < smr.bones.Length; i++)
                    {
                        if (smr.bones[i] == null)
                        {
                            ApplyHierarchyNotReadyReport(
                                "hierarchy-not-ready: SkinnedMeshRenderer.bones contains null entry.");
                            return;
                        }
                    }

                    var bones = new string[smr.bones.Length];
                    for (int i = 0; i < smr.bones.Length; i++)
                    {
                        bones[i] = smr.bones[i].name;
                    }

                    pmxBoneCount = bones.Length;
                    mappingInputSource = ImportedHierarchyInputSource;
                    ApplyMappingReport(
                        MmdHumanoidBoneMappingEvaluator.EvaluateBoneNames(bones));
                    return;
                }

                mappingInputSource = ImportedHierarchyInputSource;
                pmxBoneCount = 0;
                ApplyMappingReport(
                    MmdHumanoidBoneMappingEvaluator.EvaluateBoneNames(Array.Empty<string>()));
            }
            catch (Exception ex)
            {
                mappingReadiness = EvaluationFailedReadiness;
                requiredMappedBoneCount = 0;
                optionalMappedBoneCount = 0;
                missingRequiredBoneCount = 0;
                ambiguousMappingCount = 0;
                ignoredHelperBoneCount = 0;
                mappingInputSource = NoMappingInputSource;
                mappingDiagnostics = new[] { "evaluation-failed: " + ex.GetType().Name + ": " + ex.Message };
            }
        }

        private void ApplyHierarchyNotReadyReport(string diagnostic)
        {
            mappingReadiness = HierarchyNotReadyReadiness;
            pmxBoneCount = 0;
            requiredMappedBoneCount = 0;
            optionalMappedBoneCount = 0;
            missingRequiredBoneCount = 0;
            ambiguousMappingCount = 0;
            ignoredHelperBoneCount = 0;
            mappingInputSource = NoMappingInputSource;
            mappingDiagnostics = new[] { diagnostic };
            mappingEntries = Array.Empty<MmdSerializableBoneMappingEntry>();
        }

        private void ApplyMappingReport(MmdHumanoidBoneMappingReport report)
        {
            mappingReadiness = report.Readiness;
            requiredMappedBoneCount = report.RequiredMappedBoneCount;
            optionalMappedBoneCount = report.OptionalMappedBoneCount;
            missingRequiredBoneCount = report.MissingRequiredBoneCount;
            ambiguousMappingCount = report.AmbiguousMappingCount;
            ignoredHelperBoneCount = report.IgnoredHelperBoneCount;
            mappingDiagnostics = report.Diagnostics;
            mappingEntries = report.MappingEntries != null
                ? (MmdSerializableBoneMappingEntry[])report.MappingEntries.Clone()
                : Array.Empty<MmdSerializableBoneMappingEntry>();
        }
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
