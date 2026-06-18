#nullable enable

using System.Collections.Generic;
using Mmd.Motion;
using Mmd.Parser;

namespace Mmd.Pose
{
    public static class MmdAppendTransformEvaluator
    {
        public static MmdSampledMotion ApplyAppendTransforms(MmdModelDefinition model, MmdSampledMotion sampledMotion)
        {
            var result = CopyMotion(sampledMotion);

            if (model == null)
            {
                return result;
            }

            IReadOnlyList<MmdBoneDefinition> bones = BonesOrEmpty(model);
            List<MmdBoneDefinition> evaluationOrder = BuildEvaluationOrder(bones);
            var appendTranslations = new Dictionary<int, float[]>(evaluationOrder.Count, EqualityComparer<int>.Default);
            var appendRotations = new Dictionary<int, float[]>(evaluationOrder.Count, EqualityComparer<int>.Default);
            foreach (MmdBoneDefinition bone in evaluationOrder)
            {
                if (bone.appendParentIndex < 0)
                {
                    continue;
                }

                MmdBoneDefinition? parent = FindBone(model, bone.appendParentIndex);
                if (parent == null)
                {
                    continue;
                }

                MmdBonePoseSample current = PoseOrIdentity(result, bone.name);
                MmdBonePoseSample append = PoseOrIdentity(result, parent.name);

                float[] translation = current.Translation;
                float[] rotation = current.Rotation;

                if (bone.appendTranslation)
                {
                    bool parentHasAppend = parent.appendParentIndex >= 0;
                    float[] appendTranslation = !bone.appendLocal && parentHasAppend && appendTranslations.TryGetValue(parent.index, out float[]? inheritedTranslation)
                        ? inheritedTranslation
                        : MmdPoseEvaluator.GetLocalTranslation(model, parent, append);
                    float[] weightedTranslation = Scale(appendTranslation, bone.appendRatio);
                    appendTranslations[bone.index] = weightedTranslation;
                    translation = Add(current.Translation, weightedTranslation);
                }

                if (bone.appendRotation)
                {
                    bool parentHasAppend = parent.appendParentIndex >= 0;
                    float[] appendRotation = !bone.appendLocal && parentHasAppend && appendRotations.TryGetValue(parent.index, out float[]? inheritedRotation)
                        ? inheritedRotation
                        : append.Rotation;
                    float[] weightedRotation = MmdQuaternionMath.Slerp(MmdBonePoseSample.Identity.Rotation, appendRotation, bone.appendRatio);
                    appendRotations[bone.index] = weightedRotation;
                    rotation = MmdQuaternionMath.Multiply(current.Rotation, weightedRotation);
                }

                result.Bones[bone.name] = new MmdBonePoseSample(translation, rotation);
            }

            return result;
        }

        public static MmdSampledMotion ReapplyAppendTransformsForSources(
            MmdModelDefinition model,
            MmdSampledMotion preAppendMotion,
            MmdSampledMotion appendedMotion,
            IReadOnlyCollection<int> sourceBoneIndices)
        {
            var result = CopyMotion(appendedMotion);
            if (model == null || preAppendMotion == null || sourceBoneIndices.Count == 0)
            {
                return result;
            }

            IReadOnlyList<MmdBoneDefinition> bones = BonesOrEmpty(model);
            List<MmdBoneDefinition> evaluationOrder = BuildEvaluationOrder(bones);
            var changedBoneIndices = new HashSet<int>(sourceBoneIndices);
            var sourceBoneIndexSet = new HashSet<int>(sourceBoneIndices);
            var reappliedBoneIndices = new HashSet<int>(evaluationOrder.Count);
            var appendTranslations = new Dictionary<int, float[]>(evaluationOrder.Count, EqualityComparer<int>.Default);
            var appendRotations = new Dictionary<int, float[]>(evaluationOrder.Count, EqualityComparer<int>.Default);
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (MmdBoneDefinition bone in evaluationOrder)
                {
                    if (reappliedBoneIndices.Contains(bone.index)
                        || sourceBoneIndexSet.Contains(bone.index)
                        || bone.appendParentIndex < 0
                        || !changedBoneIndices.Contains(bone.appendParentIndex)
                        || (!bone.appendRotation && !bone.appendTranslation))
                    {
                        continue;
                    }

                    MmdBoneDefinition? parent = FindBone(model, bone.appendParentIndex);
                    if (parent == null)
                    {
                        continue;
                    }

                    MmdBonePoseSample basePose = PoseOrIdentity(preAppendMotion, bone.name);
                    MmdBonePoseSample sourcePose = PoseOrIdentity(result, parent.name);

                    float[] translation = basePose.Translation;
                    float[] rotation = basePose.Rotation;
                    bool parentHasAppend = parent.appendParentIndex >= 0;

                    if (bone.appendRotation)
                    {
                        float[] sourceRotation = !bone.appendLocal && parentHasAppend && appendRotations.TryGetValue(parent.index, out float[]? inheritedRotation)
                            ? inheritedRotation
                            : sourcePose.Rotation;
                        float[] weightedRotation = MmdQuaternionMath.Slerp(MmdBonePoseSample.Identity.Rotation, sourceRotation, bone.appendRatio);
                        appendRotations[bone.index] = weightedRotation;
                        rotation = MmdQuaternionMath.Multiply(basePose.Rotation, weightedRotation);
                    }

                    if (bone.appendTranslation)
                    {
                        float[] sourceTranslation;
                        if (!bone.appendLocal && parentHasAppend && appendTranslations.TryGetValue(parent.index, out float[]? inheritedTranslation))
                        {
                            sourceTranslation = inheritedTranslation;
                        }
                        else
                        {
                            sourceTranslation = MmdPoseEvaluator.GetLocalTranslation(model, parent, sourcePose);
                            MmdBonePoseSample preAppendParentPose = PoseOrIdentity(preAppendMotion, parent.name);
                            float[] preAppendParentTranslation = MmdPoseEvaluator.GetLocalTranslation(model, parent, preAppendParentPose);
                            sourceTranslation = Subtract(sourceTranslation, preAppendParentTranslation);
                        }

                        float[] weightedTranslation = Scale(sourceTranslation, bone.appendRatio);
                        appendTranslations[bone.index] = weightedTranslation;
                        translation = Add(basePose.Translation, weightedTranslation);
                    }

                    result.Bones[bone.name] = new MmdBonePoseSample(translation, rotation);
                    reappliedBoneIndices.Add(bone.index);
                    changedBoneIndices.Add(bone.index);
                    changed = true;
                }
            }

