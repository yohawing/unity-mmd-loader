#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Mmd.Parser;

namespace Mmd
{
    /// <summary>
    /// Result of creating a hidden Humanoid proxy rig from an MMD model definition.
    /// The proxy rig is a Transform hierarchy matching mapped HumanBodyBones only.
    /// Transform localPosition is derived from MmdBoneDefinition.origin with
    /// MMD->Unity conversion [-x, y, -z]. No Avatar, Animator, or mapping assets
    /// are created.
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
    /// Creates a hidden Unity Transform hierarchy that mirrors the HumanBodyBones
    /// detected from an MMD model's bone list. No Avatar, Animator, or mapping assets
    /// are created.
    ///
    /// Transform localPosition is set from MmdBoneDefinition.origin with
    /// MMD->Unity conversion [-x, y, -z]. When the parent bone is also mapped,
    /// localPosition is parent-relative (origin child minus origin parent in Unity space).
    /// Bones with null or short origin arrays fall back to Vector3.zero.
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
            string proxyRootName = "MmdHumanoidProxyRig")
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

            // Step 3: Build the proxy Transform hierarchy.
            // We build a hierarchy that mirrors the parent-child relationships of the
            // MMD bones, but only includes bones that were successfully mapped.
            var boneMap = new Dictionary<HumanBodyBones, Transform>();
            var mmdBoneIndexToTransform = new Dictionary<int, Transform>();
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
                mmdBoneIndexToTransform[match.MmdBoneIndex] = boneTransform;

                // Record Unity-space origin position for localPosition calculation.
                MmdBoneDefinition? boneDef = FindBoneByIndex(model, match.MmdBoneIndex);
                Vector3 unityPos = MmdOriginToUnityPosition(boneDef?.origin);
                mmdBoneIndexToUnityPos[match.MmdBoneIndex] = unityPos;
            }

            // Second pass: set parent-child relationships based on MMD bone parentIndex
            // and compute parent-relative localPosition from origin.
            foreach (MmdHumanoidBoneMappingMatch match in matches)
            {
                if (!boneMap.TryGetValue(match.HumanBone, out Transform currentTransform))
                    continue;

                MmdBoneDefinition? boneDef = FindBoneByIndex(model, match.MmdBoneIndex);
                if (boneDef == null)
                    continue;

                // Compute localPosition from origin.
                Vector3 myUnityPos = mmdBoneIndexToUnityPos[match.MmdBoneIndex];

                if (boneDef.parentIndex >= 0 &&
                    mmdBoneIndexToTransform.TryGetValue(boneDef.parentIndex, out Transform parentTransform))
                {
                    // Reparent under the mapped parent.
                    currentTransform.SetParent(parentTransform, worldPositionStays: false);
                    // Parent-relative: child origin minus parent origin in Unity space.
                    Vector3 parentUnityPos = mmdBoneIndexToUnityPos[boneDef.parentIndex];
                    currentTransform.localPosition = myUnityPos - parentUnityPos;
                }
                else
                {
                    // No mapped parent; position relative to root (origin directly).
                    currentTransform.localPosition = myUnityPos;
                }
            }

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
            var combinedDiagnostics = new List<string>();
            combinedDiagnostics.AddRange(report.Diagnostics);
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
            string proxyRootName = "MmdHumanoidProxyRig")
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
                    proxyRootName);
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
        /// <param name="hierarchyRoot">Hierarchy root transform used when no mapped parent exists.</param>
        /// <param name="proxyRootName">Name for the proxy root GameObject.</param>
        /// <returns>A result containing the proxy rig root and related mapping info.</returns>
        internal static MmdHumanoidProxyRigResult CreateProxyRigFromBoneTransforms(
            IReadOnlyList<Transform> bones,
            Transform hierarchyRoot,
            string proxyRootName = "MmdHumanoidProxyRig")
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
            var indexByTransform = new Dictionary<Transform, int>(bones.Count);
            for (int i = 0; i < bones.Count; i++)
            {
                Transform? bone = bones[i];
                sourceBoneNames[i] = bone != null ? bone.name : string.Empty;
                if (bone != null)
                {
                    indexByTransform[bone] = i;
                }
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

            if (report.Readiness == MmdHumanoidSetupAsset.NoBonesReadiness)
            {
                return new MmdHumanoidProxyRigResult(
                    null,
                    null,
                    matches.AsReadOnly(),
                    MmdHumanoidSetupAsset.NoBonesReadiness,
                    new[]
                    {
                        "proxy-rig: input=ImportedHierarchy",
                        "proxy-rig: " + report.Readiness,
                    });
            }

            var boneMap = new Dictionary<HumanBodyBones, Transform>();
            var proxyBySourceIndex = new Dictionary<int, Transform>(bones.Count);
            var root = new GameObject(proxyRootName);
            root.hideFlags = HideFlags.HideInHierarchy;
            root.SetActive(false);
            root.transform.SetPositionAndRotation(hierarchyRoot.position, hierarchyRoot.rotation);
            root.transform.localScale = hierarchyRoot.localScale;

            foreach (MmdHumanoidBoneMappingMatch match in matches)
            {
                Transform sourceBone = bones[match.MmdBoneIndex];
                var proxyBone = new GameObject(match.HumanBone.ToString());
                proxyBone.hideFlags = HideFlags.HideInHierarchy;
                Transform proxyBoneTransform = proxyBone.transform;
                proxyBoneTransform.SetParent(root.transform, worldPositionStays: false);
                boneMap[match.HumanBone] = proxyBoneTransform;
                proxyBySourceIndex[match.MmdBoneIndex] = proxyBoneTransform;
            }

            foreach (MmdHumanoidBoneMappingMatch match in matches)
            {
                Transform sourceBone = bones[match.MmdBoneIndex];
                if (!boneMap.TryGetValue(match.HumanBone, out Transform proxyBoneTransform))
                {
                    continue;
                }

                Transform? mappedParent = null;
                for (Transform parent = sourceBone.parent; parent != null; parent = parent.parent)
                {
                    if (parent == hierarchyRoot)
                    {
                        break;
                    }

                    if (indexByTransform.TryGetValue(parent, out int parentIndex)
                        && proxyBySourceIndex.TryGetValue(parentIndex, out Transform parentProxy))
                    {
                        mappedParent = parentProxy;
                        break;
                    }
                }

                if (mappedParent != null)
                {
                    proxyBoneTransform.SetParent(mappedParent, worldPositionStays: false);
                }
                else
                {
                    proxyBoneTransform.SetParent(root.transform, worldPositionStays: false);
                }
            }

            foreach (MmdHumanoidBoneMappingMatch match in matches)
            {
                Transform sourceBone = bones[match.MmdBoneIndex];
                if (!boneMap.TryGetValue(match.HumanBone, out Transform proxyBoneTransform))
                {
                    continue;
                }

                proxyBoneTransform.SetPositionAndRotation(sourceBone.position, sourceBone.rotation);
                proxyBoneTransform.localScale = sourceBone.localScale;
            }

            var diagnostics = new List<string> { "proxy-rig: input=ImportedHierarchy" };
            diagnostics.AddRange(report.Diagnostics);
            diagnostics.Add(
                "proxy-rig: created " + boneMap.Count + " bone transforms, root hidden");

            return new MmdHumanoidProxyRigResult(
                root,
                boneMap,
                matches,
                report.Readiness,
                diagnostics.ToArray());
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

            // Proxy bone transforms, preserving local transforms including origin positions.
            var humanBoneList = new List<HumanBone>(boneCount);
            foreach (KeyValuePair<HumanBodyBones, Transform> kvp in proxyRig.BoneMap)
            {
                string humanName = kvp.Key.ToString();
                Transform t = kvp.Value;

                humanBoneList.Add(new HumanBone
                {
                    humanName = humanName,
                    boneName = t.name,
                    limit = { useDefaultValues = true }
                });

                skeletonList.Add(new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale
                });
            }

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
    }
}
