#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmd
{
    [Serializable]
    public sealed class MmdHumanoidBoneMappingDiagnosticSummary
    {
        [SerializeField] private string readiness = MmdHumanoidSetupAsset.NotEvaluatedReadiness;
        [SerializeField] private MmdHumanoidBoneMappingDiagnosticEntry[] mappedEntries =
            Array.Empty<MmdHumanoidBoneMappingDiagnosticEntry>();
        [SerializeField] private MmdHumanoidMissingRequiredBone[] missingRequiredBones =
            Array.Empty<MmdHumanoidMissingRequiredBone>();
        [SerializeField] private string[] conflictDiagnostics = Array.Empty<string>();

        public MmdHumanoidBoneMappingDiagnosticSummary() { }

        public MmdHumanoidBoneMappingDiagnosticSummary(
            string readiness,
            MmdHumanoidBoneMappingDiagnosticEntry[]? mappedEntries,
            MmdHumanoidMissingRequiredBone[]? missingRequiredBones,
            string[]? conflictDiagnostics)
        {
            this.readiness = string.IsNullOrWhiteSpace(readiness)
                ? MmdHumanoidSetupAsset.NotEvaluatedReadiness
                : readiness;
            this.mappedEntries = mappedEntries != null
                ? (MmdHumanoidBoneMappingDiagnosticEntry[])mappedEntries.Clone()
                : Array.Empty<MmdHumanoidBoneMappingDiagnosticEntry>();
            this.missingRequiredBones = missingRequiredBones != null
                ? (MmdHumanoidMissingRequiredBone[])missingRequiredBones.Clone()
                : Array.Empty<MmdHumanoidMissingRequiredBone>();
            this.conflictDiagnostics = conflictDiagnostics != null
                ? (string[])conflictDiagnostics.Clone()
                : Array.Empty<string>();
        }

        public string Readiness => readiness;

        public IReadOnlyList<MmdHumanoidBoneMappingDiagnosticEntry> MappedEntries => mappedEntries;

        public IReadOnlyList<MmdHumanoidMissingRequiredBone> MissingRequiredBones => missingRequiredBones;

        public IReadOnlyList<string> ConflictDiagnostics => conflictDiagnostics;

        public static MmdHumanoidBoneMappingDiagnosticSummary Empty { get; } =
            new MmdHumanoidBoneMappingDiagnosticSummary(
                MmdHumanoidSetupAsset.NotEvaluatedReadiness,
                Array.Empty<MmdHumanoidBoneMappingDiagnosticEntry>(),
                Array.Empty<MmdHumanoidMissingRequiredBone>(),
                Array.Empty<string>());
    }

    [Serializable]
    public sealed class MmdHumanoidBoneMappingDiagnosticEntry
    {
        [SerializeField] private HumanBodyBones humanBone;
        [SerializeField] private string mmdBoneName = string.Empty;
        [SerializeField] private int mmdBoneIndex;
        [SerializeField] private bool required;
        [SerializeField] private string source = MmdHumanoidBoneMappingDiagnosticsBuilder.AutomaticSource;

        public MmdHumanoidBoneMappingDiagnosticEntry() { }

        public MmdHumanoidBoneMappingDiagnosticEntry(
            HumanBodyBones humanBone,
            string mmdBoneName,
            int mmdBoneIndex,
            bool required,
            string source)
        {
            this.humanBone = humanBone;
            this.mmdBoneName = mmdBoneName ?? string.Empty;
            this.mmdBoneIndex = mmdBoneIndex;
            this.required = required;
            this.source = string.IsNullOrWhiteSpace(source)
                ? MmdHumanoidBoneMappingDiagnosticsBuilder.AutomaticSource
                : source;
        }

        public HumanBodyBones HumanBone => humanBone;

        public string MmdBoneName => mmdBoneName;

        public int MmdBoneIndex => mmdBoneIndex;

        public bool Required => required;

        public string Source => source;
    }

    [Serializable]
    public sealed class MmdHumanoidMissingRequiredBone
    {
        [SerializeField] private HumanBodyBones humanBone;
        [SerializeField] private string expectedMmdBoneName = string.Empty;

        public MmdHumanoidMissingRequiredBone() { }

        public MmdHumanoidMissingRequiredBone(HumanBodyBones humanBone, string expectedMmdBoneName)
        {
            this.humanBone = humanBone;
            this.expectedMmdBoneName = expectedMmdBoneName ?? string.Empty;
        }

        public HumanBodyBones HumanBone => humanBone;

        public string ExpectedMmdBoneName => expectedMmdBoneName;
    }

    internal static class MmdHumanoidBoneMappingDiagnosticsBuilder
    {
        public const string AutomaticSource = "Automatic";
        public const string ManualOverrideSource = "Manual Override";

        public static MmdHumanoidBoneMappingDiagnosticSummary Build(
            MmdHumanoidProxyRigResult? proxyRig,
            IReadOnlyList<MmdHumanoidBoneMappingOverride>? mappingOverrides)
        {
            if (proxyRig == null)
            {
                return MmdHumanoidBoneMappingDiagnosticSummary.Empty;
            }

            Dictionary<HumanBodyBones, string> manualOverrideByHumanBone =
                BuildManualOverrideLookup(mappingOverrides);
            var mappedEntries = new List<MmdHumanoidBoneMappingDiagnosticEntry>();
            var mappedRequired = new HashSet<HumanBodyBones>();

            foreach (MmdHumanoidBoneMappingMatch match in proxyRig.Matches)
            {
                string source = IsManualOverrideMatch(match, manualOverrideByHumanBone)
                    ? ManualOverrideSource
                    : AutomaticSource;
                mappedEntries.Add(new MmdHumanoidBoneMappingDiagnosticEntry(
                    match.HumanBone,
                    match.MmdBoneName,
                    match.MmdBoneIndex,
                    MmdHumanoidBoneMappingEvaluator.IsRequiredHumanBone(match.HumanBone),
                    source));

                if (MmdHumanoidBoneMappingEvaluator.IsRequiredHumanBone(match.HumanBone))
                {
                    mappedRequired.Add(match.HumanBone);
                }
            }

            var missingRequired = new List<MmdHumanoidMissingRequiredBone>();
            foreach (MmdHumanoidRequiredBoneInfo required in MmdHumanoidBoneMappingEvaluator.GetRequiredHumanBones())
            {
                if (!mappedRequired.Contains(required.HumanBone))
                {
                    missingRequired.Add(new MmdHumanoidMissingRequiredBone(
                        required.HumanBone,
                        required.MmdBoneName));
                }
            }

            return new MmdHumanoidBoneMappingDiagnosticSummary(
                proxyRig.Readiness,
                mappedEntries.ToArray(),
                missingRequired.ToArray(),
                FilterConflictDiagnostics(proxyRig.Diagnostics));
        }

        private static Dictionary<HumanBodyBones, string> BuildManualOverrideLookup(
            IReadOnlyList<MmdHumanoidBoneMappingOverride>? mappingOverrides)
        {
            var lookup = new Dictionary<HumanBodyBones, string>();
            if (mappingOverrides == null)
            {
                return lookup;
            }

            for (int i = 0; i < mappingOverrides.Count; i++)
            {
                MmdHumanoidBoneMappingOverride? mappingOverride = mappingOverrides[i];
                if (mappingOverride == null)
                {
                    continue;
                }

                HumanBodyBones humanBone = mappingOverride.HumanBone;
                if (humanBone == HumanBodyBones.LastBone || !Enum.IsDefined(typeof(HumanBodyBones), humanBone))
                {
                    continue;
                }

                string mmdBoneName = mappingOverride.MmdBoneName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(mmdBoneName))
                {
                    continue;
                }

                lookup[humanBone] = mmdBoneName.Trim();
            }

            return lookup;
        }

        private static bool IsManualOverrideMatch(
            MmdHumanoidBoneMappingMatch match,
            Dictionary<HumanBodyBones, string> manualOverrideByHumanBone)
        {
            return manualOverrideByHumanBone.TryGetValue(match.HumanBone, out string overrideBoneName)
                   && string.Equals(
                       (match.MmdBoneName ?? string.Empty).Trim(),
                       overrideBoneName,
                       StringComparison.Ordinal);
        }

        private static string[] FilterConflictDiagnostics(IReadOnlyList<string> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
            {
                return Array.Empty<string>();
            }

            var filtered = new List<string>();
            foreach (string diagnostic in diagnostics)
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                {
                    continue;
                }

                if (diagnostic.StartsWith("manual-override:", StringComparison.Ordinal)
                    || diagnostic.StartsWith("manual-overrides:", StringComparison.Ordinal)
                    || diagnostic.IndexOf("skipped duplicate", StringComparison.Ordinal) >= 0
                    || diagnostic.StartsWith("ambiguous:", StringComparison.Ordinal))
                {
                    filtered.Add(diagnostic);
                }
            }

            return filtered.ToArray();
        }
    }
}
