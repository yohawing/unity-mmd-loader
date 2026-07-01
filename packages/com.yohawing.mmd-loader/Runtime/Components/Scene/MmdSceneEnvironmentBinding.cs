#nullable enable

using System;
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
    /// Result of recording an MMD self-shadow state on the scene environment binding.
    /// </summary>
    public enum MmdSceneSelfShadowApplyStatus
    {
        /// <summary>Default before any apply call — distinguishes "never applied" from "applied".</summary>
        NotApplied = 0,

        /// <summary>Self-shadow state recording is disabled by policy; no active state was recorded.</summary>
        Disabled = 1,

        /// <summary>The sampled self-shadow state was recorded on the binding.</summary>
        Recorded = 2,

        /// <summary>Compatibility alias. Self-shadow is now recorded as scene state and does not mutate Light.</summary>
        [Obsolete("Use Recorded. Self-shadow is now scene/render state and no longer mutates Light.", false)]
        Applied = Recorded,

        /// <summary>Compatibility value. Missing Light is no longer relevant to self-shadow state recording.</summary>
        [Obsolete("Self-shadow no longer targets Light; use SelfShadowEnabled and Recorded/Disabled status.", false)]
        NoTargetLight = 3,

        /// <summary>Compatibility value. Light type is no longer relevant to self-shadow state recording.</summary>
        [Obsolete("Self-shadow no longer targets Light; use SelfShadowEnabled and Recorded/Disabled status.", false)]
        UnsupportedLightType = 4
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

        [SerializeField]
        // Do not migrate the old Light-application flag; state recording defaults on for existing bindings too.
        [Tooltip("Records sampled VMD self-shadow as MMD scene/render state. Disable to ignore sampled self-shadow keys.")]
        private bool selfShadowEnabled = true;

        private Vector3 lastUnityLightDirection;
        private bool hasLastUnityLightDirection;

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

        public bool SelfShadowEnabled
        {
            get => selfShadowEnabled;
            set => selfShadowEnabled = value;
        }

        [Obsolete("Use SelfShadowEnabled. Self-shadow no longer drives Unity Light shadows.", false)]
        public bool ApplySelfShadowToLight
        {
            get => selfShadowEnabled;
            set => selfShadowEnabled = value;
        }

        [Obsolete("Self-shadow distance is now recorded from VMD state; Light shadow-distance mapping is not applied.", false)]
        public float SelfShadowDistanceScale
        {
            get => 100.0f;
            set { }
        }

        [Obsolete("Self-shadow distance is now recorded from VMD state; Light shadow-distance mapping is not applied.", false)]
        public float SelfShadowMinDistance
        {
            get => 1.0f;
            set { }
        }

        [Obsolete("Self-shadow distance is now recorded from VMD state; Light shadow-distance mapping is not applied.", false)]
        public float SelfShadowMaxDistance
        {
            get => 100.0f;
            set { }
        }

        [Obsolete("Self-shadow no longer drives Unity Light shadowStrength.", false)]
        public float SelfShadowStrength
        {
            get => 1.0f;
            set { }
        }

        [Obsolete("Self-shadow no longer drives Unity Light.shadows.", false)]
        public LightShadows SelfShadowLightMode
        {
            get => LightShadows.Soft;
            set { }
        }

        /// <summary>The status of the most recent <see cref="ApplyCameraState"/> call.</summary>
        public MmdSceneCameraApplyStatus LastCameraApplyStatus { get; private set; }

        /// <summary>The status of the most recent <see cref="ApplyLightState"/> call.</summary>
        public MmdSceneLightApplyStatus LastLightApplyStatus { get; private set; }

        /// <summary>
        /// Returns the most recent Unity-space MMD light direction. If no VMD light state has been
        /// sampled yet, a bound Directional Light is used as an authoring fallback. This is read-only
        /// scene state for MMD lighting/self-shadow and does not imply Unity shadow usage.
        /// </summary>
        public bool TryGetLastUnityLightDirection(out Vector3 direction)
        {
            if (targetLight != null &&
                targetLight.type == LightType.Directional &&
                targetLight.isActiveAndEnabled &&
                IsUsableDirection(targetLight.transform.forward))
            {
                direction = targetLight.transform.forward.normalized;
                return true;
            }

            if (IsUsableDirection(lastUnityLightDirection) && hasLastUnityLightDirection)
            {
                direction = lastUnityLightDirection.normalized;
                return true;
            }

            direction = default;
            return false;
        }

        /// <summary>The status of the most recent <see cref="ApplySelfShadowState"/> call.</summary>
        public MmdSceneSelfShadowApplyStatus LastSelfShadowApplyStatus { get; private set; }

        /// <summary>The most recent active MMD self-shadow scene/render state.</summary>
        public MmdSelfShadowState LastSelfShadowState { get; private set; }

        /// <summary>Compatibility diagnostic mirror. It no longer represents values applied to a Unity Light.</summary>
        public MmdSelfShadowUnityShadowSettings LastSelfShadowSettings { get; private set; }

        /// <summary>Pure MMD self-shadow projection/far policy used for future dedicated shadow texture setup.</summary>
        public MmdSelfShadowProjectionPolicy SelfShadowProjectionPolicy { get; set; } =
            MmdSelfShadowProjectionPolicy.Default;

        /// <summary>The most recent dedicated MMD self-shadow projection/far state.</summary>
        public MmdSelfShadowProjectionState LastSelfShadowProjectionState { get; private set; }

        /// <summary>
        /// Converts <paramref name="state"/> (via <see cref="MmdCameraStateToUnity"/>) and applies it to
        /// the bound Camera's transform and field of view. Returns a structured status; never throws on
        /// a missing target.
        /// </summary>
        public MmdSceneCameraApplyStatus ApplyCameraState(
            MmdCameraState state,
            float minFieldOfView = MmdCameraStateToUnity.DefaultMinFieldOfView,
            float importScale = 1.0f)
        {
            if (targetCamera == null)
            {
                LastCameraApplyStatus = MmdSceneCameraApplyStatus.NoTargetCamera;
                return LastCameraApplyStatus;
            }

            MmdUnityCameraPose pose = MmdCameraStateToUnity.Convert(state, minFieldOfView, importScale);
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
            Vector3 mmdDir = new Vector3(
                Component(state.Direction, 0),
                Component(state.Direction, 1),
                Component(state.Direction, 2));
            Vector3 unityDir = MmdCoordinateSpace.MmdToUnityPosition(mmdDir);
            hasLastUnityLightDirection = IsUsableDirection(unityDir);
            lastUnityLightDirection = hasLastUnityLightDirection ? unityDir.normalized : default;

            if (targetLight == null)
            {
                LastLightApplyStatus = MmdSceneLightApplyStatus.NoTargetLight;
                return LastLightApplyStatus;
            }

            targetLight.color = new Color(
                Mathf.Clamp01(Component(state.Color, 0)),
                Mathf.Clamp01(Component(state.Color, 1)),
                Mathf.Clamp01(Component(state.Color, 2)));

            if (hasLastUnityLightDirection)
            {
                // A Directional Light only uses its forward axis; roll about that axis has no lighting
                // effect, so LookRotation's default up is fine even for a near-vertical light direction.
                targetLight.transform.rotation = Quaternion.LookRotation(lastUnityLightDirection);
            }

            LastLightApplyStatus = targetLight.type == LightType.Directional
                ? MmdSceneLightApplyStatus.Applied
                : MmdSceneLightApplyStatus.AppliedNonDirectional;
            return LastLightApplyStatus;
        }

        /// <summary>
        /// Records <paramref name="state"/> as MMD scene/render state. This method intentionally does
        /// not mutate Light.shadows, Light.shadowStrength, RenderSettings, QualitySettings shadow
        /// distance, URP assets, or Materials.
        /// </summary>
        public MmdSceneSelfShadowApplyStatus ApplySelfShadowState(MmdSelfShadowState state)
        {
            if (!selfShadowEnabled)
            {
                LastSelfShadowState = MmdSelfShadowState.Default;
                LastSelfShadowProjectionState = MmdSelfShadowProjectionState.Inactive;
                LastSelfShadowSettings = new MmdSelfShadowUnityShadowSettings(
                    runtimeApplicationEnabled: false,
                    castShadows: false,
                    mode: state.Mode,
                    shadowDistance: 0.0f,
                    shadowStrength: 0.0f);
                LastSelfShadowApplyStatus = MmdSceneSelfShadowApplyStatus.Disabled;
                return LastSelfShadowApplyStatus;
            }

            LastSelfShadowState = state;
            LastSelfShadowProjectionState = SelfShadowProjectionPolicy.Evaluate(state);
            LastSelfShadowSettings = new MmdSelfShadowUnityShadowSettings(
                runtimeApplicationEnabled: false,
                castShadows: false,
                mode: state.Mode,
                shadowDistance: 0.0f,
                shadowStrength: 0.0f);
            LastSelfShadowApplyStatus = MmdSceneSelfShadowApplyStatus.Recorded;
            return LastSelfShadowApplyStatus;
        }

        private static float Component(float[] values, int index)
        {
            return values != null && values.Length > index && float.IsFinite(values[index]) ? values[index] : 0f;
        }

        private static bool IsUsableDirection(Vector3 direction)
        {
            return float.IsFinite(direction.x) &&
                float.IsFinite(direction.y) &&
                float.IsFinite(direction.z) &&
                direction.sqrMagnitude > 1e-12f;
        }
    }
}
