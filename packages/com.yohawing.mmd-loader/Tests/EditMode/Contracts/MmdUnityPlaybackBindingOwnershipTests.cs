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
