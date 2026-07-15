#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mmd.Parser;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    public static class MmdHumanoidClipConversionPlanner
    {
        public const string ReadyReadiness = "prerequisites-ready";
        public const string NotReadyReadiness = "prerequisites-not-ready";
        public const string ImportedPmxHumanoidMappingSource = "Imported PMX Humanoid settings";

        public static MmdHumanoidClipConversionPlan AnalyzePrerequisites(
            MmdPmxAsset? pmxAsset,
            MmdVmdAsset? vmdAsset)
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

            if (!IsAllInputsPresent(pmxAsset, vmdAsset))
            {
                return CreateResult(
                    false,
                    canCreateClipNow: false,
                    notReadyReadiness: NotReadyReadiness,
                    diagnostics: diagnostics,
                    pmxAsset: pmxAsset,
                    vmdAsset: vmdAsset,
                    motion: null);
            }

            ValidatePmxImportReadiness(pmxAsset!, diagnostics);
            TryResolveImportedHumanoidState(pmxAsset!, diagnostics, out _, out _);

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
                diagnostics.Add("mapping-source: " + ImportedPmxHumanoidMappingSource + ".");
                diagnostics.Add("writer-status: CanCreateClipNow is true (in-memory writer in H6 slice 1).");
            }

            return CreateResult(
                ready,
                canCreateClipNow: ready,
                readiness,
                diagnostics,
                pmxAsset,
                vmdAsset,
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

            if (status == Mmd.MmdVmdImportSummaryStatus.NotParsed)
            {
                diagnostics.Add("vmd validation failed: import summary status is NotParsed (cache missing). Reimport the .vmd asset.");
                return;
            }

            if (status == Mmd.MmdVmdImportSummaryStatus.Failed)
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

        private static bool IsAllInputsPresent(
            MmdPmxAsset? pmxAsset,
            MmdVmdAsset? vmdAsset)
        {
            return pmxAsset != null && vmdAsset != null;
        }

        internal static bool TryResolveImportedHumanoidState(
            MmdPmxAsset pmxAsset,
            List<string> diagnostics,
            out MmdUnityPlaybackController? controller,
            out Transform[] nativeBones)
        {
            controller = null;
            nativeBones = Array.Empty<Transform>();

            if (!string.Equals(pmxAsset.AnimationType, "Humanoid", StringComparison.Ordinal))
            {
                diagnostics.Add(
                    "pmx humanoid validation failed: AnimationType is "
                    + pmxAsset.AnimationType
                    + ", expected Humanoid. Reimport the PMX with Animation Type set to Humanoid.");
                return false;
            }

            Avatar? avatar = pmxAsset.ImportedAvatar;
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                diagnostics.Add("pmx humanoid validation failed: ImportedAvatar is not a valid Humanoid Avatar.");
            }

            if (!string.Equals(
                    pmxAsset.HumanoidAvatarReadiness,
                    MmdHumanoidMappingReadiness.Ready,
                    StringComparison.Ordinal))
            {
                diagnostics.Add(
                    "pmx humanoid validation failed: HumanoidAvatarReadiness is "
                    + pmxAsset.HumanoidAvatarReadiness
                    + ", expected " + MmdHumanoidMappingReadiness.Ready + ".");
            }

            GameObject? importedRoot = pmxAsset.ImportedRoot;
            if (importedRoot == null)
            {
                diagnostics.Add("pmx humanoid validation failed: ImportedRoot is null.");
                return false;
            }

            controller = importedRoot.GetComponent<MmdUnityPlaybackController>();
            if (controller == null)
            {
                diagnostics.Add("pmx humanoid validation failed: imported root has no MmdUnityPlaybackController.");
                return false;
            }

            Transform? proxyRoot = controller.HumanoidProxyRoot;
            if (proxyRoot == null)
            {
                diagnostics.Add("pmx humanoid validation failed: imported controller has no HumanoidProxyRoot.");
                return false;
            }

            SkinnedMeshRenderer? smr = importedRoot.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
            nativeBones = smr != null && smr.bones != null ? smr.bones : Array.Empty<Transform>();
            if (nativeBones.Length == 0)
            {
                diagnostics.Add("pmx humanoid validation failed: imported SkinnedMeshRenderer has no bones.");
                return false;
            }

            IReadOnlyList<MmdHumanoidRetargetBinding> entries = controller.HumanoidRetargetEntries;
            if (entries == null || entries.Count == 0)
            {
                diagnostics.Add("pmx humanoid validation failed: imported controller has no HumanoidRetargetEntries.");
                return false;
            }

            bool valid = true;
            var usedHumanBones = new HashSet<HumanBodyBones>();
            for (int i = 0; i < entries.Count; i++)
            {
                MmdHumanoidRetargetBinding? entry = entries[i];
                if (entry == null)
                {
                    diagnostics.Add("pmx humanoid validation failed: HumanoidRetargetEntries contains a null entry.");
                    valid = false;
                    continue;
                }

                if (entry.HumanBone < 0
                    || entry.HumanBone >= HumanBodyBones.LastBone
                    || !usedHumanBones.Add(entry.HumanBone))
                {
                    diagnostics.Add("pmx humanoid validation failed: HumanoidRetargetEntries contains an invalid or duplicate HumanBodyBones mapping.");
                    valid = false;
                }

                int boneIndex = entry.MmdBoneIndex;
                if (boneIndex < 0 || boneIndex >= nativeBones.Length || nativeBones[boneIndex] == null)
                {
                    diagnostics.Add("pmx humanoid validation failed: retarget binding has an unusable MMD bone index.");
                    valid = false;
                    continue;
                }

                if (!ReferenceEquals(entry.NativeTransform, nativeBones[boneIndex]))
                {
                    diagnostics.Add("pmx humanoid validation failed: retarget binding native transform does not match its persisted MMD bone index.");
                    valid = false;
                }

                if (entry.ProxyTransform == null || !IsDescendantOrSelf(entry.ProxyTransform, proxyRoot))
                {
                    diagnostics.Add("pmx humanoid validation failed: retarget binding has an unusable proxy transform.");
                    valid = false;
                }
            }

            return valid && avatar != null && avatar.isValid && avatar.isHuman
                         && string.Equals(
                             pmxAsset.HumanoidAvatarReadiness,
                             MmdHumanoidMappingReadiness.Ready,
                             StringComparison.Ordinal);
        }

        private static bool IsDescendantOrSelf(Transform transform, Transform root)
        {
            for (Transform? current = transform; current != null; current = current.parent)
            {
                if (ReferenceEquals(current, root))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidatePmxImportReadiness(MmdPmxAsset pmxAsset, List<string> diagnostics)
        {
            GameObject? importedRoot = pmxAsset.ImportedRoot;
            if (importedRoot == null)
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
                importedRoot.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
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
                pmxBoneCount: pmxAsset?.BoneCount ?? 0,
                pmxHierarchyReadiness: pmxAsset != null ? pmxAsset.HierarchyReadiness.ToString() : string.Empty,
                pmxRendererReadiness: pmxAsset != null ? pmxAsset.RendererReadiness.ToString() : string.Empty,
                pmxBoneBindingReadiness: pmxAsset != null ? pmxAsset.BoneBindingReadiness.ToString() : string.Empty,
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
            int pmxBoneCount,
            string pmxHierarchyReadiness,
            string pmxRendererReadiness,
            string pmxBoneBindingReadiness,
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
            PmxBoneCount = pmxBoneCount;
            PmxHierarchyReadiness = pmxHierarchyReadiness ?? string.Empty;
            PmxRendererReadiness = pmxRendererReadiness ?? string.Empty;
            PmxBoneBindingReadiness = pmxBoneBindingReadiness ?? string.Empty;
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

        public int PmxBoneCount { get; }

        public string PmxHierarchyReadiness { get; }

        public string PmxRendererReadiness { get; }

        public string PmxBoneBindingReadiness { get; }

        public int VmdMaxFrame { get; }

        public int VmdBoneKeyframeCount { get; }

        public int VmdMorphKeyframeCount { get; }

        public int VmdModelKeyframeCount { get; }
    }
}
