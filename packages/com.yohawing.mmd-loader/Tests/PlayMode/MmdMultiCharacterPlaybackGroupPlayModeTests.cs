#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEngine;
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
                        for (int frame = 0; frame < 5; frame++)
                        {
                            yield return null;
                        }

                        Assert.That(group.IsPlaybackActive, Is.True, group.LastFailureReason);
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
        public IEnumerator OneCharacterWorkerSupportsPhysicsOffAndLiveParityAndLifecycle()
        {
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
                var offSerial = CreateFixture(pmxAsset, vmdAsset, "single-off-serial");
                var offGrouped = CreateFixture(pmxAsset, vmdAsset, "single-off-grouped");
                var offRoot = new GameObject("single-off-group");
                MmdMultiCharacterPlaybackGroup? offGroup = null;
                LateUpdateObservation? offObservation = null;
                try
                {
                    offGrouped.Root.transform.SetParent(offRoot.transform, worldPositionStays: false);
                    yield return null;
                    AttachSources(offSerial, pmxAsset, vmdAsset);
                    AttachSources(offGrouped, pmxAsset, vmdAsset);
                    int mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                    int poseAppliedCount = 0;
                    int poseAppliedThreadId = -1;
                    MmdPlaybackSnapshot? poseAppliedSnapshot = null;
                    offGrouped.Controller.PoseApplied += snapshot =>
                    {
                        poseAppliedCount++;
                        poseAppliedThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                        poseAppliedSnapshot = snapshot;
                    };
                    offSerial.Controller.Play();
                    offGrouped.Controller.Play();
                    offGroup = offRoot.AddComponent<MmdMultiCharacterPlaybackGroup>();
                    offObservation = offRoot.AddComponent<LateUpdateObservation>();
                    offObservation.Track(offGrouped.Controller, livePhysics: false);

                    for (int step = 0; step < 4; step++)
                    {
                        int previousPoseAppliedCount = poseAppliedCount;
                        yield return WaitForPostLateUpdate(
                            offObservation,
                            () => poseAppliedCount > previousPoseAppliedCount);
                        Assert.That(
                            poseAppliedCount,
                            Is.GreaterThan(previousPoseAppliedCount),
                            "The worker group did not publish an applied pose.");
                        int frame = offObservation.AppliedFrame;
                        offSerial.Controller.ApplyFrame(frame);
                        Assert.That(offGroup.IsPlaybackActive, Is.True, offGroup.LastFailureReason);
                        Assert.That(offGroup.HasWorkerPool, Is.True);
                        AssertAfterLateUpdate(offObservation);
                        Assert.That(poseAppliedThreadId, Is.EqualTo(mainThreadId));
                        Assert.That(poseAppliedSnapshot, Is.SameAs(offGrouped.Controller.LastSnapshot));
                        Assert.That(poseAppliedSnapshot!.frame.frame, Is.EqualTo(frame));
                        AssertPoseEqual(offSerial.Instance, offGrouped.Instance);
                    }

                    offGrouped.Controller.Pause();
                    yield return null;
                    int preDiscardFrame = offGrouped.Controller.CurrentFrame;
                    int preDiscardPlaybackFrameBits = BitConverter.SingleToInt32Bits(
                        GetPlaybackFrame(offGrouped.Controller));
                    int preDiscardSnapshotFrame = offGrouped.Controller.LastSnapshot!.frame.frame;
                    offGrouped.Controller.Play();
                    typeof(MmdMultiCharacterPlaybackGroup)
                        .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(offGroup, null);
                    Assert.That(offGrouped.Controller.CurrentFrame, Is.GreaterThan(preDiscardFrame));
                    offGroup.enabled = false;
                    offGrouped.Controller.Pause();

                    Assert.That(offGroup.enabled, Is.False);
                    Assert.That(offGroup.HasWorkerPool, Is.False);
                    Assert.That(offGrouped.Controller.IsMultiCharacterClaimed, Is.False);
                    Assert.That(offGrouped.Controller.CurrentFrame, Is.EqualTo(preDiscardFrame));
                    Assert.That(
                        BitConverter.SingleToInt32Bits(GetPlaybackFrame(offGrouped.Controller)),
                        Is.EqualTo(preDiscardPlaybackFrameBits));
                    Assert.That(
                        offGrouped.Controller.LastSnapshot!.frame.frame,
                        Is.EqualTo(preDiscardSnapshotFrame));
                }
                finally
                {
                    if (offGroup != null)
                    {
                        Object.DestroyImmediate(offGroup);
                    }

                    DestroyFixtures(new List<PlaybackFixture> { offSerial, offGrouped });
                    Object.DestroyImmediate(offRoot);
                }

                MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
                if (!availability.backendAvailable)
                {
                    TestContext.WriteLine(
                        "Live Physics branch skipped because Bullet is unavailable: " +
                        availability.unsupportedReason);
                    yield break;
                }

                var liveSerial = CreateLiveFixture(pmxBytes, vmdBytes, pmxPath, "single-live-serial");
                var liveGrouped = CreateLiveFixture(pmxBytes, vmdBytes, pmxPath, "single-live-grouped");
                var liveRoot = new GameObject("single-live-group");
                MmdMultiCharacterPlaybackGroup? liveGroup = null;
                LateUpdateObservation? liveObservation = null;
                try
                {
                    liveGrouped.Root.transform.SetParent(liveRoot.transform, worldPositionStays: false);
                    yield return null;
                    AttachSources(liveSerial, pmxAsset, vmdAsset, initialFrame: 0);
                    AttachSources(liveGrouped, pmxAsset, vmdAsset, initialFrame: 0);
                    liveSerial.Controller.SetPhysicsMode(MmdPhysicsMode.Live);
                    liveGrouped.Controller.SetPhysicsMode(MmdPhysicsMode.Live);
                    liveGrouped.Controller.Play();
                    liveGroup = liveRoot.AddComponent<MmdMultiCharacterPlaybackGroup>();
                    liveObservation = liveRoot.AddComponent<LateUpdateObservation>();
                    liveObservation.Track(liveGrouped.Controller, livePhysics: true);

                    for (int step = 0; step < 4; step++)
                    {
                        yield return WaitForPostLateUpdate(liveObservation);
                        int frame = liveObservation.AppliedFrame;
                        liveSerial.Controller.ApplyFrame(frame);
                        Assert.That(liveGroup.IsPlaybackActive, Is.True, liveGroup.LastFailureReason);
                        Assert.That(liveGroup.HasWorkerPool, Is.True);
                        AssertAfterLateUpdate(liveObservation);
                        AssertPhysicsBodiesEqual(liveSerial.Instance, liveGrouped.Instance);
                        AssertPoseEqual(liveSerial.Instance, liveGrouped.Instance);
                    }

                    int heldFrame = liveGrouped.Controller.CurrentFrame;
                    liveSerial.Controller.Pause();
                    liveGrouped.Controller.Pause();
                    yield return null;
                    Assert.That(liveGrouped.Controller.CurrentFrame, Is.EqualTo(heldFrame));
                    Assert.That(liveGroup.IsPlaybackActive, Is.True);

                    liveGrouped.Controller.Play();
                    for (int wait = 0; wait < 120 && liveObservation.AppliedFrame <= heldFrame; wait++)
                    {
                        yield return WaitForPostLateUpdate(liveObservation);
                    }
                    int resumedCurrentFrame = liveGrouped.Controller.CurrentFrame;
                    int resumedFrame = liveObservation.AppliedFrame;
                    Assert.That(resumedCurrentFrame, Is.GreaterThanOrEqualTo(heldFrame));
                    Assert.That(resumedFrame, Is.GreaterThan(heldFrame));
                    AssertAfterLateUpdate(liveObservation);

                    liveGroup.enabled = false;
                    Assert.That(liveGroup.HasWorkerPool, Is.False);
                    Assert.That(liveGrouped.Controller.IsMultiCharacterClaimed, Is.False);

                    liveGroup.enabled = true;
                    yield return WaitForPostLateUpdate(liveObservation);
                    Assert.That(liveGroup.IsPlaybackActive, Is.True, liveGroup.LastFailureReason);
                    Assert.That(liveGroup.HasWorkerPool, Is.True);

                    Object.DestroyImmediate(liveGroup);
                    liveGroup = null;
                    Assert.That(liveGrouped.Controller.IsMultiCharacterClaimed, Is.False);

                    liveGroup = liveRoot.AddComponent<MmdMultiCharacterPlaybackGroup>();
                    yield return WaitForPostLateUpdate(liveObservation);
                    Assert.That(liveGroup.IsPlaybackActive, Is.True, liveGroup.LastFailureReason);
                    int timelineFrame = liveGrouped.Controller.CurrentFrame + 2;
                    MmdPlaybackSnapshot timelineSnapshot = liveGrouped.Controller.ApplyTimelineTime(
                        timelineFrame / FrameRate,
                        FrameRate);
                    Assert.That(timelineSnapshot.frame.frame, Is.EqualTo(timelineFrame));
                    Assert.That(liveGroup.enabled, Is.False);
                    Assert.That(liveGroup.HasWorkerPool, Is.False);
                    Assert.That(liveGrouped.Controller.IsMultiCharacterClaimed, Is.False);
                    Assert.That(liveGroup.LastFailureReason, Does.Contain("serial playback"));
                }
                finally
                {
                    if (liveGroup != null)
                    {
                        Object.DestroyImmediate(liveGroup);
                    }

                    DestroyFixtures(new List<PlaybackFixture> { liveSerial, liveGrouped });
                    Object.DestroyImmediate(liveRoot);
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
                            int currentFrame = System.Math.Max(0, grouped[0].Controller.CurrentFrame - 1);
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
                            System.Math.Max(0, grouped[0].Controller.CurrentFrame - 1);
                        Assert.That(evaluatedFrame, Is.GreaterThanOrEqualTo(2));
                        for (int i = 0; i < characterCount; i++)
                        {
                            Assert.That(
                                grouped[i].Controller.CurrentFrame,
                                Is.GreaterThanOrEqualTo(evaluatedFrame));
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

        private static IEnumerator WaitForPostLateUpdate(
            LateUpdateObservation observation,
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

            Assert.Fail("The group did not satisfy its post-LateUpdate observation within 120 frames.");
        }

        private static void AssertAfterLateUpdate(LateUpdateObservation observation)
        {
            Assert.That(observation.HasObservation, Is.True);
            Assert.That(observation.AppliedFrame, Is.EqualTo(observation.CurrentFrameAtLate));
        }

        private static float GetPlaybackFrame(MmdUnityPlaybackController controller)
        {
            FieldInfo field = typeof(MmdUnityPlaybackController).GetField(
                "playbackFrame",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (float)field.GetValue(controller)!;
        }

        [DefaultExecutionOrder(1000)]
        private sealed class LateUpdateObservation : MonoBehaviour
        {
            private MmdUnityPlaybackController? controller;
            private bool livePhysics;

            internal bool HasObservation { get; private set; }

            internal int AppliedFrame { get; private set; } = -1;

            internal int CurrentFrameAtLate { get; private set; } = -1;

            internal void Track(MmdUnityPlaybackController target, bool livePhysics)
            {
                controller = target ?? throw new ArgumentNullException(nameof(target));
                this.livePhysics = livePhysics;
            }

            internal void Clear()
            {
                HasObservation = false;
                AppliedFrame = -1;
                CurrentFrameAtLate = -1;
            }

            private void LateUpdate()
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
                CurrentFrameAtLate = controller.CurrentFrame;
                HasObservation = true;
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
