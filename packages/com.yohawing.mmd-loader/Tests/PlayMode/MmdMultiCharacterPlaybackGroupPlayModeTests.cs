#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Mmd.Tests.PlayMode
{
    public sealed class MmdMultiCharacterPlaybackGroupPlayModeTests
    {
        private const float FrameRate = 30.0f;
        private const int InitialFrame = 12;

        [UnityTest]
        public IEnumerator TwoAndFourCharactersMatchSerialAndTimelineFallsBackCleanly()
        {
            string pmxPath = ResolveFixture("test_1bone_cube.pmx");
            string vmdPath = ResolveFixture("test_1bone_cube_motion.vmd");
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            pmxAsset.Initialize(File.ReadAllBytes(pmxPath), Path.GetFileName(pmxPath), pmxPath);
            vmdAsset.Initialize(File.ReadAllBytes(vmdPath), Path.GetFileName(vmdPath), vmdPath);

            try
            {
                foreach (int characterCount in new[] { 2, 4 })
                {
                    var serial = new List<PlaybackFixture>(characterCount);
                    var grouped = new List<PlaybackFixture>(characterCount);
                    var groupRoot = new GameObject($"multi-character-{characterCount}");
                    try
                    {
                        for (int i = 0; i < characterCount; i++)
                        {
                            serial.Add(CreateFixture(pmxAsset, vmdAsset, $"serial-{i}"));
                            PlaybackFixture groupedFixture =
                                CreateFixture(pmxAsset, vmdAsset, $"grouped-{i}");
                            groupedFixture.Root.transform.SetParent(
                                groupRoot.transform,
                                worldPositionStays: false);
                            groupedFixture.Controller.Play();
                            grouped.Add(groupedFixture);
                        }

                        // Let controller Start run before attaching authored sources. The fixture
                        // binding is already configured, so Start must not try to rebuild it from
                        // transient ScriptableObjects that have no imported material sub-assets.
                        yield return null;
                        for (int i = 0; i < characterCount; i++)
                        {
                            AttachSources(serial[i], pmxAsset, vmdAsset);
                            AttachSources(grouped[i], pmxAsset, vmdAsset);
                        }

                        MmdMultiCharacterPlaybackGroup group =
                            groupRoot.AddComponent<MmdMultiCharacterPlaybackGroup>();
                        UpdateObservation observation = groupRoot.AddComponent<UpdateObservation>();
                        observation.Track(grouped[0].Controller, livePhysics: false);
                        var poseAppliedCounts = new int[characterCount];
                        var poseAppliedFrames = new int[characterCount];
                        for (int i = 0; i < characterCount; i++)
                        {
                            int index = i;
                            grouped[i].Controller.PoseApplied += snapshot =>
                            {
                                poseAppliedCounts[index]++;
                                poseAppliedFrames[index] = snapshot.frame.frame;
                            };
                        }
                        for (int frame = 0; frame < 5; frame++)
                        {
                            yield return null;
                        }

                        Assert.That(group.IsPlaybackActive, Is.True, group.LastFailureReason);
                        AssertAfterUpdate(observation);
                        for (int i = 0; i < characterCount; i++)
                        {
                            Assert.That(poseAppliedCounts[i], Is.GreaterThan(0));
                            Assert.That(
                                poseAppliedFrames[i],
                                Is.EqualTo(grouped[i].Controller.LastSnapshot!.frame.frame));
                        }
                        int evaluatedFrame = grouped[0].Controller.LastSnapshot?.frame.frame ??
                            grouped[0].Controller.CurrentFrame;
                        Assert.That(evaluatedFrame, Is.GreaterThanOrEqualTo(InitialFrame));
                        for (int i = 0; i < characterCount; i++)
                        {
                            Assert.That(
                                grouped[i].Controller.CurrentFrame,
                                Is.GreaterThanOrEqualTo(evaluatedFrame));
                            serial[i].Controller.ApplyFrame(evaluatedFrame);
                            AssertPoseEqual(serial[i].Instance, grouped[i].Instance);
                        }

                        MmdPlaybackSnapshot timelineSnapshot = grouped[0].Controller.ApplyTimelineTime(
                            evaluatedFrame / FrameRate,
                            FrameRate);
                        Assert.That(timelineSnapshot.frame.frame, Is.EqualTo(evaluatedFrame));
                        Assert.That(group.enabled, Is.False);
                        Assert.That(group.IsPlaybackActive, Is.False);
                        Assert.That(grouped[0].Controller.IsMultiCharacterClaimed, Is.False);
                        Assert.That(group.LastFailureReason, Does.Contain("serial playback"));
                    }
                    finally
                    {
                        DestroyFixtures(serial);
                        DestroyFixtures(grouped);
                        Object.DestroyImmediate(groupRoot);
                    }

                    yield return null;
                }
            }
            finally
            {
                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
            }
        }

        [UnityTest]
        public IEnumerator OneCharacterGroupLeavesControllerOnSerialUpdatePath()
        {
            string pmxPath = ResolveFixture("test_1bone_cube.pmx");
            string vmdPath = ResolveFixture("test_1bone_cube_motion.vmd");
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            pmxAsset.Initialize(File.ReadAllBytes(pmxPath), Path.GetFileName(pmxPath), pmxPath);
            vmdAsset.Initialize(File.ReadAllBytes(vmdPath), Path.GetFileName(vmdPath), vmdPath);
            PlaybackFixture fixture = CreateFixture(pmxAsset, vmdAsset, "single-character");
            var groupRoot = new GameObject("single-character-group");
            MmdMultiCharacterPlaybackGroup? group = null;
            float previousCaptureDeltaTime = Time.captureDeltaTime;
            float previousTimeScale = Time.timeScale;
            Time.captureDeltaTime = 1.0f / FrameRate;
            Time.timeScale = 1.0f;

            try
            {
                fixture.Root.transform.SetParent(groupRoot.transform, worldPositionStays: false);
                yield return null;
                AttachSources(fixture, pmxAsset, vmdAsset, initialFrame: 0);
                int poseAppliedCount = 0;
                fixture.Controller.PoseApplied += _ => poseAppliedCount++;
                fixture.Controller.Play();
                group = groupRoot.AddComponent<MmdMultiCharacterPlaybackGroup>();
                UpdateObservation observation = groupRoot.AddComponent<UpdateObservation>();
                observation.Track(fixture.Controller, livePhysics: false);

                yield return WaitForPostUpdate(
                    observation,
                    () => poseAppliedCount > 0 && fixture.Controller.CurrentFrame > 0);

                Assert.That(fixture.Controller.CurrentFrame, Is.GreaterThan(0));
                Assert.That(poseAppliedCount, Is.GreaterThan(0));
                AssertAfterUpdate(observation);
                Assert.That(group.IsPlaybackActive, Is.False);
                Assert.That(group.HasWorkerPool, Is.False);
                Assert.That(fixture.Controller.IsMultiCharacterClaimed, Is.False);
                Assert.That(group.LastFailureReason, Does.Contain("serial playback"));
            }
            finally
            {
                Time.captureDeltaTime = previousCaptureDeltaTime;
                Time.timeScale = previousTimeScale;
                if (group != null)
                {
                    Object.DestroyImmediate(group);
                }

                DestroyFixtures(new List<PlaybackFixture> { fixture });
                Object.DestroyImmediate(groupRoot);
                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
            }
        }

        [UnityTest]
        public IEnumerator TwoCharacterWorkerAppliesBeforeParentConstraintEvaluates()
        {
            string pmxPath = ResolveFixture("test_1bone_cube.pmx");
            string vmdPath = ResolveFixture("test_1bone_cube_motion.vmd");
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            pmxAsset.Initialize(File.ReadAllBytes(pmxPath), Path.GetFileName(pmxPath), pmxPath);
            vmdAsset.Initialize(File.ReadAllBytes(vmdPath), Path.GetFileName(vmdPath), vmdPath);
            var fixtures = new List<PlaybackFixture>(2);
            var groupRoot = new GameObject("parent-constraint-worker-group");
            GameObject? constraintSource = null;
            GameObject? constrainedObject = null;
            MmdMultiCharacterPlaybackGroup? group = null;
            ParentConstraintObservation? observation = null;

            try
            {
                for (int i = 0; i < 2; i++)
                {
                    PlaybackFixture fixture = CreateFixture(pmxAsset, vmdAsset, $"constraint-grouped-{i}");
                    fixture.Root.transform.SetParent(groupRoot.transform, worldPositionStays: false);
                    fixtures.Add(fixture);
                }

                yield return null;
                for (int i = 0; i < fixtures.Count; i++)
                {
                    AttachSources(fixtures[i], pmxAsset, vmdAsset, initialFrame: 0);
                    fixtures[i].Controller.Play();
                }

                constraintSource = new GameObject("parent-constraint-source");
                constraintSource.transform.SetParent(groupRoot.transform, worldPositionStays: false);
                Transform source = constraintSource.transform;
                constrainedObject = new GameObject("parent-constraint-target");
                constrainedObject.transform.SetParent(groupRoot.transform, worldPositionStays: false);
                constrainedObject.transform.SetPositionAndRotation(source.position, source.rotation);
                ParentConstraint constraint = constrainedObject.AddComponent<ParentConstraint>();
                constraint.AddSource(new ConstraintSource { sourceTransform = source, weight = 1.0f });
                constraint.SetTranslationOffset(0, Vector3.zero);
                constraint.SetRotationOffset(0, Vector3.zero);
                constraint.constraintActive = true;
                constraint.locked = true;
                observation = constrainedObject.AddComponent<ParentConstraintObservation>();
                observation.Track(source, constrainedObject.transform);
                fixtures[0].Controller.PoseApplied += _ =>
                    source.localPosition += new Vector3(0.01f, 0.0f, 0.0f);
                group = groupRoot.AddComponent<MmdMultiCharacterPlaybackGroup>();

                for (int step = 0; step < 120 && !observation.ObservedTravel; step++)
                {
                    yield return null;
                }

                Assert.That(observation.TotalTravel, Is.GreaterThan(1.0e-5f));
                Assert.That(observation.ObservedTravel, Is.True, "The motion fixture did not provide a travel frame.");
                Assert.That(
                    observation.MaximumGapOnTravel,
                    Is.LessThanOrEqualTo(1.0e-4f),
                    "ParentConstraint did not consume the worker-applied pose in the same frame.");
                Assert.That(group.IsPlaybackActive, Is.True, group.LastFailureReason);
                Assert.That(group.HasWorkerPool, Is.True);
            }
            finally
            {
                if (group != null)
                {
                    Object.DestroyImmediate(group);
                }

                if (constrainedObject != null)
                {
                    Object.DestroyImmediate(constrainedObject);
                }

                if (constraintSource != null)
                {
                    Object.DestroyImmediate(constraintSource);
                }

                DestroyFixtures(fixtures);
                Object.DestroyImmediate(groupRoot);
                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
            }
        }

        [UnityTest]
        public IEnumerator TwoAndFourLiveCharactersMatchSerialAndReleaseWorkerOwnershipCleanly()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
                yield break;
            }

            string pmxPath = ResolveFixture("test_hair_physics.pmx");
            string vmdPath = ResolveFixture("test_1bone_cube_motion.vmd");
            byte[] pmxBytes = File.ReadAllBytes(pmxPath);
            byte[] vmdBytes = File.ReadAllBytes(vmdPath);
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            pmxAsset.Initialize(pmxBytes, Path.GetFileName(pmxPath), pmxPath);
            vmdAsset.Initialize(vmdBytes, Path.GetFileName(vmdPath), vmdPath);
            float previousCaptureDeltaTime = Time.captureDeltaTime;
            float previousTimeScale = Time.timeScale;
            Time.captureDeltaTime = 1.0f / FrameRate;
            Time.timeScale = 1.0f;

            try
            {
                foreach (int characterCount in new[] { 2, 4 })
                {
                    Time.timeScale = 1.0f;
                    var serial = new List<PlaybackFixture>(characterCount);
                    var grouped = new List<PlaybackFixture>(characterCount);
                    var groupRoot = new GameObject($"multi-character-live-{characterCount}");
                    MmdMultiCharacterPlaybackGroup? group = null;
                    try
                    {
                        for (int i = 0; i < characterCount; i++)
                        {
                            serial.Add(CreateLiveFixture(
                                pmxBytes,
                                vmdBytes,
                                pmxPath,
                                $"serial-live-{i}"));
                            PlaybackFixture groupedFixture = CreateLiveFixture(
                                pmxBytes,
                                vmdBytes,
                                pmxPath,
                                $"grouped-live-{i}");
                            groupedFixture.Root.transform.SetParent(groupRoot.transform, worldPositionStays: false);
                            grouped.Add(groupedFixture);
                        }

                        yield return null;
                        for (int i = 0; i < characterCount; i++)
                        {
                            AttachSources(serial[i], pmxAsset, vmdAsset, initialFrame: 0);
                            AttachSources(grouped[i], pmxAsset, vmdAsset, initialFrame: 0);
                            serial[i].Controller.SetPhysicsMode(MmdPhysicsMode.Live);
                            grouped[i].Controller.SetPhysicsMode(MmdPhysicsMode.Live);
                            grouped[i].Controller.Play();
                        }

                        group = groupRoot.AddComponent<MmdMultiCharacterPlaybackGroup>();
                        for (int step = 0; step < 4; step++)
                        {
                            yield return null;
                            int currentFrame = grouped[0].Controller.CurrentFrame;
                            for (int i = 0; i < characterCount; i++)
                            {
                                serial[i].Controller.ApplyFrame(currentFrame);
                                AssertPhysicsBodiesEqual(serial[i].Instance, grouped[i].Instance);
                                AssertPoseEqual(serial[i].Instance, grouped[i].Instance);
                            }
                        }

                        Assert.That(group.IsPlaybackActive, Is.True, group.LastFailureReason);
                        Assert.That(group.HasWorkerPool, Is.True);
                        int evaluatedFrame = grouped[0].Controller.LastLivePhysicsDiagnostics?.frame ??
                            grouped[0].Controller.CurrentFrame;
                        Assert.That(evaluatedFrame, Is.GreaterThanOrEqualTo(2));
                        for (int i = 0; i < characterCount; i++)
                        {
                            Assert.That(
                                grouped[i].Controller.CurrentFrame,
                                Is.EqualTo(evaluatedFrame));
                            Assert.That(grouped[i].Controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                            Assert.That(
                                grouped[i].Controller.LastLivePhysicsDiagnostics!.frame,
                                Is.EqualTo(evaluatedFrame));
                        }

                        if (characterCount == 2)
                        {
                            Time.timeScale = 0.0f;
                            yield return null;
                            int heldFrame = grouped[0].Controller.CurrentFrame;
                            MmdLivePhysicsFrameDiagnostics? heldDiagnostics =
                                grouped[0].Controller.LastLivePhysicsDiagnostics;
                            Vector3 heldBodyPosition = grouped[0].Instance.PhysicsBodies[0].NativePosition;
                            yield return null;
                            Assert.That(grouped[0].Controller.CurrentFrame, Is.EqualTo(heldFrame));
                            Assert.That(grouped[0].Controller.LastLivePhysicsDiagnostics, Is.SameAs(heldDiagnostics));
                            Assert.That(
                                grouped[0].Instance.PhysicsBodies[0].NativePosition,
                                Is.EqualTo(heldBodyPosition));

                            for (int i = 0; i < grouped.Count; i++)
                            {
                                grouped[i].Controller.Pause();
                            }

                            int pausedFrame = grouped[0].Controller.CurrentFrame;
                            MmdLivePhysicsFrameDiagnostics? pausedDiagnostics =
                                grouped[0].Controller.LastLivePhysicsDiagnostics;
                            yield return null;
                            Assert.That(group.IsPlaybackActive, Is.True);
                            Assert.That(group.HasWorkerPool, Is.True);
                            Assert.That(grouped[0].Controller.CurrentFrame, Is.EqualTo(pausedFrame));
                            Assert.That(
                                grouped[0].Controller.LastLivePhysicsDiagnostics,
                                Is.SameAs(pausedDiagnostics));

                            Time.timeScale = 1.0f;
                            for (int i = 0; i < grouped.Count; i++)
                            {
                                grouped[i].Controller.Play();
                            }

                            yield return null;
                            yield return null;
                            Assert.That(grouped[0].Controller.CurrentFrame, Is.GreaterThan(evaluatedFrame));
                            Assert.That(grouped[0].Controller.LastLivePhysicsDiagnostics, Is.Not.SameAs(heldDiagnostics));

                            group.enabled = false;
                            Assert.That(group.HasWorkerPool, Is.False);
                            for (int i = 0; i < grouped.Count; i++)
                            {
                                Assert.That(grouped[i].Controller.IsMultiCharacterClaimed, Is.False);
                            }

                            group.enabled = true;
                            yield return null;
                            Assert.That(group.IsPlaybackActive, Is.True, group.LastFailureReason);
                            Assert.That(group.HasWorkerPool, Is.True);

                            int timelineFrame = grouped[0].Controller.CurrentFrame + 2;
                            MmdPlaybackSnapshot timelineSnapshot = grouped[0].Controller.ApplyTimelineTime(
                                timelineFrame / FrameRate,
                                FrameRate);
                            for (int i = 0; i < grouped.Count; i++)
                            {
                                grouped[i].Controller.Pause();
                            }

                            Assert.That(timelineSnapshot.frame.frame, Is.EqualTo(timelineFrame));
                            Assert.That(group.enabled, Is.False);
                            Assert.That(group.HasWorkerPool, Is.False);
                            Assert.That(grouped[0].Controller.IsMultiCharacterClaimed, Is.False);
                            Assert.That(grouped[0].Controller.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Live));
                            Assert.That(group.LastFailureReason, Does.Contain("serial playback"));
                            Assert.That(
                                grouped[1].Controller.CurrentFrame,
                                Is.Not.EqualTo(grouped[0].Controller.CurrentFrame));

                            // Timeline suppresses self/group ticks through the following player-loop
                            // frame. Keep both serial controllers paused until that ownership window
                            // expires so the next group failure is specifically the frame divergence.
                            yield return null;
                            yield return null;
                            group.enabled = true;
                            yield return null;
                            Assert.That(group.enabled, Is.False);
                            Assert.That(group.IsPlaybackActive, Is.False);
                            Assert.That(group.HasWorkerPool, Is.False);
                            Assert.That(group.LastFailureReason, Does.Contain("logical frame"));

                            int resynchronizedFrame = timelineFrame + 1;
                            for (int i = 0; i < grouped.Count; i++)
                            {
                                MmdPlaybackSnapshot synchronized = grouped[i].Controller.ApplyTimelineTime(
                                    resynchronizedFrame / FrameRate,
                                    FrameRate);
                                Assert.That(synchronized.frame.frame, Is.EqualTo(resynchronizedFrame));
                            }

                            yield return null;
                            group.enabled = true;
                            Assert.That(group.IsPlaybackActive, Is.True, group.LastFailureReason);
                            Assert.That(group.HasWorkerPool, Is.False);
                            for (int i = 0; i < grouped.Count; i++)
                            {
                                grouped[i].Controller.Play();
                            }

                            yield return null;
                            Assert.That(group.HasWorkerPool, Is.True);
                            Object.DestroyImmediate(group);
                            group = null;
                            for (int i = 0; i < grouped.Count; i++)
                            {
                                Assert.That(grouped[i].Controller.IsMultiCharacterClaimed, Is.False);
                            }
                        }
                    }
                    finally
                    {
                        Time.timeScale = previousTimeScale;
                        if (group != null)
                        {
                            Object.DestroyImmediate(group);
                        }

                        DestroyFixtures(serial);
                        DestroyFixtures(grouped);
                        Object.DestroyImmediate(groupRoot);
                    }

                    yield return null;
                }
            }
            finally
            {
                Time.captureDeltaTime = previousCaptureDeltaTime;
                Time.timeScale = previousTimeScale;
                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
            }
        }

        private static PlaybackFixture CreateFixture(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            string name)
        {
            MmdUnityPlaybackBinding binding = MmdUnityPlaybackBinding.CreateSkinned(pmxAsset, vmdAsset);
            binding.Instance.Root.name = name;
            MmdUnityPlaybackController controller =
                binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
            controller.SetPhysicsMode(Mmd.Physics.MmdPhysicsMode.Off);
            controller.Configure(binding, FrameRate, playOnStart: false);
            controller.ApplyFrame(0);
            return new PlaybackFixture(binding.Instance, controller);
        }

        private static PlaybackFixture CreateLiveFixture(
            byte[] pmxBytes,
            byte[] vmdBytes,
            string pmxPath,
            string name)
        {
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(pmxBytes);
            model.physics.joints.RemoveAll(joint =>
                joint.rigidbodyAIndex < 0 && joint.rigidbodyBIndex < 0);
            MmdMotionDefinition motion = parser.LoadMotion(vmdBytes);
            MmdUnityPlaybackBinding binding = MmdUnityPlaybackBinding.CreateSkinned(
                model,
                motion,
                Path.GetFileName(pmxPath),
                "test_1bone_cube_motion.vmd",
                pmxPath);
            binding.Instance.Root.name = name;
            MmdUnityPlaybackController controller =
                binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
            controller.Configure(binding, FrameRate, playOnStart: false);
            return new PlaybackFixture(binding.Instance, controller);
        }

        private static void AttachSources(
            PlaybackFixture fixture,
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            int initialFrame = InitialFrame)
        {
            MmdUnityPlaybackController controller = fixture.Controller;
            controller.ConfigureModelAsset(pmxAsset);
            controller.ConfigureMotionAsset(vmdAsset);
            Assert.That(controller.TryEnableFastRuntimeFromConfiguredSource(out string reason), Is.True, reason);
            controller.SeekFrame(initialFrame);
        }

        private static void AssertPoseEqual(
            MmdUnityModelInstance expected,
            MmdUnityModelInstance actual)
        {
            Assert.That(actual.BoneTransforms.Length, Is.EqualTo(expected.BoneTransforms.Length));
            for (int bone = 0; bone < expected.BoneTransforms.Length; bone++)
            {
                Matrix4x4 expectedMatrix = expected.BoneTransforms[bone].localToWorldMatrix;
                Matrix4x4 actualMatrix = actual.BoneTransforms[bone].localToWorldMatrix;
                for (int row = 0; row < 4; row++)
                {
                    for (int column = 0; column < 4; column++)
                    {
                        Assert.That(
                            actualMatrix[row, column],
                            Is.EqualTo(expectedMatrix[row, column]).Within(1.0e-5f),
                            $"bone={bone}, row={row}, column={column}");
                    }
                }
            }
        }

        private static void AssertPhysicsBodiesEqual(
            MmdUnityModelInstance expected,
            MmdUnityModelInstance actual)
        {
            Assert.That(actual.PhysicsBodies.Length, Is.EqualTo(expected.PhysicsBodies.Length));
            for (int body = 0; body < expected.PhysicsBodies.Length; body++)
            {
                MmdUnityPhysicsBody expectedBody = expected.PhysicsBodies[body];
                MmdUnityPhysicsBody actualBody = actual.PhysicsBodies[body];
                Assert.That(actualBody.HasNativeTransform, Is.EqualTo(expectedBody.HasNativeTransform), $"body={body}");
                Assert.That(
                    Vector3.Distance(actualBody.NativePosition, expectedBody.NativePosition),
                    Is.LessThanOrEqualTo(1.0e-5f),
                    $"body={body} position");
                Assert.That(
                    Quaternion.Angle(actualBody.NativeRotation, expectedBody.NativeRotation),
                    Is.LessThanOrEqualTo(1.0e-4f),
                    $"body={body} rotation");
            }
        }

        private static IEnumerator WaitForPostUpdate(
            UpdateObservation observation,
            Func<bool>? additionalCondition = null)
        {
            observation.Clear();
            for (int frame = 0; frame < 120; frame++)
            {
                if (Application.isBatchMode)
                {
                    yield return null;
                }
                else
                {
                    yield return new WaitForEndOfFrame();
                }

                if (observation.HasObservation && (additionalCondition?.Invoke() ?? true))
                {
                    yield break;
                }
            }

            Assert.Fail("The group did not satisfy its post-Update observation within 120 frames.");
        }

        private static void AssertAfterUpdate(UpdateObservation observation)
        {
            Assert.That(observation.HasObservation, Is.True);
            Assert.That(observation.AppliedFrame, Is.EqualTo(observation.CurrentFrameAtUpdate));
        }

        [DefaultExecutionOrder(1000)]
        private sealed class UpdateObservation : MonoBehaviour
        {
            private MmdUnityPlaybackController? controller;
            private bool livePhysics;

            internal bool HasObservation { get; private set; }

            internal int AppliedFrame { get; private set; } = -1;

            internal int CurrentFrameAtUpdate { get; private set; } = -1;

            internal void Track(MmdUnityPlaybackController target, bool livePhysics)
            {
                controller = target ?? throw new ArgumentNullException(nameof(target));
                this.livePhysics = livePhysics;
            }

            internal void Clear()
            {
                HasObservation = false;
                AppliedFrame = -1;
                CurrentFrameAtUpdate = -1;
            }

            private void Update()
            {
                if (controller == null)
                {
                    return;
                }

                int? appliedFrame = livePhysics
                    ? controller.LastLivePhysicsDiagnostics?.frame
                    : controller.LastSnapshot?.frame.frame;
                if (!appliedFrame.HasValue)
                {
                    return;
                }

                AppliedFrame = appliedFrame.Value;
                CurrentFrameAtUpdate = controller.CurrentFrame;
                HasObservation = true;
            }
        }

        [DefaultExecutionOrder(1000)]
        private sealed class ParentConstraintObservation : MonoBehaviour
        {
            private Transform? source;
            private Transform? target;
            private Vector3 previousSourcePosition;

            internal bool ObservedTravel { get; private set; }

            internal float TotalTravel { get; private set; }

            internal float MaximumGapOnTravel { get; private set; }

            internal void Track(Transform sourceTransform, Transform targetTransform)
            {
                source = sourceTransform ?? throw new ArgumentNullException(nameof(sourceTransform));
                target = targetTransform ?? throw new ArgumentNullException(nameof(targetTransform));
                previousSourcePosition = source.position;
            }

            private void LateUpdate()
            {
                if (source == null || target == null)
                {
                    return;
                }

                float travel = Vector3.Distance(source.position, previousSourcePosition);
                previousSourcePosition = source.position;
                TotalTravel += travel;
                if (travel <= 1.0e-5f)
                {
                    return;
                }

                ObservedTravel = true;
                MaximumGapOnTravel = Mathf.Max(
                    MaximumGapOnTravel,
                    Vector3.Distance(source.position, target.position));
            }
        }

        private static void DestroyFixtures(List<PlaybackFixture> fixtures)
        {
            for (int i = fixtures.Count - 1; i >= 0; i--)
            {
                if (fixtures[i].Root != null)
                {
                    Object.DestroyImmediate(fixtures[i].Root);
                }
            }
        }

        private static string ResolveFixture(string fileName)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Packages",
                "com.yohawing.mmd-loader",
                "Tests",
                "Fixtures",
                "Assets",
                fileName));
        }

        private readonly struct PlaybackFixture
        {
            internal PlaybackFixture(
                MmdUnityModelInstance instance,
                MmdUnityPlaybackController controller)
            {
                Instance = instance;
                Controller = controller;
            }

            internal MmdUnityModelInstance Instance { get; }

            internal GameObject Root => Instance.Root;

            internal MmdUnityPlaybackController Controller { get; }
        }
    }
}
