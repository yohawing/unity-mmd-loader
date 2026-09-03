#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.TestTools;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Timeline;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed class MmdUnityPlaybackControllerPlayModeTests
    {
        private const string SyntheticPlayModeModelName = MmdPlayModeTestFixtures.MinimalTriangleModelName;

        [UnityTest]
        public IEnumerator PlayOnStartBeginsForwardPlaybackInPlayMode()
        {
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    MmdPlayModeTestFixtures.CreateMinimalTriangleModel(SyntheticPlayModeModelName),
                    MmdPlayModeTestFixtures.CreateRootTranslationMotion(SyntheticPlayModeModelName),
                    "playmode-synthetic.pmx",
                    "playmode-synthetic.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                controller.Configure(binding, 30.0f, playOnStart: true);

                yield return null;
                controller.Tick(10.0f / 30.0f);

                Assert.That(controller.PlayOnStart, Is.True);
                Assert.That(controller.IsPlaying, Is.True);
                Assert.That(controller.CurrentFrame, Is.EqualTo(10));
                Assert.That(controller.LastSnapshot, Is.Not.Null);
                Assert.That(controller.LastSnapshot!.frame.frame, Is.EqualTo(10));
                Assert.That(binding.Instance.BoneTransforms[0].localPosition, Is.EqualTo(new Vector3(-2.0f, 0.0f, 0.0f)));
            }
            finally
            {
                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [UnityTest]
        public IEnumerator StopReturnsToBindFrameInPlayMode()
        {
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    MmdPlayModeTestFixtures.CreateMinimalTriangleModel(SyntheticPlayModeModelName),
                    MmdPlayModeTestFixtures.CreateRootTranslationMotion(SyntheticPlayModeModelName),
                    "playmode-synthetic.pmx",
                    "playmode-synthetic.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                controller.Configure(binding, 30.0f, playOnStart: true);

                yield return null;
                controller.Tick(10.0f / 30.0f);
                controller.Stop();

                Assert.That(controller.IsPlaying, Is.False);
                Assert.That(controller.CurrentFrame, Is.EqualTo(0));
                Assert.That(controller.LastSnapshot, Is.Not.Null);
                Assert.That(controller.LastSnapshot!.frame.frame, Is.EqualTo(0));
                Assert.That(binding.Instance.BoneTransforms[0].localPosition, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [UnityTest]
        public IEnumerator PlayableDirectorTwoVmdClipsRebindsMotionAtBoundary()
        {
            const string modelSourceId = "playmode-model.pmx";
            const string firstMotionSourceId = "playmode-first.vmd";
            const string secondMotionSourceId = "playmode-second.vmd";

            MmdPmxAsset? modelAsset = null;
            MmdVmdAsset? firstMotionAsset = null;
            MmdVmdAsset? secondMotionAsset = null;
            MmdUnityModelInstance? instance = null;
            MmdUnityPlaybackBinding? binding = null;
            GameObject? directorObject = null;
            TimelineAsset? timelineAsset = null;
            try
            {
                byte[] pmxBytes = File.ReadAllBytes(ResolvePackageFixture("test_1bone_cube.pmx"));
                byte[] firstVmdBytes = File.ReadAllBytes(ResolvePackageFixture("test_1bone_cube_motion.vmd"));
                byte[] secondVmdBytes = File.ReadAllBytes(ResolvePackageFixture("test_append_bone.vmd"));
                Assert.That(secondVmdBytes, Is.Not.EqualTo(firstVmdBytes));

                modelAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                modelAsset.Initialize(pmxBytes, modelSourceId, modelSourceId);
                firstMotionAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
                firstMotionAsset.Initialize(firstVmdBytes, firstMotionSourceId, firstMotionSourceId);
                secondMotionAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
                secondMotionAsset.Initialize(secondVmdBytes, secondMotionSourceId, secondMotionSourceId);

                var parser = new NativeMmdParser();
                MmdModelDefinition model = parser.LoadModel(pmxBytes);
                MmdMotionDefinition firstMotion = firstMotionAsset.LoadMotion(parser);
                MmdMotionDefinition secondMotion = secondMotionAsset.LoadMotion(parser);
                Assert.That(secondMotion.maxFrame, Is.Not.EqualTo(firstMotion.maxFrame));
                instance = MmdUnityModelFactory.CreateSkinnedModel(model);
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    instance,
                    modelAsset,
                    firstMotionAsset,
                    firstMotion);
                MmdUnityPlaybackController controller = instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                controller.ConfigureModelAsset(modelAsset);
                controller.Configure(binding, 30.0f, playOnStart: false);

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack track = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD");
                TimelineClip firstClip = track.CreateClip<MmdVmdTimelineClip>();
                firstClip.start = 0.0;
                firstClip.duration = 1.0;
                var firstClipAsset = (MmdVmdTimelineClip)firstClip.asset;
                firstClipAsset.MotionAsset = firstMotionAsset;
                firstClipAsset.ModelSourceId = modelSourceId;
                firstClipAsset.MotionSourceId = firstMotionSourceId;
                firstClipAsset.FrameRate = 30.0f;

                TimelineClip secondClip = track.CreateClip<MmdVmdTimelineClip>();
                secondClip.start = 1.0;
                secondClip.duration = 1.0;
                var secondClipAsset = (MmdVmdTimelineClip)secondClip.asset;
                secondClipAsset.MotionAsset = secondMotionAsset;
                secondClipAsset.ModelSourceId = modelSourceId;
                secondClipAsset.MotionSourceId = secondMotionSourceId;
                secondClipAsset.FrameRate = 30.0f;

                directorObject = new GameObject("playmode-two-clip-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timelineAsset;
                director.SetGenericBinding(track, controller);

                director.time = 0.5;
                director.Evaluate();
                Assert.That(controller.MotionSourceId, Is.EqualTo(firstMotionSourceId));
                Assert.That(controller.CurrentFrame, Is.EqualTo(15));
                int firstConfigurationRevision = controller.ConfigurationRevision;

                director.time = 1.5;
                director.Evaluate();
                Assert.That(controller.MotionSourceId, Is.EqualTo(secondMotionSourceId));
                Assert.That(controller.MotionMaxFrame, Is.EqualTo(secondMotion.maxFrame),
                    "the second clip must expose its own motion frame range");
                Assert.That(controller.CurrentFrame, Is.EqualTo(15),
                    "the timeline time must remain stable while the winning motion is rebound");
                Assert.That(controller.ConfigurationRevision, Is.EqualTo(firstConfigurationRevision + 1),
                    "crossing the real PlayableGraph clip boundary must reconfigure the winning motion once");
                Assert.That(controller.LastTimelineSetupTiming, Is.Not.Null);
                Assert.That(controller.LastTimelineSetupTiming!.succeeded, Is.True);
                int secondConfigurationRevision = controller.ConfigurationRevision;

                director.Evaluate();
                Assert.That(controller.ConfigurationRevision, Is.EqualTo(secondConfigurationRevision),
                    "re-evaluating the same timeline time must not reconfigure the winning motion again");

                director.time = 0.5;
                director.Evaluate();
                Assert.That(controller.MotionSourceId, Is.EqualTo(firstMotionSourceId));
                Assert.That(controller.ConfigurationRevision, Is.EqualTo(secondConfigurationRevision + 1),
                    "seeking back across the boundary must select the first clip again once");
                int thirdConfigurationRevision = controller.ConfigurationRevision;

                director.Evaluate();
                Assert.That(controller.ConfigurationRevision, Is.EqualTo(thirdConfigurationRevision),
                    "re-evaluating the same backward-seek time must not reconfigure the winning motion again");
            }
            finally
            {
                if (directorObject != null)
                {
                    directorObject.GetComponent<PlayableDirector>()?.Stop();
                    Object.Destroy(directorObject);
                }

                if (timelineAsset != null)
                {
                    Object.Destroy(timelineAsset);
                }

                binding?.Dispose();
                if (instance != null)
                {
                    MmdPlayModeTestInstanceScope.DestroyInstance(instance);
                }

                if (modelAsset != null)
                {
                    Object.Destroy(modelAsset);
                }

                if (firstMotionAsset != null)
                {
                    Object.Destroy(firstMotionAsset);
                }

                if (secondMotionAsset != null)
                {
                    Object.Destroy(secondMotionAsset);
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TickRejectsInvalidDeltaTimeInPlayMode()
        {
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    MmdPlayModeTestFixtures.CreateMinimalTriangleModel(SyntheticPlayModeModelName),
                    MmdPlayModeTestFixtures.CreateRootTranslationMotion(SyntheticPlayModeModelName),
                    "playmode-synthetic.pmx",
                    "playmode-synthetic.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                controller.Configure(binding, 30.0f, playOnStart: true);

                yield return null;

                Assert.That(
                    () => controller.Tick(-0.1f),
                    Throws.TypeOf<ArgumentOutOfRangeException>().With.Property("ParamName").EqualTo("deltaTime"));
                Assert.That(
                    () => controller.Tick(float.NaN),
                    Throws.TypeOf<ArgumentOutOfRangeException>().With.Property("ParamName").EqualTo("deltaTime"));
                Assert.That(
                    () => controller.Tick(float.PositiveInfinity),
                    Throws.TypeOf<ArgumentOutOfRangeException>().With.Property("ParamName").EqualTo("deltaTime"));
            }
            finally
            {
                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [UnityTest]
        public IEnumerator NativePlaybackControllerRunsWithPackageFixtureWithoutConsumerScene()
        {
            const string modelSourceId = "package-fixture-model.pmx";
            const string motionSourceId = "package-fixture-motion.vmd";
            MmdPmxAsset? modelAsset = null;
            MmdVmdAsset? motionAsset = null;
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");
                string vmdPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                modelAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                modelAsset.Initialize(File.ReadAllBytes(pmxPath), modelSourceId, pmxPath);
                motionAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
                motionAsset.Initialize(File.ReadAllBytes(vmdPath), motionSourceId, vmdPath);

                binding = MmdUnityPlaybackBinding.CreateSkinned(modelAsset, motionAsset);
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                controller.ConfigureModelAsset(modelAsset);
                controller.Configure(binding, 30.0f, playOnStart: true);

                yield return null;

                // Set the motion source after Start has run. Otherwise the controller would
                // treat both serialized sources as a request to rebind an existing scene model,
                // which is intentionally not part of this package-only fixture.
                controller.ConfigureMotionAsset(motionAsset);

                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.IsConfigured, Is.True);
                Assert.That(controller.IsPlaying, Is.True);
                Assert.That(controller.PlayOnStart, Is.True);
                Assert.That(controller.ModelAssetSource, Is.SameAs(modelAsset));
                Assert.That(controller.MotionAssetSource, Is.SameAs(motionAsset));

                MmdUnityModelInstance instance = binding.Instance;
                SkinnedMeshRenderer? sceneSmr = instance.SkinnedMeshRenderer;
                Assert.That(sceneSmr, Is.Not.Null, "the package fixture must create a skinned playback hierarchy");
                Assert.That(sceneSmr!.sharedMesh, Is.SameAs(instance.Mesh));
                Assert.That(sceneSmr.bones, Is.Not.Null.And.Not.Empty);
                Assert.That(instance.BoneTransforms, Is.Not.Null.And.Not.Empty);
                Assert.That(sceneSmr.bones.Length, Is.EqualTo(instance.BoneTransforms.Length));
                Assert.That(sceneSmr.rootBone, Is.Not.Null);

                controller.Pause();
                MmdPlaybackSnapshot frameZero = controller.ApplyFrame(0);
                MmdPlaybackSnapshot frameTen = controller.ApplyFrame(10);

                Assert.That(frameZero.frame.frame, Is.EqualTo(0));
                Assert.That(frameTen.frame.frame, Is.EqualTo(10));
                Assert.That(controller.CurrentFrame, Is.EqualTo(10));
                Assert.That(controller.LastSnapshot, Is.SameAs(frameTen));
            }
            finally
            {
                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
                if (modelAsset != null)
                {
                    Object.Destroy(modelAsset);
                }

                if (motionAsset != null)
                {
                    Object.Destroy(motionAsset);
                }
            }
        }

        private static void AddPinnedRootRigidbody(MmdModelDefinition model)
        {
            model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
            {
                index = 0,
                name = "root pinned body",
                boneIndex = 0,
                boneName = "root",
                shapeType = "sphere",
                size = new[] { 0.25f, 0.25f, 0.25f },
                position = new[] { 0.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f },
                mass = 0.0f,
                linearDamping = 0.5f,
                angularDamping = 0.5f,
                friction = 0.5f,
                restitution = 0.0f,
                group = 0,
                mask = 0,
                physicsKind = "static"
            });
        }

        private static void AssertLivePhysicsShapesMatchDescriptors(MmdLivePhysicsFrameDiagnostics diagnostics)
        {
            Assert.That(diagnostics.bodyDiagnostics, Is.Not.Null.And.Not.Empty);
            foreach (MmdLivePhysicsBodyDiagnostics body in diagnostics.bodyDiagnostics)
            {
                string context = $"body={body.bodyIndex}:{body.bodyName}";
                Assert.That(body.nativeShapeType, Is.EqualTo(body.shapeType), context + " native shape must match PMX descriptor shape");
                Assert.That(body.debugColliderType, Is.EqualTo(body.shapeType), context + " debug collider must match PMX descriptor shape");
                Assert.That(body.descriptorSize, Is.Not.EqualTo(Vector3.zero), context + " descriptor size must be present");
                if (string.Equals(body.shapeType, "capsule", StringComparison.Ordinal))
                {
                    Assert.That(body.debugColliderSize.x, Is.GreaterThan(0.0f), context + " capsule radius must be positive");
                    Assert.That(body.debugColliderSize.y, Is.GreaterThan(body.debugColliderSize.x * 2.0f), context + " capsule height must include a non-zero cylinder section");
                    Assert.That(body.debugColliderSize.z, Is.EqualTo(1.0f), context + " capsule direction must be Y-axis");
                }
            }
        }

        [UnityTest]
        public IEnumerator HairPhysicsFixtureWithPureWorldAnchorJointsIsRejectedByLivePhysics()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
                yield break;
            }

            MmdUnityPlaybackBinding? binding = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_hair_physics.pmx");
                byte[] pmxBytes = File.ReadAllBytes(pmxPath);
                var parser = new NativeMmdParser();
                MmdModelDefinition model = parser.LoadModel(pmxBytes);
                Assert.That(
                    model.physics.joints.Any(j => j.rigidbodyAIndex < 0 && j.rigidbodyBIndex < 0),
                    Is.True,
                    "test_hair_physics.pmx must keep at least one pure world-anchor joint for this regression test");

                MmdMotionDefinition motion = CreateRestPoseMotion(model);
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model, motion, "test_hair_physics.pmx", "rest-pose", pmxPath);

                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                yield return null;

                Assert.That(
                    () => controller.ApplyFrame(0),
                    Throws.TypeOf<InvalidOperationException>()
                        .With.Message.Contains("joint has both rigidbody endpoints set to -1"));
            }
            finally
            {
                if (binding?.Instance?.Root != null)
                {
                    MmdPlayModeTestInstanceScope.DestroyInstance(binding.Instance);
                }
                else
                {
                    binding?.Dispose();
                }
            }
        }

        [UnityTest]
        public IEnumerator HairPhysicsFixtureLiveForwardPlaybackProducesDiagnostics()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
                yield break;
            }

            MmdUnityPlaybackBinding? binding = null;
            MmdModelDefinition? model = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_hair_physics.pmx");
                byte[] pmxBytes = File.ReadAllBytes(pmxPath);
                var parser = new NativeMmdParser();
                model = parser.LoadModel(pmxBytes);
                Assert.That(model.physics.rigidbodies.Count, Is.GreaterThan(0),
                    "test_hair_physics.pmx must contain rigidbody definitions");

                // test_hair_physics.pmx contains joints where both rigidbody endpoints
                // are -1 (pure world-anchor). These are rejected by the descriptor
                // validator, so remove them from the live-physics model before creating
                // the binding. One-sided -1 joints (valid body + -1) remain and will be
                // reported through backend diagnostics.
                model.physics.joints.RemoveAll(j => j.rigidbodyAIndex < 0 && j.rigidbodyBIndex < 0);

                MmdMotionDefinition motion = CreateRestPoseMotion(model);

                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model, motion, "test_hair_physics.pmx", "rest-pose", pmxPath);

                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.LivePhysicsBodyDiagnosticsSampleInterval = 1;
                controller.Configure(binding, 30.0f, playOnStart: false);
                yield return null;

                // ---- Frame 0: initialise live physics (deltaTime = 0, no stepping) ----
                MmdPlaybackSnapshot frameZero = controller.ApplyFrame(0);
                Assert.That(controller.CurrentFrame, Is.EqualTo(0));
                Assert.That(binding.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(binding.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(0));
                Assert.That(binding.LastLivePhysicsDiagnostics.backendName, Is.EqualTo("mmd-anim-bullet-native"),
                    "The migration fixture must exercise the mmd-anim Bullet host backend, not the legacy fallback.");
                Assert.That(binding.LastLivePhysicsDiagnostics.stepPhysicsMs, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(binding.LastLivePhysicsDiagnostics.pinnedBodies.pinnedBodyCount, Is.GreaterThan(0));
                Assert.That(binding.LastLivePhysicsDiagnostics.pinnedBodies.dynamicOrientationPinnedBodyCount, Is.GreaterThan(0),
                    "Expected PMX mode 2 dynamic-with-bone rigidbodies to be initialized from the animated bone pose during reset seed");
                int expectedInitialDynamicBodies = model.physics.rigidbodies.Count(
                    body => string.Equals(body.physicsKind, "dynamic", StringComparison.Ordinal) &&
                            body.boneIndex >= 0 &&
                            body.boneIndex < binding.Instance.BoneTransforms.Length);
                Assert.That(binding.LastLivePhysicsDiagnostics.pinnedBodies.dynamicInitialPinnedBodyCount, Is.EqualTo(expectedInitialDynamicBodies),
                    "Expected PMX mode 1 dynamic rigidbodies to be initialized from the frame-0 animated bone pose before the first Bullet step");
                AssertLivePhysicsShapesMatchDescriptors(binding.LastLivePhysicsDiagnostics);

                // test_hair_physics.pmx contains one-sided world-anchored joints (one
                // rigidbody index = -1) that are unsupported by the Bullet backend. The
                // diagnostics must report the exact count of skipped joints. Pure
                // world-anchor joints were removed earlier to satisfy the descriptor validator.
                int expectedSkippedWorldAnchorJoints = model.physics.joints.Count(
                    j => (j.rigidbodyAIndex < 0) != (j.rigidbodyBIndex < 0));
                Assert.That(
                    binding.LastLivePhysicsDiagnostics.unsupportedWorldAnchorJointCount,
                    Is.EqualTo(expectedSkippedWorldAnchorJoints),
                    $"Expected {expectedSkippedWorldAnchorJoints} skipped world-anchored joints in test_hair_physics.pmx");

                // Capture frame-0 positions for bones attached to non-static rigidbodies.
                var hairBoneSlots = new HashSet<int>();
                for (int i = 0; i < model.physics.rigidbodies.Count; i++)
                {
                    MmdRigidbodyDefinition body = model.physics.rigidbodies[i];
                    if (!string.Equals(body.physicsKind, "static", StringComparison.Ordinal))
                    {
                        hairBoneSlots.Add(body.boneIndex);
                    }
                }

                Assert.That(hairBoneSlots.Count, Is.GreaterThan(0),
                    "Expected at least one non-static rigidbody in test_hair_physics.pmx");

                var frameZeroPositions = new Dictionary<int, Vector3>();
                var frameZeroRotations = new Dictionary<int, Quaternion>();
                foreach (int slot in hairBoneSlots)
                {
                    if (slot >= 0 && slot < binding.Instance.BoneTransforms.Length)
                    {
                        frameZeroPositions[slot] = binding.Instance.BoneTransforms[slot].localPosition;
                        frameZeroRotations[slot] = binding.Instance.BoneTransforms[slot].localRotation;
                    }
                }

                var frameZeroNativePositions = new Dictionary<int, Vector3>();
                var frameZeroNativeRotations = new Dictionary<int, Quaternion>();
                foreach (MmdUnityPhysicsBody body in binding.Instance.PhysicsBodies)
                {
                    if (body != null &&
                        body.HasNativeTransform &&
                        !string.Equals(body.PhysicsKind, "static", StringComparison.Ordinal))
                    {
                        frameZeroNativePositions[body.BodyIndex] = body.NativePosition;
                        frameZeroNativeRotations[body.BodyIndex] = body.NativeRotation;
                    }
                }

                // ---- Frame 1: forward tick, physics steps with dt = 1/30 ----
                MmdPlaybackSnapshot frameOne = controller.ApplyFrame(1);
                Assert.That(controller.CurrentFrame, Is.EqualTo(1));
                Assert.That(binding.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(binding.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(1));
                Assert.That(binding.LastLivePhysicsDiagnostics.stepPhysicsMs, Is.GreaterThan(0.0));
                Assert.That(binding.LastLivePhysicsDiagnostics.pinnedBodies.pinnedBodyCount, Is.GreaterThan(0));
                Assert.That(binding.LastLivePhysicsDiagnostics.pinnedBodies.dynamicOrientationPinnedBodyCount, Is.EqualTo(0),
                    "PMX mode 2 dynamic-with-bone rigidbodies must remain active dynamic bodies on normal forward frames");
                Assert.That(binding.LastLivePhysicsDiagnostics.pinnedBodies.dynamicInitialPinnedBodyCount, Is.EqualTo(0),
                    "Dynamic rigidbodies must only be force-initialized on the first live physics frame");

                // ---- Frame 5: tick further, giving physics several steps to accumulate ----
                MmdPlaybackSnapshot frameFive = controller.ApplyFrame(5);
                Assert.That(controller.CurrentFrame, Is.EqualTo(5));
                Assert.That(binding.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(binding.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(5));
                Assert.That(binding.LastLivePhysicsDiagnostics.stepPhysicsMs, Is.GreaterThan(0.0));
                Assert.That(binding.LastLivePhysicsDiagnostics.pinnedBodies.pinnedBodyCount, Is.GreaterThan(0));
                Assert.That(binding.LastLivePhysicsDiagnostics.pinnedBodies.dynamicOrientationPinnedBodyCount, Is.EqualTo(0),
                    "PMX mode 2 dynamic-with-bone rigidbodies must not be re-pinned during accumulated forward simulation");

                // Bullet readback alone is not enough for the golden-path visual result:
                // at least one attached bone must also receive the live physics feedback.
                bool anyBoneTransformChanged = false;
                foreach (int slot in hairBoneSlots)
                {
                    if (slot >= 0 && slot < binding.Instance.BoneTransforms.Length)
                    {
                        Vector3 posZero = frameZeroPositions[slot];
                        Vector3 posFive = binding.Instance.BoneTransforms[slot].localPosition;
                        Quaternion rotZero = frameZeroRotations[slot];
                        Quaternion rotFive = binding.Instance.BoneTransforms[slot].localRotation;
                        if ((posFive - posZero).sqrMagnitude > 0.0001f ||
                            Quaternion.Angle(rotZero, rotFive) > 0.01f)
                        {
                            anyBoneTransformChanged = true;
                            break;
                        }
                    }
                }

                bool anyNativePhysicsBodyChanged = false;
                foreach (MmdUnityPhysicsBody body in binding.Instance.PhysicsBodies)
                {
                    if (body == null ||
                        !body.HasNativeTransform ||
                        string.Equals(body.PhysicsKind, "static", StringComparison.Ordinal) ||
                        !frameZeroNativePositions.TryGetValue(body.BodyIndex, out Vector3 nativePositionZero) ||
                        !frameZeroNativeRotations.TryGetValue(body.BodyIndex, out Quaternion nativeRotationZero))
                    {
                        continue;
                    }

                    if ((body.NativePosition - nativePositionZero).sqrMagnitude > 0.0001f ||
                        Quaternion.Angle(nativeRotationZero, body.NativeRotation) > 0.01f)
                    {
                        anyNativePhysicsBodyChanged = true;
                        break;
                    }
                }

                Assert.That(anyNativePhysicsBodyChanged, Is.True,
                    "Expected at least one debug physics body/native readback transform to change between frame 0 and frame 5");
                Assert.That(anyBoneTransformChanged, Is.True,
                    "Expected at least one hair/physics bone transform to change between frame 0 and frame 5; native-only motion means Bullet readback is not being applied to the rig");
            }
            finally
            {
                if (binding?.Instance?.Root != null)
                {
                    // DestroyInstance destroys Root (and thus the controller via OnDestroy -> binding.Dispose).
                    MmdPlayModeTestInstanceScope.DestroyInstance(binding.Instance);
                }
                else
                {
                    binding?.Dispose();
                }
            }
        }

        [UnityTest]
        public IEnumerator HairPhysicsFixtureImportScaleZeroDotOneKeepsNativeReadbackInMmdSpaceAndScalesUnityObjects()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
                yield break;
            }

            HairPhysicsScaleSample scaleOne = RunHairPhysicsForwardPlayback(importScale: 1.0f);
            HairPhysicsScaleSample scalePointOne = RunHairPhysicsForwardPlayback(importScale: 0.1f);

            Assert.That(scalePointOne.importScale, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(scalePointOne.nativePosition.x, Is.EqualTo(scaleOne.nativePosition.x).Within(0.001f));
            Assert.That(scalePointOne.nativePosition.y, Is.EqualTo(scaleOne.nativePosition.y).Within(0.001f));
            Assert.That(scalePointOne.nativePosition.z, Is.EqualTo(scaleOne.nativePosition.z).Within(0.001f));
            Assert.That(scalePointOne.boneWorldPosition.x, Is.EqualTo(scaleOne.boneWorldPosition.x * 0.1f).Within(0.001f));
            Assert.That(scalePointOne.boneWorldPosition.y, Is.EqualTo(scaleOne.boneWorldPosition.y * 0.1f).Within(0.001f));
            Assert.That(scalePointOne.boneWorldPosition.z, Is.EqualTo(scaleOne.boneWorldPosition.z * 0.1f).Within(0.001f));
            Assert.That(scalePointOne.debugWorldPosition.x, Is.EqualTo(scaleOne.debugWorldPosition.x * 0.1f).Within(0.001f));
            Assert.That(scalePointOne.debugWorldPosition.y, Is.EqualTo(scaleOne.debugWorldPosition.y * 0.1f).Within(0.001f));
            Assert.That(scalePointOne.debugWorldPosition.z, Is.EqualTo(scaleOne.debugWorldPosition.z * 0.1f).Within(0.001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator HumanoidRetargetLateUpdateStepsLivePhysicsFromCurrentPose()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
                yield break;
            }

            MmdUnityModelInstance? instance = null;
            MmdPmxAsset? pmxAsset = null;
            MmdVmdAsset? vmdAsset = null;
            MmdHumanoidProxyRigResult? proxyRig = null;
            Avatar? avatar = null;
            PlayableGraph graph = default;
            try
            {
                string pmxPath = ResolvePackageFixture("test_hair_physics.pmx");
                byte[] pmxBytes = File.ReadAllBytes(pmxPath);
                MmdModelDefinition model = LoadHairPhysicsModelForLive(pmxPath);
                instance = MmdUnityModelFactory.CreateSkinnedModel(
                    model,
                    pmxPath,
                    MmdPmxAsset.DefaultImportScale);
                pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                pmxAsset.Initialize(
                    pmxBytes,
                    "test_hair_physics.pmx",
                    pmxPath,
                    MmdPmxAsset.DefaultImportScale,
                    parseSummary: MmdPmxParseSummary.FromModel(model));
                string vmdPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
                vmdAsset.Initialize(File.ReadAllBytes(vmdPath), "test_1bone_cube_motion.vmd", vmdPath);
                SkinnedMeshRenderer renderer = instance.SkinnedMeshRenderer!;
                Mesh authoredMesh = renderer.sharedMesh;
                Material authoredMaterial = renderer.sharedMaterials[0];

                MmdUnityPlaybackController controller = instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.IkMaxIterationsCap = 64;
                controller.LivePhysicsBodyDiagnosticsSampleInterval = 1;
                controller.ConfigureModelAsset(pmxAsset);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);

                int drivenBoneIndex = FindFirstValidStaticPhysicsBone(model, instance);
                Assert.That(drivenBoneIndex, Is.GreaterThanOrEqualTo(0),
                    "test_hair_physics.pmx must expose a valid static/bone-driven body for humanoid physics seeding");

                proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(CreateHumanoidMappingModelWithOriginsForLivePhysics());
                Assert.That(proxyRig.ProxyRoot, Is.Not.Null);
                proxyRig.ProxyRoot!.transform.SetParent(instance.Root.transform, worldPositionStays: false);
                proxyRig.ProxyRoot.SetActive(true);
                MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyRig);
                Assert.That(avatarResult.IsValidHumanAvatar, Is.True, string.Join("\n", avatarResult.Diagnostics));
                avatar = avatarResult.Avatar;

                Animator animator = instance.Root.AddComponent<Animator>();
                animator.avatar = avatar;
                graph = CreateBoundAnimatorGraph(animator);

                Transform proxyHips = proxyRig.BoneMap[HumanBodyBones.Hips];
                Transform drivenBone = instance.BoneTransforms[drivenBoneIndex];
                Vector3 proxyBindPosition = proxyHips.localPosition;
                Vector3 drivenBindPosition = drivenBone.localPosition;
                HashSet<int> hairBoneSlots = CollectNonStaticPhysicsBoneSlots(model, instance);
                Assert.That(hairBoneSlots.Count, Is.GreaterThan(0),
                    "Expected at least one non-static rigidbody linked to a hair bone");
                controller.ConfigureHumanoidRetarget(
                    proxyRig.ProxyRoot.transform,
                    new[]
                    {
                        new MmdHumanoidRetargetBinding(
                            HumanBodyBones.Hips,
                            drivenBoneIndex,
                            proxyHips,
                            drivenBone,
                            proxyHips.localRotation,
                            drivenBone.localRotation,
                            copyLocalPosition: true,
                            translationTargetTransform: drivenBone,
                            translationTargetMmdBoneIndex: drivenBoneIndex,
                            proxyBindLocalPosition: proxyBindPosition,
                            translationTargetBindLocalPosition: drivenBindPosition)
                    },
                    Array.Empty<MmdHumanoidAppendTransformBinding>());

                // Prove the retarget contract with a deterministic, physics-free write before
                // the Live loop: the native final-matrix diagnostics below are meaningful only
                // when the preceding host pose actually reached the Unity bone transform.
                int configuredIkMaxIterationsCap = controller.IkMaxIterationsCap;
                controller.IkMaxIterationsCap = 0;
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                Vector3 directRetargetDelta = new Vector3(0.03f, 0.0f, 0.0f);
                proxyHips.localPosition = proxyBindPosition + directRetargetDelta;
                MmdHumanoidRetargeterResult directRetargetResult = controller.ApplyHumanoidRetargetNow();
                Assert.That(directRetargetResult.CopiedBoneCount, Is.EqualTo(1));
                Assert.That(directRetargetResult.CopiedTranslationCount, Is.EqualTo(1));
                Assert.That(
                    Vector3.Distance(drivenBone.localPosition, drivenBindPosition + directRetargetDelta),
                    Is.LessThanOrEqualTo(1.0e-5f),
                    "retarget must write the requested host translation to the Unity bone before Live physics");
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                controller.IkMaxIterationsCap = configuredIkMaxIterationsCap;

                proxyHips.localPosition = proxyBindPosition + new Vector3(0.05f, 0.0f, 0.0f);
                proxyHips.localRotation = Quaternion.Euler(0.0f, 4.0f, 0.0f);
                Vector3 liveStartWorldPosition = drivenBone.position;
                yield return null;

                Assert.That(controller.LastHumanoidRetargetGate, Is.EqualTo(MmdHumanoidRetargetGate.Ready));
                Assert.That(controller.IsConfigured, Is.False,
                    "Self-tick humanoid Live physics must not configure the VMD playback binding.");
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(0));
                MmdLivePhysicsBodyDiagnostics[] initialHairDiagnostics = controller.LastLivePhysicsDiagnostics.bodyDiagnostics
                    .Where(body => hairBoneSlots.Contains(body.boneIndex))
                    .ToArray();
                Assert.That(initialHairDiagnostics, Is.Not.Empty);
                Assert.That(controller.IkMaxIterationsCap, Is.EqualTo(64));
                Assert.That(controller.LastLivePhysicsDiagnostics.deltaTime, Is.EqualTo(0.0f));
                Assert.That(controller.LastLivePhysicsDiagnostics.evaluationPath, Is.EqualTo("HumanoidNativeFinal"));
                Assert.That(controller.LastLivePhysicsDiagnostics.phaseDiagnosticsPresent, Is.True);
                Assert.That(controller.LastLivePhysicsDiagnostics.nativeStepReportPresent, Is.True);
                Assert.That(controller.LastLivePhysicsDiagnostics.nativeHostFramePresent, Is.True);
                Assert.That(controller.LastLivePhysicsDiagnostics.playbackEvaluateBeforePhysicsPresent, Is.False,
                    "Humanoid Live uses the atomic host-frame ABI, not the VMD playback calls.");
                Assert.That(controller.LastLivePhysicsDiagnostics.playbackCopyEvaluatedOutputsPresent, Is.False);
                Assert.That(controller.LastLivePhysicsDiagnostics.physicsWorldStepRuntimePresent, Is.False);
                Assert.That(controller.LastLivePhysicsDiagnostics.playbackEvaluateBeforePhysicsMs, Is.Zero);
                Assert.That(controller.LastLivePhysicsDiagnostics.playbackCopyEvaluatedOutputsMs, Is.Zero);
                Assert.That(controller.LastLivePhysicsDiagnostics.physicsWorldStepRuntimeMs, Is.Zero);
                Assert.That(controller.LastLivePhysicsDiagnostics.hostPoseCapturePresent, Is.True,
                    "Humanoid Live must retain Animator/retarget output as its host-pose authority.");
                Assert.That(controller.LastLivePhysicsDiagnostics.managedBodyTransformApplyPresent, Is.False,
                    "Humanoid Live applies the native final matrix pose instead of the VMD compatibility body path.");
                Assert.That(controller.LastLivePhysicsDiagnostics.nativeSubstepCount, Is.GreaterThanOrEqualTo(0));
                Assert.That(controller.LastLivePhysicsDiagnostics.nativeRigidbodyCount, Is.EqualTo(model.physics.rigidbodies.Count));
                Assert.That(controller.LastLivePhysicsDiagnostics.nativeBoneCount, Is.EqualTo(model.bones.Count));
                Assert.That(controller.LastLivePhysicsDiagnostics.ensureBackendPresent, Is.True);
                Assert.That(controller.LastLivePhysicsDiagnostics.evaluateFramePresent, Is.False);
                Assert.That(controller.LastLivePhysicsDiagnostics.applyAnimationFramePresent, Is.False);
                Assert.That(controller.LastLivePhysicsDiagnostics.snapshotBuildPresent, Is.False);
                Assert.That(controller.LastSnapshot, Is.Null,
                    "Humanoid-driven live physics must not overwrite the VMD playback snapshot surface.");

                // This redistributable fixture intentionally exercises static + dynamicBone bodies;
                // its rest pose is already at equilibrium, so this case verifies host-pose routing
                // and complete native readback rather than inventing a motion oracle for the hair.
                for (int i = 1; i <= 5; i++)
                {
                    proxyHips.localPosition = proxyBindPosition + new Vector3(0.05f + 0.01f * i, 0.0f, 0.0f);
                    proxyHips.localRotation = Quaternion.Euler(0.0f, 4.0f + i, 0.0f);
                    yield return null;
                }
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(5));
                Assert.That(controller.LastLivePhysicsDiagnostics.deltaTime, Is.GreaterThan(0.0f));
                Assert.That(controller.LastLivePhysicsDiagnostics.stepPhysicsMs, Is.GreaterThan(0.0));
                Assert.That(controller.LastLivePhysicsDiagnostics.sampledBodyDiagnosticsThisFrame, Is.True);
                Assert.That(controller.LastLivePhysicsDiagnostics.bodyDiagnosticsFrame, Is.EqualTo(5));
                Assert.That(
                    controller.LastLivePhysicsDiagnostics.bodyDiagnostics,
                    Has.Length.EqualTo(model.physics.rigidbodies.Count),
                    "the real hair fixture must expose a complete native readback sample");
                Assert.That(controller.LastLivePhysicsDiagnostics.managedBodyTransformApplyPresent, Is.False,
                    "Humanoid Live applies the native final matrix pose once");
                Assert.That(controller.LastLivePhysicsDiagnostics.matrixTransformApplyPresent, Is.True);
                Assert.That(controller.LastLivePhysicsDiagnostics.nativeBonesWrittenBack, Is.GreaterThan(0));
                Assert.That(
                    Vector3.Distance(drivenBone.position, liveStartWorldPosition),
                    Is.GreaterThan(0.001f),
                    "Humanoid Live must apply a changed native final pose to the configured Unity bone");
                Assert.That(
                    HasNativeHairBodyReadbackChanged(
                        initialHairDiagnostics,
                        controller.LastLivePhysicsDiagnostics.bodyDiagnostics),
                    Is.True,
                    "Humanoid Live must advance at least one non-static hair body's native readback");

                Mesh humanoidPlaybackMesh = renderer.sharedMesh;
                FieldInfo? physicsModeField = typeof(MmdUnityPlaybackController).GetField(
                    "physicsMode",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(physicsModeField, Is.Not.Null);
                physicsModeField!.SetValue(controller, MmdPhysicsMode.Off);
                controller.ConfigureFromAssets(pmxAsset, vmdAsset, 30.0f, startFrame: 0, playOnStart: false);
                yield return null;
                Assert.That(controller.IsConfigured, Is.True);
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Null,
                    "VMD rebind with physics Off must release the previous Humanoid physics binding.");
                Assert.That(humanoidPlaybackMesh, Is.SameAs(authoredMesh),
                    "model-only Humanoid physics reuses the authored blend-shape Mesh");
                Mesh vmdPlaybackMesh = renderer.sharedMesh;
                Assert.That(vmdPlaybackMesh, Is.SameAs(authoredMesh));

                controller.ReleasePlaybackResources();
                yield return null;
                Assert.That(vmdPlaybackMesh, Is.SameAs(authoredMesh));
                Assert.That(renderer.sharedMesh, Is.SameAs(authoredMesh));
                Assert.That(renderer.sharedMaterials[0], Is.SameAs(authoredMaterial));
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                if (avatar != null)
                {
                    UnityEngine.Object.Destroy(avatar);
                }

                if (proxyRig?.ProxyRoot != null)
                {
                    UnityEngine.Object.Destroy(proxyRig.ProxyRoot);
                }

                if (pmxAsset != null)
                {
                    UnityEngine.Object.Destroy(pmxAsset);
                }

                if (vmdAsset != null)
                {
                    UnityEngine.Object.Destroy(vmdAsset);
                }

                MmdPlayModeTestInstanceScope.DestroyInstance(instance);
            }
        }

        [UnityTest]
        public IEnumerator HumanoidRetargetLateUpdateCreatesModelOnlyPhysicsBindingFromModelAsset()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
                yield break;
            }

            MmdUnityModelInstance? instance = null;
            MmdPmxAsset? pmxAsset = null;
            MmdHumanoidProxyRigResult? proxyRig = null;
            Avatar? avatar = null;
            PlayableGraph graph = default;
            try
            {
                string pmxPath = ResolvePackageFixture("test_hair_physics.pmx");
                byte[] pmxBytes = File.ReadAllBytes(pmxPath);
                MmdModelDefinition model = LoadHairPhysicsModelForLive(pmxPath);
                instance = MmdUnityModelFactory.CreateSkinnedModel(
                    model,
                    pmxPath,
                    MmdPmxAsset.DefaultImportScale);
                pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                pmxAsset.Initialize(
                    pmxBytes,
                    "test_hair_physics.pmx",
                    pmxPath,
                    MmdPmxAsset.DefaultImportScale,
                    parseSummary: MmdPmxParseSummary.FromModel(model));

                MmdUnityPlaybackController controller = instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.ConfigureModelAsset(pmxAsset);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                controller.LivePhysicsBodyDiagnosticsSampleInterval = 1;

                int drivenBoneIndex = FindFirstValidStaticPhysicsBone(model, instance);
                Assert.That(drivenBoneIndex, Is.GreaterThanOrEqualTo(0),
                    "test_hair_physics.pmx must expose a valid static/bone-driven body for humanoid physics seeding");

                proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(CreateHumanoidMappingModelWithOriginsForLivePhysics());
                Assert.That(proxyRig.ProxyRoot, Is.Not.Null);
                proxyRig.ProxyRoot!.transform.SetParent(instance.Root.transform, worldPositionStays: false);
                proxyRig.ProxyRoot.SetActive(true);
                MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyRig);
                Assert.That(avatarResult.IsValidHumanAvatar, Is.True, string.Join("\n", avatarResult.Diagnostics));
                avatar = avatarResult.Avatar;

                Animator animator = instance.Root.AddComponent<Animator>();
                animator.avatar = avatar;
                graph = CreateBoundAnimatorGraph(animator);

                Transform proxyHips = proxyRig.BoneMap[HumanBodyBones.Hips];
                Transform drivenBone = instance.BoneTransforms[drivenBoneIndex];
                Vector3 proxyBindPosition = proxyHips.localPosition;
                Vector3 drivenBindPosition = drivenBone.localPosition;
                HashSet<int> hairBoneSlots = CollectNonStaticPhysicsBoneSlots(model, instance);
                Assert.That(hairBoneSlots.Count, Is.GreaterThan(0),
                    "Expected at least one non-static rigidbody linked to a hair bone");
                controller.ConfigureHumanoidRetarget(
                    proxyRig.ProxyRoot.transform,
                    new[]
                    {
                        new MmdHumanoidRetargetBinding(
                            HumanBodyBones.Hips,
                            drivenBoneIndex,
                            proxyHips,
                            drivenBone,
                            proxyHips.localRotation,
                            drivenBone.localRotation,
                            copyLocalPosition: true,
                            translationTargetTransform: drivenBone,
                            translationTargetMmdBoneIndex: drivenBoneIndex,
                            proxyBindLocalPosition: proxyBindPosition,
                            translationTargetBindLocalPosition: drivenBindPosition)
                    },
                    Array.Empty<MmdHumanoidAppendTransformBinding>());

                Assert.That(controller.IsConfigured, Is.False,
                    "The regression must start with no manually injected playback binding.");

                // Keep the host-pose precondition observable independently from the rest-pose
                // physics fixture: Live diagnostics cannot distinguish a stale Unity Transform
                // from a stable dynamicBone equilibrium on their own.
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                Vector3 directRetargetDelta = new Vector3(0.03f, 0.0f, 0.0f);
                proxyHips.localPosition = proxyBindPosition + directRetargetDelta;
                MmdHumanoidRetargeterResult directRetargetResult = controller.ApplyHumanoidRetargetNow();
                Assert.That(directRetargetResult.CopiedBoneCount, Is.EqualTo(1));
                Assert.That(directRetargetResult.CopiedTranslationCount, Is.EqualTo(1));
                Assert.That(
                    Vector3.Distance(drivenBone.localPosition, drivenBindPosition + directRetargetDelta),
                    Is.LessThanOrEqualTo(1.0e-5f),
                    "retarget must write the requested host translation to the Unity bone before Live physics");
                controller.SetPhysicsMode(MmdPhysicsMode.Live);

                proxyHips.localPosition = proxyBindPosition + new Vector3(0.05f, 0.0f, 0.0f);
                proxyHips.localRotation = Quaternion.Euler(0.0f, 4.0f, 0.0f);
                Vector3 liveStartWorldPosition = drivenBone.position;
                yield return null;

                Assert.That(controller.LastHumanoidRetargetGate, Is.EqualTo(MmdHumanoidRetargetGate.Ready));
                Assert.That(controller.IsConfigured, Is.False,
                    "Model-only humanoid physics binding must stay separate from the VMD playback binding.");
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(0));
                Assert.That(controller.LastSnapshot, Is.Null,
                    "Model-only humanoid physics binding must not create a VMD playback snapshot.");
                MmdLivePhysicsBodyDiagnostics[] initialHairDiagnostics = controller.LastLivePhysicsDiagnostics.bodyDiagnostics
                    .Where(body => hairBoneSlots.Contains(body.boneIndex))
                    .ToArray();
                Assert.That(initialHairDiagnostics, Is.Not.Empty);

                // The real fixture is the graph/binding probe here; deterministic mode-1 movement
                // is covered by the synthetic two-bone Timeline regression in EditMode.

                for (int i = 1; i <= 5; i++)
                {
                    proxyHips.localPosition = proxyBindPosition + new Vector3(0.05f + 0.01f * i, 0.0f, 0.0f);
                    proxyHips.localRotation = Quaternion.Euler(0.0f, 4.0f + i, 0.0f);
                    yield return null;
                }
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(5));
                Assert.That(controller.LastLivePhysicsDiagnostics.deltaTime, Is.GreaterThan(0.0f));
                Assert.That(controller.LastLivePhysicsDiagnostics.stepPhysicsMs, Is.GreaterThan(0.0));
                Assert.That(controller.LastLivePhysicsDiagnostics.sampledBodyDiagnosticsThisFrame, Is.True);
                Assert.That(controller.LastLivePhysicsDiagnostics.bodyDiagnosticsFrame, Is.EqualTo(5));
                Assert.That(
                    controller.LastLivePhysicsDiagnostics.bodyDiagnostics,
                    Has.Length.EqualTo(model.physics.rigidbodies.Count),
                    "the real hair fixture must expose a complete native readback sample");
                Assert.That(controller.LastLivePhysicsDiagnostics.managedBodyTransformApplyPresent, Is.False,
                    "Humanoid Live applies the native final matrix pose once");
                Assert.That(controller.LastLivePhysicsDiagnostics.matrixTransformApplyPresent, Is.True);
                Assert.That(controller.LastLivePhysicsDiagnostics.nativeBonesWrittenBack, Is.GreaterThan(0));
                Assert.That(
                    Vector3.Distance(drivenBone.position, liveStartWorldPosition),
                    Is.GreaterThan(0.001f),
                    "Humanoid Live must apply a changed native final pose to the configured Unity bone");
                Assert.That(
                    HasNativeHairBodyReadbackChanged(
                        initialHairDiagnostics,
                        controller.LastLivePhysicsDiagnostics.bodyDiagnostics),
                    Is.True,
                    "Humanoid Live must advance at least one non-static hair body's native readback");
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                if (avatar != null)
                {
                    UnityEngine.Object.Destroy(avatar);
                }

                if (proxyRig?.ProxyRoot != null)
                {
                    UnityEngine.Object.Destroy(proxyRig.ProxyRoot);
                }

                if (pmxAsset != null)
                {
                    UnityEngine.Object.Destroy(pmxAsset);
                }

                MmdPlayModeTestInstanceScope.DestroyInstance(instance);
            }
        }

        // Alternative C regression: a SINGLE MmdHumanoidAnimationTrack (bound only to the
        // controller, no separate AnimationTrack/Animator binding) carrying a real Humanoid
        // muscle clip must (a) pose the proxy avatar via the track's manually-created
        // AnimationPlayableOutput, (b) run the controller retarget side-effect so the native MMD
        // bone tracks the proxy, and (c) step Live physics — all from one track.
        [UnityTest]
        public IEnumerator HumanoidAnimationTrackSingleTrackMuscleClipDrivesProxyAndRetargetsAndStepsPhysics()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
                yield break;
            }

            MmdUnityModelInstance? instance = null;
            MmdPmxAsset? pmxAsset = null;
            MmdHumanoidProxyRigResult? proxyRig = null;
            Avatar? avatar = null;
            TimelineAsset? timelineAsset = null;
            AnimationClip? muscleClip = null;
            GameObject? directorObject = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_hair_physics.pmx");
                byte[] pmxBytes = File.ReadAllBytes(pmxPath);
                MmdModelDefinition model = LoadHairPhysicsModelForLive(pmxPath);
                instance = MmdUnityModelFactory.CreateSkinnedModel(
                    model,
                    pmxPath,
                    MmdPmxAsset.DefaultImportScale);
                pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                pmxAsset.Initialize(
                    pmxBytes,
                    "test_hair_physics.pmx",
                    pmxPath,
                    MmdPmxAsset.DefaultImportScale,
                    parseSummary: MmdPmxParseSummary.FromModel(model));

                MmdUnityPlaybackController controller = instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.ConfigureModelAsset(pmxAsset);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);

                int drivenBoneIndex = FindFirstValidStaticPhysicsBone(model, instance);
                Assert.That(drivenBoneIndex, Is.GreaterThanOrEqualTo(0),
                    "test_hair_physics.pmx must expose a valid static/bone-driven body for humanoid physics seeding");

                proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(CreateHumanoidMappingModelWithOriginsForLivePhysics());
                Assert.That(proxyRig.ProxyRoot, Is.Not.Null);
                proxyRig.ProxyRoot!.transform.SetParent(instance.Root.transform, worldPositionStays: false);
                proxyRig.ProxyRoot.SetActive(true);
                MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyRig);
                Assert.That(avatarResult.IsValidHumanAvatar, Is.True, string.Join("\n", avatarResult.Diagnostics));
                avatar = avatarResult.Avatar;

                Animator animator = instance.Root.AddComponent<Animator>();
                animator.avatar = avatar;

                Transform proxySpine = proxyRig.BoneMap[HumanBodyBones.Spine];
                Transform drivenBone = instance.BoneTransforms[drivenBoneIndex];
                Quaternion proxySpineBind = proxySpine.localRotation;
                Quaternion drivenBoneBind = drivenBone.localRotation;
                Vector3 proxySpineBindPosition = proxySpine.localPosition;
                Vector3 drivenBindPosition = drivenBone.localPosition;
                controller.ConfigureHumanoidRetarget(
                    proxyRig.ProxyRoot.transform,
                    new[]
                    {
                        new MmdHumanoidRetargetBinding(
                            HumanBodyBones.Spine,
                            drivenBoneIndex,
                            proxySpine,
                            drivenBone,
                            proxySpineBind,
                            drivenBoneBind,
                            copyLocalPosition: true,
                            translationTargetTransform: drivenBone,
                            translationTargetMmdBoneIndex: drivenBoneIndex,
                            proxyBindLocalPosition: proxySpineBindPosition,
                            translationTargetBindLocalPosition: drivenBindPosition)
                    },
                    Array.Empty<MmdHumanoidAppendTransformBinding>());

                // Build a real Humanoid muscle clip: a spine muscle that ramps up so the proxy
                // avatar bends progressively as the timeline advances. This exercises the actual
                // muscle-space retarget path (not a direct transform poke), which is the user's
                // hard condition: a standard Humanoid AnimationClip must drive the MMD model.
                string spineMuscleName = ResolveSpineMuscleName();
                Assert.That(spineMuscleName, Is.Not.Null.And.Not.Empty,
                    "could not resolve a Spine muscle name from HumanTrait");
                muscleClip = new AnimationClip { frameRate = 30.0f };
                muscleClip.SetCurve(
                    string.Empty,
                    typeof(Animator),
                    spineMuscleName,
                    AnimationCurve.Linear(0.0f, 0.0f, 0.5f, 0.9f));

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdHumanoidAnimationTrack humanoidTrack =
                    timelineAsset.CreateTrack<MmdHumanoidAnimationTrack>(null, "MMD Humanoid");
                TimelineClip humanoidClip = humanoidTrack.CreateClip<MmdHumanoidAnimationClip>();
                humanoidClip.start = 0.0;
                humanoidClip.duration = 0.5;
                var humanoidClipAsset = (MmdHumanoidAnimationClip)humanoidClip.asset;
                humanoidClipAsset.clip = muscleClip;

                directorObject = new GameObject("humanoid-animation-track-single-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.playableAsset = timelineAsset;
                // SINGLE generic binding: both the retarget controller and its co-located Animator
                // are derived from this source of truth.
                director.SetGenericBinding(humanoidTrack, controller);

                Assert.That(controller.IsConfigured, Is.False,
                    "The single humanoid track may only create a physics-only binding, never a VMD playback binding.");
                for (int i = 0; i <= 5; i++)
                {
                    director.time = i / 30.0;
                    director.Evaluate();
                    yield return null;
                }

                // Re-evaluate at the same final time to settle the accepted 1-evaluation lag
                // (ProcessFrame reads the proxy the animation output posed on the prior pass).
                director.time = 5 / 30.0;
                director.Evaluate();
                yield return null;

                // (a) the single track's AnimationPlayableOutput posed the proxy from the muscle clip
                float proxyAngle = Quaternion.Angle(proxySpineBind, proxySpine.localRotation);
                Assert.That(proxyAngle, Is.GreaterThan(2.0f),
                    "the muscle clip should pose the proxy spine via the single track's AnimationPlayableOutput " +
                    "(got angle " + proxyAngle + ", muscle=" + spineMuscleName + ")");

                // (b) the same track's ScriptPlayable ran the controller retarget
                Assert.That(controller.LastHumanoidRetargetGate, Is.EqualTo(MmdHumanoidRetargetGate.Ready));
                Assert.That(controller.LastHumanoidRetargetResult, Is.Not.Null);
                Assert.That(controller.LastHumanoidRetargetResult!.CopiedBoneCount, Is.EqualTo(1));

                // (c) the native MMD bone tracks the proxy rotation delta (retarget side-effect)
                float nativeAngle = Quaternion.Angle(drivenBoneBind, drivenBone.localRotation);
                Assert.That(nativeAngle, Is.EqualTo(proxyAngle).Within(0.5f),
                    "the native bone should track the proxy spine rotation delta via the retarget side-effect " +
                    "(proxy " + proxyAngle + " vs native " + nativeAngle + ")");

                // (d) Live physics stepped from the single track, with no VMD playback binding
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null,
                    "the single humanoid track ProcessFrame must step Live physics in Play Mode.");
                Assert.That(controller.LastLivePhysicsDiagnostics!.deltaTime, Is.GreaterThan(0.0f));
                Assert.That(controller.LastSnapshot, Is.Null,
                    "model-only humanoid Timeline physics must not create a VMD playback snapshot.");
            }
            finally
            {
                if (directorObject != null)
                {
                    UnityEngine.Object.Destroy(directorObject);
                }

                if (timelineAsset != null)
                {
                    UnityEngine.Object.Destroy(timelineAsset);
                }

                if (muscleClip != null)
                {
                    UnityEngine.Object.Destroy(muscleClip);
                }

                if (avatar != null)
                {
                    UnityEngine.Object.Destroy(avatar);
                }

                if (proxyRig?.ProxyRoot != null)
                {
                    UnityEngine.Object.Destroy(proxyRig.ProxyRoot);
                }

                if (pmxAsset != null)
                {
                    UnityEngine.Object.Destroy(pmxAsset);
                }

                MmdPlayModeTestInstanceScope.DestroyInstance(instance);
            }
        }

        private static string ResolveSpineMuscleName()
        {
            for (int dof = 0; dof < 3; dof++)
            {
                int muscle = HumanTrait.MuscleFromBone((int)HumanBodyBones.Spine, dof);
                if (muscle >= 0)
                {
                    return HumanTrait.MuscleName[muscle];
                }
            }

            return string.Empty;
        }

        // Regression: the custom Humanoid Timeline output must not let Unity accumulate the
        // body-height offset into the PMX root. Horizontal RootT and RootQ are applied as an
        // absolute root pose, while the imported Animator setting is restored after playback.
        [UnityTest]
        public IEnumerator HumanoidAnimationTrackAppliesStableRootPoseWithoutHeightCollapse()
        {
            GameObject? root = null;
            Avatar? avatar = null;
            TimelineAsset? timelineAsset = null;
            AnimationClip? muscleClip = null;
            GameObject? directorObject = null;
            MmdHumanoidProxyRigResult? proxyRig = null;
            try
            {
                root = new GameObject("humanoid-rootmotion-root");
                var nativeBoneObject = new GameObject("NativeSpine");
                nativeBoneObject.transform.SetParent(root.transform, worldPositionStays: false);
                var nativeCenterObject = new GameObject("NativeCenter");
                nativeCenterObject.transform.SetParent(root.transform, worldPositionStays: false);

                proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(CreateHumanoidMappingModelWithOriginsForLivePhysics());
                Assert.That(proxyRig.ProxyRoot, Is.Not.Null);
                proxyRig.ProxyRoot!.transform.SetParent(root.transform, worldPositionStays: false);
                proxyRig.ProxyRoot.SetActive(true);
                MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(
                    proxyRig,
                    avatarRoot: root);
                Assert.That(avatarResult.IsValidHumanAvatar, Is.True, string.Join("\n", avatarResult.Diagnostics));
                avatar = avatarResult.Avatar;

                Animator animator = root.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.applyRootMotion = true;
                root.AddComponent<MmdHumanoidRootMotionDriver>();

                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(MmdPhysicsMode.Off);

                Transform proxySpine = proxyRig.BoneMap[HumanBodyBones.Spine];
                Transform proxyHips = proxyRig.BoneMap[HumanBodyBones.Hips];
                Vector3 proxyHipsBindPosition = proxyHips.localPosition;
                Vector3 nativeCenterBindPosition = nativeCenterObject.transform.localPosition;
                controller.ConfigureHumanoidRetarget(
                    proxyRig.ProxyRoot.transform,
                    new[]
                    {
                        new MmdHumanoidRetargetBinding(
                            HumanBodyBones.Spine,
                            0,
                            proxySpine,
                            nativeBoneObject.transform,
                            proxySpine.localRotation,
                            nativeBoneObject.transform.localRotation),
                        new MmdHumanoidRetargetBinding(
                            HumanBodyBones.Hips,
                            1,
                            proxyHips,
                            nativeCenterObject.transform,
                            proxyHips.localRotation,
                            nativeCenterObject.transform.localRotation,
                            copyLocalPosition: true,
                            translationTargetTransform: nativeCenterObject.transform,
                            translationTargetMmdBoneIndex: 1,
                            proxyBindLocalPosition: proxyHipsBindPosition,
                            translationTargetBindLocalPosition: nativeCenterBindPosition)
                    },
                    Array.Empty<MmdHumanoidAppendTransformBinding>());

                muscleClip = new AnimationClip { frameRate = 30.0f };
                muscleClip.SetCurve(
                    string.Empty,
                    typeof(Animator),
                    ResolveSpineMuscleName(),
                    AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.5f));
                float bodyHeight = proxyRig.BoneMap[HumanBodyBones.Hips].position.y - root.transform.position.y;
                muscleClip.SetCurve(string.Empty, typeof(Animator), "RootT.x", AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.5f));
                muscleClip.SetCurve(
                    string.Empty,
                    typeof(Animator),
                    "RootT.y",
                    AnimationCurve.Linear(0.0f, bodyHeight, 1.0f, bodyHeight + 0.25f));
                muscleClip.SetCurve(
                    string.Empty,
                    typeof(MmdHumanoidRootMotionDriver),
                    "clipRootVerticalOffset",
                    AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.25f / animator.humanScale));
                muscleClip.SetCurve(string.Empty, typeof(Animator), "RootT.z", AnimationCurve.Constant(0.0f, 1.0f, 0.0f));
                Quaternion rootRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f);
                muscleClip.SetCurve(string.Empty, typeof(Animator), "RootQ.x", AnimationCurve.Linear(0.0f, 0.0f, 1.0f, rootRotation.x));
                muscleClip.SetCurve(string.Empty, typeof(Animator), "RootQ.y", AnimationCurve.Linear(0.0f, 0.0f, 1.0f, rootRotation.y));
                muscleClip.SetCurve(string.Empty, typeof(Animator), "RootQ.z", AnimationCurve.Linear(0.0f, 0.0f, 1.0f, rootRotation.z));
                muscleClip.SetCurve(string.Empty, typeof(Animator), "RootQ.w", AnimationCurve.Linear(0.0f, 1.0f, 1.0f, rootRotation.w));

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdHumanoidAnimationTrack humanoidTrack =
                    timelineAsset.CreateTrack<MmdHumanoidAnimationTrack>(null, "MMD Humanoid");
                TimelineClip humanoidClip = humanoidTrack.CreateClip<MmdHumanoidAnimationClip>();
                humanoidClip.start = 0.0;
                humanoidClip.duration = 1.0;
                var humanoidClipAsset = (MmdHumanoidAnimationClip)humanoidClip.asset;
                humanoidClipAsset.clip = muscleClip;

                directorObject = new GameObject("humanoid-rootmotion-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.Manual;
                director.playableAsset = timelineAsset;
                director.SetGenericBinding(humanoidTrack, controller);

                Assert.That(animator.applyRootMotion, Is.True, "precondition: imported Humanoid root motion starts enabled");

                director.time = 0.0;
                director.Play();
                director.Evaluate();
                yield return null;

                Assert.That(animator.applyRootMotion, Is.False,
                    "the custom Timeline output must suppress Unity's accumulating automatic root motion");
                Assert.That(root.transform.position.y, Is.EqualTo(0.0f).Within(0.001f),
                    "RootT body height must not lower the imported PMX root");
                Vector3 retargetPositionDelta = new Vector3(0.25f, 0.1f, -0.15f);
                proxyHips.localPosition = proxyHipsBindPosition + retargetPositionDelta;
                MmdHumanoidRetargeterResult timelineRetargetResult =
                    controller.ApplyHumanoidRetargetFromTimeline();
                Assert.That(timelineRetargetResult.CopiedTranslationCount, Is.EqualTo(0),
                    "Timeline retarget must not copy proxy Hips translation while RootT is active");
                Assert.That(nativeCenterObject.transform.localPosition, Is.EqualTo(nativeCenterBindPosition),
                    "RootT and native-center translation must not be applied together");

                MmdHumanoidRetargeterResult manualRetargetResult = controller.ApplyHumanoidRetargetNow();
                Assert.That(manualRetargetResult.CopiedTranslationCount, Is.EqualTo(1),
                    "manual retarget must preserve explicit position-copy behavior");
                Vector3 expectedRetargetPosition = nativeCenterBindPosition + retargetPositionDelta;
                Assert.That(
                    Vector3.Distance(nativeCenterObject.transform.localPosition, expectedRetargetPosition),
                    Is.LessThanOrEqualTo(1.0e-5f),
                    "manual retarget must copy the requested position within float precision");
                nativeCenterObject.transform.localPosition = nativeCenterBindPosition;
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Assert.That(Vector3.Distance(leftFoot.position, rightFoot.position), Is.GreaterThan(0.05f),
                    "Timeline playback must disable AnimationClipPlayable's default Foot IK when " +
                    "the baked clip has no foot-goal curves; otherwise both feet collapse to one fallback goal");

                director.time = 0.9;
                director.Evaluate();
                yield return null;
                Vector3 firstPosition = root.transform.position;
                Quaternion firstRotation = root.transform.rotation;
                Assert.That(firstPosition.x, Is.GreaterThan(0.1f), "horizontal RootT must reach the PMX root");
                Assert.That(firstPosition.y, Is.GreaterThan(0.1f),
                    "vertical RootT displacement must reach the PMX root after removing bind body height");
                Assert.That(Quaternion.Angle(Quaternion.identity, firstRotation), Is.GreaterThan(2.0f),
                    "RootQ must reach the PMX root");

                director.Evaluate();
                yield return null;
                Assert.That(Vector3.Distance(root.transform.position, firstPosition), Is.LessThan(0.001f),
                    "re-evaluating one Timeline time must not accumulate root translation");
                Assert.That(Quaternion.Angle(root.transform.rotation, firstRotation), Is.LessThan(0.1f),
                    "re-evaluating one Timeline time must not accumulate root rotation");

                director.Stop();
                yield return null;
                Assert.That(animator.applyRootMotion, Is.True,
                    "Timeline teardown must restore the imported Animator setting");
                proxyHips.localPosition = proxyHipsBindPosition + retargetPositionDelta;
                MmdHumanoidRetargeterResult inactiveTimelineRetargetResult =
                    controller.ApplyHumanoidRetargetFromTimeline();
                Assert.That(inactiveTimelineRetargetResult.CopiedTranslationCount, Is.EqualTo(1),
                    "Timeline retarget without an active root driver must preserve position copying");
                Assert.That(
                    Vector3.Distance(
                        nativeCenterObject.transform.localPosition,
                        nativeCenterBindPosition + retargetPositionDelta),
                    Is.LessThanOrEqualTo(1.0e-5f));

                director.time = 0.0;
                director.Play();
                director.Evaluate();
                yield return null;
                Assert.That(animator.applyRootMotion, Is.False,
                    "replaying a retained Timeline graph must reactivate guarded root motion");
                director.Stop();
            }
            finally
            {
                if (directorObject != null)
                {
                    UnityEngine.Object.Destroy(directorObject);
                }

                if (timelineAsset != null)
                {
                    UnityEngine.Object.Destroy(timelineAsset);
                }

                if (muscleClip != null)
                {
                    UnityEngine.Object.Destroy(muscleClip);
                }

                if (avatar != null)
                {
                    UnityEngine.Object.Destroy(avatar);
                }

                if (proxyRig?.ProxyRoot != null)
                {
                    UnityEngine.Object.Destroy(proxyRig.ProxyRoot);
                }

                if (root != null)
                {
                    UnityEngine.Object.Destroy(root);
                }
            }
        }

        [UnityTest]
        public IEnumerator HumanoidRetargetLateUpdateWithPhysicsOffDoesNotUpdateLiveDiagnostics()
        {
            MmdUnityPlaybackBinding? binding = null;
            MmdHumanoidProxyRigResult? proxyRig = null;
            Avatar? avatar = null;
            PlayableGraph graph = default;
            try
            {
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    MmdPlayModeTestFixtures.CreateMinimalTriangleModel(SyntheticPlayModeModelName),
                    MmdPlayModeTestFixtures.CreateRootTranslationMotion(SyntheticPlayModeModelName),
                    "humanoid-retarget-physics-off.pmx",
                    "humanoid-retarget-physics-off.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Off);

                proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(CreateHumanoidMappingModelWithOriginsForLivePhysics());
                Assert.That(proxyRig.ProxyRoot, Is.Not.Null);
                proxyRig.ProxyRoot!.transform.SetParent(binding.Instance.Root.transform, worldPositionStays: false);
                proxyRig.ProxyRoot.SetActive(true);
                MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyRig);
                Assert.That(avatarResult.IsValidHumanAvatar, Is.True, string.Join("\n", avatarResult.Diagnostics));
                avatar = avatarResult.Avatar;

                Animator animator = binding.Instance.Root.AddComponent<Animator>();
                animator.avatar = avatar;
                graph = CreateBoundAnimatorGraph(animator);

                Transform proxyHips = proxyRig.BoneMap[HumanBodyBones.Hips];
                Transform nativeRoot = binding.Instance.BoneTransforms[0];
                controller.ConfigureHumanoidRetarget(
                    proxyRig.ProxyRoot.transform,
                    new[]
                    {
                        new MmdHumanoidRetargetBinding(
                            HumanBodyBones.Hips,
                            0,
                            proxyHips,
                            nativeRoot)
                    },
                    Array.Empty<MmdHumanoidAppendTransformBinding>());

                proxyHips.localRotation = Quaternion.Euler(0.0f, 9.0f, 0.0f);
                yield return null;

                Assert.That(controller.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Off));
                Assert.That(controller.LastHumanoidRetargetGate, Is.EqualTo(MmdHumanoidRetargetGate.Ready));
                Assert.That(controller.LastHumanoidRetargetResult, Is.Not.Null);
                Assert.That(controller.LastHumanoidRetargetResult!.CopiedBoneCount, Is.EqualTo(1));
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Null);
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                if (avatar != null)
                {
                    UnityEngine.Object.Destroy(avatar);
                }

                if (proxyRig?.ProxyRoot != null)
                {
                    UnityEngine.Object.Destroy(proxyRig.ProxyRoot);
                }

                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [UnityTest]
        public IEnumerator HumanoidRetargetPhysicsOffUsesNativeAppendAndIkWithoutConsoleErrors()
        {
            string pmxPath = ResolvePackageFixture("test_semi_basic_bone.pmx");
            MmdPmxAsset? pmxAsset = null;
            MmdUnityModelInstance? instance = null;
            MmdHumanoidProxyRigResult? proxyRig = null;
            Avatar? avatar = null;
            PlayableGraph graph = default;
            GameObject? managedAppendProbe = null;
            try
            {
                byte[] pmxBytes = File.ReadAllBytes(pmxPath);
                pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                pmxAsset.Initialize(pmxBytes, pmxPath, pmxPath, assetImportScale: 1.0f);
                MmdModelDefinition model = pmxAsset.LoadModel(new NativeMmdParser());
                instance = MmdUnityModelFactory.CreateSkinnedModel(model, pmxPath, 1.0f);

                proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
                Assert.That(proxyRig.ProxyRoot, Is.Not.Null);
                MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyRig);
                Assert.That(avatarResult.IsValidHumanAvatar, Is.True, string.Join("\n", avatarResult.Diagnostics));
                avatar = avatarResult.Avatar;
                proxyRig.ProxyRoot!.transform.SetParent(instance.Root.transform, worldPositionStays: false);
                proxyRig.ProxyRoot.SetActive(true);

                var animator = instance.Root.AddComponent<Animator>();
                animator.avatar = avatar;
                graph = CreateBoundAnimatorGraph(animator);

                var entries = new List<MmdHumanoidRetargetBinding>();
                foreach (MmdHumanoidBoneMappingMatch match in proxyRig.Matches)
                {
                    if (match.MmdBoneIndex < 0 || match.MmdBoneIndex >= instance.BoneTransforms.Length ||
                        !proxyRig.BoneMap.TryGetValue(match.HumanBone, out Transform? proxyTransform))
                    {
                        continue;
                    }

                    entries.Add(new MmdHumanoidRetargetBinding(
                        match.HumanBone,
                        match.MmdBoneIndex,
                        proxyTransform,
                        instance.BoneTransforms[match.MmdBoneIndex]));
                }
                Assert.That(entries, Is.Not.Empty);

                MmdBoneDefinition? appendBone = model.bones.Find(
                    bone => bone.appendParentIndex >= 0 && bone.appendRotation &&
                            Mathf.Abs(bone.appendRatio - 0.25f) < 0.0001f);
                Assert.That(appendBone, Is.Not.Null, "fixture native append helper");
                int appendSourceIndex = appendBone!.appendParentIndex;
                int appendTargetIndex = appendBone.index;
                MmdIkDefinition? ik = model.ik.Find(definition => definition.links?.Count >= 2);
                Assert.That(ik, Is.Not.Null, "fixture native IK chain");
                int ikHandleIndex = ik!.boneIndex;
                int ikLinkIndexA = ik.links[0].boneIndex;
                int ikLinkIndexB = ik.links[1].boneIndex;

                Quaternion appendTargetBind = instance.BindLocalRotations[appendTargetIndex];
                Quaternion ikLinkBindA = instance.BindLocalRotations[ikLinkIndexA];
                Quaternion ikLinkBindB = instance.BindLocalRotations[ikLinkIndexB];
                instance.BoneTransforms[appendSourceIndex].localRotation = Quaternion.Euler(18.0f, 24.0f, 30.0f);
                instance.BoneTransforms[ikHandleIndex].localPosition =
                    instance.BindLocalPositions[ikHandleIndex] + new Vector3(0.2f, 0.0f, 0.0f);

                MmdHumanoidRetargetBinding mappedEntry = entries[0];
                managedAppendProbe = new GameObject("playmode-managed-append-probe");
                managedAppendProbe.transform.SetParent(instance.Root.transform, worldPositionStays: false);
                Quaternion managedAppendBind = Quaternion.Euler(3.0f, 5.0f, 7.0f);
                managedAppendProbe.transform.localRotation = managedAppendBind;
                var managedAppend = new[]
                {
                    new MmdHumanoidAppendTransformBinding(
                        managedAppendProbe.transform,
                        mappedEntry.MmdBoneIndex,
                        mappedEntry.NativeTransform!,
                        mappedEntry.MmdBoneIndex,
                        1.0f,
                        appendRotation: true,
                        appendTranslation: false,
                        appendLocal: true,
                        managedAppendBind,
                        managedAppendProbe.transform.localPosition,
                        mappedEntry.NativeBindLocalRotation,
                        mappedEntry.NativeTransform!.localPosition,
                        evaluationOrder: 100)
                };

                MmdUnityPlaybackController controller = instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.ConfigureModelAsset(pmxAsset);
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                controller.ConfigureHumanoidRetarget(proxyRig.ProxyRoot.transform, entries, managedAppend);
                foreach (MmdHumanoidRetargetBinding entry in entries)
                {
                    entry.ProxyTransform!.localRotation = entry.ProxyBindLocalRotation;
                }
                mappedEntry.ProxyTransform!.localRotation = Quaternion.Euler(8.0f, 13.0f, 21.0f);

                yield return null;

                Assert.That(controller.LastHumanoidRetargetGate, Is.EqualTo(MmdHumanoidRetargetGate.Ready));
                Assert.That(controller.LastHumanoidRetargetResult, Is.Not.Null);
                Assert.That(controller.LastHumanoidRetargetResult!.AllSucceeded, Is.True);
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Null);
                Assert.That(Quaternion.Angle(managedAppendProbe.transform.localRotation, managedAppendBind), Is.LessThan(0.001f),
                    "successful native host pose must skip managed append in PlayMode");
                Assert.That(Quaternion.Angle(
                        instance.BoneTransforms[appendTargetIndex].localRotation,
                        appendTargetBind),
                    Is.GreaterThan(0.01f),
                    "native append must evaluate in PlayMode");
                float ikLinkAngle = Mathf.Max(
                    Quaternion.Angle(instance.BoneTransforms[ikLinkIndexA].localRotation, ikLinkBindA),
                    Quaternion.Angle(instance.BoneTransforms[ikLinkIndexB].localRotation, ikLinkBindB));
                Assert.That(ikLinkAngle, Is.GreaterThan(0.01f), "native IK must evaluate in PlayMode");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                if (avatar != null)
                {
                    UnityEngine.Object.Destroy(avatar);
                }

                if (proxyRig?.ProxyRoot != null)
                {
                    UnityEngine.Object.Destroy(proxyRig.ProxyRoot);
                }

                MmdPlayModeTestInstanceScope.DestroyInstance(instance);
                if (pmxAsset != null)
                {
                    UnityEngine.Object.Destroy(pmxAsset);
                }
            }
        }

        [UnityTest]
        public IEnumerator HumanoidRetargetLiveUsesNativeAppendAndIkFinalPose()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
                yield break;
            }

            string pmxPath = ResolvePackageFixture("test_semi_basic_bone.pmx");
            MmdPmxAsset? pmxAsset = null;
            MmdUnityModelInstance? instance = null;
            MmdHumanoidProxyRigResult? proxyRig = null;
            Avatar? avatar = null;
            PlayableGraph graph = default;
            GameObject? managedAppendProbe = null;
            try
            {
                byte[] pmxBytes = File.ReadAllBytes(pmxPath);
                pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                pmxAsset.Initialize(pmxBytes, pmxPath, pmxPath, assetImportScale: 1.0f);
                MmdModelDefinition model = pmxAsset.LoadModel(new NativeMmdParser());
                instance = MmdUnityModelFactory.CreateSkinnedModel(model, pmxPath, 1.0f);
                proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(model);
                proxyRig.ProxyRoot!.transform.SetParent(instance.Root.transform, worldPositionStays: false);
                proxyRig.ProxyRoot.SetActive(true);
                MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyRig);
                Assert.That(avatarResult.IsValidHumanAvatar, Is.True, string.Join("\n", avatarResult.Diagnostics));
                avatar = avatarResult.Avatar;
                Animator animator = instance.Root.AddComponent<Animator>();
                animator.avatar = avatar;
                graph = CreateBoundAnimatorGraph(animator);

                var entries = new List<MmdHumanoidRetargetBinding>();
                foreach (MmdHumanoidBoneMappingMatch match in proxyRig.Matches)
                {
                    if (match.MmdBoneIndex < 0 || match.MmdBoneIndex >= instance.BoneTransforms.Length ||
                        !proxyRig.BoneMap.TryGetValue(match.HumanBone, out Transform? proxyTransform))
                    {
                        continue;
                    }

                    entries.Add(new MmdHumanoidRetargetBinding(
                        match.HumanBone,
                        match.MmdBoneIndex,
                        proxyTransform,
                        instance.BoneTransforms[match.MmdBoneIndex]));
                }

                MmdBoneDefinition appendBone = model.bones.Find(
                    bone => bone.appendParentIndex >= 0 && bone.appendRotation &&
                            Mathf.Abs(bone.appendRatio - 0.25f) < 0.0001f)!;
                MmdIkDefinition ik = model.ik.Find(definition => definition.links?.Count >= 2)!;
                Assert.That(appendBone, Is.Not.Null, "fixture native append helper");
                Assert.That(ik, Is.Not.Null, "fixture native IK chain");
                int appendSourceIndex = appendBone.appendParentIndex;
                int appendTargetIndex = appendBone.index;
                int ikHandleIndex = ik.boneIndex;
                int ikLinkIndex = ik.links[0].boneIndex;
                Quaternion appendTargetBind = instance.BindLocalRotations[appendTargetIndex];
                Quaternion ikLinkBind = instance.BindLocalRotations[ikLinkIndex];

                // The fixture's twist source and IK handle are native-only helpers, so add
                // explicit proxy bindings to keep their pre-append inputs observable after the
                // model-only Live physics binding is created.
                Transform sourceProxy = proxyRig.BoneMap[HumanBodyBones.LeftUpperArm];
                entries.Add(new MmdHumanoidRetargetBinding(
                    HumanBodyBones.LeftUpperArm,
                    appendSourceIndex,
                    sourceProxy,
                    instance.BoneTransforms[appendSourceIndex],
                    sourceProxy.localRotation,
                    instance.BindLocalRotations[appendSourceIndex]));
                Transform ikProxy = proxyRig.BoneMap[HumanBodyBones.LeftFoot];
                entries.Add(new MmdHumanoidRetargetBinding(
                    HumanBodyBones.LeftFoot,
                    ikHandleIndex,
                    ikProxy,
                    instance.BoneTransforms[ikHandleIndex],
                    copyLocalPosition: true,
                    translationTargetTransform: instance.BoneTransforms[ikHandleIndex],
                    translationTargetMmdBoneIndex: ikHandleIndex,
                    proxyBindLocalPosition: ikProxy.localPosition,
                    translationTargetBindLocalPosition: instance.BindLocalPositions[ikHandleIndex]));
                entries.Add(new MmdHumanoidRetargetBinding(
                    HumanBodyBones.LastBone,
                    -1,
                    proxyTransform: null,
                    nativeTransform: null));
                MmdHumanoidRetargetBinding mappedEntry = entries.Find(
                    entry => entry.MmdBoneIndex == appendSourceIndex) ?? entries[0];

                managedAppendProbe = new GameObject("playmode-live-managed-append-probe");
                managedAppendProbe.transform.SetParent(instance.Root.transform, worldPositionStays: false);
                Quaternion managedAppendBind = Quaternion.Euler(3.0f, 5.0f, 7.0f);
                managedAppendProbe.transform.localRotation = managedAppendBind;
                var managedAppend = new[]
                {
                    new MmdHumanoidAppendTransformBinding(
                        managedAppendProbe.transform,
                        mappedEntry.MmdBoneIndex,
                        mappedEntry.NativeTransform!,
                        mappedEntry.MmdBoneIndex,
                        1.0f,
                        appendRotation: true,
                        appendTranslation: false,
                        appendLocal: true,
                        managedAppendBind,
                        managedAppendProbe.transform.localPosition,
                        mappedEntry.NativeBindLocalRotation,
                        mappedEntry.NativeTransform!.localPosition,
                        evaluationOrder: 100)
                };

                MmdUnityPlaybackController controller = instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.ConfigureModelAsset(pmxAsset);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                controller.ConfigureHumanoidRetarget(proxyRig.ProxyRoot.transform, entries, managedAppend);
                mappedEntry.ProxyTransform!.localRotation = Quaternion.Euler(8.0f, 13.0f, 21.0f);
                ikProxy.localPosition = ikProxy.localPosition + new Vector3(0.2f, 0.0f, 0.0f);

                yield return null;

                Assert.That(controller.LastHumanoidRetargetGate, Is.EqualTo(MmdHumanoidRetargetGate.Ready));
                Assert.That(controller.LastHumanoidRetargetResult!.CopiedBoneCount, Is.GreaterThan(0));
                Assert.That(controller.LastHumanoidRetargetResult.SkippedBoneCount, Is.GreaterThan(0),
                    "partial Humanoid retarget must continue through the native Live physics path");
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(0));
                Assert.That(Quaternion.Angle(managedAppendProbe.transform.localRotation, managedAppendBind), Is.LessThan(0.001f),
                    "native Humanoid Live must skip managed append");
                Assert.That(Quaternion.Angle(instance.BoneTransforms[appendTargetIndex].localRotation, appendTargetBind),
                    Is.GreaterThan(0.01f), "native append must evaluate in Humanoid Live");
                Assert.That(Quaternion.Angle(instance.BoneTransforms[ikLinkIndex].localRotation, ikLinkBind),
                    Is.GreaterThan(0.01f), "native IK must evaluate in Humanoid Live");

                // The test resumes before LateUpdate on the next Unity frame, so this first manual
                // call advances native physics once. Subsequent calls below remain in that frame.
                controller.ApplyHumanoidRetargetNow();
                int sameFrameNativeStep = controller.LastLivePhysicsDiagnostics!.frame;
                Quaternion sameFrameAppendRotation = instance.BoneTransforms[appendTargetIndex].localRotation;
                Quaternion sameFrameIkRotation = instance.BoneTransforms[ikLinkIndex].localRotation;
                controller.ApplyHumanoidRetargetNow();
                Assert.That(Quaternion.Angle(instance.BoneTransforms[appendTargetIndex].localRotation, sameFrameAppendRotation),
                    Is.LessThan(0.001f), "same-frame Humanoid Live evaluation must reuse cached native final pose");
                Assert.That(Quaternion.Angle(instance.BoneTransforms[ikLinkIndex].localRotation, sameFrameIkRotation),
                    Is.LessThan(0.001f), "same-frame native IK pose must not be lost or stepped twice");
                Assert.That(controller.LastLivePhysicsDiagnostics.frame, Is.EqualTo(sameFrameNativeStep),
                    "same-frame cached evaluation must not advance native physics");

                Quaternion changedMappedInput = Quaternion.Euler(31.0f, 17.0f, 9.0f);
                Quaternion expectedChangedNativeRotation =
                    mappedEntry.NativeBindLocalRotation *
                    Quaternion.Inverse(mappedEntry.ProxyBindLocalRotation) *
                    changedMappedInput;
                mappedEntry.ProxyTransform!.localRotation = changedMappedInput;
                controller.ApplyHumanoidRetargetNow();
                Assert.That(Quaternion.Angle(mappedEntry.NativeTransform!.localRotation, expectedChangedNativeRotation),
                    Is.LessThan(0.001f), "changed same-frame Humanoid input must replace the stale cached native pose");
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(sameFrameNativeStep),
                    "changed same-frame input must not advance the native physics frame");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                if (avatar != null)
                {
                    UnityEngine.Object.Destroy(avatar);
                }

                if (managedAppendProbe != null)
                {
                    UnityEngine.Object.Destroy(managedAppendProbe);
                }

                if (proxyRig?.ProxyRoot != null)
                {
                    UnityEngine.Object.Destroy(proxyRig.ProxyRoot);
                }

                MmdPlayModeTestInstanceScope.DestroyInstance(instance);
                if (pmxAsset != null)
                {
                    UnityEngine.Object.Destroy(pmxAsset);
                }
            }
        }

        [UnityTest]
        public IEnumerator ControllerForwardPlaybackInPlayModeRunsLivePhysics()
        {
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                MmdModelDefinition model = MmdPlayModeTestFixtures.CreateMinimalTriangleModel(SyntheticPlayModeModelName);
                AddPinnedRootRigidbody(model);
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model,
                    MmdPlayModeTestFixtures.CreateRootTranslationMotion(SyntheticPlayModeModelName),
                    "controller-playmode-live.pmx",
                    "controller-playmode-live.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);

                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(0));
                Assert.That(controller.LastLivePhysicsDiagnostics.pinnedBodies.pinnedBodyCount, Is.GreaterThan(0));

                controller.Play();
                controller.Tick(1.0f / 30.0f);

                Assert.That(controller.CurrentFrame, Is.EqualTo(1));
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(1));
                Assert.That(controller.LastLivePhysicsDiagnostics.deltaTime, Is.EqualTo(1.0f / 30.0f).Within(0.0001f));
                Assert.That(controller.LastLivePhysicsDiagnostics.stepPhysicsMs, Is.GreaterThan(0.0));
                yield return null;
            }
            finally
            {
                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [UnityTest]
        public IEnumerator PlayableDirectorEvaluateInPlayModeStepsLivePhysics()
        {
            // End-to-end via a real PlayableDirector (controller as the track binding): in Play Mode,
            // a Timeline evaluation steps Live physics through ProcessFrame -> ApplyTimelineLivePhysicsForward.
            //
            // NOTE: within Play Mode, FrameData reports identical signals (evaluationType=Playback,
            // seekOccurred=true, effectivePlayState=Playing) for both forward playback and a programmatic
            // director.Evaluate(), so they cannot be distinguished — the Play Mode boundary is the contract
            // (scrubbing is an Editor/non-playing operation). Editor-side suppression (Application.isPlaying
            // == false) is covered by the EditMode timeline behaviour tests.
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
                yield break;
            }

            MmdUnityPlaybackBinding? binding = null;
            TimelineAsset? timelineAsset = null;
            GameObject? directorObject = null;
            try
            {
                MmdModelDefinition model = MmdPlayModeTestFixtures.CreateMinimalTriangleModel(SyntheticPlayModeModelName);
                AddPinnedRootRigidbody(model);
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model,
                    MmdPlayModeTestFixtures.CreateRootTranslationMotion(SyntheticPlayModeModelName),
                    "timeline-playmode-director-live.pmx",
                    "timeline-playmode-director-live.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack track = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD");
                TimelineClip clip = track.CreateClip<MmdVmdTimelineClip>();
                clip.start = 0.0;
                clip.duration = 1.0;
                var mmdClip = (MmdVmdTimelineClip)clip.asset;
                mmdClip.FrameRate = 30.0f;
                mmdClip.MotionSourceId = "timeline-playmode-director-live.vmd";

                directorObject = new GameObject("timeline-playmode-director-live-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.playableAsset = timelineAsset;
                director.SetGenericBinding(track, controller);

                director.time = 10.0 / 30.0;
                director.Evaluate();

                Assert.That(controller.CurrentFrame, Is.EqualTo(10));
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null,
                    "Play Mode Timeline evaluation must step Live physics through the PlayableDirector path");
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(10));
                Assert.That(controller.LastLivePhysicsDiagnostics.stepPhysicsMs, Is.GreaterThan(0.0));
                Assert.That(controller.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Live));
                yield return null;
            }
            finally
            {
                if (directorObject != null)
                {
                    UnityEngine.Object.Destroy(directorObject);
                }

                if (timelineAsset != null)
                {
                    UnityEngine.Object.Destroy(timelineAsset);
                }

                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [UnityTest]
        public IEnumerator ApplyLivePhysicsForwardFrameSeedsAtArbitraryStartFrame()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
                yield break;
            }

            MmdUnityPlaybackBinding? binding = null;
            try
            {
                MmdModelDefinition model = MmdPlayModeTestFixtures.CreateMinimalTriangleModel(SyntheticPlayModeModelName);
                AddPinnedRootRigidbody(model);
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model,
                    MmdPlayModeTestFixtures.CreateRootTranslationMotion(SyntheticPlayModeModelName),
                    "timeline-forward-arbitrary-start.pmx",
                    "timeline-forward-arbitrary-start.vmd");
                binding.SetPhysicsMode(MmdPhysicsMode.Live);

                // Forward Timeline playback may begin live physics at a non-zero frame; ApplyFrame
                // would reject this with "must start from frame 0".
                MmdPlaybackSnapshot seed = binding.ApplyLivePhysicsForwardFrame(5, 30.0f);

                Assert.That(seed.frame.frame, Is.EqualTo(5));
                Assert.That(binding.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(binding.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(5));
                Assert.That(binding.LastLivePhysicsDiagnostics.deltaTime, Is.EqualTo(0.0f),
                    "first stepped frame must seed the simulation with zero deltaTime");

                binding.ApplyLivePhysicsForwardFrame(6, 30.0f);
                Assert.That(binding.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(6));
                Assert.That(binding.LastLivePhysicsDiagnostics.deltaTime, Is.EqualTo(1.0f / 30.0f).Within(0.0001f));
                Assert.That(binding.LastLivePhysicsDiagnostics.stepPhysicsMs, Is.GreaterThan(0.0));
                yield return null;
            }
            finally
            {
                binding?.Dispose();
                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [UnityTest]
        public IEnumerator BasicPlaybackSampleTimelineEvaluatesThroughPlayableDirector()
        {
            MmdUnityPlaybackBinding? binding = null;
            TimelineAsset? timelineAsset = null;
            GameObject? directorObject = null;
            try
            {
                string pmxPath = ResolveBasicPlaybackSampleAsset("mmt_test_model.pmx");
                string vmdPath = ResolveBasicPlaybackSampleAsset("mmt_test_model_test_motion.vmd");
                var parser = new NativeMmdParser();
                MmdModelDefinition model = parser.LoadModel(File.ReadAllBytes(pmxPath));
                MmdMotionDefinition motion = parser.LoadMotion(File.ReadAllBytes(vmdPath));
                Assert.That(model.vertices.Count, Is.GreaterThan(0), "BasicPlayback sample PMX must contain geometry.");
                Assert.That(model.bones.Count, Is.GreaterThan(0), "BasicPlayback sample PMX must contain bones.");
                Assert.That(motion.boneKeyframes.Count, Is.GreaterThan(0), "BasicPlayback sample VMD must contain bone keys.");

                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model,
                    motion,
                    pmxPath,
                    vmdPath,
                    pmxPath);
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Off);

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack track = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD");
                TimelineClip clip = track.CreateClip<MmdVmdTimelineClip>();
                clip.start = 0.0;
                clip.duration = 1.0;
                var mmdClip = (MmdVmdTimelineClip)clip.asset;
                mmdClip.MotionSourceId = vmdPath;
                mmdClip.FrameRate = 30.0f;
                mmdClip.MotionAsset = null;

                directorObject = new GameObject("basic-playback-sample-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.playableAsset = timelineAsset;
                director.SetGenericBinding(track, controller);

                director.time = 10.0 / 30.0;
                director.Evaluate();

                Assert.That(controller.CurrentFrame, Is.EqualTo(10));
                Assert.That(controller.LastSnapshot, Is.Not.Null, "Timeline evaluation must produce a playback snapshot.");
                Assert.That(controller.LastSnapshot!.frame.bones, Is.Not.Empty);
                Assert.That(binding.Instance.Root.GetComponentsInChildren<SkinnedMeshRenderer>(), Is.Not.Empty,
                    "BasicPlayback sample PMX must create a placed skinned hierarchy.");
                yield return null;
            }
            finally
            {
                if (directorObject != null)
                {
                    UnityEngine.Object.Destroy(directorObject);
                }

                if (timelineAsset != null)
                {
                    UnityEngine.Object.Destroy(timelineAsset);
                }

                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [UnityTest]
        public IEnumerator AutomaticTimelineWorkerBatchAppliesTwoControllersBeforeLateUpdate()
        {
            MmdPmxAsset? modelAsset = null;
            MmdVmdAsset? motionAsset = null;
            MmdUnityPlaybackBinding? firstBinding = null;
            MmdUnityPlaybackBinding? secondBinding = null;
            TimelineAsset? timelineAsset = null;
            GameObject? directorObject = null;
            TimelineWorkerLateUpdateObservation? firstObservation = null;
            TimelineWorkerLateUpdateObservation? secondObservation = null;
            MmdMultiCharacterWorkerPool? firstTimelinePool = null;
            int processFrameCount = 0;
            Action<double> processFrameObserver = _ => processFrameCount++;
            try
            {
                MmdVmdTimelineBehaviour.ProcessFrameEvaluated += processFrameObserver;
                string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");
                string vmdPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                modelAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                motionAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
                modelAsset.Initialize(File.ReadAllBytes(pmxPath), Path.GetFileName(pmxPath), pmxPath);
                motionAsset.Initialize(File.ReadAllBytes(vmdPath), Path.GetFileName(vmdPath), vmdPath);

                firstBinding = MmdUnityPlaybackBinding.CreateSkinned(modelAsset, motionAsset);
                secondBinding = MmdUnityPlaybackBinding.CreateSkinned(modelAsset, motionAsset);
                MmdUnityPlaybackController firstController =
                    firstBinding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                MmdUnityPlaybackController secondController =
                    secondBinding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                firstController.SetPhysicsMode(MmdPhysicsMode.Off);
                firstController.Configure(firstBinding, 30.0f, playOnStart: false);
                secondController.SetPhysicsMode(MmdPhysicsMode.Off);
                secondController.Configure(secondBinding, 30.0f, playOnStart: false);

                // Let controller Start() complete before attaching provider assets. This preserves
                // the existing runtime setup contract used by the PlayMode fixture helpers.
                yield return null;
                ConfigureTimelineWorkerController(firstController, firstBinding, modelAsset, motionAsset);
                ConfigureTimelineWorkerController(secondController, secondBinding, modelAsset, motionAsset);
                Assert.That(
                    firstController.TryGetOrCreateTimelineWorkerPool(
                        out firstTimelinePool,
                        out string firstPoolReason),
                    Is.True,
                    firstPoolReason);
                firstObservation = firstBinding.Instance.Root.AddComponent<TimelineWorkerLateUpdateObservation>();
                secondObservation = secondBinding.Instance.Root.AddComponent<TimelineWorkerLateUpdateObservation>();
                firstObservation.Track(firstController, firstBinding.Instance.BoneTransforms[0]);
                secondObservation.Track(secondController, secondBinding.Instance.BoneTransforms[0]);

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack firstTrack = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD First");
                MmdVmdTimelineTrack secondTrack = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD Second");
                ConfigureTimelineWorkerClip(firstTrack.CreateClip<MmdVmdTimelineClip>(), modelAsset, motionAsset);
                ConfigureTimelineWorkerClip(secondTrack.CreateClip<MmdVmdTimelineClip>(), modelAsset, motionAsset);

                directorObject = new GameObject("timeline-worker-batch-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.GameTime;
                director.playableAsset = timelineAsset;
                director.SetGenericBinding(firstTrack, firstController);
                director.SetGenericBinding(secondTrack, secondController);
                Assert.That(EnsureTimelineWorkerSchedulerInstalled(), Is.True);

                director.time = 10.0 / 30.0;
                // The explicit Evaluate call is outside the collection boundary and must remain
                // synchronous for programmatic seeks/preview-style callers.
                director.Evaluate();
                Assert.That(firstController.CurrentFrame, Is.EqualTo(10));
                Assert.That(secondController.CurrentFrame, Is.EqualTo(10));
                Assert.That(firstController.LastSnapshot, Is.Not.Null);
                Assert.That(secondController.LastSnapshot, Is.Not.Null);

                // Keep the controllers eligible for standalone playback while Timeline is active.
                // The Timeline ownership marker must suppress the standalone boundary for the same
                // frame, otherwise the shared Off worker would be driven twice.
                firstController.Play();
                secondController.Play();
                director.Play();
                director.DeferredEvaluate();
                for (int frame = 0; frame < 30 &&
                     (firstObservation.ObservedBatchSize != 2 || secondObservation.ObservedBatchSize != 2); frame++)
                {
                    yield return null;
                }

                Assert.That(processFrameCount, Is.GreaterThanOrEqualTo(2),
                    "The PlayableDirector did not evaluate both Timeline tracks in PlayMode.");
                Assert.That(
                    GetTimelineWorkerSchedulerMetric<int>("LastCompletedBatchSize"),
                    Is.EqualTo(2),
                    $"processFrameCount={processFrameCount}, " +
                    $"lastDrainFrame={GetTimelineWorkerSchedulerMetric<int>("LastDrainFrameCount")}, " +
                    $"firstObservedBatch={firstObservation.ObservedBatchSize}, " +
                    $"secondObservedBatch={secondObservation.ObservedBatchSize}");
                Assert.That(GetTimelineWorkerSchedulerMetric<ulong>("LastCompletedGeneration"), Is.GreaterThan(0UL));
                Assert.That(firstObservation.ObservedBatchSize, Is.EqualTo(2));
                Assert.That(secondObservation.ObservedBatchSize, Is.EqualTo(2));
                Assert.That(firstObservation.ObservedGeneration, Is.EqualTo(secondObservation.ObservedGeneration));
                Assert.That(firstObservation.ObservedPlayerLoopFrame, Is.EqualTo(secondObservation.ObservedPlayerLoopFrame));
                Assert.That(firstObservation.ObservedAppliedFrame, Is.EqualTo(firstController.CurrentFrame));
                Assert.That(secondObservation.ObservedAppliedFrame, Is.EqualTo(secondController.CurrentFrame));
                Assert.That(firstObservation.ObservedPlayerLoopFrame, Is.EqualTo(firstController.LastTimelineDriveFrameCount));
                Assert.That(secondObservation.ObservedPlayerLoopFrame, Is.EqualTo(secondController.LastTimelineDriveFrameCount));
                Assert.That(firstObservation.ObservedPosition, Is.EqualTo(secondObservation.ObservedPosition));
                Assert.That(
                    MmdStandaloneWorkerScheduler.LastBatchSize,
                    Is.EqualTo(0),
                    "Timeline-owned controllers must not be double-driven by standalone workers.");

                director.Stop();
                for (int frame = 0; frame < 5 && MmdStandaloneWorkerScheduler.LastBatchSize != 2; frame++)
                {
                    yield return null;
                }

                Assert.That(
                    MmdStandaloneWorkerScheduler.LastBatchSize,
                    Is.EqualTo(2),
                    "Standalone playback must resume after Timeline stops.");
                Assert.That(
                    firstController.TryGetOrCreateStandaloneWorkerPool(
                        out MmdMultiCharacterWorkerPool standalonePool,
                        out string standalonePoolReason),
                    Is.True,
                    standalonePoolReason);
                Assert.That(
                    standalonePool,
                    Is.SameAs(firstTimelinePool),
                    "Timeline and standalone Physics Off playback must reuse the same worker slot.");
            }
            finally
            {
                MmdVmdTimelineBehaviour.ProcessFrameEvaluated -= processFrameObserver;
                if (directorObject != null)
                {
                    directorObject.GetComponent<PlayableDirector>()?.Stop();
                    Object.DestroyImmediate(directorObject);
                }

                if (timelineAsset != null)
                {
                    Object.DestroyImmediate(timelineAsset);
                }

                MmdPlayModeTestInstanceScope.DestroyInstance(firstBinding?.Instance);
                MmdPlayModeTestInstanceScope.DestroyInstance(secondBinding?.Instance);
                if (modelAsset != null)
                {
                    Object.DestroyImmediate(modelAsset);
                }

                if (motionAsset != null)
                {
                    Object.DestroyImmediate(motionAsset);
                }
            }
        }

        [UnityTest]
        public IEnumerator AutomaticTimelineWorkerBatchCombinesTwoDirectors()
        {
            MmdPmxAsset? modelAsset = null;
            MmdVmdAsset? motionAsset = null;
            MmdUnityPlaybackBinding? firstBinding = null;
            MmdUnityPlaybackBinding? secondBinding = null;
            TimelineAsset? firstTimelineAsset = null;
            TimelineAsset? secondTimelineAsset = null;
            GameObject? firstDirectorObject = null;
            GameObject? secondDirectorObject = null;
            TimelineWorkerLateUpdateObservation? firstObservation = null;
            TimelineWorkerLateUpdateObservation? secondObservation = null;
            int processFrameCount = 0;
            Action<double> processFrameObserver = _ => processFrameCount++;
            try
            {
                MmdVmdTimelineBehaviour.ProcessFrameEvaluated += processFrameObserver;
                string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");
                string vmdPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                modelAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                motionAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
                modelAsset.Initialize(File.ReadAllBytes(pmxPath), Path.GetFileName(pmxPath), pmxPath);
                motionAsset.Initialize(File.ReadAllBytes(vmdPath), Path.GetFileName(vmdPath), vmdPath);

                firstBinding = MmdUnityPlaybackBinding.CreateSkinned(modelAsset, motionAsset);
                secondBinding = MmdUnityPlaybackBinding.CreateSkinned(modelAsset, motionAsset);
                MmdUnityPlaybackController firstController =
                    firstBinding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                MmdUnityPlaybackController secondController =
                    secondBinding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                firstController.SetPhysicsMode(MmdPhysicsMode.Off);
                firstController.Configure(firstBinding, 30.0f, playOnStart: false);
                secondController.SetPhysicsMode(MmdPhysicsMode.Off);
                secondController.Configure(secondBinding, 30.0f, playOnStart: false);

                yield return null;
                ConfigureTimelineWorkerController(firstController, firstBinding, modelAsset, motionAsset);
                ConfigureTimelineWorkerController(secondController, secondBinding, modelAsset, motionAsset);
                firstObservation = firstBinding.Instance.Root.AddComponent<TimelineWorkerLateUpdateObservation>();
                secondObservation = secondBinding.Instance.Root.AddComponent<TimelineWorkerLateUpdateObservation>();
                firstObservation.Track(firstController, firstBinding.Instance.BoneTransforms[0]);
                secondObservation.Track(secondController, secondBinding.Instance.BoneTransforms[0]);

                firstTimelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack firstTrack = firstTimelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD First");
                ConfigureTimelineWorkerClip(firstTrack.CreateClip<MmdVmdTimelineClip>(), modelAsset, motionAsset);
                firstDirectorObject = new GameObject("timeline-worker-batch-first-director");
                PlayableDirector firstDirector = firstDirectorObject.AddComponent<PlayableDirector>();
                firstDirector.playOnAwake = false;
                firstDirector.timeUpdateMode = DirectorUpdateMode.GameTime;
                firstDirector.playableAsset = firstTimelineAsset;
                firstDirector.SetGenericBinding(firstTrack, firstController);

                secondTimelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack secondTrack = secondTimelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD Second");
                ConfigureTimelineWorkerClip(secondTrack.CreateClip<MmdVmdTimelineClip>(), modelAsset, motionAsset);
                secondDirectorObject = new GameObject("timeline-worker-batch-second-director");
                PlayableDirector secondDirector = secondDirectorObject.AddComponent<PlayableDirector>();
                secondDirector.playOnAwake = false;
                secondDirector.timeUpdateMode = DirectorUpdateMode.GameTime;
                secondDirector.playableAsset = secondTimelineAsset;
                secondDirector.SetGenericBinding(secondTrack, secondController);
                Assert.That(EnsureTimelineWorkerSchedulerInstalled(), Is.True);

                firstDirector.Play();
                secondDirector.Play();
                firstDirector.DeferredEvaluate();
                secondDirector.DeferredEvaluate();
                for (int frame = 0; frame < 30 &&
                     (firstObservation.ObservedBatchSize != 2 || secondObservation.ObservedBatchSize != 2); frame++)
                {
                    yield return null;
                }

                Assert.That(processFrameCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(GetTimelineWorkerSchedulerMetric<int>("LastCompletedBatchSize"), Is.EqualTo(2));
                Assert.That(firstObservation.ObservedBatchSize, Is.EqualTo(2));
                Assert.That(secondObservation.ObservedBatchSize, Is.EqualTo(2));
                Assert.That(firstObservation.ObservedGeneration, Is.EqualTo(secondObservation.ObservedGeneration));
                Assert.That(firstObservation.ObservedPlayerLoopFrame, Is.EqualTo(secondObservation.ObservedPlayerLoopFrame));
                Assert.That(firstObservation.ObservedAppliedFrame, Is.EqualTo(firstController.CurrentFrame));
                Assert.That(secondObservation.ObservedAppliedFrame, Is.EqualTo(secondController.CurrentFrame));
            }
            finally
            {
                MmdVmdTimelineBehaviour.ProcessFrameEvaluated -= processFrameObserver;
                if (firstDirectorObject != null)
                {
                    firstDirectorObject.GetComponent<PlayableDirector>()?.Stop();
                    Object.DestroyImmediate(firstDirectorObject);
                }

                if (secondDirectorObject != null)
                {
                    secondDirectorObject.GetComponent<PlayableDirector>()?.Stop();
                    Object.DestroyImmediate(secondDirectorObject);
                }

                if (firstTimelineAsset != null)
                {
                    Object.DestroyImmediate(firstTimelineAsset);
                }

                if (secondTimelineAsset != null)
                {
                    Object.DestroyImmediate(secondTimelineAsset);
                }

                MmdPlayModeTestInstanceScope.DestroyInstance(firstBinding?.Instance);
                MmdPlayModeTestInstanceScope.DestroyInstance(secondBinding?.Instance);
                if (modelAsset != null)
                {
                    Object.DestroyImmediate(modelAsset);
                }

                if (motionAsset != null)
                {
                    Object.DestroyImmediate(motionAsset);
                }
            }
        }

        private static bool EnsureTimelineWorkerSchedulerInstalled()
        {
            Type schedulerType = ResolveTimelineWorkerSchedulerType();
            MethodInfo method = schedulerType.GetMethod(
                "EnsureInstalled",
                BindingFlags.NonPublic | BindingFlags.Static)!;
            return (bool)method.Invoke(null, null)!;
        }

        private static T GetTimelineWorkerSchedulerMetric<T>(string propertyName)
        {
            Type schedulerType = ResolveTimelineWorkerSchedulerType();
            PropertyInfo property = schedulerType.GetProperty(
                propertyName,
                BindingFlags.NonPublic | BindingFlags.Static)!;
            return (T)property.GetValue(null)!;
        }

        private static Type ResolveTimelineWorkerSchedulerType()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Mmd.Timeline.MmdTimelineWorkerBatchScheduler"))
                .First(type => type != null)!;
        }

        private static void ConfigureTimelineWorkerController(
            MmdUnityPlaybackController controller,
            MmdUnityPlaybackBinding binding,
            MmdPmxAsset modelAsset,
            MmdVmdAsset motionAsset)
        {
            controller.ConfigureModelAsset(modelAsset);
            controller.ConfigureMotionAsset(motionAsset);
            Assert.That(
                controller.TryEnableFastRuntimeFromConfiguredSource(out string reason),
                Is.True,
                reason);
            controller.SeekFrame(0);
        }

        private static void ConfigureTimelineWorkerClip(
            TimelineClip clip,
            MmdPmxAsset modelAsset,
            MmdVmdAsset motionAsset)
        {
            clip.start = 0.0;
            clip.duration = 1.0;
            var asset = (MmdVmdTimelineClip)clip.asset;
            asset.ModelSourceId = modelAsset.SourceId;
            asset.MotionSourceId = motionAsset.SourceId;
            asset.MotionAsset = motionAsset;
            asset.FrameRate = 30.0f;
        }

        [DefaultExecutionOrder(1000)]
        private sealed class TimelineWorkerLateUpdateObservation : MonoBehaviour
        {
            private MmdUnityPlaybackController? controller;
            private Transform? observedTransform;

            internal int ObservedBatchSize { get; private set; } = -1;

            internal ulong ObservedGeneration { get; private set; }

            internal int ObservedPlayerLoopFrame { get; private set; } = -1;

            internal int ObservedAppliedFrame { get; private set; } = -1;

            internal Vector3 ObservedPosition { get; private set; }

            internal void Track(MmdUnityPlaybackController target, Transform transform)
            {
                controller = target ?? throw new ArgumentNullException(nameof(target));
                observedTransform = transform ?? throw new ArgumentNullException(nameof(transform));
            }

            private void LateUpdate()
            {
                if (controller == null || observedTransform == null || controller.LastSnapshot == null)
                {
                    return;
                }

                ObservedBatchSize = GetTimelineWorkerSchedulerMetric<int>("LastCompletedBatchSize");
                ObservedGeneration = GetTimelineWorkerSchedulerMetric<ulong>("LastCompletedGeneration");
                ObservedPlayerLoopFrame = Time.frameCount;
                ObservedAppliedFrame = controller.LastSnapshot.frame.frame;
                ObservedPosition = observedTransform.localPosition;
            }
        }

        [UnityTest]
        public IEnumerator TimelineForwardPlaybackEvaluationStepsLivePhysics()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
                yield break;
            }

            MmdUnityPlaybackBinding? binding = null;
            try
            {
                MmdModelDefinition model = MmdPlayModeTestFixtures.CreateMinimalTriangleModel(SyntheticPlayModeModelName);
                AddPinnedRootRigidbody(model);
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model,
                    MmdPlayModeTestFixtures.CreateRootTranslationMotion(SyntheticPlayModeModelName),
                    "timeline-forward-live.pmx",
                    "timeline-forward-live.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    FrameRate = 30.0f
                };

                // runLivePhysics:true emulates the forward (Playback) evaluation gate that
                // MmdVmdTimelineBehaviour.ProcessFrame computes in real Play Mode playback.
                MmdPlaybackSnapshot frameZero = behaviour.EvaluateAtLocalTime(controller, 0.0, runLivePhysics: true);
                Assert.That(frameZero.frame.frame, Is.EqualTo(0));
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(0));

                behaviour.EvaluateAtLocalTime(controller, 1.0 / 30.0, runLivePhysics: true);
                Assert.That(controller.CurrentFrame, Is.EqualTo(1));
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(1));
                Assert.That(controller.LastLivePhysicsDiagnostics.deltaTime, Is.EqualTo(1.0f / 30.0f).Within(0.0001f));
                Assert.That(controller.LastLivePhysicsDiagnostics.stepPhysicsMs, Is.GreaterThan(0.0));
                Assert.That(controller.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Live));
                yield return null;
            }
            finally
            {
                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [UnityTest]
        public IEnumerator TimelineScrubSuspendsLivePhysicsAndResumeReseeds()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
                yield break;
            }

            MmdUnityPlaybackBinding? binding = null;
            try
            {
                MmdModelDefinition model = MmdPlayModeTestFixtures.CreateMinimalTriangleModel(SyntheticPlayModeModelName);
                AddPinnedRootRigidbody(model);
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model,
                    MmdPlayModeTestFixtures.CreateRootTranslationMotion(SyntheticPlayModeModelName),
                    "timeline-scrub-resume.pmx",
                    "timeline-scrub-resume.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    FrameRate = 30.0f
                };

                // Forward playback steps physics.
                behaviour.EvaluateAtLocalTime(controller, 0.0, runLivePhysics: true);
                behaviour.EvaluateAtLocalTime(controller, 1.0 / 30.0, runLivePhysics: true);
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);

                // Scrub/seek (runLivePhysics:false) suspends physics and resets the simulation.
                MmdPlaybackSnapshot scrub = behaviour.EvaluateAtLocalTime(controller, 8.0 / 30.0, runLivePhysics: false);
                Assert.That(scrub.frame.frame, Is.EqualTo(8));
                Assert.That(controller.CurrentFrame, Is.EqualTo(8));
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Null,
                    "scrub must suspend and reset live physics");
                Assert.That(controller.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Live));

                // Resuming forward playback re-seeds physics from the current pose (zero deltaTime).
                behaviour.EvaluateAtLocalTime(controller, 9.0 / 30.0, runLivePhysics: true);
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(9));
                Assert.That(controller.LastLivePhysicsDiagnostics.deltaTime, Is.EqualTo(0.0f),
                    "resume after scrub must re-seed physics with zero deltaTime");
                yield return null;
            }
            finally
            {
                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [UnityTest]
        public IEnumerator TimelineForwardPlaybackPropagatesHairPhysicsToBones()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
                yield break;
            }

            MmdUnityPlaybackBinding? binding = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_hair_physics.pmx");
                var parser = new NativeMmdParser();
                MmdModelDefinition model = parser.LoadModel(File.ReadAllBytes(pmxPath));
                Assert.That(model.physics.rigidbodies.Count, Is.GreaterThan(0),
                    "test_hair_physics.pmx must contain rigidbody definitions");

                // Pure world-anchor joints (both endpoints -1) are rejected by the descriptor
                // validator; drop them before binding (same as the non-Timeline hair test).
                model.physics.joints.RemoveAll(j => j.rigidbodyAIndex < 0 && j.rigidbodyBIndex < 0);

                MmdMotionDefinition motion = CreateRestPoseMotion(model);
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model, motion, "test_hair_physics.pmx", "rest-pose", pmxPath);
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    FrameRate = 30.0f
                };

                // Seed live physics at frame 0 through the Timeline forward-playback path.
                // runLivePhysics:true is exactly what ProcessFrame computes for evaluationType == Playback
                // in Play Mode; this is the path that was previously suppressed (animation-only).
                behaviour.EvaluateAtLocalTime(controller, 0.0, runLivePhysics: true);

                var hairBoneSlots = new HashSet<int>();
                foreach (MmdRigidbodyDefinition body in model.physics.rigidbodies)
                {
                    if (!string.Equals(body.physicsKind, "static", StringComparison.Ordinal))
                    {
                        hairBoneSlots.Add(body.boneIndex);
                    }
                }

                Assert.That(hairBoneSlots.Count, Is.GreaterThan(0),
                    "Expected at least one non-static rigidbody in test_hair_physics.pmx");

                var frameZeroPositions = new Dictionary<int, Vector3>();
                var frameZeroRotations = new Dictionary<int, Quaternion>();
                foreach (int slot in hairBoneSlots)
                {
                    if (slot >= 0 && slot < binding.Instance.BoneTransforms.Length)
                    {
                        frameZeroPositions[slot] = binding.Instance.BoneTransforms[slot].localPosition;
                        frameZeroRotations[slot] = binding.Instance.BoneTransforms[slot].localRotation;
                    }
                }

                // Advance the Timeline forward; live physics must step each frame.
                for (int frame = 1; frame <= 5; frame++)
                {
                    behaviour.EvaluateAtLocalTime(controller, frame / 30.0, runLivePhysics: true);
                }

                Assert.That(controller.CurrentFrame, Is.EqualTo(5));
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(5));
                Assert.That(controller.LastLivePhysicsDiagnostics.stepPhysicsMs, Is.GreaterThan(0.0));

                bool anyBoneTransformChanged = false;
                foreach (int slot in hairBoneSlots)
                {
                    if (slot < 0 || slot >= binding.Instance.BoneTransforms.Length)
                    {
                        continue;
                    }

                    Vector3 posFive = binding.Instance.BoneTransforms[slot].localPosition;
                    Quaternion rotFive = binding.Instance.BoneTransforms[slot].localRotation;
                    if ((posFive - frameZeroPositions[slot]).sqrMagnitude > 0.0001f ||
                        Quaternion.Angle(frameZeroRotations[slot], rotFive) > 0.01f)
                    {
                        anyBoneTransformChanged = true;
                        break;
                    }
                }

                Assert.That(anyBoneTransformChanged, Is.True,
                    "Expected at least one hair bone transform to change between frame 0 and frame 5 when driven " +
                    "through the Timeline forward-playback path; native-only motion means Live physics is not " +
                    "propagating to the rig during Timeline play.");
            }
            finally
            {
                if (binding?.Instance?.Root != null)
                {
                    MmdPlayModeTestInstanceScope.DestroyInstance(binding.Instance);
                }
                else
                {
                    binding?.Dispose();
                }
            }
        }

        // NOTE: An end-to-end test that calls PlayableDirector.Play() and yields frames to assert
        // live physics steps during real forward playback is intentionally omitted: in headless
        // -batchmode -runTests the PlayableDirector does not advance playback time across
        // `yield return null` (and an unfocused Editor throttles the player loop), so the
        // Playback-evaluation gate cannot be exercised here. The forward routing and hair-bone
        // propagation are covered deterministically by TimelineForwardPlaybackEvaluationStepsLivePhysics,
        // TimelineScrubSuspendsLivePhysicsAndResumeReseeds, and TimelineForwardPlaybackPropagatesHairPhysicsToBones
        // (via the runLivePhysics flag); the FrameData.evaluationType == Playback gate itself is computed
        // in MmdVmdTimelineBehaviour.ProcessFrame and was verified via Editor.log instrumentation.
        private static string ResolvePackageFixture(string fileName)
        {
            return MmdPlayModeTestFixtures.ResolvePackageFixture(fileName);
        }

        private static string ResolveBasicPlaybackSampleAsset(string fileName)
        {
            return MmdPlayModeTestFixtures.ResolveBasicPlaybackSampleAsset(fileName);
        }

        private static HairPhysicsScaleSample RunHairPhysicsForwardPlayback(float importScale)
        {
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_hair_physics.pmx");
                MmdModelDefinition model = LoadHairPhysicsModelForLive(pmxPath);
                MmdMotionDefinition motion = CreateRestPoseMotion(model);
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model, motion, "test_hair_physics.pmx", "rest-pose", pmxPath, importScale);
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.LivePhysicsBodyDiagnosticsSampleInterval = 1;
                controller.Configure(binding, 30.0f, playOnStart: false);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);

                controller.ApplyFrame(0);
                controller.ApplyFrame(1);
                controller.ApplyFrame(5);
                Assert.That(binding.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(binding.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(5));

                MmdUnityPhysicsBody body = binding.Instance.PhysicsBodies.First(physicsBody =>
                    physicsBody != null &&
                    physicsBody.HasNativeTransform &&
                    !string.Equals(physicsBody.PhysicsKind, "static", StringComparison.Ordinal) &&
                    physicsBody.BoneIndex >= 0 &&
                    physicsBody.BoneIndex < binding.Instance.BoneTransforms.Length);
                return new HairPhysicsScaleSample(
                    binding.Instance.ImportScale,
                    body.NativePosition,
                    binding.Instance.BoneTransforms[body.BoneIndex].position,
                    body.transform.position);
            }
            finally
            {
                if (binding?.Instance?.Root != null)
                {
                    MmdPlayModeTestInstanceScope.DestroyInstance(binding.Instance);
                }
                else
                {
                    binding?.Dispose();
                }
            }
        }

        private static MmdModelDefinition LoadHairPhysicsModelForLive(string pmxPath)
        {
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(File.ReadAllBytes(pmxPath));
            Assert.That(model.physics.rigidbodies.Count, Is.GreaterThan(0),
                "test_hair_physics.pmx must contain rigidbody definitions");
            model.physics.joints.RemoveAll(j => j.rigidbodyAIndex < 0 && j.rigidbodyBIndex < 0);
            return model;
        }

        private static int FindFirstValidStaticPhysicsBone(MmdModelDefinition model, MmdUnityModelInstance instance)
        {
            foreach (MmdRigidbodyDefinition body in model.physics.rigidbodies)
            {
                if (string.Equals(body.physicsKind, "static", StringComparison.Ordinal) &&
                    body.boneIndex >= 0 &&
                    body.boneIndex < instance.BoneTransforms.Length)
                {
                    return body.boneIndex;
                }
            }

            return -1;
        }

        private static HashSet<int> CollectNonStaticPhysicsBoneSlots(
            MmdModelDefinition model,
            MmdUnityModelInstance instance)
        {
            var slots = new HashSet<int>();
            foreach (MmdRigidbodyDefinition body in model.physics.rigidbodies)
            {
                if (!string.Equals(body.physicsKind, "static", StringComparison.Ordinal) &&
                    body.boneIndex >= 0 &&
                    body.boneIndex < instance.BoneTransforms.Length)
                {
                    slots.Add(body.boneIndex);
                }
            }

            return slots;
        }

        private static bool HasNativeHairBodyReadbackChanged(
            IReadOnlyList<MmdLivePhysicsBodyDiagnostics> initial,
            IReadOnlyList<MmdLivePhysicsBodyDiagnostics> final)
        {
            foreach (MmdLivePhysicsBodyDiagnostics initialBody in initial)
            {
                MmdLivePhysicsBodyDiagnostics? finalBody = final.FirstOrDefault(
                    body => body.bodyIndex == initialBody.bodyIndex);
                if (finalBody == null)
                {
                    continue;
                }

                if ((finalBody.readbackMmdPosition - initialBody.readbackMmdPosition).sqrMagnitude > 1.0e-6f ||
                    Quaternion.Angle(finalBody.readbackMmdRotation, initialBody.readbackMmdRotation) > 0.01f)
                {
                    return true;
                }
            }

            return false;
        }

        private static PlayableGraph CreateBoundAnimatorGraph(Animator animator)
        {
            PlayableGraph graph = PlayableGraph.Create("HumanoidRetargetLivePhysicsTestGraph");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, new AnimationClip());
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "HumanoidRetargetOutput", animator);
            output.SetSourcePlayable(clipPlayable);
            graph.Play();
            return graph;
        }

        private static MmdModelDefinition CreateHumanoidMappingModelWithOriginsForLivePhysics()
        {
            var model = new MmdModelDefinition();
            AddHumanoidBone(model, 0, "下半身", -1, new[] { 0f, 90f, 0f });
            AddHumanoidBone(model, 1, "上半身", 0, new[] { 0f, 115f, 0f });
            AddHumanoidBone(model, 2, "首", 1, new[] { 0f, 150f, 0f });
            AddHumanoidBone(model, 3, "頭", 2, new[] { 0f, 165f, 0f });
            AddHumanoidBone(model, 4, "左足", 0, new[] { 8f, 85f, 0f });
            AddHumanoidBone(model, 5, "左ひざ", 4, new[] { 8f, 45f, 0f });
            AddHumanoidBone(model, 6, "左足首", 5, new[] { 8f, 5f, 3f });
            AddHumanoidBone(model, 7, "右足", 0, new[] { -8f, 85f, 0f });
            AddHumanoidBone(model, 8, "右ひざ", 7, new[] { -8f, 45f, 0f });
            AddHumanoidBone(model, 9, "右足首", 8, new[] { -8f, 5f, 3f });
            AddHumanoidBone(model, 10, "左腕", 1, new[] { 25f, 135f, 0f });
            AddHumanoidBone(model, 11, "左ひじ", 10, new[] { 50f, 135f, 0f });
            AddHumanoidBone(model, 12, "左手首", 11, new[] { 70f, 135f, 0f });
            AddHumanoidBone(model, 13, "右腕", 1, new[] { -25f, 135f, 0f });
            AddHumanoidBone(model, 14, "右ひじ", 13, new[] { -50f, 135f, 0f });
            AddHumanoidBone(model, 15, "右手首", 14, new[] { -70f, 135f, 0f });
            return model;
        }

        private static void AddHumanoidBone(MmdModelDefinition model, int index, string name, int parentIndex, float[] origin)
        {
            model.bones.Add(new MmdBoneDefinition
            {
                index = index,
                name = name,
                parentIndex = parentIndex,
                origin = origin
            });
        }

        private static MmdMotionDefinition CreateRestPoseMotion(MmdModelDefinition model)
        {
            var parser = new NativeMmdParser();
            return parser.LoadMotion(CreateEmptyVmdBytes(model.name ?? string.Empty));
        }

        private static byte[] CreateEmptyVmdBytes(string modelName)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            WriteFixedSjis(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedSjis(writer, modelName, 20);
            writer.Write(0u); // bone count
            writer.Write(0u); // morph count
            writer.Write(0u); // camera count
            writer.Write(0u); // light count
            writer.Write(0u); // self-shadow count
            writer.Write(0u); // show/IK count
            return stream.ToArray();
        }

        private static void WriteFixedSjis(BinaryWriter writer, string value, int byteCount)
        {
            byte[] buffer = new byte[byteCount];
            byte[] encoded = Encoding.GetEncoding(932).GetBytes(value ?? string.Empty);
            Array.Copy(encoded, 0, buffer, 0, Math.Min(encoded.Length, buffer.Length));
            writer.Write(buffer);
        }

        private readonly struct HairPhysicsScaleSample
        {
            public readonly float importScale;
            public readonly Vector3 nativePosition;
            public readonly Vector3 boneWorldPosition;
            public readonly Vector3 debugWorldPosition;

            public HairPhysicsScaleSample(
                float importScale,
                Vector3 nativePosition,
                Vector3 boneWorldPosition,
                Vector3 debugWorldPosition)
            {
                this.importScale = importScale;
                this.nativePosition = nativePosition;
                this.boneWorldPosition = boneWorldPosition;
                this.debugWorldPosition = debugWorldPosition;
            }
        }
    }
}
