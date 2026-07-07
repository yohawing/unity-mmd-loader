#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Mmd.Motion;
using Mmd.Native;
using Mmd.Parser;
using UnityEngine;

namespace Mmd.Tests.Contracts
{
    [TestFixture]
    public sealed class MmdNativeParityTests
    {
        [Test]
        public void NativeEvaluatorProducesConsistentWorldMatrices()
        {
            // MmdRuntimeFrameEvaluator now uses native FFI internally.
            // Verify that the evaluator's row-major output matches a direct native session call.
            RunNativeConsistencyCheck(
                "test_append_bone.pmx",
                "test_append_bone.vmd",
                new[] { 0, 15, 30 },
                maxAllowedError: 0.00001f);
        }

        [Test]
        public void SingleBoneCubeNativeEvaluatorConsistency()
        {
            RunNativeConsistencyCheck(
                "test_1bone_cube.pmx",
                "test_1bone_cube_motion.vmd",
                new[] { 0, 15, 30 },
                maxAllowedError: 0.00001f);
        }

        [Test]
        public void MorphWeightsMatchBetweenEvaluatorAndDirectSession()
        {
            RunMorphConsistencyCheck(
                "test_append_bone.pmx",
                "test_append_bone.vmd",
                new[] { 0, 15, 30 },
                maxAllowedMorphError: 0.00001f);
        }

        [Test]
        public void NativeCameraSamplingProducesSensibleDefaults()
        {
            byte[] vmdBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_append_bone.vmd");

            var parser = new NativeMmdParser();
            MmdMotionDefinition motion = parser.LoadMotion(vmdBytes);

            if (motion.cameraKeyframes == null || motion.cameraKeyframes.Count == 0)
            {
                Assert.Ignore("No camera keyframes in test_append_bone.vmd");
                return;
            }

            if (!NativeVmdCameraTrackSampler.TryCreate(vmdBytes, out var cameraSampler))
            {
                Assert.Fail("Failed to create camera track sampler");
            }

            using (cameraSampler)
            {
                Assert.That(cameraSampler!.TrySample(0.0f, out _), Is.True, "Native camera sampling should succeed");
            }
        }

        [Test]
        public void NativeLightSamplingProducesSensibleDefaults()
        {
            byte[] vmdBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_append_bone.vmd");

            var parser = new NativeMmdParser();
            MmdMotionDefinition motion = parser.LoadMotion(vmdBytes);

            if (motion.lightKeyframes == null || motion.lightKeyframes.Count == 0)
            {
                Assert.Ignore("No light keyframes in test_append_bone.vmd");
                return;
            }

            if (!NativeVmdLightTrackSampler.TryCreate(vmdBytes, out var lightSampler))
            {
                Assert.Fail("Failed to create light track sampler");
            }

            using (lightSampler)
            {
                Assert.That(lightSampler!.TrySample(0.0f, out _), Is.True, "Native light sampling should succeed");
            }
        }

        private static void RunNativeConsistencyCheck(
            string pmxFixture,
            string vmdFixture,
            int[] frames,
            float maxAllowedError)
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes(pmxFixture);
            byte[] vmdBytes = MmdTestFixtures.ReadFixtureAssetBytes(vmdFixture);

            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(pmxBytes);
            MmdMotionDefinition motion = parser.LoadMotion(vmdBytes);
            int boneCount = model.bones.Count;

            using var session = MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes);
            float[] nativeWorldMatrices = new float[session.WorldMatrixFloatCount];
            float[] nativeMorphWeights = new float[session.MorphWeightCount];
            byte[] nativeIkEnabled = new byte[session.IkEnabledCount];

            var report = new StringBuilder();
            report.AppendLine($"=== Native Consistency Report: {pmxFixture} + {vmdFixture} ===");
            report.AppendLine($"Bones: {boneCount}, Frames: {string.Join(",", frames)}");

            float globalMaxError = 0.0f;

