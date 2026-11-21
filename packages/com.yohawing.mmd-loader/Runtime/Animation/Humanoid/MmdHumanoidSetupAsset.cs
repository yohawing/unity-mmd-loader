#nullable enable

#pragma warning disable CS0618

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmd
{
    // Retained as the serialized field type of legacy MmdHumanoidSetupAsset files (setupPreset).
    [Obsolete("MmdHumanoidSetupPreset is retained only to read existing MmdHumanoidSetupAsset files. Configure Humanoid mapping on the PMX importer instead.")]
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
        [SerializeField, HideInInspector]
        private MmdSerializableBoneMappingEntry[] mappingEntries =
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
    }
}
