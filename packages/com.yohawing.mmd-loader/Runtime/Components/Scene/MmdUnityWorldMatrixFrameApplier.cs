#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Mmd.Parser;

namespace Mmd.UnityIntegration
{
    internal static class MmdUnityWorldMatrixFrameApplier
    {
        internal static void ValidateColumnMajorWorldMatrices(
            MmdUnityModelInstance instance,
            float[] worldMatrices,
            IReadOnlyList<int>? boneIndices = null)
        {
            if (instance == null || instance.Root == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            Transform[] bones = instance.BoneTransforms;
            if (bones == null)
            {
                throw new ArgumentNullException(nameof(instance.BoneTransforms));
            }

            if (worldMatrices == null)
            {
                throw new ArgumentNullException(nameof(worldMatrices));
            }

            int required = bones.Length * 16;
            if (worldMatrices.Length < required)
            {
                throw new ArgumentException(
                    $"World matrix buffer must contain at least {required} float values.",
                    nameof(worldMatrices));
            }

            if (boneIndices == null)
            {
                for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                {
                    ValidateBone(bones, boneIndex, nameof(bones));
                }

                return;
            }

            for (int i = 0; i < boneIndices.Count; i++)
            {
                ValidateBone(bones, boneIndices[i], nameof(boneIndices));
            }
        }

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

            ApplyColumnMajorWorldMatrices(
                instance.Root.transform,
                instance.BoneTransforms,
                instance.ImportScale,
                worldMatrices,
                boneIndices);
        }

        internal static void ApplyColumnMajorWorldMatrices(
            Transform root,
            Transform[] bones,
            float importScale,
            float[] worldMatrices,
            IReadOnlyList<int>? boneIndices = null)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (bones == null)
            {
                throw new ArgumentNullException(nameof(bones));
            }

            if (worldMatrices == null)
            {
                throw new ArgumentNullException(nameof(worldMatrices));
            }

            int boneCount = bones.Length;
            int required = boneCount * 16;
            if (worldMatrices.Length < required)
            {
                throw new ArgumentException($"World matrix buffer must contain at least {required} float values.", nameof(worldMatrices));
            }

            importScale = NormalizeImportScale(importScale);
            if (boneIndices == null)
            {
                for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
                {
                    ApplyBoneWorldMatrix(bones, root, importScale, worldMatrices, boneIndex);
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

                ApplyBoneWorldMatrix(bones, root, importScale, worldMatrices, boneIndex);
            }
        }

        internal static void ApplyHostRigColumnMajorWorldMatrices(
            Transform root,
            Transform[] bones,
            float importScale,
            IReadOnlyList<MmdBoneDefinition> modelBones,
            float[] worldMatrices,
            HashSet<Transform> boneTransformSet)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (bones == null)
            {
                throw new ArgumentNullException(nameof(bones));
            }

            if (modelBones == null)
            {
                throw new ArgumentNullException(nameof(modelBones));
            }

            if (worldMatrices == null)
            {
                throw new ArgumentNullException(nameof(worldMatrices));
            }

            if (boneTransformSet == null)
            {
                throw new ArgumentNullException(nameof(boneTransformSet));
            }

            int boneCount = modelBones.Count;
            if (boneCount == 0)
            {
                throw new InvalidOperationException("HostRig PMX bone topology is empty.");
            }

            if (bones.Length != boneCount)
            {
                throw new InvalidOperationException(
                    "HostRig world matrix hierarchy bone count does not match the PMX model.");
            }

            int required = checked(boneCount * 16);
            if (worldMatrices.Length < required)
            {
                throw new ArgumentException(
                    $"HostRig world matrix buffer must contain at least {required} float values.",
                    nameof(worldMatrices));
            }

