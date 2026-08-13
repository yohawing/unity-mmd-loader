#nullable enable

using System.Collections.Generic;
using Mmd.Motion;
using Unity.Cinemachine;
using UnityEngine;

namespace Mmd.UnityIntegration.Cinemachine
{
    /// <summary>
    /// Optional Cinemachine 3 camera writer for VMD camera motion. The user supplies a passive
    /// <see cref="CinemachineCamera"/>; this component never creates or mutates an output Camera.
    /// </summary>
    [AddComponentMenu("MMD/Scene/MMD Cinemachine Camera Binding")]
    public sealed class MmdCinemachineCameraBinding : MmdSceneEnvironmentBinding
    {
        private readonly List<CinemachineComponentBase> pipelineComponents = new();

        [SerializeField]
        [Tooltip("The passive Cinemachine Camera that VMD camera motion drives. Required; nothing is auto-created.")]
        private CinemachineCamera? targetCinemachineCamera;

        public CinemachineCamera? TargetCinemachineCamera
        {
            get => targetCinemachineCamera;
            set => targetCinemachineCamera = value;
        }

        public override MmdSceneCameraApplyStatus ApplyCameraState(
            MmdCameraState state,
            float minFieldOfView = MmdCameraStateToUnity.DefaultMinFieldOfView,
            float importScale = 1.0f)
        {
            if (TargetCamera != null && targetCinemachineCamera != null)
            {
                return SetLastCameraApplyStatus(MmdSceneCameraApplyStatus.ConflictingCameraTargets);
            }

            if (targetCinemachineCamera == null)
            {
                return SetLastCameraApplyStatus(MmdSceneCameraApplyStatus.NoTargetCamera);
            }

            if (HasCompetingProceduralAuthority(targetCinemachineCamera))
            {
                return SetLastCameraApplyStatus(MmdSceneCameraApplyStatus.ProceduralCameraAuthorityConflict);
            }

            MmdUnityCameraPose pose = MmdCameraStateToUnity.Convert(state, minFieldOfView, importScale);
            targetCinemachineCamera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);

            LensSettings lens = targetCinemachineCamera.Lens;
            lens.FieldOfView = pose.FieldOfView;
            lens.ModeOverride = LensSettings.OverrideModes.Perspective;
            targetCinemachineCamera.Lens = lens;

            return SetLastCameraApplyStatus(pose.Perspective
                ? MmdSceneCameraApplyStatus.Applied
                : MmdSceneCameraApplyStatus.AppliedOrthographicNotSupported);
        }

        private bool HasCompetingProceduralAuthority(CinemachineCamera camera)
        {
            if (camera.Follow != null || camera.LookAt != null)
            {
                return true;
            }

            pipelineComponents.Clear();
            camera.GetComponents(pipelineComponents);
            for (int i = 0; i < pipelineComponents.Count; i++)
            {
                if (pipelineComponents[i].enabled && pipelineComponents[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
