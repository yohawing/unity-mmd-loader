#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using Mmd;
using Mmd.Native;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.Tests;
using Mmd.UnityIntegration;

namespace Mmd.Tests.Contracts
{
    /// <summary>
    /// Pins the small set of public, serialized, diagnostic, assembly, and native
    /// names that a responsibility-preserving refactor must keep stable.
    /// Private helpers, file layout, and method ordering are intentionally absent.
    /// </summary>
    [TestFixture]
    public sealed class CompatibilitySurfaceContractTests
    {
        private static readonly string[] ExpectedAssemblyDefinitions =
        {
            "Mmd.Cinemachine", "Mmd.CinemachineEditModeTests", "Mmd.CinemachinePlayModeTests",
            "Mmd.EditModeTestKit", "Mmd.EditModeTests", "Mmd.Editor", "Mmd.Editor.Timeline",
            "Mmd.PlayModeTests", "Mmd.Rendering.Universal", "Mmd.Runtime", "Mmd.RuntimeVerification.PlayModeTests",
            "Mmd.Timeline", "Mmd.TimelineEditModeTests", "Mmd.UnityToonShaderAdapter.Sample",
            "Mmd.UnityToonShaderAdapter.Sample.Tests"
        };

        private static readonly IReadOnlyDictionary<string, Type> ExpectedPmxSerializedFields =
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                ["data"] = typeof(byte[]),
                ["sourceId"] = typeof(string),
                ["sourcePath"] = typeof(string),
                ["importScale"] = typeof(float),
                ["modelPreset"] = typeof(string),
                ["animationType"] = typeof(string),
                ["shaderPreset"] = typeof(string),
                ["importedAvatar"] = typeof(Avatar),
                ["humanoidAvatarReadiness"] = typeof(string),
                ["humanoidAvatarDiagnostic"] = typeof(string),
                ["humanoidBoneMappingDiagnostics"] = typeof(MmdHumanoidBoneMappingDiagnosticSummary),
                ["importSummaryStatus"] = typeof(MmdPmxImportSummaryStatus),
                ["modelName"] = typeof(string),
                ["modelEnglishName"] = typeof(string),
                ["modelComment"] = typeof(string),
                ["modelEnglishComment"] = typeof(string),
                ["vertexCount"] = typeof(int),
                ["indexCount"] = typeof(int),
                ["boneCount"] = typeof(int),
                ["morphCount"] = typeof(int),
                ["materialCount"] = typeof(int),
                ["diffuseTextureReferenceCount"] = typeof(int),
                ["sphereTextureReferenceCount"] = typeof(int),
                ["toonTextureReferenceCount"] = typeof(int),
                ["resolvedProjectTextureReferenceCount"] = typeof(int),
                ["missingProjectTextureReferenceCount"] = typeof(int),
                ["missingProjectTextureReferenceSample"] = typeof(string),
                ["transparentMaterialCount"] = typeof(int),
                ["edgeMaterialCount"] = typeof(int),
                ["ikCount"] = typeof(int),
                ["rigidbodyCount"] = typeof(int),
                ["jointCount"] = typeof(int),
                ["boundsMin"] = typeof(Vector3),
                ["boundsMax"] = typeof(Vector3),
                ["boundsSize"] = typeof(Vector3),
                ["materialSummaries"] = typeof(MmdPmxMaterialSummary[]),
                ["importedMesh"] = typeof(Mesh),
                ["importedMaterials"] = typeof(Material[]),
                ["materialRemaps"] = typeof(Material[]),
                ["materialOverrideAsset"] = typeof(MmdMaterialOverrideAsset),
                ["materialProfileAsset"] = typeof(MmdMaterialProfileAsset),
                ["materialProfileProvenance"] = typeof(MmdMaterialProfileProvenance),
                ["importedRoot"] = typeof(GameObject),
                ["hierarchyReadiness"] = typeof(MmdImportReadiness),
                ["rendererReadiness"] = typeof(MmdImportReadiness),
                ["boneBindingReadiness"] = typeof(MmdImportReadiness),
                ["hierarchyReadinessDiagnostic"] = typeof(string),
                ["rendererReadinessDiagnostic"] = typeof(string),
                ["boneBindingReadinessDiagnostic"] = typeof(string)
            };

