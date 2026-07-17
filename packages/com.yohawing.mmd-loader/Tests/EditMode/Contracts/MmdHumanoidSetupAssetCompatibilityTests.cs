#nullable enable

#pragma warning disable CS0618

using System;
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
            var asset = ScriptableObject.CreateInstance<MmdHumanoidSetupAsset>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(
                    "{\"setupPreset\":1,\"pmxBoneCount\":42,\"mappingReadiness\":\"Ready\",\"mappingInputSource\":\"ImportedHierarchy\",\"nativePlaybackImpact\":\"None\"}",
                    asset);

                Assert.That(asset.SetupPreset, Is.EqualTo(MmdHumanoidSetupPreset.MmdStandard));
                Assert.That(asset.PmxBoneCount, Is.EqualTo(42));
                Assert.That(asset.MappingReadiness, Is.EqualTo(MmdHumanoidMappingReadiness.Ready));
                Assert.That(asset.MappingInputSource, Is.EqualTo(MmdHumanoidSetupAsset.ImportedHierarchyInputSource));
                Assert.That(asset.NativePlaybackImpact, Is.EqualTo(MmdHumanoidSetupAsset.NoNativePlaybackImpact));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
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
