#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mmd.Parser;
using UnityEngine;

namespace Mmd.Tests
{
    internal static class MmdPlayModeTestFixtures
    {
        internal const string MinimalTriangleModelName = "minimal-playmode-triangle";

        internal static MmdModelDefinition CreateMinimalTriangleModel(string modelName)
        {
            _ = modelName;
            var parser = new NativeMmdParser();
            return parser.LoadModel(File.ReadAllBytes(ResolvePackageFixture("test_1bone_cube.pmx")));
        }

        internal static MmdMotionDefinition CreateRootTranslationMotion(string targetModelName, int maxFrame = 10)
        {
            MmdModelDefinition model = CreateMinimalTriangleModel(targetModelName);
            string modelName = model.name ?? targetModelName;
            string boneName = model.bones.Count > 0 && !string.IsNullOrWhiteSpace(model.bones[0].name)
                ? model.bones[0].name
                : "root";
            var keyframes = new[]
            {
                new GeneratedVmdBoneKeyframe(boneName, 0, new Vector3(0.0f, 0.0f, 0.0f), Quaternion.identity),
                new GeneratedVmdBoneKeyframe(boneName, (uint)maxFrame, new Vector3(2.0f, 0.0f, 0.0f), Quaternion.identity)
            };
            var parser = new NativeMmdParser();
            return parser.LoadMotion(CreateVmdBytes(modelName, keyframes));
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

        private static string ResolvePackageFixture(string fileName)
        {
            string? projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException("Unity project root could not be resolved from Application.dataPath.");
            }

            string packageRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "packages", "com.yohawing.mmd-loader"));
            return Path.Combine(packageRoot, "Tests", "Fixtures", "Assets", fileName);
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

        private static void WriteFixedSjis(BinaryWriter writer, string value, int byteCount)
        {
            byte[] buffer = new byte[byteCount];
            byte[] encoded = Encoding.GetEncoding(932).GetBytes(value ?? string.Empty);
            Array.Copy(encoded, 0, buffer, 0, Math.Min(encoded.Length, buffer.Length));
            writer.Write(buffer);
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
}
