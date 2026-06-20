#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Mmd.Parser;

namespace Mmd
{
    /// <summary>
    /// Result of creating a hidden Humanoid proxy rig from an MMD model definition.
    /// The proxy rig is a Transform hierarchy matching mapped HumanBodyBones only,
    /// parented with Unity Humanoid relationships. No Avatar, Animator, or mapping
    /// assets are created.
    /// </summary>
    public sealed class MmdHumanoidProxyRigResult
    {
        public MmdHumanoidProxyRigResult(
            GameObject? proxyRoot,
            IReadOnlyDictionary<HumanBodyBones, Transform>? boneMap,
            IReadOnlyList<MmdHumanoidBoneMappingMatch> matches,
            string readiness,
            string[] diagnostics)
        {
            ProxyRoot = proxyRoot;
            BoneMap = boneMap ?? new Dictionary<HumanBodyBones, Transform>();
            Matches = matches ?? Array.Empty<MmdHumanoidBoneMappingMatch>();
            Readiness = readiness ?? MmdHumanoidSetupAsset.NotEvaluatedReadiness;
            Diagnostics = diagnostics ?? Array.Empty<string>();
        }

        /// <summary>
        /// The hidden root GameObject of the proxy rig. Null when creation failed.
        /// </summary>
        public GameObject? ProxyRoot { get; }

        /// <summary>
        /// Map from HumanBodyBones to their corresponding proxy Transform.
        /// Only contains bones that were successfully mapped and created.
        /// </summary>
        public IReadOnlyDictionary<HumanBodyBones, Transform> BoneMap { get; }

        /// <summary>
        /// The list of mapping matches used to build the proxy rig.
        /// </summary>
        public IReadOnlyList<MmdHumanoidBoneMappingMatch> Matches { get; }

        /// <summary>
        /// Readiness string: Ready, MissingRequired, Ambiguous, NoBones, EvaluationFailed.
        /// </summary>
        public string Readiness { get; }

        /// <summary>
        /// Human-readable diagnostics from the mapping evaluation.
        /// </summary>
        public IReadOnlyList<string> Diagnostics { get; }
    }

    /// <summary>
    /// Result of building a Unity Avatar from an MMD Humanoid proxy rig.
    /// The Avatar is created via AvatarBuilder.BuildHumanAvatar using a
    /// synthetic HumanDescription built from the proxy rig transforms.
    /// </summary>
    public sealed class MmdHumanoidAvatarBuildResult
    {
        public MmdHumanoidAvatarBuildResult(
            Avatar? avatar,
            IReadOnlyList<string> diagnostics)
        {
            Avatar = avatar;
            Diagnostics = diagnostics ?? Array.Empty<string>();
        }

        /// <summary>
        /// The built Avatar, or null if building failed.
        /// When non-null and valid, hideFlags is HideAndDontSave.
        /// </summary>
        public Avatar? Avatar { get; }

        /// <summary>
        /// Human-readable diagnostics from the Avatar build process.
        /// Includes whether Avatar is null, isValid, and isHuman.
        /// </summary>
        public IReadOnlyList<string> Diagnostics { get; }

        /// <summary>
        /// True when Avatar is non-null, isValid, and isHuman.
        /// </summary>
        public bool IsValidHumanAvatar =>
            Avatar != null && Avatar.isValid && Avatar.isHuman;
    }

