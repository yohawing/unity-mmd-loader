#nullable enable

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Editor;
using Mmd.Physics;

namespace Mmd.Tests
{
    public sealed class MmdGenericAnimationClipWriterTests
    {
        private const string CubePmx = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube.pmx";
        private const string CubeVmd = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube_motion.vmd";
        private const string MorphPmx = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_vertex_morph.pmx";
        private const string MorphVmd = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_vertex_morph_motion.vmd";

        [Test]
        public void BakeKeepsDenseChangingCurvesAndCompactsOnlyConstantCurvesWithPhysicsOff()
        {
            CreateAssets(CubePmx, CubeVmd, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            try
            {
                MmdGenericAnimationClipWriterResult result =
                    MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30.0f, 0, 9);

                Assert.That(result.Clip, Is.Not.Null);
                Assert.That(result.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Off));
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(result.Clip!);
                Assert.That(bindings.Count(binding => binding.type == typeof(Transform)), Is.EqualTo(7));
                int[] keyCounts = bindings
                    .Where(binding => binding.type == typeof(Transform))
                    .Select(binding => AnimationUtility.GetEditorCurve(result.Clip!, binding)!.keys.Length)
                    .ToArray();
                Assert.That(keyCounts, Does.Contain(10), "at least one changing transform curve stays dense");
                Assert.That(keyCounts, Does.Contain(2), "constant transform curves keep only duration endpoints");
                foreach (int keyCount in keyCounts)
                {
                    Assert.That(keyCount, Is.EqualTo(2).Or.EqualTo(10));
                }

                Assert.That(result.Diagnostics, Has.Some.Contains("persistent native evaluation"));
                Assert.That(result.Diagnostics, Has.Some.Contains("compacted"));
            }
            finally
            {
                DestroyAssets(pmx, vmd);
            }
        }

        [Test]
        public void DenseBakeIsRepeatableForSameInputs()
        {
            CreateAssets(CubePmx, CubeVmd, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            try
            {
                MmdGenericAnimationClipWriterResult first = MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30, 0, 9);
                MmdGenericAnimationClipWriterResult second = MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30, 0, 9);
                EditorCurveBinding[] firstBindings = AnimationUtility.GetCurveBindings(first.Clip!);
                EditorCurveBinding[] secondBindings = AnimationUtility.GetCurveBindings(second.Clip!);
                Assert.That(secondBindings.Select(BindingKey), Is.EqualTo(firstBindings.Select(BindingKey)));
                for (int i = 0; i < firstBindings.Length; i++)
                    Assert.That(AnimationUtility.GetEditorCurve(second.Clip!, secondBindings[i])!.keys.Select(k => (k.time, k.value)),
                        Is.EqualTo(AnimationUtility.GetEditorCurve(first.Clip!, firstBindings[i])!.keys.Select(k => (k.time, k.value))));
                UnityEngine.Object.DestroyImmediate(first.Clip);
                UnityEngine.Object.DestroyImmediate(second.Clip);
            }
            finally { DestroyAssets(pmx, vmd); }
        }

        [Test]
        public void VertexMorphBakeWritesBlendShapeCurveInPercentUnits()
        {
            CreateAssets(MorphPmx, MorphVmd, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            try
            {
                MmdGenericAnimationClipWriterResult result = MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30, 0, 10);
                Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                EditorCurveBinding morphBinding = AnimationUtility.GetCurveBindings(result.Clip!)
                    .Single(binding => binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal));
                AnimationCurve curve = AnimationUtility.GetEditorCurve(result.Clip!, morphBinding)!;
                Assert.That(curve.keys, Has.Length.EqualTo(11));
                Assert.That(curve.keys.Max(key => key.value), Is.GreaterThan(0.0f).And.LessThanOrEqualTo(100.0f));
                UnityEngine.Object.DestroyImmediate(result.Clip);
            }
            finally { DestroyAssets(pmx, vmd); }
        }

        [TestCase(0.0f, 0, 0)]
        [TestCase(float.NaN, 0, 0)]
        [TestCase(30.0f, -1, 0)]
        [TestCase(30.0f, 2, 1)]
        public void InvalidSamplingInputsReturnNoClip(float rate, int start, int end)
        {
            CreateAssets(CubePmx, CubeVmd, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            try
            {
                MmdGenericAnimationClipWriterResult result = MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, rate, start, end);
                Assert.That(result.Clip, Is.Null);
                Assert.That(result.Diagnostics, Is.Not.Empty);
            }
            finally { DestroyAssets(pmx, vmd); }
        }

        [Test]
        public void ExplicitAssetPersistenceUsesValidatedAnimPathAndCanBeCleanedUp()
        {
            string directory = "Assets/MmdGenericAnimationClipWriterTests_" + Guid.NewGuid().ToString("N");
            string path = directory + "/clip.anim";
            CreateAssets(CubePmx, CubeVmd, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            try
            {
                MmdGenericAnimationClipWriterResult result = MmdGenericAnimationClipWriter.CreateAnimationClipAsset(pmx, vmd, 30, 0, 1, path);
                Assert.That(result.Clip, Is.Not.Null);
                Assert.That(result.AssetPath, Is.EqualTo(path));
                Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(path), Is.SameAs(result.Clip));
            }
            finally
            {
                AssetDatabase.DeleteAsset(directory);
                DestroyAssets(pmx, vmd);
            }
        }

        [Test]
        public void DefaultPathIsDeterministicAndProjectRelative()
        {
            MmdPmxAsset pmx = ScriptableObject.CreateInstance<MmdPmxAsset>();
            MmdVmdAsset vmd = ScriptableObject.CreateInstance<MmdVmdAsset>();
            pmx.name = "model";
            vmd.name = "motion";
            Assert.That(MmdGenericAnimationClipWriter.GetDefaultOutputPath(pmx, vmd), Is.EqualTo("Assets/MmdGenericClip_asset_asset.anim"));
            DestroyAssets(pmx, vmd);
        }

        [Test]
        public void DuplicateSiblingBoneNamesAreRejectedBeforeCurvesCanCollide()
        {
            var root = new GameObject("root");
            var first = new GameObject("duplicate");
            var second = new GameObject("duplicate");
            first.transform.SetParent(root.transform, false);
            second.transform.SetParent(root.transform, false);
            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                    MmdGenericAnimationClipWriter.CalculateUniqueBonePaths(
                        new[] { first.transform, second.transform }, root.transform))!;
                Assert.That(exception.Message, Does.Contain("duplicate transform path 'duplicate'"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string BindingKey(EditorCurveBinding binding) => binding.path + "|" + binding.type.FullName + "|" + binding.propertyName;

        private static void CreateAssets(string pmxPath, string vmdPath, out MmdPmxAsset pmx, out MmdVmdAsset vmd)
        {
            pmx = ScriptableObject.CreateInstance<MmdPmxAsset>();
            pmx.Initialize(File.ReadAllBytes(pmxPath), Path.GetFileName(pmxPath), pmxPath, 1.0f);
            vmd = ScriptableObject.CreateInstance<MmdVmdAsset>();
            vmd.Initialize(File.ReadAllBytes(vmdPath), Path.GetFileName(vmdPath), vmdPath);
        }

        private static void DestroyAssets(MmdPmxAsset? pmx, MmdVmdAsset? vmd)
        {
            if (pmx != null) UnityEngine.Object.DestroyImmediate(pmx);
            if (vmd != null) UnityEngine.Object.DestroyImmediate(vmd);
        }
    }
}
