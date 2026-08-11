#nullable enable

using Mmd.Motion;
using Mmd.UnityIntegration;
using Mmd.UnityIntegration.Cinemachine;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

namespace Mmd.Tests.Cinemachine
{
    public sealed class MmdCinemachineCameraBindingTests
    {
        [Test]
        public void ApplyCameraStateMatchesDirectCameraPoseAndVerticalFieldOfView()
        {
            var directBindingGo = new GameObject("direct binding");
            var directCameraGo = new GameObject("direct camera");
            var cinemachineBindingGo = new GameObject("cinemachine binding");
            var cinemachineCameraGo = new GameObject("cinemachine camera");
            try
            {
                Camera directCamera = directCameraGo.AddComponent<Camera>();
                MmdSceneEnvironmentBinding directBinding = directBindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                directBinding.TargetCamera = directCamera;

                CinemachineCamera cinemachineCamera = cinemachineCameraGo.AddComponent<CinemachineCamera>();
                MmdCinemachineCameraBinding cinemachineBinding =
                    cinemachineBindingGo.AddComponent<MmdCinemachineCameraBinding>();
                cinemachineBinding.TargetCinemachineCamera = cinemachineCamera;

                MmdCameraState state = State(-45f, 1f, 10f, -2f, 0.3f, 0.5f, 0.1f, 35f, true);

                Assert.That(directBinding.ApplyCameraState(state, importScale: 0.1f),
                    Is.EqualTo(MmdSceneCameraApplyStatus.Applied));
                Assert.That(cinemachineBinding.ApplyCameraState(state, importScale: 0.1f),
                    Is.EqualTo(MmdSceneCameraApplyStatus.Applied));
                Assert.That(Vector3.Distance(cinemachineCamera.transform.position, directCamera.transform.position),
                    Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(cinemachineCamera.transform.rotation, directCamera.transform.rotation),
                    Is.LessThan(0.05f));
                Assert.That(cinemachineCamera.Lens.FieldOfView,
                    Is.EqualTo(directCamera.fieldOfView).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cinemachineCameraGo);
                Object.DestroyImmediate(cinemachineBindingGo);
                Object.DestroyImmediate(directCameraGo);
                Object.DestroyImmediate(directBindingGo);
            }
        }

