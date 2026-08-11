#nullable enable

using System.Collections;
using Mmd.Motion;
using Mmd.UnityIntegration;
using Mmd.UnityIntegration.Cinemachine;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace Mmd.Tests.Cinemachine
{
    public sealed class MmdCinemachineBrainPlayModeTests
    {
        [UnityTest]
        public IEnumerator BrainIsTheOnlyWriterOfTheOutputCamera()
        {
            var outputGo = new GameObject("output camera");
            var sourceGo = new GameObject("source camera");
            var bindingGo = new GameObject("binding");
            try
            {
                Camera output = outputGo.AddComponent<Camera>();
                CinemachineBrain brain = outputGo.AddComponent<CinemachineBrain>();
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                CinemachineCamera source = sourceGo.AddComponent<CinemachineCamera>();
                MmdCinemachineCameraBinding binding = bindingGo.AddComponent<MmdCinemachineCameraBinding>();
                binding.TargetCinemachineCamera = source;
                yield return null;

                output.transform.SetPositionAndRotation(new Vector3(20f, 30f, 40f), Quaternion.identity);
                output.fieldOfView = 64f;
                Vector3 sentinel = output.transform.position;
                Assert.That(binding.ApplyCameraState(State(-35f, 1f, 8f, -2f, 0.1f, 0.3f, 0.2f, 38f)),
                    Is.EqualTo(MmdSceneCameraApplyStatus.Applied));

                Assert.That(output.transform.position, Is.EqualTo(sentinel));
                Assert.That(output.fieldOfView, Is.EqualTo(64f));

                brain.ManualUpdate(1, -1f);

                Assert.That(Vector3.Distance(output.transform.position, source.transform.position), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(output.transform.rotation, source.transform.rotation), Is.LessThan(0.05f));
                Assert.That(output.fieldOfView, Is.EqualTo(source.Lens.FieldOfView).Within(0.001f));
            }
            finally
            {
                Object.Destroy(outputGo);
                Object.Destroy(sourceGo);
                Object.Destroy(bindingGo);
            }
        }

        [UnityTest]
        public IEnumerator TimelineShotsCutAndBlendBetweenUserSuppliedCameras()
        {
            var outputGo = new GameObject("output camera");
            var sourceAGo = new GameObject("shot A");
            var sourceBGo = new GameObject("shot B");
            var directorGo = new GameObject("director");
            TimelineAsset? timeline = null;
            try
            {
                outputGo.AddComponent<Camera>();
                CinemachineBrain brain = outputGo.AddComponent<CinemachineBrain>();
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                CinemachineCamera sourceA = sourceAGo.AddComponent<CinemachineCamera>();
                CinemachineCamera sourceB = sourceBGo.AddComponent<CinemachineCamera>();
                SetSourcePose(sourceA, new Vector3(-10f, 2f, 5f), Quaternion.Euler(0f, 10f, 0f), 30f);
                SetSourcePose(sourceB, new Vector3(10f, 6f, -5f), Quaternion.Euler(5f, 80f, 0f), 60f);

                PlayableDirector director = directorGo.AddComponent<PlayableDirector>();
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                CinemachineTrack track = timeline.CreateTrack<CinemachineTrack>(null, "shots");
                AddShot(director, track, sourceA, "shot-a", 0.0, 2.0, 0.0, 1.0);
                AddShot(director, track, sourceB, "shot-b", 1.0, 2.0, 1.0, 0.0);
                director.playableAsset = timeline;
                director.SetGenericBinding(track, brain);
                director.timeUpdateMode = DirectorUpdateMode.Manual;
                director.RebuildGraph();
                yield return null;

                Evaluate(director, brain, 0.5, 10);
                Assert.That(Vector3.Distance(outputGo.transform.position, sourceA.transform.position), Is.LessThan(0.001f));

                Evaluate(director, brain, 1.5, 11);
                Assert.That(brain.ActiveBlend, Is.Not.Null);
                Assert.That(outputGo.transform.position.x, Is.GreaterThan(sourceA.transform.position.x));
                Assert.That(outputGo.transform.position.x, Is.LessThan(sourceB.transform.position.x));
                Assert.That(outputGo.GetComponent<Camera>().fieldOfView, Is.InRange(30.01f, 59.99f));

                Evaluate(director, brain, 2.5, 12);
                Assert.That(Vector3.Distance(outputGo.transform.position, sourceB.transform.position), Is.LessThan(0.001f));
                Assert.That(outputGo.GetComponent<Camera>().fieldOfView, Is.EqualTo(60f).Within(0.001f));
            }
            finally
            {
                if (timeline != null)
                {
                    Object.Destroy(timeline);
                }
                Object.Destroy(directorGo);
                Object.Destroy(sourceBGo);
                Object.Destroy(sourceAGo);
                Object.Destroy(outputGo);
            }
        }

        private static void AddShot(
            PlayableDirector director,
            CinemachineTrack track,
            CinemachineCamera camera,
            string exposedName,
            double start,
            double duration,
            double easeInDuration,
            double easeOutDuration)
        {
            TimelineClip clip = track.CreateClip<CinemachineShot>();
            clip.start = start;
            clip.duration = duration;
            clip.easeInDuration = easeInDuration;
            clip.easeOutDuration = easeOutDuration;
            var shot = (CinemachineShot)clip.asset;
            var propertyName = new PropertyName(exposedName);
            shot.VirtualCamera = new ExposedReference<CinemachineVirtualCameraBase> { exposedName = propertyName };
            director.SetReferenceValue(propertyName, camera);
        }

        private static void Evaluate(PlayableDirector director, CinemachineBrain brain, double time, int frame)
        {
            director.time = time;
            director.Evaluate();
            brain.ManualUpdate(frame, -1f);
        }

        private static void SetSourcePose(CinemachineCamera camera, Vector3 position, Quaternion rotation, float fov)
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            LensSettings lens = camera.Lens;
            lens.FieldOfView = fov;
            lens.ModeOverride = LensSettings.OverrideModes.Perspective;
            camera.Lens = lens;
        }

        private static MmdCameraState State(
            float distance, float px, float py, float pz, float rx, float ry, float rz, float fov)
        {
            return new MmdCameraState(distance, new[] { px, py, pz }, new[] { rx, ry, rz }, fov, true);
        }
    }
}
