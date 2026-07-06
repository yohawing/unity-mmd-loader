#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd.Motion;
using Mmd.Native;
using Mmd.Parser;
using Mmd.Timeline;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdVmdCameraTimelineTests
    {
        private static MmdCameraKeyframeDefinition Keyframe(
            int frame, float distance, float[] position, float[] rotation, int viewAngle, bool perspective)
        {
            return new MmdCameraKeyframeDefinition
            {
                frame = frame,
                distance = distance,
                position = position,
                rotation = rotation,
                viewAngle = viewAngle,
                perspective = perspective,
                interpolation = new byte[24]
            };
        }

        private static List<MmdCameraKeyframeDefinition> TwoKeyframeTrack()
        {
            return new List<MmdCameraKeyframeDefinition>
            {
                Keyframe(0, -40f, new[] { 0f, 10f, 0f }, new[] { 0f, 0f, 0f }, 20, true),
                Keyframe(30, -20f, new[] { 2f, 20f, -4f }, new[] { 0.1f, 0.2f, 0.1f }, 40, true)
            };
        }

        private static List<MmdLightKeyframeDefinition> LightKeyframes()
        {
            return new List<MmdLightKeyframeDefinition>
            {
                new MmdLightKeyframeDefinition
                {
                    frame = 0,
                    color = new[] { 0.2f, 0.2f, 0.2f },
                    direction = new[] { -0.5f, -1f, 0.5f }
                },
                new MmdLightKeyframeDefinition
                {
                    frame = 30,
                    color = new[] { 0.8f, 0.8f, 0.8f },
                    direction = new[] { 0f, -1f, 0f }
                }
            };
        }

        private static List<MmdSelfShadowKeyframeDefinition> SelfShadowKeyframes()
        {
            return new List<MmdSelfShadowKeyframeDefinition>
            {
                new MmdSelfShadowKeyframeDefinition
                {
                    frame = 0,
                    mode = 1,
                    distance = 0.2f
                },
                new MmdSelfShadowKeyframeDefinition
                {
                    frame = 30,
                    mode = 2,
                    distance = 0.6f
                }
            };
        }

        [Test]
        public void EvaluateAtLocalTimeAppliesSampledCameraStateToBoundProxy()
        {
            var bindingGo = new GameObject("binding");
            var cameraGo = new GameObject("camera");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetCamera = camera;

                byte[] vmdBytes = BuildCameraVmdBytes();
                var behaviour = new MmdVmdCameraBehaviour
                {
                    MotionBytes = vmdBytes,
                    FrameRate = 30f,
                    ImportScale = 1.0f
                };

                // localTime 0.5s at 30fps -> frame 15 (midway between keyframes 0 and 30).
                MmdSceneCameraApplyStatus status = behaviour.EvaluateAtLocalTime(binding, 0.5);

                MmdCameraState expectedState = SampleNativeCamera(vmdBytes, 15f);
                MmdUnityCameraPose expectedPose = MmdCameraStateToUnity.Convert(expectedState);

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.Applied));
                Assert.That(Vector3.Distance(camera.transform.position, expectedPose.Position), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(camera.transform.rotation, expectedPose.Rotation), Is.LessThan(0.05f));
                Assert.That(camera.fieldOfView, Is.EqualTo(expectedPose.FieldOfView).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void EvaluateAtLocalTimeScalesCameraPositionWithoutChangingRotationOrFov()
        {
            var bindingGo = new GameObject("binding");
            var cameraGo = new GameObject("camera");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetCamera = camera;

                byte[] vmdBytes = BuildCameraVmdBytes();
                var behaviour = new MmdVmdCameraBehaviour
                {
                    MotionBytes = vmdBytes,
                    FrameRate = 30f,
                    ImportScale = 0.1f
                };

                MmdSceneCameraApplyStatus status = behaviour.EvaluateAtLocalTime(binding, 0.5);

                MmdCameraState expectedState = SampleNativeCamera(vmdBytes, 15f);
                MmdUnityCameraPose scaleOnePose = MmdCameraStateToUnity.Convert(expectedState, importScale: 1.0f);
                MmdUnityCameraPose expectedPose = MmdCameraStateToUnity.Convert(expectedState, importScale: 0.1f);

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.Applied));
                Assert.That(Vector3.Distance(camera.transform.position, expectedPose.Position), Is.LessThan(0.001f));
                Assert.That(camera.transform.position.x, Is.EqualTo(scaleOnePose.Position.x * 0.1f).Within(0.001f));
                Assert.That(camera.transform.position.y, Is.EqualTo(scaleOnePose.Position.y * 0.1f).Within(0.001f));
                Assert.That(camera.transform.position.z, Is.EqualTo(scaleOnePose.Position.z * 0.1f).Within(0.001f));
                Assert.That(Quaternion.Angle(camera.transform.rotation, scaleOnePose.Rotation), Is.LessThan(0.05f));
                Assert.That(camera.fieldOfView, Is.EqualTo(scaleOnePose.FieldOfView).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void InvalidMotionBytesCameraNotApplied()
        {
            var bindingGo = new GameObject("binding");
            var cameraGo = new GameObject("camera");
            try
            {
                var binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                Camera camera = cameraGo.AddComponent<Camera>();
                binding.TargetCamera = camera;
                Vector3 before = camera.transform.position;

                var behaviour = new MmdVmdCameraBehaviour
                {
                    MotionBytes = new byte[] { 0, 1, 2, 3 },
                    FrameRate = 30f,
                    ImportScale = 1.0f
                };

                MmdSceneCameraApplyStatus status = behaviour.EvaluateAtLocalTime(binding, 0.5);

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.NotApplied));
                Assert.That(camera.transform.position, Is.EqualTo(before));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bindingGo);
                UnityEngine.Object.DestroyImmediate(cameraGo);
            }
        }

        [Test]
        public void EvaluateAtLocalTimeAppliesLightToBoundProxy()
        {
            var bindingGo = new GameObject("binding");
            var cameraGo = new GameObject("camera");
            var lightGo = new GameObject("light");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetCamera = camera;
                binding.TargetLight = light;

                byte[] vmdBytes = BuildCameraAndLightVmdBytes();
                var behaviour = new MmdVmdCameraBehaviour
                {
                    MotionBytes = vmdBytes,
                    FrameRate = 30f,
                    ImportScale = 1.0f
                };

                MmdSceneCameraApplyStatus status = behaviour.EvaluateAtLocalTime(binding, 0.5);
                MmdSceneLightApplyStatus lightStatus = binding.LastLightApplyStatus;

                MmdLightState expectedLight = SampleNativeLight(vmdBytes, 15f);
                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.Applied));
                Assert.That(lightStatus, Is.EqualTo(MmdSceneLightApplyStatus.Applied));
                Assert.That(light.color.r, Is.EqualTo(expectedLight.Color[0]).Within(0.001f));
                Assert.That(light.color.g, Is.EqualTo(expectedLight.Color[1]).Within(0.001f));
                Assert.That(light.color.b, Is.EqualTo(expectedLight.Color[2]).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightGo);
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void CameraEmptyWithLightAppliesLightOnly()
        {
            // Core of the scene-shared track: a light-only VMD (no camera keyframes) still drives the
            // bound light, while the camera lane reports NotApplied.
            var bindingGo = new GameObject("binding");
            var cameraGo = new GameObject("camera");
            var lightGo = new GameObject("light");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetCamera = camera;
                binding.TargetLight = light;

                byte[] vmdBytes = BuildLightOnlyVmdBytes();
                var behaviour = new MmdVmdCameraBehaviour
                {
                    MotionBytes = vmdBytes,
                    FrameRate = 30f,
                    ImportScale = 1.0f
                };

                MmdSceneCameraApplyStatus status = behaviour.EvaluateAtLocalTime(binding, 0.5);

                MmdLightState expectedLight = SampleNativeLight(vmdBytes, 15f);
                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.NotApplied));
                Assert.That(binding.LastLightApplyStatus, Is.EqualTo(MmdSceneLightApplyStatus.Applied));
                Assert.That(light.color.r, Is.EqualTo(expectedLight.Color[0]).Within(0.001f));
                Assert.That(light.color.g, Is.EqualTo(expectedLight.Color[1]).Within(0.001f));
                Assert.That(light.color.b, Is.EqualTo(expectedLight.Color[2]).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightGo);
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void CameraEmptyWithNativeMotionBytesAppliesLightOnly()
        {
            var bindingGo = new GameObject("binding");
            var cameraGo = new GameObject("camera");
            var lightGo = new GameObject("light");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetCamera = camera;
                binding.TargetLight = light;

                var behaviour = new MmdVmdCameraBehaviour
                {
                    CameraKeyframes = Array.Empty<MmdCameraKeyframeDefinition>(),
                    LightKeyframes = Array.Empty<MmdLightKeyframeDefinition>(),
                    MotionBytes = BuildLightOnlyVmdBytes(),
                    FrameRate = 30f,
                    ImportScale = 1.0f
                };

                MmdSceneCameraApplyStatus status = behaviour.EvaluateAtLocalTime(binding, 20.0 / 30.0);

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.NotApplied));
                Assert.That(binding.LastLightApplyStatus, Is.EqualTo(MmdSceneLightApplyStatus.Applied));
                Assert.That(light.color.r, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(light.color.g, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(light.color.b, Is.EqualTo(0.5f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightGo);
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void EmptyLightTrackDoesNotApplyLight()
        {
            var bindingGo = new GameObject("binding");
            var cameraGo = new GameObject("camera");
            var lightGo = new GameObject("light");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = Color.red;
                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetCamera = camera;
                binding.TargetLight = light;

                var behaviour = new MmdVmdCameraBehaviour
                {
                    MotionBytes = BuildCameraVmdBytes(),
                    FrameRate = 30f,
                    ImportScale = 1.0f
                };

                behaviour.EvaluateAtLocalTime(binding, 0.5);

                Assert.That(binding.LastLightApplyStatus, Is.EqualTo(MmdSceneLightApplyStatus.NotApplied));
                Assert.That(light.color, Is.EqualTo(Color.red));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightGo);
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void EvaluateAtLocalTimeRecordsSelfShadowByDefaultWithoutMutatingLight()
        {
            var bindingGo = new GameObject("binding");
            var lightGo = new GameObject("light");
            float originalShadowDistance = QualitySettings.shadowDistance;
            try
            {
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.Hard;
                light.shadowStrength = 0.3f;

                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetLight = light;

                var behaviour = new MmdVmdCameraBehaviour
                {
                    CameraKeyframes = Array.Empty<MmdCameraKeyframeDefinition>(),
                    LightKeyframes = Array.Empty<MmdLightKeyframeDefinition>(),
                    SelfShadowKeyframes = SelfShadowKeyframes(),
                    FrameRate = 30f,
                    ImportScale = 1.0f
                };

                MmdSceneCameraApplyStatus status = behaviour.EvaluateAtLocalTime(binding, 0.5);

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.NotApplied));
                Assert.That(binding.LastSelfShadowApplyStatus, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Recorded));
                Assert.That(binding.LastSelfShadowState.Mode, Is.EqualTo(1));
                Assert.That(binding.LastSelfShadowState.Distance, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(light.shadows, Is.EqualTo(LightShadows.Hard));
                Assert.That(light.shadowStrength, Is.EqualTo(0.3f).Within(0.001f));
                Assert.That(QualitySettings.shadowDistance, Is.EqualTo(originalShadowDistance).Within(0.001f));
            }
            finally
            {
                QualitySettings.shadowDistance = originalShadowDistance;
                UnityEngine.Object.DestroyImmediate(lightGo);
                UnityEngine.Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void EvaluateAtLocalTimeKeepsEnabledDefaultSelfShadowWithoutSelfShadowKeyframes()
        {
            var bindingGo = new GameObject("binding");
            var lightGo = new GameObject("light");
            try
            {
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.None;
                light.shadowStrength = 0.25f;

                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetLight = light;

                var behaviour = new MmdVmdCameraBehaviour
                {
                    CameraKeyframes = Array.Empty<MmdCameraKeyframeDefinition>(),
                    LightKeyframes = Array.Empty<MmdLightKeyframeDefinition>(),
                    SelfShadowKeyframes = Array.Empty<MmdSelfShadowKeyframeDefinition>(),
                    FrameRate = 30f,
                    ImportScale = 1.0f
                };

                MmdSceneCameraApplyStatus status = behaviour.EvaluateAtLocalTime(binding, 0.5);

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.NotApplied));
                Assert.That(binding.LastSelfShadowApplyStatus, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Recorded));
                Assert.That(binding.LastSelfShadowState.Mode, Is.EqualTo(MmdSceneEnvironmentBinding.DefaultSelfShadowMode));
                Assert.That(binding.LastSelfShadowState.Distance, Is.EqualTo(MmdSceneEnvironmentBinding.DefaultSelfShadowDistance));
                Assert.That(binding.LastSelfShadowDiagnosticStatus, Is.EqualTo(MmdSceneSelfShadowDiagnosticStatus.Active));
                Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
                Assert.That(light.shadowStrength, Is.EqualTo(0.25f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightGo);
                UnityEngine.Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void EvaluateAtLocalTimeRecordsSelfShadowWhenBindingExplicitlyEnabledWithoutMutatingLight()
        {
            var bindingGo = new GameObject("binding");
            var lightGo = new GameObject("light");
            try
            {
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.None;
                light.shadowStrength = 0.25f;

                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetLight = light;
                binding.SelfShadowEnabled = true;

                var behaviour = new MmdVmdCameraBehaviour
                {
                    CameraKeyframes = Array.Empty<MmdCameraKeyframeDefinition>(),
                    LightKeyframes = Array.Empty<MmdLightKeyframeDefinition>(),
                    SelfShadowKeyframes = SelfShadowKeyframes(),
                    FrameRate = 30f,
                    ImportScale = 1.0f
                };

                MmdSceneCameraApplyStatus status = behaviour.EvaluateAtLocalTime(binding, 0.5);

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.NotApplied));
                Assert.That(binding.LastSelfShadowApplyStatus, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Recorded));
                Assert.That(binding.LastSelfShadowState.Mode, Is.EqualTo(1));
                Assert.That(binding.LastSelfShadowState.Distance, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(binding.LastSelfShadowSettings.Mode, Is.EqualTo(1));
                Assert.That(binding.LastSelfShadowSettings.RuntimeApplicationEnabled, Is.False);
                Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
                Assert.That(light.shadowStrength, Is.EqualTo(0.25f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightGo);
                UnityEngine.Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void EvaluateAtLocalTimeLeavesSelfShadowDisabledWhenBindingDisabled()
        {
            var bindingGo = new GameObject("binding");
            var lightGo = new GameObject("light");
            try
            {
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.Hard;
                light.shadowStrength = 0.3f;

                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetLight = light;
                binding.SelfShadowEnabled = false;

                var behaviour = new MmdVmdCameraBehaviour
                {
                    CameraKeyframes = Array.Empty<MmdCameraKeyframeDefinition>(),
                    LightKeyframes = Array.Empty<MmdLightKeyframeDefinition>(),
                    SelfShadowKeyframes = SelfShadowKeyframes(),
                    FrameRate = 30f,
                    ImportScale = 1.0f
                };

                MmdSceneCameraApplyStatus status = behaviour.EvaluateAtLocalTime(binding, 0.5);

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.NotApplied));
                Assert.That(binding.LastSelfShadowApplyStatus, Is.EqualTo(MmdSceneSelfShadowApplyStatus.Disabled));
                Assert.That(binding.LastSelfShadowState.Mode, Is.EqualTo(0));
                Assert.That(binding.LastSelfShadowState.Distance, Is.EqualTo(0.0f));
                Assert.That(light.shadows, Is.EqualTo(LightShadows.Hard));
                Assert.That(light.shadowStrength, Is.EqualTo(0.3f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightGo);
                UnityEngine.Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void StartOffsetShiftsTheSampledFrame()
        {
            var bindingGo = new GameObject("binding");
            var cameraGo = new GameObject("camera");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetCamera = camera;

                byte[] vmdBytes = BuildCameraVmdBytes();
                var behaviour = new MmdVmdCameraBehaviour
                {
                    MotionBytes = vmdBytes,
                    FrameRate = 30f,
                    StartOffsetSeconds = 0.5f, // +15 frames
                    ImportScale = 1.0f
                };

                // localTime 0 + 0.5s offset = frame 15, same as the midpoint above.
                behaviour.EvaluateAtLocalTime(binding, 0.0);

                MmdUnityCameraPose expectedPose = MmdCameraStateToUnity.Convert(SampleNativeCamera(vmdBytes, 15f));
                Assert.That(Vector3.Distance(camera.transform.position, expectedPose.Position), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void EmptyCameraTrackIsNoOp()
        {
            var bindingGo = new GameObject("binding");
            var cameraGo = new GameObject("camera");
            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();
                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                binding.TargetCamera = camera;
                Vector3 before = camera.transform.position;

                var behaviour = new MmdVmdCameraBehaviour
                {
                    CameraKeyframes = Array.Empty<MmdCameraKeyframeDefinition>(),
                    FrameRate = 30f,
                    ImportScale = 1.0f
                };

                MmdSceneCameraApplyStatus status = behaviour.EvaluateAtLocalTime(binding, 1.0);

                Assert.That(status, Is.EqualTo(MmdSceneCameraApplyStatus.NotApplied));
                Assert.That(camera.transform.position, Is.EqualTo(before));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void NegativeLocalTimeThrows()
        {
            var bindingGo = new GameObject("binding");
            try
            {
                MmdSceneEnvironmentBinding binding = bindingGo.AddComponent<MmdSceneEnvironmentBinding>();
                var behaviour = new MmdVmdCameraBehaviour { CameraKeyframes = TwoKeyframeTrack() };

                Assert.Throws<ArgumentOutOfRangeException>(() => behaviour.EvaluateAtLocalTime(binding, -1.0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bindingGo);
            }
        }

        [Test]
        public void NullTargetThrows()
        {
            var behaviour = new MmdVmdCameraBehaviour { CameraKeyframes = TwoKeyframeTrack() };
            Assert.Throws<ArgumentNullException>(() => behaviour.EvaluateAtLocalTime(null, 0.0));
        }

        [Test]
        public void CreatePlayableCarriesClipSettingsToBehaviour()
        {
            var ownerGo = new GameObject("owner");
            PlayableGraph graph = PlayableGraph.Create("MmdVmdCameraTimelineTests");
            MmdVmdCameraClip clip = ScriptableObject.CreateInstance<MmdVmdCameraClip>();
            try
            {
                clip.FrameRate = 24f;
                clip.StartOffsetSeconds = 0.1f;
                clip.MinFieldOfView = 5f;
                clip.ImportScale = 0.25f;
                clip.MotionSourceId = "cam-src";
                clip.LoopPolicy = MmdVmdTimelineLoopPolicy.None;
                clip.MotionAsset = null; // no camera source -> empty (no-op) track

                Assert.That(clip.clipCaps, Is.EqualTo(ClipCaps.None));

                Playable playable = clip.CreatePlayable(graph, ownerGo);
                MmdVmdCameraBehaviour behaviour = ((ScriptPlayable<MmdVmdCameraBehaviour>)playable).GetBehaviour();

                Assert.That(behaviour.FrameRate, Is.EqualTo(24f).Within(0.001f));
                Assert.That(behaviour.StartOffsetSeconds, Is.EqualTo(0.1f).Within(0.001f));
                Assert.That(behaviour.MinFieldOfView, Is.EqualTo(5f).Within(0.001f));
                Assert.That(behaviour.ImportScale, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(behaviour.MotionSourceId, Is.EqualTo("cam-src"));
                Assert.That(behaviour.LoopPolicy, Is.EqualTo(MmdVmdTimelineLoopPolicy.None));
                Assert.That(behaviour.MotionBytes, Is.Null);
                Assert.That(behaviour.CameraKeyframes, Is.Empty);
                Assert.That(behaviour.LightKeyframes, Is.Empty);
                Assert.That(behaviour.SelfShadowKeyframes, Is.Empty);
                Assert.That(behaviour.Binding, Is.Null, "unresolved ExposedReference resolves to null");
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                UnityEngine.Object.DestroyImmediate(clip);
                UnityEngine.Object.DestroyImmediate(ownerGo);
            }
        }

        [Test]
        public void CameraClipDefaultImportScaleMatchesPmxImportDefault()
        {
            MmdVmdCameraClip clip = ScriptableObject.CreateInstance<MmdVmdCameraClip>();
            try
            {
                Assert.That(clip.ImportScale, Is.EqualTo(MmdPmxAsset.DefaultImportScale).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        private static byte[] BuildCameraVmdBytes()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            WriteFixedAscii(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedAscii(writer, "camera_test", 20);
            writer.Write(0u); // bone frames
            writer.Write(0u); // morph frames
            writer.Write(2u); // camera frames
            WriteCameraFrame(writer, 0u, -40f, 0f, 10f, 0f, 0f, 0f, 0f, 20u, 0);
            WriteCameraFrame(writer, 30u, -20f, 2f, 20f, -4f, 0.1f, 0.2f, 0.1f, 40u, 0);
            writer.Write(0u); // light frames
            writer.Write(0u); // self-shadow frames
            writer.Write(0u); // property frames
            return stream.ToArray();
        }

        private static byte[] BuildCameraAndLightVmdBytes()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            WriteFixedAscii(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedAscii(writer, "camera_light", 20);
            writer.Write(0u); // bone frames
            writer.Write(0u); // morph frames
            writer.Write(2u); // camera frames
            WriteCameraFrame(writer, 0u, -40f, 0f, 10f, 0f, 0f, 0f, 0f, 20u, 0);
            WriteCameraFrame(writer, 30u, -20f, 2f, 20f, -4f, 0.1f, 0.2f, 0.1f, 40u, 0);
            writer.Write(2u); // light frames
            WriteLightFrame(writer, 0u, 0.2f, 0.2f, 0.2f, -0.5f, -1f, 0.5f);
            WriteLightFrame(writer, 30u, 0.8f, 0.8f, 0.8f, 0f, -1f, 0f);
            writer.Write(0u); // self-shadow frames
            writer.Write(0u); // property frames
            return stream.ToArray();
        }

        private static byte[] BuildLightOnlyVmdBytes()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            WriteFixedAscii(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedAscii(writer, "light_shadow", 20);
            writer.Write(0u); // bone frames
            writer.Write(0u); // morph frames
            writer.Write(0u); // camera frames
            writer.Write(2u); // light frames
            WriteLightFrame(writer, 10u, 0.0f, 0.0f, 1.0f, 1.0f, 0.0f, 0.0f);
            WriteLightFrame(writer, 30u, 1.0f, 0.5f, 0.0f, 0.0f, -1.0f, 0.0f);
            writer.Write(0u); // self-shadow frames
            writer.Write(0u); // property frames
            return stream.ToArray();
        }

        private static void WriteCameraFrame(
            BinaryWriter writer,
            uint frame,
            float distance,
            float posX, float posY, float posZ,
            float rotX, float rotY, float rotZ,
            uint viewAngle,
            byte perspective)
        {
            writer.Write(frame);
            writer.Write(distance);
            writer.Write(posX);
            writer.Write(posY);
            writer.Write(posZ);
            writer.Write(rotX);
            writer.Write(rotY);
            writer.Write(rotZ);
            writer.Write(new byte[24]); // interpolation
            writer.Write(viewAngle);
            writer.Write(perspective);
        }

        private static void WriteLightFrame(
            BinaryWriter writer,
            uint frame,
            float r,
            float g,
            float b,
            float x,
            float y,
            float z)
        {
            writer.Write(frame);
            writer.Write(r);
            writer.Write(g);
            writer.Write(b);
            writer.Write(x);
            writer.Write(y);
            writer.Write(z);
        }

        private static MmdCameraState SampleNativeCamera(byte[] vmdBytes, float frame)
        {
            float[] buf = new float[9];
            byte ok = MmdRuntimeFfiMethods.VmdSampleCamera(
                vmdBytes, new IntPtr(vmdBytes.Length), frame, buf, new IntPtr(buf.Length));
            Assert.That(ok, Is.EqualTo((byte)1), $"Native camera sample failed at frame {frame}");
            return new MmdCameraState(buf[0],
                new[] { buf[1], buf[2], buf[3] },
                new[] { buf[4], buf[5], buf[6] },
                buf[7], buf[8] != 0.0f);
        }

        private static MmdLightState SampleNativeLight(byte[] vmdBytes, float frame)
        {
            float[] buf = new float[6];
            byte ok = MmdRuntimeFfiMethods.VmdSampleLight(
                vmdBytes, new IntPtr(vmdBytes.Length), frame, buf, new IntPtr(buf.Length));
            Assert.That(ok, Is.EqualTo((byte)1), $"Native light sample failed at frame {frame}");
            return new MmdLightState(
                new[] { buf[0], buf[1], buf[2] },
                new[] { buf[3], buf[4], buf[5] });
        }

        private static void WriteFixedAscii(BinaryWriter writer, string text, int byteLength)
        {
            byte[] bytes = new byte[byteLength];
            byte[] source = System.Text.Encoding.ASCII.GetBytes(text);
            Array.Copy(source, bytes, Math.Min(source.Length, bytes.Length));
            writer.Write(bytes);
        }
    }
}
