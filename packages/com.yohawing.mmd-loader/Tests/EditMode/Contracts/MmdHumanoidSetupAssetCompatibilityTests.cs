#nullable enable

#pragma warning disable CS0618

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Editor;

namespace Mmd.Tests
{
    public sealed class MmdHumanoidSetupAssetCompatibilityTests
    {
        [Test]
        public void LegacyMonoScriptKeepsPublishedGuid()
        {
            var asset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            try
            {
                MonoScript script = MonoScript.FromScriptableObject(asset);
                string path = AssetDatabase.GetAssetPath(script);

                Assert.That(path, Is.Not.Empty);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo("954e97ec9101f0c49bba3077496fe31f"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void LegacySerializedFieldsRemainReadable()
        {
            const string AssetPath = "Assets/__MmdLegacyHumanoidSetup.asset";
            string absolutePath = Path.Combine(Application.dataPath, "__MmdLegacyHumanoidSetup.asset");
            try
            {
                File.WriteAllText(absolutePath,
                    "%YAML 1.1\n"
                    + "%TAG !u! tag:unity3d.com,2011:\n"
                    + "--- !u!114 &11400000\n"
                    + "MonoBehaviour:\n"
                    + "  m_ObjectHideFlags: 0\n"
                    + "  m_CorrespondingSourceObject: {fileID: 0}\n"
                    + "  m_PrefabInstance: {fileID: 0}\n"
                    + "  m_PrefabAsset: {fileID: 0}\n"
                    + "  m_GameObject: {fileID: 0}\n"
                    + "  m_Enabled: 1\n"
                    + "  m_EditorHideFlags: 0\n"
                    + "  m_Script: {fileID: 11500000, guid: 954e97ec9101f0c49bba3077496fe31f, type: 3}\n"
                    + "  m_Name: LegacyHumanoidSetup\n"
                    + "  m_EditorClassIdentifier:\n"
                    + "  pmxAsset: {fileID: 0}\n"
                    + "  setupPreset: 1\n"
                    + "  pmxBoneCount: 42\n"
                    + "  mappingReadiness: Ready\n"
                    + "  mappingInputSource: ImportedHierarchy\n"
                    + "  requiredMappedBoneCount: 0\n"
                    + "  optionalMappedBoneCount: 0\n"
                    + "  missingRequiredBoneCount: 0\n"
                    + "  ambiguousMappingCount: 0\n"
                    + "  ignoredHelperBoneCount: 0\n"
                    + "  mappingDiagnostics: []\n"
                    + "  nativePlaybackImpact: None\n"
                    + "  mappingEntries: []\n");
                AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);

                MmdHumanoidSetupAsset asset =
                    AssetDatabase.LoadAssetAtPath<MmdHumanoidSetupAsset>(AssetPath);
                Assert.That(asset, Is.Not.Null);

                Assert.That(asset.SetupPreset, Is.EqualTo(MmdHumanoidSetupPreset.MmdStandard));
                Assert.That(asset.PmxBoneCount, Is.EqualTo(42));
                Assert.That(asset.MappingReadiness, Is.EqualTo(MmdHumanoidMappingReadiness.Ready));
                Assert.That(asset.MappingInputSource, Is.EqualTo(MmdHumanoidSetupAsset.ImportedHierarchyInputSource));
                Assert.That(asset.NativePlaybackImpact, Is.EqualTo(MmdHumanoidSetupAsset.NoNativePlaybackImpact));
            }
            finally
            {
                AssetDatabase.DeleteAsset(AssetPath);
                if (File.Exists(absolutePath)) File.Delete(absolutePath);
                if (File.Exists(absolutePath + ".meta")) File.Delete(absolutePath + ".meta");
            }
        }

        [Test]
        public void LegacyInitializeRejectsNullSource()
        {
            var asset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            try
            {
                Assert.Throws<ArgumentNullException>(() => asset.Initialize(null!));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void LegacyBuilderKeepsDefaultPathHelper()
        {
            var pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                Assert.That(
                    MmdHumanoidSetupAssetBuilder.GetDefaultSetupAssetPath(pmxAsset),
                    Is.EqualTo("Assets/MmdHumanoidSetup.asset"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pmxAsset);
            }
        }

        [Test]
        public void LegacyBuilderRejectsCreationBeforeValidatingInputs()
        {
            NotSupportedException? exception = Assert.Throws<NotSupportedException>(() =>
                MmdHumanoidSetupAssetBuilder.CreateHumanoidSetupAsset(null!, string.Empty));

            Assert.That(exception!.Message, Does.Contain("Animation Type = Humanoid"));
        }

        [Test]
        public void LegacyBuilderNeverCreatesNewSetupAsset()
        {
            var pmxAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                NotSupportedException? exception = Assert.Throws<NotSupportedException>(() =>
                    MmdHumanoidSetupAssetBuilder.CreateHumanoidSetupAsset(
                        pmxAsset,
                        "Assets/MmdHumanoidSetup.asset",
                        MmdHumanoidSetupPreset.MmdStandard));

                Assert.That(exception!.Message, Does.Contain("Animation Type = Humanoid"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pmxAsset);
            }
        }
    }
}
