#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Mmd.Motion;
using Mmd.Parser;
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

        private static MmdSelfShadowKeyframeDefinition SelfShadowKeyframe(int frame, byte mode, float distance)
        {
            return new MmdSelfShadowKeyframeDefinition
            {
                frame = frame,
                mode = mode,
                distance = distance
            };
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
                Assert.That(binding.LastSelfShadowProjectionState.Active, Is.False);
                Assert.That(binding.LastSelfShadowProjectionState.Scope, Is.EqualTo(MmdSelfShadowProjectionScope.CharacterOnly));
                Assert.That(binding.LastSelfShadowDiagnosticStatus, Is.EqualTo(MmdSceneSelfShadowDiagnosticStatus.NoSelfShadowState));
                Assert.That(binding.EvaluateSelfShadowDiagnosticStatus(), Is.EqualTo(MmdSceneSelfShadowDiagnosticStatus.NoSelfShadowState));
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
                Assert.That(binding.TryGetLastUnityLightDirection(out Vector3 recordedDirection), Is.True);
                Assert.That(Vector3.Dot(recordedDirection, expectedDirection), Is.EqualTo(1f).Within(0.001f));
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
        public void LastUnityLightDirectionPrefersCurrentBoundDirectionalLight()
        {
            var go = new GameObject("binding");
            var lightGo = new GameObject("light");
            try
            {
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;

                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetLight = light;

                binding.ApplyLightState(
                    new MmdLightState(new[] { 0.2f, 0.4f, 0.6f }, new[] { -0.5f, -1f, 0.5f }));
                light.transform.rotation = Quaternion.LookRotation(Vector3.left);

                Assert.That(binding.TryGetLastUnityLightDirection(out Vector3 direction), Is.True);
                Assert.That(Vector3.Dot(direction, Vector3.left), Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SelfShadowUnityLightDirectionPrefersExplicitDirectionalLightWithoutChangingLastLightPolicy()
        {
            var go = new GameObject("binding");
            var targetLightGo = new GameObject("target-light");
            var selfShadowLightGo = new GameObject("self-shadow-light");
            try
            {
                Light targetLight = targetLightGo.AddComponent<Light>();
                targetLight.type = LightType.Directional;
                Light selfShadowLight = selfShadowLightGo.AddComponent<Light>();
                selfShadowLight.type = LightType.Directional;

                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetLight = targetLight;
                binding.SelfShadowDirectionLight = selfShadowLight;

                binding.ApplyLightState(
                    new MmdLightState(new[] { 0.2f, 0.4f, 0.6f }, new[] { -0.5f, -1f, 0.5f }));
                targetLight.transform.rotation = Quaternion.LookRotation(Vector3.left);
                selfShadowLight.transform.rotation = Quaternion.LookRotation(Vector3.right);

                Assert.That(binding.TryGetSelfShadowUnityLightDirection(out Vector3 selfShadowDirection), Is.True);
                Assert.That(Vector3.Dot(selfShadowDirection, Vector3.right), Is.EqualTo(1f).Within(0.001f));
                Assert.That(binding.TryGetLastUnityLightDirection(out Vector3 lastLightDirection), Is.True);
                Assert.That(Vector3.Dot(lastLightDirection, Vector3.left), Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(selfShadowLightGo);
                Object.DestroyImmediate(targetLightGo);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SelfShadowUnityLightDirectionFallsBackPastInvalidExplicitLightToTargetThenRecordedVmdLight()
        {
            var go = new GameObject("binding");
            var targetLightGo = new GameObject("target-light");
            var invalidSelfShadowLightGo = new GameObject("self-shadow-light");
            try
            {
                Light targetLight = targetLightGo.AddComponent<Light>();
                targetLight.type = LightType.Directional;
                targetLight.transform.rotation = Quaternion.LookRotation(Vector3.left);
                Light invalidSelfShadowLight = invalidSelfShadowLightGo.AddComponent<Light>();
                invalidSelfShadowLight.type = LightType.Point;

                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetLight = targetLight;
                binding.SelfShadowDirectionLight = invalidSelfShadowLight;
                binding.ApplyLightState(
                    new MmdLightState(new[] { 0.2f, 0.4f, 0.6f }, new[] { -0.25f, -1f, 0.75f }));
                targetLight.transform.rotation = Quaternion.LookRotation(Vector3.left);

                Assert.That(binding.TryGetSelfShadowUnityLightDirection(out Vector3 targetDirection), Is.True);
                Assert.That(Vector3.Dot(targetDirection, Vector3.left), Is.EqualTo(1f).Within(0.001f));

                binding.TargetLight = null;
                Vector3 expectedRecordedDirection = MmdCoordinateSpace.MmdToUnityPosition(
                    new Vector3(-0.25f, -1.0f, 0.75f)).normalized;

                Assert.That(binding.TryGetSelfShadowUnityLightDirection(out Vector3 recordedDirection), Is.True);
                Assert.That(Vector3.Dot(recordedDirection, expectedRecordedDirection), Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(invalidSelfShadowLightGo);
                Object.DestroyImmediate(targetLightGo);
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
                binding.SelfShadowProjectionPolicy = new MmdSelfShadowProjectionPolicy(
                    distanceScale: 10.0f,
                    minFarDistance: 2.0f,
                    maxFarDistance: 5.0f,
                    boundsPadding: 0.25f);

                MmdSceneSelfShadowApplyStatus status = binding.ApplySelfShadowState(new MmdSelfShadowState(1, 0.4f));

                Assert.That(status, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Recorded));
                Assert.That(binding.LastSelfShadowApplyStatus, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Recorded));
                Assert.That(binding.LastSelfShadowState.Mode, Is.EqualTo(1));
                Assert.That(binding.LastSelfShadowState.Distance, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(binding.LastSelfShadowProjectionState.Active, Is.True);
                Assert.That(binding.LastSelfShadowProjectionState.Mode, Is.EqualTo(1));
                Assert.That(binding.LastSelfShadowProjectionState.FarDistance, Is.EqualTo(4.0f).Within(0.001f));
                Assert.That(binding.LastSelfShadowProjectionState.BoundsPadding, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(binding.LastSelfShadowProjectionState.Scope, Is.EqualTo(MmdSelfShadowProjectionScope.CharacterOnly));
                Assert.That(binding.LastSelfShadowProjectionState.IncludesBackground, Is.False);
                Assert.That(binding.LastSelfShadowDiagnosticStatus, Is.EqualTo(MmdSceneSelfShadowDiagnosticStatus.Active));
                Assert.That(binding.EvaluateSelfShadowDiagnosticStatus(), Is.EqualTo(MmdSceneSelfShadowDiagnosticStatus.Active));
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
                binding.SelfShadowProjectionPolicy = new MmdSelfShadowProjectionPolicy(
                    scope: MmdSelfShadowProjectionScope.CharacterAndOptInBackground);

                MmdSceneSelfShadowApplyStatus status = binding.ApplySelfShadowState(new MmdSelfShadowState(2, 0.8f));

                Assert.That(status, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Recorded));
                Assert.That(binding.LastSelfShadowSettings.RuntimeApplicationEnabled, Is.False);
                Assert.That(binding.LastSelfShadowSettings.CastShadows, Is.False);
                Assert.That(binding.LastSelfShadowState.Mode, Is.EqualTo(2));
                Assert.That(binding.LastSelfShadowState.Distance, Is.EqualTo(0.8f).Within(0.001f));
                Assert.That(binding.LastSelfShadowProjectionState.Active, Is.True);
                Assert.That(binding.LastSelfShadowProjectionState.Scope, Is.EqualTo(MmdSelfShadowProjectionScope.CharacterAndOptInBackground));
                Assert.That(binding.LastSelfShadowProjectionState.IncludesBackground, Is.True);
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
                Assert.That(binding.LastSelfShadowProjectionState.Active, Is.False);
                Assert.That(binding.LastSelfShadowProjectionState.Scope, Is.EqualTo(MmdSelfShadowProjectionScope.CharacterOnly));
                Assert.That(binding.LastSelfShadowDiagnosticStatus, Is.EqualTo(MmdSceneSelfShadowDiagnosticStatus.ModeDisabled));
                Assert.That(binding.EvaluateSelfShadowDiagnosticStatus(), Is.EqualTo(MmdSceneSelfShadowDiagnosticStatus.ModeDisabled));
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
                Assert.That(binding.LastSelfShadowProjectionState.Active, Is.False);
                Assert.That(binding.LastSelfShadowProjectionState.Mode, Is.EqualTo(0));
                Assert.That(binding.LastSelfShadowDiagnosticStatus, Is.EqualTo(MmdSceneSelfShadowDiagnosticStatus.ModeDisabled));
                Assert.That(binding.EvaluateSelfShadowDiagnosticStatus(), Is.EqualTo(MmdSceneSelfShadowDiagnosticStatus.ModeDisabled));
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
        public void TryEvaluateSelfShadowAtFrameRecordsSampledStateAndDiagnostic()
        {
            var go = new GameObject("binding");
            try
            {
                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();
                var keyframes = new List<MmdSelfShadowKeyframeDefinition>
                {
                    SelfShadowKeyframe(0, 1, 0.2f),
                    SelfShadowKeyframe(30, 2, 0.8f)
                };

                bool sampled = binding.TryEvaluateSelfShadowAtFrame(keyframes, 15.0f, out MmdSceneSelfShadowApplyStatus status);

                Assert.That(sampled, Is.True);
                Assert.That(status, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Recorded));
                Assert.That(binding.LastSelfShadowApplyStatus, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Recorded));
                Assert.That(binding.LastSelfShadowState.Mode, Is.EqualTo(1));
                Assert.That(binding.LastSelfShadowState.Distance, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(binding.LastSelfShadowDiagnosticStatus, Is.EqualTo(MmdSceneSelfShadowDiagnosticStatus.Active));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TryEvaluateSelfShadowAtFrameWithoutKeyframesReportsNoSelfShadowState()
        {
            var go = new GameObject("binding");
            try
            {
                MmdSceneEnvironmentBinding binding = go.AddComponent<MmdSceneEnvironmentBinding>();

                bool sampled = binding.TryEvaluateSelfShadowAtFrame(
                    new List<MmdSelfShadowKeyframeDefinition>(),
                    15.0f,
                    out MmdSceneSelfShadowApplyStatus status);

                Assert.That(sampled, Is.False);
                Assert.That(status, Is.EqualTo(MmdSceneSelfShadowApplyStatus.NotApplied));
                Assert.That(binding.LastSelfShadowApplyStatus, Is.EqualTo(MmdSceneSelfShadowApplyStatus.NotApplied));
                Assert.That(binding.LastSelfShadowDiagnosticStatus, Is.EqualTo(MmdSceneSelfShadowDiagnosticStatus.NoSelfShadowState));
            }
            finally
            {
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
                Vector3 expectedDirection = MmdCoordinateSpace.MmdToUnityPosition(new Vector3(-0.5f, -1f, 0.5f)).normalized;

                MmdSceneLightApplyStatus status = binding.ApplyLightState(
                    new MmdLightState(new[] { 0.2f, 0.4f, 0.6f }, new[] { -0.5f, -1f, 0.5f }));

                Assert.That(status, Is.EqualTo(MmdSceneLightApplyStatus.NoTargetLight));
                Assert.That(binding.LastLightApplyStatus, Is.EqualTo(MmdSceneLightApplyStatus.NoTargetLight));
                Assert.That(binding.TryGetLastUnityLightDirection(out Vector3 recordedDirection), Is.True);
                Assert.That(Vector3.Dot(recordedDirection, expectedDirection), Is.EqualTo(1f).Within(0.001f));
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
