#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Yohawing.MmdUnity.Parser;

namespace Yohawing.MmdUnity.Editor
{
    public static class MmdHumanoidClipConversionPlanner
    {
        public const string ReadyReadiness = "prerequisites-ready";
        public const string NotReadyReadiness = "prerequisites-not-ready";
        public const string DeferredReadiness = "writer-deferred";

        public static MmdHumanoidClipConversionPlan Analyze(
            MmdPmxAsset? pmxAsset,
            MmdVmdAsset? vmdAsset,
            MmdHumanoidSetupAsset? setupAsset)
        {
            return AnalyzePrerequisites(pmxAsset, vmdAsset, setupAsset);
        }

        public static MmdHumanoidClipConversionPlan AnalyzePrerequisites(
            MmdPmxAsset? pmxAsset,
            MmdVmdAsset? vmdAsset,
            MmdHumanoidSetupAsset? setupAsset)
        {
            var diagnostics = new List<string>();
            MmdMotionDefinition? motion = null;

            if (pmxAsset == null)
            {
                diagnostics.Add("pmx asset is null");
            }

            if (vmdAsset == null)
            {
                diagnostics.Add("vmd asset is null");
            }

            if (setupAsset == null)
            {
                diagnostics.Add("humanoid setup asset is null");
            }

            if (!IsAllInputsPresent(pmxAsset, vmdAsset, setupAsset))
            {
                return CreateResult(
                    false,
                    canCreateClipNow: false,
                    notReadyReadiness: NotReadyReadiness,
                    diagnostics: diagnostics,
                    pmxAsset: pmxAsset,
                    vmdAsset: vmdAsset,
                    setupAsset: setupAsset,
                    motion: null);
            }

            ValidateSetupAssociation(pmxAsset!, setupAsset!, diagnostics);
            ValidatePmxImportReadiness(pmxAsset!, diagnostics);
            ValidateSetupReadiness(setupAsset!, diagnostics);

            // VMD validation uses ONLY import-time cache (ImportSummaryStatus, StructuralDiagnostics, Max*/KeyframeCount).
            // Never call LoadMotion() here: analysis / inspector preview / readiness must not parse VMD.
            int cachedVmdMaxFrame = 0;
            int cachedVmdBoneKeyframeCount = 0;
            int cachedVmdMorphKeyframeCount = 0;
            int cachedVmdModelKeyframeCount = 0;
            ValidateVmdFromImportCache(
                vmdAsset!,
                diagnostics,
                out cachedVmdMaxFrame,
                out cachedVmdBoneKeyframeCount,
                out cachedVmdMorphKeyframeCount,
                out cachedVmdModelKeyframeCount);

            // motion remains null for analysis path (counts come from vmdAsset cache via CreateResult fallback).
            // Explicit bake/write paths (outside this analysis) may still LoadMotion when creating clip.
            motion = null;

            bool ready = diagnostics.Count == 0;
            string readiness = ready ? ReadyReadiness : NotReadyReadiness;
            if (ready)
            {
                diagnostics.Add("conversion-prerequisites: ready");
                diagnostics.Add("writer-status: CanCreateClipNow is true (in-memory writer in H6 slice 1).");
            }

            return CreateResult(
                ready,
                canCreateClipNow: ready,
                readiness,
                diagnostics,
                pmxAsset,
                vmdAsset,
                setupAsset,
                motion,
                cachedVmdMaxFrame: cachedVmdMaxFrame,
                cachedVmdBoneKeyframeCount: cachedVmdBoneKeyframeCount,
                cachedVmdMorphKeyframeCount: cachedVmdMorphKeyframeCount,
                cachedVmdModelKeyframeCount: cachedVmdModelKeyframeCount);
        }

