#nullable enable

using System;
using NUnit.Framework;
using UnityEngine;
using Mmd.Parser;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmdVmdAssetNativeClipContractTests
    {
        [Test]
        public void FailedImportSummaryPreservesBytesButRejectsNativeClipHeader()
        {
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            byte[] sourceBytes = { 0x56, 0x4D, 0x44, 0x00 };

            try
            {
                asset.Initialize(
                    sourceBytes,
                    "invalid.vmd",
                    "Assets/invalid.vmd",
                    new MmdVmdParseSummary("invalid", 0, 0, 0, 0, 0),
                    new[] { "Failed to parse VMD during import: truncated record" });

                Assert.That(asset.ByteLength, Is.EqualTo(sourceBytes.Length));
                Assert.That(asset.GetBytesCopy(), Is.EqualTo(sourceBytes));
                Assert.That(asset.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.Failed));

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => asset.CreateNativeClipMotionHeader())!;

                Assert.That(exception.Message, Does.Contain("Failed to parse VMD during import"));
                Assert.That(exception.Message, Does.Contain("truncated record"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void UnparsedAssetBuildsNativeClipHeaderFromSummaryReader()
        {
            byte[] sourceBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();

            try
            {
                asset.Initialize(sourceBytes, "test_1bone_cube_motion.vmd", "Assets/test_1bone_cube_motion.vmd");

                Assert.That(asset.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.NotParsed));
                MmdMotionDefinition header = asset.CreateNativeClipMotionHeader();
                MmdMotionDefinition parsed = new NativeMmdParser().LoadMotion(sourceBytes);

                Assert.That(header.maxFrame, Is.EqualTo(parsed.maxFrame));
                Assert.That(header.boneKeyframes, Is.Empty);
                Assert.That(header.morphKeyframes, Is.Empty);
                Assert.That(header.sourceBytes, Is.EqualTo(sourceBytes));
                Assert.That(header.sourceBytes, Is.Not.SameAs(sourceBytes));
                Assert.That(asset.GetBytesCopy(), Is.Not.SameAs(header.sourceBytes));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void StaticNativeClipHeaderClonesLegacySourceBytes()
        {
            byte[] sourceBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            MmdMotionDefinition header = MmdVmdAsset.CreateNativeClipMotionHeader(
                sourceBytes,
                new MmdVmdParseSummary("test", 49, 6, 0, 0, 0));

            Assert.That(header.sourceBytes, Is.Not.SameAs(sourceBytes));
            Assert.That(header.sourceBytes, Is.EqualTo(sourceBytes));
        }

        [Test]
        public void PassedSummaryWithStructuralDiagnosticsRejectsNativeClipHeader()
        {
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                asset.Initialize(
                    MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd"),
                    "diagnostic.vmd",
                    "Assets/diagnostic.vmd",
                    new MmdVmdParseSummary("model", 1, 1, 0, 0, 0),
                    new[] { "structural: invalid interpolation" });

                Assert.That(asset.ImportSummaryStatus, Is.EqualTo(MmdVmdImportSummaryStatus.Passed));
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => asset.CreateNativeClipMotionHeader())!;

                Assert.That(exception.Message, Does.Contain("invalid interpolation"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void NativeVmdContextIsReusedForTheSameAssetSourceIdentity()
        {
            byte[] sourceBytes = MmdTestFixtures.ReadFixtureAssetBytes("test_1bone_cube_motion.vmd");
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();

            try
            {
                asset.Initialize(
                    sourceBytes,
                    "test_1bone_cube_motion.vmd",
                    "Assets/test_1bone_cube_motion.vmd",
                    new MmdVmdParseSummary("test", 49, 6, 0, 0, 0));

                if (!asset.TryGetOrCreateNativeVmdContext(out var first, out string firstReason))
                {
                    Assert.Ignore("Shared VMD context is unavailable: " + firstReason);
                }

                Assert.That(first, Is.Not.Null);
                Assert.That(
                    asset.TryGetOrCreateNativeVmdContext(out var second, out string secondReason),
                    Is.True,
                    secondReason);
                Assert.That(second, Is.SameAs(first));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
