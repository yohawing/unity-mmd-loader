#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
        public void MorphWeightsMatchBetweenEvaluatorAndDirectSession()
        {
            RunMorphConsistencyCheck(
                "test_append_bone.pmx",
                "test_append_bone.vmd",
                new[] { 0, 15, 30 },
                maxAllowedMorphError: 0.00001f);
        }

        [Test]
        public void MmdAnimCliParityReportComparesCliWithPackagedNativeRuntime()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("YMU_MMD_ANIM_CLI_PARITY"),
                    "1",
                    StringComparison.Ordinal))
            {
                Assert.Ignore("Set YMU_MMD_ANIM_CLI_PARITY=1 or run scripts/run-mmd-anim-cli-parity-report.ps1.");
            }

            var report = new MmdAnimCliParityReport
            {
                schemaVersion = 1,
                generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                comparisonSpace = "mmd",
                nativeDll = BuildNativeDllReport(),
                mmdAnim = BuildMmdAnimReport()
            };

            var cases = new List<MmdAnimCliParityCase>();
            cases.AddRange(BuildRuntimeImportCases("test_append_bone.pmx", "test_append_bone.vmd", new[] { 0.0f, 15.0f, 30.0f }, "bone"));
            cases.AddRange(BuildRuntimeImportCases("test_vertex_morph.pmx", "test_vertex_morph_motion.vmd", new[] { 0.0f, 15.0f, 30.0f }, "morph"));
            cases.Add(BuildCameraCase(15.0f));
            cases.Add(BuildLightCase(20.0f));
            cases.Add(BuildSelfShadowCase(20.0f));
            report.cases = cases.ToArray();

            var summaryCases = BuildVmdSummaryCases().ToList();
            report.summaryCases = summaryCases.ToArray();

            int failed = cases.Count(c => !string.Equals(c.status, "passed", StringComparison.Ordinal));
            int summaryFailed = summaryCases.Count(c => !string.Equals(c.status, "passed", StringComparison.Ordinal));
            int total = cases.Count + summaryCases.Count;
            report.summary = new MmdAnimCliParitySummary
            {
                total = total,
                passed = total - failed - summaryFailed,
                failed = failed + summaryFailed
            };

            string reportDir = Path.Combine(MmdTestFixtures.RepositoryRoot, "artifacts", "parity");
            Directory.CreateDirectory(reportDir);
            string reportPath = Path.Combine(reportDir, "mmd-anim-cli-parity-report.json");
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, prettyPrint: true));
            UnityEngine.Debug.Log("mmd-anim CLI parity report: " + reportPath);

            Assert.That(report.summary.failed, Is.EqualTo(0), "mmd-anim CLI parity report has failed cases: " + reportPath);
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

            float globalMaxError = 0.0f;
            int worstFrame = -1;
            string worstBoneName = string.Empty;

            foreach (int frame in frames)
            {
                session.EvaluateAndCopy(frame, nativeWorldMatrices, nativeMorphWeights, nativeIkEnabled);

                MmdEvaluatedFrame evaluated = MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrame(
                    model, motion, frame, frame / 30.0f);

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

                    if (translationError > globalMaxError)
                    {
                        globalMaxError = translationError;
                        worstFrame = frame;
                        worstBoneName = bonePose.name;
                    }
                }
            }

            Assert.That(globalMaxError, Is.LessThanOrEqualTo(maxAllowedError),
                $"{pmxFixture}: max translation error {globalMaxError:F6} exceeds threshold {maxAllowedError:F6} " +
                $"at frame {worstFrame}, bone '{worstBoneName}'.");
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

            float globalMaxError = 0.0f;
            int worstFrame = -1;
            string worstMorphName = string.Empty;

            foreach (int frame in frames)
            {
                session.EvaluateAndCopy(frame, nativeWorldMatrices, nativeMorphWeights, nativeIkEnabled);

                MmdEvaluatedFrame evaluated = MmdRuntimeFrameEvaluator.EvaluatePhaseOneFrame(
                    model, motion, frame, frame / 30.0f);

                var evaluatedMorphMap = new Dictionary<string, float>();
                foreach (MmdEvaluatedMorphWeight morph in evaluated.morphs)
                    evaluatedMorphMap[morph.name] = morph.weight;

                for (int morphIndex = 0; morphIndex < model.morphs.Count && morphIndex < nativeMorphWeights.Length; morphIndex++)
                {
                    string morphName = model.morphs[morphIndex].name;
                    float directWeight = nativeMorphWeights[morphIndex];
                    evaluatedMorphMap.TryGetValue(morphName, out float evaluatedWeight);
                    float diff = Math.Abs(directWeight - evaluatedWeight);
                    if (diff > globalMaxError)
                    {
                        globalMaxError = diff;
                        worstFrame = frame;
                        worstMorphName = morphName;
                    }
                }
            }

            Assert.That(globalMaxError, Is.LessThanOrEqualTo(maxAllowedMorphError),
                $"{pmxFixture}: max morph weight error {globalMaxError:F6} exceeds threshold {maxAllowedMorphError:F6} " +
                $"at frame {worstFrame}, morph '{worstMorphName}'.");
        }

        private static MmdAnimCliParityNativeDllReport BuildNativeDllReport()
        {
            string path = Path.Combine(
                MmdTestFixtures.PackageRoot,
                "Runtime",
                "Plugins",
                "x86_64",
                "mmd_runtime_ffi.dll");
            return new MmdAnimCliParityNativeDllReport
            {
                path = path,
                sha256 = File.Exists(path) ? Sha256(path) : string.Empty,
                abiVersion = MmdRuntimeFfiMethods.ValidateAbiVersion(),
                expectedAbiVersion = MmdRuntimeFfiMethods.ExpectedAbiVersion
            };
        }

        private static MmdAnimCliParityMmdAnimReport BuildMmdAnimReport()
        {
            string root = MmdAnimRoot;
            return new MmdAnimCliParityMmdAnimReport
            {
                root = root,
                commit = RunProcess("git", "-C " + Quote(root) + " rev-parse HEAD").Trim(),
                describe = RunProcess("git", "-C " + Quote(root) + " describe --tags --always --dirty").Trim(),
                cliCommand = "cargo run -q -p mmd-anim-cli --manifest-path " + Quote(Path.Combine(root, "Cargo.toml")) + " --"
            };
        }

        private static IEnumerable<MmdAnimCliParityCase> BuildRuntimeImportCases(
            string pmxFixture,
            string vmdFixture,
            float[] frames,
            string channel)
        {
            string pmxPath = MmdTestFixtures.FixtureAssetPath(pmxFixture);
            string vmdPath = MmdTestFixtures.FixtureAssetPath(vmdFixture);
            string frameList = string.Join(",", frames.Select(f => f.ToString("0.###", CultureInfo.InvariantCulture)));
            string json = RunMmdAnimCli("import " + Quote(pmxPath) + " " + Quote(vmdPath) + " --frames " + frameList + " --json");
            CliImportReport cli = JsonUtility.FromJson<CliImportReport>(json);

            byte[] pmxBytes = File.ReadAllBytes(pmxPath);
            byte[] vmdBytes = File.ReadAllBytes(vmdPath);
            using var session = MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes);
            float[] worldMatrices = new float[session.WorldMatrixFloatCount];
            float[] morphWeights = new float[session.MorphWeightCount];
            byte[] ikEnabled = new byte[session.IkEnabledCount];

            var cases = new List<MmdAnimCliParityCase>();
            foreach (CliImportFrame cliFrame in cli.perFrame ?? Array.Empty<CliImportFrame>())
            {
                session.EvaluateAndCopy(cliFrame.frame, worldMatrices, morphWeights, ikEnabled);
                long packagedTranslationChecksum = TranslationChecksum(worldMatrices);
                long packagedMorphChecksum = F32Checksum(morphWeights);

                bool passed = channel == "bone"
                    ? cliFrame.translationChecksum == packagedTranslationChecksum &&
                      cliFrame.worldMatrices == session.WorldMatrixFloatCount / 16
                    : cliFrame.morphChecksum == packagedMorphChecksum &&
                      cliFrame.nonzeroMorphs == CountNonZero(morphWeights);

                cases.Add(new MmdAnimCliParityCase
                {
                    id = channel + "-" + Path.GetFileNameWithoutExtension(pmxFixture) + "-frame-" + cliFrame.frame.ToString("0.###", CultureInfo.InvariantCulture),
                    channel = channel,
                    frame = cliFrame.frame,
                    status = passed ? "passed" : "failed",
                    fixtureModel = pmxFixture,
                    fixtureMotion = vmdFixture,
                    cliTranslationChecksum = cliFrame.translationChecksum.ToString(CultureInfo.InvariantCulture),
                    packagedTranslationChecksum = packagedTranslationChecksum.ToString(CultureInfo.InvariantCulture),
                    cliMorphChecksum = cliFrame.morphChecksum.ToString(CultureInfo.InvariantCulture),
                    packagedMorphChecksum = packagedMorphChecksum.ToString(CultureInfo.InvariantCulture),
                    cliCount = channel == "bone" ? cliFrame.worldMatrices : cliFrame.nonzeroMorphs,
                    packagedCount = channel == "bone" ? session.WorldMatrixFloatCount / 16 : CountNonZero(morphWeights),
                    maxAbsError = 0.0f
                });
            }

            return cases;
        }

        private static IEnumerable<MmdAnimCliParitySummaryCase> BuildVmdSummaryCases()
        {
            yield return BuildVmdSummaryCase(
                MmdTestFixtures.FixtureAssetPath("test_append_bone.vmd"),
                "test_append_bone.vmd");
            yield return BuildVmdSummaryCase(
                MmdTestFixtures.FixtureAssetPath("test_vertex_morph_motion.vmd"),
                "test_vertex_morph_motion.vmd");
            yield return BuildVmdSummaryCase(
                Path.Combine(MmdAnimRoot, "crates", "mmd-anim-format", "fixtures", "vmd", "simple_camera.vmd"),
                "simple_camera.vmd");
            string sceneTrackPath = WriteSceneTrackFixtureForCli();
            yield return BuildVmdSummaryCase(sceneTrackPath, Path.GetFileName(sceneTrackPath));
            string propertyTrackPath = WritePropertyTrackFixtureForCli();
            yield return BuildVmdSummaryCase(propertyTrackPath, Path.GetFileName(propertyTrackPath));
        }

        private static MmdAnimCliParitySummaryCase BuildVmdSummaryCase(string vmdPath, string fixtureMotion)
        {
            byte[] bytes = File.ReadAllBytes(vmdPath);
            MmdVmdParseSummary managed = MmdVmdBinarySummaryReader.Read(bytes);

            string json = RunMmdAnimCli("inspect " + Quote(vmdPath) + " --json");
            CliVmdInspectReport cli = ParseCliVmdInspectReport(json, vmdPath);
            CliVmdInspectMetadata metadata = cli.metadata!;
            CliVmdInspectCounts counts = metadata.counts!;
            CliVmdInspectPropertyFrame[] propertyFrames = cli.propertyFrames!;
            int cliConstraintStateCount = propertyFrames.Sum(frame => frame.ikStates!.Length);

            bool passed = string.Equals(
                    managed.TargetModelName,
                    metadata.modelName,
                    StringComparison.Ordinal) &&
                managed.MaxFrame == metadata.maxFrame &&
                managed.BoneKeyframeCount == counts.bones &&
                managed.MorphKeyframeCount == counts.morphs &&
                managed.ModelKeyframeCount == counts.properties &&
                managed.ConstraintStateCount == cliConstraintStateCount &&
                managed.CameraKeyframeCount == counts.cameras &&
                managed.LightKeyframeCount == counts.lights &&
                managed.SelfShadowKeyframeCount == counts.selfShadows;

            return new MmdAnimCliParitySummaryCase
            {
                id = "summary-" + Path.GetFileNameWithoutExtension(fixtureMotion),
                channel = "summary",
                status = passed ? "passed" : "failed",
                fixtureMotion = fixtureMotion,
                managedTargetModelName = managed.TargetModelName,
                cliTargetModelName = metadata.modelName,
                managedMaxFrame = managed.MaxFrame,
                cliMaxFrame = metadata.maxFrame,
                managedBoneKeyframeCount = managed.BoneKeyframeCount,
                cliBoneCount = counts.bones,
                managedMorphKeyframeCount = managed.MorphKeyframeCount,
                cliMorphCount = counts.morphs,
                managedModelKeyframeCount = managed.ModelKeyframeCount,
                managedConstraintStateCount = managed.ConstraintStateCount,
                cliPropertyCount = counts.properties,
                cliConstraintStateCount = cliConstraintStateCount,
                managedCameraKeyframeCount = managed.CameraKeyframeCount,
                cliCameraCount = counts.cameras,
                managedLightKeyframeCount = managed.LightKeyframeCount,
                cliLightCount = counts.lights,
                managedSelfShadowKeyframeCount = managed.SelfShadowKeyframeCount,
                cliSelfShadowCount = counts.selfShadows
            };
        }

        private static CliVmdInspectReport ParseCliVmdInspectReport(string json, string vmdPath)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("mmd-anim inspect returned empty JSON for " + vmdPath);
            }

            RequireJsonKeys(
                json,
                vmdPath,
                "metadata",
                "modelName",
                "maxFrame",
                "counts",
                "bones",
                "morphs",
                "cameras",
                "lights",
                "selfShadows",
                "properties",
                "propertyFrames");

            CliVmdInspectReport? report;
            try
            {
                report = JsonUtility.FromJson<CliVmdInspectReport>(json);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Failed to parse mmd-anim inspect JSON for " + vmdPath,
                    exception);
            }

            if (report == null || report.metadata == null || report.metadata.counts == null)
            {
                throw new InvalidOperationException(
                    "mmd-anim inspect JSON is missing metadata/counts for " + vmdPath);
            }

            if (report.propertyFrames == null)
            {
                throw new InvalidOperationException(
                    "mmd-anim inspect JSON is missing propertyFrames for " + vmdPath);
            }

            for (int i = 0; i < report.propertyFrames.Length; i++)
            {
                if (report.propertyFrames[i] == null || report.propertyFrames[i].ikStates == null)
                {
                    throw new InvalidOperationException(
                        "mmd-anim inspect JSON is missing propertyFrames[" + i + "].ikStates for " + vmdPath);
                }
            }

            return report;
        }

        private static void RequireJsonKeys(string json, string vmdPath, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (json.IndexOf('"' + key + '"', StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException(
                        "mmd-anim inspect JSON is missing key '" + key + "' for " + vmdPath);
                }
            }
        }

        private static MmdAnimCliParityCase BuildCameraCase(float frame)
        {
            string vmdPath = Path.Combine(MmdAnimRoot, "crates", "mmd-anim-format", "fixtures", "vmd", "simple_camera.vmd");
            string json = RunMmdAnimCli("vmd-sample " + Quote(vmdPath) + " --kind camera --frame " + FrameArg(frame) + " --json");
            CliVmdSampleReport cli = JsonUtility.FromJson<CliVmdSampleReport>(json);

            byte[] bytes = File.ReadAllBytes(vmdPath);
            Assert.That(NativeVmdCameraTrackSampler.TryCreate(bytes, out var sampler), Is.True, "camera sampler");
            using (sampler)
            {
                Assert.That(sampler!.TrySample(frame, out MmdCameraState state), Is.True, "camera sample");
                float max = MaxAbs(
                    cli.camera.distance - state.Distance,
                    cli.camera.position[0] - state.Position[0],
                    cli.camera.position[1] - state.Position[1],
                    cli.camera.position[2] - state.Position[2],
                    cli.camera.rotation[0] - state.Rotation[0],
                    cli.camera.rotation[1] - state.Rotation[1],
                    cli.camera.rotation[2] - state.Rotation[2],
                    cli.camera.fov - state.ViewAngle,
                    (cli.camera.perspective ? 1.0f : 0.0f) - (state.Perspective ? 1.0f : 0.0f));
                return SceneCase("camera", "simple_camera.vmd", frame, max, 0.0001f);
            }
        }

        private static MmdAnimCliParityCase BuildLightCase(float frame)
        {
            string vmdPath = WriteSceneTrackFixtureForCli();
            string json = RunMmdAnimCli("vmd-sample " + Quote(vmdPath) + " --kind light --frame " + FrameArg(frame) + " --json");
            CliVmdSampleReport cli = JsonUtility.FromJson<CliVmdSampleReport>(json);

            byte[] bytes = File.ReadAllBytes(vmdPath);
            Assert.That(NativeVmdLightTrackSampler.TryCreate(bytes, out var sampler), Is.True, "light sampler");
            using (sampler)
            {
                Assert.That(sampler!.TrySample(frame, out MmdLightState state), Is.True, "light sample");
                float max = MaxAbs(
                    cli.light.color[0] - state.Color[0],
                    cli.light.color[1] - state.Color[1],
                    cli.light.color[2] - state.Color[2],
                    cli.light.direction[0] - state.Direction[0],
                    cli.light.direction[1] - state.Direction[1],
                    cli.light.direction[2] - state.Direction[2]);
                return SceneCase("light", Path.GetFileName(vmdPath), frame, max, 0.0001f);
            }
        }

        private static MmdAnimCliParityCase BuildSelfShadowCase(float frame)
        {
            string vmdPath = WriteSceneTrackFixtureForCli();
            string json = RunMmdAnimCli("vmd-sample " + Quote(vmdPath) + " --kind self-shadow --frame " + FrameArg(frame) + " --json");
            CliVmdSampleReport cli = JsonUtility.FromJson<CliVmdSampleReport>(json);

            byte[] bytes = File.ReadAllBytes(vmdPath);
            IntPtr track = MmdRuntimeFfiMethods.VmdSelfShadowTrackCreateFromVmdBytes(bytes, new IntPtr(bytes.Length));
            Assert.That(track, Is.Not.EqualTo(IntPtr.Zero), "self-shadow sampler");
            try
            {
                float[] values = new float[2];
                Assert.That(
                    MmdRuntimeFfiMethods.VmdSelfShadowTrackSample(track, frame, values, new IntPtr(values.Length)),
                    Is.Not.Zero,
                    "self-shadow sample");
                float max = MaxAbs(cli.selfShadow.mode - values[0], cli.selfShadow.distance - values[1]);
                return SceneCase("self-shadow", Path.GetFileName(vmdPath), frame, max, 0.0001f);
            }
            finally
            {
                MmdRuntimeFfiMethods.VmdSelfShadowTrackFree(track);
            }
        }

        private static MmdAnimCliParityCase SceneCase(string channel, string motion, float frame, float maxAbsError, float tolerance)
        {
            return new MmdAnimCliParityCase
            {
                id = channel + "-frame-" + frame.ToString("0.###", CultureInfo.InvariantCulture),
                channel = channel,
                frame = frame,
                status = maxAbsError <= tolerance ? "passed" : "failed",
                fixtureMotion = motion,
                maxAbsError = maxAbsError,
                tolerance = tolerance
            };
        }

        private static string WriteSceneTrackFixtureForCli()
        {
            string dir = Path.Combine(MmdTestFixtures.RepositoryRoot, "artifacts", "parity");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "scene-track-light-self-shadow.vmd");
            File.WriteAllBytes(path, MmdTestFixtures.BuildSceneTrackVmdBytes("cli_parity"));
            return path;
        }

        private static string WritePropertyTrackFixtureForCli()
        {
            string dir = Path.Combine(MmdTestFixtures.RepositoryRoot, "artifacts", "parity");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "property-track-ik.vmd");

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            WriteFixedAscii(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedAscii(writer, "cli_property", 20);
            writer.Write(0u); // bone frames
            writer.Write(0u); // morph frames
            writer.Write(0u); // camera frames
            writer.Write(0u); // light frames
            writer.Write(0u); // self-shadow frames
            writer.Write(1u); // property frames
            writer.Write(14u);
            writer.Write((byte)0);
            writer.Write(2u); // IK states
            WriteFixedAscii(writer, "cli_leg_ik", 20);
            writer.Write((byte)0);
            WriteFixedAscii(writer, "cli_arm_ik", 20);
            writer.Write((byte)1);
            File.WriteAllBytes(path, stream.ToArray());
            return path;
        }

        private static void WriteFixedAscii(BinaryWriter writer, string value, int byteCount)
        {
            byte[] buffer = new byte[byteCount];
            byte[] encoded = Encoding.ASCII.GetBytes(value ?? string.Empty);
            Buffer.BlockCopy(encoded, 0, buffer, 0, Math.Min(encoded.Length, buffer.Length));
            writer.Write(buffer);
        }

        private static string RunMmdAnimCli(string arguments)
        {
            return RunProcess(
                "cargo",
                "run -q -p mmd-anim-cli --manifest-path " + Quote(Path.Combine(MmdAnimRoot, "Cargo.toml")) + " -- " + arguments);
        }

        private static string RunProcess(string fileName, string arguments)
        {
            var start = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = MmdTestFixtures.RepositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start process: " + fileName);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    fileName + " " + arguments + " failed with exit code " + process.ExitCode + Environment.NewLine + stderr);
            }

            return stdout;
        }

        private static string MmdAnimRoot => Path.Combine(MmdTestFixtures.RepositoryRoot, "native", "mmd-anim");

        private static string FrameArg(float frame) => frame.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

        private static string Sha256(string path)
        {
            using var sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static long TranslationChecksum(float[] nativeWorldMatrices)
        {
            uint hash = 0x811c9dc5u;
            for (int offset = 0; offset + 14 < nativeWorldMatrices.Length; offset += 16)
            {
                hash = Fnv(hash, nativeWorldMatrices[offset + 12]);
                hash = Fnv(hash, nativeWorldMatrices[offset + 13]);
                hash = Fnv(hash, nativeWorldMatrices[offset + 14]);
            }

            return hash;
        }

        private static long F32Checksum(float[] values)
        {
            uint hash = 0x811c9dc5u;
            foreach (float value in values)
            {
                hash = Fnv(hash, value);
            }

            return hash;
        }

        private static uint Fnv(uint hash, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            uint bits = BitConverter.ToUInt32(bytes, 0);
            unchecked
            {
                return (hash ^ bits) * 0x01000193u;
            }
        }

        private static int CountNonZero(float[] values)
        {
            int count = 0;
            foreach (float value in values)
            {
                if (Math.Abs(value) > float.Epsilon)
                    count++;
            }

            return count;
        }

        private static float MaxAbs(params float[] values)
        {
            float max = 0.0f;
            foreach (float value in values)
            {
                max = Math.Max(max, Math.Abs(value));
            }

            return max;
        }

        [Serializable]
        private sealed class CliImportReport
        {
            public CliImportFrame[] perFrame = Array.Empty<CliImportFrame>();
        }

        [Serializable]
        private sealed class CliImportFrame
        {
            public float frame;
            public int worldMatrices;
            public long translationChecksum;
            public int nonzeroMorphs;
            public long morphChecksum;
        }

        [Serializable]
        private sealed class CliVmdSampleReport
        {
            public CliCameraState camera = new();
            public CliLightState light = new();
            public CliSelfShadowState selfShadow = new();
        }

        [Serializable]
        private sealed class CliVmdInspectReport
        {
            public CliVmdInspectMetadata? metadata;
            public CliVmdInspectPropertyFrame[]? propertyFrames;
        }

        [Serializable]
        private sealed class CliVmdInspectMetadata
        {
            public string modelName = string.Empty;
            public int maxFrame;
            public CliVmdInspectCounts? counts;
        }

        [Serializable]
        private sealed class CliVmdInspectCounts
        {
            public int bones;
            public int morphs;
            public int cameras;
            public int lights;
            public int selfShadows;
            public int properties;
        }

        [Serializable]
        private sealed class CliVmdInspectPropertyFrame
        {
            public CliVmdInspectIkState[]? ikStates;
        }

        [Serializable]
        private sealed class CliVmdInspectIkState
        {
            public string boneName = string.Empty;
            public bool enabled;
        }

        [Serializable]
        private sealed class CliCameraState
        {
            public float distance;
            public float[] position = Array.Empty<float>();
            public float[] rotation = Array.Empty<float>();
            public float fov;
            public bool perspective;
        }

        [Serializable]
        private sealed class CliLightState
        {
            public float[] color = Array.Empty<float>();
            public float[] direction = Array.Empty<float>();
        }

        [Serializable]
        private sealed class CliSelfShadowState
        {
            public int mode;
            public float distance;
        }

        [Serializable]
        private sealed class MmdAnimCliParityReport
        {
            public int schemaVersion;
            public string generatedAtUtc = string.Empty;
            public string comparisonSpace = string.Empty;
            public MmdAnimCliParityNativeDllReport nativeDll = new();
            public MmdAnimCliParityMmdAnimReport mmdAnim = new();
            public MmdAnimCliParitySummary summary = new();
            public MmdAnimCliParityCase[] cases = Array.Empty<MmdAnimCliParityCase>();
            public MmdAnimCliParitySummaryCase[] summaryCases = Array.Empty<MmdAnimCliParitySummaryCase>();
        }

        [Serializable]
        private sealed class MmdAnimCliParityNativeDllReport
        {
            public string path = string.Empty;
            public string sha256 = string.Empty;
            public uint abiVersion;
            public uint expectedAbiVersion;
        }

        [Serializable]
        private sealed class MmdAnimCliParityMmdAnimReport
        {
            public string root = string.Empty;
            public string commit = string.Empty;
            public string describe = string.Empty;
            public string cliCommand = string.Empty;
        }

        [Serializable]
        private sealed class MmdAnimCliParitySummary
        {
            public int total;
            public int passed;
            public int failed;
        }

        [Serializable]
        private sealed class MmdAnimCliParityCase
        {
            public string id = string.Empty;
            public string channel = string.Empty;
            public string status = string.Empty;
            public string fixtureModel = string.Empty;
            public string fixtureMotion = string.Empty;
            public float frame;
            public string cliTranslationChecksum = string.Empty;
            public string packagedTranslationChecksum = string.Empty;
            public string cliMorphChecksum = string.Empty;
            public string packagedMorphChecksum = string.Empty;
            public int cliCount;
            public int packagedCount;
            public float maxAbsError;
            public float tolerance;
        }

        [Serializable]
        private sealed class MmdAnimCliParitySummaryCase
        {
            public string id = string.Empty;
            public string channel = string.Empty;
            public string status = string.Empty;
            public string fixtureMotion = string.Empty;
            public string managedTargetModelName = string.Empty;
            public string cliTargetModelName = string.Empty;
            public int managedMaxFrame;
            public int cliMaxFrame;
            public int managedBoneKeyframeCount;
            public int cliBoneCount;
            public int managedMorphKeyframeCount;
            public int cliMorphCount;
            public int managedModelKeyframeCount;
            public int managedConstraintStateCount;
            public int cliPropertyCount;
            public int cliConstraintStateCount;
            public int managedCameraKeyframeCount;
            public int cliCameraCount;
            public int managedLightKeyframeCount;
            public int cliLightCount;
            public int managedSelfShadowKeyframeCount;
            public int cliSelfShadowCount;
        }
    }
}
