#nullable enable

using UnityEngine;
using Mmd;
using System.Collections.Generic;
using Mmd.Parser;

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
            public IReadOnlyList<MmdHumanoidAppendTransformBinding> AppendBindings { get; }
            public string Readiness { get; }
            public string Diagnostic { get; }
            public MmdHumanoidBoneMappingDiagnosticSummary MappingDiagnostics { get; }

            public MmdPmxHumanoidAvatarImportResult(
                Avatar? avatar,
                string readiness,
                string diagnostic,
                MmdHumanoidBoneMappingDiagnosticSummary? mappingDiagnostics = null,
                GameObject? proxyRoot = null,
                IReadOnlyList<MmdHumanoidRetargetBinding>? retargetBindings = null,
                IReadOnlyList<MmdHumanoidAppendTransformBinding>? appendBindings = null)
            {
                Avatar = avatar;
                ProxyRoot = proxyRoot;
                RetargetBindings = retargetBindings ?? System.Array.Empty<MmdHumanoidRetargetBinding>();
                AppendBindings = appendBindings ?? System.Array.Empty<MmdHumanoidAppendTransformBinding>();
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
            System.Collections.Generic.IReadOnlyList<MmdHumanoidBoneMappingOverride>? mappingOverrides = null,
            MmdModelDefinition? model = null,
            MmdHumanoidRetargetQualitySettings? retargetQualitySettings = null)
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
            Avatar? createdAvatar = null;
            bool keepAvatar = false;
            try
            {
                MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(
                    proxyRig,
                    retargetQualitySettings);
                createdAvatar = avatarResult.Avatar;
                diagnostic = CombineDiagnostics(diagnostic, avatarResult.Diagnostics);

                if (!avatarResult.IsValidHumanAvatar || avatarResult.Avatar == null)
                {
                    readiness = string.Equals(proxyRig.Readiness, MmdHumanoidMappingReadiness.Ready, System.StringComparison.Ordinal)
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
                IReadOnlyList<MmdHumanoidAppendTransformBinding> appendBindings =
                    BuildRuntimeAppendTransformBindings(model ?? asset.LoadModel(), asset.ImportedRoot);
                readiness = MmdHumanoidMappingReadiness.Ready;
                keepProxyRoot = true;
                keepAvatar = true;
                return new MmdPmxHumanoidAvatarImportResult(
                    avatar,
                    readiness,
                    diagnostic,
                    mappingDiagnostics,
                    proxyRig.ProxyRoot,
                    retargetBindings,
                    appendBindings);
            }
            finally
            {
                if (!keepProxyRoot)
                {
                    Object.DestroyImmediate(proxyRig.ProxyRoot);
                }
                if (!keepAvatar && createdAvatar != null)
                {
                    Object.DestroyImmediate(createdAvatar);
                }
            }
        }

        private static IReadOnlyList<MmdHumanoidRetargetBinding> BuildRuntimeRetargetBindings(
            MmdPmxAsset asset,
            MmdHumanoidProxyRigResult proxyRig)
        {
            GameObject? importedRoot = asset.ImportedRoot;
            if (importedRoot == null || proxyRig.ProxyRoot == null || proxyRig.Matches.Count == 0)
            {
                return System.Array.Empty<MmdHumanoidRetargetBinding>();
            }

            SkinnedMeshRenderer? smr = importedRoot.GetComponentInChildren<SkinnedMeshRenderer>(
                includeInactive: true);
            Transform[] nativeBones = smr != null && smr.bones != null
                ? smr.bones
                : System.Array.Empty<Transform>();

            var nativeTransformByHumanBone = new Dictionary<HumanBodyBones, Transform>();
            foreach (MmdHumanoidBoneMappingMatch match in proxyRig.Matches)
            {
                if (match.MmdBoneIndex >= 0 && match.MmdBoneIndex < nativeBones.Length)
                {
                    nativeTransformByHumanBone[match.HumanBone] = nativeBones[match.MmdBoneIndex];
                }
            }

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
                        proxyTransform.localRotation,
                        nativeTransform != null ? nativeTransform.localRotation : Quaternion.identity,
                        copyLocalPosition: true,
                        translationTargetTransform: translationTarget!,
                        translationTargetMmdBoneIndex: translationTargetIndex,
                        proxyBindLocalPosition: proxyTransform.localPosition,
                        translationTargetBindLocalPosition: translationTarget!.localPosition));
                    continue;
                }

                Quaternion nativeBindLocalRotation = nativeTransform != null
                    ? ResolveNativeHumanoidBindLocalRotation(
                        match.HumanBone,
                        nativeTransform,
                        nativeTransformByHumanBone)
                    : Quaternion.identity;
                bindings.Add(new MmdHumanoidRetargetBinding(
                    match.HumanBone,
                    match.MmdBoneIndex,
                    proxyTransform,
                    nativeTransform,
                    proxyTransform != null ? proxyTransform.localRotation : Quaternion.identity,
                    nativeBindLocalRotation));
            }

            return bindings;
        }

        private static Quaternion ResolveNativeHumanoidBindLocalRotation(
            HumanBodyBones humanBone,
            Transform nativeTransform,
            IReadOnlyDictionary<HumanBodyBones, Transform> nativeTransformByHumanBone)
        {
            HumanBodyBones childBone;
            Vector3 targetWorldDirection;
            switch (humanBone)
            {
                case HumanBodyBones.LeftUpperArm:
                    childBone = HumanBodyBones.LeftLowerArm;
                    targetWorldDirection = Vector3.left;
                    break;
                case HumanBodyBones.RightUpperArm:
                    childBone = HumanBodyBones.RightLowerArm;
                    targetWorldDirection = Vector3.right;
                    break;
                default:
                    return nativeTransform.localRotation;
            }

            if (!nativeTransformByHumanBone.TryGetValue(childBone, out Transform childTransform))
            {
                return nativeTransform.localRotation;
            }

            Vector3 currentDirection = childTransform.position - nativeTransform.position;
            if (!float.IsFinite(currentDirection.x) ||
                !float.IsFinite(currentDirection.y) ||
                !float.IsFinite(currentDirection.z) ||
                currentDirection.sqrMagnitude < 1e-8f)
            {
                return nativeTransform.localRotation;
            }

            Quaternion deltaWorld = Quaternion.FromToRotation(
                currentDirection.normalized,
                targetWorldDirection);
            Quaternion targetWorldRotation = deltaWorld * nativeTransform.rotation;
            Transform? parent = nativeTransform.parent;
            return parent != null
                ? Quaternion.Inverse(parent.rotation) * targetWorldRotation
                : targetWorldRotation;
        }

        internal static IReadOnlyList<MmdHumanoidAppendTransformBinding> BuildRuntimeAppendTransformBindings(
            MmdModelDefinition model,
            GameObject? importedRoot)
        {
            if (model == null || importedRoot == null || model.bones == null || model.bones.Count == 0)
            {
                return System.Array.Empty<MmdHumanoidAppendTransformBinding>();
            }

            SkinnedMeshRenderer? smr = importedRoot.GetComponentInChildren<SkinnedMeshRenderer>(
                includeInactive: true);
            Transform[] nativeBones = smr != null && smr.bones != null
                ? smr.bones
                : System.Array.Empty<Transform>();
            if (nativeBones.Length == 0)
            {
                return System.Array.Empty<MmdHumanoidAppendTransformBinding>();
            }

            var bindings = new List<MmdHumanoidAppendTransformBinding>();
            foreach (MmdBoneDefinition bone in model.bones)
            {
                if (bone == null
                    || bone.appendParentIndex < 0
                    || (!bone.appendRotation && !bone.appendTranslation))
                {
                    continue;
                }

                Transform? target = bone.index >= 0 && bone.index < nativeBones.Length
                    ? nativeBones[bone.index]
                    : null;
                Transform? appendParent = bone.appendParentIndex >= 0 && bone.appendParentIndex < nativeBones.Length
                    ? nativeBones[bone.appendParentIndex]
                    : null;
                if (target == null || appendParent == null)
                {
                    continue;
                }

                bindings.Add(new MmdHumanoidAppendTransformBinding(
                    target,
                    bone.index,
                    appendParent,
                    bone.appendParentIndex,
                    bone.appendRatio,
                    bone.appendRotation,
                    bone.appendTranslation,
                    bone.appendLocal,
                    target.localRotation,
                    target.localPosition,
                    appendParent.localRotation,
                    appendParent.localPosition,
                    bone.index));
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
