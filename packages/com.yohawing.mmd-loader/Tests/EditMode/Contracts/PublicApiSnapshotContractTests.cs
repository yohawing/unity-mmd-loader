#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    /// The tier ledger <c>PublicApi/public-api-tiers.tsv</c> must list every public
    /// type of those baselines exactly once with a known tier.
    /// </summary>
    [TestFixture]
    public sealed class PublicApiSnapshotContractTests
    {
        private const string CinemachineAssembly = "Mmd.Cinemachine";
        private const string TypeLineMarker = " :: (type) ";

        private static readonly string[] SnapshotAssemblies =
        {
            "Mmd.Runtime",
            "Mmd.Timeline",
            "Mmd.Rendering.Universal",
            "Mmd.Editor",
            "Mmd.Editor.Timeline"
        };

        private static readonly string[] LedgerTiers =
        {
            "stable",
            "advanced",
            "unity-entry",
            "obsolete",
            "obsolete-exposed"
        };

        private static IEnumerable<string> SnapshotAssemblyNames => SnapshotAssemblies;

        private static string BaselineRoot =>
            Path.Combine(MmdTestFixtures.PackageRoot, "Tests", "EditMode", "Contracts", "PublicApi");

        private static string CinemachineBaselinePath =>
            Path.Combine(MmdTestFixtures.PackageRoot, "Tests", "EditMode", "Cinemachine", "PublicApi", CinemachineAssembly + ".api.txt");

        private static string TierLedgerPath => Path.Combine(BaselineRoot, "public-api-tiers.tsv");

        [TestCaseSource(nameof(SnapshotAssemblyNames))]
        public void PublicApiMatchesCommittedBaseline(string assemblyName)
        {
            PublicApiBaselineAssert.AssertMatchesBaseline(
                PublicApiBaselineAssert.RequireLoadedAssembly(assemblyName),
                Path.Combine(BaselineRoot, assemblyName + ".api.txt"));
        }

        /// <summary>
        /// InternalsVisibleTo grants widen the effective API as much as public
        /// members do, so each grant must be listed here deliberately.
        /// </summary>
        [Test]
        public void InternalsVisibleToGrantsAreExplicit()
        {
            var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Mmd.Runtime"] = new[]
                {
                    "Mmd.DevTools.EditModeTests", "Mmd.DevTools.Editor", "Mmd.EditModeTests", "Mmd.Editor", "Mmd.Editor.Timeline", "Mmd.PlayModeTests",
                    "Mmd.Rendering.Universal", "Mmd.Timeline", "Mmd.TimelineEditModeTests"
                },
                ["Mmd.Timeline"] = new[] { "Mmd.TimelineEditModeTests" },
                ["Mmd.Rendering.Universal"] = Array.Empty<string>(),
                ["Mmd.Editor"] = new[] { "Mmd.DevTools.Editor", "Mmd.EditModeTests", "Mmd.TimelineEditModeTests" },
                ["Mmd.Editor.Timeline"] = new[] { "Mmd.Editor", "Mmd.TimelineEditModeTests" }
            };

            Assert.That(expected.Keys.OrderBy(name => name, StringComparer.Ordinal),
                Is.EqualTo(SnapshotAssemblies.OrderBy(name => name, StringComparer.Ordinal)));
            foreach (KeyValuePair<string, string[]> entry in expected)
            {
                string[] actual = PublicApiBaselineAssert.RequireLoadedAssembly(entry.Key)
                    .GetCustomAttributes(typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute), false)
                    .Cast<System.Runtime.CompilerServices.InternalsVisibleToAttribute>()
                    .Select(attribute => attribute.AssemblyName)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                Assert.That(actual, Is.EqualTo(entry.Value.OrderBy(name => name, StringComparer.Ordinal).ToArray()),
                    "InternalsVisibleTo grants of " + entry.Key);
            }
        }

        [Test]
        public void TierLedgerListsEveryPublicTypeExactlyOnce()
        {
            // The ledger is compared with the committed baseline files, so the optional
            // Mmd.Cinemachine baseline is always included even when Cinemachine 3 is absent.
            var baselines = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string assemblyName in SnapshotAssemblies)
            {
                baselines.Add(assemblyName, Path.Combine(BaselineRoot, assemblyName + ".api.txt"));
            }

            baselines.Add(CinemachineAssembly, CinemachineBaselinePath);

            var expected = new SortedSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> baseline in baselines)
            {
                foreach (string line in ReadLines(baseline.Value))
                {
                    int marker = line.IndexOf(TypeLineMarker, StringComparison.Ordinal);
                    if (marker > 0)
                    {
                        expected.Add(baseline.Key + "\t" + line.Substring(0, marker));
                    }
                }
            }

            Assert.That(expected, Is.Not.Empty, "No (type) lines were read from the public API baselines.");
            Assert.That(File.Exists(TierLedgerPath), Is.True, "Tier ledger is missing: " + TierLedgerPath);

            var errors = new List<string>();
            var actual = new SortedSet<string>(StringComparer.Ordinal);
            bool headerSeen = false;
            foreach (string line in ReadLines(TierLedgerPath))
            {
                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!headerSeen)
                {
                    headerSeen = true;
                    if (!string.Equals(line, "assembly\ttype\ttier\treason", StringComparison.Ordinal))
                    {
                        errors.Add("unexpected header: " + line);
                    }

                    continue;
                }

                string[] columns = line.Split('\t');
                if (columns.Length != 4 || columns.Any(column => column.Trim().Length == 0))
                {
                    errors.Add("row must have 4 non-empty columns: " + line);
                    continue;
                }

                string assemblyName = columns[0];
                if (!LedgerTiers.Contains(columns[2], StringComparer.Ordinal))
                {
                    errors.Add("unknown tier '" + columns[2] + "': " + line);
                }

                if (!baselines.ContainsKey(assemblyName))
                {
                    errors.Add("unknown assembly '" + assemblyName + "': " + line);
                    continue;
                }

                if (!actual.Add(assemblyName + "\t" + columns[1]))
                {
                    errors.Add("duplicate row: " + line);
                }
            }

            foreach (string missing in expected.Except(actual, StringComparer.Ordinal))
            {
                errors.Add("public type missing from ledger: " + missing);
            }

            foreach (string extra in actual.Except(expected, StringComparer.Ordinal))
            {
                errors.Add("ledger row is not a public type: " + extra);
            }

            if (errors.Count > 0)
            {
                var message = new StringBuilder();
                message.Append("Tier ledger ").Append(TierLedgerPath).Append(" does not match the public API baselines:\n");
                foreach (string error in errors)
                {
                    message.Append("  ").Append(error).Append('\n');
                }

                Assert.Fail(message.ToString());
            }
        }

        private static IEnumerable<string> ReadLines(string path)
        {
            return File.ReadAllText(path)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Where(line => line.Length > 0);
        }
    }
}
