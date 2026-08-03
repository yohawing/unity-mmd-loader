#nullable enable

using Mmd.Motion;
using Mmd.Parser;
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
                bonePose.localPosition = WriteVector3(
                    bonePose.localPosition,
                    ToMmdModelPosition(localDelta, importScale));
                bonePose.localRotation = WriteQuaternion(
                    bonePose.localRotation,
                    ToMmdModelRotation(localRotation));
                bonePose.localScale = WriteVector3(bonePose.localScale, bone.localScale);
                bonePose.worldMatrix = WriteMmdModelMatrix(
                    bonePose.worldMatrix,
                    root,
                    bone,
                    importScale);
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

        private static float[] WriteVector3(float[]? destination, Vector3 value)
        {
            destination ??= new float[3];
            if (destination.Length < 3)
            {
                destination = new float[3];
            }

            destination[0] = value.x;
            destination[1] = value.y;
            destination[2] = value.z;
            return destination;
        }

        private static float[] WriteQuaternion(float[]? destination, Quaternion value)
        {
            destination ??= new float[4];
            if (destination.Length < 4)
            {
                destination = new float[4];
            }

            destination[0] = value.x;
            destination[1] = value.y;
            destination[2] = value.z;
            destination[3] = value.w;
            return destination;
        }

        private static float[] WriteMmdModelMatrix(
            float[]? destination,
            Transform root,
            Transform bone,
            float importScale)
        {
            destination ??= new float[16];
            if (destination.Length < 16)
            {
                destination = new float[16];
            }

            Vector3 position = ToMmdModelPosition(root.InverseTransformPoint(bone.position), importScale);
            Quaternion rotation = ToMmdModelRotation(Quaternion.Inverse(root.rotation) * bone.rotation);
            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            destination[0] = matrix.m00;
            destination[1] = matrix.m10;
            destination[2] = matrix.m20;
            destination[3] = matrix.m30;
            destination[4] = matrix.m01;
            destination[5] = matrix.m11;
            destination[6] = matrix.m21;
            destination[7] = matrix.m31;
            destination[8] = matrix.m02;
            destination[9] = matrix.m12;
            destination[10] = matrix.m22;
            destination[11] = matrix.m32;
            destination[12] = matrix.m03;
            destination[13] = matrix.m13;
            destination[14] = matrix.m23;
            destination[15] = matrix.m33;
            return destination;
        }

        private static float NormalizeImportScale(float importScale)
        {
            return float.IsFinite(importScale) && importScale > 0.0f ? importScale : 1.0f;
        }
    }
}
