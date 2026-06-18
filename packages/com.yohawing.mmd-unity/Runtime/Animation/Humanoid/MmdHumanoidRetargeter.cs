#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Yohawing.MmdUnity.UnityIntegration;

namespace Yohawing.MmdUnity
{
    /// <summary>
    /// Result of a single MmdHumanoidRetargeter.RetargetPose call.
    /// Records copied bone count, skipped bones with reasons, diagnostics,
    /// and an overall success-ish flag.
    /// </summary>
    public sealed class MmdHumanoidRetargeterResult
    {
        public MmdHumanoidRetargeterResult(
            int copiedBoneCount,
            int skippedBoneCount,
            IReadOnlyList<string> diagnostics)
        {
            CopiedBoneCount = copiedBoneCount;
            SkippedBoneCount = skippedBoneCount;
            Diagnostics = diagnostics ?? Array.Empty<string>();
        }

        /// <summary>
        /// Number of bones whose localRotation was successfully copied.
        /// </summary>
        public int CopiedBoneCount { get; }

        /// <summary>
        /// Number of bones skipped due to validation failures.
        /// </summary>
        public int SkippedBoneCount { get; }

        /// <summary>
        /// Human-readable diagnostics from the retargeting pass.
        /// </summary>
        public IReadOnlyList<string> Diagnostics { get; }

        /// <summary>
        /// True when every matched bone was successfully copied (skipped count is zero
        /// and at least one bone was attempted). False when any match was skipped or
        /// when no matches were available.
        /// </summary>
        public bool AllSucceeded => SkippedBoneCount == 0 && CopiedBoneCount > 0;
    }