        private static void ValidateVmdFromImportCache(
            MmdVmdAsset vmdAsset,
            List<string> diagnostics,
            out int maxFrame,
            out int boneKeyframeCount,
            out int morphKeyframeCount,
            out int modelKeyframeCount)
        {
            maxFrame = 0;
            boneKeyframeCount = 0;
            morphKeyframeCount = 0;
            modelKeyframeCount = 0;

            if (vmdAsset == null)
            {
                diagnostics.Add("vmd validation failed: vmd asset is null");
                return;
            }

            var status = vmdAsset.ImportSummaryStatus;
            var cachedDiags = vmdAsset.StructuralDiagnostics;

            if (status == Yohawing.MmdUnity.MmdVmdImportSummaryStatus.NotParsed)
            {
                diagnostics.Add("vmd validation failed: import summary status is NotParsed (cache missing). Reimport the .vmd asset.");
                return;
            }

            if (status == Yohawing.MmdUnity.MmdVmdImportSummaryStatus.Failed)
            {
                diagnostics.Add("vmd validation failed: import parse failed (cached status=Failed).");
            }

            if (cachedDiags != null && cachedDiags.Count > 0)
            {
                diagnostics.Add("vmd validation failed: cached structural diagnostics present (" + cachedDiags.Count + " issues). First: " + cachedDiags[0]);
            }

            if (diagnostics.Any(d => d.StartsWith("vmd validation failed")))
            {
                // not ready; do not trust counts from cache for failed/not-parsed cases
                return;
            }

            // Passed + empty structural diagnostics: safe to use import-time cached summary values. No LoadMotion().
            maxFrame = vmdAsset.MaxFrame;
            boneKeyframeCount = vmdAsset.BoneKeyframeCount;
            morphKeyframeCount = vmdAsset.MorphKeyframeCount;
            modelKeyframeCount = vmdAsset.ModelKeyframeCount;
        }

        private static void ValidateVmdMotion(
            MmdVmdAsset vmdAsset,
            out MmdMotionDefinition? motion,
            List<string> diagnostics)
        {
            motion = null;
            if (vmdAsset == null)
            {
                diagnostics.Add("motion validation: skipped because vmd asset is null");
                return;
            }

            try
            {
                motion = vmdAsset.LoadMotion();
            }
            catch (Exception ex)
            {
                diagnostics.Add("motion validation failed: load exception: " + ex.Message);
                return;
            }

            try
            {
                MmdMotionValidator.ThrowIfInvalid(motion);
            }
            catch (Exception ex)
            {
                diagnostics.Add("motion validation failed: structural validation: " + ex.Message);
            }
        }

        private static bool IsAllInputsPresent(
            MmdPmxAsset? pmxAsset,
            MmdVmdAsset? vmdAsset,
            MmdHumanoidSetupAsset? setupAsset)
        {
            return pmxAsset != null && vmdAsset != null && setupAsset != null;
        }

        private static void ValidateSetupAssociation(
            MmdPmxAsset pmxAsset,
            MmdHumanoidSetupAsset setupAsset,
            List<string> diagnostics)
        {
            if (!ReferenceEquals(setupAsset.PmxAsset, pmxAsset))
            {
                diagnostics.Add("setup validation failed: setup.PmxAsset mismatch.");
            }
        }

        private static void ValidateSetupReadiness(
            MmdHumanoidSetupAsset setupAsset,
            List<string> diagnostics)
        {
            if (setupAsset.MappingReadiness != MmdHumanoidSetupAsset.ReadyReadiness)
            {
                diagnostics.Add("setup validation failed: mapping not ready.");
            }

            if (!string.Equals(
                    setupAsset.MappingInputSource,
                    MmdHumanoidSetupAsset.ImportedHierarchyInputSource,
                    StringComparison.Ordinal))
            {
                diagnostics.Add(
                    "setup validation failed: mapping source is "
                    + setupAsset.MappingInputSource
                    + ", expected "
                    + MmdHumanoidSetupAsset.ImportedHierarchyInputSource + ".");
            }
        }