            importScale = NormalizeImportScale(importScale);
            ValidateHostRigHierarchy(root, bones, modelBones, boneTransformSet);

            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                Matrix4x4 localMatrix = BuildHostRigLocalMatrix(
                    worldMatrices,
                    modelBones,
                    boneIndex);
                ExtractHostRigLocalPose(
                    localMatrix,
                    importScale,
                    out Vector3 localPosition,
                    out Quaternion localRotation,
                    out Vector3 localScale);
                Transform bone = bones[boneIndex];
                bone.localPosition = localPosition;
                bone.localRotation = localRotation;
                bone.localScale = localScale;
            }
        }

        private static void ApplyBoneWorldMatrix(
            Transform[] bones,
            Transform root,
            float importScale,
            float[] worldMatrices,
            int boneIndex)
        {
            Transform bone = bones[boneIndex];
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

        private static void ValidateHostRigHierarchy(
            Transform root,
            Transform[] bones,
            IReadOnlyList<MmdBoneDefinition> modelBones,
            HashSet<Transform> boneTransformSet)
        {
            int boneCount = modelBones.Count;
            boneTransformSet.Clear();
            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                MmdBoneDefinition? modelBone = modelBones[boneIndex];
                if (modelBone == null || modelBone.index != boneIndex)
                {
                    throw new InvalidOperationException(
                        "HostRig PMX bone topology has a non-contiguous index at " + boneIndex + ".");
                }

                if (modelBone.parentIndex < -1 || modelBone.parentIndex >= boneCount)
                {
                    throw new InvalidOperationException(
                        "HostRig PMX parent index is invalid for bone " + boneIndex + ": " +
                        modelBone.parentIndex + ".");
                }

                if (bones[boneIndex] == null)
                {
                    throw new InvalidOperationException(
                        "HostRig Unity bone transform is missing at index " + boneIndex + ".");
                }

                if (!boneTransformSet.Add(bones[boneIndex]))
                {
                    throw new InvalidOperationException(
                        "HostRig Unity bone transform is assigned to multiple PMX indices.");
                }
            }

            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                MmdBoneDefinition modelBone = modelBones[boneIndex];
                Transform bone = bones[boneIndex];
                if (modelBone.parentIndex >= 0)
                {
                    if (!ReferenceEquals(bone.parent, bones[modelBone.parentIndex]))
                    {
                        throw new InvalidOperationException(
                            "HostRig Unity parent does not match PMX parent for bone " + boneIndex + ".");
                    }

                    continue;
                }

                ValidateHostRigRootParent(root, bones, boneTransformSet, boneIndex);
            }
        }

        private static void ValidateHostRigRootParent(
            Transform root,
            Transform[] bones,
            HashSet<Transform> boneTransformSet,
            int boneIndex)
        {
            Transform? current = bones[boneIndex].parent;
            if (current == null)
            {
                throw new InvalidOperationException(
                    "HostRig root bone must be parented below the model instance root.");
            }

            while (!ReferenceEquals(current, root))
            {
                if (boneTransformSet.Contains(current))
                {
                    throw new InvalidOperationException(
                        "HostRig root bone parent chain must not pass through another native bone.");
                }

                if (!IsIdentityHostRigContainer(current))
                {
                    throw new InvalidOperationException(
                        "HostRig root bone parent container must preserve the model instance basis.");
                }

                current = current.parent;
                if (current == null)
                {
                    throw new InvalidOperationException(
                        "HostRig root bone must be parented below the model instance root.");
                }
            }
        }

        private static bool IsIdentityHostRigContainer(Transform transform)
        {
            Quaternion rotation = transform.localRotation;
            Vector3 position = transform.localPosition;
            Vector3 scale = transform.localScale;
            return position.sqrMagnitude <= 0.00000001f &&
                Mathf.Abs(rotation.x) <= 0.0001f &&
                Mathf.Abs(rotation.y) <= 0.0001f &&
                Mathf.Abs(rotation.z) <= 0.0001f &&
                Mathf.Abs(Mathf.Abs(rotation.w) - 1.0f) <= 0.0001f &&
                (scale - Vector3.one).sqrMagnitude <= 0.00000001f;
        }

        private static Matrix4x4 BuildHostRigLocalMatrix(
            float[] worldMatrices,
            IReadOnlyList<MmdBoneDefinition> modelBones,
            int boneIndex)
        {
            Matrix4x4 childWorld = ReadHostRigNativeMatrix(
                worldMatrices,
                checked(boneIndex * 16));
            int parentIndex = modelBones[boneIndex].parentIndex;
            if (parentIndex < 0)
            {
                return childWorld;
            }

            Matrix4x4 parentWorld = ReadHostRigNativeMatrix(
                worldMatrices,
                checked(parentIndex * 16));
            return parentWorld.inverse * childWorld;
        }

        private static Matrix4x4 ReadHostRigNativeMatrix(
            float[] worldMatrices,
            int offset)
        {
            return new Matrix4x4
            {
                m00 = worldMatrices[offset],
                m10 = worldMatrices[offset + 1],
                m20 = worldMatrices[offset + 2],
                m30 = worldMatrices[offset + 3],
                m01 = worldMatrices[offset + 4],
                m11 = worldMatrices[offset + 5],
                m21 = worldMatrices[offset + 6],
                m31 = worldMatrices[offset + 7],
                m02 = worldMatrices[offset + 8],
                m12 = worldMatrices[offset + 9],
                m22 = worldMatrices[offset + 10],
                m32 = worldMatrices[offset + 11],
                m03 = worldMatrices[offset + 12],
                m13 = worldMatrices[offset + 13],
                m23 = worldMatrices[offset + 14],
                m33 = worldMatrices[offset + 15]
            };
        }

        private static void ExtractHostRigLocalPose(
            Matrix4x4 matrix,
            float importScale,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale)
        {
            position = MmdCoordinateSpace.MmdToUnityPosition(
                new Vector3(matrix.m03, matrix.m13, matrix.m23),
                importScale);
            Vector3 right = matrix.GetColumn(0);
            Vector3 up = matrix.GetColumn(1);
            Vector3 forward = matrix.GetColumn(2);
            float rightMagnitude = right.magnitude;
            float upMagnitude = up.magnitude;
            float forwardMagnitude = forward.magnitude;
            rotation = MmdCoordinateSpace.MmdToUnityRotation(
                Quaternion.LookRotation(forward / forwardMagnitude, up / upMagnitude));
            scale = new Vector3(rightMagnitude, upMagnitude, forwardMagnitude);
        }

        private static void ValidateBone(Transform[] bones, int boneIndex, string parameterName)
        {
            if (boneIndex < 0 || boneIndex >= bones.Length)
            {
                throw new ArgumentException(
                    $"World matrix bone index {boneIndex} is outside the Unity bone array.",
                    parameterName);
            }

            if (bones[boneIndex] == null)
            {
                throw new ArgumentException(
                    $"Unity bone transform {boneIndex} is missing.",
                    parameterName);
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
