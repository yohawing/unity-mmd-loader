#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Mmd.Samples.RuntimeVerification.Tests
{
    public sealed class MmdRuntimeViewerControllerUiTests
    {
        private const string RecentsFileName = "mmd-viewer-recents.json";

        [UnityTest]
        public IEnumerator FileInputLoadSeekAndRecentsStayConnected()
        {
            string recentsPath = Path.Combine(Application.persistentDataPath, RecentsFileName);
            string? recentsBackup = BackupAndDelete(recentsPath);
            GameObject? viewerObject = null;
            try
            {
                viewerObject = new GameObject("Runtime Viewer UI Test");
                var controller = viewerObject.AddComponent<MmdRuntimeViewerController>();
                controller.Initialize(MmdRuntimeVerificationArguments.Parse(Array.Empty<string>()));
                yield return null;

                VisualElement root = viewerObject.GetComponent<UIDocument>().rootVisualElement;
                TextField pmxField = Require<TextField>(root, "pmx-field");
                TextField vmdField = Require<TextField>(root, "vmd-field");
                Button loadButton = Require<Button>(root, "load-button");
                Slider frameSlider = Require<Slider>(root, "frame-slider");
                VisualElement recentsSection = Require<VisualElement>(root, "recents-section");
                VisualElement recentsList = Require<VisualElement>(root, "recents-list");

                pmxField.value = ResolvePackageFixture("test_1bone_cube.pmx");
                vmdField.value = ResolvePackageFixture("test_1bone_cube_motion.vmd");
                Assert.That(loadButton.enabledSelf, Is.True, "Load button must be enabled before file input playback.");
                InvokePrivate(controller, "LoadFromInput");

                yield return null;
                yield return null;

                var playbackController = GetPrivateField<MmdUnityPlaybackController>(controller, "playbackController");
                Assert.That(playbackController, Is.Not.Null, "File input load must create a playback controller.");
                Assert.That(playbackController!.IsPlaying, Is.True, "File input load starts playback for interactive verification.");
                Assert.That(frameSlider.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(recentsSection.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(recentsList.childCount, Is.GreaterThan(0), "Successful file input load must add a recent entry.");
                Assert.That(File.Exists(recentsPath), Is.True, "Successful file input load must persist recents.");

                frameSlider.value = 10.0f;
                yield return null;

                Assert.That(playbackController.IsPlaying, Is.False, "Slider seek pauses forward playback.");
                Assert.That(playbackController.CurrentFrame, Is.EqualTo(10), "Slider seek must apply the selected frame.");
            }
            finally
            {
                if (viewerObject != null)
                {
                    Object.Destroy(viewerObject);
                }

                RestoreBackup(recentsPath, recentsBackup);
            }
        }

        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            T? element = root.Q<T>(name);
            Assert.That(element, Is.Not.Null, "Runtime viewer UI element is missing: " + name);
            return element!;
        }

        private static T? GetPrivateField<T>(object owner, string name) where T : class
        {
            FieldInfo? field = owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Private field is missing: " + name);
            return field!.GetValue(owner) as T;
        }

        private static void InvokePrivate(object owner, string name)
        {
            MethodInfo? method = owner.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Private method is missing: " + name);
            method!.Invoke(owner, null);
        }

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

        private static string? BackupAndDelete(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            string backupPath = path + ".playmode-test-backup";
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            File.Move(path, backupPath);
            return backupPath;
        }

        private static void RestoreBackup(string path, string? backupPath)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (!string.IsNullOrWhiteSpace(backupPath) && File.Exists(backupPath))
            {
                File.Move(backupPath, path);
            }
        }
    }
}
