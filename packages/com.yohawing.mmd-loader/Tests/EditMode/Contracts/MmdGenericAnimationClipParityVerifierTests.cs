#nullable enable

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Editor;

namespace Mmd.Tests
{
    public sealed class MmdGenericAnimationClipParityVerifierTests
    {
        private const string CubePmx = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube.pmx";
        private const string CubeVmd = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube_motion.vmd";
        private const string MorphPmx = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_vertex_morph.pmx";
        private const string MorphVmd = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_vertex_morph_motion.vmd";
        private static readonly MmdGenericAnimationClipParityTolerance Tolerance =
            new(0.0001f, 0.0001f, 0.01f, 0.01f, 0.001f);

        [TestCase(CubePmx, CubeVmd, 9)]
        [TestCase(MorphPmx, MorphVmd, 10)]
        public void WriterGeneratedClipPassesFixedFrameParity(string pmxPath, string vmdPath, int endFrame)
        {
            CreateAssets(pmxPath, vmdPath, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            AnimationClip? clip = null;
            try
            {
                clip = MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30, 0, endFrame).Clip;
                MmdGenericAnimationClipParityReport report = MmdGenericAnimationClipParityVerifier.Verify(
                    pmx, vmd, clip!, 30, 0, endFrame, new[] { 0, endFrame / 2, endFrame }, Tolerance);

                Assert.That(report.Diagnostics, Is.Empty);
                Assert.That(report.Passed, Is.True);
                Assert.That(report.SampleCount, Is.EqualTo(3));
                Assert.That(report.BoneSampleCount, Is.GreaterThan(0));
                if (pmxPath == MorphPmx) Assert.That(report.BlendShapeSampleCount, Is.GreaterThan(0));
            }
            finally { Destroy(clip, pmx, vmd); }
        }

        [Test]
        public void MutatedTransformCurveFailsParity()
        {
            CreateAssets(CubePmx, CubeVmd, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            AnimationClip? clip = null;
            try
            {
                clip = MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30, 0, 9).Clip;
                EditorCurveBinding binding = AnimationUtility.GetCurveBindings(clip!)
                    .First(item => item.propertyName == "m_LocalPosition.x");
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip!, binding)!;
                Keyframe[] keys = curve.keys;
                Keyframe mutated = keys[^1];
                mutated.value += 1.0f;
                keys[^1] = mutated;
                AnimationUtility.SetEditorCurve(clip!, binding, new AnimationCurve(keys));

                MmdGenericAnimationClipParityReport report = MmdGenericAnimationClipParityVerifier.Verify(
                    pmx, vmd, clip!, 30, 0, 9, new[] { 9 }, Tolerance);

                Assert.That(report.Passed, Is.False);
                Assert.That(report.MaxLocalPositionDistance, Is.GreaterThan(Tolerance.LocalPositionDistance));
            }
            finally { Destroy(clip, pmx, vmd); }
        }

        [Test]
        public void MissingRequiredCurveFailsWithExplicitBindingDiagnostic()
        {
            CreateAssets(CubePmx, CubeVmd, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            AnimationClip? clip = null;
            try
            {
                clip = MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30, 0, 9).Clip;
                EditorCurveBinding binding = AnimationUtility.GetCurveBindings(clip!).First();
                AnimationUtility.SetEditorCurve(clip!, binding, null);

                MmdGenericAnimationClipParityReport report = MmdGenericAnimationClipParityVerifier.Verify(
                    pmx, vmd, clip!, 30, 0, 9, new[] { 0 }, Tolerance);

                Assert.That(report.Passed, Is.False);
                Assert.That(report.Diagnostics.Any(message => message.StartsWith("binding: missing", StringComparison.Ordinal)), Is.True);
            }
            finally { Destroy(clip, pmx, vmd); }
        }

        [Test]
        public void InvalidFixedFrameFailsBeforeSampling()
        {
            CreateAssets(CubePmx, CubeVmd, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            AnimationClip? clip = null;
            try
            {
                clip = MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30, 0, 9).Clip;
                MmdGenericAnimationClipParityReport report = MmdGenericAnimationClipParityVerifier.Verify(
                    pmx, vmd, clip!, 30, 0, 9, new[] { 10 }, Tolerance);
                Assert.That(report.Passed, Is.False);
                Assert.That(string.Join("\n", report.Diagnostics), Does.Contain("outside the requested frame range"));
                Assert.That(report.SampleCount, Is.Zero);
            }
            finally { Destroy(clip, pmx, vmd); }
        }

        [Test]
        public void PersistedClipReimportPassesParityOnFreshInstances()
        {
            string directory = "Assets/MmdGenericParityPersisted_" + Guid.NewGuid().ToString("N");
            string path = directory + "/clip.anim";
            CreateAssets(MorphPmx, MorphVmd, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            try
            {
                MmdGenericAnimationClipWriterResult written =
                    MmdGenericAnimationClipWriter.CreateAnimationClipAsset(pmx, vmd, 30, 0, 10, path);
                Assert.That(written.Clip, Is.Not.Null, string.Join("\n", written.Diagnostics));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                AnimationClip? reimported = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                Assert.That(reimported, Is.Not.Null);

                MmdGenericAnimationClipParityReport report = MmdGenericAnimationClipParityVerifier.Verify(
                    pmx, vmd, reimported!, 30, 0, 10, new[] { 0, 5, 10 }, Tolerance);

                Assert.That(report.Diagnostics, Is.Empty);
                Assert.That(report.Passed, Is.True);
                Assert.That(report.BoneSampleCount, Is.GreaterThan(0));
                Assert.That(report.BlendShapeSampleCount, Is.GreaterThan(0));
            }
            finally
            {
                AssetDatabase.DeleteAsset(directory);
                Destroy(null, pmx, vmd);
            }
        }

        private static void CreateAssets(string pmxPath, string vmdPath, out MmdPmxAsset pmx, out MmdVmdAsset vmd)
        {
            pmx = ScriptableObject.CreateInstance<MmdPmxAsset>();
            pmx.Initialize(File.ReadAllBytes(pmxPath), Path.GetFileName(pmxPath), pmxPath, 1.0f);
            vmd = ScriptableObject.CreateInstance<MmdVmdAsset>();
            vmd.Initialize(File.ReadAllBytes(vmdPath), Path.GetFileName(vmdPath), vmdPath);
        }

        private static void Destroy(AnimationClip? clip, MmdPmxAsset pmx, MmdVmdAsset vmd)
        {
            if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
            UnityEngine.Object.DestroyImmediate(pmx);
            UnityEngine.Object.DestroyImmediate(vmd);
        }
    }
}
