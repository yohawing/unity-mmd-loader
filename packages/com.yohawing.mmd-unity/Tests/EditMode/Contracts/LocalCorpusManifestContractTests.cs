using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Yohawing.MmdUnity.Tests
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
    }
}