    /// <summary>
    /// Creates a hidden Unity Transform hierarchy using Unity Humanoid parent
    /// relationships for the HumanBodyBones detected from an MMD model's bone
    /// list. No Avatar, Animator, or mapping assets are created.
    ///
    /// Transform world position is set from MmdBoneDefinition.origin with
    /// MMD->Unity conversion [-x, y, -z], then preserved while reparenting into
    /// the Humanoid hierarchy. MMD helper bones and unmapped MMD parents are not
    /// mirrored into the proxy rig.
    ///
    /// Design: Dual Rig 方式の Hidden Humanoid Proxy Rig 生成を担当する。
    /// 生成された Transform 階層は HumanBodyBones 名にマッピングされた MMD ボーンのみを含む。
    /// IK/捩/操作 helper ボーンは含まれない。
    /// </summary>
    public static class MmdHumanoidProxyRigFactory
    {
        /// <summary>
        /// Create a hidden proxy rig from an MMD model definition.
        /// </summary>
        /// <param name="model">Validated MmdModelDefinition containing the bone list.</param>
        /// <param name="proxyRootName">Optional name for the proxy root GameObject (default: "MmdHumanoidProxyRig").</param>
        /// <returns>A result containing the proxy root, bone map, matches, and diagnostics.</returns>
        public static MmdHumanoidProxyRigResult CreateProxyRig(
            MmdModelDefinition model,
            string proxyRootName = "MmdHumanoidProxyRig",
            IReadOnlyList<MmdHumanoidBoneMappingOverride>? mappingOverrides = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            // Step 1: Evaluate mapping from MMD bone names to HumanBodyBones.
            MmdHumanoidBoneMappingReport report = MmdHumanoidBoneMappingEvaluator.Evaluate(model);
            string readiness = report.Readiness;

            // Stop early if there are no bones at all.
            if (readiness == MmdHumanoidSetupAsset.NoBonesReadiness)
            {
                return new MmdHumanoidProxyRigResult(
                    null,
                    null,
                    Array.Empty<MmdHumanoidBoneMappingMatch>(),
                    readiness,
                    report.Diagnostics);
            }

            // Step 2: Collect mapping matches (only exact, unambiguous first matches).
            var matches = new List<MmdHumanoidBoneMappingMatch>();
            var usedHumanBones = new HashSet<HumanBodyBones>();
            foreach (MmdBoneDefinition bone in model.bones)
            {
                if (bone == null || string.IsNullOrWhiteSpace(bone.name))
                {
                    continue;
                }

                if (!MmdHumanoidBoneMappingEvaluator.TryMapBoneName(
                        bone.name, out HumanBodyBones humanBone, out bool required))
                {
                    continue;
                }

                // Skip if we already have a match for this HumanBodyBones (use first match).
                if (!usedHumanBones.Add(humanBone))
                {
                    continue;
                }

                matches.Add(new MmdHumanoidBoneMappingMatch(
                    humanBone,
                    bone.name,
                    bone.index,
                    required));
            }

            var combinedDiagnostics = new List<string>();
            combinedDiagnostics.AddRange(report.Diagnostics);
            if (HasMappingOverrides(mappingOverrides))
            {
                matches = ApplyMappingOverrides(
                    matches,
                    BuildModelBoneNameList(model),
                    BuildModelBoneIndexList(model),
                    mappingOverrides,
                    combinedDiagnostics,
                    out int appliedOverrideCount);
                if (appliedOverrideCount > 0)
                {
                    readiness = ClassifyMergedReadiness(matches, model.bones.Count);
                }
            }

            // Step 3: Build the proxy Transform hierarchy.
            // The hidden proxy uses Unity Humanoid parent relationships, not the
            // MMD/imported hierarchy. This keeps helpers such as center/groove/waist
            // out of the Avatar skeleton while preserving source world pose.
            var boneMap = new Dictionary<HumanBodyBones, Transform>();
            var mmdBoneIndexToUnityPos = new Dictionary<int, Vector3>();

            GameObject root = new GameObject(proxyRootName);
            root.hideFlags = HideFlags.HideInHierarchy;
            root.SetActive(false);

            // First pass: create Transforms for each mapped bone.
            foreach (MmdHumanoidBoneMappingMatch match in matches)
            {
                GameObject boneObject = new GameObject(match.HumanBone.ToString());
                boneObject.hideFlags = HideFlags.HideInHierarchy;
                Transform boneTransform = boneObject.transform;
                boneTransform.SetParent(root.transform, worldPositionStays: false);
                boneTransform.hideFlags = HideFlags.HideInHierarchy;
                boneMap[match.HumanBone] = boneTransform;

                // Record Unity-space origin position for localPosition calculation.
                MmdBoneDefinition? boneDef = FindBoneByIndex(model, match.MmdBoneIndex);
                Vector3 unityPos = MmdOriginToUnityPosition(boneDef?.origin);
                mmdBoneIndexToUnityPos[match.MmdBoneIndex] = unityPos;
                boneTransform.SetPositionAndRotation(unityPos, Quaternion.identity);
                boneTransform.localScale = Vector3.one;
            }

            foreach (MmdHumanoidBoneMappingMatch match in EnumerateMatchesInHumanoidOrder(matches))
            {
                if (!boneMap.TryGetValue(match.HumanBone, out Transform currentTransform))
                    continue;

                Vector3 myUnityPos = mmdBoneIndexToUnityPos[match.MmdBoneIndex];
                Quaternion myUnityRot = Quaternion.identity;
                Transform parentTransform = ResolveHumanoidParent(match.HumanBone, boneMap, root.transform);

                currentTransform.SetPositionAndRotation(myUnityPos, myUnityRot);
                currentTransform.SetParent(parentTransform, worldPositionStays: true);
                currentTransform.SetPositionAndRotation(myUnityPos, myUnityRot);
                currentTransform.localScale = Vector3.one;
            }
            ApplyHumanoidSiblingOrder(root.transform, boneMap);

            // Build combined diagnostics.
            int originCount = 0;
            int originZeroCount = 0;
            foreach (MmdBoneDefinition b in model.bones)
            {
                if (b != null && b.origin != null && b.origin.Length >= 3)
                    originCount++;
                else if (b != null)
                    originZeroCount++;
            }
            combinedDiagnostics.Add("proxy-rig: created " + boneMap.Count + " bone transforms, root hidden");
            combinedDiagnostics.Add("proxy-rig: origins found=" + originCount + " fallback-zero=" + originZeroCount);

            return new MmdHumanoidProxyRigResult(
                root,
                boneMap,
                matches,
                readiness,
                combinedDiagnostics.ToArray());
        }

