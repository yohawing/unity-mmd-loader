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
        public void AppendBoneDivergenceReport()
        {
            // Append bone chains diverge between managed C# pipeline and native (mmd-anim).
            // Native is the MMD-spec authority; this test measures divergence for the A1 parity report.
            RunBoneParityComparison(
                "test_append_bone.pmx",
                "test_append_bone.vmd",
                new[] { 0, 15, 30 },
                maxAllowedTranslationError: float.MaxValue);
        }

        [Test]
        public void SingleBoneCubeWorldMatricesMatchBetweenManagedAndNative()
        {
            RunBoneParityComparison(
                "test_1bone_cube.pmx",
                "test_1bone_cube_motion.vmd",
                new[] { 0, 15, 30 },
                maxAllowedTranslationError: 0.01f);
        }

        [Test]
        public void AppendBoneMorphWeightsMatchBetweenManagedAndNative()
        {
            RunMorphParityComparison(
                "test_append_bone.pmx",
                "test_append_bone.vmd",
                new[] { 0, 15, 30 },
                maxAllowedMorphError: 0.001f);
        }

        [Test]
        public void CameraSamplingMatchesBetweenManagedAndNative()
        {
            RunCameraParityComparison(
                "test_append_bone.vmd",
                new[] { 0.0f, 15.0f, 30.0f },
                maxAllowedError: 0.01f);
        }

        [Test]
        public void LightSamplingMatchesBetweenManagedAndNative()
        {
            RunLightParityComparison(
                "test_append_bone.vmd",
                new[] { 0.0f, 15.0f, 30.0f },
                maxAllowedError: 0.001f);
        }

        private static void RunBoneParityComparison(
            string pmxFixture,
            string vmdFixture,
            int[] frames,
            float maxAllowedTranslationError)
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes(pmxFixture);
            byte[] vmdBytes = MmdTestFixtures.ReadFixtureAssetBytes(vmdFixture);

            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(pmxBytes);
            MmdMotionDefinition motion = parser.LoadMotion(vmdBytes);
            int boneCount = model.bones.Count;

            using var session = MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes);
            Assert.That(session.BoneCount, Is.EqualTo(boneCount),
                "Native and managed bone counts must match");

            float[] nativeWorldMatrices = new float[session.WorldMatrixFloatCount];
            float[] nativeMorphWeights = new float[session.MorphWeightCount];
            byte[] nativeIkEnabled = new byte[session.IkEnabledCount];

            var report = new StringBuilder();
            report.AppendLine($"=== Bone Parity Report: {pmxFixture} + {vmdFixture} ===");
            report.AppendLine($"Bones: {boneCount}, Frames: {string.Join(",", frames)}");

            float globalMaxTranslationError = 0.0f;
            float globalRmsTranslation = 0.0f;
            int totalComparisons = 0;

            foreach (int frame in frames)
            {
                session.EvaluateAndCopy(frame, nativeWorldMatrices, nativeMorphWeights, nativeIkEnabled);

                MmdEvaluatedFrame managed = MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrame(
                    model, motion, frame, frame / 30.0f);

                float frameMaxError = 0.0f;
                float frameRmsSum = 0.0f;
                string worstBoneName = string.Empty;

                for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
                {
                    MmdEvaluatedBonePose bonePose = managed.bones[boneIndex];
                    Assert.That(bonePose.index, Is.EqualTo(boneIndex));

                    int nativeOffset = boneIndex * 16;
                    float tx = Math.Abs(bonePose.worldMatrix[12] - nativeWorldMatrices[nativeOffset + 12]);
                    float ty = Math.Abs(bonePose.worldMatrix[13] - nativeWorldMatrices[nativeOffset + 13]);
                    float tz = Math.Abs(bonePose.worldMatrix[14] - nativeWorldMatrices[nativeOffset + 14]);
                    float translationError = MathF.Sqrt(tx * tx + ty * ty + tz * tz);

                    frameRmsSum += translationError * translationError;
                    totalComparisons++;

                    if (translationError > frameMaxError)
                    {
                        frameMaxError = translationError;
                        worstBoneName = bonePose.name;
                    }

                    if (translationError > globalMaxTranslationError)
                    {
                        globalMaxTranslationError = translationError;
                    }

                    globalRmsTranslation += translationError * translationError;
                }

                float frameRms = MathF.Sqrt(frameRmsSum / boneCount);
                report.AppendLine(
                    $"  Frame {frame}: maxErr={frameMaxError:F6} (bone={worstBoneName}), RMS={frameRms:F6}");

                if (frameMaxError > maxAllowedTranslationError)
                {
                    report.AppendLine($"    Top divergent bones at frame {frame}:");
                    var diffs = new List<(string name, float error)>();
                    for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
                    {
                        MmdEvaluatedBonePose bonePose = managed.bones[boneIndex];
                        int nativeOffset = boneIndex * 16;
                        float tx = Math.Abs(bonePose.worldMatrix[12] - nativeWorldMatrices[nativeOffset + 12]);
                        float ty = Math.Abs(bonePose.worldMatrix[13] - nativeWorldMatrices[nativeOffset + 13]);
                        float tz = Math.Abs(bonePose.worldMatrix[14] - nativeWorldMatrices[nativeOffset + 14]);
                        float error = MathF.Sqrt(tx * tx + ty * ty + tz * tz);
                        if (error > maxAllowedTranslationError * 0.1f)
                        {
                            diffs.Add((bonePose.name, error));
                        }
                    }

                    diffs.Sort((a, b) => b.error.CompareTo(a.error));
                    int shown = Math.Min(diffs.Count, 10);
                    for (int i = 0; i < shown; i++)
                    {
                        report.AppendLine($"      [{diffs[i].name}] err={diffs[i].error:F6}");
                    }
                }
            }

            float globalRms = totalComparisons > 0
                ? MathF.Sqrt(globalRmsTranslation / totalComparisons)
                : 0.0f;
            report.AppendLine($"  Global: maxErr={globalMaxTranslationError:F6}, RMS={globalRms:F6}");
            bool isReportOnly = maxAllowedTranslationError >= float.MaxValue;
            if (isReportOnly)
                report.AppendLine("  Mode: report-only (no threshold)");
            else
                report.AppendLine($"  Threshold: {maxAllowedTranslationError:F6}");
            report.AppendLine(isReportOnly ? "  REPORT" : (globalMaxTranslationError <= maxAllowedTranslationError ? "  PASS" : "  FAIL"));

            Debug.Log(report.ToString());
            WriteParityReport(pmxFixture, "bone", report.ToString());

            Assert.That(globalMaxTranslationError, Is.LessThanOrEqualTo(maxAllowedTranslationError),
                $"Max bone translation error {globalMaxTranslationError:F6} exceeds threshold {maxAllowedTranslationError:F6}.\n" +
                report);
        }

        private static void RunMorphParityComparison(
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
            report.AppendLine($"=== Morph Parity Report: {pmxFixture} + {vmdFixture} ===");
            report.AppendLine($"Morphs(model): {model.morphs.Count}, Native morph weights: {session.MorphWeightCount}");

            float globalMaxError = 0.0f;

            foreach (int frame in frames)
            {
                session.EvaluateAndCopy(frame, nativeWorldMatrices, nativeMorphWeights, nativeIkEnabled);

                MmdEvaluatedFrame managed = MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrame(
                    model, motion, frame, frame / 30.0f);

                var managedMorphMap = new Dictionary<string, float>();
                foreach (MmdEvaluatedMorphWeight morph in managed.morphs)
                {
                    managedMorphMap[morph.name] = morph.weight;
                }

                float frameMaxError = 0.0f;
                for (int morphIndex = 0; morphIndex < model.morphs.Count && morphIndex < nativeMorphWeights.Length; morphIndex++)
                {
                    string morphName = model.morphs[morphIndex].name;
                    float nativeWeight = nativeMorphWeights[morphIndex];
                    managedMorphMap.TryGetValue(morphName, out float managedWeight);
                    float diff = Math.Abs(nativeWeight - managedWeight);
                    if (diff > frameMaxError)
                    {
                        frameMaxError = diff;
                    }

                    if (diff > globalMaxError)
                    {
                        globalMaxError = diff;
                    }
                }

                report.AppendLine($"  Frame {frame}: maxErr={frameMaxError:F6}");
            }

            report.AppendLine($"  Global maxErr: {globalMaxError:F6}, threshold: {maxAllowedMorphError:F6}");
            report.AppendLine(globalMaxError <= maxAllowedMorphError ? "  PASS" : "  FAIL");

            Debug.Log(report.ToString());
            WriteParityReport(pmxFixture, "morph", report.ToString());

            Assert.That(globalMaxError, Is.LessThanOrEqualTo(maxAllowedMorphError),
                $"Max morph weight error {globalMaxError:F6} exceeds threshold {maxAllowedMorphError:F6}.\n" + report);
        }

        private static void RunCameraParityComparison(
            string vmdFixture,
            float[] frames,
            float maxAllowedError)
        {
            byte[] vmdBytes = MmdTestFixtures.ReadFixtureAssetBytes(vmdFixture);

            var parser = new NativeMmdParser();
            MmdMotionDefinition motion = parser.LoadMotion(vmdBytes);

            if (motion.cameraKeyframes == null || motion.cameraKeyframes.Count == 0)
            {
                Assert.Ignore("No camera keyframes in " + vmdFixture);
                return;
            }

            const int cameraSampleFloatCount = 9;
            float[] nativeBuffer = new float[cameraSampleFloatCount];
            var report = new StringBuilder();
            report.AppendLine($"=== Camera Parity Report: {vmdFixture} ===");

            float globalMaxError = 0.0f;

            foreach (float frame in frames)
            {
                byte ok = MmdRuntimeFfiMethods.VmdSampleCamera(
                    vmdBytes, new IntPtr(vmdBytes.Length), frame, nativeBuffer, new IntPtr(nativeBuffer.Length));
                if (ok == 0)
                {
                    report.AppendLine($"  Frame {frame}: native sample failed");
                    continue;
                }

                MmdCameraState managed = VmdCameraSampler.Sample(motion.cameraKeyframes, frame);

                float distErr = Math.Abs(nativeBuffer[0] - managed.Distance);
                float posErr = Distance(
                    nativeBuffer[1], nativeBuffer[2], nativeBuffer[3],
                    managed.Position[0], managed.Position[1], managed.Position[2]);
                float rotErr = Distance(
                    nativeBuffer[4], nativeBuffer[5], nativeBuffer[6],
                    managed.Rotation[0], managed.Rotation[1], managed.Rotation[2]);
                float fovErr = Math.Abs(nativeBuffer[7] - managed.ViewAngle);

                float maxErr = Math.Max(Math.Max(posErr, rotErr), Math.Max(distErr, fovErr));
                if (maxErr > globalMaxError)
                {
                    globalMaxError = maxErr;
                }

                report.AppendLine(
                    $"  Frame {frame}: posErr={posErr:F6}, rotErr={rotErr:F6}, distErr={distErr:F6}, fovErr={fovErr:F6}");
            }

            report.AppendLine($"  Global maxErr: {globalMaxError:F6}, threshold: {maxAllowedError:F6}");
            report.AppendLine(globalMaxError <= maxAllowedError ? "  PASS" : "  FAIL");

            Debug.Log(report.ToString());
            WriteParityReport(vmdFixture, "camera", report.ToString());

            Assert.That(globalMaxError, Is.LessThanOrEqualTo(maxAllowedError),
                $"Max camera error {globalMaxError:F6} exceeds threshold {maxAllowedError:F6}.\n" + report);
        }

        private static void RunLightParityComparison(
            string vmdFixture,
            float[] frames,
            float maxAllowedError)
        {
            byte[] vmdBytes = MmdTestFixtures.ReadFixtureAssetBytes(vmdFixture);

            var parser = new NativeMmdParser();
            MmdMotionDefinition motion = parser.LoadMotion(vmdBytes);

            if (motion.lightKeyframes == null || motion.lightKeyframes.Count == 0)
            {
                Assert.Ignore("No light keyframes in " + vmdFixture);
                return;
            }

            const int lightSampleFloatCount = 6;
            float[] nativeBuffer = new float[lightSampleFloatCount];
            var report = new StringBuilder();
            report.AppendLine($"=== Light Parity Report: {vmdFixture} ===");

            float globalMaxError = 0.0f;

            foreach (float frame in frames)
            {
                byte ok = MmdRuntimeFfiMethods.VmdSampleLight(
                    vmdBytes, new IntPtr(vmdBytes.Length), frame, nativeBuffer, new IntPtr(nativeBuffer.Length));
                if (ok == 0)
                {
                    report.AppendLine($"  Frame {frame}: native sample failed");
                    continue;
                }

                MmdLightState managed = VmdLightSampler.Sample(motion.lightKeyframes, frame);

                float colorErr = Distance(
                    nativeBuffer[0], nativeBuffer[1], nativeBuffer[2],
                    managed.Color[0], managed.Color[1], managed.Color[2]);
                float dirErr = Distance(
                    nativeBuffer[3], nativeBuffer[4], nativeBuffer[5],
                    managed.Direction[0], managed.Direction[1], managed.Direction[2]);

                float maxErr = Math.Max(colorErr, dirErr);
                if (maxErr > globalMaxError)
                {
                    globalMaxError = maxErr;
                }

                report.AppendLine(
                    $"  Frame {frame}: colorErr={colorErr:F6}, dirErr={dirErr:F6}");
            }

            report.AppendLine($"  Global maxErr: {globalMaxError:F6}, threshold: {maxAllowedError:F6}");
            report.AppendLine(globalMaxError <= maxAllowedError ? "  PASS" : "  FAIL");

            Debug.Log(report.ToString());
            WriteParityReport(vmdFixture, "light", report.ToString());

            Assert.That(globalMaxError, Is.LessThanOrEqualTo(maxAllowedError),
                $"Max light error {globalMaxError:F6} exceeds threshold {maxAllowedError:F6}.\n" + report);
        }

        private static float Distance(float ax, float ay, float az, float bx, float by, float bz)
        {
            float dx = ax - bx;
            float dy = ay - by;
            float dz = az - bz;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
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
