#nullable enable

using NUnit.Framework;
using UnityEngine;
using Mmd.Motion;
using Mmd.UnityIntegration;

namespace Mmd.Tests
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
        public void FreshBindingReportsNotAppliedSelfShadow()
        {
            var go = new GameObject("binding");
            try
            {
                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                Assert.That(binding.LastSelfShadowApplyStatus, Is.EqualTo(MmdSceneSelfShadowApplyStatus.NotApplied));
                Assert.That(binding.SelfShadowEnabled, Is.True);
                Assert.That(binding.LastSelfShadowState.Mode, Is.EqualTo(0));
                Assert.That(binding.LastSelfShadowState.Distance, Is.EqualTo(0.0f));
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
        public void ApplyLightStateToNonDirectionalLightStillAppliesButReportsDiagnostic()
        {
            var go = new GameObject("binding");
            var lightGo = new GameObject("light");
            try
            {
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.intensity = 2.5f;

                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetLight = light;

                MmdLightState state = new MmdLightState(
                    new[] { 0.2f, 0.4f, 0.6f },
                    new[] { -0.5f, -1f, 0.5f });

                MmdSceneLightApplyStatus status = binding.ApplyLightState(state);

                // Non-directional bind is a diagnostic, not a hard failure: color/direction are still applied.
                Assert.That(status, Is.EqualTo(MmdSceneLightApplyStatus.AppliedNonDirectional));
                Assert.That(binding.LastLightApplyStatus, Is.EqualTo(MmdSceneLightApplyStatus.AppliedNonDirectional));
                Assert.That(light.color.r, Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(light.color.g, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(light.color.b, Is.EqualTo(0.6f).Within(0.001f));
                // v1 policy: intensity is never touched.
                Assert.That(light.intensity, Is.EqualTo(2.5f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyLightStateLeavesDirectionalLightIntensityUnchanged()
        {
            var go = new GameObject("binding");
            var lightGo = new GameObject("light");
            try
            {
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 3.0f;

                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetLight = light;

                MmdSceneLightApplyStatus status = binding.ApplyLightState(
                    new MmdLightState(new[] { 0.2f, 0.4f, 0.6f }, new[] { -0.5f, -1f, 0.5f }));

                Assert.That(status, Is.EqualTo(MmdSceneLightApplyStatus.Applied));
                // v1 policy: VMD light carries color + direction only; intensity stays at the scene value.
                Assert.That(light.intensity, Is.EqualTo(3.0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplySelfShadowState_DefaultEnabledRecordsStateWithoutMutatingLightOrGlobalShadowDistance()
        {
            var go = new GameObject("binding");
            var lightGo = new GameObject("light");
            float originalShadowDistance = QualitySettings.shadowDistance;
            try
            {
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.Hard;
                light.shadowStrength = 0.25f;

                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetLight = light;

                MmdSceneSelfShadowApplyStatus status = binding.ApplySelfShadowState(new MmdSelfShadowState(1, 0.4f));

                Assert.That(status, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Recorded));
                Assert.That(binding.LastSelfShadowApplyStatus, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Recorded));
                Assert.That(binding.LastSelfShadowState.Mode, Is.EqualTo(1));
                Assert.That(binding.LastSelfShadowState.Distance, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(binding.LastSelfShadowSettings.RuntimeApplicationEnabled, Is.False);
                Assert.That(light.shadows, Is.EqualTo(LightShadows.Hard));
                Assert.That(light.shadowStrength, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(QualitySettings.shadowDistance, Is.EqualTo(originalShadowDistance).Within(0.001f));
            }
            finally
            {
                QualitySettings.shadowDistance = originalShadowDistance;
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplySelfShadowState_ExplicitEnableDoesNotMutateLightLocalShadowSettings()
        {
            var go = new GameObject("binding");
            var lightGo = new GameObject("light");
            float originalShadowDistance = QualitySettings.shadowDistance;
            try
            {
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.None;
                light.shadowStrength = 0.2f;

                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetLight = light;
                binding.SelfShadowEnabled = true;

                MmdSceneSelfShadowApplyStatus status = binding.ApplySelfShadowState(new MmdSelfShadowState(2, 0.8f));

                Assert.That(status, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Recorded));
                Assert.That(binding.LastSelfShadowSettings.RuntimeApplicationEnabled, Is.False);
                Assert.That(binding.LastSelfShadowSettings.CastShadows, Is.False);
                Assert.That(binding.LastSelfShadowState.Mode, Is.EqualTo(2));
                Assert.That(binding.LastSelfShadowState.Distance, Is.EqualTo(0.8f).Within(0.001f));
                Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
                Assert.That(light.shadowStrength, Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(QualitySettings.shadowDistance, Is.EqualTo(originalShadowDistance).Within(0.001f));
            }
            finally
            {
                QualitySettings.shadowDistance = originalShadowDistance;
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplySelfShadowState_DisabledDoesNotRecordActiveStateOrMutateLightOrGlobalShadowDistance()
        {
            var go = new GameObject("binding");
            var lightGo = new GameObject("light");
            float originalShadowDistance = QualitySettings.shadowDistance;
            try
            {
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.None;
                light.shadowStrength = 0.2f;

                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetLight = light;
                binding.SelfShadowEnabled = false;

                MmdSceneSelfShadowApplyStatus status = binding.ApplySelfShadowState(new MmdSelfShadowState(2, 0.8f));

                Assert.That(status, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Disabled));
                Assert.That(binding.LastSelfShadowApplyStatus, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Disabled));
                Assert.That(binding.LastSelfShadowState.Mode, Is.EqualTo(0));
                Assert.That(binding.LastSelfShadowState.Distance, Is.EqualTo(0.0f));
                Assert.That(binding.LastSelfShadowSettings.RuntimeApplicationEnabled, Is.False);
                Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
                Assert.That(light.shadowStrength, Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(QualitySettings.shadowDistance, Is.EqualTo(originalShadowDistance).Within(0.001f));
            }
            finally
            {
                QualitySettings.shadowDistance = originalShadowDistance;
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplySelfShadowState_ModeZeroRecordsDisabledMmdModeWithoutMutatingLight()
        {
            var go = new GameObject("binding");
            var lightGo = new GameObject("light");
            try
            {
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.75f;

                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetLight = light;
                binding.SelfShadowEnabled = true;

                MmdSceneSelfShadowApplyStatus status = binding.ApplySelfShadowState(new MmdSelfShadowState(0, 0.4f));

                Assert.That(status, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Recorded));
                Assert.That(binding.LastSelfShadowState.Mode, Is.EqualTo(0));
                Assert.That(binding.LastSelfShadowState.Distance, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(binding.LastSelfShadowSettings.CastShadows, Is.False);
                Assert.That(light.shadows, Is.EqualTo(LightShadows.Soft));
                Assert.That(light.shadowStrength, Is.EqualTo(0.75f).Within(0.001f));
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
