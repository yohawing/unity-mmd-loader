#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class LocalCorpusManifestContractTests
    {
        [Test]
        public void LocalThreeMmdLoaderCorpusManifestImportsPmxAndPmdWhenPresent()
        {
            if (!File.Exists(MmdTestFixtures.LocalCorpusManifestPath))
            {
                Assert.Ignore("Local corpus manifest not found: " + MmdTestFixtures.LocalCorpusManifestPath);
            }

            List<ModelFixtureEntry> fixtures = MmdTestFixtures.LoadLocalCorpusModelFixtures().ToList();
            Assert.That(fixtures.Select(fixture => fixture.format), Does.Contain("pmx"));
            Assert.That(fixtures.Select(fixture => fixture.format), Does.Contain("pmd"));
        }

        // scripts/run-local-asset-fixture-gate.ps1 picks the corpus root as basePath, then root,
        // then "."; a relative root is resolved against the manifest's directory. The C# loader
        // must pick the same root, otherwise the gate validates one set of files and the
        // contract reads another (the Unity working directory is the project, not the manifest).
        [TestCase("basePath", "corpus", false)]
        [TestCase("root", "corpus", false)]
        [TestCase("both", "corpus", false)]
        [TestCase("none", "", false)]
        [TestCase("basePath", "corpus", true)]
        public void LocalCorpusManifestResolvesRootLikeTheLocalAssetGate(string rootForm, string rootValue, bool absoluteRoot)
        {
            using var temp = new MmdTestTempScope();
            string manifestDirectory = Path.Combine(temp.Path, "manifest");
            string corpusDirectory = Path.GetFullPath(Path.Combine(manifestDirectory, rootValue));
            string pmxPath = Path.Combine(corpusDirectory, "pmx", "model.pmx");
            string pmdPath = Path.Combine(corpusDirectory, "pmd", "model.pmd");
            Directory.CreateDirectory(Path.GetDirectoryName(pmxPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(pmdPath)!);
            File.WriteAllBytes(pmxPath, new byte[] { 0 });
            File.WriteAllBytes(pmdPath, new byte[] { 0 });

            string declaredRoot = absoluteRoot ? corpusDirectory.Replace(Path.DirectorySeparatorChar, '/') : rootValue;
            string rootFields = rootForm switch
            {
                "basePath" => "\"basePath\": \"" + declaredRoot + "\",",
                "root" => "\"root\": \"" + declaredRoot + "\",",
                // basePath wins over root, as in the gate.
                "both" => "\"basePath\": \"" + declaredRoot + "\", \"root\": \"elsewhere\",",
                _ => string.Empty
            };
            string manifestPath = Path.Combine(manifestDirectory, "fixtures.local.json");
            File.WriteAllText(
                manifestPath,
                "{ \"schemaVersion\": 1, " + rootFields +
                " \"paths\": { \"releaseSmoke\": { \"byExtension\": {" +
                " \"pmx\": { \"pmx001\": \"pmx/model.pmx\" }," +
                " \"pmd\": { \"pmd001\": \"pmd/model.pmd\" } } } } }");

            Dictionary<string, string> resolved = MmdTestFixtures.LoadLocalCorpusModelFixtures(manifestPath)
                .ToDictionary(fixture => fixture.format, fixture => fixture.ModelPath);

            Assert.That(resolved.Keys, Is.EquivalentTo(new[] { "pmx", "pmd" }));
            Assert.That(resolved["pmx"], Is.EqualTo(pmxPath));
            Assert.That(resolved["pmd"], Is.EqualTo(pmdPath));
        }
    }
}