        /// <summary>
        /// Create a hidden proxy rig from importer-owned hierarchy bones.
        /// </summary>
        /// <param name="pmxAsset">Source PMX asset holding the imported hierarchy.</param>
        /// <param name="proxyRootName">Optional name for the proxy root GameObject (default: "MmdHumanoidProxyRig").</param>
        /// <returns>A result containing the proxy root, bone map, matches, and diagnostics.</returns>
        public static MmdHumanoidProxyRigResult CreateProxyRig(
            MmdPmxAsset pmxAsset,
            string proxyRootName = "MmdHumanoidProxyRig",
            IReadOnlyList<MmdHumanoidBoneMappingOverride>? mappingOverrides = null)
        {
            if (pmxAsset == null)
            {
                throw new ArgumentNullException(nameof(pmxAsset));
            }

            if (pmxAsset.ImportedRoot == null)
            {
                return new MmdHumanoidProxyRigResult(
                    null,
                    null,
                    Array.Empty<MmdHumanoidBoneMappingMatch>(),
                    MmdHumanoidSetupAsset.HierarchyNotReadyReadiness,
                    new[] { "hierarchy-not-ready: ImportedRoot is null. Reimport the .pmx asset." });
            }

            if (pmxAsset.BoneCount > 0)
            {
                SkinnedMeshRenderer? smr = pmxAsset.ImportedRoot.GetComponentInChildren<SkinnedMeshRenderer>(
                    includeInactive: true);

                if (smr == null)
                {
                    return new MmdHumanoidProxyRigResult(
                        null,
                        null,
                        Array.Empty<MmdHumanoidBoneMappingMatch>(),
                        MmdHumanoidSetupAsset.HierarchyNotReadyReadiness,
                        new[] { "hierarchy-not-ready: No SkinnedMeshRenderer found under ImportedRoot." });
                }

                if (smr.bones == null)
                {
                    return new MmdHumanoidProxyRigResult(
                        null,
                        null,
                        Array.Empty<MmdHumanoidBoneMappingMatch>(),
                        MmdHumanoidSetupAsset.HierarchyNotReadyReadiness,
                        new[] { "hierarchy-not-ready: SkinnedMeshRenderer.bones is null under ImportedRoot." });
                }

                if (smr.bones.Length != pmxAsset.BoneCount)
                {
                    return new MmdHumanoidProxyRigResult(
                        null,
                        null,
                        Array.Empty<MmdHumanoidBoneMappingMatch>(),
                        MmdHumanoidSetupAsset.HierarchyNotReadyReadiness,
                        new[]
                        {
                            "hierarchy-not-ready: SkinnedMeshRenderer.bones length does not match PMX BoneCount."
                        });
                }

                for (int i = 0; i < smr.bones.Length; i++)
                {
                    if (smr.bones[i] == null)
                    {
                        return new MmdHumanoidProxyRigResult(
                            null,
                            null,
                            Array.Empty<MmdHumanoidBoneMappingMatch>(),
                            MmdHumanoidSetupAsset.HierarchyNotReadyReadiness,
                            new[] { "hierarchy-not-ready: SkinnedMeshRenderer.bones contains null entry." });
                    }
                }

                return CreateProxyRigFromBoneTransforms(
                    smr.bones!,
                    pmxAsset.ImportedRoot.transform,
                    proxyRootName,
                    mappingOverrides);
            }

            return new MmdHumanoidProxyRigResult(
                null,
                null,
                Array.Empty<MmdHumanoidBoneMappingMatch>(),
                MmdHumanoidSetupAsset.NoBonesReadiness,
                new[] { "no-bones: PMX model has no bones." });
        }

        /// <summary>
        /// Create a hidden proxy rig from imported hierarchy bone transforms.
        /// </summary>
        /// <param name="bones">Ordered transform list matching MMD bone indices.</param>
        /// <param name="hierarchyRoot">Hierarchy root transform used as the proxy rig root pose.</param>
        /// <param name="proxyRootName">Name for the proxy root GameObject.</param>
        /// <returns>A result containing the proxy rig root and related mapping info.</returns>
        internal static MmdHumanoidProxyRigResult CreateProxyRigFromBoneTransforms(
            IReadOnlyList<Transform> bones,
            Transform hierarchyRoot,
            string proxyRootName = "MmdHumanoidProxyRig",
            IReadOnlyList<MmdHumanoidBoneMappingOverride>? mappingOverrides = null)
        {
            if (bones == null)
            {
                throw new ArgumentNullException(nameof(bones));
            }

            if (hierarchyRoot == null)
            {
                throw new ArgumentNullException(nameof(hierarchyRoot));
            }

            var sourceBoneNames = new string[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                Transform? bone = bones[i];
                sourceBoneNames[i] = bone != null ? bone.name : string.Empty;
            }

            MmdHumanoidBoneMappingReport report = MmdHumanoidBoneMappingEvaluator.EvaluateBoneNames(sourceBoneNames);

            var matches = new List<MmdHumanoidBoneMappingMatch>();
            var usedHumanBones = new HashSet<HumanBodyBones>();
            for (int i = 0; i < bones.Count; i++)
            {
                Transform? sourceBone = bones[i];
                if (sourceBone == null)
                {
                    continue;
                }

                if (!MmdHumanoidBoneMappingEvaluator.TryMapBoneName(
                        sourceBone.name,
                        out HumanBodyBones humanBone,
                        out bool required))
                {
                    continue;
                }

                if (usedHumanBones.Add(humanBone))
                {
                    matches.Add(new MmdHumanoidBoneMappingMatch(humanBone, sourceBone.name, i, required));
                }
            }

            var diagnostics = new List<string> { "proxy-rig: input=ImportedHierarchy" };
            diagnostics.AddRange(report.Diagnostics);
            string readiness = report.Readiness;
            if (HasMappingOverrides(mappingOverrides))
            {
                matches = ApplyMappingOverrides(
                    matches,
                    sourceBoneNames,
                    null,
                    mappingOverrides,
                    diagnostics,
                    out int appliedOverrideCount);
                if (appliedOverrideCount > 0)
                {
                    readiness = ClassifyMergedReadiness(matches, bones.Count);
                }
            }

            if (readiness == MmdHumanoidSetupAsset.NoBonesReadiness)
            {
                return new MmdHumanoidProxyRigResult(
                    null,
                    null,
                    matches.AsReadOnly(),
                    MmdHumanoidSetupAsset.NoBonesReadiness,
                    diagnostics.ToArray());
            }

            var boneMap = new Dictionary<HumanBodyBones, Transform>();
            var root = new GameObject(proxyRootName);
            root.hideFlags = HideFlags.HideInHierarchy;
            root.SetActive(false);
            root.transform.SetPositionAndRotation(hierarchyRoot.position, hierarchyRoot.rotation);
            root.transform.localScale = hierarchyRoot.localScale;
            var sourceWorldPoseByHumanBone = new Dictionary<HumanBodyBones, (Vector3 position, Quaternion rotation, Vector3 scale)>();

            foreach (MmdHumanoidBoneMappingMatch match in matches)
            {
                Transform sourceBone = bones[match.MmdBoneIndex];
                var proxyBone = new GameObject(match.HumanBone.ToString());
                proxyBone.hideFlags = HideFlags.HideInHierarchy;
                Transform proxyBoneTransform = proxyBone.transform;
                proxyBoneTransform.SetParent(root.transform, worldPositionStays: false);
                boneMap[match.HumanBone] = proxyBoneTransform;
                sourceWorldPoseByHumanBone[match.HumanBone] =
                    (sourceBone.position, sourceBone.rotation, sourceBone.localScale);
                proxyBoneTransform.SetPositionAndRotation(sourceBone.position, sourceBone.rotation);
                proxyBoneTransform.localScale = sourceBone.localScale;
            }

            foreach (MmdHumanoidBoneMappingMatch match in EnumerateMatchesInHumanoidOrder(matches))
            {
                if (!boneMap.TryGetValue(match.HumanBone, out Transform proxyBoneTransform))
                {
                    continue;
                }

                Transform parentTransform = ResolveHumanoidParent(match.HumanBone, boneMap, root.transform);
                (Vector3 position, Quaternion rotation, Vector3 scale) pose = sourceWorldPoseByHumanBone[match.HumanBone];
                proxyBoneTransform.SetPositionAndRotation(pose.position, pose.rotation);
                proxyBoneTransform.SetParent(parentTransform, worldPositionStays: true);
                proxyBoneTransform.SetPositionAndRotation(pose.position, pose.rotation);
                proxyBoneTransform.localScale = pose.scale;
            }
            ApplyHumanoidSiblingOrder(root.transform, boneMap);

            diagnostics.Add(
                "proxy-rig: created " + boneMap.Count + " bone transforms, root hidden");

            return new MmdHumanoidProxyRigResult(
                root,
                boneMap,
                matches,
                readiness,
                diagnostics.ToArray());
        }

