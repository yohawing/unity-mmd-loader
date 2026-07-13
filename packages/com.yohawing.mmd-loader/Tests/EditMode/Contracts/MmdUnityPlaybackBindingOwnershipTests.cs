#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmdUnityPlaybackBindingOwnershipTests
    {
        private const string ModelFixture = "test_1bone_cube.pmx";
        private const string MotionFixture = "test_1bone_cube_motion.vmd";

        [Test]
        public void OwnedBindingDisposeDestroysGeneratedObjectsAndIsIdempotent()
        {
            string tempRoot = CreateTempDirectory();
            try
            {
                (MmdModelDefinition model, MmdMotionDefinition motion) = LoadFixturePair();
                string pmxPath = Path.Combine(tempRoot, "model.pmx");
                string texturePath = Path.Combine(tempRoot, "owned.png");
                File.WriteAllBytes(pmxPath, new byte[] { 0x50, 0x4d, 0x58 });
                WritePng(texturePath);
                model.materials[0].texture = "owned.png";

                MmdUnityPlaybackBinding binding = MmdUnityPlaybackBinding.CreateSkinned(
                    model,
                    motion,
                    "owned-model",
                    "owned-motion",
                    pmxPath);
                GameObject root = binding.Instance.Root;
                Mesh mesh = binding.Instance.Mesh;
                Material[] materials = binding.Instance.Materials.ToArray();
                Texture2D[] textures = binding.Instance.OwnedTextures.ToArray();
                Assert.That(textures, Is.Not.Empty);

                binding.Dispose();
                Assert.DoesNotThrow(binding.Dispose);

                Assert.That(root == null, Is.True);
                Assert.That(mesh == null, Is.True);
                Assert.That(materials.All(material => material == null), Is.True);
                Assert.That(textures.All(texture => texture == null), Is.True);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void BorrowedBindingDisposePreservesCallerInstance()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadFixturePair();
            MmdUnityModelInstance instance = MmdUnityModelFactory.CreateSkinnedModel(model);
            MmdVmdAsset motionAsset = CreateMotionAsset();
            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = MmdUnityPlaybackBinding.CreateSkinned(
                    instance,
                    model,
                    motionAsset,
                    motion,
                    "borrowed-model");
                GameObject root = instance.Root;
                Mesh mesh = instance.Mesh;
                Material[] materials = instance.Materials.ToArray();

                binding.Dispose();

                Assert.That(root != null, Is.True);
                Assert.That(mesh != null, Is.True);
                Assert.That(materials.All(material => material != null), Is.True);
            }
            finally
            {
                binding?.Dispose();
                Object.DestroyImmediate(motionAsset);
                MmdTestInstanceScope.DestroyInstance(instance);
            }
        }

        [Test]
        public void ControllerReplacementOneHundredTimesDestroysEveryOwnedInstance()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadFixturePair();
            var holder = new GameObject("ownership-controller-holder");
            var ownedObjects = new List<Object>();
            MmdUnityPlaybackBinding? lastBinding = null;
            try
            {
                MmdUnityPlaybackController controller = holder.AddComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                for (int i = 0; i < 100; i++)
                {
                    MmdUnityPlaybackBinding binding = MmdUnityPlaybackBinding.CreateSkinned(
                        model,
                        motion,
                        "owned-model-" + i,
                        "owned-motion-" + i);
                    binding.Instance.Root.name = "owned-root-" + i;
                    binding.Instance.Mesh.name = "owned-mesh-" + i;
                    foreach (Material material in binding.Instance.Materials)
                    {
                        material.name += "-" + i;
                    }
                    ownedObjects.Add(binding.Instance.Root);
                    ownedObjects.Add(binding.Instance.Mesh);
                    ownedObjects.AddRange(binding.Instance.Materials);
                    ownedObjects.AddRange(binding.Instance.OwnedTextures);
                    controller.Configure(binding, 30.0f, playOnStart: false);
                    lastBinding = binding;
                }

                lastBinding!.Dispose();
                Object.DestroyImmediate(holder);
                string[] survivors = ownedObjects
                    .Where(value => value != null)
                    .Select(value => value.GetType().Name + ":" + value.name)
                    .ToArray();
                Assert.That(survivors, Is.Empty, string.Join(", ", survivors));
            }
            finally
            {
                if (holder != null)
                {
                    Object.DestroyImmediate(holder);
                }
            }
        }

        [Test]
        public void CreationFailureAfterInstanceAllocationLeavesNoUnityObjects()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadFixturePair();
            HashSet<int> before = CaptureNonPersistentObjectIds();

            Assert.Throws<ArgumentException>(() => MmdUnityPlaybackBinding.CreateSkinned(
                model,
                motion,
                " ",
                "motion"));

            Assert.That(CaptureNonPersistentObjectIds(), Is.EquivalentTo(before));
        }

        [Test]
        public void ExistingSceneRebindLeaseRollbackRestoresBorrowedStateAndDestroysGeneratedResources()
        {
            (MmdModelDefinition model, _) = LoadFixturePair();
            MmdUnityModelInstance? sceneInstance = null;
            Mesh? borrowedMesh = null;
            Mesh? generatedMesh = null;
            Material? generatedMaterial = null;
            SkinnedMeshRenderer? renderer = null;
            Material[] originalMaterials = Array.Empty<Material>();
            Transform[] originalBones = Array.Empty<Transform>();
            Transform? originalRootBone = null;
            Bounds originalLocalBounds = default;
            Vector3 originalBonePosition = default;
            Quaternion originalBoneRotation = default;
            Vector3 originalBoneScale = default;
            HashSet<int>? before = null;
            MmdExistingSceneRebindLease? lease = null;

            try
            {
                sceneInstance = MmdUnityModelFactory.CreateSkinnedModel(model);
                renderer = sceneInstance.SkinnedMeshRenderer!;
                originalMaterials = renderer.sharedMaterials;
                originalBones = renderer.bones;
                borrowedMesh = new Mesh { name = "borrowed-empty-mesh" };
                renderer.sharedMesh = borrowedMesh;

                Transform bone = originalBones[0];
                bone.localPosition = new Vector3(1.0f, 2.0f, 3.0f);
                bone.localRotation = Quaternion.Euler(10.0f, 20.0f, 30.0f);
                bone.localScale = new Vector3(1.2f, 0.8f, 1.1f);
                originalBonePosition = bone.localPosition;
                originalBoneRotation = bone.localRotation;
                originalBoneScale = bone.localScale;
                renderer.rootBone = null;
                originalRootBone = renderer.rootBone;
                renderer.localBounds = new Bounds(new Vector3(3.0f, 4.0f, 5.0f), new Vector3(6.0f, 7.0f, 8.0f));
                originalLocalBounds = renderer.localBounds;

                before = CaptureNonPersistentObjectIds();
                lease = new MmdExistingSceneRebindLease(sceneInstance.Root);
                generatedMesh = new Mesh { name = "generated-rebind-mesh" };
                generatedMaterial = new Material(originalMaterials[0]) { name = "generated-rebind-material" };
                renderer.sharedMesh = generatedMesh;
                renderer.sharedMaterials = new[] { generatedMaterial };
                renderer.rootBone = bone;
                renderer.localBounds = new Bounds(Vector3.one, Vector3.one * 2.0f);
                bone.localPosition = Vector3.zero;
                bone.localRotation = Quaternion.identity;
                bone.localScale = Vector3.one;
                lease.AdoptGeneratedMesh(generatedMesh);
                lease.AdoptGeneratedMaterials(new[] { generatedMaterial });

                lease.RollbackFactoryFailure();
                lease = null;

                Assert.That(renderer.sharedMesh, Is.SameAs(borrowedMesh));
                Assert.That(renderer.sharedMaterials, Is.EqualTo(originalMaterials));
                Assert.That(renderer.rootBone, Is.SameAs(originalRootBone));
                Assert.That(renderer.localBounds, Is.EqualTo(originalLocalBounds));
                Assert.That(bone.localPosition, Is.EqualTo(originalBonePosition));
                Assert.That(Quaternion.Angle(bone.localRotation, originalBoneRotation), Is.LessThan(0.0001f));
                Assert.That(bone.localScale, Is.EqualTo(originalBoneScale));
                Assert.That(generatedMesh == null, Is.True);
                Assert.That(generatedMaterial == null, Is.True);
                Assert.That(CaptureNonPersistentObjectIds(), Is.EquivalentTo(before));
            }
            finally
            {
                lease?.Dispose();
                if (renderer != null)
                {
                    renderer.sharedMesh = borrowedMesh;
                    renderer.sharedMaterials = originalMaterials;
                    renderer.bones = originalBones;
                    renderer.rootBone = originalRootBone;
                    renderer.localBounds = originalLocalBounds;
                    if (originalBones.Length > 0 && originalBones[0] != null)
                    {
                        originalBones[0].localPosition = originalBonePosition;
                        originalBones[0].localRotation = originalBoneRotation;
                        originalBones[0].localScale = originalBoneScale;
                    }
                }

                DestroyCreatedMeshesAndMaterials(before);
                Object.DestroyImmediate(borrowedMesh);
                MmdTestInstanceScope.DestroyInstance(sceneInstance);
            }
        }

        [Test]
        public void ExistingSceneBindingDisposeRestoresBorrowedStateAndDestroysGeneratedResources()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadFixturePair();
            MmdUnityModelInstance? sceneInstance = null;
            MmdPmxAsset? modelAsset = null;
            MmdVmdAsset? motionAsset = null;
            MmdMaterialOverrideAsset? overrideAsset = null;
            MmdUnityPlaybackBinding? binding = null;
            Mesh? borrowedMesh = null;
            SkinnedMeshRenderer? renderer = null;
            Material[] originalMaterials = Array.Empty<Material>();
            Transform[] originalBones = Array.Empty<Transform>();
            Transform? originalRootBone = null;
            Bounds originalLocalBounds = default;
            Vector3 originalBonePosition = default;
            Quaternion originalBoneRotation = default;
            Vector3 originalBoneScale = default;
            HashSet<int>? before = null;

            try
            {
                sceneInstance = MmdUnityModelFactory.CreateSkinnedModel(model);
                renderer = sceneInstance.SkinnedMeshRenderer!;
                originalMaterials = renderer.sharedMaterials;
                originalBones = renderer.bones;
                borrowedMesh = new Mesh { name = "borrowed-empty-mesh" };
                renderer.sharedMesh = borrowedMesh;

                Transform bone = originalBones[0];
                bone.localPosition = new Vector3(1.0f, 2.0f, 3.0f);
                bone.localRotation = Quaternion.Euler(10.0f, 20.0f, 30.0f);
                bone.localScale = new Vector3(1.2f, 0.8f, 1.1f);
                originalBonePosition = bone.localPosition;
                originalBoneRotation = bone.localRotation;
                originalBoneScale = bone.localScale;
                renderer.rootBone = null;
                originalRootBone = renderer.rootBone;
                renderer.localBounds = new Bounds(new Vector3(3.0f, 4.0f, 5.0f), new Vector3(6.0f, 7.0f, 8.0f));
                originalLocalBounds = renderer.localBounds;

                overrideAsset = CreateAlphaOverride();
                byte[] modelBytes = MmdTestFixtures.ReadFixtureAssetBytes(ModelFixture);
                modelAsset = ScriptableObject.CreateInstance<MmdPmxAsset>();
                modelAsset.Initialize(
                    modelBytes,
                    ModelFixture,
                    MmdTestFixtures.FixtureAssetPath(ModelFixture),
                    importedMaterialOverrideAsset: overrideAsset);
                motionAsset = CreateMotionAsset();
                before = CaptureNonPersistentObjectIds();

                binding = MmdUnityPlaybackBinding.CreateSkinnedFromExistingSceneModel(
                    sceneInstance.Root,
                    modelAsset,
                    motionAsset,
                    motion);
                binding.SetPhysicsMode(MmdPhysicsMode.Off);
                Mesh generatedMesh = renderer.sharedMesh!;
                Material[] generatedMaterials = renderer.sharedMaterials;
                Assert.That(generatedMesh, Is.Not.SameAs(borrowedMesh));
                Assert.That(generatedMaterials[0], Is.Not.SameAs(originalMaterials[0]));

                binding.Dispose();
                binding = null;

                Assert.That(renderer.sharedMesh, Is.SameAs(borrowedMesh));
                Assert.That(renderer.sharedMaterials, Is.EqualTo(originalMaterials));
                Assert.That(renderer.rootBone, Is.SameAs(originalRootBone));
                Assert.That(renderer.localBounds, Is.EqualTo(originalLocalBounds));
                Assert.That(bone.localPosition, Is.EqualTo(originalBonePosition));
                Assert.That(Quaternion.Angle(bone.localRotation, originalBoneRotation), Is.LessThan(0.0001f));
                Assert.That(bone.localScale, Is.EqualTo(originalBoneScale));
                Assert.That(generatedMesh == null, Is.True);
                Assert.That(generatedMaterials.All(material => material == null), Is.True);
                Assert.That(CaptureNonPersistentObjectIds(), Is.EquivalentTo(before));
            }
            finally
            {
                binding?.Dispose();
                if (renderer != null)
                {
                    renderer.sharedMesh = borrowedMesh;
                    renderer.sharedMaterials = originalMaterials;
                    renderer.bones = originalBones;
                    renderer.rootBone = originalRootBone;
                    renderer.localBounds = originalLocalBounds;
                    if (originalBones.Length > 0 && originalBones[0] != null)
                    {
                        originalBones[0].localPosition = originalBonePosition;
                        originalBones[0].localRotation = originalBoneRotation;
                        originalBones[0].localScale = originalBoneScale;
                    }
                }

                DestroyCreatedMeshesAndMaterials(before);
                Object.DestroyImmediate(modelAsset);
                Object.DestroyImmediate(motionAsset);
                Object.DestroyImmediate(overrideAsset);
                Object.DestroyImmediate(borrowedMesh);
                MmdTestInstanceScope.DestroyInstance(sceneInstance);
            }
        }

        private static (MmdModelDefinition Model, MmdMotionDefinition Motion) LoadFixturePair()
        {
            var parser = new NativeMmdParser();
            return (
                parser.LoadModel(MmdTestFixtures.ReadFixtureAssetBytes(ModelFixture)),
                parser.LoadMotion(MmdTestFixtures.ReadFixtureAssetBytes(MotionFixture)));
        }

        private static MmdVmdAsset CreateMotionAsset()
        {
            byte[] bytes = MmdTestFixtures.ReadFixtureAssetBytes(MotionFixture);
            var asset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            asset.Initialize(bytes, MotionFixture, MmdTestFixtures.FixtureAssetPath(MotionFixture));
            return asset;
        }

        private static MmdMaterialOverrideAsset CreateAlphaOverride()
        {
            var asset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
            asset.entries = new[]
            {
                new MmdMaterialOverrideEntry
                {
                    materialIndex = 0,
                    hasAlpha = true,
                    alpha = 0.45f
                }
            };
            return asset;
        }

        private static void DestroyCreatedMeshesAndMaterials(HashSet<int>? before)
        {
            if (before == null)
            {
                return;
            }

            DestroyCreatedObjects<Mesh>(before);
            DestroyCreatedObjects<Material>(before);
        }

        private static void DestroyCreatedObjects<T>(HashSet<int> before) where T : Object
        {
            foreach (T value in Resources.FindObjectsOfTypeAll<T>())
            {
                if (!EditorUtility.IsPersistent(value) && !before.Contains(value.GetInstanceID()))
                {
                    Object.DestroyImmediate(value);
                }
            }
        }

        private static HashSet<int> CaptureNonPersistentObjectIds()
        {
            var ids = new HashSet<int>();
            AddNonPersistentIds<GameObject>(ids);
            AddNonPersistentIds<Mesh>(ids);
            AddNonPersistentIds<Material>(ids);
            AddNonPersistentIds<Texture2D>(ids);
            return ids;
        }

        private static void AddNonPersistentIds<T>(HashSet<int> destination) where T : Object
        {
            foreach (T value in Resources.FindObjectsOfTypeAll<T>())
            {
                if (!EditorUtility.IsPersistent(value))
                {
                    destination.Add(value.GetInstanceID());
                }
            }
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "mmd-binding-ownership-" + Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            return path;
        }

        private static void WritePng(string path)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
