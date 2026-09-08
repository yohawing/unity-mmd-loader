#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Mmd.UnityIntegration;
using Mmd.Parser;

namespace Mmd
{
    [Serializable]
    public sealed class MmdHumanoidRetargetBinding
    {
        [SerializeField] private HumanBodyBones humanBone = HumanBodyBones.LastBone;
        [SerializeField] private int mmdBoneIndex = -1;
        [SerializeField] private Transform? proxyTransform;
        [SerializeField] private Transform? nativeTransform;
        [SerializeField] private Quaternion proxyBindLocalRotation = Quaternion.identity;
        [SerializeField] private Quaternion nativeBindLocalRotation = Quaternion.identity;
        [SerializeField] private bool copyLocalPosition;
        [SerializeField] private Transform? translationTargetTransform;
        [SerializeField] private int translationTargetMmdBoneIndex = -1;
        [SerializeField] private Vector3 proxyBindLocalPosition;
        [SerializeField] private Vector3 translationTargetBindLocalPosition;

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

        public MmdHumanoidRetargetBinding(
            HumanBodyBones humanBone,
            int mmdBoneIndex,
            Transform? proxyTransform,
            Transform? nativeTransform,
            Quaternion proxyBindLocalRotation,
            Quaternion nativeBindLocalRotation)
            : this(humanBone, mmdBoneIndex, proxyTransform, nativeTransform)
        {
            this.proxyBindLocalRotation = proxyBindLocalRotation;
            this.nativeBindLocalRotation = nativeBindLocalRotation;
        }

        public MmdHumanoidRetargetBinding(
            HumanBodyBones humanBone,
            int mmdBoneIndex,
            Transform? proxyTransform,
            Transform? nativeTransform,
            bool copyLocalPosition,
            Transform? translationTargetTransform,
            int translationTargetMmdBoneIndex,
            Vector3 proxyBindLocalPosition,
            Vector3 translationTargetBindLocalPosition)
            : this(
                humanBone,
                mmdBoneIndex,
                proxyTransform,
                nativeTransform,
                Quaternion.identity,
                Quaternion.identity,
                copyLocalPosition,
                translationTargetTransform,
                translationTargetMmdBoneIndex,
                proxyBindLocalPosition,
                translationTargetBindLocalPosition)
        {
        }

        public MmdHumanoidRetargetBinding(
            HumanBodyBones humanBone,
            int mmdBoneIndex,
            Transform? proxyTransform,
            Transform? nativeTransform,
            Quaternion proxyBindLocalRotation,
            Quaternion nativeBindLocalRotation,
            bool copyLocalPosition,
            Transform? translationTargetTransform,
            int translationTargetMmdBoneIndex,
            Vector3 proxyBindLocalPosition,
            Vector3 translationTargetBindLocalPosition)
            : this(
                humanBone,
                mmdBoneIndex,
                proxyTransform,
                nativeTransform,
                proxyBindLocalRotation,
                nativeBindLocalRotation)
        {
            this.copyLocalPosition = copyLocalPosition;
            this.translationTargetTransform = translationTargetTransform;
            this.translationTargetMmdBoneIndex = translationTargetMmdBoneIndex;
            this.proxyBindLocalPosition = proxyBindLocalPosition;
            this.translationTargetBindLocalPosition = translationTargetBindLocalPosition;
        }

        public HumanBodyBones HumanBone => humanBone;

        public int MmdBoneIndex => mmdBoneIndex;

        public Transform? ProxyTransform => proxyTransform;

        public Transform? NativeTransform => nativeTransform;

        public Quaternion ProxyBindLocalRotation => proxyBindLocalRotation;

        public Quaternion NativeBindLocalRotation => nativeBindLocalRotation;

        public bool CopyLocalPosition => copyLocalPosition;

        public Transform? TranslationTargetTransform => translationTargetTransform;

        public int TranslationTargetMmdBoneIndex => translationTargetMmdBoneIndex;

        public Vector3 ProxyBindLocalPosition => proxyBindLocalPosition;