        private static readonly HumanBodyBones[] HumanoidTraversalOrder =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftEye,
            HumanBodyBones.RightEye,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.LeftThumbProximal,
            HumanBodyBones.LeftThumbIntermediate,
            HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal,
            HumanBodyBones.LeftIndexIntermediate,
            HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal,
            HumanBodyBones.LeftMiddleIntermediate,
            HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal,
            HumanBodyBones.LeftRingIntermediate,
            HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal,
            HumanBodyBones.LeftLittleIntermediate,
            HumanBodyBones.LeftLittleDistal,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.RightThumbProximal,
            HumanBodyBones.RightThumbIntermediate,
            HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal,
            HumanBodyBones.RightIndexIntermediate,
            HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal,
            HumanBodyBones.RightMiddleIntermediate,
            HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal,
            HumanBodyBones.RightRingIntermediate,
            HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal,
            HumanBodyBones.RightLittleIntermediate,
            HumanBodyBones.RightLittleDistal,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            HumanBodyBones.RightToes,
        };

        private static IEnumerable<MmdHumanoidBoneMappingMatch> EnumerateMatchesInHumanoidOrder(
            IReadOnlyList<MmdHumanoidBoneMappingMatch> matches)
        {
            var byHumanBone = new Dictionary<HumanBodyBones, MmdHumanoidBoneMappingMatch>();
            foreach (MmdHumanoidBoneMappingMatch match in matches)
            {
                byHumanBone[match.HumanBone] = match;
            }

            var emitted = new HashSet<HumanBodyBones>();
            foreach (HumanBodyBones humanBone in HumanoidTraversalOrder)
            {
                if (byHumanBone.TryGetValue(humanBone, out MmdHumanoidBoneMappingMatch match))
                {
                    emitted.Add(humanBone);
                    yield return match;
                }
            }

            foreach (MmdHumanoidBoneMappingMatch match in matches)
            {
                if (emitted.Add(match.HumanBone))
                {
                    yield return match;
                }
            }
        }

        private static Transform ResolveHumanoidParent(
            HumanBodyBones humanBone,
            IReadOnlyDictionary<HumanBodyBones, Transform> boneMap,
            Transform root)
        {
            return TryResolveHumanoidParent(humanBone, boneMap, out Transform? parent)
                ? parent
                : root;
        }

