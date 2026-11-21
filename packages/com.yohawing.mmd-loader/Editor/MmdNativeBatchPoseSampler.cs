#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    /// <summary>
    /// Shared reads of mmd-runtime batch world matrices for the Generic and Humanoid clip writers.
    /// Time sampling, key writing and proxy application stay in each writer.
    /// </summary>
    internal static class MmdNativeBatchPoseSampler
    {
        internal static bool CanUseNativeBatch(
            MmdUnityPlaybackBinding binding,
            MmdUnityModelInstance instance,
            out int[] parentBoneIndices,
            out Vector3[] staticParentPositions,
            out Quaternion[] staticParentRotations,
            bool requireMorphCompatibility = true)
        {
            parentBoneIndices = Array.Empty<int>();
            staticParentPositions = Array.Empty<Vector3>();
            staticParentRotations = Array.Empty<Quaternion>();
            if (!binding.HasFastRuntimeBatch ||
                binding.FastRuntimeWorldMatrixFloatCount != instance.BoneTransforms.Length * 16 ||
                requireMorphCompatibility &&
                (binding.FastRuntimeMorphWeightCount <= 0 && instance.VertexMorphBlendShapes.Count > 0 ||
                 instance.RenderingDescriptor.flipMorphs.Count > 0))
            {
                return false;
            }

            var boneIndices = new Dictionary<Transform, int>(instance.BoneTransforms.Length);
            for (int bone = 0; bone < instance.BoneTransforms.Length; bone++)
            {
                boneIndices[instance.BoneTransforms[bone]] = bone;
            }

            parentBoneIndices = new int[instance.BoneTransforms.Length];
            staticParentPositions = new Vector3[instance.BoneTransforms.Length];
            staticParentRotations = new Quaternion[instance.BoneTransforms.Length];
            Transform root = instance.Root.transform;
            for (int bone = 0; bone < instance.BoneTransforms.Length; bone++)
            {
                Transform? parent = instance.BoneTransforms[bone].parent;
                if (parent != null && boneIndices.TryGetValue(parent, out int parentBone))
                {
                    parentBoneIndices[bone] = parentBone;
                }
                else if (parent != null && (parent == root || parent.IsChildOf(root)))
                {
                    parentBoneIndices[bone] = -1;
                    staticParentPositions[bone] = root.InverseTransformPoint(parent.position);
                    staticParentRotations[bone] = Quaternion.Inverse(root.rotation) * parent.rotation;
                }
                else
                {
                    parentBoneIndices = Array.Empty<int>();
                    staticParentPositions = Array.Empty<Vector3>();
                    staticParentRotations = Array.Empty<Quaternion>();
                    return false;
                }
            }

            return true;
        }

        internal static void ReadWorldPose(
            ReadOnlySpan<float> frameWorld,
            int bone,
            out Vector3 position,
            out Quaternion rotation)
        {
            int offset = bone * 16;
            position = MmdCoordinateSpace.MmdToUnityPosition(new Vector3(
                frameWorld[offset + 12],
                frameWorld[offset + 13],
                frameWorld[offset + 14]));
            Vector3 forward = new Vector3(
                frameWorld[offset + 8],
                frameWorld[offset + 9],
                frameWorld[offset + 10]);
            Vector3 up = new Vector3(
                frameWorld[offset + 4],
                frameWorld[offset + 5],
                frameWorld[offset + 6]);
            Quaternion mmdRotation = forward.sqrMagnitude > 0.0f && up.sqrMagnitude > 0.0f
                ? Quaternion.LookRotation(forward.normalized, up.normalized)
                : Quaternion.identity;
            rotation = MmdCoordinateSpace.MmdToUnityRotation(mmdRotation);
        }
    }
}
