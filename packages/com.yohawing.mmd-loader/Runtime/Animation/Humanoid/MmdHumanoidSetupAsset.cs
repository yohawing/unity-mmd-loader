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
                if (sourceAsset.ImportedRoot == null)
                {
                    ApplyHierarchyNotReadyReport("hierarchy-not-ready: ImportedRoot is null. Reimport the .pmx asset.");
                    return;
                }

                SkinnedMeshRenderer? smr = sourceAsset.ImportedRoot.GetComponentInChildren<SkinnedMeshRenderer>(
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

    public static class MmdHumanoidBoneMappingEvaluator
    {
        private static readonly BoneRule[] RequiredRules =
        {
            new(HumanBodyBones.Hips, "下半身", "Hips", "Hip", "Pelvis", "Lower Body", "LowerBody"),
            new(HumanBodyBones.Spine, "上半身", "Spine", "Upper Body", "UpperBody"),
            new(HumanBodyBones.Neck, "首", "Neck"),
            new(HumanBodyBones.Head, "頭", "Head"),
            new(HumanBodyBones.LeftUpperLeg, "左足", "Left Leg", "Left Upper Leg", "LeftUpperLeg", "Left Thigh", "leg_L"),
            new(HumanBodyBones.LeftLowerLeg, "左ひざ", "左膝", "Left Knee", "Left Lower Leg", "LeftLowerLeg", "knee_L"),
            new(HumanBodyBones.LeftFoot, "左足首", "Left Ankle", "Left Foot", "LeftFoot", "ankle_L", "foot_L"),
            new(HumanBodyBones.RightUpperLeg, "右足", "Right Leg", "Right Upper Leg", "RightUpperLeg", "Right Thigh", "leg_R"),
            new(HumanBodyBones.RightLowerLeg, "右ひざ", "右膝", "Right Knee", "Right Lower Leg", "RightLowerLeg", "knee_R"),
            new(HumanBodyBones.RightFoot, "右足首", "Right Ankle", "Right Foot", "RightFoot", "ankle_R", "foot_R"),
            new(HumanBodyBones.LeftUpperArm, "左腕", "Left Arm", "Left Upper Arm", "LeftUpperArm", "arm_L"),
            new(HumanBodyBones.LeftLowerArm, "左ひじ", "左肘", "Left Elbow", "Left Lower Arm", "LeftLowerArm", "elbow_L"),
            new(HumanBodyBones.LeftHand, "左手首", "Left Wrist", "Left Hand", "LeftHand", "wrist_L", "hand_L"),
            new(HumanBodyBones.RightUpperArm, "右腕", "Right Arm", "Right Upper Arm", "RightUpperArm", "arm_R"),
            new(HumanBodyBones.RightLowerArm, "右ひじ", "右肘", "Right Elbow", "Right Lower Arm", "RightLowerArm", "elbow_R"),
            new(HumanBodyBones.RightHand, "右手首", "Right Wrist", "Right Hand", "RightHand", "wrist_R", "hand_R"),
        };

        private static readonly BoneRule[] OptionalRules =
        {
            new(HumanBodyBones.Chest, "上半身2", "胸", "Chest", "Upper Chest", "UpperChest", "Upper Body 2", "UpperBody2"),
            new(HumanBodyBones.LeftShoulder, "左肩", "Left Shoulder", "LeftShoulder", "shoulder_L"),
            new(HumanBodyBones.RightShoulder, "右肩", "Right Shoulder", "RightShoulder", "shoulder_R"),
            new(HumanBodyBones.LeftToes, "左つま先", "左爪先", "Left Toe", "Left Toes", "LeftToes", "toe_L", "toes_L"),
            new(HumanBodyBones.RightToes, "右つま先", "右爪先", "Right Toe", "Right Toes", "RightToes", "toe_R", "toes_R"),
            new(HumanBodyBones.LeftEye, "左目", "Left Eye", "LeftEye", "eye_L"),
            new(HumanBodyBones.RightEye, "右目", "Right Eye", "RightEye", "eye_R"),
        };

        private static readonly BoneRule[] FingerRules =
        {
            // ── Left thumb ──
            new(HumanBodyBones.LeftThumbProximal, "左親指１"),
            new(HumanBodyBones.LeftThumbIntermediate, "左親指２"),
            new(HumanBodyBones.LeftThumbDistal, "左親指先"),
            // ── Right thumb ──
            new(HumanBodyBones.RightThumbProximal, "右親指１"),
            new(HumanBodyBones.RightThumbIntermediate, "右親指２"),
            new(HumanBodyBones.RightThumbDistal, "右親指先"),
            // ── Left index (人指 variant) ──
            new(HumanBodyBones.LeftIndexProximal, "左人指１"),
            new(HumanBodyBones.LeftIndexIntermediate, "左人指２"),
            new(HumanBodyBones.LeftIndexDistal, "左人指３"),
            new(HumanBodyBones.LeftIndexDistal, "左人指先"),
            // ── Left index (人差指 variant) ──
            new(HumanBodyBones.LeftIndexProximal, "左人差指１"),
            new(HumanBodyBones.LeftIndexIntermediate, "左人差指２"),
            new(HumanBodyBones.LeftIndexDistal, "左人差指３"),
            new(HumanBodyBones.LeftIndexDistal, "左人差指先"),
            // ── Right index (人指 variant) ──
            new(HumanBodyBones.RightIndexProximal, "右人指１"),
            new(HumanBodyBones.RightIndexIntermediate, "右人指２"),
            new(HumanBodyBones.RightIndexDistal, "右人指３"),
            new(HumanBodyBones.RightIndexDistal, "右人指先"),
            // ── Right index (人差指 variant) ──
            new(HumanBodyBones.RightIndexProximal, "右人差指１"),
            new(HumanBodyBones.RightIndexIntermediate, "右人差指２"),
            new(HumanBodyBones.RightIndexDistal, "右人差指３"),
            new(HumanBodyBones.RightIndexDistal, "右人差指先"),
            // ── Left middle ──
            new(HumanBodyBones.LeftMiddleProximal, "左中指１"),
            new(HumanBodyBones.LeftMiddleIntermediate, "左中指２"),
            new(HumanBodyBones.LeftMiddleDistal, "左中指３"),
            new(HumanBodyBones.LeftMiddleDistal, "左中指先"),
            // ── Right middle ──
            new(HumanBodyBones.RightMiddleProximal, "右中指１"),
            new(HumanBodyBones.RightMiddleIntermediate, "右中指２"),
            new(HumanBodyBones.RightMiddleDistal, "右中指３"),
            new(HumanBodyBones.RightMiddleDistal, "右中指先"),
            // ── Left ring ──
            new(HumanBodyBones.LeftRingProximal, "左薬指１"),
            new(HumanBodyBones.LeftRingIntermediate, "左薬指２"),
            new(HumanBodyBones.LeftRingDistal, "左薬指３"),
            new(HumanBodyBones.LeftRingDistal, "左薬指先"),
            // ── Right ring ──
            new(HumanBodyBones.RightRingProximal, "右薬指１"),
            new(HumanBodyBones.RightRingIntermediate, "右薬指２"),
            new(HumanBodyBones.RightRingDistal, "右薬指３"),
            new(HumanBodyBones.RightRingDistal, "右薬指先"),
            // ── Left little ──
            new(HumanBodyBones.LeftLittleProximal, "左小指１"),
            new(HumanBodyBones.LeftLittleIntermediate, "左小指２"),
            new(HumanBodyBones.LeftLittleDistal, "左小指３"),
            new(HumanBodyBones.LeftLittleDistal, "左小指先"),
            // ── Right little ──
            new(HumanBodyBones.RightLittleProximal, "右小指１"),
            new(HumanBodyBones.RightLittleIntermediate, "右小指２"),
            new(HumanBodyBones.RightLittleDistal, "右小指３"),
            new(HumanBodyBones.RightLittleDistal, "右小指先"),
        };

        private static readonly string[] IgnoredNameFragments =
        {
            "IK",
            "ＩＫ",
            "捩",
            "操作",
        };

        public static MmdHumanoidBoneMappingReport Evaluate(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            IReadOnlyList<MmdBoneDefinition> bones = model.bones != null
                ? model.bones
                : Array.Empty<MmdBoneDefinition>();

            var boneNames = new string[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                boneNames[i] = bones[i]?.name ?? string.Empty;
            }

            return EvaluateBoneNames(boneNames);
        }

        public static MmdHumanoidBoneMappingReport EvaluateBoneNames(IReadOnlyList<string> boneNames)
        {
            if (boneNames == null)
            {
                throw new ArgumentNullException(nameof(boneNames));
            }

            if (boneNames.Count == 0)
            {
                return new MmdHumanoidBoneMappingReport(
                    MmdHumanoidSetupAsset.NoBonesReadiness,
                    0,
                    0,
                    0,
                    RequiredRules.Length,
                    0,
                    0,
                    new[] { "no-bones: PMX model has no bones." });
            }

            var requiredMatches = CreateMatchBuckets(RequiredRules);
            var optionalMatches = CreateMatchBuckets(OptionalRules);
            var fingerMatches = CreateMatchBuckets(FingerRules);
            int ignoredCount = 0;

            for (int i = 0; i < boneNames.Count; i++)
            {
                string name = boneNames[i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (IsIgnoredHelperBone(name))
                {
                    ignoredCount++;
                    continue;
                }

                AddExactMatch(requiredMatches, RequiredRules, name, i, required: true);
                AddExactMatch(optionalMatches, OptionalRules, name, i, required: false);
                AddExactMatch(fingerMatches, FingerRules, name, i, required: false);
            }

            int requiredMapped = CountMapped(requiredMatches);
            int optionalMapped = CountMapped(optionalMatches);
            int fingerMapped = CountMappedEntries(fingerMatches);
            int missingRequired = RequiredRules.Length - requiredMapped;
            int requiredAmbiguous = CountAmbiguous(requiredMatches);
            int ambiguous = requiredAmbiguous + CountAmbiguous(optionalMatches) + CountAmbiguous(fingerMatches);
            string readiness = ClassifyReadiness(boneNames.Count, missingRequired, requiredAmbiguous);
            string[] diagnostics = BuildDiagnostics(
                readiness,
                requiredMatches,
                optionalMatches,
                fingerMatches,
                fingerMapped,
                missingRequired,
                ambiguous,
                ignoredCount);

            MmdSerializableBoneMappingEntry[] entries = CollectAllEntries(
                requiredMatches, optionalMatches, fingerMatches);

            return new MmdHumanoidBoneMappingReport(
                readiness,
                boneNames.Count,
                requiredMapped,
                optionalMapped,
                missingRequired,
                ambiguous,
                ignoredCount,
                diagnostics,
                entries);
        }

        public static bool TryMapBoneName(string boneName, out HumanBodyBones humanBone, out bool required)
        {
            humanBone = HumanBodyBones.LastBone;
            required = false;
            if (string.IsNullOrWhiteSpace(boneName) || IsIgnoredHelperBone(boneName))
            {
                return false;
            }

            string normalized = NormalizeBoneName(boneName);
            foreach (BoneRule rule in RequiredRules)
            {
                if (rule.Matches(normalized))
                {
                    humanBone = rule.HumanBone;
                    required = true;
                    return true;
                }
            }

            foreach (BoneRule rule in OptionalRules)
            {
                if (rule.Matches(normalized))
                {
                    humanBone = rule.HumanBone;
                    required = false;
                    return true;
                }
            }

            foreach (BoneRule rule in FingerRules)
            {
                if (rule.Matches(normalized))
                {
                    humanBone = rule.HumanBone;
                    required = false;
                    return true;
                }
            }

            return false;
        }

        public static bool IsRequiredHumanBone(HumanBodyBones humanBone)
        {
            foreach (BoneRule rule in RequiredRules)
            {
                if (rule.HumanBone == humanBone)
                {
                    return true;
                }
            }

            return false;
        }

        public static int RequiredHumanBoneCount => RequiredRules.Length;

        internal static MmdHumanoidRequiredBoneInfo[] GetRequiredHumanBones()
        {
            var required = new MmdHumanoidRequiredBoneInfo[RequiredRules.Length];
            for (int i = 0; i < RequiredRules.Length; i++)
            {
                BoneRule rule = RequiredRules[i];
                required[i] = new MmdHumanoidRequiredBoneInfo(rule.HumanBone, rule.MmdBoneName);
            }

            return required;
        }

        private static Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> CreateMatchBuckets(BoneRule[] rules)
        {
            var buckets = new Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>>();
            foreach (BoneRule rule in rules)
            {
                buckets[rule.HumanBone] = new List<MmdHumanoidBoneMappingMatch>();
            }

            return buckets;
        }

        private static void AddExactMatch(
            Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> buckets,
            BoneRule[] rules,
            MmdBoneDefinition bone,
            bool required)
        {
            AddExactMatch(buckets, rules, bone.name, bone.index, required);
        }

        private static void AddExactMatch(
            Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> buckets,
            BoneRule[] rules,
            string boneName,
            int boneIndex,
            bool required)
        {
            string normalizedName = NormalizeBoneName(boneName);
            foreach (BoneRule rule in rules)
            {
                if (!rule.Matches(normalizedName))
                {
                    continue;
                }

                buckets[rule.HumanBone].Add(new MmdHumanoidBoneMappingMatch(
                    rule.HumanBone,
                    boneName,
                    boneIndex,
                    required));
            }
        }

        private static int CountMapped(Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> buckets)
        {
            int count = 0;
            foreach (List<MmdHumanoidBoneMappingMatch> matches in buckets.Values)
            {
                if (matches.Count > 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountMappedEntries(Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> buckets)
        {
            int count = 0;
            foreach (List<MmdHumanoidBoneMappingMatch> matches in buckets.Values)
            {
                count += matches.Count;
            }

            return count;
        }

        private static int CountAmbiguous(Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> buckets)
        {
            int count = 0;
            foreach (List<MmdHumanoidBoneMappingMatch> matches in buckets.Values)
            {
                if (matches.Count > 1)
                {
                    count++;
                }
            }

            return count;
        }

        private static string ClassifyReadiness(int boneCount, int missingRequired, int requiredAmbiguous)
        {
            if (boneCount == 0)
            {
                return MmdHumanoidSetupAsset.NoBonesReadiness;
            }

            if (missingRequired > 0)
            {
                return MmdHumanoidSetupAsset.MissingRequiredReadiness;
            }

            if (requiredAmbiguous > 0)
            {
                return MmdHumanoidSetupAsset.AmbiguousReadiness;
            }

            return MmdHumanoidSetupAsset.ReadyReadiness;
        }

        private static string[] BuildDiagnostics(
            string readiness,
            Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> requiredMatches,
            Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> optionalMatches,
            Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> fingerMatches,
            int fingerMapped,
            int missingRequired,
            int ambiguous,
            int ignoredCount)
        {
            var diagnostics = new List<string>
            {
                "readiness: " + readiness,
                "counts: required=" + CountMapped(requiredMatches) + "/" + RequiredRules.Length +
                " optional=" + CountMapped(optionalMatches) + "/" + OptionalRules.Length +
                " finger=" + fingerMapped +
                " missingRequired=" + missingRequired +
                " ambiguous=" + ambiguous +
                " ignoredHelper=" + ignoredCount,
            };

            AppendMissingRequiredDiagnostics(diagnostics, requiredMatches);
            AppendAmbiguousDiagnostics(diagnostics, requiredMatches);
            AppendAmbiguousDiagnostics(diagnostics, optionalMatches);
            AppendAmbiguousDiagnostics(diagnostics, fingerMatches);
            return diagnostics.ToArray();
        }

        private static void AppendMissingRequiredDiagnostics(
            List<string> diagnostics,
            Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> requiredMatches)
        {
            var missing = new List<string>();
            foreach (BoneRule rule in RequiredRules)
            {
                if (requiredMatches[rule.HumanBone].Count == 0)
                {
                    missing.Add(rule.HumanBone + "(" + rule.MmdBoneName + ")");
                }
            }

            if (missing.Count > 0)
            {
                diagnostics.Add("missing-required: " + string.Join(", ", missing));
            }
        }

        private static void AppendAmbiguousDiagnostics(
            List<string> diagnostics,
            Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> buckets)
        {
            foreach (KeyValuePair<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> pair in buckets)
            {
                if (pair.Value.Count <= 1)
                {
                    continue;
                }

                var names = new List<string>();
                foreach (MmdHumanoidBoneMappingMatch match in pair.Value)
                {
                    names.Add(match.MmdBoneName + "#" + match.MmdBoneIndex);
                }

                diagnostics.Add("ambiguous: " + pair.Key + " <- " + string.Join(", ", names));
            }
        }

        private static MmdSerializableBoneMappingEntry[] CollectAllEntries(
            Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> requiredMatches,
            Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> optionalMatches,
            Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> fingerMatches)
        {
            var entries = new List<MmdSerializableBoneMappingEntry>();
            AddBucketEntries(requiredMatches, "Required", entries);
            AddBucketEntries(optionalMatches, "Optional", entries);
            AddBucketEntries(fingerMatches, "Finger", entries);
            return entries.ToArray();
        }

        private static void AddBucketEntries(
            Dictionary<HumanBodyBones, List<MmdHumanoidBoneMappingMatch>> buckets,
            string category,
            List<MmdSerializableBoneMappingEntry> entries)
        {
            foreach (List<MmdHumanoidBoneMappingMatch> matches in buckets.Values)
            {
                foreach (MmdHumanoidBoneMappingMatch match in matches)
                {
                    entries.Add(new MmdSerializableBoneMappingEntry(
                        match.HumanBone,
                        match.MmdBoneName,
                        match.MmdBoneIndex,
                        match.Required,
                        category));
                }
            }
        }

        private static bool IsIgnoredHelperBone(string boneName)
        {
            foreach (string fragment in IgnoredNameFragments)
            {
                if (boneName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeBoneName(string boneName)
        {
            return (boneName ?? string.Empty).Trim().Normalize(NormalizationForm.FormKC);
        }

        private readonly struct BoneRule
        {
            public BoneRule(HumanBodyBones humanBone, string mmdBoneName, params string[] aliases)
            {
                HumanBone = humanBone;
                MmdBoneName = mmdBoneName;
                Aliases = aliases != null ? (string[])aliases.Clone() : Array.Empty<string>();
            }

            public HumanBodyBones HumanBone { get; }

            public string MmdBoneName { get; }

            private string[] Aliases { get; }

            public bool Matches(string normalizedBoneName)
            {
                if (IsSameNormalizedName(normalizedBoneName, MmdBoneName))
                {
                    return true;
                }

                foreach (string alias in Aliases)
                {
                    if (IsSameNormalizedName(normalizedBoneName, alias))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool IsSameNormalizedName(string normalizedBoneName, string ruleName)
            {
                return string.Equals(
                    normalizedBoneName,
                    NormalizeBoneName(ruleName),
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
