#nullable enable

using System.Collections.Generic;
using System;
using Yohawing.MmdUnity.Motion;
using Yohawing.MmdUnity.Parser;

namespace Yohawing.MmdUnity.Pose
{
    public static class MmdPoseEvaluator
    {
        public static Dictionary<int, float[]> EvaluateWorldMatrices(MmdModelDefinition? model, MmdSampledMotion? sampledMotion)
        {
            if (model == null)
            {
                return new Dictionary<int, float[]>();
            }

            IReadOnlyList<MmdBoneDefinition> bones = BonesOrEmpty(model);
            var worldMatrices = new Dictionary<int, float[]>(bones.Count);
            var visiting = new HashSet<int>(bones.Count);
            for (int i = 0; i < bones.Count; i++)
            {
                visiting.Clear();
                EvaluateBone(model, sampledMotion, bones[i].index, worldMatrices, visiting);
            }

            return worldMatrices;
        }

        private static float[] EvaluateBone(
            MmdModelDefinition model,
            MmdSampledMotion? sampledMotion,
            int boneIndex,
            Dictionary<int, float[]> worldMatrices,
            HashSet<int> visiting)
        {
            if (worldMatrices.TryGetValue(boneIndex, out float[]? existing))
            {
                return existing;
            }

            if (!visiting.Add(boneIndex))
            {
                throw new InvalidOperationException($"Bone parent cycle detected at index {boneIndex}.");
            }

            MmdBoneDefinition? bone = null;
            IReadOnlyList<MmdBoneDefinition> bones = BonesOrEmpty(model);
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i].index == boneIndex)
                {
                    bone = bones[i];
                    break;
                }
            }
            if (bone == null)
            {
                float[] identity = MmdPoseMath.LocalMatrix(
                    new[] { 0.0f, 0.0f, 0.0f },
                    new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                    new[] { 1.0f, 1.0f, 1.0f });
                worldMatrices[boneIndex] = identity;
                return identity;
            }

            MmdBonePoseSample sample = sampledMotion != null && sampledMotion.Bones.TryGetValue(bone.name, out MmdBonePoseSample found)
                ? found
                : MmdBonePoseSample.Identity;

            float[] local = MmdPoseMath.LocalMatrix(GetLocalTranslation(model, bone, sample), sample.Rotation, new[] { 1.0f, 1.0f, 1.0f });
            float[] world = bone.parentIndex >= 0
                ? MmdPoseMath.Multiply(EvaluateBone(model, sampledMotion, bone.parentIndex, worldMatrices, visiting), local)
                : local;

            worldMatrices[boneIndex] = world;
            visiting.Remove(boneIndex);
            return world;
        }

        public static float[] GetLocalTranslation(MmdModelDefinition model, MmdBoneDefinition bone, MmdBonePoseSample sample)
        {
            float[] bindOffset = bone.parentIndex >= 0
                ? Subtract(OriginOrZero(bone), OriginOrZero(FindBone(model, bone.parentIndex)))
                : OriginOrZero(bone);

            return new[]
            {
                bindOffset[0] + sample.Translation[0],
                bindOffset[1] + sample.Translation[1],
                bindOffset[2] + sample.Translation[2]
            };
        }

        private static MmdBoneDefinition? FindBone(MmdModelDefinition model, int boneIndex)
        {
            IReadOnlyList<MmdBoneDefinition> bones = BonesOrEmpty(model);
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i].index == boneIndex)
                {
                    return bones[i];
                }
            }

            return null;
        }

        private static IReadOnlyList<MmdBoneDefinition> BonesOrEmpty(MmdModelDefinition model)
        {
            return model.bones != null ? model.bones : Array.Empty<MmdBoneDefinition>();
        }

        private static float[] OriginOrZero(MmdBoneDefinition? bone)
        {
            if (bone?.origin == null || bone.origin.Length != 3)
            {
                return new[] { 0.0f, 0.0f, 0.0f };
            }

            return bone.origin;
        }

        private static float[] Subtract(float[] left, float[] right)
        {
            return new[]
            {
                left[0] - right[0],
                left[1] - right[1],
                left[2] - right[2]
            };
        }
    }
}
