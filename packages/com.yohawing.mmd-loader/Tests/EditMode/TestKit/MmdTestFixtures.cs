#nullable enable

using System.IO;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Mmd.Parser;

namespace Mmd.Tests
{
    internal static class MmdTestFixtures
    {
        internal static string ProjectRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        internal static string RepositoryRoot =>
            Path.GetFullPath(Path.Combine(ProjectRoot, ".."));

        internal static string PackageRoot
        {
            get
            {
                UnityEditor.PackageManager.PackageInfo? package =
                    UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(MmdTestFixtures).Assembly);
                return package != null && !string.IsNullOrWhiteSpace(package.resolvedPath)
                    ? Path.GetFullPath(package.resolvedPath)
                    : Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-loader");
            }
        }

        internal static string FixturesRoot =>
            Path.Combine(PackageRoot, "Tests", "Fixtures");

        internal static string GoldenRoot =>
            Path.Combine(FixturesRoot, "Golden");

        internal static string PackageModelManifestPath =>
            Path.Combine(FixturesRoot, "model-fixtures.json");

        internal static string? LocalCorpusManifestPath =>
            Environment.GetEnvironmentVariable("YMU_LOCAL_FIXTURES_MANIFEST");

        internal static string FixtureAssetPath(string fileName) =>
            Path.Combine(FixturesRoot, "Assets", fileName);

        internal static byte[] ReadFixtureAssetBytes(string fileName) =>
            File.ReadAllBytes(FixtureAssetPath(fileName));

        internal static void RequireGoldenGenerationOptIn()
        {
            Assert.That(
                Environment.GetEnvironmentVariable("YMU_GENERATE_GOLDENS"),
                Is.EqualTo("1"),
                "Golden generation is maintainer-only. Set YMU_GENERATE_GOLDENS=1 before selecting the explicit generation test.");
        }

        internal static string GenerateModelGoldenCandidate(ModelFixtureEntry fixture)
        {
            Assert.That(fixture.IsLocalCorpus, Is.False, fixture.Context("local corpus goldens are never committed"));
            Assert.That(fixture.ModelPath, Does.Exist, "Model asset not found: " + fixture.ModelPath);

            MmdModelDefinition model = ParseModel(fixture);
            string candidatePath = GoldenCandidatePath(fixture.RequireGoldenPath());
            File.WriteAllText(candidatePath, JsonUtility.ToJson(model, prettyPrint: true));
            return candidatePath;
        }

        internal static string GenerateMotionGoldenCandidate(string baseName)
        {
            string goldenPath = MotionGoldenPath(baseName);
            string vmdPath = FixtureAssetPath(MotionFixtureFileName(baseName));
            Assert.That(vmdPath, Does.Exist, "VMD asset not found: " + vmdPath);

            MmdMotionDefinition motion = ParseMotionFile(MotionFixtureFileName(baseName));
            string candidatePath = GoldenCandidatePath(goldenPath);
            File.WriteAllText(candidatePath, JsonUtility.ToJson(motion, prettyPrint: true));
            return candidatePath;
        }

        private static string GoldenCandidatePath(string goldenPath)
        {
            string relativePath = Path.GetRelativePath(GoldenRoot, goldenPath);
            string candidatePath = Path.Combine(GoldenCandidateRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(candidatePath)!);
            return candidatePath;
        }

        internal static MmdModelDefinition LoadModelGolden(ModelFixtureEntry fixture)
        {
            string json = File.ReadAllText(fixture.RequireGoldenPath());
            return JsonUtility.FromJson<MmdModelDefinition>(json);
        }

        internal static MmdMotionDefinition LoadMotionGolden(string baseName)
        {
            string json = File.ReadAllText(MotionGoldenPath(baseName));
            return JsonUtility.FromJson<MmdMotionDefinition>(json);
        }

        internal static MmdModelDefinition ParseModel(ModelFixtureEntry fixture)
        {
            Assert.That(fixture.ModelPath, Does.Exist, "Model asset not found: " + fixture.ModelPath);

            var parser = new NativeMmdParser();
            return parser.LoadModel(File.ReadAllBytes(fixture.ModelPath));
        }

