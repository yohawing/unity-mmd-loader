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
                CollectionAssert.AreEqual(
                    expectedKey.interpolation.translationX,
                    actualKey.interpolation.translationX);
                CollectionAssert.AreEqual(
                    expectedKey.interpolation.translationY,
                    actualKey.interpolation.translationY);
                CollectionAssert.AreEqual(
                    expectedKey.interpolation.translationZ,
                    actualKey.interpolation.translationZ);
                CollectionAssert.AreEqual(
                    expectedKey.interpolation.rotation,
                    actualKey.interpolation.rotation);
                CollectionAssert.AreEqual(expectedKey.rawInterpolation, actualKey.rawInterpolation);
            }

            MmdMotionValidator.ThrowIfInvalid(actual);
        }

        [Test]
        public void ReadbackPreservesNonUniformRawBoneInterpolationAndManagedChannelParity()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            byte[] vmdBytes = BuildNonUniformBoneInterpolationVmdBytes();
            var parser = new NativeMmdParser();
            MmdModelDefinition model = parser.LoadModel(pmxBytes);
            MmdMotionDefinition expected = parser.LoadMotion(vmdBytes);

            MmdMotionDefinition actual = MmdNativeAuthoredMotionReadbackAdapter.Read(
                model,
                pmxBytes,
                vmdBytes);

            Assert.That(actual.boneKeyframes, Has.Count.EqualTo(expected.boneKeyframes.Count));
            for (int i = 0; i < expected.boneKeyframes.Count; i++)
            {
                MmdBoneKeyframeDefinition expectedKey = expected.boneKeyframes[i];
                MmdBoneKeyframeDefinition actualKey = actual.boneKeyframes[i];
                CollectionAssert.AreEqual(expectedKey.rawInterpolation, actualKey.rawInterpolation, "raw " + i);
                CollectionAssert.AreEqual(
                    expectedKey.interpolation.translationX,
                    actualKey.interpolation.translationX,
                    "translation X " + i);
                CollectionAssert.AreEqual(
                    expectedKey.interpolation.translationY,
                    actualKey.interpolation.translationY,
                    "translation Y " + i);
                CollectionAssert.AreEqual(
                    expectedKey.interpolation.translationZ,
                    actualKey.interpolation.translationZ,
                    "translation Z " + i);
                CollectionAssert.AreEqual(
                    expectedKey.interpolation.rotation,
                    actualKey.interpolation.rotation,
                    "rotation " + i);
            }
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
        public void ReadbackRejectsUnresolvedMorphNameWithUnsupportedException()
        {
            var model = new MmdModelDefinition();
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "known-morph"
            });

            var summary = new MmdVmdParseSummary(
                "unresolved-morph",
                maxFrame: 0,
                boneKeyframeCount: 0,
                morphKeyframeCount: 1,
                modelKeyframeCount: 0,
                constraintStateCount: 0);
            var descriptor = new MmdRuntimeFfiMethods.MorphTrackDescriptor
            {
                morphIndex = 1
            };
            var morphKeys = new[]
            {
                new[]
                {
                    new MmdRuntimeFfiMethods.MorphTrackKey
                    {
                        morphIndex = 1,
                        frame = 0,
                        weight = 0.5f
                    }
                }
            };

            MmdRuntimeUnsupportedException exception = Assert.Throws<MmdRuntimeUnsupportedException>(() =>
                MmdNativeMotionReadbackConverter.Build(
                    model,
                    summary,
                    Array.Empty<MmdRuntimeFfiMethods.VmdBoneKeyframe>(),
                    new[] { descriptor },
                    morphKeys,
                    Array.Empty<MmdRuntimeFfiMethods.VmdCameraKeyframe>(),
                    Array.Empty<MmdRuntimeFfiMethods.VmdLightKeyframe>(),
                    Array.Empty<MmdRuntimeFfiMethods.VmdSelfShadowKeyframe>(),
                    Array.Empty<MmdRuntimeFfiMethods.VmdPropertyKeyframe>(),
                    Array.Empty<MmdRuntimeFfiMethods.VmdPropertyIkEntry>(),
                    new byte[] { 1 }))!;

            Assert.That(exception.Message,
                Is.EqualTo("Native morph track index 1 is not present in MmdModelDefinition: track 0."));
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
        public void ReadbackCopiesPropertyVisibilityAndCp932IkNames()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            byte[] vmdBytes = BuildPropertyVmdBytes();
            MmdModelDefinition model = new NativeMmdParser().LoadModel(pmxBytes);

            MmdMotionDefinition actual = MmdNativeAuthoredMotionReadbackAdapter.Read(model, pmxBytes, vmdBytes);

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

        [Test]
        public void ReadbackFailsClosedWhenContextReportsUnresolvedModelBoneNames()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            byte[] vmdBytes = BuildVmdWithOneUnresolvedBoneName();
            MmdModelDefinition model = new NativeMmdParser().LoadModel(pmxBytes);

            MmdRuntimeUnsupportedException exception = Assert.Throws<MmdRuntimeUnsupportedException>(
                () => MmdNativeAuthoredMotionReadbackAdapter.Read(model, pmxBytes, vmdBytes))!;

            Assert.That(exception.Message, Does.Contain("skipped 1"));
            Assert.That(exception.Message, Does.Contain("unresolved"));
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

        private static byte[] BuildVmdWithOneUnresolvedBoneName()
        {
            byte[] bytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            WriteFixedSjisAt(bytes, 54, "missing-native-bone", 15);
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