            foreach (int frame in frames)
            {
                session.EvaluateAndCopy(frame, nativeWorldMatrices, nativeMorphWeights, nativeIkEnabled);

                MmdEvaluatedFrame evaluated = MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrame(
                    model, motion, frame, frame / 30.0f);

                float frameMaxError = 0.0f;
                string worstBoneName = string.Empty;

                for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
                {
                    MmdEvaluatedBonePose bonePose = evaluated.bones[boneIndex];
                    int nativeOffset = boneIndex * 16;

                    // Evaluator stores row-major; native is column-major.
                    // Row-major translation: indices 3, 7, 11
                    // Column-major translation: indices 12, 13, 14
                    float tx = Math.Abs(bonePose.worldMatrix[3] - nativeWorldMatrices[nativeOffset + 12]);
                    float ty = Math.Abs(bonePose.worldMatrix[7] - nativeWorldMatrices[nativeOffset + 13]);
                    float tz = Math.Abs(bonePose.worldMatrix[11] - nativeWorldMatrices[nativeOffset + 14]);
                    float translationError = MathF.Sqrt(tx * tx + ty * ty + tz * tz);

                    if (translationError > frameMaxError)
                    {
                        frameMaxError = translationError;
                        worstBoneName = bonePose.name;
                    }

                    if (translationError > globalMaxError)
                        globalMaxError = translationError;
                }

                report.AppendLine(
                    $"  Frame {frame}: maxErr={frameMaxError:F6} (bone={worstBoneName})");
            }

            report.AppendLine($"  Global maxErr: {globalMaxError:F6}, threshold: {maxAllowedError:F6}");
            report.AppendLine(globalMaxError <= maxAllowedError ? "  PASS" : "  FAIL");

            Debug.Log(report.ToString());
            WriteParityReport(pmxFixture, "native_consistency", report.ToString());

            Assert.That(globalMaxError, Is.LessThanOrEqualTo(maxAllowedError),
                $"Max translation error {globalMaxError:F6} exceeds threshold {maxAllowedError:F6}.\n" + report);
        }

        private static void RunMorphConsistencyCheck(
            string pmxFixture,
            string vmdFixture,
            int[] frames,
            float maxAllowedMorphError)
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes(pmxFixture);
            byte[] vmdBytes = MmdTestFixtures.ReadFixtureAssetBytes(vmdFixture);

            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(pmxBytes);
            MmdMotionDefinition motion = parser.LoadMotion(vmdBytes);

            using var session = MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes);
            float[] nativeWorldMatrices = new float[session.WorldMatrixFloatCount];
            float[] nativeMorphWeights = new float[session.MorphWeightCount];
            byte[] nativeIkEnabled = new byte[session.IkEnabledCount];

            var report = new StringBuilder();
            report.AppendLine($"=== Morph Consistency Report: {pmxFixture} + {vmdFixture} ===");

            float globalMaxError = 0.0f;

            foreach (int frame in frames)
            {
                session.EvaluateAndCopy(frame, nativeWorldMatrices, nativeMorphWeights, nativeIkEnabled);

                MmdEvaluatedFrame evaluated = MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrame(
                    model, motion, frame, frame / 30.0f);

                var evaluatedMorphMap = new Dictionary<string, float>();
                foreach (MmdEvaluatedMorphWeight morph in evaluated.morphs)
                    evaluatedMorphMap[morph.name] = morph.weight;

                float frameMaxError = 0.0f;
                for (int morphIndex = 0; morphIndex < model.morphs.Count && morphIndex < nativeMorphWeights.Length; morphIndex++)
                {
                    string morphName = model.morphs[morphIndex].name;
                    float directWeight = nativeMorphWeights[morphIndex];
                    evaluatedMorphMap.TryGetValue(morphName, out float evaluatedWeight);
                    float diff = Math.Abs(directWeight - evaluatedWeight);
                    if (diff > frameMaxError) frameMaxError = diff;
                    if (diff > globalMaxError) globalMaxError = diff;
                }

                report.AppendLine($"  Frame {frame}: maxErr={frameMaxError:F6}");
            }

            report.AppendLine($"  Global maxErr: {globalMaxError:F6}, threshold: {maxAllowedMorphError:F6}");
            report.AppendLine(globalMaxError <= maxAllowedMorphError ? "  PASS" : "  FAIL");

            Debug.Log(report.ToString());
            WriteParityReport(pmxFixture, "morph_consistency", report.ToString());

            Assert.That(globalMaxError, Is.LessThanOrEqualTo(maxAllowedMorphError),
                $"Max morph weight error {globalMaxError:F6} exceeds threshold {maxAllowedMorphError:F6}.\n" + report);
        }

        private static void WriteParityReport(string fixture, string channel, string report)
        {
            string repoRoot = MmdTestFixtures.RepositoryRoot;
            string reportDir = Path.Combine(repoRoot, "artifacts", "parity");
            if (!Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }

            string safeName = Path.GetFileNameWithoutExtension(fixture);
            string reportPath = Path.Combine(reportDir, $"{safeName}_{channel}_parity.txt");
            File.WriteAllText(reportPath, report);
        }
    }
}
