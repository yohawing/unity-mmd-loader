#nullable enable

#pragma warning disable CS0618

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmd
{
    [Obsolete("MmdHumanoidSetupPreset is retained only for source compatibility. Configure Humanoid mapping on the PMX importer instead.")]
    public enum MmdHumanoidSetupPreset
    {
        MmdSemiStandard = 0,
        MmdStandard = 1,
        Custom = 2
    }

    /// <summary>
    /// Compatibility container for setup assets created by package versions before 0.2.0.
    /// New Humanoid workflows use the Avatar and retarget mapping persisted by PMX import.
    /// </summary>
    [Obsolete("MmdHumanoidSetupAsset is retained only to load existing assets and compile existing integrations. Reimport the PMX as Humanoid and use its imported Avatar and persisted retarget mapping.")]
    public sealed class MmdHumanoidSetupAsset : ScriptableObject
    {
        public const string NotEvaluatedReadiness = MmdHumanoidMappingReadiness.NotEvaluated;
        public const string ReadyReadiness = MmdHumanoidMappingReadiness.Ready;
        public const string MissingRequiredReadiness = MmdHumanoidMappingReadiness.MissingRequired;
        public const string AmbiguousReadiness = MmdHumanoidMappingReadiness.Ambiguous;
        public const string NoBonesReadiness = MmdHumanoidMappingReadiness.NoBones;
        public const string EvaluationFailedReadiness = "EvaluationFailed";
        public const string HierarchyNotReadyReadiness = MmdHumanoidMappingReadiness.HierarchyNotReady;
        public const string NoNativePlaybackImpact = "None";
        public const string ImportedHierarchyInputSource = "ImportedHierarchy";
        public const string NoMappingInputSource = "None";

        // Field names and types are an upgrade contract for existing serialized .asset files.
        [SerializeField, HideInInspector] private MmdPmxAsset? pmxAsset;
        [SerializeField, HideInInspector] private MmdHumanoidSetupPreset setupPreset = MmdHumanoidSetupPreset.MmdSemiStandard;
        [SerializeField, HideInInspector] private int pmxBoneCount;
        [SerializeField, HideInInspector] private string mappingReadiness = NotEvaluatedReadiness;
        [SerializeField, HideInInspector] private string mappingInputSource = NoMappingInputSource;
        [SerializeField, HideInInspector] private int requiredMappedBoneCount;
        [SerializeField, HideInInspector] private int optionalMappedBoneCount;
        [SerializeField, HideInInspector] private int missingRequiredBoneCount;
        [SerializeField, HideInInspector] private int ambiguousMappingCount;
        [SerializeField, HideInInspector] private int ignoredHelperBoneCount;
        [SerializeField, HideInInspector] private string[] mappingDiagnostics = Array.Empty<string>();
        [SerializeField, HideInInspector] private string nativePlaybackImpact = NoNativePlaybackImpact;
        [SerializeField, HideInInspector] private MmdSerializableBoneMappingEntry[] mappingEntries =
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

        [Obsolete("Reimport the PMX with Animation Type set to Humanoid instead. This method only supports legacy integrations.")]
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

                SkinnedMeshRenderer? renderer = importedRoot.GetComponentInChildren<SkinnedMeshRenderer>(
                    includeInactive: true);
                if (sourceAsset.BoneCount > 0)
                {
                    if (renderer == null || renderer.bones == null || renderer.bones.Length != sourceAsset.BoneCount)
                    {
                        ApplyHierarchyNotReadyReport(
                            "hierarchy-not-ready: Imported SkinnedMeshRenderer bones do not match PMX BoneCount.");
                        return;
                    }

                    var boneNames = new string[renderer.bones.Length];
                    for (int i = 0; i < renderer.bones.Length; i++)
                    {
                        if (renderer.bones[i] == null)
                        {
                            ApplyHierarchyNotReadyReport(
                                "hierarchy-not-ready: SkinnedMeshRenderer.bones contains null entry.");
                            return;
                        }

                        boneNames[i] = renderer.bones[i].name;
                    }

                    pmxBoneCount = boneNames.Length;
                    mappingInputSource = ImportedHierarchyInputSource;
                    ApplyMappingReport(MmdHumanoidBoneMappingEvaluator.EvaluateBoneNames(boneNames));
                    return;
                }

                mappingInputSource = ImportedHierarchyInputSource;
                pmxBoneCount = 0;
                ApplyMappingReport(MmdHumanoidBoneMappingEvaluator.EvaluateBoneNames(Array.Empty<string>()));
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
                mappingEntries = Array.Empty<MmdSerializableBoneMappingEntry>();
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
}
