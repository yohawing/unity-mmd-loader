#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using Mmd.Native;
using Mmd.Parser;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class NativeInteropContractTests
    {
        [Test]
        public void ParserFfiParsesPmxNonGeometryJsonFromPackageFixture()
        {
            string json = MmdParserFfiMethods.ParsePmxNonGeometryJson(
                MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx"));

            Assert.That(json, Does.Contain("\"metadata\""));
            Assert.That(json, Does.Contain("\"materials\""));
            Assert.That(json, Does.Contain("\"skeleton\""));
            Assert.That(json, Does.Not.Contain("\"geometry\""));
        }

        [TestCase("test_1bone_cube.pmx", 1, false)]
        [TestCase("GeneratedPmx/mixed-deform-types.pmx", 3, true)]
        public void ParseOnceHandleAndLegacyFallbackBuildEquivalentModel(
            string fixtureName,
            int minimumDistinctSkinningModes,
            bool requireMultiBoneWeighting)
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes(fixtureName);
            MmdModelDefinition handleModel = new NativeMmdParser().LoadModel(pmxBytes);

            string nonGeometryJson = MmdParserFfiMethods.ParsePmxNonGeometryJson(pmxBytes);
            NativeMmdParser.PmxModelSourceSnapshot legacySnapshot =
                JsonUtility.FromJson<NativeMmdParser.PmxModelSourceSnapshot>(nonGeometryJson)
                ?? new NativeMmdParser.PmxModelSourceSnapshot();
            legacySnapshot.geometry = NativeMmdParser.CreatePmxGeometryFromLegacyBuffers(pmxBytes);
            MmdModelDefinition legacyModel = NativeMmdParser.BuildModelDefinition(legacySnapshot);

            AssertModelGeometryAndSkinningParity(handleModel, legacyModel, fixtureName);

            Assert.That(handleModel.vertices.Select(vertex => vertex.skinningMode).Distinct().Count(),
                Is.GreaterThanOrEqualTo(minimumDistinctSkinningModes),
                fixtureName + " must retain the expected number of skinning modes.");
            Assert.That(handleModel.vertices.Any(vertex => vertex.boneIndices.Length > 1 &&
                vertex.boneWeights.Skip(1).Any(weight => weight > 0.0f)),
                Is.EqualTo(requireMultiBoneWeighting),
                fixtureName + " multi-bone weighting expectation.");
        }

        [Test]
        public void NativeParserUsesOneGeometryHandleAndFreesItAfterAllAccessors()
        {
            var reader = new CountingPmxGeometryReader();

            var geometry = NativeMmdParser.CreatePmxGeometryFromNativeHandle(new byte[] { 1 }, reader);

            Assert.That(reader.CreateCount, Is.EqualTo(1));
            Assert.That(reader.ReadAllCount, Is.EqualTo(1));
            Assert.That(reader.FreeCount, Is.EqualTo(1));

            CollectionAssert.AreEqual(new[] { 1.0f, 2.0f, 3.0f }, geometry.positions);
            CollectionAssert.AreEqual(new uint[] { 0, 0, 0 }, geometry.indices);
            CollectionAssert.AreEqual(new[] { "bdef1" }, geometry.skinningModes);
        }

        [Test]
        public void NativeParserFreesGeometryHandleWhenReadAllFails()
        {
            var reader = new CountingPmxGeometryReader { ThrowOnReadAll = true };

            Assert.Throws<InvalidOperationException>(() =>
                NativeMmdParser.CreatePmxGeometryFromNativeHandle(new byte[] { 1 }, reader));

            Assert.That(reader.CreateCount, Is.EqualTo(1));
            Assert.That(reader.ReadAllCount, Is.EqualTo(1));
            Assert.That(reader.FreeCount, Is.EqualTo(1));
        }

        [Test]
        public void NativeParserRejectsNullGeometryHandleWithoutFreeingIt()
        {
            var reader = new CountingPmxGeometryReader { CreatedHandle = IntPtr.Zero };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                NativeMmdParser.CreatePmxGeometryFromNativeHandle(new byte[] { 1 }, reader))!;

            Assert.That(exception.Message, Does.Contain("returned null"));
            Assert.That(reader.CreateCount, Is.EqualTo(1));
            Assert.That(reader.FreeCount, Is.EqualTo(0));
        }

        [Test]
        public void ParserFfiParsesPmxGeometryHandleFromPackageFixture()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            IntPtr geometry = MmdParserFfiMethods.CreatePmxGeometry(pmxBytes);
            Assert.That(geometry, Is.Not.EqualTo(IntPtr.Zero), "parse-once PMX geometry handle");

            try
            {
                float[] positions = MmdParserFfiMethods.ParsePmxGeometryPositions(geometry);
                float[] normals = MmdParserFfiMethods.ParsePmxGeometryNormals(geometry);
                float[] uvs = MmdParserFfiMethods.ParsePmxGeometryUvs(geometry);
                float[] edgeScale = MmdParserFfiMethods.ParsePmxGeometryEdgeScale(geometry);
                uint[] indices = MmdParserFfiMethods.ParsePmxGeometryIndices(geometry);
                uint[] skinIndices = MmdParserFfiMethods.ParsePmxGeometrySkinIndices(geometry);
                float[] skinWeights = MmdParserFfiMethods.ParsePmxGeometrySkinWeights(geometry);
                bool[] sdefEnabled = MmdParserFfiMethods.ParsePmxGeometrySdefEnabled(geometry);
                float[] sdefC = MmdParserFfiMethods.ParsePmxGeometrySdefC(geometry);
                float[] sdefR0 = MmdParserFfiMethods.ParsePmxGeometrySdefR0(geometry);
                float[] sdefR1 = MmdParserFfiMethods.ParsePmxGeometrySdefR1(geometry);
                string modesJson = MmdParserFfiMethods.ParsePmxGeometrySkinningModesJson(geometry);

                // positions: flat xyz, non-zero multiple of 3
                Assert.That(positions.Length % 3, Is.EqualTo(0), "positions.Length % 3");
                Assert.That(positions.Length, Is.GreaterThan(0), "positions vertex count");
                int vertexCount = positions.Length / 3;

                // normals: same flat xyz layout as positions
                Assert.That(normals.Length, Is.EqualTo(positions.Length), "normals.Length == positions.Length");

                // uvs: flat uv pairs
                Assert.That(uvs.Length, Is.EqualTo(vertexCount * 2), "uvs.Length == vertexCount*2");
                Assert.That(edgeScale.Length, Is.EqualTo(vertexCount), "edgeScale.Length == vertexCount");

                // indices: triangles, non-zero multiple of 3
                Assert.That(indices.Length % 3, Is.EqualTo(0), "indices.Length % 3");
                Assert.That(indices.Length, Is.GreaterThan(0), "indices non-empty");

                // skinning: 4 values per vertex
                Assert.That(skinIndices.Length, Is.EqualTo(vertexCount * 4), "skinIndices.Length == vertexCount*4");
                Assert.That(skinWeights.Length, Is.EqualTo(vertexCount * 4), "skinWeights.Length == vertexCount*4");

                // SDEF: one bool per vertex, xyz triples for C/R0/R1
                Assert.That(sdefEnabled.Length, Is.EqualTo(vertexCount), "sdefEnabled.Length == vertexCount");
                Assert.That(sdefC.Length, Is.EqualTo(vertexCount * 3), "sdefC.Length == vertexCount*3");
                Assert.That(sdefR0.Length, Is.EqualTo(vertexCount * 3), "sdefR0.Length == vertexCount*3");
                Assert.That(sdefR1.Length, Is.EqualTo(vertexCount * 3), "sdefR1.Length == vertexCount*3");

                // skinning modes JSON must be a non-empty JSON payload
                Assert.That(modesJson, Is.Not.Null.And.Not.Empty, "skinningModesJson non-empty");
            }
            finally
            {
                MmdParserFfiMethods.FreePmxGeometry(geometry);
            }
        }

        private static void AssertModelGeometryAndSkinningParity(
            MmdModelDefinition expected,
            MmdModelDefinition actual,
            string fixtureName)
        {
            Assert.That(actual.vertices.Count, Is.EqualTo(expected.vertices.Count), fixtureName + " vertex count");
            Assert.That(actual.indices, Is.EqualTo(expected.indices), fixtureName + " indices");
            for (int i = 0; i < expected.vertices.Count; i++)
            {
                MmdVertexDefinition expectedVertex = expected.vertices[i];
                MmdVertexDefinition actualVertex = actual.vertices[i];
                AssertFloatArraysEqual(expectedVertex.position, actualVertex.position, fixtureName + " position[" + i + "]");
                AssertFloatArraysEqual(expectedVertex.normal, actualVertex.normal, fixtureName + " normal[" + i + "]");
                AssertFloatArraysEqual(expectedVertex.uv, actualVertex.uv, fixtureName + " uv[" + i + "]");
                AssertFloatEqualNaNAware(expectedVertex.edgeScale, actualVertex.edgeScale, fixtureName + " edgeScale[" + i + "]");
                Assert.That(actualVertex.skinningMode, Is.EqualTo(expectedVertex.skinningMode), fixtureName + " skinningMode[" + i + "]");
                Assert.That(actualVertex.boneIndices, Is.EqualTo(expectedVertex.boneIndices), fixtureName + " boneIndices[" + i + "]");
                AssertFloatArraysEqual(expectedVertex.boneWeights, actualVertex.boneWeights, fixtureName + " boneWeights[" + i + "]");
                Assert.That(actualVertex.hasSdefParameters, Is.EqualTo(expectedVertex.hasSdefParameters), fixtureName + " hasSdef[" + i + "]");
                AssertFloatArraysEqual(expectedVertex.sdefC, actualVertex.sdefC, fixtureName + " sdefC[" + i + "]");
                AssertFloatArraysEqual(expectedVertex.sdefR0, actualVertex.sdefR0, fixtureName + " sdefR0[" + i + "]");
                AssertFloatArraysEqual(expectedVertex.sdefR1, actualVertex.sdefR1, fixtureName + " sdefR1[" + i + "]");
            }
        }

        private static void AssertFloatArraysEqual(float[] expected, float[] actual, string label)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), label + " length");
            for (int i = 0; i < expected.Length; i++)
            {
                if (float.IsNaN(expected[i]))
                {
                    Assert.That(actual[i], Is.NaN, label + "[" + i + "]");
                }
                else
                {
                    Assert.That(actual[i], Is.EqualTo(expected[i]).Within(0.000001f), label + "[" + i + "]");
                }
            }
        }

        private static void AssertFloatEqualNaNAware(float expected, float actual, string label)
        {
            if (float.IsNaN(expected))
            {
                Assert.That(actual, Is.NaN, label);
            }
            else if (float.IsInfinity(expected))
            {
                Assert.That(actual, Is.EqualTo(expected), label);
            }
            else
            {
                Assert.That(actual, Is.EqualTo(expected).Within(0.000001f), label);
            }
        }

        [Test]
        public void FastRuntimeNameAndAbiArePinned()
        {
            Assert.That(MmdRuntimeFfiMethods.LibraryName, Is.EqualTo("mmd_runtime_ffi"));
            Assert.That(MmdRuntimeFfiMethods.ExpectedAbiVersion, Is.EqualTo(3));
            Assert.That(MmdRuntimeFfiMethods.ValidateAbiVersion(), Is.EqualTo(3));
        }

        [Test]
        public void RuntimeFfiPinsVmdCameraSamplerEntrypoints()
        {
            AssertRuntimeFfiSignature("VmdCameraTrackCreateFromVmdBytes", typeof(IntPtr), typeof(byte[]), typeof(IntPtr));
            AssertRuntimeFfiSignature("VmdCameraTrackFrameCount", typeof(IntPtr), typeof(IntPtr));
            AssertRuntimeFfiSignature("VmdCameraTrackSample", typeof(byte), typeof(IntPtr), typeof(float), typeof(float[]), typeof(IntPtr));
            AssertRuntimeFfiSignature("VmdLightTrackCreateFromVmdBytes", typeof(IntPtr), typeof(byte[]), typeof(IntPtr));
            AssertRuntimeFfiSignature("VmdLightTrackFrameCount", typeof(IntPtr), typeof(IntPtr));
            AssertRuntimeFfiSignature("VmdLightTrackSample", typeof(byte), typeof(IntPtr), typeof(float), typeof(float[]), typeof(IntPtr));
            AssertRuntimeFfiSignature("VmdLightTrackFree", typeof(void), typeof(IntPtr));
            AssertRuntimeFfiSignature("VmdSelfShadowTrackCreateFromVmdBytes", typeof(IntPtr), typeof(byte[]), typeof(IntPtr));
            AssertRuntimeFfiSignature("VmdSelfShadowTrackFrameCount", typeof(IntPtr), typeof(IntPtr));
            AssertRuntimeFfiSignature("VmdSelfShadowTrackSample", typeof(byte), typeof(IntPtr), typeof(float), typeof(float[]), typeof(IntPtr));
            AssertRuntimeFfiSignature("VmdSelfShadowTrackFree", typeof(void), typeof(IntPtr));
            AssertRuntimeFfiSignature("VmdCameraTrackFree", typeof(void), typeof(IntPtr));
        }

        [Test]
        public void RuntimeFfiPinsClipFrameBatchEntrypoints()
        {
            AssertRuntimeFfiSignature(
                "InstanceClipFrameBatchWorldMatrixF32Len",
                typeof(IntPtr),
                typeof(IntPtr),
                typeof(IntPtr));
            AssertRuntimeFfiSignature(
                "InstanceClipFrameBatchMorphWeightF32Len",
                typeof(IntPtr),
                typeof(IntPtr),
                typeof(IntPtr));
            AssertRuntimeFfiSignature(
                "InstanceEvaluateClipFrameBatch",
                typeof(byte),
                typeof(IntPtr),
                typeof(IntPtr),
                typeof(float),
                typeof(float),
                typeof(IntPtr),
                typeof(uint),
                typeof(float[]),
                typeof(IntPtr),
                typeof(float[]),
                typeof(IntPtr));
        }

        [Test]
        public void RuntimeFfiPinsSparseReducedCurveEntrypointsWithoutDenseSampleBinding()
        {
            AssertRuntimeFfiSignature(
                "ReducedPoseCreateFromDense",
                typeof(int),
                typeof(IntPtr),
                typeof(ulong),
                typeof(float[]),
                typeof(IntPtr),
                typeof(float[]),
                typeof(IntPtr),
                typeof(IntPtr),
                typeof(float),
                typeof(float),
                typeof(uint),
                typeof(MmdRuntimeFfiMethods.ReductionTolerances),
                typeof(IntPtr).MakeByRefType());
            AssertRuntimeFfiSignature("ReducedPoseFree", typeof(void), typeof(IntPtr));
            AssertRuntimeFfiSignature(
                "ReducedPoseGenericCurveInfo",
                typeof(int),
                typeof(IntPtr),
                typeof(MmdRuntimeFfiMethods.GenericCurveInfo).MakeByRefType());
            AssertRuntimeFfiSignature(
                "ReducedPoseGenericCurveCount",
                typeof(int),
                typeof(IntPtr),
                typeof(IntPtr).MakeByRefType());
            AssertRuntimeFfiSignature(
                "ReducedPoseGenericCurveDescriptor",
                typeof(int),
                typeof(IntPtr),
                typeof(IntPtr),
                typeof(MmdRuntimeFfiMethods.GenericCurveDescriptor).MakeByRefType());
            AssertRuntimeFfiSignature(
                "ReducedPoseGenericCurveKeys",
                typeof(int),
                typeof(IntPtr),
                typeof(IntPtr),
                typeof(MmdRuntimeFfiMethods.GenericCurveKey[]),
                typeof(IntPtr),
                typeof(IntPtr),
                typeof(IntPtr).MakeByRefType());
            Assert.That(
                typeof(MmdRuntimeFfiMethods).GetMethod(
                    "ReducedPoseSample", BindingFlags.NonPublic | BindingFlags.Static),
                Is.Null,
                "the transitional dense reduced-pose sampler must not have a managed binding");
            Assert.That(
                typeof(MmdRuntimeFfiMethods).GetMethod(
                    "ReducedPoseUnityCurveCount", BindingFlags.NonPublic | BindingFlags.Static),
                Is.Null,
                "the deprecated Unity-specific curve ABI must not remain bound");
            Assert.That(Marshal.SizeOf<MmdRuntimeFfiMethods.ReductionTolerances>(), Is.EqualTo(20));
            Assert.That(
                Marshal.SizeOf<MmdRuntimeFfiMethods.GenericCurveInfo>(),
                Is.EqualTo(IntPtr.Size == 8 ? 72 : 64));
            Assert.That(
                Marshal.SizeOf<MmdRuntimeFfiMethods.GenericCurveDescriptor>(),
                Is.EqualTo(IntPtr.Size == 8 ? 40 : 36));
            Assert.That(
                Marshal.SizeOf<MmdRuntimeFfiMethods.GenericCurveKey>(),
                Is.EqualTo(IntPtr.Size == 8 ? 128 : 120));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.ReductionTolerances>("localPosition").ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.ReductionTolerances>("localRotationRadians").ToInt32(), Is.EqualTo(4));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.ReductionTolerances>("worldPosition").ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.ReductionTolerances>("worldRotationRadians").ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.ReductionTolerances>("morphWeight").ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.GenericCurveInfo>("modelIdentity").ToInt32(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.GenericCurveInfo>("frameCount").ToInt32(), Is.EqualTo(48));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.GenericCurveDescriptor>("targetIndex").ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.GenericCurveDescriptor>("keyCount").ToInt32(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.GenericCurveKey>("frame").ToInt32(), Is.EqualTo(IntPtr.Size));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.GenericCurveKey>("rotationX").ToInt32(), Is.EqualTo(IntPtr.Size == 8 ? 24 : 20));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.GenericCurveKey>("segmentCurrentInScalar").ToInt32(), Is.EqualTo(IntPtr.Size == 8 ? 120 : 116));

            foreach (string methodName in new[]
                     {
                         "ReducedPoseCreateFromDense",
                         "ReducedPoseFree",
                         "ReducedPoseGenericCurveInfo",
                         "ReducedPoseGenericCurveCount",
                         "ReducedPoseGenericCurveDescriptor",
                         "ReducedPoseGenericCurveKeys"
                     })
            {
                DllImportAttribute import = GetRuntimeFfiMethod(methodName).GetCustomAttribute<DllImportAttribute>()!;
                Assert.That(import, Is.Not.Null, methodName);
                Assert.That(import.CallingConvention, Is.EqualTo(CallingConvention.Cdecl), methodName);
            }

        }

        [Test]
        public void ReducedPoseZeroKeyBufferUsesSharedEmptyArray()
        {
            MmdRuntimeFfiMethods.GenericCurveKey[] keys =
                MmdRuntimeReducedPose.AllocateGenericCurveKeyBuffer(IntPtr.Zero);
            Assert.That(keys, Is.SameAs(Array.Empty<MmdRuntimeFfiMethods.GenericCurveKey>()));
        }

        [Test]
        public void ReducedPoseDenseInputHasExplicitMemorySafetyLimit()
        {
            const int WorldFloatsPerFrame = 16;
            int allowedFrames = checked((int)(
                MmdRuntimeFfiPlaybackSession.MaxReductionInputBytes /
                (WorldFloatsPerFrame * sizeof(float))));

            Assert.DoesNotThrow(() => MmdRuntimeFfiPlaybackSession.ThrowIfReductionInputTooLarge(
                WorldFloatsPerFrame, 0, allowedFrames));
            MmdRuntimeReductionInputTooLargeException exception =
                Assert.Throws<MmdRuntimeReductionInputTooLargeException>(() =>
                MmdRuntimeFfiPlaybackSession.ThrowIfReductionInputTooLarge(
                    WorldFloatsPerFrame, 0, allowedFrames + 1))!;
            Assert.That(exception.Message, Does.Contain("safety limit"));
        }

        [Test]
        public void UnityAnimationClipReductionKeepsPositionErrorWithinOneCentimeter()
        {
            MmdRuntimeFfiMethods.ReductionTolerances tolerances =
                MmdRuntimeFfiMethods.ReductionTolerances.ForUnityAnimationClip(0.1f);

            Assert.That(tolerances.localPosition * 0.1f, Is.EqualTo(0.01f).Within(1.0e-7f));
            Assert.That(tolerances.localRotationRadians, Is.EqualTo(0.005f));
            Assert.That(tolerances.worldPosition * 0.1f, Is.EqualTo(0.01f).Within(1.0e-7f));
            Assert.That(tolerances.worldRotationRadians, Is.EqualTo(0.005f));
            Assert.That(tolerances.morphWeight, Is.EqualTo(0.0001f));

            MmdRuntimeFfiMethods.ReductionTolerances unitScale =
                MmdRuntimeFfiMethods.ReductionTolerances.ForUnityAnimationClip(1.0f);
            Assert.That(unitScale.localPosition, Is.EqualTo(0.01f));
            Assert.That(unitScale.worldPosition, Is.EqualTo(0.01f));

            MmdRuntimeFfiMethods.ReductionTolerances highPrecision =
                MmdRuntimeFfiMethods.ReductionTolerances.ForUnityAnimationClip(
                    0.1f,
                    highPrecision: true);
            Assert.That(
                highPrecision.localPosition * 0.1f,
                Is.EqualTo(0.001f).Within(1.0e-7f));
            Assert.That(
                highPrecision.worldPosition * 0.1f,
                Is.EqualTo(0.001f).Within(1.0e-7f));
        }

        [Test]
        public void RuntimeFfiClipFrameBatchMatchesSequentialEvaluation()
        {
#if !UNITY_EDITOR_WIN
            Assert.Ignore("mmd-runtime batch playback is only distributed for the Windows Editor.");
#endif
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            byte[] vmdBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            using var session = MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes);
            const int frameCount = 3;
            var batchWorld = new float[session.WorldMatrixFloatCount * frameCount];
            var batchMorphs = new float[session.MorphWeightCount * frameCount];

            session.EvaluateBatch(0.0f, 1.0f, frameCount, 0, batchWorld, batchMorphs);

            var sequentialWorld = new float[session.WorldMatrixFloatCount];
            var sequentialMorphs = new float[session.MorphWeightCount];
            var sequentialIk = new byte[session.IkEnabledCount];
            for (int frame = 0; frame < frameCount; frame++)
            {
                session.EvaluateAndCopy(frame, sequentialWorld, sequentialMorphs, sequentialIk);
                CollectionAssert.AreEqual(
                    sequentialWorld,
                    batchWorld.Skip(frame * session.WorldMatrixFloatCount)
                        .Take(session.WorldMatrixFloatCount)
                        .ToArray(),
                    $"world matrices at frame {frame}");
                CollectionAssert.AreEqual(
                    sequentialMorphs,
                    batchMorphs.Skip(frame * session.MorphWeightCount)
                        .Take(session.MorphWeightCount)
                        .ToArray(),
                    $"morph weights at frame {frame}");
            }
        }

        [Test]
        public void ReducedPoseEnumeratesSparseKeysAfterSourceSessionIsDisposed()
        {
#if !UNITY_EDITOR_WIN
            Assert.Ignore("mmd-runtime reduced curves are only distributed for the Windows Editor.");
#endif
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            byte[] vmdBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            MmdRuntimeReducedPose reducedPose;
            using (var session = MmdRuntimeFfiPlaybackSession.Create(pmxBytes, vmdBytes))
            {
                reducedPose = session.ReduceBatch(
                    0.0f, 10, 0, MmdRuntimeFfiMethods.ReductionTolerances.Default);
            }

            try
            {
                MmdRuntimeFfiMethods.GenericCurveInfo info = reducedPose.GetGenericCurveInfo();
                Assert.That(info.abiVersion, Is.EqualTo(MmdRuntimeFfiMethods.GenericCurveAbiVersionV1));
                Assert.That(info.boneCount.ToInt64(), Is.EqualTo(1));
                int trackCount = reducedPose.GetGenericCurveCount();
                Assert.That(trackCount, Is.EqualTo(1), "one bone exposes one generic transform track");
                for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
                {
                    MmdRuntimeFfiMethods.GenericCurveDescriptor descriptor =
                        reducedPose.GetGenericCurveDescriptor(trackIndex);
                    MmdRuntimeFfiMethods.GenericCurveKey[] keys =
                        reducedPose.GetGenericCurveKeys(trackIndex);
                    Assert.That(keys.Length, Is.EqualTo(descriptor.keyCount.ToInt64()));
                    Assert.That(keys, Is.Not.Empty);
                    Assert.That(keys.All(key =>
                        float.IsFinite(key.frame) &&
                        float.IsFinite(key.translationX) &&
                        float.IsFinite(key.rotationW) &&
                        float.IsFinite(key.segmentCurrentInRotationZ)), Is.True);
                }
            }
            finally
            {
                reducedPose.Dispose();
                reducedPose.Dispose();
            }
        }

        [Test]
        public void RuntimeFfiSamplesVmdCameraIntoCallerOwnedBuffer()
        {
            string fixturePath = Path.Combine(
                MmdTestFixtures.RepositoryRoot,
                "native",
                "mmd-anim",
                "crates",
                "mmd-anim-format",
                "fixtures",
                "vmd",
                "simple_camera.vmd");
            Assert.That(fixturePath, Does.Exist);

            byte[] vmdBytes = File.ReadAllBytes(fixturePath);
            float[] values = new float[9];

            IntPtr track = MmdRuntimeFfiMethods.VmdCameraTrackCreateFromVmdBytes(vmdBytes, new IntPtr(vmdBytes.Length));
            Assert.That(track, Is.Not.EqualTo(IntPtr.Zero));
            try
            {
                Array.Clear(values, 0, values.Length);
                Assert.That(
                    MmdRuntimeFfiMethods.VmdCameraTrackSample(track, 22.5f, values, new IntPtr(values.Length)),
                    Is.Not.Zero,
                    "track camera sample");
                AssertCameraSample(values);

                float[] shortBuffer = new float[8];
                Assert.That(
                    MmdRuntimeFfiMethods.VmdCameraTrackSample(track, 22.5f, shortBuffer, new IntPtr(shortBuffer.Length)),
                    Is.Zero,
                    "short buffer must be rejected");
            }
            finally
            {
                MmdRuntimeFfiMethods.VmdCameraTrackFree(track);
            }
        }

        [Test]
        public void RuntimeFfiSamplesVmdLightIntoCallerOwnedBuffer()
        {
            byte[] vmdBytes = MmdTestFixtures.BuildSceneTrackVmdBytes("light_shadow");
            float[] values = new float[6];

            IntPtr track = MmdRuntimeFfiMethods.VmdLightTrackCreateFromVmdBytes(vmdBytes, new IntPtr(vmdBytes.Length));
            Assert.That(track, Is.Not.EqualTo(IntPtr.Zero));
            try
            {
                Assert.That(MmdRuntimeFfiMethods.VmdLightTrackFrameCount(track).ToInt64(), Is.EqualTo(2));
                Array.Clear(values, 0, values.Length);
                Assert.That(
                    MmdRuntimeFfiMethods.VmdLightTrackSample(track, 20.0f, values, new IntPtr(values.Length)),
                    Is.Not.Zero,
                    "track light sample");
                AssertLightSample(values);

                float[] shortBuffer = new float[5];
                Assert.That(
                    MmdRuntimeFfiMethods.VmdLightTrackSample(track, 20.0f, shortBuffer, new IntPtr(shortBuffer.Length)),
                    Is.Zero,
                    "short buffer must be rejected");
            }
            finally
            {
                MmdRuntimeFfiMethods.VmdLightTrackFree(track);
            }
        }

        [Test]
        public void RuntimeFfiSamplesVmdSelfShadowIntoCallerOwnedBuffer()
        {
            byte[] vmdBytes = MmdTestFixtures.BuildSceneTrackVmdBytes("light_shadow");
            float[] values = new float[2];

            IntPtr track = MmdRuntimeFfiMethods.VmdSelfShadowTrackCreateFromVmdBytes(vmdBytes, new IntPtr(vmdBytes.Length));
            Assert.That(track, Is.Not.EqualTo(IntPtr.Zero));
            try
            {
                Assert.That(MmdRuntimeFfiMethods.VmdSelfShadowTrackFrameCount(track).ToInt64(), Is.EqualTo(2));
                Array.Clear(values, 0, values.Length);
                Assert.That(
                    MmdRuntimeFfiMethods.VmdSelfShadowTrackSample(track, 20.0f, values, new IntPtr(values.Length)),
                    Is.Not.Zero,
                    "track self-shadow sample");
                Assert.That(values[0], Is.EqualTo(1.0f).Within(0.0001f), "mode");
                Assert.That(values[1], Is.EqualTo(0.3f).Within(0.0001f), "distance");

                float[] shortBuffer = new float[1];
                Assert.That(
                    MmdRuntimeFfiMethods.VmdSelfShadowTrackSample(track, 20.0f, shortBuffer, new IntPtr(shortBuffer.Length)),
                    Is.Zero,
                    "short buffer must be rejected");
            }
            finally
            {
                MmdRuntimeFfiMethods.VmdSelfShadowTrackFree(track);
            }
        }

        [Test]
        public void WindowsPluginLayoutContainsOnlyRuntimeFfi()
        {
            string pluginRoot = Path.Combine(MmdTestFixtures.PackageRoot, "Runtime", "Plugins", "x86_64");
            string[] dllNames = Directory.GetFiles(pluginRoot, "*.dll")
                .Select(Path.GetFileName)
                .OrderBy(name => name, System.StringComparer.Ordinal)
                .ToArray();

            Assert.That(Path.Combine(pluginRoot, "mmd_runtime_ffi.dll"), Does.Exist);
            Assert.That(Path.Combine(pluginRoot, "mmd_runtime_ffi.dll.meta"), Does.Exist);
            Assert.That(Path.Combine(pluginRoot, "mmd_bullet.dll"), Does.Not.Exist);
            Assert.That(Path.Combine(pluginRoot, "mmd_bullet.dll.meta"), Does.Not.Exist);
            CollectionAssert.AreEqual(
                new[] { "mmd_runtime_ffi.dll" },
                dllNames);
            Assert.That(Path.Combine(pluginRoot, "yohawing_mmd_unity_native.dll"), Does.Not.Exist);
            Assert.That(Path.Combine(pluginRoot, "yohawing_mmd_unity_native.dll.meta"), Does.Not.Exist);
        }

        private static void AssertRuntimeFfiSignature(string methodName, Type returnType, params Type[] parameterTypes)
        {
            MethodInfo method = GetRuntimeFfiMethod(methodName);
            Assert.That(method, Is.Not.Null, methodName);
            Assert.That(method.ReturnType, Is.EqualTo(returnType), methodName);
            CollectionAssert.AreEqual(parameterTypes, method.GetParameters().Select(parameter => parameter.ParameterType).ToArray(), methodName);
        }

        private static MethodInfo GetRuntimeFfiMethod(string methodName)
        {
            MethodInfo? method = typeof(MmdRuntimeFfiMethods).GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return method!;
        }

        private static void AssertCameraSample(float[] values)
        {
            Assert.That(values.Length, Is.EqualTo(9));
            Assert.That(values[0], Is.EqualTo(-40.25f).Within(0.0001f), "distance");
            Assert.That(values[1], Is.EqualTo(-0.25f).Within(0.0001f), "position.x");
            Assert.That(values[2], Is.EqualTo(6.0f).Within(0.0001f), "position.y");
            Assert.That(values[3], Is.EqualTo(1.625f).Within(0.0001f), "position.z");
            Assert.That(values[4], Is.EqualTo(-0.1f).Within(0.0001f), "rotation.x");
            Assert.That(values[5], Is.EqualTo(-0.1f).Within(0.0001f), "rotation.y");
            Assert.That(values[6], Is.EqualTo(0.75f).Within(0.0001f), "rotation.z");
            Assert.That(values[7], Is.EqualTo(47.5f).Within(0.0001f), "fov");
            Assert.That(values[8], Is.EqualTo(1.0f).Within(0.0001f), "perspective");
        }

        private static void AssertLightSample(float[] values)
        {
            Assert.That(values.Length, Is.EqualTo(6));
            Assert.That(values[0], Is.EqualTo(0.5f).Within(0.0001f), "color.r");
            Assert.That(values[1], Is.EqualTo(0.25f).Within(0.0001f), "color.g");
            Assert.That(values[2], Is.EqualTo(0.5f).Within(0.0001f), "color.b");
            Assert.That(values[3], Is.EqualTo(0.5f).Within(0.0001f), "direction.x");
            Assert.That(values[4], Is.EqualTo(-0.5f).Within(0.0001f), "direction.y");
            Assert.That(values[5], Is.EqualTo(0.0f).Within(0.0001f), "direction.z");
        }

        private sealed class CountingPmxGeometryReader : IPmxGeometryReader
        {
            internal int CreateCount { get; private set; }
            internal int ReadAllCount { get; private set; }
            internal int FreeCount { get; private set; }
            internal bool ThrowOnReadAll { get; set; }
            internal IntPtr CreatedHandle { get; set; } = new IntPtr(1);

            public IntPtr Create(byte[] data)
            {
                CreateCount++;
                return CreatedHandle;
            }

            public PmxGeometryData ReadAll(IntPtr geometry)
            {
                ReadAllCount++;
                if (ThrowOnReadAll)
                {
                    throw new InvalidOperationException("simulated geometry read failure");
                }

                return new PmxGeometryData
                {
                    skinningModesJson = "{\"skinningModes\":[\"bdef1\"]}",
                    positions = new[] { 1.0f, 2.0f, 3.0f },
                    normals = new[] { 0.0f, 1.0f, 0.0f },
                    uvs = new[] { 0.0f, 1.0f },
                    edgeScale = new[] { 1.0f },
                    indices = new uint[] { 0, 0, 0 },
                    skinIndices = new uint[] { 0, 0, 0, 0 },
                    skinWeights = new[] { 1.0f, 0.0f, 0.0f, 0.0f },
                    hasSdefParameters = new[] { false },
                    sdefC = new[] { 0.0f, 0.0f, 0.0f },
                    sdefR0 = new[] { 0.0f, 0.0f, 0.0f },
                    sdefR1 = new[] { 0.0f, 0.0f, 0.0f },
                };
            }

            public void Free(IntPtr geometry)
            {
                FreeCount++;
            }
        }

    }
}