        private static void ValidatePmxImportReadiness(MmdPmxAsset pmxAsset, List<string> diagnostics)
        {
            if (pmxAsset.ImportedRoot == null)
            {
                diagnostics.Add("pmx validation failed: ImportedRoot is null.");
                return;
            }

            if (pmxAsset.HierarchyReadiness != MmdImportReadiness.Ready)
            {
                diagnostics.Add(
                    "pmx validation failed: hierarchy readiness is "
                    + pmxAsset.HierarchyReadiness
                    + ", expected "
                    + MmdImportReadiness.Ready + ".");
            }

            if (pmxAsset.RendererReadiness != MmdImportReadiness.Ready)
            {
                diagnostics.Add(
                    "pmx validation failed: renderer readiness is "
                    + pmxAsset.RendererReadiness
                    + ", expected "
                    + MmdImportReadiness.Ready + ".");
            }

            if (pmxAsset.BoneBindingReadiness != MmdImportReadiness.Ready)
            {
                diagnostics.Add(
                    "pmx validation failed: bone binding readiness is "
                    + pmxAsset.BoneBindingReadiness
                    + ", expected "
                    + MmdImportReadiness.Ready + ".");
            }

            SkinnedMeshRenderer? smr =
                pmxAsset.ImportedRoot.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
            if (smr == null)
            {
                diagnostics.Add("pmx validation failed: no SkinnedMeshRenderer under ImportedRoot.");
                return;
            }

            if (smr.sharedMesh == null)
            {
                diagnostics.Add("pmx validation failed: SkinnedMeshRenderer.sharedMesh is null.");
            }

            if (smr.bones == null || smr.bones.Length == 0)
            {
                diagnostics.Add("pmx validation failed: SkinnedMeshRenderer.bones is null or empty.");
            }
        }

        private static MmdHumanoidClipConversionPlan CreateResult(
            bool prerequisitesReady,
            bool canCreateClipNow,
            string notReadyReadiness,
            List<string> diagnostics,
            MmdPmxAsset? pmxAsset,
            MmdVmdAsset? vmdAsset,
            MmdHumanoidSetupAsset? setupAsset,
            MmdMotionDefinition? motion,
            int cachedVmdMaxFrame = 0,
            int cachedVmdBoneKeyframeCount = 0,
            int cachedVmdMorphKeyframeCount = 0,
            int cachedVmdModelKeyframeCount = 0)
        {
            int vmdMaxFrame = motion != null ? motion.maxFrame : (vmdAsset != null ? vmdAsset.MaxFrame : cachedVmdMaxFrame);
            int vmdBoneKeyframeCount = motion != null
                ? (motion.boneKeyframes != null ? motion.boneKeyframes.Count : 0)
                : (vmdAsset != null ? vmdAsset.BoneKeyframeCount : cachedVmdBoneKeyframeCount);
            int vmdMorphKeyframeCount = motion != null
                ? (motion.morphKeyframes != null ? motion.morphKeyframes.Count : 0)
                : (vmdAsset != null ? vmdAsset.MorphKeyframeCount : cachedVmdMorphKeyframeCount);
            int vmdModelKeyframeCount = motion != null
                ? (motion.modelKeyframes != null ? motion.modelKeyframes.Count : 0)
                : (vmdAsset != null ? vmdAsset.ModelKeyframeCount : cachedVmdModelKeyframeCount);

            return new MmdHumanoidClipConversionPlan(
                prerequisitesReady,
                canCreateClipNow: canCreateClipNow,
                prerequisitesReady ? ReadyReadiness : notReadyReadiness,
                diagnostics,
                pmxSourceId: pmxAsset?.SourceId ?? string.Empty,
                vmdSourceId: vmdAsset?.SourceId ?? string.Empty,
                setupSourceId: setupAsset?.PmxAsset?.SourceId ?? string.Empty,
                setupPmxAssetMatch: setupAsset != null && setupAsset.PmxAsset == pmxAsset,
                pmxBoneCount: pmxAsset?.BoneCount ?? 0,
                pmxHierarchyReadiness: pmxAsset != null ? pmxAsset.HierarchyReadiness.ToString() : string.Empty,
                pmxRendererReadiness: pmxAsset != null ? pmxAsset.RendererReadiness.ToString() : string.Empty,
                pmxBoneBindingReadiness: pmxAsset != null ? pmxAsset.BoneBindingReadiness.ToString() : string.Empty,
                setupMappingReadiness: setupAsset?.MappingReadiness ?? string.Empty,
                setupMappingInputSource: setupAsset?.MappingInputSource ?? string.Empty,
                setupRequiredMappedBoneCount: setupAsset?.RequiredMappedBoneCount ?? 0,
                setupOptionalMappedBoneCount: setupAsset?.OptionalMappedBoneCount ?? 0,
                setupMissingRequiredBoneCount: setupAsset?.MissingRequiredBoneCount ?? 0,
                setupAmbiguousMappingCount: setupAsset?.AmbiguousMappingCount ?? 0,
                vmdMaxFrame: vmdMaxFrame,
                vmdBoneKeyframeCount: vmdBoneKeyframeCount,
                vmdMorphKeyframeCount: vmdMorphKeyframeCount,
                vmdModelKeyframeCount: vmdModelKeyframeCount);
        }
    }

