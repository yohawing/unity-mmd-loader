using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
            Assert.That(MmdRuntimeFfiMethods.ExpectedAbiVersion, Is.EqualTo(1));
            Assert.That(MmdNativePhysicsMethods.LibraryName, Is.EqualTo("yohawing_mmd_unity_bullet"));
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
            Assert.That(Path.Combine(pluginRoot, "yohawing_mmd_unity_bullet.dll"), Does.Exist);
            Assert.That(Path.Combine(pluginRoot, "yohawing_mmd_unity_bullet.dll.meta"), Does.Exist);
            CollectionAssert.AreEqual(
                new[] { "mmd_runtime_ffi.dll", "yohawing_mmd_unity_bullet.dll" },
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
    }
}