        private static bool TryResolveHumanoidParent(
            HumanBodyBones humanBone,
            IReadOnlyDictionary<HumanBodyBones, Transform> boneMap,
            out Transform? parent)
        {
            parent = null;
            switch (humanBone)
            {
                case HumanBodyBones.Hips:
                    return false;
                case HumanBodyBones.Spine:
                    return boneMap.TryGetValue(HumanBodyBones.Hips, out parent);
                case HumanBodyBones.Chest:
                    return boneMap.TryGetValue(HumanBodyBones.Spine, out parent);
                case HumanBodyBones.Neck:
                    return TryResolveTorsoParent(boneMap, out parent);
                case HumanBodyBones.Head:
                    return boneMap.TryGetValue(HumanBodyBones.Neck, out parent);
                case HumanBodyBones.LeftEye:
                case HumanBodyBones.RightEye:
                    return boneMap.TryGetValue(HumanBodyBones.Head, out parent);
                case HumanBodyBones.LeftShoulder:
                case HumanBodyBones.RightShoulder:
                    return TryResolveTorsoParent(boneMap, out parent);
                case HumanBodyBones.LeftUpperArm:
                    return boneMap.TryGetValue(HumanBodyBones.LeftShoulder, out parent)
                           || TryResolveTorsoParent(boneMap, out parent);
                case HumanBodyBones.LeftLowerArm:
                    return boneMap.TryGetValue(HumanBodyBones.LeftUpperArm, out parent);
                case HumanBodyBones.LeftHand:
                    return boneMap.TryGetValue(HumanBodyBones.LeftLowerArm, out parent);
                case HumanBodyBones.RightUpperArm:
                    return boneMap.TryGetValue(HumanBodyBones.RightShoulder, out parent)
                           || TryResolveTorsoParent(boneMap, out parent);
                case HumanBodyBones.RightLowerArm:
                    return boneMap.TryGetValue(HumanBodyBones.RightUpperArm, out parent);
                case HumanBodyBones.RightHand:
                    return boneMap.TryGetValue(HumanBodyBones.RightLowerArm, out parent);
                case HumanBodyBones.LeftUpperLeg:
                case HumanBodyBones.RightUpperLeg:
                    return boneMap.TryGetValue(HumanBodyBones.Hips, out parent);
                case HumanBodyBones.LeftLowerLeg:
                    return boneMap.TryGetValue(HumanBodyBones.LeftUpperLeg, out parent);
                case HumanBodyBones.LeftFoot:
                    return boneMap.TryGetValue(HumanBodyBones.LeftLowerLeg, out parent);
                case HumanBodyBones.LeftToes:
                    return boneMap.TryGetValue(HumanBodyBones.LeftFoot, out parent);
                case HumanBodyBones.RightLowerLeg:
                    return boneMap.TryGetValue(HumanBodyBones.RightUpperLeg, out parent);
                case HumanBodyBones.RightFoot:
                    return boneMap.TryGetValue(HumanBodyBones.RightLowerLeg, out parent);
                case HumanBodyBones.RightToes:
                    return boneMap.TryGetValue(HumanBodyBones.RightFoot, out parent);
                case HumanBodyBones.LeftThumbProximal:
                case HumanBodyBones.LeftIndexProximal:
                case HumanBodyBones.LeftMiddleProximal:
                case HumanBodyBones.LeftRingProximal:
                case HumanBodyBones.LeftLittleProximal:
                    return boneMap.TryGetValue(HumanBodyBones.LeftHand, out parent);
                case HumanBodyBones.LeftThumbIntermediate:
                    return boneMap.TryGetValue(HumanBodyBones.LeftThumbProximal, out parent);
                case HumanBodyBones.LeftThumbDistal:
                    return boneMap.TryGetValue(HumanBodyBones.LeftThumbIntermediate, out parent);
                case HumanBodyBones.LeftIndexIntermediate:
                    return boneMap.TryGetValue(HumanBodyBones.LeftIndexProximal, out parent);
                case HumanBodyBones.LeftIndexDistal:
                    return boneMap.TryGetValue(HumanBodyBones.LeftIndexIntermediate, out parent);
                case HumanBodyBones.LeftMiddleIntermediate:
                    return boneMap.TryGetValue(HumanBodyBones.LeftMiddleProximal, out parent);
                case HumanBodyBones.LeftMiddleDistal:
                    return boneMap.TryGetValue(HumanBodyBones.LeftMiddleIntermediate, out parent);
                case HumanBodyBones.LeftRingIntermediate:
                    return boneMap.TryGetValue(HumanBodyBones.LeftRingProximal, out parent);
                case HumanBodyBones.LeftRingDistal:
                    return boneMap.TryGetValue(HumanBodyBones.LeftRingIntermediate, out parent);
                case HumanBodyBones.LeftLittleIntermediate:
                    return boneMap.TryGetValue(HumanBodyBones.LeftLittleProximal, out parent);
                case HumanBodyBones.LeftLittleDistal:
                    return boneMap.TryGetValue(HumanBodyBones.LeftLittleIntermediate, out parent);
                case HumanBodyBones.RightThumbProximal:
                case HumanBodyBones.RightIndexProximal:
                case HumanBodyBones.RightMiddleProximal:
                case HumanBodyBones.RightRingProximal:
                case HumanBodyBones.RightLittleProximal:
                    return boneMap.TryGetValue(HumanBodyBones.RightHand, out parent);
                case HumanBodyBones.RightThumbIntermediate:
                    return boneMap.TryGetValue(HumanBodyBones.RightThumbProximal, out parent);
                case HumanBodyBones.RightThumbDistal:
                    return boneMap.TryGetValue(HumanBodyBones.RightThumbIntermediate, out parent);
                case HumanBodyBones.RightIndexIntermediate:
                    return boneMap.TryGetValue(HumanBodyBones.RightIndexProximal, out parent);
                case HumanBodyBones.RightIndexDistal:
                    return boneMap.TryGetValue(HumanBodyBones.RightIndexIntermediate, out parent);
                case HumanBodyBones.RightMiddleIntermediate:
                    return boneMap.TryGetValue(HumanBodyBones.RightMiddleProximal, out parent);
                case HumanBodyBones.RightMiddleDistal:
                    return boneMap.TryGetValue(HumanBodyBones.RightMiddleIntermediate, out parent);
                case HumanBodyBones.RightRingIntermediate:
                    return boneMap.TryGetValue(HumanBodyBones.RightRingProximal, out parent);
                case HumanBodyBones.RightRingDistal:
                    return boneMap.TryGetValue(HumanBodyBones.RightRingIntermediate, out parent);
                case HumanBodyBones.RightLittleIntermediate:
                    return boneMap.TryGetValue(HumanBodyBones.RightLittleProximal, out parent);
                case HumanBodyBones.RightLittleDistal:
                    return boneMap.TryGetValue(HumanBodyBones.RightLittleIntermediate, out parent);
                default:
                    return false;
            }
        }

