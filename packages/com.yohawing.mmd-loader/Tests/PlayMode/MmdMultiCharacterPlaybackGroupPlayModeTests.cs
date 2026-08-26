#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.IO;
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
                        int evaluatedFrame = grouped[0].Controller.CurrentFrame;
                        Assert.That(evaluatedFrame, Is.GreaterThanOrEqualTo(InitialFrame));
                        for (int i = 0; i < characterCount; i++)
                        {
                            Assert.That(grouped[i].Controller.CurrentFrame, Is.EqualTo(evaluatedFrame));
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

        private static void AttachSources(
            PlaybackFixture fixture,
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset)
        {
            MmdUnityPlaybackController controller = fixture.Controller;
            controller.ConfigureModelAsset(pmxAsset);
            controller.ConfigureMotionAsset(vmdAsset);
            Assert.That(controller.TryEnableFastRuntimeFromConfiguredSource(out string reason), Is.True, reason);
            controller.SeekFrame(InitialFrame);
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
