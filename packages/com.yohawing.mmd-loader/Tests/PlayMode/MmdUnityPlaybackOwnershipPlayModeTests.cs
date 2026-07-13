#nullable enable

using System.Collections;
using System.Linq;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.UnityIntegration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mmd.Tests
{
    public sealed class MmdUnityPlaybackOwnershipPlayModeTests
    {
        [UnityTest]
        public IEnumerator ControllerDestroyDisposesOwnedRuntimeInstance()
        {
            const string modelName = "owned-playmode-model";
            MmdModelDefinition model = MmdPlayModeTestFixtures.CreateMinimalTriangleModel(modelName);
            MmdMotionDefinition motion = MmdPlayModeTestFixtures.CreateRootTranslationMotion(modelName);
            var holder = new GameObject("owned-playback-holder");
            MmdUnityPlaybackBinding binding = MmdUnityPlaybackBinding.CreateSkinned(
                model,
                motion,
                modelName,
                "owned-playmode-motion");
            GameObject root = binding.Instance.Root;
            Mesh mesh = binding.Instance.Mesh;
            Material[] materials = binding.Instance.Materials.ToArray();
            try
            {
                MmdUnityPlaybackController controller = holder.AddComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                controller.Configure(binding, 30.0f, playOnStart: false);

                Object.Destroy(holder);
                yield return null;
                yield return null;

                Assert.That(root == null, Is.True);
                Assert.That(mesh == null, Is.True);
                Assert.That(materials.All(material => material == null), Is.True);
            }
            finally
            {
                if (holder != null)
                {
                    Object.Destroy(holder);
                }

                binding.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ControllerDestroyPreservesBorrowedRuntimeInstance()
        {
            const string modelName = "borrowed-playmode-model";
            MmdModelDefinition model = MmdPlayModeTestFixtures.CreateMinimalTriangleModel(modelName);
            MmdMotionDefinition motion = MmdPlayModeTestFixtures.CreateRootTranslationMotion(modelName);
            MmdUnityModelInstance instance = MmdUnityModelFactory.CreateSkinnedModel(model);
            MmdVmdAsset motionAsset = ScriptableObject.CreateInstance<MmdVmdAsset>();
            motionAsset.name = "borrowed-playmode-motion";
            var holder = new GameObject("borrowed-playback-holder");
            MmdUnityPlaybackBinding binding = MmdUnityPlaybackBinding.CreateSkinned(
                instance,
                model,
                motionAsset,
                motion,
                modelName);
            try
            {
                MmdUnityPlaybackController controller = holder.AddComponent<MmdUnityPlaybackController>();
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                controller.Configure(binding, 30.0f, playOnStart: false);

                Object.Destroy(holder);
                yield return null;
                yield return null;

                Assert.That(instance.Root != null, Is.True);
                Assert.That(instance.Mesh != null, Is.True);
                Assert.That(instance.Materials.All(material => material != null), Is.True);
            }
            finally
            {
                if (holder != null)
                {
                    Object.Destroy(holder);
                }

                binding.Dispose();
                Object.Destroy(motionAsset);
                MmdPlayModeTestInstanceScope.DestroyInstance(instance);
            }
        }
    }
}
