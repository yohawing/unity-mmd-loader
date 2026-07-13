#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Mmd.Editor;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed partial class MmdAssetImporterTests
    {
        [TestCase(nameof(MmdPmxImportStage.AssetCacheCreated))]
        [TestCase(nameof(MmdPmxImportStage.ProjectTexturesBound))]
        [TestCase(nameof(MmdPmxImportStage.ImportedAssetCreated))]
        [TestCase(nameof(MmdPmxImportStage.GenericAvatarCreated))]
        [TestCase(nameof(MmdPmxImportStage.HierarchyConfigured))]
        [TestCase(nameof(MmdPmxImportStage.BeforeSubAssetRegistration))]
        [TestCase(nameof(MmdPmxImportStage.PmxSubAssetRegistered))]
        [TestCase(nameof(MmdPmxImportStage.MaterialsRegistered))]
        [TestCase(nameof(MmdPmxImportStage.HierarchyRegistered))]
        [TestCase(nameof(MmdPmxImportStage.MainObjectSet))]
        public void PmxImporterFaultRollbackRestoresGenericImportObjectAndSubAssetBaselines(
            string stageName)
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            AssertFaultedReimportReturnsToBaseline(
                TempPmxPath,
                Enum.Parse<MmdPmxImportStage>(stageName));
        }

        [TestCase(nameof(MmdPmxImportStage.HumanoidAvatarCreated))]
        [TestCase(nameof(MmdPmxImportStage.HumanoidProxyParented))]
        public void PmxImporterFaultRollbackRestoresHumanoidImportObjectAndSubAssetBaselines(
            string stageName)
        {
            CopyFixtureToAssetDatabase("test_semi_basic_bone.pmx", TempHumanoidPmxPath);
            SetPmxImporterAnimationType(TempHumanoidPmxPath, MmdPmxAnimationType.Humanoid);
            AssertFaultedReimportReturnsToBaseline(
                TempHumanoidPmxPath,
                Enum.Parse<MmdPmxImportStage>(stageName));
        }

        [Test]
        public void ImportObjectTransactionRejectsBorrowedPersistentAssets()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);
            MmdPmxAsset asset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(asset, Is.Not.Null);

            using var transaction = new MmdImportObjectTransaction();
            Assert.Throws<InvalidOperationException>(() => transaction.Track(asset));
            Assert.That(asset, Is.Not.Null, "Borrowed persistent asset must survive a rejected ownership transfer.");
        }

        private static void AssertFaultedReimportReturnsToBaseline(string assetPath, MmdPmxImportStage stage)
        {
            string[] baselineSubAssets = GetSubAssetSignature(assetPath);
            Dictionary<string, int> baselineObjectCounts = GetGeneratedObjectCounts(baselineSubAssets);
            Assert.That(baselineSubAssets, Is.Not.Empty, "A successful baseline import is required.");

            var injectedFailurePattern = new Regex(
                "Injected PMX importer failure at stage '" + Regex.Escape(stage.ToString()) + "'");
            LogAssert.Expect(LogType.Exception, injectedFailurePattern);
            LogAssert.Expect(LogType.Error, injectedFailurePattern);
            using (MmdPmxImportFaultInjection.FailAt(assetPath, stage))
            {
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }

            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            string[] reimportedSubAssets = GetSubAssetSignature(assetPath);
            Assert.That(reimportedSubAssets, Is.EqualTo(baselineSubAssets),
                "A clean reimport after rollback must reproduce the exact sub-asset type/name set.");
            Assert.That(GetGeneratedObjectCounts(baselineSubAssets), Is.EqualTo(baselineObjectCounts),
                "Faulted import objects must be gone after the next successful reimport.");
        }

        private static string[] GetSubAssetSignature(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .Where(value => value != null)
                .Select(value => value.GetType().FullName + "|" + value.name)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static Dictionary<string, int> GetGeneratedObjectCounts(IEnumerable<string> signatures)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string signature in signatures.Distinct(StringComparer.Ordinal))
            {
                int separator = signature.IndexOf('|');
                string typeName = signature.Substring(0, separator);
                string objectName = signature.Substring(separator + 1);
                int count = Resources.FindObjectsOfTypeAll<Object>()
                    .Count(value => value != null
                        && string.Equals(value.GetType().FullName, typeName, StringComparison.Ordinal)
                        && string.Equals(value.name, objectName, StringComparison.Ordinal));
                result[signature] = count;
            }

            return result;
        }
    }
}
