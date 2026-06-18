#nullable enable

using System;
using UnityEngine;
using Yohawing.MmdUnity.Motion;

namespace Yohawing.MmdUnity.UnityIntegration
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
    /// MMD cameras follow MikuMikuDance's view convention: the view matrix is
    /// <c>Translate(0,0,distance) · Rotate(euler) · Translate(-target)</c>, so the eye sits at
    /// <c>target + R⁻¹ · (0,0,-distance)</c> and the camera looks at <paramref name="target"/>.
    /// The MMD-space pose is then mapped to Unity with the SAME convention this package uses for
    /// bones (<see cref="MmdUnityFrameApplier"/>): a 180° rotation about Y, i.e. position
    /// (-x, y, -z) and rotation (-x, y, -z, w). Because that map sends the MMD camera's local
    /// forward (-Z) to Unity's local forward (+Z), the resulting Unity camera looks at the target
    /// without any extra fix-up, and stays consistent with how the model bones are placed.
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

        // 180° about Y. The MMD camera looks toward the target along the rotated axis; because Unity's
        // camera looks down +Z (opposite three.js' -Z), this flip re-orients the Unity camera back
        // toward the target. It is applied for the usual negative MMD distance (camera placed in front
        // of the target). See the golden tests for the pinned per-channel behavior.
        private static readonly Quaternion ViewDirectionFlip = new Quaternion(0.0f, 1.0f, 0.0f, 0.0f);

        public static MmdUnityCameraPose Convert(MmdCameraState state, float minFieldOfView = DefaultMinFieldOfView)
        {
            float[] position = state.Position ?? Array.Empty<float>();
            float[] rotation = state.Rotation ?? Array.Empty<float>();

            // Guard the scalar inputs the same way Component() guards the array inputs, so a
            // non-finite upstream value never produces a non-finite Unity pose.
            float distance = float.IsFinite(state.Distance) ? state.Distance : 0.0f;

            Vector3 targetMmd = new Vector3(
                Component(position, 0),
                Component(position, 1),
                Component(position, 2));

            // R = Rotate(euler) in MMD's YXZ order. Unity's Quaternion.Euler applies Z, then X,
            // then Y, which matches MMD's camera rotation order.
            Quaternion mmdRotation = Quaternion.Euler(
                Component(rotation, 0) * Mathf.Rad2Deg,
                Component(rotation, 1) * Mathf.Rad2Deg,
                Component(rotation, 2) * Mathf.Rad2Deg);

            // Camera world orientation = R⁻¹; eye = target + R⁻¹ · (0,0,distance). MMD distance is
            // signed (typically negative), placing the eye on the side that, after the MMD→Unity map,
            // sits in front of the model so the camera frames its face.
            Quaternion cameraOrientationMmd = Quaternion.Inverse(mmdRotation);
            Vector3 eyeMmd = targetMmd + cameraOrientationMmd * new Vector3(0.0f, 0.0f, distance);

            if (distance < 0.0f)
            {
                cameraOrientationMmd *= ViewDirectionFlip;
            }

            Vector3 positionUnity = MmdCoordinateSpace.MmdToUnityPosition(eyeMmd);
            Quaternion rotationUnity = MmdCoordinateSpace.MmdToUnityRotation(cameraOrientationMmd);

            float fieldOfView = float.IsFinite(state.ViewAngle)
                ? Mathf.Max(state.ViewAngle, minFieldOfView)
                : minFieldOfView;
            return new MmdUnityCameraPose(positionUnity, rotationUnity, fieldOfView, state.Perspective);
        }

        private static float Component(float[] values, int index)
        {
            return values != null && values.Length > index && float.IsFinite(values[index]) ? values[index] : 0.0f;
        }
    }
}