        internal static MmdMotionDefinition ParseMotionFile(string fileName)
        {
            string path = FixtureAssetPath(fileName);
            Assert.That(path, Does.Exist, "VMD asset not found: " + path);

            var parser = new NativeMmdParser();
            return parser.LoadMotion(File.ReadAllBytes(path));
        }

        internal static MmdMotionDefinition ParseGeneratedRestPoseMotion(string targetModelName)
        {
            var parser = new NativeMmdParser();
            return parser.LoadMotion(CreateVmdBytes(targetModelName, Array.Empty<GeneratedVmdBoneKeyframe>()));
        }

        internal static MmdMotionDefinition ParseGeneratedBoneTranslationMotion(
            string targetModelName,
            string boneName,
            int maxFrame,
            float endTranslationX)
        {
            Assert.That(maxFrame, Is.GreaterThanOrEqualTo(0));
            var keyframes = new[]
            {
                new GeneratedVmdBoneKeyframe(boneName, 0, new Vector3(0.0f, 0.0f, 0.0f), Quaternion.identity),
                new GeneratedVmdBoneKeyframe(boneName, (uint)maxFrame, new Vector3(endTranslationX, 0.0f, 0.0f), Quaternion.identity)
            };
            var parser = new NativeMmdParser();
            return parser.LoadMotion(CreateVmdBytes(targetModelName, keyframes));
        }

        internal static byte[] BuildSceneTrackVmdBytes(string modelName)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            WriteFixedSjis(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedSjis(writer, modelName ?? string.Empty, 20);
            writer.Write(0u); // bone frames
            writer.Write(0u); // morph frames
            writer.Write(0u); // camera frames
            writer.Write(2u); // light frames
            WriteSceneTrackLightFrame(writer, 10u, 0.0f, 0.0f, 1.0f, 1.0f, 0.0f, 0.0f);
            WriteSceneTrackLightFrame(writer, 30u, 1.0f, 0.5f, 0.0f, 0.0f, -1.0f, 0.0f);
            writer.Write(2u); // self-shadow frames
            WriteSceneTrackSelfShadowFrame(writer, 10u, 1, 0.2f);
            WriteSceneTrackSelfShadowFrame(writer, 30u, 2, 0.4f);
            writer.Write(0u); // property frames
            return stream.ToArray();
        }

        internal static byte[] BuildCameraTrackVmdBytes(string modelName)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            WriteFixedSjis(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedSjis(writer, modelName ?? string.Empty, 20);
            writer.Write(0u); // bone frames
            writer.Write(0u); // morph frames
            writer.Write(2u); // camera frames
            WriteSceneTrackCameraFrame(writer, 0u, -40.0f, 0.0f, 10.0f, 0.0f, 0.0f, 0.0f, 0.0f, 20u, 0);
            WriteSceneTrackCameraFrame(writer, 30u, -20.0f, 2.0f, 20.0f, -4.0f, 0.1f, 0.2f, 0.1f, 40u, 1);
            writer.Write(0u); // light frames
            writer.Write(0u); // self-shadow frames
            writer.Write(0u); // property frames
            return stream.ToArray();
        }

        internal static string MotionFixtureFileName(string baseName)
        {
            return baseName == "test_1bone_cube" ? "test_1bone_cube_motion.vmd" : baseName + ".vmd";
        }

        internal static IEnumerable<string> MotionFixtureBaseNames()
        {
            yield return "test_1bone_cube";
            yield return "test_append_bone";
        }

        internal static string MotionGoldenPath(string baseName) =>
            Path.Combine(GoldenRoot, baseName + "_motion.json");

        internal static string GoldenCandidateRoot =>
            Path.Combine(RepositoryRoot, "artifacts", "golden-candidates");

