#nullable enable

using System;
using System.Collections.Generic;
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
        public void ParserFfiPinsParseOncePmxGeometryHandleEntrypoints()
        {
            Assert.That(MmdParserFfiMethods.PmxGeometryCreateEntryPoint, Is.EqualTo("mmd_runtime_pmx_geometry_create"));
            Assert.That(MmdParserFfiMethods.PmxGeometryFreeEntryPoint, Is.EqualTo("mmd_runtime_pmx_geometry_free"));
            Assert.That(MmdParserFfiMethods.PmxGeometryPositionsBufferEntryPoint, Is.EqualTo("mmd_runtime_pmx_geometry_positions_buffer"));
            Assert.That(MmdParserFfiMethods.PmxGeometryNormalsBufferEntryPoint, Is.EqualTo("mmd_runtime_pmx_geometry_normals_buffer"));
            Assert.That(MmdParserFfiMethods.PmxGeometryUvsBufferEntryPoint, Is.EqualTo("mmd_runtime_pmx_geometry_uvs_buffer"));
            Assert.That(MmdParserFfiMethods.PmxGeometryEdgeScaleBufferEntryPoint, Is.EqualTo("mmd_runtime_pmx_geometry_edge_scale_buffer"));
            Assert.That(MmdParserFfiMethods.PmxGeometryIndicesBufferEntryPoint, Is.EqualTo("mmd_runtime_pmx_geometry_indices_buffer"));
            Assert.That(MmdParserFfiMethods.PmxGeometrySkinIndicesBufferEntryPoint, Is.EqualTo("mmd_runtime_pmx_geometry_skin_indices_buffer"));
            Assert.That(MmdParserFfiMethods.PmxGeometrySkinWeightsBufferEntryPoint, Is.EqualTo("mmd_runtime_pmx_geometry_skin_weights_buffer"));
            Assert.That(MmdParserFfiMethods.PmxGeometrySdefEnabledBufferEntryPoint, Is.EqualTo("mmd_runtime_pmx_geometry_sdef_enabled_buffer"));
            Assert.That(MmdParserFfiMethods.PmxGeometrySdefCBufferEntryPoint, Is.EqualTo("mmd_runtime_pmx_geometry_sdef_c_buffer"));
            Assert.That(MmdParserFfiMethods.PmxGeometrySdefR0BufferEntryPoint, Is.EqualTo("mmd_runtime_pmx_geometry_sdef_r0_buffer"));
            Assert.That(MmdParserFfiMethods.PmxGeometrySdefR1BufferEntryPoint, Is.EqualTo("mmd_runtime_pmx_geometry_sdef_r1_buffer"));
            Assert.That(MmdParserFfiMethods.PmxGeometrySkinningModesJsonEntryPoint, Is.EqualTo("mmd_runtime_pmx_geometry_skinning_modes_json"));

            AssertPrivateFfiSignature("PmxGeometryCreate", typeof(IntPtr), typeof(byte[]), typeof(IntPtr));
            AssertPrivateFfiSignature("PmxGeometryFree", typeof(void), typeof(IntPtr));
            foreach (string methodName in new[]
                     {
                         "PmxGeometryPositionsBuffer",
                         "PmxGeometryNormalsBuffer",
                         "PmxGeometryUvsBuffer",
                         "PmxGeometryEdgeScaleBuffer",
                         "PmxGeometryIndicesBuffer",
                         "PmxGeometrySkinIndicesBuffer",
                         "PmxGeometrySkinWeightsBuffer",
                         "PmxGeometrySdefEnabledBuffer",
                         "PmxGeometrySdefCBuffer",
                         "PmxGeometrySdefR0Buffer",
                         "PmxGeometrySdefR1Buffer",
                         "PmxGeometrySkinningModesJsonBuffer"
                     })
            {
                AssertPrivateFfiSignatureReturnName(methodName, "ByteBuffer", typeof(IntPtr));
            }
        }

        [Test]
        public void ParserFfiParseOncePmxGeometryHandleMatchesLegacyBuffers()
        {
            byte[] pmxBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube.pmx");
            IntPtr geometry = MmdParserFfiMethods.CreatePmxGeometry(pmxBytes);
            Assert.That(geometry, Is.Not.EqualTo(IntPtr.Zero), "geometry handle");
            try
            {
                CollectionAssert.AreEqual(MmdParserFfiMethods.ParsePmxPositions(pmxBytes), MmdParserFfiMethods.ParsePmxGeometryPositions(geometry));
                CollectionAssert.AreEqual(MmdParserFfiMethods.ParsePmxNormals(pmxBytes), MmdParserFfiMethods.ParsePmxGeometryNormals(geometry));
                CollectionAssert.AreEqual(MmdParserFfiMethods.ParsePmxUvs(pmxBytes), MmdParserFfiMethods.ParsePmxGeometryUvs(geometry));
                CollectionAssert.AreEqual(MmdParserFfiMethods.ParsePmxEdgeScale(pmxBytes), MmdParserFfiMethods.ParsePmxGeometryEdgeScale(geometry));
                CollectionAssert.AreEqual(MmdParserFfiMethods.ParsePmxIndices(pmxBytes), MmdParserFfiMethods.ParsePmxGeometryIndices(geometry));
                CollectionAssert.AreEqual(MmdParserFfiMethods.ParsePmxSkinIndices(pmxBytes), MmdParserFfiMethods.ParsePmxGeometrySkinIndices(geometry));
                CollectionAssert.AreEqual(MmdParserFfiMethods.ParsePmxSkinWeights(pmxBytes), MmdParserFfiMethods.ParsePmxGeometrySkinWeights(geometry));
                CollectionAssert.AreEqual(MmdParserFfiMethods.ParsePmxSdefEnabled(pmxBytes), MmdParserFfiMethods.ParsePmxGeometrySdefEnabled(geometry));
                CollectionAssert.AreEqual(MmdParserFfiMethods.ParsePmxSdefC(pmxBytes), MmdParserFfiMethods.ParsePmxGeometrySdefC(geometry));
                CollectionAssert.AreEqual(MmdParserFfiMethods.ParsePmxSdefR0(pmxBytes), MmdParserFfiMethods.ParsePmxGeometrySdefR0(geometry));
                CollectionAssert.AreEqual(MmdParserFfiMethods.ParsePmxSdefR1(pmxBytes), MmdParserFfiMethods.ParsePmxGeometrySdefR1(geometry));
                Assert.That(MmdParserFfiMethods.ParsePmxGeometrySkinningModesJson(geometry),
                    Is.EqualTo(MmdParserFfiMethods.ParsePmxSkinningModesJson(pmxBytes)));
            }
            finally
            {
                MmdParserFfiMethods.FreePmxGeometry(geometry);
            }
        }

        [Test]
        public void NativeParserUsesOneGeometryHandleAndFreesItAfterAllAccessors()
        {
            var reader = new CountingPmxGeometryReader();

            var geometry = NativeMmdParser.CreatePmxGeometryFromNativeHandle(new byte[] { 1 }, reader);

            Assert.That(reader.CreateCount, Is.EqualTo(1));
            Assert.That(reader.FreeCount, Is.EqualTo(1));
            foreach (string accessor in CountingPmxGeometryReader.AccessorNames)
            {
                Assert.That(reader.AccessorCallCount(accessor), Is.EqualTo(1), accessor);
            }

            CollectionAssert.AreEqual(new[] { 1.0f, 2.0f, 3.0f }, geometry.positions);
            CollectionAssert.AreEqual(new uint[] { 0, 0, 0 }, geometry.indices);
            CollectionAssert.AreEqual(new[] { "bdef1" }, geometry.skinningModes);
        }

        [Test]
        public void NativeParserFreesGeometryHandleWhenAnAccessorFails()
        {
            var reader = new CountingPmxGeometryReader { ThrowOnAccessor = "normals" };

            Assert.Throws<InvalidOperationException>(() =>
                NativeMmdParser.CreatePmxGeometryFromNativeHandle(new byte[] { 1 }, reader));

            Assert.That(reader.CreateCount, Is.EqualTo(1));
            Assert.That(reader.AccessorCallCount("positions"), Is.EqualTo(1));
            Assert.That(reader.AccessorCallCount("normals"), Is.EqualTo(1));
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
            Assert.That(MmdRuntimeFfiMethods.ExpectedAbiVersion, Is.EqualTo(3));
            Assert.That(MmdRuntimeFfiMethods.ValidateAbiVersion(), Is.EqualTo(3));
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

        private sealed class CountingPmxGeometryReader : IPmxGeometryReader
        {
            internal static readonly string[] AccessorNames =
            {
                "positions", "normals", "uvs", "edgeScale", "indices", "skinIndices", "skinWeights",
                "sdefEnabled", "sdefC", "sdefR0", "sdefR1", "skinningModesJson"
            };

            private readonly Dictionary<string, int> accessorCalls = new Dictionary<string, int>();

            internal int CreateCount { get; private set; }
            internal int FreeCount { get; private set; }
            internal string ThrowOnAccessor { get; set; } = string.Empty;
            internal IntPtr CreatedHandle { get; set; } = new IntPtr(1);

            public IntPtr Create(byte[] data)
            {
                CreateCount++;
                return CreatedHandle;
            }

            public void Free(IntPtr geometry)
            {
                FreeCount++;
            }

            public float[] Positions(IntPtr geometry) => Float("positions", 1.0f, 2.0f, 3.0f);
            public float[] Normals(IntPtr geometry) => Float("normals", 0.0f, 1.0f, 0.0f);
            public float[] Uvs(IntPtr geometry) => Float("uvs", 0.0f, 1.0f);
            public float[] EdgeScale(IntPtr geometry) => Float("edgeScale", 1.0f);
            public uint[] Indices(IntPtr geometry) => Uint("indices", 0, 0, 0);
            public uint[] SkinIndices(IntPtr geometry) => Uint("skinIndices", 0, 0, 0, 0);
            public float[] SkinWeights(IntPtr geometry) => Float("skinWeights", 1.0f, 0.0f, 0.0f, 0.0f);
            public bool[] SdefEnabled(IntPtr geometry) => Bool("sdefEnabled", false);
            public float[] SdefC(IntPtr geometry) => Float("sdefC", 0.0f, 0.0f, 0.0f);
            public float[] SdefR0(IntPtr geometry) => Float("sdefR0", 0.0f, 0.0f, 0.0f);
            public float[] SdefR1(IntPtr geometry) => Float("sdefR1", 0.0f, 0.0f, 0.0f);
            public string SkinningModesJson(IntPtr geometry)
            {
                Record("skinningModesJson");
                return "{\"skinningModes\":[\"bdef1\"]}";
            }

            internal int AccessorCallCount(string name)
                => accessorCalls.TryGetValue(name, out int count) ? count : 0;

            private float[] Float(string name, params float[] values)
            {
                Record(name);
                return values;
            }

            private uint[] Uint(string name, params uint[] values)
            {
                Record(name);
                return values;
            }

            private bool[] Bool(string name, params bool[] values)
            {
                Record(name);
                return values;
            }

            private void Record(string name)
            {
                accessorCalls[name] = AccessorCallCount(name) + 1;
                if (string.Equals(name, ThrowOnAccessor, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("simulated accessor failure");
                }
            }
        }

    }
}
