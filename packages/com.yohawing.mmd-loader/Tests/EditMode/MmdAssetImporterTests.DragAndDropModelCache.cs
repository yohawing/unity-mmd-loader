#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Editor;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    // The PMX scene drop reads its model from the asset's synchronous playback cache. These tests
    // pin that the drop reuses the cache, that a reimport with new bytes is reflected on the next
    // drop, and that neither the drop nor the playback paths that share the cache modify it.
    public sealed partial class MmdAssetImporterTests
    {
        [Test]
        public void SceneDragAndDropReusesSynchronousPlaybackModelCache()
        {
            MmdPmxAsset pmxAsset = ImportCharacterizationFixture("test_vertex_morph.pmx", textureFixture: null);
            MmdModelDefinition cached = pmxAsset.LoadValidatedModelForSynchronousPlayback(parser: null, out _);
            Assert.That(cached.materials, Is.Not.Empty);
            string originalName = cached.materials[0].name;
            const string sentinel = "__dnd-model-cache-sentinel__";
            MmdUnityModelInstance? instance = null;
            try
            {
                // Mark the cached model so the drop's descriptor reveals which model it was built from.
                cached.materials[0].name = sentinel;

                instance = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.zero, parent: null);

                Assert.That(instance.RenderingDescriptor.materials[0].name, Is.EqualTo(sentinel),
                    "the drop must build its descriptor from the cached model instead of parsing again");
            }
            finally
            {
                cached.materials[0].name = originalName;
                DestroyDroppedInstance(instance);
            }
        }

        [Test]
        public void SceneDragAndDropSameAssetTwiceProducesIndependentCorrectInstances()
        {
            MmdPmxAsset pmxAsset = ImportCharacterizationFixture("test_vertex_morph.pmx", textureFixture: null);
            MmdModelDefinition model = pmxAsset.LoadModel();
            MmdUnityModelInstance? first = null;
            MmdUnityModelInstance? second = null;
            try
            {
                first = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.zero, parent: null);
                second = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.one, parent: null);

                pmxAsset.LoadValidatedModelForSynchronousPlayback(parser: null, out bool cacheHit);
                Assert.That(cacheHit, Is.True, "the first drop must leave the parsed model in the asset cache");
                Assert.That(second.Root, Is.Not.SameAs(first.Root));
                Assert.That(second.RenderingDescriptor, Is.Not.SameAs(first.RenderingDescriptor));
                Assert.That(second.RenderingDescriptor.materials, Is.Not.SameAs(first.RenderingDescriptor.materials));
                AssertDragAndDropInstanceCharacterization(pmxAsset, model, first, expectMaterialClones: false);
                AssertDragAndDropInstanceCharacterization(pmxAsset, model, second, expectMaterialClones: false);
            }
            finally
            {
                DestroyDroppedInstance(first);
                DestroyDroppedInstance(second);
            }
        }

        [Test]
        public void SceneDragAndDropAfterReimportWithNewBytesUsesNewModel()
        {
            string pmxAssetPath = TempDirectory + "/dnd_model_cache_reimport.pmx";
            Directory.CreateDirectory(Path.Combine(MmdTestFixtures.ProjectRoot, TempDirectory));
            MmdTestFixtures.CopyFixtureToAssetDatabase("test_1bone_cube.pmx", pmxAssetPath);
            MmdPmxAsset firstAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(pmxAssetPath);
            Assert.That(firstAsset, Is.Not.Null);
            MmdModelDefinition firstModel = firstAsset.LoadModel();
            MmdUnityModelInstance? before = null;
            MmdUnityModelInstance? after = null;
            try
            {
                before = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(firstAsset, Vector3.zero, parent: null);
                AssertDragAndDropInstanceCharacterization(firstAsset, firstModel, before, expectMaterialClones: false);
                firstAsset.LoadValidatedModelForSynchronousPlayback(parser: null, out bool warmed);
                Assert.That(warmed, Is.True, "precondition: the cache holds the first model before the reimport");
                DestroyDroppedInstance(before);
                before = null;

                // Same asset path, different PMX content.
                File.Copy(
                    MmdTestFixtures.FixtureAssetPath("test_vertex_morph.pmx"),
                    Path.Combine(MmdTestFixtures.ProjectRoot, pmxAssetPath),
                    overwrite: true);
                AssetDatabase.ImportAsset(pmxAssetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                MmdPmxAsset secondAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(pmxAssetPath);
                Assert.That(secondAsset, Is.Not.Null);
                MmdModelDefinition secondModel = secondAsset.LoadModel();
                Assert.That(
                    (secondModel.vertices.Count, secondModel.materials.Count, secondModel.morphs.Count),
                    Is.Not.EqualTo((firstModel.vertices.Count, firstModel.materials.Count, firstModel.morphs.Count)),
                    "the two fixtures must differ for this check to mean anything");

                after = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(secondAsset, Vector3.zero, parent: null);

                AssertDragAndDropInstanceCharacterization(secondAsset, secondModel, after, expectMaterialClones: false);
                MmdModelDefinition cached = secondAsset.LoadValidatedModelForSynchronousPlayback(parser: null, out _);
                Assert.That(ComputeModelHash(cached), Is.EqualTo(ComputeModelHash(secondModel)));
            }
            finally
            {
                DestroyDroppedInstance(before);
                DestroyDroppedInstance(after);
            }
        }

        // The hair fixture has world-anchored joints, which Live physics on the full model rejects;
        // its Live physics runs through the model-only physics binding instead.
        [TestCase("test_vertex_morph.pmx", "test_vertex_morph_motion.vmd", MmdPhysicsMode.Live)]
        [TestCase("test_hair_physics.pmx", "test_1bone_cube_motion.vmd", MmdPhysicsMode.Off)]
        public void SceneDragAndDropAndCachedPlaybackPathsDoNotModifyCachedModel(
            string pmxFixture,
            string vmdFixture,
            MmdPhysicsMode playbackPhysicsMode)
        {
            MmdPmxAsset pmxAsset = ImportCharacterizationFixture(pmxFixture, textureFixture: null);
            string vmdAssetPath = TempDirectory + "/" + vmdFixture;
            MmdTestFixtures.CopyFixtureToAssetDatabase(vmdFixture, vmdAssetPath);
            MmdVmdAsset vmdAsset = AssetDatabase.LoadAssetAtPath<MmdVmdAsset>(vmdAssetPath);
            Assert.That(vmdAsset, Is.Not.Null);
            MmdModelDefinition cached = pmxAsset.LoadValidatedModelForSynchronousPlayback(parser: null, out _);
            string expectedHash = ComputeModelHash(cached);
            Assert.That(expectedHash, Is.EqualTo(ComputeModelHash(pmxAsset.LoadModel())));
            MmdUnityModelInstance? dropped = null;
            MmdUnityModelInstance? secondDropped = null;
            MmdUnityPlaybackBinding? ownedBinding = null;
            MmdUnityPlaybackBinding? modelOnlyPhysicsBinding = null;
            try
            {
                dropped = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.zero, parent: null);
                Assert.That(ComputeModelHash(cached), Is.EqualTo(expectedHash), "after the drop");

                // Playback on the dropped instance rebinds through the same cached model.
                MmdUnityPlaybackController controller = dropped.Root.GetComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(playbackPhysicsMode);
                controller.ConfigureFromPlaybackSource(
                    pmxAsset,
                    vmdAsset,
                    new MmdPlaybackConfig(frameRate: 30.0f, initialFrame: 0, playOnStart: false));
                controller.ApplyFrame(1);
                controller.ApplyFrame(2);
                Assert.That(ComputeModelHash(cached), Is.EqualTo(expectedHash), "after controller playback");

                // Owned binding from the assets also reads the cached model.
                ownedBinding = MmdUnityPlaybackBinding.CreateSkinned(pmxAsset, vmdAsset);
                ownedBinding.SetPhysicsMode(playbackPhysicsMode);
                ownedBinding.ApplyFrame(0, 30.0f);
                ownedBinding.ApplyFrame(1, 30.0f);
                Assert.That(ComputeModelHash(cached), Is.EqualTo(expectedHash), "after the owned binding");

                // The model-only physics binding edits its joints, on its own parse.
                secondDropped = MmdSceneDragAndDrop.LoadPmxForDragAndDrop(pmxAsset, Vector3.one, parent: null);
                modelOnlyPhysicsBinding = MmdUnityPlaybackBinding.CreateSkinnedForModelOnlyPhysicsFromExistingSceneModel(
                    secondDropped.Root,
                    pmxAsset,
                    "model-cache-test");
                modelOnlyPhysicsBinding.SetPhysicsMode(MmdPhysicsMode.Live);
                modelOnlyPhysicsBinding.ApplyFrame(0, 30.0f);
                modelOnlyPhysicsBinding.ApplyFrame(1, 30.0f);
                Assert.That(ComputeModelHash(cached), Is.EqualTo(expectedHash), "after the model-only physics binding");

                Assert.That(pmxAsset.LoadValidatedModelForSynchronousPlayback(parser: null, out bool cacheHit), Is.SameAs(cached));
                Assert.That(cacheHit, Is.True);
            }
            finally
            {
                modelOnlyPhysicsBinding?.Dispose();
                ownedBinding?.Dispose();
                DestroyDroppedInstance(dropped);
                DestroyDroppedInstance(secondDropped);
            }
        }

        private static void DestroyDroppedInstance(MmdUnityModelInstance? instance)
        {
            if (instance?.Root != null)
            {
                Object.DestroyImmediate(instance.Root);
            }
        }

        private static string ComputeModelHash(MmdModelDefinition model)
        {
            // The parsed IR (JSON) and the [NonSerialized] PMX source bytes that native evaluation
            // reads are hashed separately, so a write to either one changes the result.
            using var sha256 = SHA256.Create();
            byte[] irHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(model)));
            byte[] sourceHash = model.sourceBytes != null
                ? sha256.ComputeHash(model.sourceBytes)
                : Array.Empty<byte>();
            return BitConverter.ToString(irHash) + "|source:" + BitConverter.ToString(sourceHash);
        }
    }
}
