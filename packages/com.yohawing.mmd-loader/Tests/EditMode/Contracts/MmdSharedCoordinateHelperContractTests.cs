#nullable enable

using System.Reflection;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    /// <summary>
    /// Pins the shared helpers that replaced per-class copies: runtime import-scale normalization,
    /// MMD float triples, and the asset source id fallback.
    /// </summary>
    [TestFixture]
    public sealed class MmdSharedCoordinateHelperContractTests
    {
        [TestCase(0.1f, 0.1f)]
        [TestCase(2.5f, 2.5f)]
        [TestCase(0.0f, 1.0f)]
        [TestCase(-0.1f, 1.0f)]
        [TestCase(float.NaN, 1.0f)]
        [TestCase(float.PositiveInfinity, 1.0f)]
        [TestCase(float.NegativeInfinity, 1.0f)]
        public void NormalizeImportScaleFallsBackToOne(float input, float expected)
        {
            Assert.That(MmdCoordinateSpace.NormalizeImportScale(input), Is.EqualTo(expected));
        }

        [Test]
        public void NormalizeImportScaleIsUsedByScaledMmdToUnityPosition()
        {
            Vector3 mmd = new Vector3(1.0f, 2.0f, 3.0f);

            Assert.That(MmdCoordinateSpace.MmdToUnityPosition(mmd, float.NaN), Is.EqualTo(new Vector3(-1.0f, 2.0f, -3.0f)));
            Assert.That(MmdCoordinateSpace.MmdToUnityPosition(mmd, 0.5f), Is.EqualTo(new Vector3(-0.5f, 1.0f, -1.5f)));
        }

        [Test]
        public void ToVector3ReadsTripleWithoutAxisFlip()
        {
            Assert.That(MmdCoordinateSpace.ToVector3(new[] { 1.0f, -2.0f, 3.0f }), Is.EqualTo(new Vector3(1.0f, -2.0f, 3.0f)));
            Assert.That(MmdCoordinateSpace.ToVector3(new[] { 1.0f, 2.0f, 3.0f, 4.0f }), Is.EqualTo(new Vector3(1.0f, 2.0f, 3.0f)));
            Assert.That(MmdCoordinateSpace.ToVector3(new[] { 1.0f, 2.0f }), Is.EqualTo(Vector3.zero));
            Assert.That(MmdCoordinateSpace.ToVector3(null), Is.EqualTo(Vector3.zero));
        }

        [TestCase("", "asset-name")]
        [TestCase("   ", "asset-name")]
        [TestCase(null, "asset-name")]
        [TestCase("models/miku.pmx", "models/miku.pmx")]
        public void PmxResolvedSourceIdFallsBackToAssetName(string? sourceId, string expected)
        {
            MmdPmxAsset asset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                asset.name = "asset-name";
                SetSourceId(asset, sourceId);

                Assert.That(asset.ResolvedSourceId, Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [TestCase("", "asset-name")]
        [TestCase("   ", "asset-name")]
        [TestCase(null, "asset-name")]
        [TestCase("motions/dance.vmd", "motions/dance.vmd")]
        public void VmdResolvedSourceIdFallsBackToAssetName(string? sourceId, string expected)
        {
            MmdVmdAsset asset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            try
            {
                asset.name = "asset-name";
                SetSourceId(asset, sourceId);

                Assert.That(asset.ResolvedSourceId, Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        private static void SetSourceId(ScriptableObject asset, string? sourceId)
        {
            FieldInfo field = asset.GetType().GetField("sourceId", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.That(field, Is.Not.Null);
            field.SetValue(asset, sourceId);
        }
    }
}
