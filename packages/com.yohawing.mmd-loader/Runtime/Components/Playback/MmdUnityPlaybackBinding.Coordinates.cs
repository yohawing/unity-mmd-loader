#nullable enable

using Mmd.Motion;
using Mmd.Parser;
using Mmd.Pose;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public sealed partial class MmdUnityPlaybackBinding
    {
        private void RefreshEvaluatedFrameFromUnityTransforms(MmdEvaluatedFrame frame)
        {
            Transform root = playbackInstance.Root.transform;
            float importScale = NormalizeImportScale(playbackInstance.ImportScale);
            foreach (MmdEvaluatedBonePose bonePose in frame.bones)
            {
                int index = bonePose.index;
                if (index < 0 || index >= playbackInstance.BoneTransforms.Length)
                {
                    continue;
                }

                Transform bone = playbackInstance.BoneTransforms[index];
                Vector3 localDelta = bone.localPosition - playbackInstance.BindLocalPositions[index];
                Quaternion localRotation = Quaternion.Inverse(playbackInstance.BindLocalRotations[index]) * bone.localRotation;
                bonePose.localPosition = ToArray(ToMmdModelPosition(localDelta, importScale));
                bonePose.localRotation = ToArray(ToMmdModelRotation(localRotation));
                bonePose.localScale = ToArray(bone.localScale);
                bonePose.worldMatrix = ToMmdModelMatrix(root, bone, importScale);
            }
        }

        private static Vector3 ToUnityModelPosition(float[] position)
        {
            return MmdCoordinateSpace.MmdToUnityPosition(new Vector3(position[0], position[1], position[2]));
        }

        private static Vector3 ToUnityModelPosition(float[] position, float importScale)
        {
            return ToUnityModelPosition(position) * NormalizeImportScale(importScale);
        }

        private static Vector3 ToUnityModelPosition(Vector3 position)
        {
            return MmdCoordinateSpace.MmdToUnityPosition(position);
        }

        private static Vector3 ToUnityModelPosition(Vector3 position, float importScale)
        {
            return ToUnityModelPosition(position) * NormalizeImportScale(importScale);
        }

        private static Quaternion ToUnityModelRotation(float[] rotation)
        {
            return MmdCoordinateSpace.MmdToUnityRotation(new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]));
        }

        private static Quaternion ToUnityModelRotation(Quaternion rotation)
        {
            return MmdCoordinateSpace.MmdToUnityRotation(rotation);
        }

        private static Vector3 ToMmdModelPosition(Vector3 position)
        {
            return MmdCoordinateSpace.UnityToMmdPosition(position);
        }

        private static Vector3 ToMmdModelPosition(Vector3 position, float importScale)
        {
            return ToMmdModelPosition(position) / NormalizeImportScale(importScale);
        }

        private static Quaternion ToMmdModelRotation(Quaternion rotation)
        {
            return MmdCoordinateSpace.UnityToMmdRotation(rotation);
        }

        private static Quaternion ToMmdQuaternion(float[] rotation)
        {
            return new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
        }

        private static Quaternion ToMmdEulerRotation(float[] rotation)
        {
            if (rotation == null || rotation.Length < 3)
            {
                return Quaternion.identity;
            }

            Quaternion rotateX = Quaternion.AngleAxis(rotation[0] * Mathf.Rad2Deg, Vector3.right);
            Quaternion rotateY = Quaternion.AngleAxis(rotation[1] * Mathf.Rad2Deg, Vector3.up);
            Quaternion rotateZ = Quaternion.AngleAxis(rotation[2] * Mathf.Rad2Deg, Vector3.forward);
            return rotateZ * rotateY * rotateX;
        }

        private Vector3 GetBoneOrigin(int boneIndex)
        {
            for (int i = 0; i < model.bones.Count; i++)
            {
                MmdBoneDefinition bone = model.bones[i];
                if (bone.index == boneIndex)
                {
                    return ToMmdVector3(bone.origin);
                }
            }

            return Vector3.zero;
        }

        private static Vector3 ToMmdVector3(float[] values)
        {
            if (values == null || values.Length < 3)
            {
                return Vector3.zero;
            }

            return new Vector3(values[0], values[1], values[2]);
        }

        private static float[] ToArray(Vector3 value)
        {
            return new[] { value.x, value.y, value.z };
        }

        private static float[] ToArray(Quaternion value)
        {
            return new[] { value.x, value.y, value.z, value.w };
        }

        private static float[] ToMmdModelMatrix(Transform root, Transform bone)
        {
            return ToMmdModelMatrix(root, bone, importScale: 1.0f);
        }

        private static float[] ToMmdModelMatrix(Transform root, Transform bone, float importScale)
        {
            Vector3 position = ToMmdModelPosition(root.InverseTransformPoint(bone.position), importScale);
            Quaternion rotation = ToMmdModelRotation(Quaternion.Inverse(root.rotation) * bone.rotation);
            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            return new[]
            {
                matrix.m00, matrix.m10, matrix.m20, matrix.m30,
                matrix.m01, matrix.m11, matrix.m21, matrix.m31,
                matrix.m02, matrix.m12, matrix.m22, matrix.m32,
                matrix.m03, matrix.m13, matrix.m23, matrix.m33
            };
        }

        private static float NormalizeImportScale(float importScale)
        {
            return float.IsFinite(importScale) && importScale > 0.0f ? importScale : 1.0f;
        }
    }
}