        private static bool TryResolveTorsoParent(
            IReadOnlyDictionary<HumanBodyBones, Transform> boneMap,
            out Transform? parent)
        {
            return boneMap.TryGetValue(HumanBodyBones.Chest, out parent)
                   || boneMap.TryGetValue(HumanBodyBones.Spine, out parent);
        }

        private static void ApplyHumanoidSiblingOrder(
            Transform parent,
            IReadOnlyDictionary<HumanBodyBones, Transform> boneMap)
        {
            int siblingIndex = 0;
            foreach (HumanBodyBones humanBone in HumanoidTraversalOrder)
            {
                if (boneMap.TryGetValue(humanBone, out Transform child)
                    && child.parent == parent)
                {
                    child.SetSiblingIndex(siblingIndex);
                    siblingIndex++;
                }
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                ApplyHumanoidSiblingOrder(parent.GetChild(i), boneMap);
            }
        }

        /// <summary>
        /// Build a Unity Avatar from an existing proxy rig result.
        /// This is an explicit operation separate from CreateProxyRig.
        /// Creates a HumanDescription from the proxy transforms and calls
        /// AvatarBuilder.BuildHumanAvatar.
        /// </summary>
        /// <param name="proxyRig">The result from a prior CreateProxyRig call.</param>
        /// <returns>
        /// An MmdHumanoidAvatarBuildResult containing the built Avatar (or null)
        /// and diagnostics. No Animator is created or modified.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when proxyRig is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when ProxyRoot is null (e.g., NoBones readiness).
        /// </exception>
        public static MmdHumanoidAvatarBuildResult BuildAvatar(
            MmdHumanoidProxyRigResult proxyRig)
        {
            if (proxyRig == null)
                throw new ArgumentNullException(nameof(proxyRig));

            if (proxyRig.ProxyRoot == null)
                throw new InvalidOperationException(
                    "Cannot build Avatar: proxy rig root is null (check readiness).");

            GameObject root = proxyRig.ProxyRoot;
            var diagnostics = new List<string>();

            if (!string.Equals(proxyRig.Readiness, MmdHumanoidSetupAsset.ReadyReadiness, StringComparison.Ordinal))
            {
                diagnostics.Add("avatar-build: skipped because proxy rig readiness is " + proxyRig.Readiness);
                diagnostics.AddRange(proxyRig.Diagnostics);
                return new MmdHumanoidAvatarBuildResult(null, diagnostics.ToArray());
            }

            // Build HumanDescription.
            int boneCount = proxyRig.BoneMap.Count;
            var humanDescription = new HumanDescription();
            ApplyGeometricTPose(proxyRig, diagnostics);

            // --- skeleton ---
            var skeletonList = new List<SkeletonBone>(boneCount + 1);

            // Root bone.
            skeletonList.Add(new SkeletonBone
            {
                name = root.name,
                position = root.transform.localPosition,
                rotation = root.transform.localRotation,
                scale = root.transform.localScale
            });

            var humanBoneList = new List<HumanBone>(boneCount);
            var humanBoneByTransform = new Dictionary<Transform, HumanBodyBones>(boneCount);
            var emittedHumanBones = new HashSet<HumanBodyBones>();
            foreach (MmdHumanoidBoneMappingMatch match in EnumerateMatchesInHumanoidOrder(proxyRig.Matches))
            {
                if (!proxyRig.BoneMap.TryGetValue(match.HumanBone, out Transform t))
                {
                    continue;
                }

                emittedHumanBones.Add(match.HumanBone);
                humanBoneByTransform[t] = match.HumanBone;
                humanBoneList.Add(new HumanBone
                {
                    humanName = HumanTrait.BoneName[(int)match.HumanBone],
                    boneName = t.name,
                    limit = { useDefaultValues = true }
                });
            }

            foreach (KeyValuePair<HumanBodyBones, Transform> kvp in proxyRig.BoneMap)
            {
                if (!emittedHumanBones.Add(kvp.Key))
                {
                    continue;
                }

                humanBoneByTransform[kvp.Value] = kvp.Key;
                humanBoneList.Add(new HumanBone
                {
                    humanName = HumanTrait.BoneName[(int)kvp.Key],
                    boneName = kvp.Value.name,
                    limit = { useDefaultValues = true }
                });
            }

            AddSkeletonBonesPreOrder(root.transform, humanBoneByTransform, skeletonList);

            humanDescription.human = humanBoneList.ToArray();
            humanDescription.skeleton = skeletonList.ToArray();

            // rootMotionBoneName: Hips if available, otherwise root name.
            bool hasHips = proxyRig.BoneMap.ContainsKey(HumanBodyBones.Hips);
            humanDescription.rootMotionBoneName = hasHips ? "Hips" : root.name;

            // Twist/stretch: use Unity documentation's default-safe values.
            // These are reasonable defaults for standard humanoid rigs.
            humanDescription.upperArmTwist = 0.5f;
            humanDescription.lowerArmTwist = 0.5f;
            humanDescription.upperLegTwist = 0.5f;
            humanDescription.lowerLegTwist = 0.5f;
            humanDescription.armStretch = 0.05f;
            humanDescription.legStretch = 0.05f;
            humanDescription.feetSpacing = 0.0f;
            humanDescription.hasTranslationDoF = false;

            diagnostics.Add("avatar-build: skeleton bones=" + skeletonList.Count +
                            " human bones=" + humanBoneList.Count +
                            " rootMotion=" + humanDescription.rootMotionBoneName);

            // Call AvatarBuilder.
            Avatar? avatar = null;
            try
            {
                avatar = AvatarBuilder.BuildHumanAvatar(root, humanDescription);
            }
            catch (Exception ex)
            {
                diagnostics.Add("avatar-build: exception from AvatarBuilder.BuildHumanAvatar: " +
                                ex.GetType().Name + ": " + ex.Message);
            }

            if (avatar == null)
            {
                diagnostics.Add("avatar-build: Avatar is null (BuildHumanAvatar returned null)");
            }
            else
            {
                diagnostics.Add("avatar-build: Avatar created, isValid=" +
                                avatar.isValid + " isHuman=" + avatar.isHuman);
                avatar.hideFlags = HideFlags.HideAndDontSave;
                diagnostics.Add("avatar-build: hideFlags set to HideAndDontSave");
            }

            return new MmdHumanoidAvatarBuildResult(avatar, diagnostics.ToArray());
        }

