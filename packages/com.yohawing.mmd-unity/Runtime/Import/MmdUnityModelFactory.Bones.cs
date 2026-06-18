#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Yohawing.MmdUnity.Parser;

namespace Yohawing.MmdUnity.UnityIntegration
{
    public static partial class MmdUnityModelFactory
    {
        private static Transform[] BuildBoneTransforms(Transform root, IReadOnlyList<MmdBoneDefinition>? bones)
        {
            return BuildBoneTransforms(root, bones, importScale: 1.0f);
        }

        private static Transform[] BuildBoneTransforms(Transform root, IReadOnlyList<MmdBoneDefinition>? bones, float importScale)
        {
            if (bones == null || bones.Count == 0)
            {
                return Array.Empty<Transform>();
            }

            float scale = NormalizeImportScale(importScale);
            IReadOnlyList<MmdBoneDefinition> orderedBones = CreateOrderedBones(bones);

            var transformsByIndex = new Dictionary<int, Transform>(orderedBones.Count);
            foreach (MmdBoneDefinition bone in orderedBones)
            {
                string boneName = string.IsNullOrWhiteSpace(bone.name)
                    ? $"bone_{bone.index}"
                    : bone.name;
                var boneObject = new GameObject(boneName);
                transformsByIndex[bone.index] = boneObject.transform;
            }

            foreach (MmdBoneDefinition bone in orderedBones)
            {
                Transform boneTransform = transformsByIndex[bone.index];
                Transform parentTransform = root;
                float[] parentOrigin = Array.Empty<float>();
                if (bone.parentIndex >= 0 && transformsByIndex.TryGetValue(bone.parentIndex, out Transform parentBoneTransform))
                {
                    parentTransform = parentBoneTransform;
                    MmdBoneDefinition? parentBone = FindBoneByIndex(orderedBones, bone.parentIndex);
                    if (parentBone != null)
                    {
                        parentOrigin = parentBone.origin;
                    }
                }

                boneTransform.SetParent(parentTransform, worldPositionStays: false);
                boneTransform.localPosition = ToUnityPosition(SubtractOrigin(bone.origin, parentOrigin), scale);
                boneTransform.localRotation = Quaternion.identity;
                boneTransform.localScale = Vector3.one;
            }

            var result = new Transform[orderedBones.Count];
            for (int i = 0; i < orderedBones.Count; i++)
            {
                result[i] = transformsByIndex[orderedBones[i].index];
            }

            return result;
        }

        private static IReadOnlyList<MmdBoneDefinition> CreateOrderedBones(IReadOnlyList<MmdBoneDefinition> bones)
        {
            var orderedBones = new List<MmdBoneDefinition>(bones);
            orderedBones.Sort((left, right) => left.index.CompareTo(right.index));
            return orderedBones;
        }

        private static void ResetExistingBoneTransformsToBindPose(
            IReadOnlyList<MmdBoneDefinition> orderedBones,
            Transform[] boneTransforms,
            float importScale)
        {
            float scale = NormalizeImportScale(importScale);
            for (int i = 0; i < orderedBones.Count; i++)
            {
                MmdBoneDefinition bone = orderedBones[i];
                Transform boneTransform = boneTransforms[i];
                if (boneTransform == null)
                {
                    throw new InvalidOperationException($"Existing PMX scene bone at index {i} is missing.");
                }

                float[] parentOrigin = Array.Empty<float>();
                if (bone.parentIndex >= 0)
                {
                    MmdBoneDefinition? parentBone = FindBoneByIndex(orderedBones, bone.parentIndex);
                    if (parentBone != null)
                    {
                        parentOrigin = parentBone.origin;
                    }
                }

                boneTransform.localPosition = ToUnityPosition(SubtractOrigin(bone.origin, parentOrigin), scale);
                boneTransform.localRotation = Quaternion.identity;
                boneTransform.localScale = Vector3.one;
            }
        }

        private static Dictionary<int, Transform> BuildBoneTransformMap(IReadOnlyList<MmdBoneDefinition>? bones, Transform[] boneTransforms)
        {
            if (bones == null || boneTransforms.Length == 0)
            {
                return new Dictionary<int, Transform>();
            }

            IReadOnlyList<MmdBoneDefinition> orderedBones = CreateOrderedBones(bones);
            int limit = Math.Min(orderedBones.Count, boneTransforms.Length);
            var result = new Dictionary<int, Transform>(limit);
            for (int i = 0; i < limit; i++)
            {
                result[orderedBones[i].index] = boneTransforms[i];
            }

            return result;
        }

        private static Dictionary<int, MmdBoneDefinition> BuildBoneDefinitionMap(IReadOnlyList<MmdBoneDefinition>? bones)
        {
            if (bones == null)
            {
                return new Dictionary<int, MmdBoneDefinition>();
            }

            var result = new Dictionary<int, MmdBoneDefinition>(bones.Count);
            foreach (MmdBoneDefinition bone in bones)
            {
                result[bone.index] = bone;
            }

            return result;
        }

        private static MmdBoneDefinition? FindBoneByIndex(IReadOnlyList<MmdBoneDefinition> bones, int index)
        {
            foreach (MmdBoneDefinition bone in bones)
            {
                if (bone.index == index)
                {
                    return bone;
                }
            }

            return null;
        }

        private static float[] SubtractOrigin(float[] origin, float[] parentOrigin)
        {
            if (parentOrigin == null || parentOrigin.Length < 3)
            {
                return origin;
            }

            return new[]
            {
                origin[0] - parentOrigin[0],
                origin[1] - parentOrigin[1],
                origin[2] - parentOrigin[2]
            };
        }
    }
}
