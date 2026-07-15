#nullable enable

using System.IO;
using NUnit.Framework;
using UnityEngine;
using Mmd.Editor;

namespace Mmd.Tests
{
    public sealed class MmdAnimationClipBakeWindowTests
    {
        [Test]
        public void OpenFromPmxPrefillsPmxAndCanDefaultToHumanoid()
        {
            MmdPmxAsset pmx = ScriptableObject.CreateInstance<MmdPmxAsset>();
            pmx.Initialize(new byte[] { 1 }, "prefill.pmx", "Assets/prefill.pmx", 1.0f);
            MmdGenericAnimationClipBakeWindow? window = null;
            try
            {
                window = MmdGenericAnimationClipBakeWindow.OpenFromPmx(pmx, preferHumanoid: true);

                Assert.That(window.PmxAssetForTests, Is.SameAs(pmx));
                Assert.That(window.VmdAssetForTests, Is.Null);
                Assert.That(window.ClipTypeForTests, Is.EqualTo(MmdGenericAnimationClipBakeWindow.ClipType.Humanoid));
                Assert.That(
                    window.OutputPathForTests,
                    Is.EqualTo(MmdHumanoidClipConversionWriter.GetDefaultOutputPath(pmx, null)));
            }
            finally
            {
                if (window != null) window.Close();
                Object.DestroyImmediate(pmx);
            }
        }

        [Test]
        public void OpenFromVmdPrefillsVmdAndUsesCachedMaxFrame()
        {
            string path = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube_motion.vmd";
            MmdVmdAsset vmd = ScriptableObject.CreateInstance<MmdVmdAsset>();
            vmd.Initialize(
                File.ReadAllBytes(path),
                "motion.vmd",
                path,
                new MmdVmdParseSummary("model", 27, 0, 0, 0, 0));
            MmdGenericAnimationClipBakeWindow? window = null;
            try
            {
                window = MmdGenericAnimationClipBakeWindow.OpenFromVmd(vmd);

                Assert.That(window.PmxAssetForTests, Is.Null);
                Assert.That(window.VmdAssetForTests, Is.SameAs(vmd));
                Assert.That(window.ClipTypeForTests, Is.EqualTo(MmdGenericAnimationClipBakeWindow.ClipType.Generic));
                Assert.That(window.MaxFrameForTests, Is.EqualTo(27));
                Assert.That(window.StartFrameForTests, Is.Zero);
                Assert.That(window.EndFrameForTests, Is.EqualTo(27));
                Assert.That(window.FrameRateForTests, Is.EqualTo(30.0f));
                Assert.That(window.ReduceKeysForTests, Is.True);
                Assert.That(window.HighPrecisionForTests, Is.False);
                Assert.That(
                    window.OutputPathForTests,
                    Is.EqualTo(MmdGenericAnimationClipWriter.GetDefaultOutputPath(null, vmd)));
            }
            finally
            {
                if (window != null) window.Close();
                Object.DestroyImmediate(vmd);
            }
        }

        [Test]
        public void SwitchingClipTypeRefreshesDefaultOutputPath()
        {
            MmdPmxAsset pmx = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmd = ScriptableObject.CreateInstance<MmdVmdAsset>();
            pmx.Initialize(new byte[] { 1 }, "switch.pmx", "Assets/switch.pmx", 1.0f);
            vmd.Initialize(
                new byte[] { 1 },
                "switch.vmd",
                "Assets/switch.vmd",
                new MmdVmdParseSummary("model", 12, 1, 0, 0, 0));
            MmdGenericAnimationClipBakeWindow? window = null;
            try
            {
                window = MmdGenericAnimationClipBakeWindow.OpenFromVmd(vmd);
                window.SetPmxAssetForTests(pmx);
                string genericPath = window.OutputPathForTests;

                window.SetClipTypeForTests(MmdGenericAnimationClipBakeWindow.ClipType.Humanoid);
                Assert.That(
                    window.OutputPathForTests,
                    Is.EqualTo(MmdHumanoidClipConversionWriter.GetDefaultOutputPath(pmx, vmd)));
                Assert.That(window.OutputPathForTests, Is.EqualTo(genericPath));

                window.SetClipTypeForTests(MmdGenericAnimationClipBakeWindow.ClipType.Generic);
                Assert.That(window.OutputPathForTests, Is.EqualTo(genericPath));
                Assert.That(window.EndFrameForTests, Is.EqualTo(12));
            }
            finally
            {
                if (window != null) window.Close();
                Object.DestroyImmediate(pmx);
                Object.DestroyImmediate(vmd);
            }
        }
    }
}