        private static readonly IReadOnlyDictionary<string, Type> ExpectedVmdSerializedFields =
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                ["data"] = typeof(byte[]),
                ["rawSource"] = typeof(TextAsset),
                ["sourceId"] = typeof(string),
                ["sourcePath"] = typeof(string),
                ["importSummaryStatus"] = typeof(MmdVmdImportSummaryStatus),
                ["targetModelName"] = typeof(string),
                ["maxFrame"] = typeof(int),
                ["boneKeyframeCount"] = typeof(int),
                ["morphKeyframeCount"] = typeof(int),
                ["modelKeyframeCount"] = typeof(int),
                ["constraintStateCount"] = typeof(int),
                ["cameraKeyframeCount"] = typeof(int),
                ["lightKeyframeCount"] = typeof(int),
                ["selfShadowKeyframeCount"] = typeof(int),
                ["structuralDiagnostics"] = typeof(string[])
            };

        private static readonly IReadOnlyDictionary<string, Type> ExpectedControllerSerializedFields =
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                ["playOnStart"] = typeof(bool),
                ["initialFrame"] = typeof(int),
                ["frameRate"] = typeof(float),
                ["physicsMode"] = typeof(MmdPhysicsMode),
                ["livePhysicsBodyDiagnosticsSampleInterval"] = typeof(int),
                ["ikMaxIterationsCap"] = typeof(int),
                ["modelAsset"] = typeof(MmdPmxAsset),
                ["motionAsset"] = typeof(MmdVmdAsset),
                ["lastFastRuntimeReason"] = typeof(string),
                ["proxyRoot"] = typeof(Transform),
                ["humanoidRetargetEntries"] = typeof(List<MmdHumanoidRetargetBinding>),
                ["humanoidAppendEntries"] = typeof(List<MmdHumanoidAppendTransformBinding>)
            };

        private static readonly string[] ExpectedPlaybackSummaryFields =
        {
            "model", "motion", "frame", "boneCount", "morphCount", "vertexCount", "indexCount",
            "skinningCount", "materialCount", "submeshCount", "urpMaterialBindingCount", "vertexMorphCount", "ikCount"
        };

        private static readonly string[] ExpectedPlaybackSequenceSummaryFields =
        {
            "model", "motion", "frameCount", "firstFrame", "lastFrame", "boneCount", "morphCount",
            "vertexCount", "indexCount", "skinningCount", "materialCount", "submeshCount",
            "urpMaterialBindingCount", "vertexMorphCount", "ikCount"
        };

        private static readonly string[] ExpectedShaderDiagnosticsFields =
        {
            "requestedShaderName", "resolvedShaderName", "fallbackShaderName", "fallbackReason",
            "shaderFallbackUsed", "fallbackCandidates"
        };

        private static readonly IReadOnlyDictionary<string, Type> ExpectedLivePhysicsDiagnosticsFields =
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                ["frame"] = typeof(int),
                ["backendName"] = typeof(string),
                ["evaluationPath"] = typeof(string),
                ["phaseDiagnosticsPresent"] = typeof(bool),
                ["nativeStepReportPresent"] = typeof(bool),
                ["hostPoseCapturePresent"] = typeof(bool),
                ["pinnedDiagnosticsPresent"] = typeof(bool),
                ["totalMs"] = typeof(double),
                ["bridgeTotalMs"] = typeof(double),
                ["nativeHostFrameMs"] = typeof(double),
                ["physicsWorldStepRuntimeMs"] = typeof(double),
                ["nativeRigidbodyCount"] = typeof(int),
                ["nativeBoneCount"] = typeof(int),
                ["bodyDiagnosticsFrame"] = typeof(int),
                ["unsupportedWorldAnchorJointCount"] = typeof(int),
                ["comparisonSpace"] = typeof(string),
                ["importScale"] = typeof(float),
                ["modelBoneCount"] = typeof(int),
                ["appliedBoneCount"] = typeof(int),
                ["pinnedBodies"] = typeof(MmdLivePhysicsPinnedBodyDiagnostics),
                ["bodyDiagnostics"] = typeof(MmdLivePhysicsBodyDiagnostics[])
            };

        private static readonly (string method, string entryPoint)[] ExpectedNativeEntryPoints =
        {
            ("AbiVersion", "mmd_runtime_abi_version"),
            ("FeatureFlags", "mmd_runtime_feature_flags"),
            ("LastErrorMessage", "mmd_runtime_last_error_message"),
            ("ModelCreateFromPmxBytes", "mmd_runtime_model_create_from_pmx_bytes"),
            ("ModelFree", "mmd_runtime_model_free"),
            ("VmdContextCreateFromVmdBytes", "mmd_runtime_vmd_context_create_from_vmd_bytes"),
            ("VmdContextFree", "mmd_runtime_vmd_context_free"),
            ("VmdSummaryReadFromVmdBytes", "mmd_runtime_vmd_summary_read_from_vmd_bytes"),
            ("ClipCreateFromVmdContextForModel", "mmd_runtime_clip_create_from_vmd_context_for_model"),
            ("ClipFree", "mmd_runtime_clip_free"),
            ("InstanceCreateForModel", "mmd_runtime_instance_create_for_model"),
            ("InstanceFree", "mmd_runtime_instance_free"),
            ("InstanceEvaluateClipFrame", "mmd_runtime_instance_evaluate_clip_frame"),
            ("InstanceCopyWorldMatrices", "mmd_runtime_instance_copy_world_matrices"),
            ("PhysicsWorldCreate", "mmd_runtime_physics_world_create"),
            ("PhysicsWorldFree", "mmd_runtime_physics_world_free"),
            ("PhysicsWorldStepRuntime", "mmd_runtime_physics_world_step_runtime"),
            ("EvaluateHostFrame", "mmd_runtime_evaluate_host_frame"),
            ("InstanceApplyHostPoseAndEvaluateBeforePhysics", "mmd_runtime_instance_apply_host_pose_and_evaluate_before_physics"),
            ("InstanceEvaluateCurrentPoseAfterPhysics", "mmd_runtime_instance_evaluate_current_pose_after_physics")
        };

        [Test]
        public void CompatibilitySurfaceSnapshotRemainsStable()
        {
            AssertSerializedFields(typeof(MmdPmxAsset), ExpectedPmxSerializedFields);
            AssertSerializedFields(typeof(MmdVmdAsset), ExpectedVmdSerializedFields);
            AssertSerializedFields(typeof(MmdUnityPlaybackController), ExpectedControllerSerializedFields);

            AssertPropertyType(typeof(MmdPmxAsset), "ModelName", typeof(string));
            AssertPropertyType(typeof(MmdPmxAsset), "ImportScale", typeof(float));
            AssertPropertyType(typeof(MmdPmxAsset), "ImportedRoot", typeof(GameObject));
            AssertPropertyType(typeof(MmdPmxAsset), "ImportedMaterials", typeof(Material[]));
            AssertPropertyType(typeof(MmdPmxAsset), "MaterialProfileAsset", typeof(MmdMaterialProfileAsset));
            AssertPropertyType(typeof(MmdPmxAsset), "MaterialProfileProvenance", typeof(MmdMaterialProfileProvenance));
            AssertPropertyType(typeof(MmdVmdAsset), "MaxFrame", typeof(int));
            AssertPropertyType(typeof(MmdVmdAsset), "ImportSummaryStatus", typeof(MmdVmdImportSummaryStatus));
            AssertPropertyType(typeof(MmdUnityPlaybackController), "HasModelSource", typeof(bool));
            AssertPropertyType(typeof(MmdUnityPlaybackController), "ModelSourceId", typeof(string));
            AssertPropertyType(typeof(MmdUnityPlaybackController), "HumanoidPhysicsBindingFailureReason", typeof(string));

            AssertMethodSignature(typeof(MmdPmxAsset), "Initialize", typeof(void),
                typeof(byte[]), typeof(string), typeof(string), typeof(float), typeof(string), typeof(string),
                typeof(string), typeof(string), typeof(Nullable<MmdPmxParseSummary>), typeof(Mesh), typeof(Material[]),
                typeof(Material[]), typeof(GameObject), typeof(MmdImportReadiness), typeof(MmdImportReadiness),
                typeof(MmdImportReadiness), typeof(string), typeof(string), typeof(string), typeof(string),
                typeof(MmdMaterialOverrideAsset));
            AssertMethodSignature(typeof(MmdPmxAsset), "GetBytesCopy", typeof(byte[]));
            AssertMethodSignature(typeof(MmdPmxAsset), "LoadModel", typeof(MmdModelDefinition), typeof(IMmdParser));
            AssertMethodSignature(typeof(MmdVmdAsset), "Initialize", typeof(void),
                typeof(byte[]), typeof(string), typeof(string), typeof(Nullable<MmdVmdParseSummary>), typeof(IReadOnlyList<string>));
            AssertMethodSignature(typeof(MmdVmdAsset), "GetBytesCopy", typeof(byte[]));
            AssertMethodSignature(typeof(MmdVmdAsset), "LoadMotion", typeof(MmdMotionDefinition), typeof(IMmdParser));
            AssertMethodSignature(typeof(MmdVmdAsset), "CreateNativeClipMotionHeader", typeof(MmdMotionDefinition));
            AssertMethodSignature(typeof(MmdUnityPlaybackController), "Configure", typeof(void),
                typeof(MmdUnityPlaybackBinding), typeof(float), typeof(bool));
            AssertMethodSignature(typeof(MmdUnityPlaybackController), "ConfigureFromPlaybackSource", typeof(void),
                typeof(MmdPmxAsset), typeof(MmdVmdAsset), typeof(MmdPlaybackConfig));
            AssertMethodSignature(typeof(MmdUnityPlaybackController), "ApplyFrame", typeof(MmdPlaybackSnapshot), typeof(int));
            AssertMethodSignature(typeof(MmdUnityPlaybackController), "ApplyTime", typeof(MmdPlaybackSnapshot), typeof(float), typeof(float));
            AssertMethodSignature(typeof(MmdUnityPlaybackController), "Tick", typeof(void), typeof(float));
            AssertMethodSignature(typeof(MmdUnityPlaybackController), "TryEnableFastRuntime", typeof(bool),
                typeof(byte[]), typeof(byte[]), typeof(string).MakeByRefType());

            AssertExactPublicFields(typeof(MmdPlaybackSnapshotSummary), ExpectedPlaybackSummaryFields);
            AssertExactPublicFields(typeof(MmdPlaybackSnapshotSequenceSummary), ExpectedPlaybackSequenceSummaryFields);
            AssertExactPublicFields(typeof(MmdShaderBindingDiagnostics), ExpectedShaderDiagnosticsFields);
            AssertRequiredPublicFields(typeof(MmdLivePhysicsFrameDiagnostics), ExpectedLivePhysicsDiagnosticsFields);

            string[] assemblyDefinitions = ReadAssemblyDefinitionNames();
            Assert.That(assemblyDefinitions, Is.EqualTo(ExpectedAssemblyDefinitions),
                "Assembly definition identity is a package compatibility boundary.");

            foreach ((string methodName, string entryPoint) in ExpectedNativeEntryPoints)
            {
                MethodInfo method = typeof(MmdRuntimeFfiMethods).GetMethod(
                    methodName, BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new AssertionException("Missing managed native declaration: " + methodName);
                DllImportAttribute import = method.GetCustomAttribute<DllImportAttribute>()
                    ?? throw new AssertionException("Missing DllImport attribute: " + methodName);
                Assert.That(import.EntryPoint, Is.EqualTo(entryPoint), methodName);
                Assert.That(import.CallingConvention, Is.EqualTo(CallingConvention.Cdecl), methodName);
                Assert.That(import.Value, Is.EqualTo(MmdRuntimeFfiMethods.LibraryName), methodName);
            }

            TestContext.WriteLine(BuildHumanReadableSnapshot(assemblyDefinitions));
        }

        private static void AssertSerializedFields(Type type, IReadOnlyDictionary<string, Type> expectedTypes)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(field => field.GetCustomAttribute<SerializeField>() != null)
                .ToArray();
            string[] actual = fields
                .Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            string[] sortedExpected = expectedTypes.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Assert.That(actual, Is.EqualTo(sortedExpected), type.FullName + " serialized fields");
            foreach (FieldInfo field in fields)
            {
                Assert.That(expectedTypes.TryGetValue(field.Name, out Type? expectedType), Is.True,
                    type.FullName + " serialized field type is not pinned: " + field.Name);
                Assert.That(field.FieldType, Is.EqualTo(expectedType), type.FullName + "." + field.Name + " type");
            }
        }

        private static void AssertPropertyType(Type type, string name, Type expectedType)
        {
            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public)
                ?? throw new AssertionException(type.FullName + " missing public property " + name);
            Assert.That(property.PropertyType, Is.EqualTo(expectedType), type.FullName + "." + name + " type");
        }

        private static void AssertMethodSignature(Type type, string name, Type expectedReturnType, params Type[] expectedParameterTypes)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public)
                .Where(method => method.Name == name)
                .ToArray();
            MethodInfo? match = methods.SingleOrDefault(method =>
                method.ReturnType == expectedReturnType &&
                method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(expectedParameterTypes));
            string expectedSignature = type.FullName + " signature " + name + "(" +
                string.Join(",", expectedParameterTypes.Select(parameterType => parameterType.Name)) + ")";
            string actualSignatures = string.Join(" | ", methods.Select(method => method.ToString()));
            Assert.That(match, Is.Not.Null, expectedSignature + "; actual=" + actualSignatures);
        }

        private static void AssertExactPublicFields(Type type, IReadOnlyCollection<string> expected)
        {
            string[] actual = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            string[] sortedExpected = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Assert.That(actual, Is.EqualTo(sortedExpected), type.FullName + " public fields");
        }

        private static void AssertRequiredPublicFields(Type type, IReadOnlyDictionary<string, Type> expected)
        {
            Dictionary<string, FieldInfo> actual = type
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .ToDictionary(field => field.Name, StringComparer.Ordinal);
            foreach ((string name, Type expectedType) in expected)
            {
                Assert.That(actual.TryGetValue(name, out FieldInfo? field), Is.True,
                    type.FullName + " missing public field " + name);
                Assert.That(field!.FieldType, Is.EqualTo(expectedType), type.FullName + "." + name + " type");
            }
        }

        private static string[] ReadAssemblyDefinitionNames()
        {
            string packageRoot = MmdTestFixtures.PackageRoot;
            return Directory.GetFiles(packageRoot, "*.asmdef", SearchOption.AllDirectories)
                .Select(path => JsonUtility.FromJson<AssemblyDefinitionIdentity>(File.ReadAllText(path))?.name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        private static string BuildHumanReadableSnapshot(IReadOnlyCollection<string> assemblyDefinitions)
        {
            return string.Join(Environment.NewLine, new[]
            {
                "compatibility-surface-v1",
                "asmdefs=" + string.Join(",", assemblyDefinitions),
                "pmx.serialized=" + string.Join(",", ExpectedPmxSerializedFields.Keys.OrderBy(name => name, StringComparer.Ordinal)),
                "vmd.serialized=" + string.Join(",", ExpectedVmdSerializedFields.Keys.OrderBy(name => name, StringComparer.Ordinal)),
                "controller.serialized=" + string.Join(",", ExpectedControllerSerializedFields.Keys.OrderBy(name => name, StringComparer.Ordinal)),
                "native.abi=" + MmdRuntimeFfiMethods.ExpectedAbiVersion,
                "native.entrypoints=" + string.Join(",", ExpectedNativeEntryPoints.Select(pair => pair.entryPoint))
            });
        }

        [Serializable]
        private sealed class AssemblyDefinitionIdentity
        {
            public string name = string.Empty;
        }
    }
}
