#nullable enable

using System;
using System.IO;
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
        private const string PlaybackPmxId = "test_1bone_cube.pmx";
        private const string PlaybackVmdId = "test_1bone_cube_motion.vmd";

        [Test]
        public void TimelineClipDoesNotAdvertiseBlendCaps()
        {
            var clip = new MmdVmdTimelineClip();

            Assert.That(clip.clipCaps, Is.EqualTo(ClipCaps.None));
        }

        [Test]
        public void TrackCreatesSingleWinnerMixerPlayable()
        {
            PlayableGraph graph = PlayableGraph.Create("mmd-vmd-track-mixer-test");
            MmdVmdTimelineTrack? track = null;
            try
            {
                track = ScriptableObject.CreateInstance<MmdVmdTimelineTrack>();
                Playable mixerPlayable = track.CreateTrackMixer(graph, go: null!, inputCount: 2);

                Assert.That(mixerPlayable.IsValid(), Is.True);
                Assert.That(mixerPlayable.GetPlayableType(), Is.EqualTo(typeof(MmdVmdTimelineMixerBehaviour)));
                Assert.That(mixerPlayable.GetInputCount(), Is.EqualTo(2));
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                if (track != null)
                {
                    Object.DestroyImmediate(track);
                }
            }
        }

        [Test]
        public void ProcessFrameWithZeroEffectiveWeightDoesNotApplyPose()
        {
            MmdUnityPlaybackBinding? binding = null;
            int processFrameCallbackCount = 0;
            void OnProcessFrame(double _)
            {
                processFrameCallbackCount++;
            }

            try
            {
                binding = CreatePlaybackBinding();
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    FrameRate = 30.0f
                };
                MmdPlaybackSnapshot frameNine = behaviour.EvaluateAtLocalTime(controller, 9.25 / 30.0);
                Quaternion beforeRotation = binding.Instance.BoneTransforms[0].localRotation;

                MmdVmdTimelineBehaviour.ProcessFrameEvaluated += OnProcessFrame;
                behaviour.ProcessFrame(default, default, controller);

                Assert.That(frameNine.frame.frame, Is.EqualTo(9));
                Assert.That(controller.CurrentFrame, Is.EqualTo(9));
                Assert.That(controller.LastSnapshot, Is.SameAs(frameNine));
                Assert.That(Quaternion.Angle(binding.Instance.BoneTransforms[0].localRotation, beforeRotation), Is.LessThan(0.001f));
                Assert.That(processFrameCallbackCount, Is.EqualTo(0));
            }
            finally
            {
                MmdVmdTimelineBehaviour.ProcessFrameEvaluated -= OnProcessFrame;
                MmdTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [Test]
        public void TimelineBehaviourUsesSharedTimePolicyForRepeatAndReverseSeek()
        {
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = CreatePlaybackBinding();
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 24.0f);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    ModelSourceId = PlaybackPmxId,
                    MotionSourceId = PlaybackVmdId,
                    FrameRate = 30.0f,
                    LoopPolicy = MmdVmdTimelineLoopPolicy.None
                };

                MmdPlaybackSnapshot frameNine = behaviour.EvaluateAtLocalTime(controller, 9.25 / 30.0);
                Quaternion frameNineRotation = binding.Instance.BoneTransforms[0].localRotation;
                MmdPlaybackSnapshot repeatedFrameNine = behaviour.EvaluateAtLocalTime(controller, 9.25 / 30.0);
                Quaternion repeatedFrameNineRotation = binding.Instance.BoneTransforms[0].localRotation;
                MmdPlaybackSnapshot frameZero = behaviour.EvaluateAtLocalTime(controller, 0.0);

                Assert.That(behaviour.PhysicsOffByDefault, Is.True);
                Assert.That(behaviour.ModelSourceId, Is.EqualTo(PlaybackPmxId));
                Assert.That(behaviour.MotionSourceId, Is.EqualTo(PlaybackVmdId));
                Assert.That(behaviour.LoopPolicy, Is.EqualTo(MmdVmdTimelineLoopPolicy.None));
                Assert.That(frameNine.frame.frame, Is.EqualTo(9));
                Assert.That(repeatedFrameNine.frame.frame, Is.EqualTo(9));
                Assert.That(frameZero.frame.frame, Is.EqualTo(0));
                Assert.That(Quaternion.Angle(repeatedFrameNineRotation, frameNineRotation), Is.LessThan(0.001f));
                Assert.That(controller.CurrentFrame, Is.EqualTo(0));
                Assert.That(controller.LastSnapshot, Is.Not.Null);
                Assert.That(controller.LastSnapshot!.frame.time, Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(Quaternion.Angle(frameNineRotation, ExpectedFrameNineUnityRotation(binding)), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(binding.Instance.BoneTransforms[0].localRotation, binding.Instance.BindLocalRotations[0]), Is.LessThan(0.001f));
            }
            finally
            {
                MmdTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [Test]
        public void TimelinePhysicsOffEvaluationRestoresExternalLiveAfterApplyTime()
        {
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = CreatePlaybackBinding();
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
                MmdTestInstanceScope.DestroyInstance(binding?.Instance);
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
                binding = CreatePlaybackBinding();
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
                MmdTestInstanceScope.DestroyInstance(binding?.Instance);
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
                binding = CreatePlaybackBinding();
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
                MmdTestInstanceScope.DestroyInstance(binding?.Instance);
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

                Transform? parent = instance.Root.transform.parent;
                if (parent != null)
                {
                    int siblingCountAfter = parent.childCount;
                    Transform? runtimeChild = parent.Find(instance.Root.name + " Runtime");
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
                MmdTestInstanceScope.DestroyInstance(instance);
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
                MmdTestInstanceScope.DestroyInstance(instance);
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
                MmdTestInstanceScope.DestroyInstance(instance);
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
                MmdTestInstanceScope.DestroyInstance(instance);
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
                binding = CreatePlaybackBinding();
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f);

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack track = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD");
                TimelineClip clip = track.CreateClip<MmdVmdTimelineClip>();
                clip.start = 0.0;
                clip.duration = 1.0;
                var mmdClip = (MmdVmdTimelineClip)clip.asset;
                mmdClip.FrameRate = 30.0f;
                mmdClip.MotionSourceId = PlaybackVmdId;

                directorObject = new GameObject("timeline-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timelineAsset;
                director.SetGenericBinding(track, controller);

                MmdVmdTimelineBehaviour.ProcessFrameEvaluated += OnProcessFrame;
                director.time = 9.25 / 30.0;
                director.Evaluate();
                Quaternion frameNineRotation = binding.Instance.BoneTransforms[0].localRotation;

                director.time = 0.0;
                director.Evaluate();

                Assert.That(controller.CurrentFrame, Is.EqualTo(0));
                Assert.That(Quaternion.Angle(frameNineRotation, ExpectedFrameNineUnityRotation(binding)), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(binding.Instance.BoneTransforms[0].localRotation, binding.Instance.BindLocalRotations[0]), Is.LessThan(0.001f));
                Assert.That(controller.LastSnapshot, Is.Not.Null);
                Assert.That(controller.LastSnapshot!.frame.time, Is.EqualTo(0.0f).Within(0.00001f));
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

                MmdTestInstanceScope.DestroyInstance(binding?.Instance);
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
                binding = CreatePlaybackBinding();
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
                mmdClip.MotionSourceId = PlaybackVmdId;

                directorObject = new GameObject("timeline-editable-rig-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timelineAsset;
                director.SetGenericBinding(track, controller);

                layer.EditableRigEnabled = false;
                director.time = 9.25 / 30.0;
                director.Evaluate();
                Assert.That(binding.Instance.BoneTransforms[0].localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(Quaternion.Angle(binding.Instance.BoneTransforms[0].localRotation, ExpectedFrameNineUnityRotation(binding)), Is.LessThan(0.001f));
                Assert.That(controller.LastEditableRigDiagnostics, Is.Not.Null);
                Assert.That(controller.LastEditableRigDiagnostics!.transformState, Is.EqualTo("native-only"));

                layer.EditableRigEnabled = true;
                layer.LayerWeight = 1.0f;
                director.Evaluate();
                Vector3 enabledPosition = binding.Instance.BoneTransforms[0].localPosition;
                Assert.That(enabledPosition, Is.EqualTo(new Vector3(0.0f, 0.5f, 0.0f)));
                Assert.That(controller.LastEditableRigDiagnostics, Is.Not.Null);
                Assert.That(controller.LastEditableRigDiagnostics!.executionStage, Is.EqualTo("post-native-apply-time"));
                Assert.That(controller.LastEditableRigDiagnostics.transformState, Is.EqualTo("post-editable-rig"));
                Assert.That(controller.LastEditableRigDiagnostics.correctedBoneCount, Is.EqualTo(1));

                director.Evaluate();
                Assert.That(binding.Instance.BoneTransforms[0].localPosition, Is.EqualTo(enabledPosition));

                layer.LayerWeight = 0.0f;
                director.Evaluate();
                Assert.That(binding.Instance.BoneTransforms[0].localPosition, Is.EqualTo(Vector3.zero));
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

                MmdTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [Test]
        public void OverlappingTrackClipsApplySingleWinnerOncePerEvaluate()
        {
            // Equal positive weights (ClipCaps.None overlap) resolve to the later input index.
            // Only the winner applies, once — no last-writer double evaluation.
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
                binding = CreatePlaybackBinding();
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f);

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack track = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD");

                TimelineClip earlyClip = track.CreateClip<MmdVmdTimelineClip>();
                earlyClip.start = 0.0;
                earlyClip.duration = 1.0;
                var earlyAsset = (MmdVmdTimelineClip)earlyClip.asset;
                earlyAsset.FrameRate = 30.0f;
                earlyAsset.MotionSourceId = PlaybackVmdId;
                earlyAsset.StartOffsetSeconds = 0.0f;

                TimelineClip laterClip = track.CreateClip<MmdVmdTimelineClip>();
                laterClip.start = 0.0;
                laterClip.duration = 1.0;
                var laterAsset = (MmdVmdTimelineClip)laterClip.asset;
                laterAsset.FrameRate = 30.0f;
                laterAsset.MotionSourceId = PlaybackVmdId;
                // Later input wins on equal weight: apply frame 9 at director.time 0.
                laterAsset.StartOffsetSeconds = 9.25f / 30.0f;

                directorObject = new GameObject("timeline-single-winner-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                director.playableAsset = timelineAsset;
                director.SetGenericBinding(track, controller);

                MmdVmdTimelineBehaviour.ProcessFrameEvaluated += OnProcessFrame;
                director.time = 0.0;
                director.Evaluate();

                Assert.That(processFrameCallbackCount, Is.EqualTo(1),
                    "Track mixer must evaluate the controller once per frame, not once per overlapping clip");
                Assert.That(controller.CurrentFrame, Is.EqualTo(9),
                    "Equal weights resolve to later input index (StartOffset frame 9)");
                Assert.That(
                    Quaternion.Angle(binding.Instance.BoneTransforms[0].localRotation, ExpectedFrameNineUnityRotation(binding)),
                    Is.LessThan(0.001f));
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

                MmdTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [Test]
        public void MixerSelectsHighestPositiveWeightInputWithoutScalingPose()
        {
            MmdUnityPlaybackBinding? binding = null;
            PlayableGraph graph = default;
            try
            {
                binding = CreatePlaybackBinding();
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f);

                graph = PlayableGraph.Create("mmd-vmd-weight-winner-test");
                ScriptPlayable<MmdVmdTimelineMixerBehaviour> mixer =
                    ScriptPlayable<MmdVmdTimelineMixerBehaviour>.Create(graph, 2);
                ScriptPlayable<MmdVmdTimelineBehaviour> lowWeight =
                    ScriptPlayable<MmdVmdTimelineBehaviour>.Create(graph);
                ScriptPlayable<MmdVmdTimelineBehaviour> highWeight =
                    ScriptPlayable<MmdVmdTimelineBehaviour>.Create(graph);

                MmdVmdTimelineBehaviour lowBehaviour = lowWeight.GetBehaviour();
                lowBehaviour.FrameRate = 30.0f;
                lowBehaviour.StartOffsetSeconds = 9.25f / 30.0f;
                lowWeight.SetTime(0.0);

                MmdVmdTimelineBehaviour highBehaviour = highWeight.GetBehaviour();
                highBehaviour.FrameRate = 30.0f;
                highBehaviour.StartOffsetSeconds = 0.0f;
                highWeight.SetTime(0.0);

                graph.Connect(lowWeight, 0, mixer, 0);
                graph.Connect(highWeight, 0, mixer, 1);
                mixer.SetInputWeight(0, 0.25f);
                mixer.SetInputWeight(1, 0.75f);

                MmdVmdTimelineMixerBehaviour mixerBehaviour = mixer.GetBehaviour();
                mixerBehaviour.PrepareFrame(mixer, default);
                mixerBehaviour.ProcessFrame(mixer, default, controller);

                // Highest weight is input 1 (frame 0), full pose — not weight-scaled.
                Assert.That(controller.CurrentFrame, Is.EqualTo(0));
                Assert.That(
                    Quaternion.Angle(binding.Instance.BoneTransforms[0].localRotation, binding.Instance.BindLocalRotations[0]),
                    Is.LessThan(0.001f));
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                MmdTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [Test]
        public void MixerIgnoresZeroWeightInputsAndAllZeroAppliesNoPose()
        {
            MmdUnityPlaybackBinding? binding = null;
            PlayableGraph graph = default;
            int processFrameCallbackCount = 0;
            void OnProcessFrame(double _)
            {
                processFrameCallbackCount++;
            }

            try
            {
                binding = CreatePlaybackBinding();
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f);

                // Seed a known pose at frame 9, then prove all-zero mixer inputs leave it untouched.
                var seed = new MmdVmdTimelineBehaviour { FrameRate = 30.0f };
                seed.EvaluateAtLocalTime(controller, 9.25 / 30.0);
                Quaternion beforeRotation = binding.Instance.BoneTransforms[0].localRotation;
                int beforeFrame = controller.CurrentFrame;

                graph = PlayableGraph.Create("mmd-vmd-zero-weight-mixer-test");
                ScriptPlayable<MmdVmdTimelineMixerBehaviour> mixer =
                    ScriptPlayable<MmdVmdTimelineMixerBehaviour>.Create(graph, 2);
                ScriptPlayable<MmdVmdTimelineBehaviour> first =
                    ScriptPlayable<MmdVmdTimelineBehaviour>.Create(graph);
                ScriptPlayable<MmdVmdTimelineBehaviour> second =
                    ScriptPlayable<MmdVmdTimelineBehaviour>.Create(graph);

                first.GetBehaviour().FrameRate = 30.0f;
                first.GetBehaviour().StartOffsetSeconds = 0.0f;
                first.SetTime(0.0);
                second.GetBehaviour().FrameRate = 30.0f;
                second.GetBehaviour().StartOffsetSeconds = 0.0f;
                second.SetTime(0.0);

                graph.Connect(first, 0, mixer, 0);
                graph.Connect(second, 0, mixer, 1);
                mixer.SetInputWeight(0, 0.0f);
                mixer.SetInputWeight(1, 0.0f);

                MmdVmdTimelineBehaviour.ProcessFrameEvaluated += OnProcessFrame;
                MmdVmdTimelineMixerBehaviour mixerBehaviour = mixer.GetBehaviour();
                mixerBehaviour.PrepareFrame(mixer, default);
                mixerBehaviour.ProcessFrame(mixer, default, controller);

                Assert.That(processFrameCallbackCount, Is.EqualTo(0));
                Assert.That(controller.CurrentFrame, Is.EqualTo(beforeFrame));
                Assert.That(
                    Quaternion.Angle(binding.Instance.BoneTransforms[0].localRotation, beforeRotation),
                    Is.LessThan(0.001f));

                // Positive weight on input 0 only must select that winner; zero-weight input is ignored.
                mixer.SetInputWeight(0, 1.0f);
                mixer.SetInputWeight(1, 0.0f);
                first.GetBehaviour().StartOffsetSeconds = 0.0f;
                second.GetBehaviour().StartOffsetSeconds = 9.25f / 30.0f;
                mixerBehaviour.ProcessFrame(mixer, default, controller);

                Assert.That(processFrameCallbackCount, Is.EqualTo(1));
                Assert.That(controller.CurrentFrame, Is.EqualTo(0));
            }
            finally
            {
                MmdVmdTimelineBehaviour.ProcessFrameEvaluated -= OnProcessFrame;
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                MmdTestInstanceScope.DestroyInstance(binding?.Instance);
            }
        }

        [Test]
        public void DirectProcessFrameStillAppliesWhenNotTrackManaged()
        {
            // Compatibility path: a behaviour not owned by MmdVmdTimelineTrack/mixer still applies pose
            // from its own ProcessFrame during graph evaluation.
            MmdUnityPlaybackBinding? binding = null;
            PlayableGraph graph = default;
            int processFrameCallbackCount = 0;
            void OnProcessFrame(double _)
            {
                processFrameCallbackCount++;
            }

            try
            {
                binding = CreatePlaybackBinding();
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                controller.Configure(binding, 30.0f);

                graph = PlayableGraph.Create("mmd-vmd-direct-process-frame");
                ScriptPlayable<MmdVmdTimelineBehaviour> playable =
                    ScriptPlayable<MmdVmdTimelineBehaviour>.Create(graph);
                MmdVmdTimelineBehaviour behaviour = playable.GetBehaviour();
                behaviour.Controller = controller;
                behaviour.FrameRate = 30.0f;
                playable.SetTime(9.25 / 30.0);

                ScriptPlayableOutput output = ScriptPlayableOutput.Create(graph, "mmd-vmd-direct");
                output.SetSourcePlayable(playable);
                output.SetUserData(controller);

                MmdVmdTimelineBehaviour.ProcessFrameEvaluated += OnProcessFrame;
                graph.Evaluate();

                Assert.That(processFrameCallbackCount, Is.EqualTo(1));
                Assert.That(controller.CurrentFrame, Is.EqualTo(9));
                Assert.That(
                    Quaternion.Angle(binding.Instance.BoneTransforms[0].localRotation, ExpectedFrameNineUnityRotation(binding)),
                    Is.LessThan(0.001f));
            }
            finally
            {
                MmdVmdTimelineBehaviour.ProcessFrameEvaluated -= OnProcessFrame;
                if (graph.IsValid())
                {
                    graph.Destroy();
                }

                MmdTestInstanceScope.DestroyInstance(binding?.Instance);
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
                MmdVmdTimelineBehaviour? behaviour = scriptPlayable.GetBehaviour();

                Assert.That(behaviour, Is.Not.Null);
                Assert.That(behaviour!.MotionAsset, Is.SameAs(motionAsset));
                Assert.That(behaviour!.MotionSourceId, Is.EqualTo("Assets/Motion/test.vmd"));
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
                Assert.That(ex, Is.Not.Null);
                Assert.That(ex!.Message, Does.Contain("Timeline").Or.Contain("timeline").Or.Contain("existing scene").Or.Contain("SkinnedMeshRenderer"));

                // Verify no '<controller> Runtime' fallback root was created.
                Assert.That(controller.IsConfigured, Is.False);
                // The holder should not have spawned a new child with " Runtime" suffix.
                Transform? parentTransform = holder.transform.parent;
                if (parentTransform != null)
                {
                    Transform? runtimeChild = parentTransform.Find(holder.name + " Runtime");
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
                Assert.That(ex, Is.Not.Null);
                Assert.That(ex!.Message, Does.Contain("Timeline").Or.Contain("timeline").Or.Contain("existing scene").Or.Contain("SkinnedMeshRenderer"));

                Assert.That(controller.IsConfigured, Is.False);
                Transform? parentTransform = holder.transform.parent;
                if (parentTransform != null)
                {
                    Transform? runtimeChild = parentTransform.Find(holder.name + " Runtime");
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

        private static MmdUnityPlaybackBinding CreatePlaybackBinding()
        {
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(MmdTestFixtures.ReadFixtureAssetBytes(PlaybackPmxId));
            MmdMotionDefinition motion = parser.LoadMotion(MmdTestFixtures.ReadFixtureAssetBytes(PlaybackVmdId));
            return MmdUnityPlaybackBinding.CreateSkinned(model, motion, PlaybackPmxId, PlaybackVmdId);
        }

        private static Quaternion ExpectedFrameNineUnityRotation(MmdUnityPlaybackBinding binding)
        {
            float[] expectedMmdLocalRotation = { -0.3826833665f, 0.0f, 0.0f, 0.9238795638f };
            return binding.Instance.BindLocalRotations[0] * ToUnityRotation(expectedMmdLocalRotation);
        }

        private static Quaternion ToUnityRotation(float[] rotation)
        {
            return new Quaternion(-rotation[0], rotation[1], -rotation[2], rotation[3]);
        }

        private static string ResolvePackageFixture(string fileName)
        {
            string? projectRoot = Path.GetDirectoryName(Application.dataPath);
            Assert.That(projectRoot, Is.Not.Null);
            string packageRoot = Path.GetFullPath(Path.Combine(projectRoot!, "..", "packages", "com.yohawing.mmd-loader"));
            return Path.Combine(packageRoot, "Tests", "Fixtures", "Assets", fileName);
        }
    }
}
