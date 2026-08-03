#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    internal static class MmdUnityWorldMatrixFrameApplier
    {
        public static void ApplyColumnMajorWorldMatrices(
            MmdUnityModelInstance instance,
            float[] worldMatrices,
            IReadOnlyList<int>? boneIndices = null)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (worldMatrices == null)
            {
                throw new ArgumentNullException(nameof(worldMatrices));
            }

            int boneCount = instance.BoneTransforms.Length;
            int required = boneCount * 16;
            if (worldMatrices.Length < required)
            {
                throw new ArgumentException($"World matrix buffer must contain at least {required} float values.", nameof(worldMatrices));
            }

            Transform root = instance.Root.transform;
            float importScale = NormalizeImportScale(instance.ImportScale);
            if (boneIndices == null)
            {
                for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
                {
                    ApplyBoneWorldMatrix(instance, root, importScale, worldMatrices, boneIndex);
                }

                return;
            }

            for (int i = 0; i < boneIndices.Count; i++)
            {
                int boneIndex = boneIndices[i];
                if (boneIndex < 0 || boneIndex >= boneCount)
                {
                    throw new ArgumentException(
                        $"World matrix bone index {boneIndex} is outside the Unity bone array.",
                        nameof(boneIndices));
                }

                ApplyBoneWorldMatrix(instance, root, importScale, worldMatrices, boneIndex);
            }
        }

        private static void ApplyBoneWorldMatrix(
            MmdUnityModelInstance instance,
            Transform root,
            float importScale,
            float[] worldMatrices,
            int boneIndex)
        {
            Transform bone = instance.BoneTransforms[boneIndex];
            int offset = boneIndex * 16;
            Vector3 mmdPosition = new Vector3(
                worldMatrices[offset + 12],
                worldMatrices[offset + 13],
                worldMatrices[offset + 14]);
            Quaternion mmdRotation = ExtractColumnMajorRotation(worldMatrices, offset);
            Vector3 worldPosition = root.TransformPoint(ToUnityModelPosition(mmdPosition) * importScale);
            Quaternion worldRotation = root.rotation * ToUnityModelRotation(mmdRotation);
            bone.SetPositionAndRotation(worldPosition, worldRotation);
            if (bone.localScale != Vector3.one)
            {
                bone.localScale = Vector3.one;
            }
        }

        private static Quaternion ExtractColumnMajorRotation(float[] matrix, int offset)
        {
            Vector3 forward = new Vector3(matrix[offset + 8], matrix[offset + 9], matrix[offset + 10]);
            Vector3 up = new Vector3(matrix[offset + 4], matrix[offset + 5], matrix[offset + 6]);
            if (forward.sqrMagnitude <= 0.0f || up.sqrMagnitude <= 0.0f)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(forward.normalized, up.normalized);
        }

        private static Vector3 ToUnityModelPosition(Vector3 position)
        {
            return MmdCoordinateSpace.MmdToUnityPosition(position);
        }

        private static Quaternion ToUnityModelRotation(Quaternion rotation)
        {
            return MmdCoordinateSpace.MmdToUnityRotation(rotation);
        }

        private static float NormalizeImportScale(float importScale)
        {
            return float.IsFinite(importScale) && importScale > 0.0f ? importScale : 1.0f;
        }
    }
}
