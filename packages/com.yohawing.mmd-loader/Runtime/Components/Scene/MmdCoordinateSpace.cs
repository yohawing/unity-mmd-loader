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
    internal static class MmdCoordinateSpace
    {
        public static Vector3 MmdToUnityPosition(Vector3 mmd)
        {
            return new Vector3(-mmd.x, mmd.y, -mmd.z);
        }

        public static Vector3 MmdToUnityPosition(Vector3 mmd, float importScale)
        {
            return MmdToUnityPosition(mmd) * NormalizeImportScale(importScale);
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

        /// <summary>
        /// Converts PMX Euler angles (radians, MMD space) to an MMD-space rotation. PMX composes the
        /// axes as <c>qz * qy * qx</c>, the same order as the native PMX path (glam <c>EulerRot::ZYX</c>).
        /// Pure math so the physics descriptor-world fallback can call it off the main thread.
        /// </summary>
        public static Quaternion MmdEulerToRotation(float[]? rotation)
        {
            if (rotation == null || rotation.Length < 3)
            {
                return Quaternion.identity;
            }

            float cx = System.MathF.Cos(rotation[0] * 0.5f);
            float sx = System.MathF.Sin(rotation[0] * 0.5f);
            float cy = System.MathF.Cos(rotation[1] * 0.5f);
            float sy = System.MathF.Sin(rotation[1] * 0.5f);
            float cz = System.MathF.Cos(rotation[2] * 0.5f);
            float sz = System.MathF.Sin(rotation[2] * 0.5f);
            return new Quaternion(
                sx * cy * cz - cx * sy * sz,
                cx * sy * cz + sx * cy * sz,
                cx * cy * sz - sx * sy * cz,
                cx * cy * cz + sx * sy * sz);
        }

        /// <summary>
        /// Import scale used by runtime conversions: a finite positive value, otherwise 1 (MMD units).
        /// Asset-facing defaults that fall back to <see cref="MmdPmxAsset.DefaultImportScale"/> instead
        /// are intentionally separate.
        /// </summary>
        public static float NormalizeImportScale(float importScale)
        {
            return float.IsFinite(importScale) && importScale > 0.0f ? importScale : 1.0f;
        }

        /// <summary>Reads an MMD float triple as-is (no axis flip); null or short arrays read as zero.</summary>
        public static Vector3 ToVector3(float[]? values)
        {
            return values != null && values.Length >= 3
                ? new Vector3(values[0], values[1], values[2])
                : Vector3.zero;
        }
    }
}
