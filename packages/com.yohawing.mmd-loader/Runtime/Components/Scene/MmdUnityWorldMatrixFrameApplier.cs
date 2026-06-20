#nullable enable

using System;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    internal static class MmdUnityWorldMatrixFrameApplier
    {
        public static void ApplyColumnMajorWorldMatrices(MmdUnityModelInstance instance, float[] worldMatrices)
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
            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                Transform bone = instance.BoneTransforms[boneIndex];
                int offset = boneIndex * 16;
                Vector3 mmdPosition = new Vector3(
                    worldMatrices[offset + 12],
                    worldMatrices[offset + 13],
                    worldMatrices[offset + 14]);
                Quaternion mmdRotation = ExtractColumnMajorRotation(worldMatrices, offset);
                bone.position = root.TransformPoint(ToUnityModelPosition(mmdPosition) * importScale);
                bone.rotation = root.rotation * ToUnityModelRotation(mmdRotation);
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
            return new Vector3(-position.x, position.y, -position.z);
        }

        private static Quaternion ToUnityModelRotation(Quaternion rotation)
        {
            return new Quaternion(-rotation.x, rotation.y, -rotation.z, rotation.w);
        }

        private static float NormalizeImportScale(float importScale)
        {
            return float.IsFinite(importScale) && importScale > 0.0f ? importScale : 1.0f;
        }
    }
}
