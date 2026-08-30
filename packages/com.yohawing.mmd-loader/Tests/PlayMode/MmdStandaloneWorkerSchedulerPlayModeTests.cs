#nullable enable

using System;
using System.Collections;
using System.IO;
using Mmd;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed class MmdStandaloneWorkerSchedulerPlayModeTests
    {
        [UnityTest]
        public IEnumerator PhysicsOffControllerUsesAutomaticWorkerBeforeControllerUpdate()
        {
            MmdPmxAsset? modelAsset = null;
            MmdVmdAsset? motionAsset = null;
            MmdUnityPlaybackBinding? binding = null;
            StandaloneWorkerUpdateObservation? observation = null;
            GameObject? downstream = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");
                string vmdPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                modelAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                modelAsset.Initialize(File.ReadAllBytes(pmxPath), Path.GetFileName(pmxPath), pmxPath);
                motionAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
                motionAsset.Initialize(File.ReadAllBytes(vmdPath), Path.GetFileName(vmdPath), vmdPath);

                binding = MmdUnityPlaybackBinding.CreateSkinned(modelAsset, motionAsset);
                MmdUnityPlaybackController controller =
                    binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                controller.Configure(binding, 30.0f, playOnStart: false);
                yield return null;

                controller.ConfigureModelAsset(modelAsset);
                controller.ConfigureMotionAsset(motionAsset);
                controller.SeekFrame(0);
                observation = binding.Instance.Root.AddComponent<StandaloneWorkerUpdateObservation>();
                observation.Track(controller);
                downstream = new GameObject("standalone-worker-parent-constraint-consumer");
                ParentConstraint constraint = downstream.AddComponent<ParentConstraint>();
                constraint.AddSource(new ConstraintSource
                {
                    sourceTransform = binding.Instance.BoneTransforms[0],
                    weight = 1.0f
                });
                constraint.translationAxis = Axis.X | Axis.Y | Axis.Z;
                constraint.rotationAxis = Axis.X | Axis.Y | Axis.Z;
                constraint.constraintActive = true;
                controller.Play();

                yield return null;

                Assert.That(observation.SawConfiguredPoseBeforeControllerUpdate, Is.True);
                Assert.That(observation.ObservedBatchSize, Is.EqualTo(1));
                Assert.That(observation.ObservedFrame, Is.GreaterThanOrEqualTo(0));
                Assert.That(controller.LastSnapshot, Is.Not.Null);
                Assert.That(
                    controller.LastSnapshot!.frame.frame,
                    Is.EqualTo(observation.ObservedFrame));
                Assert.That(controller.CurrentFrame, Is.EqualTo(observation.ObservedFrame));
                Assert.That(
                    Vector3.Distance(downstream.transform.position, binding.Instance.BoneTransforms[0].position),
                    Is.LessThan(0.001f),
                    "A same-frame downstream ParentConstraint must observe the worker-applied pose.");

                // Unity's test PlayerLoop may report zero deltaTime, so exercise the 60 Hz clock
                // contract deterministically. The first half-frame must survive apply and combine
                // with the second half-frame instead of being truncated back to frame zero.
                controller.SeekFrame(0);
                controller.enabled = false;
                controller.Play();
                MmdMultiCharacterWorkerPool? initialPool = null;
                for (int i = 0; i < 2; i++)
                {
                    Assert.That(
                        controller.TryPrepareStandaloneWorkerEvaluation(
                            1.0f / 60.0f,
                            out MmdMultiCharacterWorkerPool pool,
                            out MmdMultiCharacterWorkerRequest request,
                            out _,
                            out string prepareReason),
                        Is.True,
                        prepareReason);
                    initialPool ??= pool;
                    Assert.That(pool, Is.SameAs(initialPool));
                    pool.BeginEvaluate(request);
                    pool.CompleteEvaluate();
                    controller.ApplyPreparedMultiCharacterFrame(
                        request.Frame,
                        request.Time,
                        request.FrameRate,
                        pool.GetResult(0));
                }

                Assert.That(
                    controller.CurrentFrame,
                    Is.EqualTo(1),
                    "Two 1/60-second worker evaluations at 30 fps must accumulate to frame one.");
            }
            finally
            {
                if (downstream != null)
                {
                    Object.DestroyImmediate(downstream);
                }

                MmdPlayModeTestInstanceScope.DestroyInstance(binding?.Instance);
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
        public IEnumerator PhysicsOffControllersUseOneAutomaticBatchWithIndependentRates()
        {
            MmdPmxAsset? modelAsset = null;
            MmdVmdAsset? motionAsset = null;
            MmdUnityPlaybackBinding? firstBinding = null;
            MmdUnityPlaybackBinding? secondBinding = null;
            GameObject? firstDownstream = null;
            GameObject? secondDownstream = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");
                string vmdPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                modelAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                modelAsset.Initialize(File.ReadAllBytes(pmxPath), Path.GetFileName(pmxPath), pmxPath);
                motionAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
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
                secondController.Configure(secondBinding, 24.0f, playOnStart: false);
                yield return null;

                firstController.ConfigureModelAsset(modelAsset);
                firstController.ConfigureMotionAsset(motionAsset);
                secondController.ConfigureModelAsset(modelAsset);
                secondController.ConfigureMotionAsset(motionAsset);
                firstController.SeekFrame(0);
                secondController.SeekFrame(0);
                firstDownstream = CreateParentConstraintConsumer(
                    "standalone-worker-parent-constraint-first",
                    firstBinding.Instance.BoneTransforms[0]);
                secondDownstream = CreateParentConstraintConsumer(
                    "standalone-worker-parent-constraint-second",
                    secondBinding.Instance.BoneTransforms[0]);
                firstController.Play();
                secondController.Play();
                yield return null;

                Assert.That(MmdStandaloneWorkerScheduler.LastBatchSize, Is.EqualTo(2));
                Assert.That(firstController.LastSnapshot, Is.Not.Null);
                Assert.That(secondController.LastSnapshot, Is.Not.Null);
                Assert.That(firstController.CurrentFrame, Is.EqualTo(firstController.LastSnapshot!.frame.frame));
                Assert.That(secondController.CurrentFrame, Is.EqualTo(secondController.LastSnapshot!.frame.frame));
                Assert.That(firstController.FrameRate, Is.Not.EqualTo(secondController.FrameRate));
                Assert.That(
                    Vector3.Distance(firstDownstream.transform.position, firstBinding.Instance.BoneTransforms[0].position),
                    Is.LessThan(0.001f),
                    "The first same-frame downstream ParentConstraint must observe its worker-applied pose.");
                Assert.That(
                    Vector3.Distance(secondDownstream.transform.position, secondBinding.Instance.BoneTransforms[0].position),
                    Is.LessThan(0.001f),
                    "The second same-frame downstream ParentConstraint must observe its worker-applied pose.");
            }
            finally
            {
                if (firstDownstream != null)
                {
                    Object.DestroyImmediate(firstDownstream);
                }

                if (secondDownstream != null)
                {
                    Object.DestroyImmediate(secondDownstream);
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
        public IEnumerator PhysicsOffFourControllersUseOneAutomaticBatchWithoutGroupOwnership()
        {
            MmdPmxAsset? modelAsset = null;
            MmdVmdAsset? motionAsset = null;
            var bindings = new MmdUnityPlaybackBinding?[4];
            try
            {
                string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");
                string vmdPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                modelAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                modelAsset.Initialize(File.ReadAllBytes(pmxPath), Path.GetFileName(pmxPath), pmxPath);
                motionAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
                motionAsset.Initialize(File.ReadAllBytes(vmdPath), Path.GetFileName(vmdPath), vmdPath);

                float[] rates = { 30.0f, 24.0f, 20.0f, 15.0f };
                for (int i = 0; i < bindings.Length; i++)
                {
                    bindings[i] = MmdUnityPlaybackBinding.CreateSkinned(modelAsset, motionAsset);
                    MmdUnityPlaybackController controller =
                        bindings[i]!.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                    controller.SetPhysicsMode(MmdPhysicsMode.Off);
                    controller.Configure(bindings[i]!, rates[i], playOnStart: false);
                }

                yield return null;

                for (int i = 0; i < bindings.Length; i++)
                {
                    MmdUnityPlaybackController controller =
                        bindings[i]!.Instance.Root.GetComponent<MmdUnityPlaybackController>()!;
                    controller.ConfigureModelAsset(modelAsset);
                    controller.ConfigureMotionAsset(motionAsset);
                    controller.SeekFrame(i);
                    controller.Play();
                }

                yield return null;

                Assert.That(MmdStandaloneWorkerScheduler.LastBatchSize, Is.EqualTo(4));
                for (int i = 0; i < bindings.Length; i++)
                {
                    MmdUnityPlaybackController controller =
                        bindings[i]!.Instance.Root.GetComponent<MmdUnityPlaybackController>()!;
                    Assert.That(controller.LastSnapshot, Is.Not.Null, $"controller {i} did not apply a snapshot");
                    Assert.That(controller.CurrentFrame, Is.EqualTo(controller.LastSnapshot!.frame.frame));
                    Assert.That(controller.FrameRate, Is.EqualTo(rates[i]));
                }
            }
            finally
            {
                for (int i = 0; i < bindings.Length; i++)
                {
                    MmdPlayModeTestInstanceScope.DestroyInstance(bindings[i]?.Instance);
                }

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
        public IEnumerator LiveOneControllerAutomaticWorkerSeedsHoldsAndReseedsAfterSeek()
        {
            IgnoreIfLivePhysicsUnavailable();
            MmdPmxAsset? modelAsset = null;
            MmdVmdAsset? motionAsset = null;
            MmdUnityPlaybackBinding? automaticBinding = null;
            MmdUnityPlaybackBinding? serialBinding = null;
            try
            {
                CreateLiveFixture(
                    out MmdModelDefinition model,
                    out MmdMotionDefinition motion,
                    out modelAsset,
                    out motionAsset,
                    out string modelPath,
                    out string motionPath);
                automaticBinding = MmdUnityPlaybackBinding.CreateSkinned(
                    model,
                    motion,
                    modelPath,
                    motionPath,
                    modelPath);
                MmdUnityPlaybackController automatic =
                    automaticBinding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                ConfigureAutomaticController(automatic, automaticBinding, 30.0f);
                automatic.LivePhysicsBodyDiagnosticsSampleInterval = 1;

                // A disabled controller gives us a synchronous serial oracle without registering
                // another participant in the automatic PlayerLoop batch.
                serialBinding = MmdUnityPlaybackBinding.CreateSkinned(
                    model,
                    motion,
                    modelPath,
                    motionPath,
                    modelPath);
                MmdUnityPlaybackController serial =
                    serialBinding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                ConfigureAutomaticController(serial, serialBinding, 30.0f);
                serial.enabled = false;
                serial.SeekFrame(0);

                yield return null;

                automatic.ConfigureModelAsset(modelAsset);
                automatic.ConfigureMotionAsset(motionAsset);
                automatic.SeekFrame(0);
                automatic.Play();
                serial.ConfigureModelAsset(modelAsset);
                serial.ConfigureMotionAsset(motionAsset);

                // The test runner may resume a coroutine at the same PlayerLoop boundary where
                // Play() was called. Wait only until the first worker result is visible so the
                // assertion observes the completed automatic boundary rather than a pre-dispatch
                // LastBatchSize reset.
                for (int attempt = 0; attempt < 4 && automatic.LastLivePhysicsDiagnostics == null; attempt++)
                {
                    yield return null;
                }

                Assert.That(MmdStandaloneWorkerScheduler.LastBatchSize, Is.EqualTo(1));
                Assert.That(automatic.LastSnapshot, Is.Not.Null);
                Assert.That(automatic.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(automatic.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(automatic.CurrentFrame));
                Assert.That(automatic.LastLivePhysicsDiagnostics.deltaTime, Is.EqualTo(0.0f),
                    "The first automatic Live evaluation must be a zero-delta seed.");
                Assert.That(serial.LastSnapshot, Is.Not.Null);
                Assert.That(automatic.LastSnapshot!.frame.frame, Is.EqualTo(serial.LastSnapshot!.frame.frame));
                Assert.That(
                    Vector3.Distance(
                        automaticBinding.Instance.BoneTransforms[0].localPosition,
                        serialBinding.Instance.BoneTransforms[0].localPosition),
                    Is.LessThan(0.001f),
                    "Automatic Live seed pose must match the serial pose oracle.");

                int heldFrame = automatic.CurrentFrame;
                float heldDeltaTime = automatic.LastLivePhysicsDiagnostics.deltaTime;
                automatic.Pause();
                yield return null;
                Assert.That(automatic.CurrentFrame, Is.EqualTo(heldFrame));
                Assert.That(automatic.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(heldFrame));
                Assert.That(automatic.LastLivePhysicsDiagnostics.deltaTime, Is.EqualTo(heldDeltaTime));

                automatic.Play();
                yield return null;
                Assert.That(automatic.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(automatic.LastLivePhysicsDiagnostics!.frame, Is.GreaterThanOrEqualTo(heldFrame));

                automatic.SeekFrame(0);
                automatic.Play();
                yield return null;
                Assert.That(automatic.LastLivePhysicsDiagnostics, Is.Not.Null);
                Assert.That(automatic.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(automatic.CurrentFrame));
                Assert.That(automatic.LastLivePhysicsDiagnostics.deltaTime, Is.EqualTo(0.0f),
                    "SeekFrame must discard the old worker and reseed Live physics.");
            }
            finally
            {
                MmdPlayModeTestInstanceScope.DestroyInstance(automaticBinding?.Instance);
                MmdPlayModeTestInstanceScope.DestroyInstance(serialBinding?.Instance);
                DestroyAsset(modelAsset);
                DestroyAsset(motionAsset);
            }
        }

        [UnityTest]
        public IEnumerator LiveTwoControllersUseIndependentAutomaticWorkerSlots()
        {
            IgnoreIfLivePhysicsUnavailable();
            MmdPmxAsset? modelAsset = null;
            MmdVmdAsset? motionAsset = null;
            var bindings = new MmdUnityPlaybackBinding?[2];
            try
            {
                CreateLiveFixture(
                    out MmdModelDefinition model,
                    out MmdMotionDefinition motion,
                    out modelAsset,
                    out motionAsset,
                    out string modelPath,
                    out string motionPath);
                float[] rates = { 30.0f, 24.0f };
                for (int i = 0; i < bindings.Length; i++)
                {
                    bindings[i] = MmdUnityPlaybackBinding.CreateSkinned(
                        model,
                        motion,
                        modelPath,
                        motionPath,
                        modelPath);
                    MmdUnityPlaybackController controller =
                        bindings[i]!.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                    ConfigureAutomaticController(controller, bindings[i]!, rates[i]);
                    controller.LivePhysicsBodyDiagnosticsSampleInterval = 1;
                }

                yield return null;

                for (int i = 0; i < bindings.Length; i++)
                {
                    MmdUnityPlaybackController controller =
                        bindings[i]!.Instance.Root.GetComponent<MmdUnityPlaybackController>()!;
                    controller.ConfigureModelAsset(modelAsset);
                    controller.ConfigureMotionAsset(motionAsset);
                    controller.SeekFrame(i);
                    controller.Play();
                }

                yield return null;

                Assert.That(MmdStandaloneWorkerScheduler.LastBatchSize, Is.EqualTo(2));
                for (int i = 0; i < bindings.Length; i++)
                {
                    MmdUnityPlaybackController controller =
                        bindings[i]!.Instance.Root.GetComponent<MmdUnityPlaybackController>()!;
                    Assert.That(controller.LastSnapshot, Is.Not.Null, $"controller {i} did not apply a pose");
                    Assert.That(controller.LastLivePhysicsDiagnostics, Is.Not.Null);
                    Assert.That(controller.LastLivePhysicsDiagnostics!.frame, Is.EqualTo(controller.CurrentFrame));
                    Assert.That(controller.LastLivePhysicsDiagnostics.backendName, Is.Not.Empty);
                    Assert.That(controller.FrameRate, Is.EqualTo(rates[i]));
                }
            }
            finally
            {
                for (int i = 0; i < bindings.Length; i++)
                {
                    MmdPlayModeTestInstanceScope.DestroyInstance(bindings[i]?.Instance);
                }

                DestroyAsset(modelAsset);
                DestroyAsset(motionAsset);
            }
        }

        private static string ResolvePackageFixture(string fileName)
        {
            string? projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException("Unity project root could not be resolved from Application.dataPath.");
            }

            return Path.Combine(
                Path.GetFullPath(Path.Combine(projectRoot, "..", "packages", "com.yohawing.mmd-loader")),
                "Tests",
                "Fixtures",
                "Assets",
                fileName);
        }

        private static GameObject CreateParentConstraintConsumer(string name, Transform source)
        {
            var consumer = new GameObject(name);
            ParentConstraint constraint = consumer.AddComponent<ParentConstraint>();
            constraint.AddSource(new ConstraintSource
            {
                sourceTransform = source,
                weight = 1.0f
            });
            constraint.translationAxis = Axis.X | Axis.Y | Axis.Z;
            constraint.rotationAxis = Axis.X | Axis.Y | Axis.Z;
            constraint.constraintActive = true;
            return consumer;
        }

        private static void IgnoreIfLivePhysicsUnavailable()
        {
            MmdPhysicsBackendAvailability availability = MmdAnimPhysicsBackend.ProbeAvailability();
            if (!availability.backendAvailable)
            {
                Assert.Ignore("Bullet physics backend is not available: " + availability.unsupportedReason);
            }
        }

        private static void CreateLiveFixture(
            out MmdModelDefinition model,
            out MmdMotionDefinition motion,
            out MmdPmxAsset modelAsset,
            out MmdVmdAsset motionAsset,
            out string modelPath,
            out string motionPath)
        {
            modelPath = ResolvePackageFixture("test_1bone_cube.pmx");
            motionPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
            byte[] modelBytes = File.ReadAllBytes(modelPath);
            byte[] motionBytes = File.ReadAllBytes(motionPath);
            model = new NativeMmdParser().LoadModel(modelBytes);
            AddPinnedRootRigidbody(model);
            motion = new NativeMmdParser().LoadMotion(motionBytes);
            modelAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            modelAsset.Initialize(modelBytes, Path.GetFileName(modelPath), modelPath);
            motionAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            motionAsset.Initialize(motionBytes, Path.GetFileName(motionPath), motionPath);
        }

        private static void ConfigureAutomaticController(
            MmdUnityPlaybackController controller,
            MmdUnityPlaybackBinding binding,
            float frameRate)
        {
            controller.Configure(binding, frameRate, playOnStart: false);
            controller.SetPhysicsMode(MmdPhysicsMode.Live);
        }

        private static void AddPinnedRootRigidbody(MmdModelDefinition model)
        {
            model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
            {
                index = model.physics.rigidbodies.Count,
                name = "automatic worker pinned root",
                boneIndex = 0,
                boneName = model.bones.Count > 0 ? model.bones[0].name : "root",
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

        private static void DestroyAsset(UnityEngine.Object? asset)
        {
            if (asset != null)
            {
                Object.DestroyImmediate(asset);
            }
        }

        [DefaultExecutionOrder(-1000)]
        private sealed class StandaloneWorkerUpdateObservation : MonoBehaviour
        {
            private MmdUnityPlaybackController? controller;

            internal bool SawConfiguredPoseBeforeControllerUpdate { get; private set; }

            internal int ObservedBatchSize { get; private set; } = -1;

            internal int ObservedFrame { get; private set; } = -1;

            internal void Track(MmdUnityPlaybackController target)
            {
                controller = target ?? throw new ArgumentNullException(nameof(target));
            }

            private void Update()
            {
                if (controller == null || controller.LastSnapshot == null)
                {
                    return;
                }

                SawConfiguredPoseBeforeControllerUpdate = true;
                ObservedBatchSize = MmdStandaloneWorkerScheduler.LastBatchSize;
                ObservedFrame = controller.LastSnapshot.frame.frame;
            }
        }
    }
}