        internal static void ApplyGeometricTPose(
            MmdHumanoidProxyRigResult proxyRig,
            List<string> diagnostics)
        {
            if (proxyRig == null)
                throw new ArgumentNullException(nameof(proxyRig));

            if (diagnostics == null)
                throw new ArgumentNullException(nameof(diagnostics));

            ApplyGeometricArmTPose(
                proxyRig,
                diagnostics,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                Vector3.left);
            ApplyGeometricArmTPose(
                proxyRig,
                diagnostics,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                Vector3.right);
        }

        private static void ApplyGeometricArmTPose(
            MmdHumanoidProxyRigResult proxyRig,
            List<string> diagnostics,
            HumanBodyBones bone,
            HumanBodyBones childBone,
            Vector3 targetWorldDirection)
        {
            if (!proxyRig.BoneMap.TryGetValue(bone, out Transform boneTransform))
            {
                diagnostics.Add("avatar-build-tpose: skipped " + bone + " because bone is missing");
                return;
            }

            if (!proxyRig.BoneMap.TryGetValue(childBone, out Transform childTransform))
            {
                diagnostics.Add("avatar-build-tpose: skipped " + bone + " because child " + childBone + " is missing");
                return;
            }

            Vector3 currentDirection = childTransform.position - boneTransform.position;
            if (!IsFiniteVector3(currentDirection) || currentDirection.sqrMagnitude < 1e-8f)
            {
                diagnostics.Add("avatar-build-tpose: skipped " + bone + " because direction is invalid");
                return;
            }

            if (!IsFiniteVector3(targetWorldDirection) || targetWorldDirection.sqrMagnitude < 1e-8f)
            {
                diagnostics.Add("avatar-build-tpose: skipped " + bone + " because target direction is invalid");
                return;
            }

            Quaternion deltaWorld = Quaternion.FromToRotation(
                currentDirection.normalized,
                targetWorldDirection.normalized);
            Quaternion targetWorldRotation = deltaWorld * boneTransform.rotation;
            Transform? parent = boneTransform.parent;
            boneTransform.localRotation = parent != null
                ? Quaternion.Inverse(parent.rotation) * targetWorldRotation
                : targetWorldRotation;
            diagnostics.Add("avatar-build-tpose: applied " + bone + " toward " + targetWorldDirection);
        }

        private static void AddSkeletonBonesPreOrder(
            Transform parent,
            IReadOnlyDictionary<Transform, HumanBodyBones> humanBoneByTransform,
            List<SkeletonBone> skeletonList)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform t = parent.GetChild(i);
                if (!humanBoneByTransform.ContainsKey(t))
                {
                    continue;
                }

