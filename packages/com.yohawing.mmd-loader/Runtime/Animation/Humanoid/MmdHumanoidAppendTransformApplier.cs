#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Mmd
{
    public static class MmdHumanoidAppendTransformApplier
    {
        public static void Apply(IReadOnlyList<MmdHumanoidAppendTransformBinding> appendEntries)
        {
            if (appendEntries == null || appendEntries.Count == 0)
            {
                return;
            }

            List<MmdHumanoidAppendTransformBinding> evaluationOrder = BuildEvaluationOrder(appendEntries);
            var appendTargetIndices = new HashSet<int>();
            foreach (MmdHumanoidAppendTransformBinding entry in evaluationOrder)
            {
                if (entry != null && entry.TargetMmdBoneIndex >= 0)
                {
                    appendTargetIndices.Add(entry.TargetMmdBoneIndex);
                }
            }

            var appendTranslations = new Dictionary<int, Vector3>(evaluationOrder.Count);
            var appendRotations = new Dictionary<int, Quaternion>(evaluationOrder.Count);

            foreach (MmdHumanoidAppendTransformBinding entry in evaluationOrder)
            {
                if (entry == null)
                {
                    continue;
                }

                Transform? target = entry.TargetTransform;
                Transform? parent = entry.AppendParentTransform;
                if (target == null || parent == null)
                {
                    continue;
                }

                bool parentHasAppend = appendTargetIndices.Contains(entry.AppendParentMmdBoneIndex);

                if (entry.AppendRotation)
                {
                    Quaternion parentDelta =
                        Quaternion.Inverse(entry.AppendParentBindLocalRotation) * parent.localRotation;
                    Quaternion sourceRotation =
                        !entry.AppendLocal
                        && parentHasAppend
                        && appendRotations.TryGetValue(entry.AppendParentMmdBoneIndex, out Quaternion inheritedRotation)
                            ? inheritedRotation
                            : parentDelta;
                    Quaternion weightedRotation = Quaternion.SlerpUnclamped(
                        Quaternion.identity,
                        sourceRotation,
                        entry.AppendRatio);
                    appendRotations[entry.TargetMmdBoneIndex] = weightedRotation;
                    target.localRotation = entry.TargetBindLocalRotation * weightedRotation;
                }

                if (entry.AppendTranslation)
                {
                    Vector3 parentDelta = parent.localPosition - entry.AppendParentBindLocalPosition;
                    Vector3 sourceTranslation =
                        !entry.AppendLocal
                        && parentHasAppend
                        && appendTranslations.TryGetValue(entry.AppendParentMmdBoneIndex, out Vector3 inheritedTranslation)
                            ? inheritedTranslation
                            : parentDelta;
                    Vector3 weightedTranslation = sourceTranslation * entry.AppendRatio;
                    appendTranslations[entry.TargetMmdBoneIndex] = weightedTranslation;
                    target.localPosition = entry.TargetBindLocalPosition + weightedTranslation;
                }
            }
        }

        private static List<MmdHumanoidAppendTransformBinding> BuildEvaluationOrder(
            IReadOnlyList<MmdHumanoidAppendTransformBinding> appendEntries)
        {
            var ordered = new List<MmdHumanoidAppendTransformBinding>(appendEntries.Count);
            for (int i = 0; i < appendEntries.Count; i++)
            {
                MmdHumanoidAppendTransformBinding entry = appendEntries[i];
                if (entry != null)
                {
                    ordered.Add(entry);
                }
            }

            ordered.Sort(CompareEvaluationOrder);
            return ordered;
        }

        private static int CompareEvaluationOrder(
            MmdHumanoidAppendTransformBinding left,
            MmdHumanoidAppendTransformBinding right)
        {
            int orderComparison = left.EvaluationOrder.CompareTo(right.EvaluationOrder);
            if (orderComparison != 0)
            {
                return orderComparison;
            }

            return left.TargetMmdBoneIndex.CompareTo(right.TargetMmdBoneIndex);
        }
    }
}
