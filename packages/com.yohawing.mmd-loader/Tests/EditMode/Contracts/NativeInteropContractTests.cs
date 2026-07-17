#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Mmd.Native;
using Mmd.Parser;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class NativeInteropContractTests
    {
        [Test]
        public void ParserFfiPinsActiveJsonEntrypoints()
        {
            Assert.That(MmdParserFfiMethods.LibraryName, Is.EqualTo("mmd_runtime_ffi"));
            Assert.That(MmdParserFfiMethods.ByteBufferFreeEntryPoint, Is.EqualTo("mmd_runtime_byte_buffer_free"));
            Assert.That(MmdParserFfiMethods.ParseVmdJsonEntryPoint, Is.EqualTo("mmd_runtime_parse_vmd_json"));
            Assert.That(MmdParserFfiMethods.ParsePmxNonGeometryJsonEntryPoint, Is.EqualTo("mmd_runtime_parse_pmx_non_geometry_json"));

            AssertPrivateFfiSignatureReturnName("ParseVmdJsonBuffer", "ByteBuffer", typeof(byte[]), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("ParsePmxNonGeometryJsonBuffer", "ByteBuffer", typeof(byte[]), typeof(IntPtr));
        }

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

        [Test]
        public void ParserFfiPinsPmxGeometryBufferEntrypoints()
        {
            // Static contract checks only; no native functions are invoked.
            // These must pass even against the current locked package DLL.
            Assert.That(MmdParserFfiMethods.ParsePmxPositionsBufferEntryPoint, Is.EqualTo("mmd_runtime_parse_pmx_positions_buffer"));
            Assert.That(MmdParserFfiMethods.ParsePmxNormalsBufferEntryPoint, Is.EqualTo("mmd_runtime_parse_pmx_normals_buffer"));
            Assert.That(MmdParserFfiMethods.ParsePmxUvsBufferEntryPoint, Is.EqualTo("mmd_runtime_parse_pmx_uvs_buffer"));
            Assert.That(MmdParserFfiMethods.ParsePmxIndicesBufferEntryPoint, Is.EqualTo("mmd_runtime_parse_pmx_indices_buffer"));
            Assert.That(MmdParserFfiMethods.ParsePmxSkinIndicesBufferEntryPoint, Is.EqualTo("mmd_runtime_parse_pmx_skin_indices_buffer"));
            Assert.That(MmdParserFfiMethods.ParsePmxSkinWeightsBufferEntryPoint, Is.EqualTo("mmd_runtime_parse_pmx_skin_weights_buffer"));
            Assert.That(MmdParserFfiMethods.ParsePmxSdefEnabledBufferEntryPoint, Is.EqualTo("mmd_runtime_parse_pmx_sdef_enabled_buffer"));
            Assert.That(MmdParserFfiMethods.ParsePmxSdefCBufferEntryPoint, Is.EqualTo("mmd_runtime_parse_pmx_sdef_c_buffer"));
            Assert.That(MmdParserFfiMethods.ParsePmxSdefR0BufferEntryPoint, Is.EqualTo("mmd_runtime_parse_pmx_sdef_r0_buffer"));
            Assert.That(MmdParserFfiMethods.ParsePmxSdefR1BufferEntryPoint, Is.EqualTo("mmd_runtime_parse_pmx_sdef_r1_buffer"));
            Assert.That(MmdParserFfiMethods.ParsePmxSkinningModesJsonEntryPoint, Is.EqualTo("mmd_runtime_parse_pmx_skinning_modes_json"));

            // Private DllImport signature checks (reflection-based, no invocation).
            AssertPrivateFfiSignatureReturnName("ParsePmxPositionsBuffer", "ByteBuffer", typeof(byte[]), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("ParsePmxNormalsBuffer", "ByteBuffer", typeof(byte[]), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("ParsePmxUvsBuffer", "ByteBuffer", typeof(byte[]), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("ParsePmxIndicesBuffer", "ByteBuffer", typeof(byte[]), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("ParsePmxSkinIndicesBuffer", "ByteBuffer", typeof(byte[]), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("ParsePmxSkinWeightsBuffer", "ByteBuffer", typeof(byte[]), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("ParsePmxSdefEnabledBuffer", "ByteBuffer", typeof(byte[]), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("ParsePmxSdefCBuffer", "ByteBuffer", typeof(byte[]), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("ParsePmxSdefR0Buffer", "ByteBuffer", typeof(byte[]), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("ParsePmxSdefR1Buffer", "ByteBuffer", typeof(byte[]), typeof(IntPtr));
            AssertPrivateFfiSignatureReturnName("ParsePmxSkinningModesJsonBuffer", "ByteBuffer", typeof(byte[]), typeof(IntPtr));
        }

        [Test]
        public void ParserFfiParsesAllPmxGeometryBuffersFromPackageFixture()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");

            float[] positions = MmdParserFfiMethods.ParsePmxPositions(pmxBytes);
            float[] normals = MmdParserFfiMethods.ParsePmxNormals(pmxBytes);
            float[] uvs = MmdParserFfiMethods.ParsePmxUvs(pmxBytes);
            uint[] indices = MmdParserFfiMethods.ParsePmxIndices(pmxBytes);
            uint[] skinIndices = MmdParserFfiMethods.ParsePmxSkinIndices(pmxBytes);
            float[] skinWeights = MmdParserFfiMethods.ParsePmxSkinWeights(pmxBytes);
            bool[] sdefEnabled = MmdParserFfiMethods.ParsePmxSdefEnabled(pmxBytes);
            float[] sdefC = MmdParserFfiMethods.ParsePmxSdefC(pmxBytes);
            float[] sdefR0 = MmdParserFfiMethods.ParsePmxSdefR0(pmxBytes);
            float[] sdefR1 = MmdParserFfiMethods.ParsePmxSdefR1(pmxBytes);
            string modesJson = MmdParserFfiMethods.ParsePmxSkinningModesJson(pmxBytes);

            // positions: flat xyz, non-zero multiple of 3
            Assert.That(positions.Length % 3, Is.EqualTo(0), "positions.Length % 3");
            Assert.That(positions.Length, Is.GreaterThan(0), "positions vertex count");
            int vertexCount = positions.Length / 3;

            // normals: same flat xyz layout as positions
            Assert.That(normals.Length, Is.EqualTo(positions.Length), "normals.Length == positions.Length");

            // uvs: flat uv pairs
            Assert.That(uvs.Length, Is.EqualTo(vertexCount * 2), "uvs.Length == vertexCount*2");

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

        [Test]
        public void FastRuntimeAndPhysicsWrapperNamesAreSeparate()
        {
            Assert.That(MmdRuntimeFfiMethods.LibraryName, Is.EqualTo("mmd_runtime_ffi"));
            Assert.That(MmdRuntimeFfiMethods.ExpectedAbiVersion, Is.EqualTo(2));
            Assert.That(MmdRuntimeFfiMethods.ValidateAbiVersion(), Is.EqualTo(2));
            Assert.That(MmdNativePhysicsMethods.LibraryName, Is.EqualTo("mmd_bullet"));
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
                "ReducedPoseUnityCurveCount",
                typeof(int),
                typeof(IntPtr),
                typeof(float),
                typeof(bool),
                typeof(IntPtr).MakeByRefType());
            AssertRuntimeFfiSignature(
                "ReducedPoseUnityCurveDescriptor",
                typeof(int),
                typeof(IntPtr),
                typeof(float),
                typeof(bool),
                typeof(IntPtr),
                typeof(MmdRuntimeFfiMethods.UnityCurveDescriptor).MakeByRefType());
            AssertRuntimeFfiSignature(
                "ReducedPoseUnityCurveKeys",
                typeof(int),
                typeof(IntPtr),
                typeof(float),
                typeof(bool),
                typeof(IntPtr),
                typeof(MmdRuntimeFfiMethods.UnityCurveKey[]),
                typeof(IntPtr),
                typeof(IntPtr).MakeByRefType());
            Assert.That(
                typeof(MmdRuntimeFfiMethods).GetMethod(
                    "ReducedPoseSample", BindingFlags.NonPublic | BindingFlags.Static),
                Is.Null,
                "the transitional dense reduced-pose sampler must not have a managed binding");
            Assert.That(Marshal.SizeOf<MmdRuntimeFfiMethods.ReductionTolerances>(), Is.EqualTo(20));
            Assert.That(Marshal.SizeOf<MmdRuntimeFfiMethods.UnityCurveKey>(), Is.EqualTo(16));
            Assert.That(
                Marshal.SizeOf<MmdRuntimeFfiMethods.UnityCurveDescriptor>(),
                Is.EqualTo(IntPtr.Size == 8 ? 24 : 16));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.ReductionTolerances>("localPosition").ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.ReductionTolerances>("localRotationRadians").ToInt32(), Is.EqualTo(4));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.ReductionTolerances>("worldPosition").ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.ReductionTolerances>("worldRotationRadians").ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.ReductionTolerances>("morphWeight").ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.UnityCurveDescriptor>("semantic").ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.UnityCurveDescriptor>("targetIndex").ToInt32(), Is.EqualTo(4));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.UnityCurveDescriptor>("axis").ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.UnityCurveDescriptor>("keyCount").ToInt32(), Is.EqualTo(IntPtr.Size == 8 ? 16 : 12));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.UnityCurveKey>("timeSeconds").ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.UnityCurveKey>("value").ToInt32(), Is.EqualTo(4));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.UnityCurveKey>("inTangent").ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<MmdRuntimeFfiMethods.UnityCurveKey>("outTangent").ToInt32(), Is.EqualTo(12));

            foreach (string methodName in new[]
                     {
                         "ReducedPoseCreateFromDense",
                         "ReducedPoseFree",
                         "ReducedPoseUnityCurveCount",
                         "ReducedPoseUnityCurveDescriptor",
                         "ReducedPoseUnityCurveKeys"
                     })
            {
                DllImportAttribute import = GetRuntimeFfiMethod(methodName).GetCustomAttribute<DllImportAttribute>()!;
                Assert.That(import, Is.Not.Null, methodName);
                Assert.That(import.CallingConvention, Is.EqualTo(CallingConvention.Cdecl), methodName);
            }

            foreach (string methodName in new[]
                     {
                         "ReducedPoseUnityCurveCount",
                         "ReducedPoseUnityCurveDescriptor",
                         "ReducedPoseUnityCurveKeys"
                     })
            {
                ParameterInfo flipZ = GetRuntimeFfiMethod(methodName).GetParameters()[2];
                MarshalAsAttribute marshalAs = flipZ.GetCustomAttribute<MarshalAsAttribute>()!;
                Assert.That(marshalAs, Is.Not.Null, methodName + " flipZ");
                Assert.That(marshalAs.Value, Is.EqualTo(UnmanagedType.I1), methodName + " flipZ");
            }
        }

        [Test]
        public void ReducedPoseZeroKeyBufferUsesSharedEmptyArray()
        {
            MmdRuntimeFfiMethods.UnityCurveKey[] keys =
                MmdRuntimeReducedPose.AllocateUnityCurveKeyBuffer(IntPtr.Zero);
            Assert.That(keys, Is.SameAs(Array.Empty<MmdRuntimeFfiMethods.UnityCurveKey>()));
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
                int curveCount = reducedPose.GetUnityCurveCount(30.0f, true);
                Assert.That(curveCount, Is.EqualTo(6), "one bone exposes translation XYZ and Euler XYZ");
                for (int curveIndex = 0; curveIndex < curveCount; curveIndex++)
                {
                    MmdRuntimeFfiMethods.UnityCurveDescriptor descriptor =
                        reducedPose.GetUnityCurveDescriptor(30.0f, true, curveIndex);
                    MmdRuntimeFfiMethods.UnityCurveKey[] keys =
                        reducedPose.GetUnityCurveKeys(30.0f, true, curveIndex);
                    Assert.That(keys.Length, Is.EqualTo(descriptor.keyCount.ToInt64()));
                    Assert.That(keys, Is.Not.Empty);
                    Assert.That(keys.All(key =>
                        float.IsFinite(key.timeSeconds) &&
                        float.IsFinite(key.value) &&
                        float.IsFinite(key.inTangent) &&
                        float.IsFinite(key.outTangent)), Is.True);
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
            byte[] vmdBytes = BuildSceneTrackVmdBytes();
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
            byte[] vmdBytes = BuildSceneTrackVmdBytes();
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
        public void WindowsPluginLayoutContainsRuntimeAndPhysicsButNoLegacyParserDll()
        {
            string pluginRoot = Path.Combine(MmdTestFixtures.PackageRoot, "Runtime", "Plugins", "x86_64");
            string[] dllNames = Directory.GetFiles(pluginRoot, "*.dll")
                .Select(Path.GetFileName)
                .OrderBy(name => name, System.StringComparer.Ordinal)
                .ToArray();

            Assert.That(Path.Combine(pluginRoot, "mmd_runtime_ffi.dll"), Does.Exist);
            Assert.That(Path.Combine(pluginRoot, "mmd_runtime_ffi.dll.meta"), Does.Exist);
            Assert.That(Path.Combine(pluginRoot, "mmd_bullet.dll"), Does.Exist);
            Assert.That(Path.Combine(pluginRoot, "mmd_bullet.dll.meta"), Does.Exist);
            CollectionAssert.AreEqual(
                new[] { "mmd_bullet.dll", "mmd_runtime_ffi.dll" },
                dllNames);
            Assert.That(Path.Combine(pluginRoot, "yohawing_mmd_unity_native.dll"), Does.Not.Exist);
            Assert.That(Path.Combine(pluginRoot, "yohawing_mmd_unity_native.dll.meta"), Does.Not.Exist);
        }

        private static void AssertPrivateFfiSignature(string methodName, Type returnType, params Type[] parameterTypes)
        {
            MethodInfo method = GetPrivateFfiMethod(methodName);
            Assert.That(method.ReturnType, Is.EqualTo(returnType), methodName);
            CollectionAssert.AreEqual(parameterTypes, method.GetParameters().Select(parameter => parameter.ParameterType).ToArray(), methodName);
        }

        private static void AssertPrivateFfiSignatureReturnName(string methodName, string returnTypeName, params Type[] parameterTypes)
        {
            MethodInfo method = GetPrivateFfiMethod(methodName);
            Assert.That(method.ReturnType.Name, Is.EqualTo(returnTypeName), methodName);
            CollectionAssert.AreEqual(parameterTypes, method.GetParameters().Select(parameter => parameter.ParameterType).ToArray(), methodName);
        }

        private static MethodInfo GetPrivateFfiMethod(string methodName)
        {
            MethodInfo method = typeof(MmdParserFfiMethods).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return method;
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

        private static byte[] BuildSceneTrackVmdBytes()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            WriteFixedAscii(writer, "Vocaloid Motion Data 0002", 30);
            WriteFixedAscii(writer, "light_shadow", 20);
            writer.Write(0u); // bone frames
            writer.Write(0u); // morph frames
            writer.Write(0u); // camera frames
            writer.Write(2u); // light frames
            WriteLightFrame(writer, 10u, 0.0f, 0.0f, 1.0f, 1.0f, 0.0f, 0.0f);
            WriteLightFrame(writer, 30u, 1.0f, 0.5f, 0.0f, 0.0f, -1.0f, 0.0f);
            writer.Write(2u); // self-shadow frames
            WriteSelfShadowFrame(writer, 10u, 1, 0.2f);
            WriteSelfShadowFrame(writer, 30u, 2, 0.4f);
            writer.Write(0u); // property frames
            return stream.ToArray();
        }

        private static void WriteLightFrame(
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

        private static void WriteSelfShadowFrame(BinaryWriter writer, uint frame, byte mode, float distance)
        {
            writer.Write(frame);
            writer.Write(mode);
            writer.Write(distance);
        }

        private static void WriteFixedAscii(BinaryWriter writer, string text, int byteLength)
        {
            byte[] bytes = new byte[byteLength];
            byte[] source = System.Text.Encoding.ASCII.GetBytes(text);
            Array.Copy(source, bytes, Math.Min(source.Length, bytes.Length));
            writer.Write(bytes);
        }
    }
}
