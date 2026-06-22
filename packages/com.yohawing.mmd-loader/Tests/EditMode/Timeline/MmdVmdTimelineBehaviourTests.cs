#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Timeline;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed class MmdVmdTimelineBehaviourTests
    {
        [Test]
        public void TimelineBehaviourUsesSharedTimePolicyForRepeatAndReverseSeek()
        {
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    CreateMinimalTriangleModel(),
                    CreateRootTranslationMotion(),
                    "timeline-synthetic.pmx",
                    "timeline-synthetic.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 24.0f);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    ModelSourceId = "timeline-synthetic.pmx",
                    MotionSourceId = "timeline-synthetic.vmd",
                    FrameRate = 30.0f,
                    LoopPolicy = MmdVmdTimelineLoopPolicy.None
                };

                MmdPlaybackSnapshot frameTen = behaviour.EvaluateAtLocalTime(controller, 10.25 / 30.0);
                Vector3 frameTenPosition = binding.Instance.BoneTransforms[0].localPosition;
                MmdPlaybackSnapshot repeatedFrameTen = behaviour.EvaluateAtLocalTime(controller, 10.25 / 30.0);
                Vector3 repeatedFrameTenPosition = binding.Instance.BoneTransforms[0].localPosition;
                MmdPlaybackSnapshot frameThree = behaviour.EvaluateAtLocalTime(controller, 3.0 / 30.0);

                Assert.That(behaviour.PhysicsOffByDefault, Is.True);
                Assert.That(behaviour.ModelSourceId, Is.EqualTo("timeline-synthetic.pmx"));
                Assert.That(behaviour.MotionSourceId, Is.EqualTo("timeline-synthetic.vmd"));
                Assert.That(behaviour.LoopPolicy, Is.EqualTo(MmdVmdTimelineLoopPolicy.None));
                Assert.That(frameTen.frame.frame, Is.EqualTo(10));
                Assert.That(repeatedFrameTen.frame.frame, Is.EqualTo(10));
                Assert.That(frameThree.frame.frame, Is.EqualTo(3));
                Assert.That(repeatedFrameTenPosition, Is.EqualTo(frameTenPosition));
                Assert.That(controller.CurrentFrame, Is.EqualTo(3));
                Assert.That(controller.LastSnapshot, Is.Not.Null);
                Assert.That(controller.LastSnapshot!.frame.time, Is.EqualTo(3.0f / 30.0f).Within(0.00001f));
                Assert.That(binding.Instance.BoneTransforms[0].localPosition.x, Is.EqualTo(-0.6f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(binding?.Instance);
            }
        }

        [Test]
        public void TimelinePhysicsOffEvaluationRestoresExternalLiveAfterApplyTime()
        {
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    CreateMinimalTriangleModel(),
                    CreateRootTranslationMotion(),
                    "timeline-live-off-synthetic.pmx",
                    "timeline-live-off-synthetic.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    FrameRate = 30.0f
                };

                MmdPlaybackSnapshot snapshot = behaviour.EvaluateAtLocalTime(controller, 10.0 / 30.0);

                // ApplyTimelineTime temporarily suppresses Live on the binding so ApplyTime succeeds.
                // The controller's serialized physicsMode is never touched.
                Assert.That(snapshot.frame.frame, Is.EqualTo(10));
                // Externally configured Live is preserved after evaluation (not permanently overwritten).
                Assert.That(controller.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Live));
            }
            finally
            {
                DestroyInstance(binding?.Instance);
            }
        }

        [Test]
        public void TimelineEvaluationPreservesSerializedLiveWithoutUserExplicit()
        {
            // Verifies that serialized/Inspector Live stays Live after Timeline evaluation
            // even when SetPhysicsMode(Live) was never called explicitly (non-user-explicit Live).
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    CreateMinimalTriangleModel(),
                    CreateRootTranslationMotion(),
                    "timeline-serialized-live.pmx",
                    "timeline-serialized-live.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f);
                // Do NOT call SetPhysicsMode(Live) explicitly — rely on the serialized default (Live).
                // _userExplicitLive remains false, making this the "non-explicit Live" case.
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    FrameRate = 30.0f
                };

                MmdPlaybackSnapshot snapshot = behaviour.EvaluateAtLocalTime(controller, 10.0 / 30.0);

                // Serialized physicsMode is preserved — still Live.
                Assert.That(controller.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Live));
                Assert.That(snapshot.frame.frame, Is.EqualTo(10));
                // IsUserExplicitLive is false (never explicitly set), but controller is still Live.
                Assert.That(controller.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Live));
            }
            finally
            {
                DestroyInstance(binding?.Instance);
            }
        }

        [Test]
        public void TimelineRepeatedEvaluationWhileLiveDoesNotResetFrameZero()
        {
            // Verifies that repeated Timeline evaluations while Live do not leave
            // controller.LastSnapshot/CurrentFrame at frame 0 (the ApplyPhysicsModeToBinding side effect).
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    CreateMinimalTriangleModel(),
                    CreateRootTranslationMotion(),
                    "timeline-repeat-live.pmx",
                    "timeline-repeat-live.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f);
                controller.SetPhysicsMode(MmdPhysicsMode.Live);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    FrameRate = 30.0f
                };

                // First evaluation at frame 10.
                MmdPlaybackSnapshot first = behaviour.EvaluateAtLocalTime(controller, 10.0 / 30.0);
                Assert.That(first.frame.frame, Is.EqualTo(10));
                Assert.That(controller.CurrentFrame, Is.EqualTo(10));
                Assert.That(controller.LastSnapshot, Is.SameAs(first));

                // Second evaluation at frame 5 (reverse seek).
                MmdPlaybackSnapshot second = behaviour.EvaluateAtLocalTime(controller, 5.0 / 30.0);
                Assert.That(second.frame.frame, Is.EqualTo(5));
                // CurrentFrame reflects the latest evaluation, NOT frame 0 from a Live restore.
                Assert.That(controller.CurrentFrame, Is.EqualTo(5));
                Assert.That(controller.LastSnapshot, Is.SameAs(second));

                // Third evaluation at frame 3.
                MmdPlaybackSnapshot third = behaviour.EvaluateAtLocalTime(controller, 3.0 / 30.0);
                Assert.That(third.frame.frame, Is.EqualTo(3));
                Assert.That(controller.CurrentFrame, Is.EqualTo(3));
                Assert.That(controller.LastSnapshot, Is.SameAs(third));

                // Controller physicsMode remains Live throughout.
                Assert.That(controller.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Live));
            }
            finally
            {
                DestroyInstance(binding?.Instance);
            }
        }

        [Test]
        public void TimelineEvaluationConfiguresRuntimeImporterPlaybackBeforeApplyTime()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");
                string vmdPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                var parser = new NativeMmdParser();
                MmdModelDefinition model = parser.LoadModel(File.ReadAllBytes(pmxPath));
                instance = MmdUnityModelFactory.CreateSkinnedModel(model, pmxPath);
                MmdUnityPlaybackController controller = instance.Root.AddComponent<MmdUnityPlaybackController>();
                MmdRuntimeImporterComponent importer = instance.Root.AddComponent<MmdRuntimeImporterComponent>();
                importer.ConfigurePaths(
                    pmxPath,
                    vmdPath,
                    playbackFrameRate: 30.0f,
                    startFrame: 0,
                    shouldPlayOnStart: false);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    FrameRate = 30.0f
                };

                int siblingCountBefore = instance.Root.transform.parent == null
                    ? instance.Root.scene.rootCount
                    : instance.Root.transform.parent.childCount;

                int revisionBeforeFirst = controller.ConfigurationRevision;
                MmdPlaybackSnapshot snapshot = behaviour.EvaluateAtLocalTime(controller, 10.0 / 30.0);
                int firstFrame = snapshot.frame.frame;
                int revisionAfterFirst = controller.ConfigurationRevision;
                behaviour.EvaluateAtLocalTime(controller, 11.0 / 30.0);

                Assert.That(controller.IsConfigured, Is.True);
                Assert.That(importer.LastConfigurationStatus, Is.EqualTo("configured"));
                Assert.That(controller.ConfiguredInstanceRoot, Is.SameAs(instance.Root));
                Assert.That(firstFrame, Is.EqualTo(10));
                Assert.That(revisionBeforeFirst, Is.EqualTo(0));
                Assert.That(revisionAfterFirst - revisionBeforeFirst, Is.EqualTo(1));
                Assert.That(controller.ConfigurationRevision, Is.EqualTo(revisionAfterFirst));
                Assert.That(controller.CurrentFrame, Is.EqualTo(11));
                Assert.That(controller.LastSnapshot, Is.Not.Null);
                Assert.That(controller.LastSnapshot!.frame.frame, Is.EqualTo(11));

                Transform parent = instance.Root.transform.parent;
                if (parent != null)
                {
                    int siblingCountAfter = parent.childCount;
                    Transform runtimeChild = parent.Find(instance.Root.name + " Runtime");
                    Assert.That(runtimeChild, Is.Null);
                    Assert.That(siblingCountAfter, Is.EqualTo(siblingCountBefore));
                }
                else
                {
                    int siblingCountAfter = instance.Root.scene.rootCount;
                    Assert.That(siblingCountAfter, Is.EqualTo(siblingCountBefore));
                }
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void TimelineEvaluationUsesFastRuntimeApplyTimeWhenActive()
        {
            MmdPmxAsset? pmxAsset = null;
            MmdVmdAsset? vmdAsset = null;
            MmdUnityPlaybackController? controller = null;
            try
            {
                const float frameRate = 30.0f;
                double localTime = 10.25 / frameRate;
                string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");
                string vmdPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                pmxAsset.Initialize(File.ReadAllBytes(pmxPath), "test_1bone_cube.pmx", pmxPath, assetImportScale: 1.0f);
                vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
                vmdAsset.Initialize(File.ReadAllBytes(vmdPath), "test_1bone_cube_motion.vmd", vmdPath);
                var holder = new GameObject("timeline-fast-runtime-apply-time");
                controller = holder.AddComponent<MmdUnityPlaybackController>();
                controller.ConfigureFromAssets(pmxAsset, vmdAsset, frameRate, startFrame: 0);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    FrameRate = frameRate,
                    LoopPolicy = MmdVmdTimelineLoopPolicy.None
                };

                MmdPlaybackSnapshot snapshot = behaviour.EvaluateAtLocalTime(controller, localTime);

                Assert.That(snapshot.frame.frame, Is.EqualTo(10));
                Assert.That(snapshot.frame.time, Is.EqualTo((float)localTime).Within(0.00001f));
                Assert.That(controller.CurrentFrame, Is.EqualTo(10));
                Assert.That(controller.LastSnapshot, Is.SameAs(snapshot));
                if (controller.IsFastRuntimeEnabled)
                {
                    Assert.That(controller.LastFastRuntimeReason, Is.Empty);
                    Assert.That(snapshot.frame.bones, Is.Empty);
                }
                else
                {
                    Assert.That(controller.LastFastRuntimeReason, Is.Not.Empty);
                    Assert.That(snapshot.frame.bones, Is.Not.Empty);
                }
            }
            finally
            {
                if (controller != null)
                {
                    Object.DestroyImmediate(controller.gameObject);
                }

                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
            }
        }

        [Test]
        public void TimelineFirstEvaluateRebindsRawPmxPathOnceAndSteadyStateDoesNotRebind()
        {
            MmdVmdAsset? vmdAsset = null;
            MmdUnityModelInstance? instance = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");
                string vmdPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                var parser = new NativeMmdParser();
                MmdModelDefinition model = parser.LoadModel(File.ReadAllBytes(pmxPath));
                instance = MmdUnityModelFactory.CreateSkinnedModel(model, pmxPath);
                MmdUnityPlaybackController controller = instance.Root.AddComponent<MmdUnityPlaybackController>();
                MmdRuntimeImporterComponent importer = instance.Root.AddComponent<MmdRuntimeImporterComponent>();
                importer.ConfigureModelPath(pmxPath);
                vmdAsset = CreateVmdAsset(vmdPath);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    MotionAsset = vmdAsset,
                    FrameRate = 30.0f
                };

                int revisionBeforeFirst = controller.ConfigurationRevision;
                MmdPlaybackSnapshot first = behaviour.EvaluateAtLocalTime(controller, 10.0 / 30.0);
                int firstFrame = first.frame.frame;
                int revisionAfterFirst = controller.ConfigurationRevision;
                for (int frame = 11; frame <= 13; frame++)
                {
                    behaviour.EvaluateAtLocalTime(controller, frame / 30.0);
                }

                Assert.That(firstFrame, Is.EqualTo(10));
                Assert.That(revisionBeforeFirst, Is.EqualTo(0));
                Assert.That(revisionAfterFirst - revisionBeforeFirst, Is.EqualTo(1));
                Assert.That(controller.ConfigurationRevision, Is.EqualTo(revisionAfterFirst));
                Assert.That(controller.IsConfigured, Is.True);
                Assert.That(controller.ConfiguredInstanceRoot, Is.SameAs(instance.Root));
                Assert.That(controller.ModelSourceId, Is.EqualTo(Path.GetFullPath(pmxPath)));
            }
            finally
            {
                DestroyInstance(instance);
                Object.DestroyImmediate(vmdAsset);
            }
        }

        [Test]
        public void TimelineFirstEvaluateRebindsPmxAssetOnceAndSteadyStateDoesNotRebind()
        {
            MmdPmxAsset? pmxAsset = null;
            MmdVmdAsset? vmdAsset = null;
            MmdUnityModelInstance? instance = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");
                string vmdPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                pmxAsset = CreatePmxAsset(pmxPath);
                vmdAsset = CreateVmdAsset(vmdPath);
                var parser = new NativeMmdParser();
                MmdModelDefinition model = parser.LoadModel(File.ReadAllBytes(pmxPath));
                instance = MmdUnityModelFactory.CreateSkinnedModel(model, pmxPath);
                MmdUnityPlaybackController controller = instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.ConfigureModelAsset(pmxAsset);
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    MotionAsset = vmdAsset,
                    FrameRate = 30.0f
                };

                int revisionBeforeFirst = controller.ConfigurationRevision;
                MmdPlaybackSnapshot first = behaviour.EvaluateAtLocalTime(controller, 10.0 / 30.0);
                int firstFrame = first.frame.frame;
                int revisionAfterFirst = controller.ConfigurationRevision;
                for (int frame = 11; frame <= 13; frame++)
                {
                    behaviour.EvaluateAtLocalTime(controller, frame / 30.0);
                }

                Assert.That(firstFrame, Is.EqualTo(10));
                Assert.That(revisionBeforeFirst, Is.EqualTo(0));
                Assert.That(revisionAfterFirst - revisionBeforeFirst, Is.EqualTo(1));
                Assert.That(controller.ConfigurationRevision, Is.EqualTo(revisionAfterFirst));
                Assert.That(controller.IsConfigured, Is.True);
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
                Assert.That(controller.ConfiguredInstanceRoot, Is.SameAs(instance.Root));
            }
            finally
            {
                DestroyInstance(instance);
                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
            }
        }

        [Test]
        public void TimelinePlaybackSourceEvaluationRebindsProviderAssetsWithoutCreatingRuntimeFallback()
        {
            MmdPmxAsset? pmxAsset = null;
            MmdVmdAsset? vmdAsset = null;
            MmdUnityModelInstance? instance = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");
                string vmdPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                pmxAsset = CreatePmxAsset(pmxPath);
                vmdAsset = CreateVmdAsset(vmdPath);
                var parser = new NativeMmdParser();
                MmdModelDefinition model = parser.LoadModel(File.ReadAllBytes(pmxPath));
                instance = MmdUnityModelFactory.CreateSkinnedModel(model, pmxPath);
                MmdUnityPlaybackController controller = instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.ConfigureModelAsset(pmxAsset);
                controller.ConfigureMotionAsset(vmdAsset);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    FrameRate = 30.0f
                };

                int rootCountBefore = instance.Root.scene.rootCount;
                int revisionBeforeFirst = controller.ConfigurationRevision;
                MmdPlaybackSnapshot first = behaviour.EvaluateAtLocalTime(controller, 10.0 / 30.0);
                int revisionAfterFirst = controller.ConfigurationRevision;

                Assert.That(first.frame.frame, Is.EqualTo(10));
                Assert.That(revisionBeforeFirst, Is.EqualTo(0));
                Assert.That(revisionAfterFirst - revisionBeforeFirst, Is.EqualTo(1));
                Assert.That(controller.IsConfigured, Is.True);
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
                Assert.That(controller.ConfiguredInstanceRoot, Is.SameAs(instance.Root));
                Assert.That(instance.Root.scene.rootCount, Is.EqualTo(rootCountBefore));
            }
            finally
            {
                DestroyInstance(instance);
                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
            }
        }

        [Test]
        public void TimelineEvaluationReportsUnconfiguredTargetWithoutSerializedSource()
        {
            GameObject? targetObject = null;
            try
            {
                targetObject = new GameObject("timeline-unconfigured-target");
                MmdUnityPlaybackController controller = targetObject.AddComponent<MmdUnityPlaybackController>();
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    FrameRate = 30.0f
                };

                Assert.That(
                    () => behaviour.EvaluateAtLocalTime(controller, 0.0),
                    Throws.InvalidOperationException.With.Message.Contains("not configured and has no provider-owned PMX/VMD source"));
            }
            finally
            {
                if (targetObject != null)
                {
                    Object.DestroyImmediate(targetObject);
                }
            }
        }

        [Test]
        public void PlayableDirectorTimelineScrubAppliesControllerPose()
        {
            MmdUnityPlaybackBinding? binding = null;
            GameObject? directorObject = null;
            TimelineAsset? timelineAsset = null;
            int processFrameCallbackCount = 0;
            void OnProcessFrame(double _)
            {
                processFrameCallbackCount++;
            }

            try
            {
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    CreateMinimalTriangleModel(),
                    CreateRootTranslationMotion(),
                    "timeline-director-synthetic.pmx",
                    "timeline-director-synthetic.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f);

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack track = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD");
                TimelineClip clip = track.CreateClip<MmdVmdTimelineClip>();
                clip.start = 0.0;
                clip.duration = 1.0;
                var mmdClip = (MmdVmdTimelineClip)clip.asset;
                mmdClip.FrameRate = 30.0f;
                mmdClip.MotionSourceId = "timeline-director-synthetic.vmd";

                directorObject = new GameObject("timeline-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timelineAsset;
                director.SetGenericBinding(track, controller);

                MmdVmdTimelineBehaviour.ProcessFrameEvaluated += OnProcessFrame;
                director.time = 10.25 / 30.0;
                director.Evaluate();
                Vector3 frameTenPosition = binding.Instance.BoneTransforms[0].localPosition;

                director.time = 3.0 / 30.0;
                director.Evaluate();

                Assert.That(controller.CurrentFrame, Is.EqualTo(3));
                Assert.That(frameTenPosition.x, Is.EqualTo(-2.0f).Within(0.00001f));
                Assert.That(binding.Instance.BoneTransforms[0].localPosition.x, Is.EqualTo(-0.6f).Within(0.00001f));
                Assert.That(controller.LastSnapshot, Is.Not.Null);
                Assert.That(controller.LastSnapshot!.frame.time, Is.EqualTo(3.0f / 30.0f).Within(0.00001f));
                Assert.That(processFrameCallbackCount, Is.EqualTo(2));
                // Outside Play Mode (Application.isPlaying == false), a real PlayableDirector evaluation
                // routes through animation-only ApplyTimelineTime and must NOT step Live physics.
                Assert.That(controller.LastLivePhysicsDiagnostics, Is.Null,
                    "EditMode Timeline evaluation must not start Live physics (Play Mode boundary gate)");
            }
            finally
            {
                MmdVmdTimelineBehaviour.ProcessFrameEvaluated -= OnProcessFrame;
                if (directorObject != null)
                {
                    Object.DestroyImmediate(directorObject);
                }

                if (timelineAsset != null)
                {
                    Object.DestroyImmediate(timelineAsset);
                }

                DestroyInstance(binding?.Instance);
            }
        }

        [Test]
        public void PlayableDirectorTimelineEvaluateAppliesEditableRigBoundary()
        {
            MmdUnityPlaybackBinding? binding = null;
            GameObject? directorObject = null;
            TimelineAsset? timelineAsset = null;
            try
            {
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    CreateMinimalTriangleModel(),
                    CreateRootTranslationMotion(),
                    "timeline-editable-rig-synthetic.pmx",
                    "timeline-editable-rig-synthetic.vmd");
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f);
                var layer = binding.Instance.Root.AddComponent<MmdEditableRigLayer>();
                layer.AddBoneCorrection(
                    binding.Instance.BoneTransforms[0].name,
                    0,
                    new Vector3(0.0f, 0.5f, 0.0f),
                    Quaternion.identity,
                    Vector3.zero);

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack track = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD");
                TimelineClip clip = track.CreateClip<MmdVmdTimelineClip>();
                clip.start = 0.0;
                clip.duration = 1.0;
                var mmdClip = (MmdVmdTimelineClip)clip.asset;
                mmdClip.FrameRate = 30.0f;
                mmdClip.MotionSourceId = "timeline-editable-rig-synthetic.vmd";

                directorObject = new GameObject("timeline-editable-rig-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timelineAsset;
                director.SetGenericBinding(track, controller);

                layer.EditableRigEnabled = false;
                director.time = 10.25 / 30.0;
                director.Evaluate();
                Assert.That(binding.Instance.BoneTransforms[0].localPosition, Is.EqualTo(new Vector3(-2.0f, 0.0f, 0.0f)));
                Assert.That(controller.LastEditableRigDiagnostics, Is.Not.Null);
                Assert.That(controller.LastEditableRigDiagnostics!.transformState, Is.EqualTo("native-only"));

                layer.EditableRigEnabled = true;
                layer.LayerWeight = 1.0f;
                director.Evaluate();
                Vector3 enabledPosition = binding.Instance.BoneTransforms[0].localPosition;
                Assert.That(enabledPosition, Is.EqualTo(new Vector3(-2.0f, 0.5f, 0.0f)));
                Assert.That(controller.LastEditableRigDiagnostics, Is.Not.Null);
                Assert.That(controller.LastEditableRigDiagnostics!.executionStage, Is.EqualTo("post-native-apply-time"));
                Assert.That(controller.LastEditableRigDiagnostics.transformState, Is.EqualTo("post-editable-rig"));
                Assert.That(controller.LastEditableRigDiagnostics.correctedBoneCount, Is.EqualTo(1));

                director.Evaluate();
                Assert.That(binding.Instance.BoneTransforms[0].localPosition, Is.EqualTo(enabledPosition));

                layer.LayerWeight = 0.0f;
                director.Evaluate();
                Assert.That(binding.Instance.BoneTransforms[0].localPosition, Is.EqualTo(new Vector3(-2.0f, 0.0f, 0.0f)));
                Assert.That(controller.LastEditableRigDiagnostics, Is.Not.Null);
                Assert.That(controller.LastEditableRigDiagnostics!.transformState, Is.EqualTo("native-only"));
                Assert.That(controller.LastEditableRigDiagnostics.noOpReason, Is.EqualTo("zero-weight"));

                layer.LayerWeight = 1.0f;
                director.Evaluate();
                Assert.That(binding.Instance.BoneTransforms[0].localPosition, Is.EqualTo(enabledPosition));
                track.muted = true;
                director.RebuildGraph();
                director.Evaluate();
                Assert.That(binding.Instance.BoneTransforms[0].localPosition, Is.EqualTo(enabledPosition));
                Assert.That(controller.LastEditableRigDiagnostics, Is.Not.Null);
                Assert.That(controller.LastEditableRigDiagnostics!.transformState, Is.EqualTo("post-editable-rig"));

                Assert.That(controller.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Live));
            }
            finally
            {
                if (directorObject != null)
                {
                    Object.DestroyImmediate(directorObject);
                }

                if (timelineAsset != null)
                {
                    Object.DestroyImmediate(timelineAsset);
                }

                DestroyInstance(binding?.Instance);
            }
        }

        [Test]
        public void TimelineBehaviourRejectsInvalidLocalTimeAndFrameRate()
        {
            var behaviour = new MmdVmdTimelineBehaviour
            {
                FrameRate = 30.0f
            };
            GameObject? targetObject = null;
            try
            {
                targetObject = new GameObject("timeline-validation-target");
                MmdUnityPlaybackController controller = targetObject.AddComponent<MmdUnityPlaybackController>();

                Assert.That(
                    () => behaviour.EvaluateAtLocalTime(controller, -0.1),
                    Throws.TypeOf<System.ArgumentOutOfRangeException>().With.Property("ParamName").EqualTo("localTime"));

                behaviour.FrameRate = 0.0f;
                Assert.That(
                    () => behaviour.EvaluateAtLocalTime(controller, 0.0),
                    Throws.TypeOf<System.ArgumentOutOfRangeException>().With.Property("ParamName").EqualTo("frameRate"));
            }
            finally
            {
                if (targetObject != null)
                {
                    Object.DestroyImmediate(targetObject);
                }
            }
        }

        [Test]
        public void TimelineClipTransfersMotionAssetToBehaviour()
        {
            MmdVmdAsset motionAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            motionAsset.Initialize(new byte[] { 1, 2, 3 }, "Assets/Motion/test.vmd", "External/Motion/test.vmd");
            var clip = new MmdVmdTimelineClip
            {
                MotionAsset = motionAsset,
                FrameRate = 30.0f
            };
            PlayableGraph graph = PlayableGraph.Create("mmd-vmd-timeline-clip-test");
            GameObject? owner = null;
            try
            {
                owner = new GameObject("timeline-clip-owner");
                Playable playable = clip.CreatePlayable(graph, owner);
                var scriptPlayable = (ScriptPlayable<MmdVmdTimelineBehaviour>)playable;
                MmdVmdTimelineBehaviour behaviour = scriptPlayable.GetBehaviour();

                Assert.That(behaviour.MotionAsset, Is.SameAs(motionAsset));
                Assert.That(behaviour.MotionSourceId, Is.EqualTo("Assets/Motion/test.vmd"));
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                if (owner != null)
                {
                    Object.DestroyImmediate(owner);
                }

                Object.DestroyImmediate(motionAsset);
            }
        }

        [Test]
        public void TimelineEvaluationThrowsWhenNoExistingSceneModelWithoutCreatingFallbackRoot()
        {
            // Verifies that Timeline evaluation with controller PMX/Motion sources
            // but no bindable existing scene model throws InvalidOperationException mentioning
            // Timeline/existing scene model, without creating a '<controller> Runtime' fallback root.
            MmdPmxAsset? pmxAsset = null;
            MmdVmdAsset? vmdAsset = null;
            GameObject? holder = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");
                string vmdPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                pmxAsset = CreatePmxAsset(pmxPath);
                vmdAsset = CreateVmdAsset(vmdPath);
                holder = new GameObject("timeline-no-scene-model");
                MmdUnityPlaybackController controller = holder.AddComponent<MmdUnityPlaybackController>();
                controller.ConfigureModelAsset(pmxAsset);
                controller.ConfigureMotionAsset(vmdAsset);
                // No SkinnedMeshRenderer on holder or children — TryCreateExistingSceneBinding will fail.
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    FrameRate = 30.0f
                };

                int siblingCountBefore = holder.transform.parent == null
                    ? holder.scene.rootCount
                    : holder.transform.parent.childCount;

                var ex = Assert.Throws<InvalidOperationException>(() =>
                    behaviour.EvaluateAtLocalTime(controller, 10.0 / 30.0));
                Assert.That(ex.Message, Does.Contain("Timeline").Or.Contain("timeline").Or.Contain("existing scene").Or.Contain("SkinnedMeshRenderer"));

                // Verify no '<controller> Runtime' fallback root was created.
                Assert.That(controller.IsConfigured, Is.False);
                // The holder should not have spawned a new child with " Runtime" suffix.
                Transform parentTransform = holder.transform.parent;
                if (parentTransform != null)
                {
                    Transform runtimeChild = parentTransform.Find(holder.name + " Runtime");
                    Assert.That(runtimeChild, Is.Null);

                    int siblingCountAfter = parentTransform.childCount;
                    Assert.That(siblingCountAfter, Is.EqualTo(siblingCountBefore));
                }
                else
                {
                    // No parent; check scene root count.
                    Assert.That(holder.scene.rootCount, Is.EqualTo(siblingCountBefore));
                }
            }
            finally
            {
                if (holder != null)
                {
                    Object.DestroyImmediate(holder);
                }

                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
            }
        }

        [Test]
        public void TimelineEvaluationThrowsWithoutCreatingFallbackRootForRuntimeImporterPath()
        {
            // Verifies that Timeline evaluation via runtime importer model/motion path pair with no scene
            // SkinnedMeshRenderer throws InvalidOperationException instead of creating a fallback root.
            MmdVmdAsset? vmdAsset = null;
            GameObject? holder = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");
                string vmdPath = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                vmdAsset = CreateVmdAsset(vmdPath);
                holder = new GameObject("timeline-no-scene-importer");
                MmdUnityPlaybackController controller = holder.AddComponent<MmdUnityPlaybackController>();
                MmdRuntimeImporterComponent importer = holder.AddComponent<MmdRuntimeImporterComponent>();
                importer.ConfigurePaths(
                    pmxPath,
                    vmdPath,
                    playbackFrameRate: 30.0f,
                    startFrame: 0,
                    shouldPlayOnStart: false);
                // No SkinnedMeshRenderer on holder/children — TryCreateExistingSceneBinding
                // via MmdUnityModelFactory.CreateExistingSkinnedModelInstance will throw.
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    FrameRate = 30.0f
                };

                int siblingCountBefore = holder.transform.parent == null
                    ? holder.scene.rootCount
                    : holder.transform.parent.childCount;

                var ex = Assert.Throws<InvalidOperationException>(() =>
                    behaviour.EvaluateAtLocalTime(controller, 10.0 / 30.0));
                Assert.That(ex.Message, Does.Contain("Timeline").Or.Contain("timeline").Or.Contain("existing scene").Or.Contain("SkinnedMeshRenderer"));

                Assert.That(controller.IsConfigured, Is.False);
                Transform parentTransform = holder.transform.parent;
                if (parentTransform != null)
                {
                    Transform runtimeChild = parentTransform.Find(holder.name + " Runtime");
                    Assert.That(runtimeChild, Is.Null);

                    int siblingCountAfter = parentTransform.childCount;
                    Assert.That(siblingCountAfter, Is.EqualTo(siblingCountBefore));
                }
                else
                {
                    Assert.That(holder.scene.rootCount, Is.EqualTo(siblingCountBefore));
                }
            }
            finally
            {
                if (holder != null)
                {
                    Object.DestroyImmediate(holder);
                }

                Object.DestroyImmediate(vmdAsset);
            }
        }

        private static MmdModelDefinition CreateMinimalTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "minimal-timeline-triangle"
            };
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                transformOrder = 0,
                origin = new[] { 0.0f, 0.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "triangle-material",
                vertexCount = 3
            });
            return model;
        }

        private static MmdMotionDefinition CreateRootTranslationMotion()
        {
            var motion = new MmdMotionDefinition
            {
                targetModelName = "minimal-timeline-triangle",
                maxFrame = 10
            };
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = 0,
                translation = new[] { 0.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = LinearInterpolation()
            });
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = 10,
                translation = new[] { 2.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = LinearInterpolation()
            });
            return motion;
        }

        private static MmdBoneInterpolationDefinition LinearInterpolation()
        {
            byte[] linear = { 20, 20, 107, 107 };
            return new MmdBoneInterpolationDefinition
            {
                translationX = linear,
                translationY = linear,
                translationZ = linear,
                rotation = linear
            };
        }

        private static MmdVertexDefinition CreateVertex(
            int index,
            float x,
            float y,
            float z,
            float u,
            float v)
        {
            return new MmdVertexDefinition
            {
                index = index,
                position = new[] { x, y, z },
                normal = new[] { 0.0f, 0.0f, 1.0f },
                uv = new[] { u, v },
                boneIndices = new[] { 0 },
                boneWeights = new[] { 1.0f }
            };
        }

        private static MmdPmxAsset CreatePmxAsset(string pmxPath)
        {
            var asset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            asset.Initialize(File.ReadAllBytes(pmxPath), Path.GetFileName(pmxPath), pmxPath, assetImportScale: 1.0f);
            return asset;
        }

        private static MmdVmdAsset CreateVmdAsset(string vmdPath)
        {
            var asset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            asset.Initialize(File.ReadAllBytes(vmdPath), Path.GetFileName(vmdPath), vmdPath);
            return asset;
        }

        private static void DestroyInstance(MmdUnityModelInstance? instance)
        {
            if (instance == null)
            {
                return;
            }

            var roots = new List<Object>();
            if (instance.Root != null)
            {
                roots.Add(instance.Root);
            }

            if (instance.Mesh != null)
            {
                roots.Add(instance.Mesh);
            }

            if (instance.Materials != null)
            {
                roots.AddRange(instance.Materials.Where(material => material != null).Distinct());
            }

            roots.AddRange(instance.OwnedTextures.Where(texture => texture != null).Distinct());
            foreach (Object item in roots)
            {
                Object.DestroyImmediate(item);
            }
        }

        private static string ResolvePackageFixture(string fileName)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string packageRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "packages", "com.yohawing.mmd-loader"));
            return Path.Combine(packageRoot, "Tests", "Fixtures", "Assets", fileName);
        }
    }
}
