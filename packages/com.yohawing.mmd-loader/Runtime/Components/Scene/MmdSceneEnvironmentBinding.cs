#nullable enable

using UnityEngine;
using Mmd.Motion;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// Result of applying an MMD camera state to the bound Scene Camera.
    /// </summary>
    public enum MmdSceneCameraApplyStatus
    {
        /// <summary>Default before any apply call — distinguishes "never applied" from "applied".</summary>
        NotApplied = 0,

        /// <summary>The state was applied to a perspective camera.</summary>
        Applied = 1,

        /// <summary>No target Camera is bound; nothing was applied (structured diagnostic).</summary>
        NoTargetCamera = 2,

        /// <summary>
        /// The state requested an orthographic projection (MMD perspective flag off). v1 keeps the
        /// camera in perspective and reports this; orthographic switching is a follow-up slice.
        /// </summary>
        AppliedOrthographicNotSupported = 3
    }

    /// <summary>
    /// Result of applying an MMD light state to the bound Scene Directional Light.
    /// </summary>
    public enum MmdSceneLightApplyStatus
    {
        /// <summary>Default before any apply call — distinguishes "never applied" from "applied".</summary>
        NotApplied = 0,

        /// <summary>The state was applied to a Directional Light.</summary>
        Applied = 1,

        /// <summary>No target Light is bound; nothing was applied (structured diagnostic).</summary>
        NoTargetLight = 2,

        /// <summary>
        /// Color and direction were applied, but the bound light is not a
        /// <see cref="LightType.Directional"/> light. MMD light is a single directional source, so its
        /// semantics do not fully map to point / spot / area lights (e.g. a point light ignores rotation).
        /// v1 still applies and reports this so the caller can surface a warning rather than silently
        /// driving a mismatched light.
        /// </summary>
        AppliedNonDirectional = 3
    }

    /// <summary>
    /// The "scene environment" proxy a VMD camera (and later light / self-shadow) track binds to on
    /// a Timeline. It does NOT own a Camera — it points at an existing Scene <see cref="Camera"/> and
    /// drives it. Nothing is auto-created; an unbound target is surfaced as a structured diagnostic
    /// (<see cref="MmdSceneCameraApplyStatus.NoTargetCamera"/>) rather than silently spawning a camera.
    /// </summary>
    public sealed class MmdSceneEnvironmentBinding : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The Scene Camera that VMD camera motion drives. Required — nothing is auto-created.")]
        private Camera? targetCamera;

        [SerializeField]
        [Tooltip("The Directional Light that VMD light motion drives. Optional; nothing is auto-created.")]
        private Light? targetLight;

        public Camera? TargetCamera
        {
            get => targetCamera;
            set => targetCamera = value;
        }

        public Light? TargetLight
        {
            get => targetLight;
            set => targetLight = value;
        }

        /// <summary>The status of the most recent <see cref="ApplyCameraState"/> call.</summary>
        public MmdSceneCameraApplyStatus LastCameraApplyStatus { get; private set; }

        /// <summary>The status of the most recent <see cref="ApplyLightState"/> call.</summary>
        public MmdSceneLightApplyStatus LastLightApplyStatus { get; private set; }

        /// <summary>
        /// Converts <paramref name="state"/> (via <see cref="MmdCameraStateToUnity"/>) and applies it to
        /// the bound Camera's transform and field of view. Returns a structured status; never throws on
        /// a missing target.
        /// </summary>
        public MmdSceneCameraApplyStatus ApplyCameraState(
            MmdCameraState state,
            float minFieldOfView = MmdCameraStateToUnity.DefaultMinFieldOfView)
        {
            if (targetCamera == null)
            {
                LastCameraApplyStatus = MmdSceneCameraApplyStatus.NoTargetCamera;
                return LastCameraApplyStatus;
            }

            MmdUnityCameraPose pose = MmdCameraStateToUnity.Convert(state, minFieldOfView);
            targetCamera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);

            // v1 is perspective-only. An orthographic (perspective-off) key keeps the camera in
            // perspective; the caller can react to the returned status. The orthographic switch is
            // a follow-up slice.
            targetCamera.orthographic = false;
            targetCamera.fieldOfView = pose.FieldOfView;

            LastCameraApplyStatus = pose.Perspective
                ? MmdSceneCameraApplyStatus.Applied
                : MmdSceneCameraApplyStatus.AppliedOrthographicNotSupported;
            return LastCameraApplyStatus;
        }

        /// <summary>
        /// Converts <paramref name="state"/> (via <see cref="MmdCoordinateSpace"/>) and applies it to
        /// the bound light's color and direction. Returns a structured status; never throws on a missing
        /// target. v1 policy: <see cref="Light.intensity"/> is left unchanged (VMD light carries color +
        /// direction only). A bound light that is not <see cref="LightType.Directional"/> still receives
        /// color / direction but is reported as <see cref="MmdSceneLightApplyStatus.AppliedNonDirectional"/>
        /// so the caller can warn (MMD light maps to a directional source).
        /// </summary>
        public MmdSceneLightApplyStatus ApplyLightState(MmdLightState state)
        {
            if (targetLight == null)
            {
                LastLightApplyStatus = MmdSceneLightApplyStatus.NoTargetLight;
                return LastLightApplyStatus;
            }

            targetLight.color = new Color(
                Mathf.Clamp01(Component(state.Color, 0)),
                Mathf.Clamp01(Component(state.Color, 1)),
                Mathf.Clamp01(Component(state.Color, 2)));

            Vector3 mmdDir = new Vector3(
                Component(state.Direction, 0),
                Component(state.Direction, 1),
                Component(state.Direction, 2));
            Vector3 unityDir = MmdCoordinateSpace.MmdToUnityPosition(mmdDir);
            if (unityDir.sqrMagnitude > 1e-12f)
            {
                // A Directional Light only uses its forward axis; roll about that axis has no lighting
                // effect, so LookRotation's default up is fine even for a near-vertical light direction.
                targetLight.transform.rotation = Quaternion.LookRotation(unityDir);
            }

            LastLightApplyStatus = targetLight.type == LightType.Directional
                ? MmdSceneLightApplyStatus.Applied
                : MmdSceneLightApplyStatus.AppliedNonDirectional;
            return LastLightApplyStatus;
        }

        private static float Component(float[] values, int index)
        {
            return values != null && values.Length > index && float.IsFinite(values[index]) ? values[index] : 0f;
        }
    }
}
