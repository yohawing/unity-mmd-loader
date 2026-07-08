#nullable enable

using System;
using UnityEngine;
using Mmd.Motion;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// A camera pose in Unity space: world position, world rotation, vertical field of view
    /// (degrees), and the MMD perspective flag. Produced by <see cref="MmdCameraStateToUnity"/>.
    /// </summary>
    public readonly struct MmdUnityCameraPose
    {
        public MmdUnityCameraPose(Vector3 position, Quaternion rotation, float fieldOfView, bool perspective)
        {
            Position = position;
            Rotation = rotation;
            FieldOfView = fieldOfView;
            Perspective = perspective;
        }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public float FieldOfView { get; }

        public bool Perspective { get; }
    }

    /// <summary>
    /// Converts an MMD-space <see cref="MmdCameraState"/> (look-at + distance rig) into a Unity
    /// <see cref="MmdUnityCameraPose"/>.
    ///
    /// MMD cameras use a target + signed distance rig. The conversion mirrors the mmd-anim backed
    /// Three.js application path: target is mapped into Unity's model space, MMD camera Euler angles
    /// are applied as (-x, -y, -z) in YXZ order, and the signed distance offset is transformed by
    /// that camera quaternion. This keeps camera motion sampling independent from Unity while
    /// matching the runtime path used by adjacent mmd-anim consumers.
    ///
    /// Distance / look-at / FOV / perspective are exact. The Euler rotation order/sign is taken to
    /// match MMD's YXZ order (Unity's <see cref="Quaternion.Euler(float,float,float)"/> applies
    /// Z, X, Y in that order); the pan/tilt/roll SIGN is pinned by tests but confirmed visually when
    /// the Timeline application slice drives a real Scene Camera.
    ///
    /// Perspective-off (orthographic) handling is NOT applied here (v1 is perspective-only); callers
    /// inspect <see cref="MmdUnityCameraPose.Perspective"/> and diagnose an orthographic request.
    /// </summary>
    public static class MmdCameraStateToUnity
    {
        public const float DefaultMinFieldOfView = 1.0f;

        public static MmdUnityCameraPose Convert(
            MmdCameraState state,
            float minFieldOfView = DefaultMinFieldOfView,
            float importScale = 1.0f)
        {
            float[] position = state.Position ?? Array.Empty<float>();
            float[] rotation = state.Rotation ?? Array.Empty<float>();
            float scale = NormalizeImportScale(importScale);

            // Guard the scalar inputs the same way Component() guards the array inputs, so a
            // non-finite upstream value never produces a non-finite Unity pose.
            float distance = float.IsFinite(state.Distance) ? state.Distance : 0.0f;

            Vector3 targetMmd = new Vector3(
                Component(position, 0),
                Component(position, 1),
                Component(position, 2));
            Quaternion cameraRotation = MmdCameraEulerToQuaternion(
                Component(rotation, 0),
                Component(rotation, 1),
                Component(rotation, 2));

            Vector3 offset = cameraRotation * new Vector3(0.0f, 0.0f, distance);
            Vector3 targetUnity = MmdCoordinateSpace.MmdToUnityPosition(targetMmd);
            Vector3 positionUnity = new Vector3(
                targetUnity.x - offset.x,
                targetUnity.y + offset.y,
                targetUnity.z - offset.z) * scale;

            Vector3 look = cameraRotation * Vector3.forward;
            Vector3 forwardUnity = MmdCoordinateSpace.MmdToUnityPosition(look).normalized;
            Vector3 up = cameraRotation * Vector3.up;
            Vector3 upUnity = MmdCoordinateSpace.MmdToUnityPosition(up).normalized;
            Quaternion rotationUnity = Quaternion.LookRotation(forwardUnity, upUnity);

            float fieldOfView = float.IsFinite(state.ViewAngle)
                ? Mathf.Max(state.ViewAngle, minFieldOfView)
                : minFieldOfView;
            return new MmdUnityCameraPose(positionUnity, rotationUnity, fieldOfView, state.Perspective);
        }

        private static float Component(float[] values, int index)
        {
            return values != null && values.Length > index && float.IsFinite(values[index]) ? values[index] : 0.0f;
        }

        private static Quaternion MmdCameraEulerToQuaternion(float x, float y, float z)
        {
            // Mirrors three-mmd-loader's Euler(-x, -y, -z, "YXZ") camera convention.
            return
                Quaternion.AngleAxis(-y * Mathf.Rad2Deg, Vector3.up) *
                Quaternion.AngleAxis(-x * Mathf.Rad2Deg, Vector3.right) *
                Quaternion.AngleAxis(-z * Mathf.Rad2Deg, Vector3.forward);
        }

        private static float NormalizeImportScale(float importScale)
        {
            return float.IsFinite(importScale) && importScale > 0.0f ? importScale : 1.0f;
        }
    }
}