        internal static IEnumerable<ModelFixtureEntry> LoadPackageModelFixtures()
        {
            string json = File.ReadAllText(PackageModelManifestPath);
            var manifest = JsonUtility.FromJson<ModelFixtureManifest>(json);
            Assert.That(manifest.schemaVersion, Is.EqualTo(1), "model fixture manifest schemaVersion");
            Assert.That(manifest.fixtures, Is.Not.Null, "model fixture manifest fixtures");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ModelFixtureRecord record in manifest.fixtures)
            {
                AssertFixtureRecordShape(record, ids);
                string modelPath = ResolveUnderRoot(FixturesRoot, record.model, "model", mustExist: true);
                string goldenPath = ResolveUnderRoot(FixturesRoot, record.golden, "golden", mustExist: false);
                yield return ModelFixtureEntry.FromManifest(record, modelPath, goldenPath);
            }
        }

        internal static IEnumerable<ModelFixtureEntry> LoadLocalCorpusModelFixtures()
        {
            if (string.IsNullOrWhiteSpace(LocalCorpusManifestPath) || !File.Exists(LocalCorpusManifestPath))
            {
                yield break;
            }

            string json = File.ReadAllText(LocalCorpusManifestPath);
            string basePath = MatchRequiredString(json, "basePath");
            foreach (string format in new[] { "pmx", "pmd" })
            {
                KeyValuePair<string, string>? item = ExtractFirstReleaseSmokePath(json, format);
                if (!item.HasValue)
                {
                    continue;
                }

                string modelPath = ResolveLocalCorpusPath(basePath, item.Value.Value);
                if (!File.Exists(modelPath))
                {
                    continue;
                }

                var expected = new ModelFixtureExpected
                {
                    minVertices = 1,
                    minIndices = 3,
                    minBones = 1,
                    minMaterials = 1
                };
                yield return new ModelFixtureEntry(
                    "local-" + item.Value.Key + "-" + format,
                    format,
                    modelPath,
                    goldenPath: null,
                    expected: expected,
                    notes: "Imported from optional local corpus manifest.",
                    isLocalCorpus: true);
            }
        }

        private static void AssertFixtureRecordShape(ModelFixtureRecord record, HashSet<string> ids)
        {
            Assert.That(record.id, Is.Not.Null.And.Not.Empty, "fixtures[].id");
            Assert.That(record.id.Trim(), Is.EqualTo(record.id), "fixtures[].id must not have surrounding whitespace: " + record.id);
            Assert.That(ids.Add(record.id), Is.True, "duplicate fixture id: " + record.id);
            Assert.That(record.format, Is.EqualTo("pmx").Or.EqualTo("pmd"), "fixtures[" + record.id + "].format");
            Assert.That(record.model, Is.Not.Null.And.Not.Empty, "fixtures[" + record.id + "].model");
            Assert.That(record.golden, Is.Not.Null.And.Not.Empty, "fixtures[" + record.id + "].golden");
        }

        private static string ResolveUnderRoot(string root, string relativePath, string fieldName, bool mustExist)
        {
            Assert.That(Path.IsPathRooted(relativePath), Is.False, fieldName + " path must be relative: " + relativePath);
            string resolved = Path.GetFullPath(Path.Combine(root, relativePath));
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(resolved.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase), Is.True, fieldName + " path escapes fixture root: " + relativePath);
            if (mustExist)
            {
                Assert.That(resolved, Does.Exist, fieldName + " path not found: " + resolved);
            }

            return resolved;
        }

        private static string ResolveLocalCorpusPath(string basePath, string value)
        {
            string resolved = Path.IsPathRooted(value) ? value : Path.Combine(basePath, value);
            return Path.GetFullPath(resolved);
        }

        private static string MatchRequiredString(string json, string fieldName)
        {
            Match match = Regex.Match(json, "\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"");
            Assert.That(match.Success, Is.True, "missing string field: " + fieldName);
            return Regex.Unescape(match.Groups["value"].Value);
        }

        private static KeyValuePair<string, string>? ExtractFirstReleaseSmokePath(string json, string format)
        {
            Match formatMatch = Regex.Match(json, "\"" + Regex.Escape(format) + "\"\\s*:\\s*\\{(?<body>.*?)\\}", RegexOptions.Singleline);
            if (!formatMatch.Success)
            {
                return null;
            }

            Match itemMatch = Regex.Match(formatMatch.Groups["body"].Value, "\"(?<key>[^\"]+)\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"");
            if (!itemMatch.Success)
            {
                return null;
            }

            return new KeyValuePair<string, string>(
                itemMatch.Groups["key"].Value,
                Regex.Unescape(itemMatch.Groups["value"].Value));
        }

        internal static MmdModelDefinition CreateMinimalTriangleModel(string modelName)
        {
            var model = new MmdModelDefinition
            {
                name = modelName
            };
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                transformOrder = 0,
                origin = new[] { 0.0f, 0.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.vertices.Add(CreateSyntheticVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateSyntheticVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateSyntheticVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "triangle-material",
                vertexCount = 3
            });
            return model;
        }

        internal static MmdMotionDefinition CreateRootTranslationMotion(string targetModelName, int maxFrame = 10)
        {
            var motion = new MmdMotionDefinition
            {
                targetModelName = targetModelName,
                maxFrame = maxFrame
            };
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = 0,
                translation = new[] { 0.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = LinearBoneInterpolation()
            });
            motion.boneKeyframes.Add(new MmdBoneKeyframeDefinition
            {
                boneName = "root",
                frame = maxFrame,
                translation = new[] { 2.0f, 0.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                interpolation = LinearBoneInterpolation()
            });
            return motion;
        }

        internal static MmdBoneInterpolationDefinition LinearBoneInterpolation()
        {
            byte[] linear = { 20, 20, 107, 107 };
            return new MmdBoneInterpolationDefinition
            {
                translationX = linear,
                translationY = linear,
                translationZ = linear,
                rotation = linear
            };
        }

        internal static MmdVertexDefinition CreateSyntheticVertex(
            int index,
            float x,
            float y,
            float z,
            float u,
            float v)
        {
            return new MmdVertexDefinition
            {
                index = index,
                position = new[] { x, y, z },
                normal = new[] { 0.0f, 0.0f, 1.0f },
                uv = new[] { u, v },
                boneIndices = new[] { 0 },
                boneWeights = new[] { 1.0f }
            };
        }

        private static byte[] CreateVmdBytes(string modelName, IReadOnlyList<GeneratedVmdBoneKeyframe> boneKeyframes)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            WriteFixedSjis(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedSjis(writer, modelName ?? string.Empty, 20);
            writer.Write((uint)boneKeyframes.Count);
            byte[] interpolation = LinearVmdInterpolationBytes();
            for (int i = 0; i < boneKeyframes.Count; i++)
            {
                GeneratedVmdBoneKeyframe keyframe = boneKeyframes[i];
                WriteFixedSjis(writer, keyframe.BoneName, 15);
                writer.Write(keyframe.Frame);
                writer.Write(keyframe.Translation.x);
                writer.Write(keyframe.Translation.y);
                writer.Write(keyframe.Translation.z);
                writer.Write(keyframe.Rotation.x);
                writer.Write(keyframe.Rotation.y);
                writer.Write(keyframe.Rotation.z);
                writer.Write(keyframe.Rotation.w);
                writer.Write(interpolation);
            }

            writer.Write(0u); // morph count
            writer.Write(0u); // camera count
            writer.Write(0u); // light count
            writer.Write(0u); // self-shadow count
            writer.Write(0u); // show/IK count
            return stream.ToArray();
        }

        internal static byte[] CreateDenseVmdBytes(
            string modelName,
            string boneName,
            int boneKeyframeCount,
            int frameSpan)
        {
            Assert.That(boneKeyframeCount, Is.GreaterThan(0));
            Assert.That(frameSpan, Is.GreaterThan(0));

            using var stream = new MemoryStream(checked(54 + boneKeyframeCount * 111 + 16));
            using var writer = new BinaryWriter(stream);
            WriteFixedSjis(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedSjis(writer, modelName ?? string.Empty, 20);
            writer.Write((uint)boneKeyframeCount);
            byte[] interpolation = LinearVmdInterpolationBytes();
            for (int i = 0; i < boneKeyframeCount; i++)
            {
                WriteFixedSjis(writer, boneName ?? string.Empty, 15);
                writer.Write((uint)(i % frameSpan));
                writer.Write((float)(i % 97) * 0.001f);
                writer.Write((float)(i % 53) * -0.001f);
                writer.Write((float)(i % 31) * 0.002f);
                writer.Write(0.0f);
                writer.Write(0.0f);
                writer.Write(0.0f);
                writer.Write(1.0f);
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
            byte[] encoded = Encoding.GetEncoding(932).GetBytes(value ?? string.Empty);
            Array.Copy(encoded, 0, buffer, 0, Math.Min(encoded.Length, buffer.Length));
            writer.Write(buffer);
        }

        private static void WriteSceneTrackLightFrame(
            BinaryWriter writer,
            uint frame,
            float r,
            float g,
            float b,
            float x,
            float y,
            float z)
        {
            writer.Write(frame);
            writer.Write(r);
            writer.Write(g);
            writer.Write(b);
            writer.Write(x);
            writer.Write(y);
            writer.Write(z);
        }

        private static void WriteSceneTrackCameraFrame(
            BinaryWriter writer,
            uint frame,
            float distance,
            float posX,
            float posY,
            float posZ,
            float rotX,
            float rotY,
            float rotZ,
            uint viewAngle,
            byte perspective)
        {
            writer.Write(frame);
            writer.Write(distance);
            writer.Write(posX);
            writer.Write(posY);
            writer.Write(posZ);
            writer.Write(rotX);
            writer.Write(rotY);
            writer.Write(rotZ);
            writer.Write(new byte[24]); // interpolation
            writer.Write(viewAngle);
            writer.Write(perspective);
        }

        private static void WriteSceneTrackSelfShadowFrame(BinaryWriter writer, uint frame, byte mode, float distance)
        {
            writer.Write(frame);
            writer.Write(mode);
            writer.Write(distance);
        }

        private static byte[] LinearVmdInterpolationBytes()
        {
            var interpolation = new byte[64];
            for (int channel = 0; channel < 4; channel++)
            {
                interpolation[channel] = 20;
                interpolation[channel + 4] = 20;
                interpolation[channel + 8] = 107;
                interpolation[channel + 12] = 107;
            }

            return interpolation;
        }

        private readonly struct GeneratedVmdBoneKeyframe
        {
            public readonly string BoneName;
            public readonly uint Frame;
            public readonly Vector3 Translation;
            public readonly Quaternion Rotation;

            public GeneratedVmdBoneKeyframe(string boneName, uint frame, Vector3 translation, Quaternion rotation)
            {
                BoneName = boneName;
                Frame = frame;
                Translation = translation;
                Rotation = rotation;
            }
        }
    }

    [Serializable]
    internal sealed class ModelFixtureManifest
    {
        public int schemaVersion;
        public ModelFixtureRecord[] fixtures = Array.Empty<ModelFixtureRecord>();
    }

    [Serializable]
    public sealed class ModelFixtureRecord
    {
        public string id = string.Empty;
        public string format = string.Empty;
        public string model = string.Empty;
        public string golden = string.Empty;
        public ModelFixtureExpected expected = new();
        public string notes = string.Empty;
    }

    [Serializable]
    public sealed class ModelFixtureExpected
    {
        public int minVertices;
        public int minIndices;
        public int minBones;
        public int minMaterials;
    }

    public sealed class ModelFixtureEntry
    {
        public readonly string id;
        public readonly string format;
        public readonly string ModelPath;
        public readonly string? GoldenPath;
        public readonly ModelFixtureExpected expected;
        public readonly string notes;
        public readonly bool IsLocalCorpus;

        public ModelFixtureEntry(
            string id,
            string format,
            string modelPath,
            string? goldenPath,
            ModelFixtureExpected expected,
            string notes,
            bool isLocalCorpus = false)
        {
            this.id = id;
            this.format = format;
            ModelPath = modelPath;
            GoldenPath = goldenPath;
            this.expected = expected;
            this.notes = notes;
            IsLocalCorpus = isLocalCorpus;
        }

        public static ModelFixtureEntry FromManifest(ModelFixtureRecord record, string modelPath, string goldenPath)
        {
            return new ModelFixtureEntry(record.id, record.format, modelPath, goldenPath, record.expected, record.notes);
        }

        public string Context(string field)
        {
            return id + " (" + format + ") " + field + "; model=" + ModelPath;
        }

        public string RequireGoldenPath()
        {
            Assert.That(GoldenPath, Is.Not.Null.And.Not.Empty, Context("committed golden path"));
            return GoldenPath!;
        }

        public override string ToString()
        {
            return id;
        }
    }
}
