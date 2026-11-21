#nullable enable

using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Editor;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    /// <summary>
    /// Characterization for the Generic clip writer (REF-D D0): exact curve output, full
    /// diagnostics in order, temporary object cleanup and in-memory clip identity.
    /// </summary>
    public sealed class MmdGenericAnimationClipWriterCharacterizationTests
    {
        private const string CubePmx = "test_1bone_cube.pmx";
        private const string CubeVmd = "test_1bone_cube_motion.vmd";
        private const string AppendPmx = "test_append_bone.pmx";
        private const string AppendVmd = "test_append_bone.vmd";

        private const string NativeEnabled = "writer: enabled persistent native evaluation for Generic bake.";
        private const string SparseBuilt = "writer: built AnimationClip exclusively from mmd-runtime generic sparse track descriptors and keys.";

        private static readonly MmdGenericAnimationClipBakeOptions Dense = new(reduceKeys: false, highPrecision: false);

        // Golden files hold every curve binding, key time, value and tangent as exact float bits.
        // The dense cases cover both the animated-parent and the static-parent local pose branches
        // (append-bone hierarchy), importScale 0.1, a nonzero startFrame and a frame rate whose
        // reciprocal is not exact.
        [TestCase("generic_clip_append_dense_s01_st1.txt", 0.1f, 30.0f, 1, 5, false)]
        [TestCase("generic_clip_append_dense_s01_24fps.txt", 0.1f, 24.0f, 0, 5, false)]
        [TestCase("generic_clip_append_sparse_s1.txt", 1.0f, 30.0f, 0, 5, true)]
        public void BakedCurvesMatchGolden(
            string goldenFile,
            float importScale,
            float frameRate,
            int startFrame,
            int endFrame,
            bool reduceKeys)
        {
#if !UNITY_EDITOR_WIN
            Assert.Ignore("mmd-runtime native generic bake is only distributed for the Windows Editor.");
#endif
            CreateAssets(AppendPmx, AppendVmd, importScale, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            AnimationClip? clip = null;
            try
            {
                MmdGenericAnimationClipWriterResult result = MmdGenericAnimationClipWriter.CreateInMemoryClip(
                    pmx,
                    vmd,
                    frameRate,
                    startFrame,
                    endFrame,
                    reduceKeys ? MmdGenericAnimationClipBakeOptions.Default : Dense);
                clip = result.Clip;
                Assert.That(clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                MmdClipCurveDump.AssertMatchesGolden(
                    MmdClipCurveDump.FormatResult(clip, result.Diagnostics),
                    Path.Combine(MmdTestFixtures.GoldenRoot, goldenFile));
            }
            finally
            {
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                DestroyAssets(pmx, vmd);
            }
        }

        [Test]
        public void InMemoryClipNameAndFrameRateArePinned()
        {
#if !UNITY_EDITOR_WIN
            Assert.Ignore("mmd-runtime native generic bake is only distributed for the Windows Editor.");
#endif
            CreateAssets(CubePmx, CubeVmd, 1.0f, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            try
            {
                foreach ((float rate, int start, int end, MmdGenericAnimationClipBakeOptions options) in new[]
                         {
                             (30.0f, 0, 9, MmdGenericAnimationClipBakeOptions.Default),
                             (24.0f, 3, 7, Dense)
                         })
                {
                    MmdGenericAnimationClipWriterResult result =
                        MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, rate, start, end, options);
                    Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                    Assert.That(
                        result.Clip!.name,
                        Is.EqualTo("MmdGenericClip_test_1bone_cube_pmx_test_1bone_cube_motion_vmd_" + start + "_" + end));
                    Assert.That(result.Clip.frameRate, Is.EqualTo(rate));
                    Assert.That(result.AssetPath, Is.Empty);
                    UnityEngine.Object.DestroyImmediate(result.Clip);
                }
            }
            finally
            {
                DestroyAssets(pmx, vmd);
            }
        }

        [Test]
        public void NullInputsReportOnlyTheFirstMissingInput()
        {
            CreateAssets(CubePmx, CubeVmd, 1.0f, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            try
            {
                AssertDiagnostics(
                    MmdGenericAnimationClipWriter.CreateInMemoryClip(null!, null!, 30.0f),
                    "validation: pmxAsset is required.");
                AssertDiagnostics(
                    MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, null!, 30.0f),
                    "validation: vmdAsset is required.");
                AssertDiagnostics(
                    MmdGenericAnimationClipWriter.CreateInMemoryClip(null!, vmd, float.NaN, -1, -5),
                    "validation: pmxAsset is required.");
            }
            finally
            {
                DestroyAssets(pmx, vmd);
            }
        }

        [TestCase(0.0f, 0, 9, "validation: frameRate must be finite and > 0.")]
        [TestCase(-30.0f, 0, 9, "validation: frameRate must be finite and > 0.")]
        [TestCase(float.NaN, 0, 9, "validation: frameRate must be finite and > 0.")]
        [TestCase(float.PositiveInfinity, -1, 9, "validation: frameRate must be finite and > 0.")]
        [TestCase(30.0f, -1, 9, "validation: startFrame must be >= 0.")]
        [TestCase(30.0f, 5, 4, "validation: endFrame must be >= startFrame.")]
        [TestCase(30.0f, 0, 50, "validation: endFrame must be <= motion max frame 49.")]
        [TestCase(30.0f, 50, 50, "validation: endFrame must be <= motion max frame 49.")]
        public void InvalidSamplingInputsReportExactDiagnostics(float rate, int start, int end, string expected)
        {
            CreateAssets(CubePmx, CubeVmd, 1.0f, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            try
            {
                AssertDiagnostics(
                    MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, rate, start, end),
                    expected);
            }
            finally
            {
                DestroyAssets(pmx, vmd);
            }
        }

        [Test]
        public void StartFrameBeyondMotionWithDefaultEndReportsEndBeforeStart()
        {
            // The explicit "startFrame must be <= motion max frame" check is unreachable today:
            // the defaulted end frame (motion max) is compared against startFrame first.
            CreateAssets(CubePmx, CubeVmd, 1.0f, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            try
            {
                AssertDiagnostics(
                    MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30.0f, 60),
                    "validation: endFrame must be >= startFrame.");
            }
            finally
            {
                DestroyAssets(pmx, vmd);
            }
        }

        [Test]
        public void NativeUnavailableReportsExactDiagnostics()
        {
            CreateAssets(CubePmx, CubeVmd, 1.0f, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            try
            {
                MmdGenericAnimationClipWriter.NativeRuntimeAvailabilityOverrideForTests = (_, _) => false;
                AssertDiagnostics(
                    MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30.0f, 0, 9),
                    "writer: failed to build Generic AnimationClip: native Generic AnimationClip evaluation unavailable; "
                    + "managed transform fallback is not supported: native runtime unavailable (forced by test).");
            }
            finally
            {
                MmdGenericAnimationClipWriter.NativeRuntimeAvailabilityOverrideForTests = null;
                DestroyAssets(pmx, vmd);
            }
        }

        [Test]
        public void DenseBudgetOverflowReportsExactDiagnostics()
        {
#if !UNITY_EDITOR_WIN
            Assert.Ignore("mmd-runtime native generic bake is only distributed for the Windows Editor.");
#endif
            CreateAssets(CubePmx, null, 1.0f, out MmdPmxAsset pmx, out MmdVmdAsset vmd, BuildTwoKeyVmd("全ての親", 600_000));
            try
            {
                AssertDiagnostics(
                    MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30.0f, 0, null, Dense),
                    NativeEnabled,
                    "writer: failed to build Generic AnimationClip: validation: requested Frame Range exceeds the safe "
                    + "dense bake budget of 4,000,000 key equivalents. Narrow Frame Range and try again.");
            }
            finally
            {
                DestroyAssets(pmx, vmd);
            }
        }

        [Test]
        public void SuccessfulBakesReportExactDiagnostics()
        {
#if !UNITY_EDITOR_WIN
            Assert.Ignore("mmd-runtime native generic bake is only distributed for the Windows Editor.");
#endif
            CreateAssets(CubePmx, CubeVmd, 1.0f, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            try
            {
                MmdGenericAnimationClipWriterResult sparse =
                    MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30.0f, 0, 9);
                AssertDiagnosticsWithClip(sparse, NativeEnabled, SparseBuilt);

                MmdGenericAnimationClipWriterResult dense =
                    MmdGenericAnimationClipWriter.CreateInMemoryClip(pmx, vmd, 30.0f, 0, 9, Dense);
                AssertDiagnosticsWithClip(
                    dense,
                    NativeEnabled,
                    "writer: key reduction disabled; baked dense curves for every sampled frame.",
                    "writer: baked Generic transform and vertex morph curves from native batch evaluation with physics off; "
                    + "compacted 0 constant curves to endpoint keys.");
            }
            finally
            {
                DestroyAssets(pmx, vmd);
            }
        }

        [TestCase("C:/outside.anim", "Rooted")]
        [TestCase("/outside.anim", "Rooted")]
        [TestCase("", "Empty")]
        [TestCase("Packages/outside.anim", "NotUnderAssets")]
        [TestCase("Assets/clip.asset", "WrongExtension")]
        [TestCase("Assets//clip.anim", "EmptyOrDotSegment")]
        [TestCase("Assets/../clip.anim", "EmptyOrDotSegment")]
        public void InvalidOutputPathReportsBakeDiagnosticsThenPathError(string outputPath, string error)
        {
#if !UNITY_EDITOR_WIN
            Assert.Ignore("mmd-runtime native generic bake is only distributed for the Windows Editor.");
#endif
            CreateAssets(CubePmx, CubeVmd, 1.0f, out MmdPmxAsset pmx, out MmdVmdAsset vmd);
            try
            {
                MmdGenericAnimationClipWriterResult result =
                    MmdGenericAnimationClipWriter.CreateAnimationClipAsset(pmx, vmd, 30.0f, 0, 9, outputPath);
                AssertDiagnostics(result, NativeEnabled, SparseBuilt, "validation: invalid output path (" + error + ").");
                Assert.That(result.AssetPath, Is.Empty);
            }
            finally
            {
                DestroyAssets(pmx, vmd);
            }
        }

        [Test]
        public void BakesDoNotLeakTemporaryObjects()
        {
#if !UNITY_EDITOR_WIN
            Assert.Ignore("mmd-runtime native generic bake is only distributed for the Windows Editor.");
#endif
            CreateAssets(AppendPmx, AppendVmd, 0.1f, out MmdPmxAsset appendPmx, out MmdVmdAsset appendVmd);
            CreateAssets(CubePmx, CubeVmd, 1.0f, out MmdPmxAsset cubePmx, out MmdVmdAsset cubeVmd);
            try
            {
                void BakeAll()
                {
                    DestroyClip(MmdGenericAnimationClipWriter.CreateInMemoryClip(appendPmx, appendVmd, 30.0f, 1, 5));
                    DestroyClip(MmdGenericAnimationClipWriter.CreateInMemoryClip(appendPmx, appendVmd, 30.0f, 1, 5, Dense));
                    DestroyClip(MmdGenericAnimationClipWriter.CreateInMemoryClip(cubePmx, cubeVmd, 30.0f, 0, 50));
                    DestroyClip(MmdGenericAnimationClipWriter.CreateAnimationClipAsset(
                        cubePmx, cubeVmd, 30.0f, 0, 9, "Assets/clip.asset"));
                    MmdGenericAnimationClipWriter.NativeRuntimeAvailabilityOverrideForTests = (_, _) => false;
                    try
                    {
                        DestroyClip(MmdGenericAnimationClipWriter.CreateInMemoryClip(cubePmx, cubeVmd, 30.0f, 0, 9));
                    }
                    finally
                    {
                        MmdGenericAnimationClipWriter.NativeRuntimeAvailabilityOverrideForTests = null;
                    }
                }

                BakeAll();
                string before = CountObjects();
                BakeAll();
                Assert.That(CountObjects(), Is.EqualTo(before));
            }
            finally
            {
                DestroyAssets(appendPmx, appendVmd);
                DestroyAssets(cubePmx, cubeVmd);
            }
        }

        internal static string CountObjects()
        {
            return "GameObject=" + Resources.FindObjectsOfTypeAll<GameObject>().Length
                   + " Mesh=" + Resources.FindObjectsOfTypeAll<Mesh>().Length
                   + " Material=" + Resources.FindObjectsOfTypeAll<Material>().Length
                   + " Texture2D=" + Resources.FindObjectsOfTypeAll<Texture2D>().Length
                   + " AnimationClip=" + Resources.FindObjectsOfTypeAll<AnimationClip>().Length
                   + " Avatar=" + Resources.FindObjectsOfTypeAll<Avatar>().Length;
        }

        internal static byte[] BuildTwoKeyVmd(string boneName, uint lastFrame, string modelName = "")
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            WriteFixedSjis(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedSjis(writer, modelName, 20);
            writer.Write(2u);
            foreach ((uint frame, float x) in new[] { (0u, 0.0f), (lastFrame, 1.0f) })
            {
                WriteFixedSjis(writer, boneName, 15);
                writer.Write(frame);
                writer.Write(x);
                writer.Write(0.0f);
                writer.Write(0.0f);
                writer.Write(0.0f);
                writer.Write(0.0f);
                writer.Write(0.0f);
                writer.Write(1.0f);
                var interpolation = new byte[64];
                for (int channel = 0; channel < 4; channel++)
                {
                    interpolation[channel] = 20;
                    interpolation[channel + 4] = 20;
                    interpolation[channel + 8] = 107;
                    interpolation[channel + 12] = 107;
                }

                writer.Write(interpolation);
            }

            writer.Write(0u); // morph count
            writer.Write(0u); // camera count
            writer.Write(0u); // light count
            writer.Write(0u); // self-shadow count
            writer.Write(0u); // show/IK count
            return stream.ToArray();
        }

        private static void WriteFixedSjis(BinaryWriter writer, string value, int byteCount)
        {
            byte[] buffer = new byte[byteCount];
            byte[] encoded = Encoding.GetEncoding(932).GetBytes(value);
            Array.Copy(encoded, 0, buffer, 0, Math.Min(encoded.Length, buffer.Length));
            writer.Write(buffer);
        }

        private static void AssertDiagnostics(MmdGenericAnimationClipWriterResult result, params string[] expected)
        {
            Assert.That(result.Clip, Is.Null, string.Join("\n", result.Diagnostics));
            Assert.That(result.Diagnostics, Is.EqualTo(expected), string.Join("\n", result.Diagnostics));
        }

        private static void AssertDiagnosticsWithClip(MmdGenericAnimationClipWriterResult result, params string[] expected)
        {
            try
            {
                Assert.That(result.Clip, Is.Not.Null, string.Join("\n", result.Diagnostics));
                Assert.That(result.Diagnostics, Is.EqualTo(expected), string.Join("\n", result.Diagnostics));
            }
            finally
            {
                DestroyClip(result);
            }
        }

        private static void DestroyClip(MmdGenericAnimationClipWriterResult result)
        {
            if (result.Clip != null) UnityEngine.Object.DestroyImmediate(result.Clip);
        }

        private static void CreateAssets(
            string pmxFile,
            string? vmdFile,
            float importScale,
            out MmdPmxAsset pmx,
            out MmdVmdAsset vmd,
            byte[]? vmdBytes = null)
        {
            string pmxPath = MmdTestFixtures.FixtureAssetPath(pmxFile);
            pmx = ScriptableObject.CreateInstance<MmdPmxAsset>();
            pmx.Initialize(File.ReadAllBytes(pmxPath), Path.GetFileName(pmxPath), pmxPath, importScale);
            vmd = ScriptableObject.CreateInstance<MmdVmdAsset>();
            if (vmdFile != null)
            {
                string vmdPath = MmdTestFixtures.FixtureAssetPath(vmdFile);
                vmd.Initialize(File.ReadAllBytes(vmdPath), Path.GetFileName(vmdPath), vmdPath);
            }
            else
            {
                vmd.Initialize(vmdBytes!, "generated.vmd", "Assets/generated.vmd");
            }
        }

        private static void DestroyAssets(MmdPmxAsset? pmx, MmdVmdAsset? vmd)
        {
            if (pmx != null) UnityEngine.Object.DestroyImmediate(pmx);
            if (vmd != null) UnityEngine.Object.DestroyImmediate(vmd);
        }
    }
}
