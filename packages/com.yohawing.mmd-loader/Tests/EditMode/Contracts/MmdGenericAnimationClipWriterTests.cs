#nullable enable

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Editor;
using Mmd.Native;
using Mmd.Physics;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdGenericAnimationClipWriterTests
    {
        private const string CubePmx = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube.pmx";
        private const string CubeVmd = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_1bone_cube_motion.vmd";
        private const string MorphPmx = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_vertex_morph.pmx";
        private const string MorphVmd = "Packages/com.yohawing.mmd-loader/Tests/Fixtures/Assets/test_vertex_morph_motion.vmd";

        [Test]
        public void BakeUsesNativeSparseTranslationAndEulerCurvesWithPhysicsOff()
        {
            CreateAssets(CubePmx, CubeVmd, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            try
            {
                MmdGenericAnimationClipWriterResult result =
                    MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30.0f, 0, 9);

                Assert.That(result.Clip, Is.Not.Null);
                Assert.That(result.PhysicsMode, Is.EqualTo(MmdPhysicsMode.Off));
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(result.Clip!);
                EditorCurveBinding[] transformBindings = bindings
                    .Where(binding => binding.type == typeof(Transform))
                    .ToArray();
#if UNITY_EDITOR_WIN
                Assert.That(transformBindings, Has.Length.EqualTo(6));
                Assert.That(transformBindings.Count(binding => binding.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal)), Is.EqualTo(3));
                Assert.That(transformBindings.Count(binding => binding.propertyName.StartsWith("localEulerAnglesRaw.", StringComparison.Ordinal)), Is.EqualTo(3));
#else
                Assert.That(transformBindings, Has.Length.EqualTo(7));
#endif
                int[] keyCounts = bindings
                    .Where(binding => binding.type == typeof(Transform))
                    .Select(binding => AnimationUtility.GetEditorCurve(result.Clip!, binding)!.keys.Length)
                    .ToArray();
                Assert.That(result.Diagnostics, Has.Some.Contains("persistent native evaluation"));
#if UNITY_EDITOR_WIN
                Assert.That(keyCounts.Max(), Is.LessThan(10), "native reducer output remains sparse");
                Assert.That(result.Diagnostics, Has.Some.Contains("exclusively from mmd-runtime sparse curve descriptors and keys"));
#else
                Assert.That(keyCounts, Does.Contain(10));
                Assert.That(result.Diagnostics, Has.Some.Contains("compacted"));
#endif
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
#if UNITY_EDITOR_WIN
                Assert.That(curve.keys.Length, Is.LessThan(11));
                Assert.That(curve.keys.All(key => float.IsFinite(key.inTangent) && float.IsFinite(key.outTangent)), Is.True);
                Assert.That(result.Diagnostics, Has.Some.Contains("sparse curve descriptors and keys"));
#else
                Assert.That(curve.keys, Has.Length.EqualTo(11));
#endif
                Assert.That(curve.keys.Max(key => key.value), Is.GreaterThan(0.0f).And.LessThanOrEqualTo(100.0f));
                UnityEngine.Object.DestroyImmediate(result.Clip);
            }
            finally { DestroyAssets(pmx, vmd); }
        }

        [Test]
        public void BatchMorphCurveMatchesFastRuntimeApplication()
        {
#if !UNITY_EDITOR_WIN
            Assert.Ignore("mmd-runtime batch playback is only distributed for the Windows Editor.");
#endif
            CreateAssets(MorphPmx, MorphVmd, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            MmdUnityPlaybackBinding? binding = null;
            AnimationClip? clip = null;
            try
            {
                MmdGenericAnimationClipWriterResult result =
                    MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30.0f, 0, 10);
                clip = result.Clip;
                Assert.That(clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                Assert.That(result.Diagnostics, Has.Some.Contains("sparse curve descriptors and keys"));

                binding = MmdUnityPlaybackBinding.CreateSkinned(pmx, vmd);
                binding.SetPhysicsMode(MmdPhysicsMode.Off);
                Assert.That(
                    binding.TryEnableFastRuntime(pmx.GetBytesCopy(), vmd.GetBytesCopy(), out string reason),
                    Is.True,
                    reason);

                foreach (MmdUnityVertexMorphBlendShapeBinding morph in binding.Instance.VertexMorphBlendShapes)
                {
                    EditorCurveBinding curveBinding = AnimationUtility.GetCurveBindings(clip!)
                        .Single(candidate =>
                            candidate.type == typeof(SkinnedMeshRenderer) &&
                            candidate.propertyName == "blendShape." + morph.BlendShapeName);
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip!, curveBinding)!;
                    foreach (int frame in new[] { 0, 5, 10 })
                    {
                        binding.ApplyFrame(frame, 30.0f);
                        float appliedWeight = binding.Instance.SkinnedMeshRenderer!
                            .GetBlendShapeWeight(morph.BlendShapeIndex);
                        Assert.That(
                            curve.Evaluate(frame / 30.0f),
                            Is.EqualTo(appliedWeight).Within(0.011f),
                            $"fast-runtime morph parity for {morph.MorphName} at frame {frame}");
                    }
                }
            }
            finally
            {
                binding?.Dispose();
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                DestroyAssets(pmx, vmd);
            }
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

        [Test]
        public void NativeTranslationCurveAppliesDefaultHostScaleToValuesAndTangentsOnly()
        {
            var native = new MmdRuntimeFfiMethods.UnityCurveKey
            {
                timeSeconds = 2.0f,
                value = 3.0f,
                inTangent = 4.0f,
                outTangent = -5.0f
            };

            Keyframe key = MmdGenericAnimationClipWriter.CreateUnityKeyframe(native, 0.1f);

            Assert.That(key.time, Is.EqualTo(2.0f));
            Assert.That(key.value, Is.EqualTo(0.3f).Within(1.0e-6f));
            Assert.That(key.inTangent, Is.EqualTo(0.4f).Within(1.0e-6f));
            Assert.That(key.outTangent, Is.EqualTo(-0.5f).Within(1.0e-6f));
        }

        [Test]
        public void SparseEulerCurvesUseNativeXyzRotationOrderForMultiAxisPose()
        {
            var root = new GameObject("root");
            var bone = new GameObject("bone");
            bone.transform.SetParent(root.transform, false);
            var clip = new AnimationClip();
            try
            {
                EditorCurveBinding[] bindings =
                {
                    EditorCurveBinding.FloatCurve("bone", typeof(Transform), "localEulerAnglesRaw.x"),
                    EditorCurveBinding.FloatCurve("bone", typeof(Transform), "localEulerAnglesRaw.y"),
                    EditorCurveBinding.FloatCurve("bone", typeof(Transform), "localEulerAnglesRaw.z")
                };
                AnimationCurve[] curves =
                {
                    new AnimationCurve(new Keyframe(0.0f, 30.0f)),
                    new AnimationCurve(new Keyframe(0.0f, 40.0f)),
                    new AnimationCurve(new Keyframe(0.0f, 50.0f))
                };
                AnimationUtility.SetEditorCurves(clip, bindings, curves);

                MmdGenericAnimationClipWriter.SetSparseEulerRotationOrderToXyz(clip);

                var serializedClip = new SerializedObject(clip);
                SerializedProperty eulerCurves = serializedClip.FindProperty("m_EulerCurves");
                Assert.That(
                    eulerCurves.GetArrayElementAtIndex(0)
                        .FindPropertyRelative("curve.m_RotationOrder").intValue,
                    Is.EqualTo(0));
                clip.SampleAnimation(root, 0.0f);
                Quaternion expected =
                    Quaternion.AngleAxis(50.0f, Vector3.forward) *
                    Quaternion.AngleAxis(40.0f, Vector3.up) *
                    Quaternion.AngleAxis(30.0f, Vector3.right);
                Assert.That(Quaternion.Angle(bone.transform.localRotation, expected), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SparseFallbackAllowlistDoesNotHideContractOrOverflowFailures()
        {
            Assert.That(MmdGenericAnimationClipWriter.IsSparseNativeFallbackException(
                new DllNotFoundException()), Is.True);
            Assert.That(MmdGenericAnimationClipWriter.IsSparseNativeFallbackException(
                new EntryPointNotFoundException()), Is.True);
            Assert.That(MmdGenericAnimationClipWriter.IsSparseNativeFallbackException(
                new BadImageFormatException()), Is.True);
            Assert.That(MmdGenericAnimationClipWriter.IsSparseNativeFallbackException(
                new MmdRuntimeUnsupportedException("unsupported")), Is.True);
            Assert.That(MmdGenericAnimationClipWriter.IsSparseNativeFallbackException(
                new MmdRuntimeReductionInputTooLargeException("safety limit")), Is.False);
            Assert.That(MmdGenericAnimationClipWriter.IsSparseNativeFallbackException(
                new InvalidOperationException("descriptor mismatch")), Is.False);
            Assert.That(MmdGenericAnimationClipWriter.IsSparseNativeFallbackException(
                new OverflowException("count overflow")), Is.False);
        }

        [Test]
        public void NativeSparseBakeRemainsEnabledAtDefaultPointOneImportScale()
        {
#if !UNITY_EDITOR_WIN
            Assert.Ignore("mmd-runtime sparse curve baking is only distributed for the Windows Editor.");
#endif
            CreateAssets(CubePmx, CubeVmd, out MmdPmxAsset pmx, out MmdVmdAsset vmd, 0.1f);
            try
            {
                MmdGenericAnimationClipWriterResult result =
                    MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30.0f, 0, 9);
                Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                Assert.That(result.Diagnostics, Has.Some.Contains("exclusively from mmd-runtime sparse curve descriptors and keys"));
                EditorCurveBinding[] translations = AnimationUtility.GetCurveBindings(result.Clip!)
                    .Where(binding => binding.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal))
                    .ToArray();
                Assert.That(translations, Has.Length.EqualTo(3));
                Assert.That(translations.All(binding =>
                    AnimationUtility.GetEditorCurve(result.Clip!, binding)!.keys.All(key =>
                        float.IsFinite(key.value) &&
                        float.IsFinite(key.inTangent) &&
                        float.IsFinite(key.outTangent))), Is.True);
                UnityEngine.Object.DestroyImmediate(result.Clip);
            }
            finally
            {
                DestroyAssets(pmx, vmd);
            }
        }

        [Test]
        public void NativeSparseTransformCurvesMatchFastRuntimeWithNonzeroStartAndDefaultScale()
        {
#if !UNITY_EDITOR_WIN
            Assert.Ignore("mmd-runtime sparse curve baking is only distributed for the Windows Editor.");
#endif
            const float FrameRate = 30.0f;
            const int StartFrame = 2;
            const int EndFrame = 9;
            const float ImportScale = MmdPmxAsset.DefaultImportScale;
            CreateAssets(CubePmx, CubeVmd, out MmdPmxAsset pmx, out MmdVmdAsset vmd, ImportScale);
            MmdUnityPlaybackBinding? binding = null;
            AnimationClip? clip = null;
            try
            {
                MmdGenericAnimationClipWriterResult result =
                    MmdGenericAnimationClipWriter.CreateInMemoryClip(
                        pmx, vmd, FrameRate, StartFrame, EndFrame);
                clip = result.Clip;
                Assert.That(clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                Assert.That(result.Diagnostics, Has.Some.Contains(
                    "exclusively from mmd-runtime sparse curve descriptors and keys"));
                Assert.That(pmx.ImportScale, Is.EqualTo(ImportScale));

                binding = MmdUnityPlaybackBinding.CreateSkinned(pmx, vmd);
                binding.SetPhysicsMode(MmdPhysicsMode.Off);
                Assert.That(
                    binding.TryEnableFastRuntime(pmx.GetBytesCopy(), vmd.GetBytesCopy(), out string reason),
                    Is.True,
                    reason);

                Transform bone = binding.Instance.BoneTransforms[0];
                string path = AnimationUtility.CalculateTransformPath(
                    bone, binding.Instance.Root.transform);
                AnimationCurve px = GetTransformCurve(clip!, path, "m_LocalPosition.x");
                AnimationCurve py = GetTransformCurve(clip!, path, "m_LocalPosition.y");
                AnimationCurve pz = GetTransformCurve(clip!, path, "m_LocalPosition.z");
                AnimationCurve rx = GetTransformCurve(clip!, path, "localEulerAnglesRaw.x");
                AnimationCurve ry = GetTransformCurve(clip!, path, "localEulerAnglesRaw.y");
                AnimationCurve rz = GetTransformCurve(clip!, path, "localEulerAnglesRaw.z");
                float expectedDuration = (EndFrame - StartFrame) / FrameRate;
                foreach (AnimationCurve curve in new[] { px, py, pz, rx, ry, rz })
                {
                    Assert.That(curve.keys[0].time, Is.EqualTo(0.0f).Within(1.0e-7f),
                        "reduced time origin must be clip-relative even when startFrame is nonzero");
                    Assert.That(curve.keys[curve.keys.Length - 1].time,
                        Is.EqualTo(expectedDuration).Within(1.0e-6f));
                }

                for (int frame = StartFrame; frame <= EndFrame; frame++)
                {
                    binding.ApplyFrame(frame, FrameRate);
                    Vector3 expectedPosition = bone.localPosition;
                    Quaternion expectedRotation = bone.localRotation;
                    float time = (frame - StartFrame) / FrameRate;
                    clip!.SampleAnimation(binding.Instance.Root, time);
                    Vector3 sparsePosition = bone.localPosition;
                    Quaternion sparseRotation = bone.localRotation;

                    Assert.That(Vector3.Distance(sparsePosition, expectedPosition),
                        Is.LessThanOrEqualTo(
                            MmdRuntimeFfiMethods.ReductionTolerances.UnityPositionTolerance + 1.0e-5f),
                        "translation interpolation must remain within the Unity-space reduction contract");
                    Assert.That(Quaternion.Angle(sparseRotation, expectedRotation),
                        Is.LessThanOrEqualTo(
                            MmdRuntimeFfiMethods.ReductionTolerances.RotationToleranceRadians
                            * Mathf.Rad2Deg + 0.001f),
                        "rotation interpolation must remain within the reduction contract");
                }
            }
            finally
            {
                binding?.Dispose();
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                DestroyAssets(pmx, vmd);
            }
        }

        private static string BindingKey(EditorCurveBinding binding) => binding.path + "|" + binding.type.FullName + "|" + binding.propertyName;

        private static AnimationCurve GetTransformCurve(
            AnimationClip clip,
            string path,
            string propertyName)
        {
            EditorCurveBinding curveBinding = AnimationUtility.GetCurveBindings(clip).Single(binding =>
                binding.path == path &&
                binding.type == typeof(Transform) &&
                binding.propertyName == propertyName);
            return AnimationUtility.GetEditorCurve(clip, curveBinding)!;
        }

        private static void CreateAssets(
            string pmxPath,
            string vmdPath,
            out MmdPmxAsset pmx,
            out MmdVmdAsset vmd,
            float importScale = 1.0f)
        {
            pmx = ScriptableObject.CreateInstance<MmdPmxAsset>();
            pmx.Initialize(File.ReadAllBytes(pmxPath), Path.GetFileName(pmxPath), pmxPath, importScale);
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
