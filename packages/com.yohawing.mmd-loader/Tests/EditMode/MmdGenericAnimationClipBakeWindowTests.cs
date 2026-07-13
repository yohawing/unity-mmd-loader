#nullable enable

using System.IO;
using NUnit.Framework;
using UnityEngine;
using Mmd.Editor;

namespace Mmd.Tests
{
    public sealed class MmdGenericAnimationClipBakeWindowTests
    {
        [Test]
        public void OpenPrefillsPmxAndDeterministicOutputPath()
        {
            MmdPmxAsset pmx = ScriptableObject.CreateInstance<MmdPmxAsset>();
            pmx.Initialize(new byte[] { 1 }, "prefill.pmx", "Assets/prefill.pmx", 1.0f);
            MmdGenericAnimationClipBakeWindow? window = null;
            try
            {
                window = MmdGenericAnimationClipBakeWindow.Open(pmx);
                Assert.That(window.PmxAssetForTests, Is.SameAs(pmx));
                Assert.That(window.OutputPathForTests, Is.EqualTo("Assets/MmdGenericClip_prefill_pmx_vmd.anim"));
            }
            finally
            {
                if (window != null) window.Close();
                Object.DestroyImmediate(pmx);
            }
        }

        [Test]
        public void SettingVmdUsesCachedMaxFrameWithoutParsingMotion()
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
                window = MmdGenericAnimationClipBakeWindow.Open(null);
                window.SetVmdAssetForTests(vmd);
                Assert.That(window.VmdAssetForTests, Is.SameAs(vmd));
                Assert.That(window.EndFrameForTests, Is.EqualTo(27));
            }
            finally
            {
                if (window != null) window.Close();
                Object.DestroyImmediate(vmd);
            }
        }
    }
}