        [Test]
        public void MissingCinemachineTargetFailsClosed()
        {
            var bindingGo = new GameObject("binding");
            try
            {
                MmdCinemachineCameraBinding binding = bindingGo.AddComponent<MmdCinemachineCameraBinding>();

                MmdSceneCameraApplyStatus status = binding.ApplyCameraState(
                    State(-20f, 1f, 2f, 3f, 0f, 0f, 0f, 30f, true));

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.NoTargetCamera));
                Assert.That(binding.LastCameraApplyStatus, Is.EqualTo(status));
            }
            finally
            {
                Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void DirectAndCinemachineTargetsConflictBeforeEitherIsMutated()
        {
            var bindingGo = new GameObject("binding");
            var cinemachineCameraGo = new GameObject("cinemachine camera");
            var directCameraGo = new GameObject("direct camera");
            try
            {
                CinemachineCamera cinemachineCamera = cinemachineCameraGo.AddComponent<CinemachineCamera>();
                Camera directCamera = directCameraGo.AddComponent<Camera>();
                cinemachineCamera.transform.position = new Vector3(1f, 2f, 3f);
                directCamera.transform.position = new Vector3(4f, 5f, 6f);

                MmdCinemachineCameraBinding binding = bindingGo.AddComponent<MmdCinemachineCameraBinding>();
                binding.TargetCinemachineCamera = cinemachineCamera;
                binding.TargetCamera = directCamera;

                MmdSceneCameraApplyStatus status = binding.ApplyCameraState(
                    State(-20f, 9f, 9f, 9f, 0f, 0f, 0f, 30f, true));

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.ConflictingCameraTargets));
                Assert.That(binding.LastCameraApplyStatus, Is.EqualTo(status));
                Assert.That(cinemachineCamera.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(directCamera.transform.position, Is.EqualTo(new Vector3(4f, 5f, 6f)));
            }
            finally
            {
                Object.DestroyImmediate(directCameraGo);
                Object.DestroyImmediate(cinemachineCameraGo);
                Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void FollowAuthorityFailsClosedBeforePoseMutation()
        {
            var bindingGo = new GameObject("binding");
            var cinemachineCameraGo = new GameObject("cinemachine camera");
            var followGo = new GameObject("follow");
            try
            {
                CinemachineCamera cinemachineCamera = cinemachineCameraGo.AddComponent<CinemachineCamera>();
                cinemachineCamera.Follow = followGo.transform;
                cinemachineCamera.transform.position = new Vector3(1f, 2f, 3f);
                MmdCinemachineCameraBinding binding = bindingGo.AddComponent<MmdCinemachineCameraBinding>();
                binding.TargetCinemachineCamera = cinemachineCamera;

                MmdSceneCameraApplyStatus status = binding.ApplyCameraState(
                    State(-20f, 9f, 9f, 9f, 0f, 0f, 0f, 30f, true));

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.ProceduralCameraAuthorityConflict));
                Assert.That(cinemachineCamera.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            }
            finally
            {
                Object.DestroyImmediate(followGo);
                Object.DestroyImmediate(cinemachineCameraGo);
                Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void ActiveNoisePipelineFailsClosedBeforeLensMutation()
        {
            var bindingGo = new GameObject("binding");
            var cinemachineCameraGo = new GameObject("cinemachine camera");
            NoiseSettings? noiseSettings = null;
            try
            {
                CinemachineCamera cinemachineCamera = cinemachineCameraGo.AddComponent<CinemachineCamera>();
                CinemachineBasicMultiChannelPerlin noise =
                    cinemachineCameraGo.AddComponent<CinemachineBasicMultiChannelPerlin>();
                noiseSettings = ScriptableObject.CreateInstance<NoiseSettings>();
                noise.NoiseProfile = noiseSettings;
                LensSettings originalLens = cinemachineCamera.Lens;

                MmdCinemachineCameraBinding binding = bindingGo.AddComponent<MmdCinemachineCameraBinding>();
                binding.TargetCinemachineCamera = cinemachineCamera;

                MmdSceneCameraApplyStatus status = binding.ApplyCameraState(
                    State(-20f, 1f, 2f, 3f, 0f, 0f, 0f, 30f, true));

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.ProceduralCameraAuthorityConflict));
                Assert.That(cinemachineCamera.Lens.FieldOfView, Is.EqualTo(originalLens.FieldOfView));
            }
            finally
            {
                if (noiseSettings != null)
                {
                    Object.DestroyImmediate(noiseSettings);
                }
                Object.DestroyImmediate(cinemachineCameraGo);
                Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void OrthographicRequestAppliesPerspectivePoseAndReportsUnsupported()
        {
            var bindingGo = new GameObject("binding");
            var cinemachineCameraGo = new GameObject("cinemachine camera");
            try
            {
                CinemachineCamera cinemachineCamera = cinemachineCameraGo.AddComponent<CinemachineCamera>();
                LensSettings originalLens = cinemachineCamera.Lens;
                originalLens.ModeOverride = LensSettings.OverrideModes.Perspective;
                cinemachineCamera.Lens = originalLens;
                MmdCinemachineCameraBinding binding = bindingGo.AddComponent<MmdCinemachineCameraBinding>();
                binding.TargetCinemachineCamera = cinemachineCamera;
                MmdCameraState state = State(-45f, 1f, 2f, 3f, 0f, 0f, 0f, 32f, false);
                MmdUnityCameraPose expected = MmdCameraStateToUnity.Convert(state);

                MmdSceneCameraApplyStatus status = binding.ApplyCameraState(state);

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.AppliedOrthographicNotSupported));
                Assert.That(binding.LastCameraApplyStatus, Is.EqualTo(status));
                Assert.That(Vector3.Distance(cinemachineCamera.transform.position, expected.Position), Is.LessThan(0.001f));
                Assert.That(cinemachineCamera.Lens.FieldOfView, Is.EqualTo(expected.FieldOfView).Within(0.001f));
                Assert.That(cinemachineCamera.Lens.ModeOverride, Is.EqualTo(LensSettings.OverrideModes.Perspective));
            }
            finally
            {
                Object.DestroyImmediate(cinemachineCameraGo);
                Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void PerspectiveRequestOverridesAnOrthographicCinemachineLens()
        {
            var bindingGo = new GameObject("binding");
            var cinemachineCameraGo = new GameObject("cinemachine camera");
            try
            {
                CinemachineCamera cinemachineCamera = cinemachineCameraGo.AddComponent<CinemachineCamera>();
                LensSettings lens = cinemachineCamera.Lens;
                lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
                cinemachineCamera.Lens = lens;
                MmdCinemachineCameraBinding binding = bindingGo.AddComponent<MmdCinemachineCameraBinding>();
                binding.TargetCinemachineCamera = cinemachineCamera;

                MmdSceneCameraApplyStatus status = binding.ApplyCameraState(
                    State(-45f, 1f, 2f, 3f, 0f, 0f, 0f, 32f, true));

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.Applied));
                Assert.That(cinemachineCamera.Lens.ModeOverride,
                    Is.EqualTo(LensSettings.OverrideModes.Perspective));
            }
            finally
            {
                Object.DestroyImmediate(cinemachineCameraGo);
                Object.DestroyImmediate(bindingGo);
            }
        }

        private static MmdCameraState State(
            float distance, float px, float py, float pz, float rx, float ry, float rz, float fov, bool perspective)
        {
            return new MmdCameraState(distance, new[] { px, py, pz }, new[] { rx, ry, rz }, fov, perspective);
        }
    }
}