    /// <summary>
    /// Minimal runtime retargeter that copies Humanoid proxy rig local rotations back
    /// to the native MMD skeleton (MmdUnityModelInstance.BoneTransforms).
    ///
    /// This slice only copies localRotation. localPosition is NOT copied because proxy
    /// origins are unscaled MMD-space positions while native bind positions are
    /// import-scale aware. Position copy is deferred to a later slice that can reconcile
    /// the scale difference.
    ///
    /// Design: Dual Rig 方式の rotation-only retargeting を担当する。
    /// MmdHumanoidProxyRigResult の Matches + BoneMap を読み取り、対応する
    /// MmdUnityModelInstance.BoneTransforms[match.MmdBoneIndex] の localRotation を
    /// 書き換える。position は変更しない。Animator, Avatar, AssetDatabase,
    /// Timeline, AnimationClip には一切触れない。
    /// </summary>
    public static class MmdHumanoidRetargeter
    {
        /// <summary>
        /// Retarget proxy rig local rotations to the native MMD skeleton.
        /// Only localRotation is copied; localPosition is left unchanged
        /// (deferred to a later scale-aware slice).
        /// </summary>
        /// <param name="proxyRig">
        /// Result from a prior MmdHumanoidProxyRigFactory.CreateProxyRig call.
        /// Must have a non-null ProxyRoot and Matches with at least one entry.
        /// </param>
        /// <param name="modelInstance">
        /// The MmdUnityModelInstance whose BoneTransforms receive the retargeted rotations.
        /// </param>
        /// <returns>
        /// An MmdHumanoidRetargeterResult with copied count, skipped count, diagnostics,
        /// and a success-ish flag.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when proxyRig or modelInstance is null.</exception>
        public static MmdHumanoidRetargeterResult RetargetPose(
            MmdHumanoidProxyRigResult proxyRig,
            MmdUnityModelInstance modelInstance)
        {
            if (proxyRig == null)
                throw new ArgumentNullException(nameof(proxyRig));
            if (modelInstance == null)
                throw new ArgumentNullException(nameof(modelInstance));

            var diagnostics = new List<string>();
            int copiedCount = 0;
            int skippedCount = 0;

            // If the proxy rig has no root, no proxy transforms exist to read from.
            if (proxyRig.ProxyRoot == null)
            {
                diagnostics.Add("retarget: skipped because ProxyRoot is null");
                return new MmdHumanoidRetargeterResult(
                    copiedCount,
                    skippedCount + 1,
                    diagnostics.ToArray());
            }

            IReadOnlyList<MmdHumanoidBoneMappingMatch> matches = proxyRig.Matches;
            if (matches == null || matches.Count == 0)
            {
                diagnostics.Add("retarget: no bone matches to retarget");
                return new MmdHumanoidRetargeterResult(
                    copiedCount,
                    skippedCount,
                    diagnostics.ToArray());
            }

            IReadOnlyDictionary<HumanBodyBones, Transform> boneMap = proxyRig.BoneMap;
            Transform[] nativeTransforms = modelInstance.BoneTransforms;
            int nativeCount = nativeTransforms.Length;

            // Track which native MMD bone indices we've already written to,
            // to detect duplicate target indices.
            var usedNativeIndices = new HashSet<int>();

            foreach (MmdHumanoidBoneMappingMatch match in matches)
            {
                // Validate MmdBoneIndex is in range.
                if (match.MmdBoneIndex < 0 || match.MmdBoneIndex >= nativeCount)
                {
                    diagnostics.Add(
                        $"retarget: skipped MmdBoneIndex {match.MmdBoneIndex} " +
                        $"(HumanBone={match.HumanBone}, MmdBoneName='{match.MmdBoneName}'): " +
                        $"out of range [0, {nativeCount})");
                    skippedCount++;
                    continue;
                }

                // Detect duplicate target MMD bone indices.
                if (!usedNativeIndices.Add(match.MmdBoneIndex))
                {
                    diagnostics.Add(
                        $"retarget: skipped duplicate target MmdBoneIndex {match.MmdBoneIndex} " +
                        $"(HumanBone={match.HumanBone}, MmdBoneName='{match.MmdBoneName}')");
                    skippedCount++;
                    continue;
                }

                // Get the proxy Transform for this HumanBodyBones.
                // Use ContainsKey + indexer as TryGetValue is not available on IReadOnlyDictionary
                // in this project's C# version. Check via ContainsKey first.
                bool hasProxy = boneMap.TryGetValue(match.HumanBone, out Transform proxyTransform);
                if (!hasProxy || proxyTransform == null)
                {
                    diagnostics.Add(
                        $"retarget: skipped missing proxy transform for {match.HumanBone} " +
                        $"(MmdBoneName='{match.MmdBoneName}')");
                    skippedCount++;
                    continue;
                }

                // Validate that the proxy transform's localRotation is finite (non-NaN, non-inf).
                Quaternion proxyRot = proxyTransform.localRotation;
                if (!IsFiniteQuaternion(proxyRot))
                {
                    diagnostics.Add(
                        $"retarget: skipped non-finite proxy rotation for {match.HumanBone} " +
                        $"(MmdBoneName='{match.MmdBoneName}'): " +
                        $"rotation=({proxyRot.x}, {proxyRot.y}, {proxyRot.z}, {proxyRot.w})");
                    skippedCount++;
                    continue;
                }

                // Copy localRotation only. localPosition is NOT copied.
                Transform nativeTransform = nativeTransforms[match.MmdBoneIndex];
                nativeTransform.localRotation = proxyRot;
                copiedCount++;
            }

            diagnostics.Add(
                $"retarget: copied={copiedCount} skipped={skippedCount} total-matches={matches.Count}");

            return new MmdHumanoidRetargeterResult(
                copiedCount,
                skippedCount,
                diagnostics.ToArray());
        }

        /// <summary>
        /// Returns true when the quaternion has all finite components (no NaN, no infinity).
        /// </summary>
        internal static bool IsFiniteQuaternion(Quaternion q)
        {
            return float.IsFinite(q.x) &&
                   float.IsFinite(q.y) &&
                   float.IsFinite(q.z) &&
                   float.IsFinite(q.w);
        }
    }
}