            return result;
        }

        private static MmdSampledMotion CopyMotion(MmdSampledMotion? sampledMotion)
        {
            var result = new MmdSampledMotion();
            if (sampledMotion == null)
            {
                return result;
            }

            var bones = new List<KeyValuePair<string, MmdBonePoseSample>>(sampledMotion.Bones);
            bones.Sort((left, right) => System.StringComparer.Ordinal.Compare(left.Key, right.Key));
            foreach (KeyValuePair<string, MmdBonePoseSample> bone in bones)
            {
                result.Bones[bone.Key] = bone.Value;
            }

            var morphs = new List<KeyValuePair<string, float>>(sampledMotion.Morphs);
            morphs.Sort((left, right) => System.StringComparer.Ordinal.Compare(left.Key, right.Key));
            foreach (KeyValuePair<string, float> morph in morphs)
            {
                result.Morphs[morph.Key] = morph.Value;
            }

            var ikStates = new List<KeyValuePair<string, bool>>(sampledMotion.IkStates);
            ikStates.Sort((left, right) => System.StringComparer.Ordinal.Compare(left.Key, right.Key));
            foreach (KeyValuePair<string, bool> ikState in ikStates)
            {
                result.IkStates[ikState.Key] = ikState.Value;
            }

            return result;
        }

        private static MmdBonePoseSample PoseOrIdentity(MmdSampledMotion motion, string boneName)
        {
            return motion.Bones.TryGetValue(boneName, out MmdBonePoseSample pose)
                ? pose
                : MmdBonePoseSample.Identity;
        }

        private static float[] Scale(float[] value, float scale)
        {
            return new[] { value[0] * scale, value[1] * scale, value[2] * scale };
        }

        private static float[] Add(float[] left, float[] right)
        {
            return new[] { left[0] + right[0], left[1] + right[1], left[2] + right[2] };
        }

        private static float[] Subtract(float[] left, float[] right)
        {
            return new[] { left[0] - right[0], left[1] - right[1], left[2] - right[2] };
        }

        private static List<MmdBoneDefinition> BuildEvaluationOrder(IReadOnlyList<MmdBoneDefinition> bones)
        {
            var indexedBones = new List<IndexedBoneDefinition>(bones.Count);
            for (int boneListIndex = 0; boneListIndex < bones.Count; boneListIndex++)
            {
                indexedBones.Add(new IndexedBoneDefinition(bones[boneListIndex], boneListIndex));
            }

            indexedBones.Sort(CompareBoneEvaluationOrder);

            var orderedBones = new List<MmdBoneDefinition>(indexedBones.Count);
            for (int sortedIndex = 0; sortedIndex < indexedBones.Count; sortedIndex++)
            {
                orderedBones.Add(indexedBones[sortedIndex].Bone);
            }

            return orderedBones;
        }

        private static int CompareBoneEvaluationOrder(IndexedBoneDefinition left, IndexedBoneDefinition right)
        {
            int transformOrderComparison = left.Bone.transformOrder.CompareTo(right.Bone.transformOrder);
            if (transformOrderComparison != 0)
            {
                return transformOrderComparison;
            }

            int indexComparison = left.Bone.index.CompareTo(right.Bone.index);
            if (indexComparison != 0)
            {
                return indexComparison;
            }

            return left.SourceIndex.CompareTo(right.SourceIndex);
        }

        private readonly struct IndexedBoneDefinition
        {
            public IndexedBoneDefinition(MmdBoneDefinition bone, int sourceIndex)
            {
                Bone = bone;
                SourceIndex = sourceIndex;
            }

            public MmdBoneDefinition Bone { get; }

            public int SourceIndex { get; }
        }

        private static MmdBoneDefinition? FindBone(MmdModelDefinition model, int index)
        {
            IReadOnlyList<MmdBoneDefinition> bones = BonesOrEmpty(model);
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i].index == index)
                {
                    return bones[i];
                }
            }

            return null;
        }

        private static IReadOnlyList<MmdBoneDefinition> BonesOrEmpty(MmdModelDefinition model)
        {
            return model.bones != null ? model.bones : System.Array.Empty<MmdBoneDefinition>();
        }
    }
}
