#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd.Editor;
using Mmd.Editor.Timeline;
using Mmd.Timeline;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed class MmdTimelineAssetWorkflowTests
    {
        private const string TempDirectory = "Assets/__MmdTimelineAssetWorkflowTests";
        private const string TempPmxPath = TempDirectory + "/test_1bone_cube.pmx";
        private const string TempVmdPath = TempDirectory + "/test_1bone_cube_motion.vmd";
        private const string TempAlternateVmdPath = TempDirectory + "/test_1bone_cube_motion_alt.vmd";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempDirectory);
            AssetDatabase.Refresh();
        }

        [Test]
        public void CreatesBoundVmdTrackAndClipFromDroppedPmxObject()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            MmdUnityModelInstance? instance = null;
            GameObject? directorObject = null;
            TimelineAsset? timelineAsset = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.zero, parent: null);
                Assert.That(
                    MmdTimelineAssetWorkflow.TryGetPlaybackController(instance.Root, out MmdUnityPlaybackController? controller),
                    Is.True);
                // Timeline asset workflow uses the controller PMX source plus the clip MotionAsset.

                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                directorObject = new GameObject("mmd-asset-workflow-timeline-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                MmdVmdTimelineTrack track = MmdTimelineAssetWorkflow.CreateVmdTrack(timelineAsset, director, controller!);
                int gameObjectCountBeforeClip = Object.FindObjectsByType<GameObject>().Length;
                TimelineClip clip = MmdTimelineAssetWorkflow.CreateVmdClip(track, vmdAsset, controller!, frameRate: 30.0f);
                var mmdClip = (MmdVmdTimelineClip)clip.asset;
                int gameObjectCountAfterClip = Object.FindObjectsByType<GameObject>().Length;
                Assert.That(controller!.IsConfigured, Is.False);

                director.playableAsset = timelineAsset;
                director.SetGenericBinding(track, controller);
                director.time = 49.0 / 30.0;
                director.Evaluate();

                Assert.That(director.GetGenericBinding(track), Is.SameAs(controller));
                Assert.That(MmdTimelineAssetWorkflow.FindFirstMmdVmdTrack(timelineAsset), Is.SameAs(track));
                Assert.That(mmdClip.MotionAsset, Is.SameAs(vmdAsset));
                Assert.That(mmdClip.ModelSourceId, Is.EqualTo(TempPmxPath));
                Assert.That(mmdClip.MotionSourceId, Is.EqualTo(TempVmdPath));
                Assert.That(clip.duration, Is.EqualTo(50.0 / 30.0).Within(0.00001));
                Assert.That(gameObjectCountAfterClip, Is.EqualTo(gameObjectCountBeforeClip));
                Assert.That(controller.IsConfigured, Is.True);
                Assert.That(controller.ConfiguredInstanceRoot, Is.SameAs(instance.Root));
                Assert.That(controller.MotionSourceId, Is.EqualTo(TempVmdPath));
                Assert.That(controller.CurrentFrame, Is.EqualTo(49));
                Assert.That(controller.LastSnapshot, Is.Not.Null);
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

                DestroyInstance(instance);
            }
        }

        [Test]
        public void VmdTimelineClipEditorSetsDroppedClipDurationFromMotionAsset()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            TimelineAsset? timelineAsset = null;

            try
            {
                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack track = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD");
                TimelineClip clip = track.CreateClip<MmdVmdTimelineClip>();
                var mmdClip = (MmdVmdTimelineClip)clip.asset;
                mmdClip.MotionAsset = vmdAsset;
                mmdClip.FrameRate = 30.0f;

                bool applied = MmdVmdTimelineClipEditor.ApplyDurationFromMotionAsset(clip);

                Assert.That(applied, Is.True);
                Assert.That(clip.duration, Is.EqualTo(50.0 / 30.0).Within(0.00001));
            }
            finally
            {
                if (timelineAsset != null)
                {
                    Object.DestroyImmediate(timelineAsset);
                }
            }
        }

        [Test]
        public void VmdTimelineClipEditorSetsDroppedClipDurationFromCachedMaxFrameWithoutParsing()
        {
            MmdVmdAsset? vmdAsset = null;
            TimelineAsset? timelineAsset = null;

            try
            {
                vmdAsset = CreateSyntheticVmdAssetWithCachedSummary("cache-only-motion.vmd", maxFrame: 89);
                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack track = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD");
                TimelineClip clip = track.CreateClip<MmdVmdTimelineClip>();
                var mmdClip = (MmdVmdTimelineClip)clip.asset;
                mmdClip.MotionAsset = vmdAsset;
                mmdClip.FrameRate = 45.0f;

                bool applied = MmdVmdTimelineClipEditor.ApplyDurationFromMotionAsset(clip);

                Assert.That(applied, Is.True);
                Assert.That(clip.duration, Is.EqualTo(90.0 / 45.0).Within(0.00001));
            }
            finally
            {
                if (timelineAsset != null)
                {
                    Object.DestroyImmediate(timelineAsset);
                }

                if (vmdAsset != null)
                {
                    Object.DestroyImmediate(vmdAsset);
                }
            }
        }

        [Test]
        public void CreateVmdClipSetsDurationFromCachedMaxFrameWithoutParsing()
        {
            MmdPmxAsset? pmxAsset = null;
            MmdVmdAsset? vmdAsset = null;
            GameObject? controllerObject = null;
            TimelineAsset? timelineAsset = null;

            try
            {
                pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                pmxAsset.Initialize(new byte[] { 1 }, "synthetic-model.pmx", "External/Model/synthetic-model.pmx");
                vmdAsset = CreateSyntheticVmdAssetWithCachedSummary("cache-only-motion.vmd", maxFrame: 89);
                controllerObject = new GameObject("mmd-cache-only-controller");
                MmdUnityPlaybackController controller = controllerObject.AddComponent<MmdUnityPlaybackController>();
                controller.ConfigureModelAsset(pmxAsset);
                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack track = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD");

                TimelineClip clip = MmdTimelineAssetWorkflow.CreateVmdClip(
                    track,
                    vmdAsset,
                    controller,
                    frameRate: 45.0f);
                var mmdClip = (MmdVmdTimelineClip)clip.asset;

                Assert.That(clip.duration, Is.EqualTo(90.0 / 45.0).Within(0.00001));
                Assert.That(mmdClip.MotionAsset, Is.SameAs(vmdAsset));
                Assert.That(mmdClip.MotionSourceId, Is.EqualTo("cache-only-motion.vmd"));
                Assert.That(mmdClip.FrameRate, Is.EqualTo(45.0f));
            }
            finally
            {
                if (timelineAsset != null)
                {
                    Object.DestroyImmediate(timelineAsset);
                }

                if (controllerObject != null)
                {
                    Object.DestroyImmediate(controllerObject);
                }

                if (vmdAsset != null)
                {
                    Object.DestroyImmediate(vmdAsset);
                }

                if (pmxAsset != null)
                {
                    Object.DestroyImmediate(pmxAsset);
                }
            }
        }

        [Test]
        public void VmdTimelineClipEditorIgnoresClipWithoutMotionAsset()
        {
            TimelineAsset? timelineAsset = null;

            try
            {
                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack track = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD");
                TimelineClip clip = track.CreateClip<MmdVmdTimelineClip>();
                double originalDuration = clip.duration;

                bool applied = MmdVmdTimelineClipEditor.ApplyDurationFromMotionAsset(clip);

                Assert.That(applied, Is.False);
                Assert.That(clip.duration, Is.EqualTo(originalDuration));
            }
            finally
            {
                if (timelineAsset != null)
                {
                    Object.DestroyImmediate(timelineAsset);
                }
            }
        }

        [Test]
        public void VmdTimelineClipUsesMinimalCustomInspector()
        {
            MmdVmdTimelineClip? clipAsset = null;
            UnityEditor.Editor? editor = null;

            try
            {
                clipAsset = ScriptableObject.CreateInstance<MmdVmdTimelineClip>();
                editor = UnityEditor.Editor.CreateEditor(clipAsset);

                Assert.That(editor, Is.TypeOf<MmdVmdTimelineClipInspector>());
            }
            finally
            {
                if (editor != null)
                {
                    Object.DestroyImmediate(editor);
                }

                if (clipAsset != null)
                {
                    Object.DestroyImmediate(clipAsset);
                }
            }
        }
        [Test]
        public void MultipleCreatedClipsDoNotConfigureControllerUntilEachClipEvaluates()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempAlternateVmdPath);
            PatchSecondBoneKeyframeRotationToIdentity(TempAlternateVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset firstVmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            MmdVmdAsset secondVmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempAlternateVmdPath);
            MmdUnityModelInstance? instance = null;
            GameObject? directorObject = null;
            TimelineAsset? timelineAsset = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.zero, parent: null);
                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                directorObject = new GameObject("mmd-multi-clip-timeline-director");
                PlayableDirector director = directorObject.AddComponent<PlayableDirector>();
                MmdVmdTimelineTrack track = MmdTimelineAssetWorkflow.CreateVmdTrack(timelineAsset, director, controller);

                MmdTimelineAssetWorkflow.CreateVmdClip(track, firstVmdAsset, controller, startSeconds: 0.0, frameRate: 30.0f, director: director);
                MmdTimelineAssetWorkflow.CreateVmdClip(track, secondVmdAsset, controller, startSeconds: 2.0, frameRate: 30.0f, director: director);
                Assert.That(controller.IsConfigured, Is.False);

                var firstBehaviour = new MmdVmdTimelineBehaviour
                {
                    MotionAsset = firstVmdAsset,
                    FrameRate = 30.0f
                };
                firstBehaviour.EvaluateAtLocalTime(controller, 9.0 / 30.0);
                Quaternion firstRotation = instance.BoneTransforms[0].localRotation;
                Assert.That(controller.MotionSourceId, Is.EqualTo(TempVmdPath));

                var secondBehaviour = new MmdVmdTimelineBehaviour
                {
                    MotionAsset = secondVmdAsset,
                    FrameRate = 30.0f
                };
                secondBehaviour.EvaluateAtLocalTime(controller, 9.0 / 30.0);
                Assert.That(controller.MotionSourceId, Is.EqualTo(TempAlternateVmdPath));
                Assert.That(
                    Quaternion.Angle(firstRotation, instance.BoneTransforms[0].localRotation),
                    Is.GreaterThan(1.0f));
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

                DestroyInstance(instance);
            }
        }

        [Test]
        public void TimelineEvaluationRebindsExistingPmxSceneObjectAfterLiveInstanceIsLost()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            MmdUnityModelInstance? instance = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.zero, parent: null);
                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                // Timeline workflow precondition: model source provider from PMX drag (HasModelSource true).
                // Motion for clip/eval comes from Timeline clip / MmdVmdTimelineBehaviour.MotionAsset.
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.IsConfigured, Is.False);
                int gameObjectCountBeforeEvaluation = Object.FindObjectsByType<GameObject>().Length;

                var behaviour = new MmdVmdTimelineBehaviour
                {
                    MotionAsset = vmdAsset,
                    FrameRate = 30.0f
                };
                MmdPlaybackSnapshot snapshot = behaviour.EvaluateAtLocalTime(controller, 49.0 / 30.0);

                Assert.That(snapshot.frame.frame, Is.EqualTo(49));
                Assert.That(controller.IsConfigured, Is.True);
                Assert.That(controller.ConfiguredInstanceRoot, Is.SameAs(instance.Root));
                Assert.That(controller.MotionSourceId, Is.EqualTo(TempVmdPath));
                Assert.That(controller.CurrentFrame, Is.EqualTo(49));
                Assert.That(
                    Object.FindObjectsByType<GameObject>().Length,
                    Is.EqualTo(gameObjectCountBeforeEvaluation));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void TimelineEvaluationRebindsRawPmxPathSceneObject()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");
            MmdUnityModelInstance? instance = null;

            try
            {
                instance = MmdSceneDragAndDrop.LoadPmxPathForDragAndDrop(pmxPath, Vector3.zero, parent: null);
                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
                int gameObjectCountBeforeEvaluation = Object.FindObjectsByType<GameObject>().Length;

                var behaviour = new MmdVmdTimelineBehaviour
                {
                    MotionAsset = vmdAsset,
                    FrameRate = 30.0f
                };
                MmdPlaybackSnapshot snapshot = behaviour.EvaluateAtLocalTime(controller, 49.0 / 30.0);

                Assert.That(snapshot.frame.frame, Is.EqualTo(49));
                Assert.That(controller.IsConfigured, Is.True);
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.ConfiguredInstanceRoot, Is.SameAs(instance.Root));
                Assert.That(controller.ModelSourceId, Is.EqualTo(Path.GetFullPath(pmxPath)));
                Assert.That(controller.MotionSourceId, Is.EqualTo(TempVmdPath));
                Assert.That(controller.CurrentFrame, Is.EqualTo(49));
                Assert.That(
                    Object.FindObjectsByType<GameObject>().Length,
                    Is.EqualTo(gameObjectCountBeforeEvaluation));
            }
            finally
            {
                DestroyInstance(instance);
            }
        }

        [Test]
        public void MissingLivePmxInstanceThrowsWithoutRuntimeFallback()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);
            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            GameObject? root = null;
            TimelineAsset? timelineAsset = null;

            try
            {
                root = new GameObject("mmd-reloaded-root");
                // No SkinnedMeshRenderer on root — Timeline EvaluateAtLocalTime must not create a
                // fallback runtime root. It throws InvalidOperationException instead.
                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();
                controller.ConfigureModelAsset(pmxAsset);
                Assert.That(controller.HasModelSource, Is.True);
                timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
                MmdVmdTimelineTrack track = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(null, "MMD VMD");

                TimelineClip clip = MmdTimelineAssetWorkflow.CreateVmdClip(track, vmdAsset, controller);
                var behaviour = new MmdVmdTimelineBehaviour
                {
                    MotionAsset = vmdAsset,
                    FrameRate = 30.0f
                };

                var ex = Assert.Throws<InvalidOperationException>(
                    () => behaviour.EvaluateAtLocalTime(controller, 0.0));
                Assert.That(ex.Message, Does.Contain("Timeline").Or.Contain("timeline"));
                Assert.That(ex.Message, Does.Contain("SkinnedMeshRenderer".ToLower()).Or.Contain("SkinnedMeshRenderer"));

                // No runtime fallback root was created.
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(CountTimelineClips(track), Is.EqualTo(1));
                Assert.That(clip.asset, Is.TypeOf<MmdVmdTimelineClip>());
            }
            finally
            {
                if (timelineAsset != null)
                {
                    Object.DestroyImmediate(timelineAsset);
                }

                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void CopyFixtureToAssetDatabase(string fixtureName, string destinationAssetPath)
        {
            string source = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", fixtureName);
            Directory.CreateDirectory(Path.Combine(ProjectRoot, TempDirectory));
            File.Copy(source, Path.Combine(ProjectRoot, destinationAssetPath), overwrite: true);
            AssetDatabase.ImportAsset(destinationAssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static MmdVmdAsset CreateSyntheticVmdAssetWithCachedSummary(string sourceId, int maxFrame)
        {
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            asset.name = Path.GetFileNameWithoutExtension(sourceId);
            var summary = new MmdVmdParseSummary(
                targetModelName: "cache-only-model",
                maxFrame: maxFrame,
                boneKeyframeCount: 1,
                morphKeyframeCount: 0,
                modelKeyframeCount: 0,
                constraintStateCount: 0);
            asset.Initialize(new byte[] { 0x00 }, sourceId, "External/Motion/" + sourceId, summary);
            return asset;
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string RepositoryRoot => Path.GetFullPath(Path.Combine(ProjectRoot, ".."));

        private static string ResolvePackageFixture(string fileName)
        {
            return Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", fileName);
        }

        private static void PatchSecondBoneKeyframeRotationToIdentity(string assetPath)
        {
            string fullPath = Path.Combine(ProjectRoot, assetPath);
            byte[] bytes = File.ReadAllBytes(fullPath);
            const int headerSize = 30;
            const int modelNameSize = 20;
            const int boneCountSize = 4;
            const int boneKeyframeSize = 111;
            const int boneNameSize = 15;
            const int frameSize = 4;
            const int positionSize = 12;
            int rotationOffset = headerSize + modelNameSize + boneCountSize + boneKeyframeSize + boneNameSize + frameSize + positionSize;
            WriteFloat(bytes, rotationOffset, 0.0f);
            WriteFloat(bytes, rotationOffset + 4, 0.0f);
            WriteFloat(bytes, rotationOffset + 8, 0.0f);
            WriteFloat(bytes, rotationOffset + 12, 1.0f);
            File.WriteAllBytes(fullPath, bytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void WriteFloat(byte[] bytes, int offset, float value)
        {
            byte[] encoded = System.BitConverter.GetBytes(value);
            System.Buffer.BlockCopy(encoded, 0, bytes, offset, encoded.Length);
        }

        private static int CountTimelineClips(MmdVmdTimelineTrack track)
        {
            int count = 0;
            foreach (TimelineClip _ in track.GetClips())
            {
                count++;
            }

            return count;
        }

        private static void DestroyInstance(MmdUnityModelInstance? instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.Root != null)
            {
                Object.DestroyImmediate(instance.Root);
            }

            if (instance.Mesh != null && !AssetDatabase.Contains(instance.Mesh))
            {
                Object.DestroyImmediate(instance.Mesh);
            }

            foreach (Material material in instance.Materials)
            {
                if (material != null && !AssetDatabase.Contains(material))
                {
                    Object.DestroyImmediate(material);
                }
            }

            foreach (Texture2D texture in instance.OwnedTextures)
            {
                if (texture != null && !AssetDatabase.Contains(texture))
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }
    }
}
