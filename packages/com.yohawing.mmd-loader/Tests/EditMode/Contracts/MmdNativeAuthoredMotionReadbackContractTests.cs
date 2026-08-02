#nullable enable

using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Mmd.Native;
using Mmd.Parser;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmdNativeAuthoredMotionReadbackContractTests
    {
        [Test]
        public void ReadbackMapsCompiledBoneTrackIndicesToModelNames()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            byte[] vmdBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(pmxBytes);
            MmdMotionDefinition expected = parser.LoadMotion(vmdBytes);

            MmdMotionDefinition actual = MmdNativeAuthoredMotionReadbackAdapter.Read(
                model,
                pmxBytes,
                vmdBytes);

            Assert.That(actual.targetModelName, Is.EqualTo(expected.targetModelName));
            Assert.That(actual.maxFrame, Is.EqualTo(expected.maxFrame));
            Assert.That(actual.modelKeyframes, Is.Empty);
            Assert.That(actual.boneKeyframes, Has.Count.EqualTo(expected.boneKeyframes.Count));
            for (int i = 0; i < expected.boneKeyframes.Count; i++)
            {
                MmdBoneKeyframeDefinition expectedKey = expected.boneKeyframes[i];
                MmdBoneKeyframeDefinition actualKey = actual.boneKeyframes[i];
                Assert.That(actualKey.boneName, Is.EqualTo(expectedKey.boneName));
                Assert.That(actualKey.frame, Is.EqualTo(expectedKey.frame));
                CollectionAssert.AreEqual(expectedKey.translation, actualKey.translation);
                CollectionAssert.AreEqual(expectedKey.rotation, actualKey.rotation);
            }

            MmdMotionValidator.ThrowIfInvalid(actual);
        }

        [Test]
        public void ReadbackMapsCompiledMorphTrackIndicesToModelNames()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_vertex_morph.pmx");
            byte[] vmdBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_vertex_morph_motion.vmd");
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(pmxBytes);
            MmdMotionDefinition expected = parser.LoadMotion(vmdBytes);

            MmdMotionDefinition actual = MmdNativeAuthoredMotionReadbackAdapter.Read(
                model,
                pmxBytes,
                vmdBytes);

            Assert.That(actual.boneKeyframes, Is.Empty);
            Assert.That(actual.modelKeyframes, Is.Empty);
            Assert.That(actual.morphKeyframes, Has.Count.EqualTo(expected.morphKeyframes.Count));
            for (int i = 0; i < expected.morphKeyframes.Count; i++)
            {
                MmdMorphKeyframeDefinition expectedKey = expected.morphKeyframes[i];
                MmdMorphKeyframeDefinition actualKey = actual.morphKeyframes[i];
                Assert.That(actualKey.morphName, Is.EqualTo(expectedKey.morphName));
                Assert.That(actualKey.frame, Is.EqualTo(expectedKey.frame));
                Assert.That(actualKey.weight, Is.EqualTo(expectedKey.weight));
            }

            MmdMotionValidator.ThrowIfInvalid(actual);
        }

        [Test]
        public void ReadbackCopiesRawCameraLightAndSelfShadowKeysAndSummaryCounts()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            var parser = new NativeMmdParser();

            byte[] cameraBytes = MmdTestFixtures.BuildCameraTrackVmdBytes("native-camera");
            MmdVmdParseSummary cameraSummary = MmdVmdBinarySummaryReader.Read(cameraBytes);
            MmdMotionDefinition camera = MmdNativeAuthoredMotionReadbackAdapter.Read(
                parser.LoadModel(pmxBytes),
                pmxBytes,
                cameraBytes);
            Assert.That(camera.cameraKeyframeCount, Is.EqualTo(cameraSummary.CameraKeyframeCount));
            Assert.That(camera.cameraKeyframes, Has.Count.EqualTo(cameraSummary.CameraKeyframeCount));
            Assert.That(camera.cameraKeyframes[0].frame, Is.EqualTo(0));
            Assert.That(camera.cameraKeyframes[0].distance, Is.EqualTo(-40.0f));
            Assert.That(camera.cameraKeyframes[1].frame, Is.EqualTo(30));
            Assert.That(camera.cameraKeyframes[1].viewAngle, Is.EqualTo(40));
            Assert.That(camera.cameraKeyframes[1].perspective, Is.False);

            byte[] sceneBytes = MmdTestFixtures.BuildSceneTrackVmdBytes("native-scene");
            MmdVmdParseSummary sceneSummary = MmdVmdBinarySummaryReader.Read(sceneBytes);
            MmdMotionDefinition scene = MmdNativeAuthoredMotionReadbackAdapter.Read(
                parser.LoadModel(pmxBytes),
                pmxBytes,
                sceneBytes);
            Assert.That(scene.lightKeyframeCount, Is.EqualTo(sceneSummary.LightKeyframeCount));
            Assert.That(scene.selfShadowKeyframeCount, Is.EqualTo(sceneSummary.SelfShadowKeyframeCount));
            Assert.That(scene.lightKeyframes, Has.Count.EqualTo(sceneSummary.LightKeyframeCount));
            Assert.That(scene.selfShadowKeyframes, Has.Count.EqualTo(sceneSummary.SelfShadowKeyframeCount));
            Assert.That(scene.lightKeyframes[1].frame, Is.EqualTo(30));
            Assert.That(scene.lightKeyframes[1].color[0], Is.EqualTo(1.0f));
            Assert.That(scene.lightKeyframes[1].direction[1], Is.EqualTo(-1.0f));
            Assert.That(scene.selfShadowKeyframes[0].mode, Is.EqualTo(1));
            Assert.That(scene.selfShadowKeyframes[1].distance, Is.EqualTo(0.4f));
        }

        [Test]
        public void ReadbackRejectsCompiledPropertyAndIkTrackInsteadOfReturningEmptyDto()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            byte[] vmdBytes = BuildPropertyVmdBytes();
            MmdModelDefinition model = new NativeMmdParser().LoadModel(pmxBytes);

            MmdRuntimeUnsupportedException exception = Assert.Throws<MmdRuntimeUnsupportedException>(
                () => MmdNativeAuthoredMotionReadbackAdapter.Read(model, pmxBytes, vmdBytes))!;

            Assert.That(exception.Message, Does.Contain("property/IK"));
            Assert.That(exception.Message, Does.Contain("visibility or IK names"));
        }

        private static byte[] BuildPropertyVmdBytes()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            WriteFixedAscii(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedAscii(writer, "native-property", 20);
            writer.Write(0u); // bone frames
            writer.Write(0u); // morph frames
            writer.Write(0u); // camera frames
            writer.Write(0u); // light frames
            writer.Write(0u); // self-shadow frames
            writer.Write(1u); // property frames
            writer.Write(14u);
            writer.Write((byte)0);
            writer.Write(1u); // IK states
            WriteFixedAscii(writer, "native_ik", 20);
            writer.Write((byte)1);
            return stream.ToArray();
        }

        private static void WriteFixedAscii(BinaryWriter writer, string value, int byteCount)
        {
            byte[] buffer = new byte[byteCount];
            byte[] encoded = Encoding.ASCII.GetBytes(value);
            Buffer.BlockCopy(encoded, 0, buffer, 0, Math.Min(encoded.Length, buffer.Length));
            writer.Write(buffer);
        }
    }
}
