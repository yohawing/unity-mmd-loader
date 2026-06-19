#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Mmd.UnityIntegration;

namespace Mmd
{
    [Serializable]
    public sealed class MmdHumanoidRetargetBinding
    {
        [SerializeField] private HumanBodyBones humanBone = HumanBodyBones.LastBone;
        [SerializeField] private int mmdBoneIndex = -1;
        [SerializeField] private Transform? proxyTransform;
        [SerializeField] private Transform? nativeTransform;

        public MmdHumanoidRetargetBinding()
        {
        }

        public MmdHumanoidRetargetBinding(
            HumanBodyBones humanBone,
            int mmdBoneIndex,
            Transform? proxyTransform,
            Transform? nativeTransform)
        {
            this.humanBone = humanBone;
            this.mmdBoneIndex = mmdBoneIndex;
            this.proxyTransform = proxyTransform;
            this.nativeTransform = nativeTransform;
        }

        public HumanBodyBones HumanBone => humanBone;

        public int MmdBoneIndex => mmdBoneIndex;

        public Transform? ProxyTransform => proxyTransform;

        public Transform? NativeTransform => nativeTransform;
    }

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

            // If the proxy rig has no root, no proxy transforms exist to read from.
            if (proxyRig.ProxyRoot == null)
            {
                var diagnostics = new List<string>();
                diagnostics.Add("retarget: skipped because ProxyRoot is null");
                return new MmdHumanoidRetargeterResult(
                    0,
                    1,
                    diagnostics.ToArray());
            }

            IReadOnlyList<MmdHumanoidBoneMappingMatch> matches = proxyRig.Matches;
            if (matches == null || matches.Count == 0)
            {
                var diagnostics = new List<string>();
                diagnostics.Add("retarget: no bone matches to retarget");
                return new MmdHumanoidRetargeterResult(
                    0,
                    0,
                    diagnostics.ToArray());
            }

            IReadOnlyDictionary<HumanBodyBones, Transform> boneMap = proxyRig.BoneMap;
            Transform[] nativeTransforms = modelInstance.BoneTransforms;
            var entries = new List<MmdHumanoidRetargetBinding>(matches.Count);
            foreach (MmdHumanoidBoneMappingMatch match in matches)
            {
                boneMap.TryGetValue(match.HumanBone, out Transform proxyTransform);
                Transform? nativeTransform = match.MmdBoneIndex >= 0 && match.MmdBoneIndex < nativeTransforms.Length
                    ? nativeTransforms[match.MmdBoneIndex]
                    : null;
                entries.Add(new MmdHumanoidRetargetBinding(
                    match.HumanBone,
                    match.MmdBoneIndex,
                    proxyTransform,
                    nativeTransform));
            }

            return RetargetPose(entries);
        }

        public static MmdHumanoidRetargeterResult RetargetPose(
            IReadOnlyList<MmdHumanoidRetargetBinding> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            var diagnostics = new List<string>();
            int copiedCount = 0;
            int skippedCount = 0;

            if (entries.Count == 0)
            {
                diagnostics.Add("retarget: no bone matches to retarget");
                return new MmdHumanoidRetargeterResult(
                    copiedCount,
                    skippedCount,
                    diagnostics.ToArray());
            }

            // Track which native MMD bone indices we've already written to,
            // to detect duplicate target indices.
            var usedNativeIndices = new HashSet<int>();

            foreach (MmdHumanoidRetargetBinding entry in entries)
            {
                if (entry.MmdBoneIndex < 0)
                {
                    diagnostics.Add(
                        $"retarget: skipped MmdBoneIndex {entry.MmdBoneIndex} " +
                        $"(HumanBone={entry.HumanBone}): invalid negative index");
                    skippedCount++;
                    continue;
                }

                // Detect duplicate target MMD bone indices.
                if (!usedNativeIndices.Add(entry.MmdBoneIndex))
                {
                    diagnostics.Add(
                        $"retarget: skipped duplicate target MmdBoneIndex {entry.MmdBoneIndex} " +
                        $"(HumanBone={entry.HumanBone})");
                    skippedCount++;
                    continue;
                }

                Transform? proxyTransform = entry.ProxyTransform;
                if (proxyTransform == null)
                {
                    diagnostics.Add(
                        $"retarget: skipped missing proxy transform for {entry.HumanBone}");
                    skippedCount++;
                    continue;
                }

                Transform? nativeTransform = entry.NativeTransform;
                if (nativeTransform == null)
                {
                    diagnostics.Add(
                        $"retarget: skipped MmdBoneIndex {entry.MmdBoneIndex} " +
                        $"(HumanBone={entry.HumanBone}): native transform is null or out of range");
                    skippedCount++;
                    continue;
                }

                // Validate that the proxy transform's localRotation is finite (non-NaN, non-inf).
                Quaternion proxyRot = proxyTransform.localRotation;
                if (!IsFiniteQuaternion(proxyRot))
                {
                    diagnostics.Add(
                        $"retarget: skipped non-finite proxy rotation for {entry.HumanBone}: " +
                        $"rotation=({proxyRot.x}, {proxyRot.y}, {proxyRot.z}, {proxyRot.w})");
                    skippedCount++;
                    continue;
                }

                // Copy localRotation only. localPosition is NOT copied.
                nativeTransform.localRotation = proxyRot;
                copiedCount++;
            }

            diagnostics.Add(
                $"retarget: copied={copiedCount} skipped={skippedCount} total-matches={entries.Count}");

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