        public Vector3 TranslationTargetBindLocalPosition => translationTargetBindLocalPosition;
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
            : this(
                copiedBoneCount,
                skippedBoneCount,
                copiedTranslationCount: 0,
                skippedTranslationCount: 0,
                diagnostics)
        {
        }

        public MmdHumanoidRetargeterResult(
            int copiedBoneCount,
            int skippedBoneCount,
            int copiedTranslationCount,
            int skippedTranslationCount,
            IReadOnlyList<string> diagnostics)
        {
            CopiedBoneCount = copiedBoneCount;
            SkippedBoneCount = skippedBoneCount;
            CopiedTranslationCount = copiedTranslationCount;
            SkippedTranslationCount = skippedTranslationCount;
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
        /// Number of localPosition channels successfully copied through translation bindings.
        /// </summary>
        public int CopiedTranslationCount { get; }

        /// <summary>
        /// Number of requested translation bindings skipped due to validation failures.
        /// </summary>
        public int SkippedTranslationCount { get; }

        /// <summary>
        /// Human-readable diagnostics from the retargeting pass.
        /// </summary>
        public IReadOnlyList<string> Diagnostics { get; }

        /// <summary>
        /// True when every requested retarget operation was successfully copied (skipped counts are zero
        /// and at least one bone was attempted). False when any match was skipped or
        /// when no matches were available.
        /// </summary>
        public bool AllSucceeded => SkippedBoneCount == 0 && SkippedTranslationCount == 0 && CopiedBoneCount > 0;
    }

    /// <summary>
    /// Minimal runtime retargeter that copies Humanoid proxy rig local rotations back
    /// to the native MMD skeleton (MmdUnityModelInstance.BoneTransforms).
    ///
    /// By default this copies localRotation only. Entries can opt in to Hips/body
    /// localPosition delta copy by carrying captured proxy/native bind local positions.
    ///
    /// Design: Dual Rig 方式の retargeting を担当する。
    /// MmdHumanoidProxyRigResult の Matches + BoneMap を読み取り、対応する
    /// MmdUnityModelInstance.BoneTransforms[match.MmdBoneIndex] の localRotation を
    /// 書き換える。position は変更しない。Animator, Avatar, AssetDatabase,
    /// Timeline, AnimationClip には一切触れない。
    /// </summary>
    public static class MmdHumanoidRetargeter
    {
        /// <summary>
        /// Retarget proxy rig local rotations to the native MMD skeleton.
        /// localRotation is always the primary copy path. localPosition is copied only
        /// for bindings that explicitly opt in through CopyLocalPosition.
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
            IReadOnlyList<MmdHumanoidRetargetBinding> entries,
            bool copyLocalPositions = true)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            return RetargetPoseCore(entries, copyLocalPositions);
        }

        /// <summary>
        /// Retargets directly into the native host-pose input arrays. The arrays are rebuilt from
        /// a fresh rest pose on every call, so the previous display output is never sampled as input.
        /// </summary>
        /// <remarks>
        /// <paramref name="nativeTransforms"/> is used to validate that every configured target
        /// still points at the model bone represented by its MMD index. The managed Transform
        /// retarget path remains available as the fallback when this validation or native
        /// evaluation cannot be completed.
        /// </remarks>
        internal static MmdHumanoidRetargeterResult RetargetPoseToNativeArrays(
            IReadOnlyList<MmdHumanoidRetargetBinding> entries,
            MmdModelDefinition model,
            IReadOnlyList<Transform> nativeTransforms,
            float importScale,
            float[] localPositionOffsetsXyz,
            float[] localRotationXyzw,
            float[] localScalesXyz,
            bool copyLocalPositions = true)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (nativeTransforms == null)
                throw new ArgumentNullException(nameof(nativeTransforms));
            if (localPositionOffsetsXyz == null)
                throw new ArgumentNullException(nameof(localPositionOffsetsXyz));
            if (localRotationXyzw == null)
                throw new ArgumentNullException(nameof(localRotationXyzw));
            if (localScalesXyz == null)
                throw new ArgumentNullException(nameof(localScalesXyz));

            if (model.bones == null)
                throw new ArgumentException("Model bones are required.", nameof(model));

