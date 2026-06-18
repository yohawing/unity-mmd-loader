#nullable enable

using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd;
using Mmd.Editor;
using Mmd.Physics;
using Mmd.Timeline;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed class MmdEditorWindowTests
    {
        private const string TempDirectory = "Assets/__MmdEditorWindowTests";
        private const string TempPmxPath = TempDirectory + "/test_1bone_cube.pmx";
        private const string TempVmdPath = TempDirectory + "/test_1bone_cube_motion.vmd";

        [SetUp]
        public void SetUp()
        {
            DeleteTempDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteTempDirectory();
        }

        private static void DeleteTempDirectory()
        {
            AssetDatabase.DeleteAsset(TempDirectory);
            string fullTempDirectory = Path.Combine(ProjectRoot, TempDirectory);
            if (Directory.Exists(fullTempDirectory))
            {
                foreach (string path in Directory.EnumerateFileSystemEntries(fullTempDirectory, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }

                Directory.Delete(fullTempDirectory, recursive: true);
            }

            string metaPath = fullTempDirectory + ".meta";
            if (File.Exists(metaPath))
            {
                File.SetAttributes(metaPath, FileAttributes.Normal);
                File.Delete(metaPath);
            }

            AssetDatabase.Refresh();
        }

        private static void CopyFixtureToAssetDatabase(string fixtureName, string destinationAssetPath)
        {
            string source = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader", "Tests", "Fixtures", "Assets", fixtureName);
            Directory.CreateDirectory(Path.Combine(ProjectRoot, TempDirectory));
            File.Copy(source, Path.Combine(ProjectRoot, destinationAssetPath), overwrite: true);
            AssetDatabase.ImportAsset(destinationAssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string RepositoryRoot => Path.GetFullPath(Path.Combine(ProjectRoot, ".."));
        [Test]
        public void OpenWindowCreatesEditorWindow()
        {
            MmdEditorWindow.OpenWindow();
            MmdEditorWindow window = EditorWindow.GetWindow<MmdEditorWindow>();

            Assert.That(window, Is.Not.Null);

            window.Close();
        }

        [Test]
        public void SelectionSnapshotResolvesPmxAndVmdAssets()
        {
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                pmxAsset.name = "model";
                vmdAsset.name = "motion";
                pmxAsset.Initialize(new byte[] { 1 }, "model.pmx", "External/Model/model.pmx");
                vmdAsset.Initialize(new byte[] { 2 }, "motion.vmd", "External/Motion/motion.vmd");

                MmdEditorSelectionSnapshot snapshot = MmdEditorWindow.BuildSelectionSnapshot(
                    new Object[] { pmxAsset, vmdAsset },
                    activeGameObject: null);

                Assert.That(snapshot.PmxAsset, Is.SameAs(pmxAsset));
                Assert.That(snapshot.VmdAsset, Is.SameAs(vmdAsset));
                Assert.That(snapshot.Controller, Is.Null);
                Assert.That(snapshot.Director, Is.Null);
                Assert.That(snapshot.TimelineAsset, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
            }
        }

        [Test]
        public void SelectionSnapshotResolvesSceneControllerAndTimeline()
        {
            GameObject root = new("MMD Root");
            TimelineAsset timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
            try
            {
                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();
                PlayableDirector director = root.AddComponent<PlayableDirector>();
                director.playableAsset = timelineAsset;

                MmdEditorSelectionSnapshot snapshot = MmdEditorWindow.BuildSelectionSnapshot(
                    new Object[] { root },
                    root);

                Assert.That(snapshot.Controller, Is.SameAs(controller));
                Assert.That(snapshot.Director, Is.SameAs(director));
                Assert.That(snapshot.TimelineAsset, Is.SameAs(timelineAsset));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(timelineAsset);
            }
        }

        [Test]
        public void SelectionSnapshotResolvesRuntimeImporter()
        {
            // CanCreatePlaybackConfig resolver still works for direct builder API callers,
            // but the EditorWindow no longer advertises "Create Playback Config" as a primary button.
            GameObject root = new("MMD Source Snapshot Root");
            try
            {
                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();
                MmdRuntimeImporterComponent importer = root.AddComponent<MmdRuntimeImporterComponent>();

                MmdEditorSelectionSnapshot snapshot = MmdEditorWindow.BuildSelectionSnapshot(
                    new Object[] { root },
                    root);

                Assert.That(snapshot.Controller, Is.SameAs(controller));
                Assert.That(snapshot.RuntimeImporter, Is.SameAs(importer));
                Assert.That(MmdEditorWindow.CanCreatePlaybackConfig(snapshot), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CanCreatePlaybackConfigAcceptsRuntimeImporterWithoutController()
        {
            // Static resolver utility (not button surface) still accepts runtime importer.
            MmdRuntimeImporterComponent importer = new GameObject("MMD Runtime Config").AddComponent<MmdRuntimeImporterComponent>();
            try
            {
                Assert.That(
                    MmdEditorWindow.CanCreatePlaybackConfig(new MmdEditorSelectionSnapshot(
                        pmxAsset: null,
                        vmdAsset: null,
                        controller: null,
                        director: null,
                        timelineAsset: null,
                        runtimeImporter: importer)),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(importer.gameObject);
            }
        }

        [Test]
        public void TimelineClipActionRequiresVmdControllerDirectorTimelineAndProviderModelSource()
        {
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            GameObject root = new("MMD Timeline Root");
            TimelineAsset timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
            try
            {
                pmxAsset.Initialize(new byte[] { 1 }, "model.pmx", "External/Model/model.pmx");
                vmdAsset.Initialize(new byte[] { 2 }, "motion.vmd", "External/Motion/motion.vmd");
                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();
                PlayableDirector director = root.AddComponent<PlayableDirector>();
                director.playableAsset = timelineAsset;
                controller.ConfigureModelAsset(pmxAsset);

                var ready = new MmdEditorSelectionSnapshot(
                    pmxAsset,
                    vmdAsset,
                    controller,
                    director,
                    timelineAsset);

                Assert.That(MmdEditorWindow.CanCreateTimelineClip(ready), Is.True);
                Assert.That(
                    MmdEditorWindow.CanCreateTimelineClip(new MmdEditorSelectionSnapshot(
                        pmxAsset,
                        null,
                        controller,
                        director,
                        timelineAsset)),
                    Is.False);

                GameObject unconfiguredRoot = new("MMD Unconfigured Timeline Root");
                try
                {
                    MmdUnityPlaybackController unconfiguredController = unconfiguredRoot.AddComponent<MmdUnityPlaybackController>();
                    Assert.That(
                        MmdEditorWindow.CanCreateTimelineClip(new MmdEditorSelectionSnapshot(
                            pmxAsset,
                            vmdAsset,
                            unconfiguredController,
                            director,
                            timelineAsset)),
                        Is.False);
                }
                finally
                {
                    Object.DestroyImmediate(unconfiguredRoot);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(timelineAsset);
                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
            }
        }

        [Test]
        public void PlaybackSourceActionRequiresPmxVmdAndController()
        {
            // "Create Playback Source" (Can/Execute) is strictly the full PMX+VMD source action.
            // It must not be confused with PMX-only model-source-provider creation (drag / Load PMX paths).
            // Full requires both assets present at creation time; model-provider allows VMD later.
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            GameObject root = new("MMD Playback Source Root");
            try
            {
                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();
                var ready = new MmdEditorSelectionSnapshot(
                    pmxAsset,
                    vmdAsset,
                    controller,
                    director: null,
                    timelineAsset: null);

                Assert.That(MmdEditorWindow.CanCreatePlaybackSource(ready), Is.True);
                Assert.That(
                    MmdEditorWindow.CanCreatePlaybackSource(new MmdEditorSelectionSnapshot(
                        pmxAsset,
                        vmdAsset,
                        controller: null,
                        director: null,
                        timelineAsset: null)),
                    Is.False);
                Assert.That(
                    MmdEditorWindow.CanCreatePlaybackSource(new MmdEditorSelectionSnapshot(
                        pmxAsset,
                        vmdAsset: null,
                        controller,
                        director: null,
                        timelineAsset: null)),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
            }
        }

        [Test]
        public void ExecuteCreatePlaybackSourceCreatesOrUpdatesControllerSources()
        {
            // ExecuteCreatePlaybackSource is the full (model+motion) source action only.
            // It always ConfigureAssets both; model-only provider uses ConfigureModelAsset via drag paths.
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            MmdPmxAsset updatedPmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset updatedVmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            GameObject root = new("MMD Execute Playback Source");
            try
            {
                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();
                controller.SetPlayOnStart(false);
                controller.SetPhysicsMode(MmdPhysicsMode.Off);

                var created = MmdEditorWindow.ExecuteCreatePlaybackSource(
                    pmxAsset,
                    vmdAsset,
                    controller,
                    useUndo: false);
                Assert.That(created.success, Is.True);
                Assert.That(created.status, Does.Contain("Playback source created"));
                Assert.That(created.error, Is.Empty);
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
                Assert.That(controller.MotionAssetSource, Is.SameAs(vmdAsset));
                Assert.That(controller.PlayOnStart, Is.False);
                Assert.That(controller.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Live));

                var updated = MmdEditorWindow.ExecuteCreatePlaybackSource(
                    updatedPmxAsset,
                    updatedVmdAsset,
                    controller,
                    useUndo: false);

                Assert.That(updated.success, Is.True);
                Assert.That(updated.status, Does.Contain("Playback source updated"));
                Assert.That(updated.error, Is.Empty);
                Assert.That(controller.ModelAssetSource, Is.SameAs(updatedPmxAsset));
                Assert.That(controller.MotionAssetSource, Is.SameAs(updatedVmdAsset));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
                Object.DestroyImmediate(updatedPmxAsset);
                Object.DestroyImmediate(updatedVmdAsset);
            }
        }

        [Test]
        public void ExecuteCreatePlaybackSourceUsesControllerSourcesForNewBundled()
        {
            // Full bundled source creation now uses controller-owned source references.
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            MmdPmxAsset updatedPmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset updatedVmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            GameObject root = new("MMD Execute Playback Source Dedicated");
            try
            {
                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();

                var created = MmdEditorWindow.ExecuteCreatePlaybackSource(
                    pmxAsset,
                    vmdAsset,
                    controller,
                    useUndo: false);

                Assert.That(created.success, Is.True);
                Assert.That(controller.ModelAssetSource, Is.SameAs(pmxAsset));
                Assert.That(controller.MotionAssetSource, Is.SameAs(vmdAsset));

                var updated = MmdEditorWindow.ExecuteCreatePlaybackSource(
                    updatedPmxAsset,
                    updatedVmdAsset,
                    controller,
                    useUndo: false);

                Assert.That(updated.success, Is.True);
                Assert.That(controller.ModelAssetSource, Is.SameAs(updatedPmxAsset));
                Assert.That(controller.MotionAssetSource, Is.SameAs(updatedVmdAsset));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
                Object.DestroyImmediate(updatedPmxAsset);
                Object.DestroyImmediate(updatedVmdAsset);
            }
        }

        [Test]
        public void CanCreatePlaybackConfigRequiresSceneControllerOrRuntimeImporter()
        {
            // Static resolver utility (not button surface) still validates controller/importer presence.
            GameObject root = new("MMD Playback Config Resolver Root");
            try
            {
                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();
                var ready = new MmdEditorSelectionSnapshot(
                    pmxAsset: null,
                    vmdAsset: null,
                    controller,
                    director: null,
                    timelineAsset: null);

                Assert.That(MmdEditorWindow.CanCreatePlaybackConfig(ready), Is.True);
                Assert.That(
                    MmdEditorWindow.CanCreatePlaybackConfig(new MmdEditorSelectionSnapshot(
                        pmxAsset: null,
                        vmdAsset: null,
                        controller: null,
                        director: null,
                        timelineAsset: null)),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlaybackConfigResolutionPrefersRuntimeImporterSettingsOverControllerFallback()
        {
            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmdAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            GameObject root = new("MMD Playback Config Importer Priority");
            try
            {
                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();
                MmdRuntimeImporterComponent importer = root.AddComponent<MmdRuntimeImporterComponent>();
                importer.ConfigurePaths("dummy.pmx", "dummy.vmd", playbackFrameRate: 42.0f, startFrame: 7, shouldPlayOnStart: false);

                MmdPlaybackConfig config = MmdUnityPlaybackControllerEditor.ResolvePlaybackConfigForNewSource(
                    controller,
                    fallbackInitialFrame: controller.CurrentFrame);

                Assert.That(config.FrameRate, Is.EqualTo(42.0f));
                Assert.That(config.InitialFrame, Is.EqualTo(7));
                Assert.That(config.PlayOnStart, Is.False);
                // Resolve uses importer-owned settings (no physics); controller (default Live) owns physics mode
                Assert.That(controller.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Live));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(pmxAsset);
                Object.DestroyImmediate(vmdAsset);
            }
        }

        [Test]
        public void D1_BuildSelectionSnapshotResolvesPmxAssetFromGameObjectMain()
        {
            // D1: When a .pmx main object is a GameObject (hierarchy root), selecting it in
            // the Project view must populate snapshot.PmxAsset via the compatibility resolver.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            GameObject? mainGo = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);
            Assert.That(mainGo, Is.Not.Null, "precondition: imported .pmx must have a GameObject main object");

            MmdEditorSelectionSnapshot snapshot = MmdEditorWindow.BuildSelectionSnapshot(
                new Object[] { mainGo },
                activeGameObject: null);

            Assert.That(snapshot.PmxAsset, Is.Not.Null,
                "BuildSelectionSnapshot must resolve MmdPmxAsset from GameObject main selection");

            MmdPmxAsset? direct = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(snapshot.PmxAsset, Is.SameAs(direct),
                "snapshot.PmxAsset must match LoadAssetAtPath<MmdPmxAsset>");
        }

        [Test]
        public void D1_BuildSelectionSnapshotPrefersDirectMmdPmxAssetOverGameObjectResolution()
        {
            // When both MmdPmxAsset and GameObject are selected, the MmdPmxAsset cast wins.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            MmdPmxAsset pmxAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            GameObject? mainGo = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);
            Assert.That(pmxAsset, Is.Not.Null);
            Assert.That(mainGo, Is.Not.Null);

            // Both in selection: MmdPmxAsset cast should be found first (iteration order).
            MmdEditorSelectionSnapshot snapshot = MmdEditorWindow.BuildSelectionSnapshot(
                new Object[] { mainGo, pmxAsset },
                activeGameObject: null);

            Assert.That(snapshot.PmxAsset, Is.Not.Null);
            Assert.That(snapshot.PmxAsset, Is.SameAs(pmxAsset));
        }

        [Test]
        public void ExecuteCreateTimelineClipCreatesTrackClipAndSelects()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube_motion.vmd", TempVmdPath);

            MmdPmxAsset pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(TempVmdPath);
            TimelineAsset timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
            GameObject root = new("MMD Timeline Clip Test");
            try
            {
                Assert.That(vmdAsset, Is.Not.Null);
                pmxAsset.Initialize(new byte[] { 1 }, "model.pmx", "External/Model/model.pmx");
                MmdUnityPlaybackController controller = root.AddComponent<MmdUnityPlaybackController>();
                PlayableDirector director = root.AddComponent<PlayableDirector>();
                director.playableAsset = timelineAsset;
                controller.ConfigureModelAsset(pmxAsset);

                var result = MmdEditorWindow.ExecuteCreateTimelineClip(
                    vmdAsset,
                    director,
                    controller,
                    timelineAsset,
                    useUndo: false);

                Assert.That(result.success, Is.True);
                Assert.That(result.status, Does.Contain("Created VMD Timeline clip"));
                Assert.That(result.error, Is.Empty);

                // Verify track exists on timeline.
                MmdVmdTimelineTrack? track = timelineAsset.GetOutputTracks().OfType<MmdVmdTimelineTrack>().FirstOrDefault();
                Assert.That(track, Is.Not.Null, "MmdVmdTimelineTrack should exist on timeline");

                // Verify clip exists on track.
                Assert.That(track!.GetClips().Count(), Is.EqualTo(1));
                TimelineClip clip = track.GetClips().First();
                Assert.That(clip.asset, Is.TypeOf<MmdVmdTimelineClip>());
                var mmdClip = (MmdVmdTimelineClip)clip.asset;
                Assert.That(mmdClip.MotionAsset, Is.SameAs(vmdAsset));

                // Verify created clip asset is selected.
                Assert.That(Selection.activeObject, Is.SameAs(clip.asset));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(timelineAsset);
                Object.DestroyImmediate(pmxAsset);
            }
        }
    }
}
