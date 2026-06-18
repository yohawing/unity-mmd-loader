using NUnit.Framework;
using UnityEngine;
using Yohawing.MmdUnity.Motion;
using Yohawing.MmdUnity.UnityIntegration;

namespace Yohawing.MmdUnity.Tests
{
    public sealed class MmdSceneEnvironmentBindingTests
    {
        private static MmdCameraState State(
            float distance, float px, float py, float pz, float rx, float ry, float rz, float fov, bool perspective)
        {
            return new MmdCameraState(distance, new[] { px, py, pz }, new[] { rx, ry, rz }, fov, perspective);
        }

        [Test]
        public void FreshBindingReportsNotApplied()
        {
            var go = new GameObject("binding");
            try
            {
                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                Assert.That(binding.LastCameraApplyStatus, Is.EqualTo(MmdSceneCameraApplyStatus.NotApplied));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FreshBindingReportsNotAppliedLight()
        {
            var go = new GameObject("binding");
            try
            {
                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                Assert.That(binding.LastLightApplyStatus, Is.EqualTo(MmdSceneLightApplyStatus.NotApplied));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyCameraStateDrivesBoundCameraToConvertedPose()
        {
            var go = new GameObject("binding");
            var cameraGo = new GameObject("camera");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetCamera = camera;

                MmdCameraState state = State(-45f, 1, 10, 0, 0.3f, 0.5f, 0.1f, 35f, true);
                MmdUnityCameraPose expected = MmdCameraStateToUnity.Convert(state);

                MmdSceneCameraApplyStatus status = binding.ApplyCameraState(state);

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.Applied));
                Assert.That(Vector3.Distance(camera.transform.position, expected.Position), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(camera.transform.rotation, expected.Rotation), Is.LessThan(0.05f));
                Assert.That(camera.fieldOfView, Is.EqualTo(expected.FieldOfView).Within(0.001f));
                Assert.That(camera.orthographic, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyLightStateDrivesBoundLight()
        {
            var go = new GameObject("binding");
            var lightGo = new GameObject("light");
            try
            {
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;

                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetLight = light;

                MmdLightState state = new MmdLightState(
                    new[] { 0.2f, 0.4f, 0.6f },
                    new[] { -0.5f, -1f, 0.5f });

                MmdSceneLightApplyStatus status = binding.ApplyLightState(state);
                Vector3 expectedDirection = MmdCoordinateSpace.MmdToUnityPosition(new Vector3(-0.5f, -1f, 0.5f)).normalized;

                Assert.That(status, Is.EqualTo(MmdSceneLightApplyStatus.Applied));
                Assert.That(light.color.r, Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(light.color.g, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(light.color.b, Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(Vector3.Dot(light.transform.forward, expectedDirection), Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyCameraStateWithoutTargetReturnsNoTargetCameraAndDoesNotThrow()
        {
            var go = new GameObject("binding");
            try
            {
                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                Assert.That(binding.TargetCamera, Is.Null);

                MmdSceneCameraApplyStatus status = binding.ApplyCameraState(State(-45f, 0, 0, 0, 0, 0, 0, 30f, true));

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.NoTargetCamera));
                Assert.That(binding.LastCameraApplyStatus, Is.EqualTo(MmdSceneCameraApplyStatus.NoTargetCamera));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyLightStateWithoutTargetReturnsNoTargetLight()
        {
            var go = new GameObject("binding");
            try
            {
                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                Assert.That(binding.TargetLight, Is.Null);

                MmdSceneLightApplyStatus status = binding.ApplyLightState(
                    new MmdLightState(new[] { 0.2f, 0.4f, 0.6f }, new[] { -0.5f, -1f, 0.5f }));

                Assert.That(status, Is.EqualTo(MmdSceneLightApplyStatus.NoTargetLight));
                Assert.That(binding.LastLightApplyStatus, Is.EqualTo(MmdSceneLightApplyStatus.NoTargetLight));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OrthographicRequestKeepsPerspectiveAndReportsStatus()
        {
            var go = new GameObject("binding");
            var cameraGo = new GameObject("camera");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                camera.orthographic = false;
                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetCamera = camera;

                // perspective = false (MMD orthographic flag).
                MmdSceneCameraApplyStatus status = binding.ApplyCameraState(State(-45f, 0, 0, 0, 0, 0, 0, 30f, false));

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.AppliedOrthographicNotSupported));
                Assert.That(binding.LastCameraApplyStatus, Is.EqualTo(MmdSceneCameraApplyStatus.AppliedOrthographicNotSupported));
                Assert.That(camera.orthographic, Is.False, "v1 keeps the camera in perspective");
                Assert.That(camera.fieldOfView, Is.EqualTo(30f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(go);
            }
        }
    }
}
