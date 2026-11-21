#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Mmd.Tests;

namespace Mmd.Tests.Contracts
{
    /// <summary>
    /// Pins what Unity stores for the shipped assemblies: serialized field
    /// names/types (with [SerializeReference] and [FormerlySerializedAs]) of
    /// UnityEngine.Object-derived and [Serializable] types, public or internal,
    /// and the MonoScript GUID of every script under Runtime/ and Editor/.
    /// Making types internal, moving namespaces or splitting files must leave
    /// both baselines unchanged unless the change is an intended migration.
    /// Mmd.Cinemachine is pinned by the Cinemachine EditMode assembly.
    /// See <see cref="PublicApiBaselineAssert"/> for the baseline update workflow.
    /// </summary>
    [TestFixture]
    public sealed class SerializedSurfaceSnapshotContractTests
    {
        private static readonly string[] SnapshotAssemblies =
        {
            "Mmd.Runtime",
            "Mmd.Timeline",
            "Mmd.Rendering.Universal",
            "Mmd.Editor",
            "Mmd.Editor.Timeline"
        };

        private static string BaselineRoot =>
            Path.Combine(MmdTestFixtures.PackageRoot, "Tests", "EditMode", "Contracts", "PublicApi");

        [Test]
        public void SerializedFieldsMatchCommittedBaseline()
        {
            List<string> actual = SerializedFieldSnapshot.Build(LoadAssemblies());
            Assert.That(actual, Is.Not.Empty, "No serialized types were found; the snapshot would be meaningless.");
            PublicApiBaselineAssert.AssertLinesMatchBaseline(
                "Serialized fields",
                actual,
                Path.Combine(BaselineRoot, "serialized-fields.txt"),
                "serialized-fields.txt");
        }

        [Test]
        public void MonoScriptGuidsMatchCommittedBaseline()
        {
            List<string> actual = MonoScriptGuidSnapshot.Build(SnapshotAssemblies);
            Assert.That(actual, Is.Not.Empty, "No MonoScript resolved to a class under " + MonoScriptGuidSnapshot.PackageAssetPath);
            PublicApiBaselineAssert.WriteCandidateIfRequested(
                "monoscript-unmapped-types.txt",
                MonoScriptGuidSnapshot.FindUnityObjectTypesWithoutScript(LoadAssemblies()));
            PublicApiBaselineAssert.AssertLinesMatchBaseline(
                "MonoScript GUIDs",
                actual,
                Path.Combine(BaselineRoot, "monoscript-guids.txt"),
                "monoscript-guids.txt");
        }

        private static List<Assembly> LoadAssemblies()
        {
            return SnapshotAssemblies.Select(PublicApiBaselineAssert.RequireLoadedAssembly).ToList();
        }
    }
}
