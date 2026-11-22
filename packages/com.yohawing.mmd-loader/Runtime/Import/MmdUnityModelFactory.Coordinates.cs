#nullable enable

using UnityEngine;

namespace Mmd.UnityIntegration
{
    public static partial class MmdUnityModelFactory
    {
        private static Vector3 ToUnityPosition(float[] position)
        {
            return ToUnityPosition(position, importScale: 1.0f);
        }

        private static Vector3 ToUnityPosition(float[] position, float importScale)
        {
            float scale = MmdCoordinateSpace.NormalizeImportScale(importScale);
            return MmdCoordinateSpace.MmdToUnityPosition(
                new Vector3(position[0], position[1], position[2]),
                scale);
        }

        private static Vector3 ToUnityPosition(Vector3 position)
        {
            return ToUnityPosition(position, importScale: 1.0f);
        }

        private static Vector3 ToUnityPosition(Vector3 position, float importScale)
        {
            float scale = MmdCoordinateSpace.NormalizeImportScale(importScale);
            return MmdCoordinateSpace.MmdToUnityPosition(position, scale);
        }

        private static Quaternion ToUnityModelRotation(Quaternion rotation)
        {
            return MmdCoordinateSpace.MmdToUnityRotation(rotation);
        }

        private static Vector3 ToUnityNormal(float[] normal)
        {
            return MmdCoordinateSpace.MmdToUnityPosition(new Vector3(normal[0], normal[1], normal[2]));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
