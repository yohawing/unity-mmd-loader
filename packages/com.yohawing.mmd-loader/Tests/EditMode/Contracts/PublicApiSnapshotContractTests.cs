#nullable enable

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Mmd.Tests;

namespace Mmd.Tests.Contracts
{
    /// <summary>
    /// Pins the public/protected API surface of each shipped assembly against a
    /// committed <c>PublicApi/&lt;Assembly&gt;.api.txt</c> baseline. Assemblies are
    /// resolved by name from the editor AppDomain so this fixture does not add
    /// asmdef references. Mmd.Cinemachine is pinned by the Cinemachine EditMode
    /// assembly, which only compiles when Cinemachine 3 is installed.
    /// See <see cref="PublicApiBaselineAssert"/> for the baseline update workflow.
    /// </summary>
    [TestFixture]
    public sealed class PublicApiSnapshotContractTests
    {
        private static readonly string[] SnapshotAssemblies =
        {
            "Mmd.Runtime",
            "Mmd.Timeline",
            "Mmd.Rendering.Universal",
            "Mmd.Editor",
            "Mmd.Editor.Timeline"
        };

        private static IEnumerable<string> SnapshotAssemblyNames => SnapshotAssemblies;

        private static string BaselineRoot =>
            Path.Combine(MmdTestFixtures.PackageRoot, "Tests", "EditMode", "Contracts", "PublicApi");

        [TestCaseSource(nameof(SnapshotAssemblyNames))]
        public void PublicApiMatchesCommittedBaseline(string assemblyName)
        {
            PublicApiBaselineAssert.AssertMatchesBaseline(
                PublicApiBaselineAssert.RequireLoadedAssembly(assemblyName),
                Path.Combine(BaselineRoot, assemblyName + ".api.txt"));
        }
    }
}
