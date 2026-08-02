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
        public void LoadMotionPreservesRawCp932BoneNames()
        {
            byte[] vmdBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            MmdMotionDefinition actual = new NativeMmdParser().LoadMotion(vmdBytes);

            Assert.That(actual.boneKeyframes, Has.Count.EqualTo(6));
            for (int i = 0; i < actual.boneKeyframes.Count; i++)
            {
                MmdBoneKeyframeDefinition actualKey = actual.boneKeyframes[i];
                Assert.That(actualKey.boneName, Is.EqualTo("全ての親"));
            }

            MmdMotionValidator.ThrowIfInvalid(actual);
        }

        [Test]
        public void ReadbackPreservesNonUniformRawBoneInterpolationAndManagedChannelParity()
        {
            byte[] vmdBytes = BuildNonUniformBoneInterpolationVmdBytes();
            MmdMotionDefinition actual = new NativeMmdParser().LoadMotion(vmdBytes);

            Assert.That(actual.boneKeyframes, Has.Count.EqualTo(6));
            int[] expectedFrames = { 0, 9, 19, 29, 39, 49 };
            float[][] expectedRotations =
            {
                new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                new[] { -0.3826834f, 0.0f, 0.0f, 0.9238796f },
                new[] { 0.0f, 0.0f, -0.3826834f, 0.9238796f },
                new[] { 0.0f, 0.0f, 0.3826834f, 0.9238796f },
                new[] { 0.3826834f, 0.0f, 0.0f, 0.9238796f },
                new[] { 0.0f, 0.0f, 0.0f, 1.0f }
            };
            for (int i = 0; i < actual.boneKeyframes.Count; i++)
            {
                MmdBoneKeyframeDefinition actualKey = actual.boneKeyframes[i];
                Assert.That(actualKey.frame, Is.EqualTo(expectedFrames[i]));
                CollectionAssert.AreEqual(new[] { 0.0f, 0.0f, 0.0f }, actualKey.translation);
                for (int component = 0; component < expectedRotations[i].Length; component++)
                {
                    Assert.That(actualKey.rotation[component], Is.EqualTo(expectedRotations[i][component]).Within(1e-5f));
                }
                Assert.That(actualKey.rawInterpolation, Has.Length.EqualTo(64));
                CollectionAssert.AreEqual(
                    new[]
                    {
                        actualKey.rawInterpolation[0],
                        actualKey.rawInterpolation[4],
                        actualKey.rawInterpolation[8],
                        actualKey.rawInterpolation[12]
                    },
                    actualKey.interpolation.translationX);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        actualKey.rawInterpolation[1],
                        actualKey.rawInterpolation[5],
                        actualKey.rawInterpolation[9],
                        actualKey.rawInterpolation[13]
                    },
                    actualKey.interpolation.translationY);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        actualKey.rawInterpolation[2],
                        actualKey.rawInterpolation[6],
                        actualKey.rawInterpolation[10],
                        actualKey.rawInterpolation[14]
                    },
                    actualKey.interpolation.translationZ);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        actualKey.rawInterpolation[3],
                        actualKey.rawInterpolation[7],
                        actualKey.rawInterpolation[11],
                        actualKey.rawInterpolation[15]
                    },
                    actualKey.interpolation.rotation);
                for (int byteIndex = 0; byteIndex < actualKey.rawInterpolation.Length; byteIndex++)
                {
                    Assert.That(
                        actualKey.rawInterpolation[byteIndex],
                        Is.EqualTo((byte)((i * 17 + byteIndex) % 127)),
                        "raw " + i + ", byte " + byteIndex);
                }
            }
        }

        [Test]
        public void LoadMotionPreservesRawCp932MorphNames()
        {
            byte[] vmdBytes = BuildVmdWithCp932MorphName();
            MmdMotionDefinition actual = new NativeMmdParser().LoadMotion(vmdBytes);

            Assert.That(actual.boneKeyframes, Is.Empty);
            Assert.That(actual.modelKeyframes, Is.Empty);
            Assert.That(actual.morphKeyframes, Has.Count.EqualTo(2));
            for (int i = 0; i < actual.morphKeyframes.Count; i++)
            {
                MmdMorphKeyframeDefinition actualKey = actual.morphKeyframes[i];
                Assert.That(actualKey.morphName, Is.EqualTo(i == 0 ? "笑顔" : "blink"));
            }

            MmdMotionValidator.ThrowIfInvalid(actual);
        }

        [Test]
        public void ReadbackCopiesRawCameraLightAndSelfShadowKeysAndSummaryCounts()
        {
            var parser = new NativeMmdParser();

            byte[] cameraBytes = MmdTestFixtures.BuildCameraTrackVmdBytes("native-camera");
            MmdVmdParseSummary cameraSummary = MmdVmdNativeSummaryAdapter.Read(cameraBytes);
            MmdMotionDefinition camera = parser.LoadMotion(cameraBytes);
            Assert.That(camera.cameraKeyframeCount, Is.EqualTo(cameraSummary.CameraKeyframeCount));
            Assert.That(camera.cameraKeyframes, Has.Count.EqualTo(cameraSummary.CameraKeyframeCount));
            Assert.That(camera.cameraKeyframes[0].frame, Is.EqualTo(0));
            Assert.That(camera.cameraKeyframes[0].distance, Is.EqualTo(-40.0f));
            Assert.That(camera.cameraKeyframes[1].frame, Is.EqualTo(30));
            Assert.That(camera.cameraKeyframes[1].viewAngle, Is.EqualTo(40));
            Assert.That(camera.cameraKeyframes[1].perspective, Is.False);

            byte[] sceneBytes = MmdTestFixtures.BuildSceneTrackVmdBytes("native-scene");
            MmdVmdParseSummary sceneSummary = MmdVmdNativeSummaryAdapter.Read(sceneBytes);
            MmdMotionDefinition scene = parser.LoadMotion(sceneBytes);
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
        public void ReadbackCopiesPropertyVisibilityAndCp932IkNames()
        {
            byte[] vmdBytes = BuildPropertyVmdBytes();
            MmdMotionDefinition actual = new NativeMmdParser().LoadMotion(vmdBytes);

            Assert.That(actual.modelKeyframes, Has.Count.EqualTo(1));
            Assert.That(actual.modelKeyframes[0].visible, Is.False);
            Assert.That(actual.modelKeyframes[0].constraintStates, Has.Count.EqualTo(1));
            Assert.That(actual.modelKeyframes[0].constraintStates[0].boneName, Is.EqualTo("左IK"));
            Assert.That(actual.modelKeyframes[0].constraintStates[0].enabled, Is.True);
            MmdMotionValidator.ThrowIfInvalid(actual);
        }

        [Test]
        public void SharedContextReadbackUsesOneContextAndClipSurvivesContextDispose()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            byte[] bodyVmdBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            using var context = MmdRuntimeFfiVmdContext.Create(bodyVmdBytes);
            using MmdRuntimeFfiPlaybackSession session =
                MmdRuntimeFfiPlaybackSession.CreateFromVmdContext(pmxBytes, context);

            context.Dispose();
            context.Dispose();

            Assert.That(session.GetBoneTrackCount(), Is.GreaterThan(0));
            Assert.That(session.GetBoneTrackKeys(0), Is.Not.Empty);
        }

        [Test]
        public void SharedContextShortBufferReturnsBufferTooSmallWithoutPartialWrite()
        {
            byte[] vmdBytes = MmdTestFixtures.BuildSceneTrackVmdBytes("native-context-short-buffer");
            IntPtr context = MmdRuntimeFfiMethods.VmdContextCreateFromVmdBytes(
                vmdBytes,
                new IntPtr(vmdBytes.Length));
            Assert.That(context, Is.Not.EqualTo(IntPtr.Zero));

            int stride = System.Runtime.InteropServices.Marshal.SizeOf<MmdRuntimeFfiMethods.VmdLightKeyframe>();
            IntPtr shortBuffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(stride);
            try
            {
                byte[] sentinel = new byte[stride];
                for (int i = 0; i < sentinel.Length; i++)
                {
                    sentinel[i] = 0xA5;
                }

                System.Runtime.InteropServices.Marshal.Copy(sentinel, 0, shortBuffer, sentinel.Length);
                int status = MmdRuntimeFfiMethods.VmdContextCopyLightKeyframes(
                    context,
                    shortBuffer,
                    new IntPtr(1),
                    out IntPtr written);

                Assert.That(status, Is.EqualTo(MmdRuntimeFfiMethods.StatusBufferTooSmall));
                Assert.That(written, Is.EqualTo(IntPtr.Zero));
                byte[] after = new byte[stride];
                System.Runtime.InteropServices.Marshal.Copy(shortBuffer, after, 0, after.Length);
                Assert.That(after, Is.EqualTo(sentinel));
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(shortBuffer);
                MmdRuntimeFfiMethods.VmdContextFree(context);
            }
        }

        [Test]
        public void ContextDisposeRetainsHandleWhenCleanupFailsSoRetryCanSucceed()
        {
            int freeCount = 0;
            bool failFirstFree = true;
            var context = new MmdRuntimeFfiVmdContext(
                new IntPtr(1),
                _ =>
                {
                    freeCount++;
                    if (failFirstFree)
                    {
                        failFirstFree = false;
                        throw new InvalidOperationException("transient native cleanup failure");
                    }
                });

            Assert.Throws<InvalidOperationException>(() => context.Dispose());
            context.Dispose();
            context.Dispose();

            Assert.That(freeCount, Is.EqualTo(2));
            Assert.Throws<ObjectDisposedException>(() => context.GetNativeHandle());
        }

        [Test]
        public void SourceLessContextAndUnavailableBoundaryAreFailClosedAndDistinct()
        {
            Assert.Throws<ArgumentException>(() => MmdRuntimeFfiVmdContext.Create(Array.Empty<byte>()));

            MmdRuntimeNativeUnavailableException unavailable = MmdRuntimeNativeBoundary.Unavailable(
                "shared VMD context",
                new DllNotFoundException("machine-specific DLL path"));
            Assert.That(unavailable, Is.TypeOf<MmdRuntimeNativeUnavailableException>());
            Assert.That(unavailable.Message, Does.Contain("native is unavailable"));
            Assert.That(new MmdRuntimeUnsupportedException("feature missing"),
                Is.Not.TypeOf<MmdRuntimeNativeUnavailableException>());
        }

        private static byte[] BuildPropertyVmdBytes()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            WriteFixedAscii(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedSjis(writer, "native-property", 20);
            writer.Write(0u); // bone frames
            writer.Write(0u); // morph frames
            writer.Write(0u); // camera frames
            writer.Write(0u); // light frames
            writer.Write(0u); // self-shadow frames
            writer.Write(1u); // property frames
            writer.Write(14u);
            writer.Write((byte)0);
            writer.Write(1u); // IK states
            WriteFixedSjis(writer, "左IK", 20);
            writer.Write((byte)1);
            return stream.ToArray();
        }

        private static byte[] BuildNonUniformBoneInterpolationVmdBytes()
        {
            byte[] bytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            int boneCount = BitConverter.ToInt32(bytes, 50);
            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                int interpolationOffset = checked(54 + boneIndex * 111 + 47);
                for (int byteIndex = 0; byteIndex < 64; byteIndex++)
                {
                    bytes[interpolationOffset + byteIndex] = (byte)((boneIndex * 17 + byteIndex) % 127);
                }
            }

            return bytes;
        }

        private static byte[] BuildVmdWithCp932MorphName()
        {
            byte[] bytes = MmdTestFixtures.ReadFixtureAssetBytes("test_vertex_morph_motion.vmd");
            WriteFixedSjisAt(bytes, 58, "笑顔", 15);
            return bytes;
        }

        private static void WriteFixedSjisAt(byte[] destination, int offset, string value, int byteCount)
        {
            byte[] buffer = new byte[byteCount];
            byte[] encoded = Encoding.GetEncoding(932).GetBytes(value);
            Buffer.BlockCopy(encoded, 0, buffer, 0, Math.Min(encoded.Length, buffer.Length));
            Buffer.BlockCopy(buffer, 0, destination, offset, buffer.Length);
        }

        private static void WriteFixedAscii(BinaryWriter writer, string value, int byteCount)
        {
            byte[] buffer = new byte[byteCount];
            byte[] encoded = Encoding.ASCII.GetBytes(value);
            Buffer.BlockCopy(encoded, 0, buffer, 0, Math.Min(encoded.Length, buffer.Length));
            writer.Write(buffer);
        }

        private static void WriteFixedSjis(BinaryWriter writer, string value, int byteCount)
        {
            byte[] buffer = new byte[byteCount];
            byte[] encoded = Encoding.GetEncoding(932).GetBytes(value);
            Buffer.BlockCopy(encoded, 0, buffer, 0, Math.Min(encoded.Length, buffer.Length));
            writer.Write(buffer);
        }
    }
}