    public sealed class MmdHumanoidClipConversionPlan
    {
        public MmdHumanoidClipConversionPlan(
            bool prerequisitesReady,
            bool canCreateClipNow,
            string readiness,
            IReadOnlyList<string> diagnostics,
            string pmxSourceId,
            string vmdSourceId,
            string setupSourceId,
            bool setupPmxAssetMatch,
            int pmxBoneCount,
            string pmxHierarchyReadiness,
            string pmxRendererReadiness,
            string pmxBoneBindingReadiness,
            string setupMappingReadiness,
            string setupMappingInputSource,
            int setupRequiredMappedBoneCount,
            int setupOptionalMappedBoneCount,
            int setupMissingRequiredBoneCount,
            int setupAmbiguousMappingCount,
            int vmdMaxFrame,
            int vmdBoneKeyframeCount,
            int vmdMorphKeyframeCount,
            int vmdModelKeyframeCount)
        {
            PrerequisitesReady = prerequisitesReady;
            CanCreateClipNow = canCreateClipNow;
            Readiness = readiness ?? MmdHumanoidClipConversionPlanner.NotReadyReadiness;
            Diagnostics = diagnostics != null ? new List<string>(diagnostics).AsReadOnly() : Array.Empty<string>();
            PmxSourceId = pmxSourceId ?? string.Empty;
            VmdSourceId = vmdSourceId ?? string.Empty;
            SetupSourceId = setupSourceId ?? string.Empty;
            SetupPmxAssetMatch = setupPmxAssetMatch;
            PmxBoneCount = pmxBoneCount;
            PmxHierarchyReadiness = pmxHierarchyReadiness ?? string.Empty;
            PmxRendererReadiness = pmxRendererReadiness ?? string.Empty;
            PmxBoneBindingReadiness = pmxBoneBindingReadiness ?? string.Empty;
            SetupMappingReadiness = setupMappingReadiness ?? string.Empty;
            SetupMappingInputSource = setupMappingInputSource ?? string.Empty;
            SetupRequiredMappedBoneCount = setupRequiredMappedBoneCount;
            SetupOptionalMappedBoneCount = setupOptionalMappedBoneCount;
            SetupMissingRequiredBoneCount = setupMissingRequiredBoneCount;
            SetupAmbiguousMappingCount = setupAmbiguousMappingCount;
            VmdMaxFrame = vmdMaxFrame;
            VmdBoneKeyframeCount = vmdBoneKeyframeCount;
            VmdMorphKeyframeCount = vmdMorphKeyframeCount;
            VmdModelKeyframeCount = vmdModelKeyframeCount;
        }

        public bool PrerequisitesReady { get; }

        public bool CanCreateClipNow { get; }

        public string Readiness { get; }

        public IReadOnlyList<string> Diagnostics { get; }

        public string PmxSourceId { get; }

        public string VmdSourceId { get; }

        public string SetupSourceId { get; }

        public bool SetupPmxAssetMatch { get; }

        public int PmxBoneCount { get; }

        public string PmxHierarchyReadiness { get; }

        public string PmxRendererReadiness { get; }

        public string PmxBoneBindingReadiness { get; }

        public string SetupMappingReadiness { get; }

        public string SetupMappingInputSource { get; }

        public int SetupRequiredMappedBoneCount { get; }

        public int SetupOptionalMappedBoneCount { get; }

        public int SetupMissingRequiredBoneCount { get; }

        public int SetupAmbiguousMappingCount { get; }

        public int VmdMaxFrame { get; }

        public int VmdBoneKeyframeCount { get; }

        public int VmdMorphKeyframeCount { get; }

        public int VmdModelKeyframeCount { get; }
    }
}