                skeletonList.Add(new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale
                });

                AddSkeletonBonesPreOrder(t, humanBoneByTransform, skeletonList);
            }
        }

        /// <summary>
        /// Convert an MMD-space origin float[] to a Unity-space Vector3 using [-x, y, -z].
        /// Returns Vector3.zero when origin is null or has fewer than 3 elements.
        /// </summary>
        internal static Vector3 MmdOriginToUnityPosition(float[]? origin)
        {
            if (origin == null || origin.Length < 3)
                return Vector3.zero;
            return new Vector3(-origin[0], origin[1], -origin[2]);
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        /// <summary>
        /// Find an MmdBoneDefinition by index in the model's bone list.
        /// </summary>
        private static MmdBoneDefinition? FindBoneByIndex(MmdModelDefinition model, int index)
        {
            foreach (MmdBoneDefinition b in model.bones)
            {
                if (b.index == index)
                    return b;
            }
            return null;
        }

        private static bool HasMappingOverrides(IReadOnlyList<MmdHumanoidBoneMappingOverride>? mappingOverrides)
        {
            return mappingOverrides != null && mappingOverrides.Count > 0;
        }

        private static string[] BuildModelBoneNameList(MmdModelDefinition model)
        {
            var names = new string[model.bones.Count];
            for (int i = 0; i < model.bones.Count; i++)
            {
                names[i] = model.bones[i]?.name ?? string.Empty;
            }

            return names;
        }

        private static int[] BuildModelBoneIndexList(MmdModelDefinition model)
        {
            var indices = new int[model.bones.Count];
            for (int i = 0; i < model.bones.Count; i++)
            {
                indices[i] = model.bones[i]?.index ?? i;
            }

            return indices;
        }

        private static List<MmdHumanoidBoneMappingMatch> ApplyMappingOverrides(
            List<MmdHumanoidBoneMappingMatch> automaticMatches,
            IReadOnlyList<string> sourceBoneNames,
            IReadOnlyList<int>? sourceBoneIndices,
            IReadOnlyList<MmdHumanoidBoneMappingOverride>? mappingOverrides,
            List<string> diagnostics,
            out int appliedOverrideCount)
        {
            var overrideMatches = new Dictionary<HumanBodyBones, MmdHumanoidBoneMappingMatch>();
            var overrideOrder = new List<HumanBodyBones>();
            int applied = 0;
            int ignored = 0;

            if (mappingOverrides != null)
            {
                for (int i = 0; i < mappingOverrides.Count; i++)
                {
                    MmdHumanoidBoneMappingOverride? mappingOverride = mappingOverrides[i];
                    if (mappingOverride == null)
                    {
                        ignored++;
                        diagnostics.Add("manual-override: ignored null entry at index " + i);
                        continue;
                    }

                    HumanBodyBones humanBone = mappingOverride.HumanBone;
                    if (humanBone == HumanBodyBones.LastBone || !Enum.IsDefined(typeof(HumanBodyBones), humanBone))
                    {
                        ignored++;
                        diagnostics.Add("manual-override: ignored invalid HumanBodyBones at index " + i);
                        continue;
                    }

                    string mmdBoneName = mappingOverride.MmdBoneName ?? string.Empty;
                    int sourceIndex = FindSourceBoneIndex(sourceBoneNames, mmdBoneName);
                    if (sourceIndex < 0)
                    {
                        ignored++;
                        diagnostics.Add("manual-override: ignored missing MMD bone '" + mmdBoneName + "' for " + humanBone);
                        continue;
                    }

                    int mmdBoneIndex = ResolveSourceBoneIndex(sourceBoneIndices, sourceIndex);
                    if (!overrideMatches.ContainsKey(humanBone))
                    {
                        overrideOrder.Add(humanBone);
                    }

                    overrideMatches[humanBone] = new MmdHumanoidBoneMappingMatch(
                        humanBone,
                        sourceBoneNames[sourceIndex],
                        mmdBoneIndex,
                        MmdHumanoidBoneMappingEvaluator.IsRequiredHumanBone(humanBone));
                    applied++;
                }
            }

            var merged = new List<MmdHumanoidBoneMappingMatch>();
            var usedHumanBones = new HashSet<HumanBodyBones>();
            var usedSourceBoneIndices = new HashSet<int>();

            foreach (HumanBodyBones humanBone in overrideOrder)
            {
                if (overrideMatches.TryGetValue(humanBone, out MmdHumanoidBoneMappingMatch match))
                {
                    AddMergedMatch(match, merged, usedHumanBones, usedSourceBoneIndices, diagnostics, "manual-override");
                }
            }

            foreach (MmdHumanoidBoneMappingMatch match in automaticMatches)
            {
                if (overrideMatches.ContainsKey(match.HumanBone))
                {
                    continue;
                }

                AddMergedMatch(match, merged, usedHumanBones, usedSourceBoneIndices, diagnostics, "automatic");
            }

            diagnostics.Add("manual-overrides: applied=" + applied + " ignored=" + ignored);
            appliedOverrideCount = applied;
            return merged;
        }

        private static void AddMergedMatch(
            MmdHumanoidBoneMappingMatch match,
            List<MmdHumanoidBoneMappingMatch> merged,
            HashSet<HumanBodyBones> usedHumanBones,
            HashSet<int> usedSourceBoneIndices,
            List<string> diagnostics,
            string source)
        {
            if (!usedHumanBones.Add(match.HumanBone))
            {
                diagnostics.Add(source + ": skipped duplicate HumanBodyBones " + match.HumanBone);
                return;
            }

            if (!usedSourceBoneIndices.Add(match.MmdBoneIndex))
            {
                diagnostics.Add(source + ": skipped duplicate MMD bone '" + match.MmdBoneName + "'#" + match.MmdBoneIndex);
                return;
            }

            merged.Add(match);
        }

        private static int FindSourceBoneIndex(IReadOnlyList<string> sourceBoneNames, string mmdBoneName)
        {
            if (sourceBoneNames == null || string.IsNullOrWhiteSpace(mmdBoneName))
            {
                return -1;
            }

            string normalized = mmdBoneName.Trim();
            for (int i = 0; i < sourceBoneNames.Count; i++)
            {
                if (string.Equals((sourceBoneNames[i] ?? string.Empty).Trim(), normalized, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int ResolveSourceBoneIndex(IReadOnlyList<int>? sourceBoneIndices, int sourceListIndex)
        {
            if (sourceBoneIndices == null || sourceListIndex < 0 || sourceListIndex >= sourceBoneIndices.Count)
            {
                return sourceListIndex;
            }

            return sourceBoneIndices[sourceListIndex];
        }

        private static string ClassifyMergedReadiness(IReadOnlyList<MmdHumanoidBoneMappingMatch> matches, int sourceBoneCount)
        {
            if (sourceBoneCount == 0)
            {
                return MmdHumanoidSetupAsset.NoBonesReadiness;
            }

            int requiredMapped = 0;
            var requiredBones = new HashSet<HumanBodyBones>();
            foreach (MmdHumanoidBoneMappingMatch match in matches)
            {
                if (MmdHumanoidBoneMappingEvaluator.IsRequiredHumanBone(match.HumanBone)
                    && requiredBones.Add(match.HumanBone))
                {
                    requiredMapped++;
                }
            }

            return requiredMapped >= MmdHumanoidBoneMappingEvaluator.RequiredHumanBoneCount
                ? MmdHumanoidSetupAsset.ReadyReadiness
                : MmdHumanoidSetupAsset.MissingRequiredReadiness;
        }
    }
}
