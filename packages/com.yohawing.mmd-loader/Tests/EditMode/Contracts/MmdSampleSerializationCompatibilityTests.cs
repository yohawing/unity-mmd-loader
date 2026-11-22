#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mmd.Timeline;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    /// <summary>
    /// Characterizes that the bundled sample scenes/Timelines and user-facing components keep their
    /// script bindings and serialized values. MonoBehaviour/ScriptableObject binding goes through the
    /// MonoScript GUID; the saved <c>m_EditorClassIdentifier</c> (assembly::namespace.class) must not
    /// decide whether the component loads, so a namespace move must keep these assets loadable.
    /// </summary>
    public sealed class MmdSampleSerializationCompatibilityTests
    {
        private const string TempDirectory = "Assets/__MmdSampleSerializationCompatibilityTests";
        private const string LegacyControllerIdentifier =
            "m_EditorClassIdentifier: Mmd.Runtime::Mmd.UnityIntegration.MmdUnityPlaybackController";

        [SetUp]
        public void SetUp()
        {
            MmdTestTempScope.DeleteAssetFolder(TempDirectory, refreshAssetDatabase: false);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MmdTestTempScope.DeleteAssetFolder(TempDirectory, refreshAssetDatabase: false);
        }

        [Test]
        public void BasicPlaybackSampleSceneAndTimelineKeepScriptBindingsAndValues()
        {
            string destination = TempDirectory + "/BasicPlayback";
            CopySampleWithFreshGuids(Path.Combine("Samples~", "BasicPlayback", "Assets"), destination);
            string scenePath = destination + "/BasicPlayback.unity";
            string timelinePath = destination + "/BasicSampleTimeline.playable";
            string pmxPath = destination + "/mmt_test_model.pmx";
            string vmdPath = destination + "/mmt_test_model_test_motion.vmd";

            Assert.That(CountOccurrences(File.ReadAllText(FullPath(scenePath)), LegacyControllerIdentifier), Is.EqualTo(1),
                "the sample scene must still carry the pre-0.6.0 class identifier for the controller");
            AssertPlayableAssetsResolve(timelinePath);

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            AssertNoMissingScripts(scene);

            MmdUnityPlaybackController controller = FindSingle<MmdUnityPlaybackController>();
            Assert.That(controller.gameObject.name, Is.EqualTo("Basic Sample Model (PMX)"));
            Assert.That(controller.ModelAssetSource, Is.Not.Null.And.SameAs(AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(pmxPath)),
                "the PMX prefab instance must keep its model asset reference");

            PlayableDirector director = FindSingle<PlayableDirector>();
            Assert.That(director.gameObject.name, Is.EqualTo("Basic Timeline"));
            Assert.That(director.playOnAwake, Is.True);
            Assert.That(director.extrapolationMode, Is.EqualTo(DirectorWrapMode.None));
            var timeline = director.playableAsset as TimelineAsset;
            Assert.That(timeline, Is.Not.Null.And.SameAs(AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath)));

            TrackAsset[] tracks = timeline!.GetOutputTracks().ToArray();
            Assert.That(tracks, Has.Length.EqualTo(1));
            Assert.That(tracks[0], Is.InstanceOf<MmdVmdTimelineTrack>());
            Assert.That(tracks[0].name, Is.EqualTo("MMD VMD"));
            Assert.That(director.GetGenericBinding(tracks[0]), Is.Not.Null.And.SameAs(controller),
                "the VMD track must stay bound to the scene controller");

            TimelineClip[] clips = tracks[0].GetClips().ToArray();
            Assert.That(clips, Has.Length.EqualTo(1));
            Assert.That(clips[0].displayName, Is.EqualTo("MMT Test Motion (VMD)"));
            Assert.That(clips[0].duration, Is.EqualTo(0.2).Within(1e-6));
            var clipAsset = clips[0].asset as MmdVmdTimelineClip;
            Assert.That(clipAsset, Is.Not.Null);
            Assert.That(clipAsset!.MotionAsset, Is.Not.Null.And.SameAs(AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(vmdPath)));
            Assert.That(clipAsset.FrameRate, Is.EqualTo(30.0f));
            Assert.That(clipAsset.Controller.exposedName, Is.EqualTo(new PropertyName("41cad22c626542189ca9810634226170")));
            Assert.That(clipAsset.Controller.Resolve(director), Is.Not.Null.And.SameAs(controller),
                "the exposed controller reference must resolve through the director");
        }

        [Test]
        public void HumanoidPlaybackSampleSceneAndTimelineKeepScriptBindingsAndValues()
        {
            string destination = TempDirectory + "/HumanoidPlayback";
            CopySampleWithFreshGuids(Path.Combine("Samples~", "HumanoidPlayback", "Assets"), destination);
            string scenePath = destination + "/HumanoidPlayback.unity";
            string timelinePath = destination + "/HumanoidSampleTimeline.playable";
            string pmxPath = destination + "/HumanoidSampleModel.pmx";
            string fbxPath = destination + "/TaisouMocap.fbx";

            Assert.That(CountOccurrences(File.ReadAllText(FullPath(scenePath)), LegacyControllerIdentifier), Is.EqualTo(1),
                "the sample scene must still carry the pre-0.6.0 class identifier for the controller");
            AssertPlayableAssetsResolve(timelinePath);

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            AssertNoMissingScripts(scene);

            MmdUnityPlaybackController controller = FindSingle<MmdUnityPlaybackController>();
            Assert.That(controller.gameObject.name, Is.EqualTo("HumanoidSampleModel"));
            Assert.That(controller.ModelAssetSource, Is.Not.Null.And.SameAs(AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(pmxPath)));
            Assert.That(controller.GetComponent<MmdHumanoidRootMotionDriver>(), Is.Not.Null,
                "the Humanoid PMX prefab must keep its root motion driver");
            Animator animator = controller.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isHuman, Is.True);

            PlayableDirector director = FindSingle<PlayableDirector>();
            Assert.That(director.gameObject.name, Is.EqualTo("Humanoid Timeline"));
            Assert.That(director.playOnAwake, Is.True);
            Assert.That(director.extrapolationMode, Is.EqualTo(DirectorWrapMode.Loop));
            var timeline = director.playableAsset as TimelineAsset;
            Assert.That(timeline, Is.Not.Null.And.SameAs(AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath)));

            TrackAsset[] tracks = timeline!.GetOutputTracks().ToArray();
            Assert.That(tracks, Has.Length.EqualTo(1));
            Assert.That(tracks[0], Is.InstanceOf<MmdHumanoidAnimationTrack>());
            Assert.That(tracks[0].name, Is.EqualTo("MMD Humanoid"));
            Assert.That(director.GetGenericBinding(tracks[0]), Is.Not.Null.And.SameAs(controller),
                "the Humanoid track must stay bound to the scene controller");

            TimelineClip[] clips = tracks[0].GetClips().ToArray();
            Assert.That(clips, Has.Length.EqualTo(1));
            Assert.That(clips[0].displayName, Is.EqualTo("Taisou Mocap (FBX)"));
            Assert.That(clips[0].duration, Is.EqualTo(39.73333740234375).Within(1e-6));
            var clipAsset = clips[0].asset as MmdHumanoidAnimationClip;
            Assert.That(clipAsset, Is.Not.Null);
            Assert.That(clipAsset!.clip, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clipAsset.clip), Is.EqualTo(fbxPath));
        }

        [Test]
        public void UserComponentsSavedWithLegacyEditorClassIdentifierStillLoad()
        {
            const string prefabPath = TempDirectory + "/LegacyComponents.prefab";
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(TempDirectory));

            var source = new GameObject("Legacy Components");
            try
            {
                source.AddComponent<Animator>();
                var controller = source.AddComponent<MmdUnityPlaybackController>();
                var environment = source.AddComponent<MmdSceneEnvironmentBinding>();
                var selfShadowTarget = source.AddComponent<MmdSelfShadowTarget>();
                var runtimeImporter = source.AddComponent<MmdRuntimeImporterComponent>();
                var rootMotionDriver = source.AddComponent<MmdHumanoidRootMotionDriver>();

                SetSerialized(controller, so =>
                {
                    so.FindProperty("frameRate").floatValue = 24.0f;
                    so.FindProperty("initialFrame").intValue = 7;
                    so.FindProperty("playOnStart").boolValue = false;
                });
                SetSerialized(environment, so => so.FindProperty("selfShadowEnabled").boolValue = false);
                SetSerialized(selfShadowTarget, so =>
                {
                    so.FindProperty("selfShadowEnabled").boolValue = false;
                    so.FindProperty("sceneEnvironment").objectReferenceValue = environment;
                    so.FindProperty("boundsRoot").objectReferenceValue = source.transform;
                });
                SetSerialized(runtimeImporter, so =>
                {
                    so.FindProperty("modelPath").stringValue = "legacy/model.pmx";
                    so.FindProperty("motionPath").stringValue = "legacy/motion.vmd";
                    so.FindProperty("frameRate").floatValue = 60.0f;
                    so.FindProperty("initialFrame").intValue = 11;
                    so.FindProperty("playOnStart").boolValue = false;
                });
                SetSerialized(rootMotionDriver, so => so.FindProperty("clipRootVerticalOffset").floatValue = 0.25f);

                PrefabUtility.SaveAsPrefabAsset(source, prefabPath, out bool saved);
                Assert.That(saved, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }

            string yaml = File.ReadAllText(FullPath(prefabPath));
            string[] componentNames =
            {
                nameof(MmdUnityPlaybackController),
                nameof(MmdSceneEnvironmentBinding),
                nameof(MmdSelfShadowTarget),
                nameof(MmdRuntimeImporterComponent),
                nameof(MmdHumanoidRootMotionDriver),
            };
            foreach (string componentName in componentNames)
            {
                var identifier = new Regex(
                    @"m_EditorClassIdentifier: Mmd\.Runtime::[A-Za-z0-9_.]*\b" + componentName + @"\r?$",
                    RegexOptions.Multiline);
                Assert.That(identifier.Matches(yaml).Count, Is.EqualTo(1), componentName + " class identifier");
                yaml = identifier.Replace(
                    yaml,
                    "m_EditorClassIdentifier: Mmd.Runtime::Mmd.UnityIntegration." + componentName);
            }

            TestContext.WriteLine("Legacy YAML:\n" + yaml);
            File.WriteAllText(FullPath(prefabPath), yaml);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            GameObject loaded = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(loaded), Is.EqualTo(0));

            var loadedController = loaded.GetComponent<MmdUnityPlaybackController>();
            var loadedEnvironment = loaded.GetComponent<MmdSceneEnvironmentBinding>();
            var loadedSelfShadowTarget = loaded.GetComponent<MmdSelfShadowTarget>();
            var loadedRuntimeImporter = loaded.GetComponent<MmdRuntimeImporterComponent>();
            var loadedRootMotionDriver = loaded.GetComponent<MmdHumanoidRootMotionDriver>();
            Assert.That(loadedController, Is.Not.Null);
            Assert.That(loadedEnvironment, Is.Not.Null);
            Assert.That(loadedSelfShadowTarget, Is.Not.Null);
            Assert.That(loadedRuntimeImporter, Is.Not.Null);
            Assert.That(loadedRootMotionDriver, Is.Not.Null);

            var controllerSo = new SerializedObject(loadedController);
            Assert.That(controllerSo.FindProperty("frameRate").floatValue, Is.EqualTo(24.0f));
            Assert.That(controllerSo.FindProperty("initialFrame").intValue, Is.EqualTo(7));
            Assert.That(controllerSo.FindProperty("playOnStart").boolValue, Is.False);

            Assert.That(new SerializedObject(loadedEnvironment).FindProperty("selfShadowEnabled").boolValue, Is.False);

            var targetSo = new SerializedObject(loadedSelfShadowTarget);
            Assert.That(targetSo.FindProperty("selfShadowEnabled").boolValue, Is.False);
            Assert.That(targetSo.FindProperty("sceneEnvironment").objectReferenceValue, Is.Not.Null.And.SameAs(loadedEnvironment));
            Assert.That(targetSo.FindProperty("boundsRoot").objectReferenceValue, Is.Not.Null.And.SameAs(loaded.transform));

            var importerSo = new SerializedObject(loadedRuntimeImporter);
            Assert.That(importerSo.FindProperty("modelPath").stringValue, Is.EqualTo("legacy/model.pmx"));
            Assert.That(importerSo.FindProperty("motionPath").stringValue, Is.EqualTo("legacy/motion.vmd"));
            Assert.That(importerSo.FindProperty("frameRate").floatValue, Is.EqualTo(60.0f));
            Assert.That(importerSo.FindProperty("initialFrame").intValue, Is.EqualTo(11));
            Assert.That(importerSo.FindProperty("playOnStart").boolValue, Is.False);

            Assert.That(new SerializedObject(loadedRootMotionDriver).FindProperty("clipRootVerticalOffset").floatValue,
                Is.EqualTo(0.25f));
        }

        /// <summary>
        /// Samples~ is not imported by the AssetDatabase. Copy one sample folder into the project and give
        /// every copied asset a fresh GUID (rewriting references inside the copied text assets) so the copy
        /// cannot collide with an installed copy of the same sample.
        /// </summary>
        private static void CopySampleWithFreshGuids(string relativeSampleDirectory, string destinationAssetPath)
        {
            string sourceDirectory = Path.Combine(MmdTestFixtures.PackageRoot, relativeSampleDirectory);
            Assert.That(sourceDirectory, Does.Exist);
            string destinationDirectory = FullPath(destinationAssetPath);
            Directory.CreateDirectory(destinationDirectory);

            string[] assetFiles = Directory.GetFiles(sourceDirectory)
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var guidMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string assetFile in assetFiles)
            {
                Match guid = Regex.Match(File.ReadAllText(assetFile + ".meta"), @"^guid: ([0-9a-f]{32})", RegexOptions.Multiline);
                Assert.That(guid.Success, Is.True, assetFile + ".meta guid");
                guidMap.Add(guid.Groups[1].Value, Guid.NewGuid().ToString("N"));
            }

            foreach (string assetFile in assetFiles)
            {
                string fileName = Path.GetFileName(assetFile);
                string destinationFile = Path.Combine(destinationDirectory, fileName);
                string extension = Path.GetExtension(fileName);
                if (string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".playable", StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(destinationFile, RemapGuids(File.ReadAllText(assetFile), guidMap));
                }
                else
                {
                    File.Copy(assetFile, destinationFile);
                }

                File.WriteAllText(destinationFile + ".meta", RemapGuids(File.ReadAllText(assetFile + ".meta"), guidMap));
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static string RemapGuids(string text, IReadOnlyDictionary<string, string> guidMap)
        {
            foreach (KeyValuePair<string, string> pair in guidMap)
            {
                text = text.Replace(pair.Key, pair.Value);
            }

            return text;
        }

        private static void AssertPlayableAssetsResolve(string timelinePath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(timelinePath);
            Assert.That(assets, Is.Not.Empty);
            Assert.That(assets, Has.All.Not.Null, "every object in the Timeline asset must resolve its script");
        }

        private static void AssertNoMissingScripts(Scene scene)
        {
            Assert.That(scene.IsValid(), Is.True);
            var missing = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    {
                        missing.Add(transform.gameObject.name);
                    }
                }

                if (PrefabUtility.IsPrefabAssetMissing(root))
                {
                    missing.Add(root.name + " (missing prefab asset)");
                }
            }

            Assert.That(missing, Is.Empty, "scene objects with missing scripts or prefab assets");
        }

        private static T FindSingle<T>() where T : Object
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(found, Has.Length.EqualTo(1), typeof(T).Name);
            return found[0];
        }

        private static void SetSerialized(Object target, Action<SerializedObject> edit)
        {
            var serializedObject = new SerializedObject(target);
            edit(serializedObject);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int CountOccurrences(string text, string value)
        {
            return Regex.Matches(text, Regex.Escape(value)).Count;
        }

        private static string FullPath(string assetPath)
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath)!, assetPath);
        }
    }
}
