#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.TestTools;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Timeline;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdUnityPlaybackControllerPlayModeTests
    {
        private const string SyntheticPlayModeModelName = MmdPlayModeTestFixtures.MinimalTriangleModelName;

        private static PlaybackSmokeReport? s_smokeReport;
        private static bool s_playOnStartForwardPlayback;
        private static bool s_stopReturnedToBindFrame;
        private static bool s_invalidTickRejected;

        [OneTimeSetUp]
        public void ResetSmokeState()
        {
            s_smokeReport = null;
            s_playOnStartForwardPlayback = false;
            s_stopReturnedToBindFrame = false;
            s_invalidTickRejected = false;
        }

        [OneTimeTearDown]
        public void WriteSmokeArtifact()
        {
            WriteSmokeArtifactIfRequested();
        }

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
                s_playOnStartForwardPlayback = true;
                s_smokeReport = CreateSmokeReport(controller, binding.Instance.BoneTransforms[0].localPosition);
            }
            finally
            {
                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [UnityTest]
        public IEnumerator EditableRigLayerAppliesDuringForwardPlayModeTick()
        {
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    MmdPlayModeTestFixtures.CreateMinimalTriangleModel(SyntheticPlayModeModelName),
                    MmdPlayModeTestFixtures.CreateRootTranslationMotion(SyntheticPlayModeModelName),
                    "playmode-editable-rig-synthetic.pmx",
                    "playmode-editable-rig-synthetic.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                controller.Configure(binding, 30.0f, playOnStart: true);
                var layer = binding.Instance.Root.AddComponent<MmdEditableRigLayer>();
                layer.AddBoneCorrection(
                    binding.Instance.BoneTransforms[0].name,
                    0,
                    new Vector3(0.0f, 0.5f, 0.0f),
                    Quaternion.identity,
                    Vector3.zero);

                layer.EditableRigEnabled = false;
                yield return null;
                controller.Tick(10.0f / 30.0f);

                Assert.That(controller.CurrentFrame, Is.EqualTo(10));
                Assert.That(binding.Instance.BoneTransforms[0].localPosition, Is.EqualTo(new Vector3(-2.0f, 0.0f, 0.0f)));
                Assert.That(controller.LastEditableRigDiagnostics, Is.Not.Null);
                Assert.That(controller.LastEditableRigDiagnostics!.transformState, Is.EqualTo("native-only"));

                controller.Pause();
                controller.ApplyFrame(0);
                layer.EditableRigEnabled = true;
                layer.LayerWeight = 1.0f;
                controller.Play();
                controller.Tick(10.0f / 30.0f);

                Assert.That(controller.CurrentFrame, Is.EqualTo(10));
                Assert.That(binding.Instance.BoneTransforms[0].localPosition, Is.EqualTo(new Vector3(-2.0f, 0.5f, 0.0f)));
                Assert.That(controller.LastEditableRigDiagnostics, Is.Not.Null);
                Assert.That(controller.LastEditableRigDiagnostics!.executionStage, Is.EqualTo("post-native-apply-frame"));
                Assert.That(controller.LastEditableRigDiagnostics.correctedBoneCount, Is.EqualTo(1));

                controller.ApplyFrame(0);
                layer.LayerWeight = 0.0f;
                controller.Play();
                controller.Tick(10.0f / 30.0f);

                Assert.That(binding.Instance.BoneTransforms[0].localPosition, Is.EqualTo(new Vector3(-2.0f, 0.0f, 0.0f)));
                Assert.That(controller.LastEditableRigDiagnostics, Is.Not.Null);
                Assert.That(controller.LastEditableRigDiagnostics!.noOpReason, Is.EqualTo("zero-weight"));
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
                s_stopReturnedToBindFrame = true;
            }
            finally
            {
                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
            }
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
                s_invalidTickRejected = true;
            }
            finally
            {
                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [UnityTest]
        public IEnumerator NativePlaybackSceneLoadsAndEvaluatesPackageFixture()
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync("NativePlayback", LoadSceneMode.Single);
            Assert.That(loadOperation, Is.Not.Null);
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            yield return null;

            // The NativePlayback scene contains the configured "Native Playback" holder controller
            // AND a second controller on the instantiated imported model hierarchy, because the
            // importer attaches a MmdUnityPlaybackController to every imported model (Slice 7) and the
            // scene builder Instantiates that hierarchy as a child. FindAnyObjectByType returns an
            // arbitrary one of the two, so the holder must be selected deterministically by name.
            MmdUnityPlaybackController[] sceneControllers =
                UnityEngine.Object.FindObjectsByType<MmdUnityPlaybackController>(FindObjectsSortMode.None);
            MmdUnityPlaybackController? foundController = sceneControllers
                .FirstOrDefault(c => string.Equals(c.gameObject.name, "Native Playback", StringComparison.Ordinal));
            Assert.That(foundController, Is.Not.Null,
                $"scene must contain the 'Native Playback' holder controller (found {sceneControllers.Length} controller(s))");
            MmdUnityPlaybackController controller = foundController!;
            MmdPmxAsset modelAsset = controller.ModelAssetSource!;
            MmdVmdAsset motionAsset = controller.MotionAssetSource!;
            Assert.That(controller, Is.Not.Null);
            Assert.That(modelAsset, Is.Not.Null);
            Assert.That(motionAsset, Is.Not.Null);
            Assert.That(controller.IsConfigured, Is.True);
            Assert.That(controller.IsPlaying, Is.True);
            Assert.That(controller.PlayOnStart, Is.True);

            // Domain reload slice: verify that after scene load, the scene SMR preserves
            // importer-owned Mesh/Material/bone references instead of rebuilding with "Split Runtime".
            SkinnedMeshRenderer? foundSceneSMR = controller.ConfiguredInstanceRoot != null
                ? controller.ConfiguredInstanceRoot.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true)
                : GameObject.Find("Native Playback")?.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
            Assert.That(foundSceneSMR, Is.Not.Null, "scene must have an SMR (imported hierarchy) for domain reload rebind");
            SkinnedMeshRenderer sceneSMR = foundSceneSMR!;
            Mesh sharedMesh = sceneSMR.sharedMesh;
            Assert.That(sharedMesh, Is.Not.Null);
            Assert.That(sharedMesh.name, Does.Not.Contain("Split Runtime"),
                "scene SMR must use importer-owned Mesh, not a Split Runtime rebuild");
            Assert.That(sceneSMR.bones, Is.Not.Null.And.Not.Empty);
            Assert.That(sceneSMR.bones.Length, Is.EqualTo(modelAsset.BoneCount),
                "scene SMR bone count must match pmxAsset.BoneCount");
            Assert.That(sceneSMR.rootBone, Is.Not.Null,
                "scene SMR must have a valid rootBone");
            Assert.That(sharedMesh, Is.SameAs(modelAsset.ImportedMesh),
                "scene SMR sharedMesh must be the importer-owned Mesh sub-asset");
            if (modelAsset.ImportedMaterials is { Length: > 0 } mats)
            {
                Material[] smrMats = sceneSMR.sharedMaterials;
                Assert.That(smrMats, Is.Not.Null.And.Not.Empty);
                Assert.That(smrMats[0], Is.SameAs(mats[0]),
                    "first scene SMR material must be the importer-owned Material sub-asset");
            }

            controller.Pause();
            controller.SetPhysicsMode(MmdPhysicsMode.Off);
            MmdPlaybackSnapshot frameZero = controller.ApplyFrame(0);
            int frameZeroNumber = frameZero.frame.frame;
            Vector3 frameZeroApplied = ReadAppliedFirstBonePosition(controller, frameZero);
            MmdPlaybackSnapshot frameTen = controller.ApplyFrame(10);
            int frameTenNumber = frameTen.frame.frame;
            Vector3 frameTenApplied = ReadAppliedFirstBonePosition(controller, frameTen);

            Assert.That(frameZeroNumber, Is.EqualTo(0));
            Assert.That(frameTenNumber, Is.EqualTo(10));
            Assert.That(controller.CurrentFrame, Is.EqualTo(10));
            WriteNativeSceneSmokeArtifactIfRequested(frameTen, frameTenApplied);
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

        private static PlaybackSmokeReport CreateSmokeReport(MmdUnityPlaybackController controller, Vector3 firstBonePosition)
        {
            return new PlaybackSmokeReport
            {
                caseName = "phase10-playmode-synthetic-1bone",
                modelSourceId = "synthetic:phase10-playmode-minimal-triangle",
                motionSourceId = "synthetic:phase10-playmode-root-translation",
                frameRate = controller.FrameRate,
                startFrame = 0,
                currentFrame = controller.CurrentFrame,
                snapshotFrame = controller.LastSnapshot!.frame.frame,
                snapshotTime = controller.LastSnapshot.frame.time,
                isPlaying = controller.IsPlaying,
                playOnStart = controller.PlayOnStart,
                firstBoneLocalPosition = PlaybackVector3.From(firstBonePosition)
            };
        }

        private static void WriteSmokeArtifactIfRequested()
        {
            string? outputPath = Environment.GetEnvironmentVariable("YMU_PHASE10_PLAYMODE_SMOKE_OUT");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }

            string? directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var report = s_smokeReport ?? new PlaybackSmokeReport
            {
                caseName = "phase10-playmode-synthetic-1bone"
            };
            report.playOnStartForwardPlayback = s_playOnStartForwardPlayback;
            report.stopReturnedToBindFrame = s_stopReturnedToBindFrame;
            report.invalidTickRejected = s_invalidTickRejected;
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, prettyPrint: true));
        }

        private static void WriteNativeSceneSmokeArtifactIfRequested(MmdPlaybackSnapshot snapshot, Vector3 appliedPosition)
        {
            string? outputPath = Environment.GetEnvironmentVariable("YMU_PHASE10_NATIVE_SCENE_SMOKE_OUT");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }

            string? directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Vector3 evaluatedPosition = snapshot.frame.bones.Count > 0
                ? ToVector3(snapshot.frame.bones[0].localPosition)
                : appliedPosition;
            var report = new NativeSceneSmokeReport
            {
                caseName = "phase10-native-playback-scene-1bone",
                scenePath = "Assets/Scenes/PackagePlaybackSmoke.unity",
                modelSourceId = "packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube.pmx",
                motionSourceId = "packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube_motion.vmd",
                physicsMode = "off",
                frameRate = 30.0f,
                framesChecked = new[] { 0, 10 },
                firstBoneAppliedLocalPosition = PlaybackVector3.From(appliedPosition),
                firstBoneEvaluatedLocalPosition = PlaybackVector3.From(evaluatedPosition)
            };
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, prettyPrint: true));
        }

        private static Vector3 ReadAppliedFirstBonePosition(MmdUnityPlaybackController controller, MmdPlaybackSnapshot snapshot)
        {
            string firstBoneName = snapshot.frame.bones.Count > 0
                ? snapshot.frame.bones[0].name
                : controller.ModelAssetSource!.LoadModel().bones[0].name;
            GameObject root = controller.ConfiguredInstanceRoot ?? GameObject.Find("Native Playback Runtime");
            Assert.That(root, Is.Not.Null);
            Transform firstBone = root.GetComponentsInChildren<Transform>()
                .First(transform => string.Equals(transform.name, firstBoneName, StringComparison.Ordinal));
            return firstBone.localPosition;
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

        private static Vector3 ToVector3(float[] values)
        {
            Assert.That(values, Is.Not.Null);
            Assert.That(values, Has.Length.EqualTo(3));
            return new Vector3(values[0], values[1], values[2]);
        }

        [Serializable]
        private sealed class PlaybackSmokeReport
        {
            public string caseName = string.Empty;
            public string modelSourceId = string.Empty;
            public string motionSourceId = string.Empty;
            public float frameRate;
            public int startFrame;
            public int currentFrame;
            public int snapshotFrame;
            public float snapshotTime;
            public bool isPlaying;
            public bool playOnStart;
            public bool playOnStartForwardPlayback;
            public bool stopReturnedToBindFrame;
            public bool invalidTickRejected;
            public PlaybackVector3 firstBoneLocalPosition = new();
        }

        [Serializable]
        private sealed class NativeSceneSmokeReport
        {
            public string caseName = string.Empty;
            public string scenePath = string.Empty;
            public string modelSourceId = string.Empty;
            public string motionSourceId = string.Empty;
            public string physicsMode = string.Empty;
            public float frameRate;
            public int[] framesChecked = Array.Empty<int>();
            public PlaybackVector3 firstBoneAppliedLocalPosition = new();
            public PlaybackVector3 firstBoneEvaluatedLocalPosition = new();
        }

        [Serializable]
        private sealed class PlaybackVector3
        {
            public float x;
            public float y;
            public float z;

            public static PlaybackVector3 From(Vector3 value)
            {
                return new PlaybackVector3
                {
                    x = value.x,
                    y = value.y,
                    z = value.z
                };
            }
        }

        [UnityTest]
        public IEnumerator HairPhysicsFixtureWithPureWorldAnchorJointsIsRejectedByLivePhysics()
        {
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
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

                var motion = new MmdMotionDefinition
                {
                    targetModelName = model.name,
                    maxFrame = 0,
                    boneKeyframes = new List<MmdBoneKeyframeDefinition>(),
                    morphKeyframes = new List<MmdMorphKeyframeDefinition>(),
                    modelKeyframes = new List<MmdModelKeyframeDefinition>()
                };
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
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
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

                var motion = new MmdMotionDefinition
                {
                    targetModelName = model.name,
                    maxFrame = 0,
                    boneKeyframes = new List<MmdBoneKeyframeDefinition>(),
                    morphKeyframes = new List<MmdMorphKeyframeDefinition>(),
                    modelKeyframes = new List<MmdModelKeyframeDefinition>()
                };

                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model, motion, "test_hair_physics.pmx", "rest-pose", pmxPath);

                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f, playOnStart: false);
                yield return null;

                // ---- Frame 0: initialise live physics (deltaTime = 0, no stepping) ----
                MmdPlaybackSnapshot frameZero = controller.ApplyFrame(0);
                Assert.That(controller.CurrentFrame, Is.EqualTo(0));
                Assert.That(binding.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(binding.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(0));
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
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
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
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
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

                proxyHips.localPosition = proxyBindPosition + new Vector3(0.05f, 0.0f, 0.0f);
                proxyHips.localRotation = Quaternion.Euler(0.0f, 4.0f, 0.0f);
                yield return null;

                Assert.That(controller.LastHumanoidRetargetGate, Is.EqualTo(MmdHumanoidRetargetGate.Ready));
                Assert.That(controller.IsConfigured, Is.False,
                    "Self-tick humanoid Live physics must not configure the VMD playback binding.");
                Assert.That(controller.HasHumanoidPhysicsBinding, Is.True,
                    "Self-tick humanoid Live physics should lazily create a model-only physics binding from ModelAssetSource.");
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(0));
                Assert.That(controller.LastLivePhysicsDiagnostics.deltaTime, Is.EqualTo(0.0f));
                Assert.That(controller.LastSnapshot, Is.Null,
                    "Humanoid-driven live physics must not overwrite the VMD playback snapshot surface.");

                HashSet<int> hairBoneSlots = CollectNonStaticPhysicsBoneSlots(model, instance);
                Assert.That(hairBoneSlots.Count, Is.GreaterThan(0),
                    "Expected at least one non-static rigidbody linked to a hair bone");
                var frameZeroPositions = new Dictionary<int, Vector3>();
                var frameZeroRotations = new Dictionary<int, Quaternion>();
                foreach (int slot in hairBoneSlots)
                {
                    frameZeroPositions[slot] = instance.BoneTransforms[slot].localPosition;
                    frameZeroRotations[slot] = instance.BoneTransforms[slot].localRotation;
                }

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

                bool anyHairBoneChanged = false;
                foreach (int slot in hairBoneSlots)
                {
                    Transform bone = instance.BoneTransforms[slot];
                    if ((bone.localPosition - frameZeroPositions[slot]).sqrMagnitude > 0.0001f ||
                        Quaternion.Angle(frameZeroRotations[slot], bone.localRotation) > 0.01f)
                    {
                        anyHairBoneChanged = true;
                    }

                    Assert.That(
                        Vector3.Distance(instance.Root.transform.position, bone.position),
                        Is.LessThan(1000.0f),
                        "Humanoid-driven live physics hair bone must remain near the model root");
                }

                Assert.That(anyHairBoneChanged, Is.True,
                    "Expected humanoid-retargeted body motion to drive live physics feedback into at least one non-static hair bone");
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

        [UnityTest]
        public IEnumerator HumanoidRetargetLateUpdateCreatesModelOnlyPhysicsBindingFromModelAsset()
        {
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
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

                proxyHips.localPosition = proxyBindPosition + new Vector3(0.05f, 0.0f, 0.0f);
                proxyHips.localRotation = Quaternion.Euler(0.0f, 4.0f, 0.0f);
                yield return null;

                Assert.That(controller.LastHumanoidRetargetGate, Is.EqualTo(MmdHumanoidRetargetGate.Ready));
                Assert.That(controller.IsConfigured, Is.False,
                    "Model-only humanoid physics binding must stay separate from the VMD playback binding.");
                Assert.That(controller.HasHumanoidPhysicsBinding, Is.True,
                    "Humanoid Live physics should lazily create its own model-only binding from ModelAssetSource.");
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(0));
                Assert.That(controller.LastSnapshot, Is.Null,
                    "Model-only humanoid physics binding must not create a VMD playback snapshot.");

                HashSet<int> hairBoneSlots = CollectNonStaticPhysicsBoneSlots(model, instance);
                Assert.That(hairBoneSlots.Count, Is.GreaterThan(0),
                    "Expected at least one non-static rigidbody linked to a hair bone");
                var frameZeroPositions = new Dictionary<int, Vector3>();
                var frameZeroRotations = new Dictionary<int, Quaternion>();
                foreach (int slot in hairBoneSlots)
                {
                    frameZeroPositions[slot] = instance.BoneTransforms[slot].localPosition;
                    frameZeroRotations[slot] = instance.BoneTransforms[slot].localRotation;
                }

                for (int i = 1; i <= 5; i++)
                {
                    proxyHips.localPosition = proxyBindPosition + new Vector3(0.05f + 0.01f * i, 0.0f, 0.0f);
                    proxyHips.localRotation = Quaternion.Euler(0.0f, 4.0f + i, 0.0f);
                    yield return null;
                }

                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(5));
                Assert.That(controller.LastLivePhysicsDiagnostics.deltaTime, Is.GreaterThan(0.0f));

                bool anyHairBoneChanged = false;
                foreach (int slot in hairBoneSlots)
                {
                    Transform bone = instance.BoneTransforms[slot];
                    if ((bone.localPosition - frameZeroPositions[slot]).sqrMagnitude > 0.0001f ||
                        Quaternion.Angle(frameZeroRotations[slot], bone.localRotation) > 0.01f)
                    {
                        anyHairBoneChanged = true;
                    }
                }

                Assert.That(anyHairBoneChanged, Is.True,
                    "Expected model-only humanoid live physics to feed Bullet readback into at least one non-static hair bone");
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
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
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
                humanoidClipAsset.proxyAnimator.exposedName =
                    "mmdHumanoidProxyAnimator_" + Guid.NewGuid().ToString("N");

                directorObject = new GameObject("humanoid-animation-track-single-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.playableAsset = timelineAsset;
                // SINGLE generic binding (controller, for the retarget ProcessFrame). The proxy
                // Animator that the clip poses is supplied via an ExposedReference; setup/authoring
                // sets this automatically since the Animator is on the controller's GameObject.
                director.SetGenericBinding(humanoidTrack, controller);
                director.SetReferenceValue(humanoidClipAsset.proxyAnimator.exposedName, animator);

                Assert.That(controller.IsConfigured, Is.False,
                    "The single humanoid track may only create a physics-only binding, never a VMD playback binding.");
                Assert.That(controller.HasHumanoidPhysicsBinding, Is.False);

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
                Assert.That(controller.HasHumanoidPhysicsBinding, Is.True,
                    "the single humanoid track should lazily create a model-only physics binding from ModelAssetSource.");
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

        // Regression: the single MmdHumanoidAnimationTrack must enable applyRootMotion on the proxy
        // Animator so a Humanoid clip's root motion (RootT/RootQ) travels the model the way a standard
        // AnimationTrack did. Without it the model only does in-place body sway (the "腰の移動" /
        // center travel is dropped). We assert the track flips applyRootMotion on at graph build; that
        // the resulting root curves actually translate the model is Unity's AnimationPlayableOutput
        // behavior (verified on the real dance clip in-editor).
        [UnityTest]
        public IEnumerator HumanoidAnimationTrackEnablesRootMotionOnProxyAnimator()
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

                proxyRig = MmdHumanoidProxyRigFactory.CreateProxyRig(CreateHumanoidMappingModelWithOriginsForLivePhysics());
                Assert.That(proxyRig.ProxyRoot, Is.Not.Null);
                proxyRig.ProxyRoot!.transform.SetParent(root.transform, worldPositionStays: false);
                proxyRig.ProxyRoot.SetActive(true);
                MmdHumanoidAvatarBuildResult avatarResult = MmdHumanoidProxyRigFactory.BuildAvatar(proxyRig);
                Assert.That(avatarResult.IsValidHumanAvatar, Is.True, string.Join("\n", avatarResult.Diagnostics));
                avatar = avatarResult.Avatar;

                Animator animator = root.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.applyRootMotion = false;

                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(MmdPhysicsMode.Off);

                Transform proxySpine = proxyRig.BoneMap[HumanBodyBones.Spine];
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
                            nativeBoneObject.transform.localRotation)
                    },
                    Array.Empty<MmdHumanoidAppendTransformBinding>());

                muscleClip = new AnimationClip { frameRate = 30.0f };
                muscleClip.SetCurve(
                    string.Empty,
                    typeof(Animator),
                    ResolveSpineMuscleName(),
                    AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.5f));

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdHumanoidAnimationTrack humanoidTrack =
                    timelineAsset.CreateTrack<MmdHumanoidAnimationTrack>(null, "MMD Humanoid");
                TimelineClip humanoidClip = humanoidTrack.CreateClip<MmdHumanoidAnimationClip>();
                humanoidClip.start = 0.0;
                humanoidClip.duration = 1.0;
                var humanoidClipAsset = (MmdHumanoidAnimationClip)humanoidClip.asset;
                humanoidClipAsset.clip = muscleClip;
                humanoidClipAsset.proxyAnimator.exposedName =
                    "mmdHumanoidRootMotionAnimator_" + Guid.NewGuid().ToString("N");

                directorObject = new GameObject("humanoid-rootmotion-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.playableAsset = timelineAsset;
                director.SetGenericBinding(humanoidTrack, controller);
                director.SetReferenceValue(humanoidClipAsset.proxyAnimator.exposedName, animator);

                Assert.That(animator.applyRootMotion, Is.False, "precondition: root motion starts disabled");

                director.time = 0.0;
                director.Evaluate();
                yield return null;

                Assert.That(animator.applyRootMotion, Is.True,
                    "the single humanoid track must enable applyRootMotion so the clip's root motion travels the model");
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
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
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
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
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
        public IEnumerator TimelineForwardPlaybackEvaluationStepsLivePhysics()
        {
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
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
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
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
            MmdPhysicsBackendAvailability availability = BulletMmdPhysicsBackend.ProbeAvailability();
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

                var motion = new MmdMotionDefinition
                {
                    targetModelName = model.name,
                    maxFrame = 0,
                    boneKeyframes = new List<MmdBoneKeyframeDefinition>(),
                    morphKeyframes = new List<MmdMorphKeyframeDefinition>(),
                    modelKeyframes = new List<MmdModelKeyframeDefinition>()
                };
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
            string? projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException("Unity project root could not be resolved from Application.dataPath.");
            }

            string packageRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "packages", "com.yohawing.mmd-loader"));
            return Path.Combine(packageRoot, "Tests", "Fixtures", "Assets", fileName);
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

        private static int FindFirstValidStaticPhysicsBone(MmdModelDefinition model, MmdUnityPlaybackBinding binding)
        {
            foreach (MmdRigidbodyDefinition body in model.physics.rigidbodies)
            {
                if (string.Equals(body.physicsKind, "static", StringComparison.Ordinal) &&
                    body.boneIndex >= 0 &&
                    body.boneIndex < binding.Instance.BoneTransforms.Length)
                {
                    return body.boneIndex;
                }
            }

            return -1;
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
            MmdUnityPlaybackBinding binding)
        {
            var slots = new HashSet<int>();
            foreach (MmdRigidbodyDefinition body in model.physics.rigidbodies)
            {
                if (!string.Equals(body.physicsKind, "static", StringComparison.Ordinal) &&
                    body.boneIndex >= 0 &&
                    body.boneIndex < binding.Instance.BoneTransforms.Length)
                {
                    slots.Add(body.boneIndex);
                }
            }

            return slots;
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
            return new MmdMotionDefinition
            {
                targetModelName = model.name,
                maxFrame = 0,
                boneKeyframes = new List<MmdBoneKeyframeDefinition>(),
                morphKeyframes = new List<MmdMorphKeyframeDefinition>(),
                modelKeyframes = new List<MmdModelKeyframeDefinition>()
            };
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