            int boneCount = model.bones.Count;
            RequireExactArrayLength(localPositionOffsetsXyz, checked(boneCount * 3), nameof(localPositionOffsetsXyz));
            RequireExactArrayLength(localRotationXyzw, checked(boneCount * 4), nameof(localRotationXyzw));
            RequireExactArrayLength(localScalesXyz, checked(boneCount * 3), nameof(localScalesXyz));
            if (nativeTransforms.Count != boneCount)
                throw new ArgumentException(
                    $"Native transform count {nativeTransforms.Count} does not match model bone count {boneCount}.",
                    nameof(nativeTransforms));

            ValidateNativeArrayBoneMapping(model, nativeTransforms);
            ResetNativeArrayPose(localPositionOffsetsXyz, localRotationXyzw, localScalesXyz);

            float scale = NormalizeImportScale(importScale);
            return RetargetPoseCore(
                entries,
                copyLocalPositions,
                new NativeArrayOutput(
                    model,
                    nativeTransforms,
                    scale,
                    localPositionOffsetsXyz,
                    localRotationXyzw));
        }

        private static MmdHumanoidRetargeterResult RetargetPoseCore(
            IReadOnlyList<MmdHumanoidRetargetBinding> entries,
            bool copyLocalPositions,
            NativeArrayOutput? nativeArrayOutput = null)
        {

            var diagnostics = new List<string>();
            int copiedCount = 0;
            int skippedCount = 0;
            int copiedTranslationCount = 0;
            int skippedTranslationCount = 0;

            if (entries.Count == 0)
            {
                diagnostics.Add("retarget: no bone matches to retarget");
                return new MmdHumanoidRetargeterResult(
                    copiedCount,
                    skippedCount,
                    diagnostics.ToArray());
            }

            // Track which native MMD bone indices we've already written to,
            // to detect duplicate rotation and translation target indices independently.
            var usedRotationNativeIndices = new HashSet<int>();
            var usedTranslationNativeIndices = new HashSet<int>();

            foreach (MmdHumanoidRetargetBinding entry in entries)
            {
                Transform? proxyTransform = entry.ProxyTransform;
                if (proxyTransform == null)
                {
                    diagnostics.Add(
                        $"retarget: skipped missing proxy transform for {entry.HumanBone}");
                    skippedCount++;
                    if (entry.CopyLocalPosition)
                    {
                        diagnostics.Add(
                            $"retarget: skipped translation for {entry.HumanBone}: proxy transform is null");
                        skippedTranslationCount++;
                    }

                    continue;
                }

                if (entry.MmdBoneIndex < 0)
                {
                    diagnostics.Add(
                        $"retarget: skipped MmdBoneIndex {entry.MmdBoneIndex} " +
                        $"(HumanBone={entry.HumanBone}): invalid negative index");
                    skippedCount++;
                }
                else if (!usedRotationNativeIndices.Add(entry.MmdBoneIndex))
                {
                    diagnostics.Add(
                        $"retarget: skipped duplicate target MmdBoneIndex {entry.MmdBoneIndex} " +
                        $"(HumanBone={entry.HumanBone})");
                    skippedCount++;
                }
                else
                {
                    Transform? nativeTransform = entry.NativeTransform;
                    if (nativeTransform == null)
                    {
                        diagnostics.Add(
                            $"retarget: skipped MmdBoneIndex {entry.MmdBoneIndex} " +
                            $"(HumanBone={entry.HumanBone}): native transform is null or out of range");
                        skippedCount++;
                    }
                    else
                    {
                        // Validate that the proxy transform's localRotation is finite (non-NaN, non-inf).
                        Quaternion proxyRot = proxyTransform.localRotation;
                        if (!IsFiniteQuaternion(proxyRot))
                        {
                            diagnostics.Add(
                                $"retarget: skipped non-finite proxy rotation for {entry.HumanBone}: " +
                                $"rotation=({proxyRot.x}, {proxyRot.y}, {proxyRot.z}, {proxyRot.w})");
                            skippedCount++;
                        }
                        else
                        {
                            Quaternion retargetedRotation =
                                entry.NativeBindLocalRotation *
                                Quaternion.Inverse(entry.ProxyBindLocalRotation) *
                                proxyRot;
                            string? writeFailure = nativeArrayOutput.HasValue
                                ? nativeArrayOutput.Value.TryWriteRotation(entry, retargetedRotation)
                                : null;
                            if (writeFailure != null)
                            {
                                diagnostics.Add(
                                    $"retarget: skipped MmdBoneIndex {entry.MmdBoneIndex} " +
                                    $"(HumanBone={entry.HumanBone}): {writeFailure}");
                                skippedCount++;
                            }
                            else
                            {
                                if (!nativeArrayOutput.HasValue)
                                {
                                    nativeTransform.localRotation = retargetedRotation;
                                }

                                copiedCount++;
                            }
                        }
                    }

                }

                if (!copyLocalPositions || !entry.CopyLocalPosition)
                {
                    continue;
                }

                Transform? translationTarget = entry.TranslationTargetTransform;
                if (translationTarget == null)
                {
                    diagnostics.Add(
                        $"retarget: skipped translation for {entry.HumanBone}: translation target is null");
                    skippedTranslationCount++;
                    continue;
                }

                int translationTargetMmdBoneIndex = entry.TranslationTargetMmdBoneIndex;
                if (translationTargetMmdBoneIndex < 0)
                {
                    diagnostics.Add(
                        $"retarget: skipped translation for {entry.HumanBone}: invalid target MmdBoneIndex " +
                        translationTargetMmdBoneIndex);
                    skippedTranslationCount++;
                    continue;
                }

                if (!usedTranslationNativeIndices.Add(translationTargetMmdBoneIndex))
                {
                    diagnostics.Add(
                        $"retarget: skipped duplicate translation target MmdBoneIndex {translationTargetMmdBoneIndex} " +
                        $"(HumanBone={entry.HumanBone})");
                    skippedTranslationCount++;
                    continue;
                }

                Vector3 delta = proxyTransform.localPosition - entry.ProxyBindLocalPosition;
                string? translationWriteFailure = nativeArrayOutput.HasValue
                    ? nativeArrayOutput.Value.TryWriteTranslation(entry, delta)
                    : null;
                if (translationWriteFailure != null)
                {
                    diagnostics.Add(
                        $"retarget: skipped translation for {entry.HumanBone}: {translationWriteFailure}");
                    skippedTranslationCount++;
                }
                else
                {
                    if (!nativeArrayOutput.HasValue)
                    {
                        translationTarget.localPosition = entry.TranslationTargetBindLocalPosition + delta;
                    }

                    copiedTranslationCount++;
                }
            }

            diagnostics.Add(
                $"retarget: copied={copiedCount} skipped={skippedCount} " +
                $"copiedTranslation={copiedTranslationCount} skippedTranslation={skippedTranslationCount} " +
                $"total-matches={entries.Count}");

            return new MmdHumanoidRetargeterResult(
                copiedCount,
                skippedCount,
                copiedTranslationCount,
                skippedTranslationCount,
                diagnostics.ToArray());
        }

        private static void ResetNativeArrayPose(
            float[] localPositionOffsetsXyz,
            float[] localRotationXyzw,
            float[] localScalesXyz)
        {
            Array.Clear(localPositionOffsetsXyz, 0, localPositionOffsetsXyz.Length);
            Array.Clear(localRotationXyzw, 0, localRotationXyzw.Length);
            Array.Clear(localScalesXyz, 0, localScalesXyz.Length);
            for (int boneIndex = 0; boneIndex < localRotationXyzw.Length / 4; boneIndex++)
            {
                localRotationXyzw[boneIndex * 4 + 3] = 1.0f;
                localScalesXyz[boneIndex * 3] = 1.0f;
                localScalesXyz[boneIndex * 3 + 1] = 1.0f;
                localScalesXyz[boneIndex * 3 + 2] = 1.0f;
            }
        }

        private static void ValidateNativeArrayBoneMapping(
            MmdModelDefinition model,
            IReadOnlyList<Transform> nativeTransforms)
        {
            if (model.bones == null)
                throw new ArgumentException("Model bones are required.", nameof(model));

            for (int listIndex = 0; listIndex < model.bones.Count; listIndex++)
            {
                MmdBoneDefinition? bone = model.bones[listIndex];
                if (bone == null || bone.index != listIndex)
                    throw new ArgumentException(
                        "Model bone indices must be a unique contiguous mapping.", nameof(model));

                if (bone.parentIndex < -1 || bone.parentIndex >= model.bones.Count)
                    throw new ArgumentException(
                        $"Model parent index {bone.parentIndex} is invalid for bone {bone.index}.", nameof(model));

                if (nativeTransforms[listIndex] == null)
                    throw new ArgumentException(
                        $"Native transform {listIndex} is null.", nameof(nativeTransforms));
            }
        }

        private static bool IsActualNativeTarget(
            Transform? target,
            IReadOnlyList<Transform> nativeTransforms,
            int index)
        {
            return index >= 0 && index < nativeTransforms.Count &&
                   target != null && ReferenceEquals(target, nativeTransforms[index]);
        }

        private static Vector3 GetModelParentLocalRestPosition(MmdModelDefinition model, int index)
        {
            MmdBoneDefinition bone = model.bones[index];
            Vector3 origin = ToMmdVector3(bone.origin);
            if (bone.parentIndex < 0)
            {
                return origin;
            }

            return origin - ToMmdVector3(model.bones[bone.parentIndex].origin);
        }

        private static Vector3 ToMmdVector3(float[]? values)
        {
            return values != null && values.Length >= 3
                ? new Vector3(values[0], values[1], values[2])
                : Vector3.zero;
        }

        private static float NormalizeImportScale(float value)
        {
            return float.IsFinite(value) && value > 0.0f ? value : 1.0f;
        }

        private static void RequireExactArrayLength<T>(T[] values, int expected, string label)
        {
            if (values.Length != expected)
            {
                throw new ArgumentException(
                    label + " requires exactly " + expected + " elements, got " + values.Length + ".",
                    label);
            }
        }

        private readonly struct NativeArrayOutput
        {
            private readonly MmdModelDefinition model;
            private readonly IReadOnlyList<Transform> nativeTransforms;
            private readonly float importScale;
            private readonly float[] positions;
            private readonly float[] rotations;

            internal NativeArrayOutput(
                MmdModelDefinition model,
                IReadOnlyList<Transform> nativeTransforms,
                float importScale,
                float[] positions,
                float[] rotations)
            {
                this.model = model;
                this.nativeTransforms = nativeTransforms;
                this.importScale = importScale;
                this.positions = positions;
                this.rotations = rotations;
            }

            internal string? TryWriteRotation(
                MmdHumanoidRetargetBinding entry,
                Quaternion rotation)
            {
                int index = entry.MmdBoneIndex;
                if (!IsActualNativeTarget(entry.NativeTransform, nativeTransforms, index))
                {
                    return "native rotation target mapping does not match MMD bone index " + index;
                }

                Quaternion nativeRotation = MmdCoordinateSpace.UnityToMmdRotation(rotation);
                int offset = checked(index * 4);
                rotations[offset] = nativeRotation.x;
                rotations[offset + 1] = nativeRotation.y;
                rotations[offset + 2] = nativeRotation.z;
                rotations[offset + 3] = nativeRotation.w;
                return null;
            }

            internal string? TryWriteTranslation(
                MmdHumanoidRetargetBinding entry,
                Vector3 delta)
            {
                int index = entry.TranslationTargetMmdBoneIndex;
                if (!IsActualNativeTarget(entry.TranslationTargetTransform, nativeTransforms, index))
                {
                    return "native translation target mapping does not match MMD bone index " + index;
                }

                Vector3 restPosition = GetModelParentLocalRestPosition(model, index);
                Vector3 targetPosition = entry.TranslationTargetBindLocalPosition + delta;
                Vector3 targetPositionMmd = MmdCoordinateSpace.UnityToMmdPosition(targetPosition) / importScale;
                Vector3 offsetPosition = targetPositionMmd - restPosition;
                int offset = checked(index * 3);
                positions[offset] = offsetPosition.x;
                positions[offset + 1] = offsetPosition.y;
                positions[offset + 2] = offsetPosition.z;
                return null;
            }
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
