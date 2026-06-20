#nullable enable

using UnityEngine;
using Mmd;
using System.Collections.Generic;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// Editor-independent helper for the Humanoid Avatar sub-asset build step during PMX import.
    /// Owns only the readiness/diagnostic selection for non-Humanoid, the proxy rig creation,
    /// Avatar build, diagnostics combination, naming/hideFlags adjustment, and proxy root ownership.
    /// </summary>
    /// <remarks>
    /// This type lives in Runtime/Import under Mmd.UnityIntegration for API compat
    /// with prior import builders. The importer retains ownership of enum decisions and sub-asset
    /// registration.
    /// </remarks>
    public static class MmdPmxHumanoidAvatarImportBuilder
    {
        /// <summary>
        /// Result of the (optional) Humanoid Avatar import build.
        /// Avatar and ProxyRoot are non-null only on successful Ready case.
        /// </summary>
        public readonly struct MmdPmxHumanoidAvatarImportResult
        {
            public Avatar? Avatar { get; }
            public GameObject? ProxyRoot { get; }
            public IReadOnlyList<MmdHumanoidRetargetBinding> RetargetBindings { get; }
            public string Readiness { get; }
            public string Diagnostic { get; }
            public MmdHumanoidBoneMappingDiagnosticSummary MappingDiagnostics { get; }

            public MmdPmxHumanoidAvatarImportResult(
                Avatar? avatar,
                string readiness,
                string diagnostic,
                MmdHumanoidBoneMappingDiagnosticSummary? mappingDiagnostics = null,
                GameObject? proxyRoot = null,
                IReadOnlyList<MmdHumanoidRetargetBinding>? retargetBindings = null)
            {
                Avatar = avatar;
                ProxyRoot = proxyRoot;
                RetargetBindings = retargetBindings ?? System.Array.Empty<MmdHumanoidRetargetBinding>();
                Readiness = readiness ?? string.Empty;
                Diagnostic = diagnostic ?? string.Empty;
                MappingDiagnostics = mappingDiagnostics ?? MmdHumanoidBoneMappingDiagnosticSummary.Empty;
            }
        }

        /// <summary>
        /// Performs the Humanoid Avatar build decision and execution using the provided
        /// MmdPmxAsset (post-initialize) and model name for naming.
        /// Accepts bool + string label so the runtime helper does not depend on importer setting enums.
        /// </summary>
        public static MmdPmxHumanoidAvatarImportResult TryBuildHumanoidAvatar(
            MmdPmxAsset asset,
            string modelName,
            bool shouldBuildHumanoid,
            string animationTypeLabel,
            System.Collections.Generic.IReadOnlyList<MmdHumanoidBoneMappingOverride>? mappingOverrides = null)
        {
            if (!shouldBuildHumanoid)
            {
                return new MmdPmxHumanoidAvatarImportResult(
                    null,
                    "NotRequested",
                    "humanoid-avatar: animation type is " + animationTypeLabel);
            }

            MmdHumanoidProxyRigResult proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(
                asset,
                mappingOverrides: mappingOverrides);
            MmdHumanoidBoneMappingDiagnosticSummary mappingDiagnostics =
                MmdHumanoidBoneMappingDiagnosticsBuilder.Build(proxyRig, mappingOverrides);
            string readiness = proxyRig.Readiness;
            string diagnostic = string.Join("; ", proxyRig.Diagnostics);

            if (proxyRig.ProxyRoot == null)
            {
                return new MmdPmxHumanoidAvatarImportResult(null, readiness, diagnostic, mappingDiagnostics);
            }

            bool keepProxyRoot = false;
            try
            {
                MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyRig);
                diagnostic = CombineDiagnostics(diagnostic, avatarResult.Diagnostics);

                if (!avatarResult.IsValidHumanAvatar || avatarResult.Avatar == null)
                {
                    readiness = string.Equals(proxyRig.Readiness, MmdHumanoidSetupAsset.ReadyReadiness, System.StringComparison.Ordinal)
                        ? "AvatarInvalid"
                        : proxyRig.Readiness;
                    return new MmdPmxHumanoidAvatarImportResult(null, readiness, diagnostic, mappingDiagnostics);
                }

                Avatar avatar = avatarResult.Avatar;
                avatar.hideFlags = HideFlags.None;
                avatar.name = string.IsNullOrWhiteSpace(modelName)
                    ? "Avatar"
                    : modelName + " Avatar";
                PrepareProxyRootForImportAsset(proxyRig.ProxyRoot);
                IReadOnlyList<MmdHumanoidRetargetBinding> retargetBindings =
                    BuildRuntimeRetargetBindings(asset, proxyRig);
                readiness = MmdHumanoidSetupAsset.ReadyReadiness;
                keepProxyRoot = true;
                return new MmdPmxHumanoidAvatarImportResult(
                    avatar,
                    readiness,
                    diagnostic,
                    mappingDiagnostics,
                    proxyRig.ProxyRoot,
                    retargetBindings);
            }
            finally
            {
                if (!keepProxyRoot)
                {
                    Object.DestroyImmediate(proxyRig.ProxyRoot);
                }
            }
        }

        private static IReadOnlyList<MmdHumanoidRetargetBinding> BuildRuntimeRetargetBindings(
            MmdPmxAsset asset,
            MmdHumanoidProxyRigResult proxyRig)
        {
            if (asset.ImportedRoot == null || proxyRig.ProxyRoot == null || proxyRig.Matches.Count == 0)
            {
                return System.Array.Empty<MmdHumanoidRetargetBinding>();
            }

            SkinnedMeshRenderer? smr = asset.ImportedRoot.GetComponentInChildren<SkinnedMeshRenderer>(
                includeInactive: true);
            Transform[] nativeBones = smr != null && smr.bones != null
                ? smr.bones
                : System.Array.Empty<Transform>();

            var bindings = new List<MmdHumanoidRetargetBinding>(proxyRig.Matches.Count);
            foreach (MmdHumanoidBoneMappingMatch match in proxyRig.Matches)
            {
                proxyRig.BoneMap.TryGetValue(match.HumanBone, out Transform proxyTransform);
                Transform? nativeTransform = match.MmdBoneIndex >= 0 && match.MmdBoneIndex < nativeBones.Length
                    ? nativeBones[match.MmdBoneIndex]
                    : null;
                if (match.HumanBone == HumanBodyBones.Hips &&
                    proxyTransform != null &&
                    TryFindHipsTranslationTarget(nativeBones, match.MmdBoneIndex, out Transform? translationTarget, out int translationTargetIndex))
                {
                    bindings.Add(new MmdHumanoidRetargetBinding(
                        match.HumanBone,
                        match.MmdBoneIndex,
                        proxyTransform,
                        nativeTransform,
                        copyLocalPosition: true,
                        translationTargetTransform: translationTarget!,
                        translationTargetMmdBoneIndex: translationTargetIndex,
                        proxyBindLocalPosition: proxyTransform.localPosition,
                        translationTargetBindLocalPosition: translationTarget!.localPosition));
                    continue;
                }

                bindings.Add(new MmdHumanoidRetargetBinding(
                    match.HumanBone,
                    match.MmdBoneIndex,
                    proxyTransform,
                    nativeTransform));
            }

            return bindings;
        }

        private static bool TryFindHipsTranslationTarget(
            IReadOnlyList<Transform> nativeBones,
            int fallbackMmdBoneIndex,
            out Transform? target,
            out int targetMmdBoneIndex)
        {
            string[] preferredNames =
            {
                "センター",
                "グルーブ",
                "全ての親",
                "腰"
            };

            foreach (string preferredName in preferredNames)
            {
                for (int i = 0; i < nativeBones.Count; i++)
                {
                    Transform bone = nativeBones[i];
                    if (bone != null && string.Equals(bone.name, preferredName, System.StringComparison.Ordinal))
                    {
                        target = bone;
                        targetMmdBoneIndex = i;
                        return true;
                    }
                }
            }

            if (fallbackMmdBoneIndex >= 0 && fallbackMmdBoneIndex < nativeBones.Count)
            {
                target = nativeBones[fallbackMmdBoneIndex];
                targetMmdBoneIndex = fallbackMmdBoneIndex;
                return target != null;
            }

            target = null;
            targetMmdBoneIndex = -1;
            return false;
        }

        private static void PrepareProxyRootForImportAsset(GameObject proxyRoot)
        {
            proxyRoot.hideFlags = HideFlags.None;
            proxyRoot.SetActive(true);
            foreach (Transform child in proxyRoot.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                child.hideFlags = HideFlags.None;
                child.gameObject.hideFlags = HideFlags.None;
            }
        }

        private static string CombineDiagnostics(string first, System.Collections.Generic.IReadOnlyList<string> second)
        {
            string joinedSecond = second != null ? string.Join("; ", second) : string.Empty;
            if (string.IsNullOrWhiteSpace(first))
            {
                return joinedSecond;
            }

            if (string.IsNullOrWhiteSpace(joinedSecond))
            {
                return first;
            }

            return first + "; " + joinedSecond;
        }
    }
}
