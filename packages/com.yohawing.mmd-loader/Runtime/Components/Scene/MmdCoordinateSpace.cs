#nullable enable

using UnityEngine;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// The single source of truth for the MMD ↔ Unity coordinate convention used across this package.
    ///
    /// MMD and Unity are both left-handed, Y-up. This package maps between them with a 180° rotation
    /// about Y: position <c>(x, y, z) → (-x, y, -z)</c> and rotation <c>(x, y, z, w) → (-x, y, -z, w)</c>.
    /// The map is its own inverse. Several call sites historically hand-rolled this same flip
    /// (mesh / bone / physics / humanoid / camera); new code should call these helpers so the
    /// convention stays consistent in one place.
    /// </summary>
    public static class MmdCoordinateSpace
    {
        public static Vector3 MmdToUnityPosition(Vector3 mmd)
        {
            return new Vector3(-mmd.x, mmd.y, -mmd.z);
        }

        public static Vector3 MmdToUnityPosition(Vector3 mmd, float importScale)
        {
            return MmdToUnityPosition(mmd) * NormalizeScale(importScale);
        }

        public static Quaternion MmdToUnityRotation(Quaternion mmd)
        {
            return new Quaternion(-mmd.x, mmd.y, -mmd.z, mmd.w);
        }

        public static Vector3 UnityToMmdPosition(Vector3 unity)
        {
            // The 180°-about-Y map is its own inverse.
            return new Vector3(-unity.x, unity.y, -unity.z);
        }

        public static Quaternion UnityToMmdRotation(Quaternion unity)
        {
            return new Quaternion(-unity.x, unity.y, -unity.z, unity.w);
        }

        private static float NormalizeScale(float importScale)
        {
            return float.IsFinite(importScale) && importScale > 0.0f ? importScale : 1.0f;
        }
    }
}
