#nullable enable

using UnityEngine;

namespace Yohawing.MmdUnity.UnityIntegration
{
    public static partial class MmdUnityModelFactory
    {
        private static Vector3 ToUnityPosition(float[] position)
        {
            return ToUnityPosition(position, importScale: 1.0f);
        }

        private static Vector3 ToUnityPosition(float[] position, float importScale)
        {
            float scale = NormalizeImportScale(importScale);
            return new Vector3(-position[0], position[1], -position[2]) * scale;
        }

        private static Vector3 ToUnityPosition(Vector3 position)
        {
            return ToUnityPosition(position, importScale: 1.0f);
        }

        private static Vector3 ToUnityPosition(Vector3 position, float importScale)
        {
            float scale = NormalizeImportScale(importScale);
            return new Vector3(-position.x, position.y, -position.z) * scale;
        }

        private static Quaternion ToUnityModelRotation(Quaternion rotation)
        {
            return new Quaternion(-rotation.x, rotation.y, -rotation.z, rotation.w);
        }

        private static Vector3 ToUnityNormal(float[] normal)
        {
            return new Vector3(-normal[0], normal[1], -normal[2]);
        }

        private static Vector3 ToVector3(float[] values)
        {
            if (values == null || values.Length < 3)
            {
                return Vector3.zero;
            }

            return new Vector3(values[0], values[1], values[2]);
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

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float NormalizeImportScale(float value)
        {
            return float.IsFinite(value) && value > 0.0f ? value : 1.0f;
        }
    }
}
