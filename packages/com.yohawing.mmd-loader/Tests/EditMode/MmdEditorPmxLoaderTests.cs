#nullable enable

using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Editor;
using Mmd.Rendering;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Tests
{
    public sealed class MmdEditorPmxLoaderTests
    {
        private const string TempDirectory = "Assets/__MmdEditorPmxLoaderTests";
        private const string TempPmxPath = TempDirectory + "/test_1bone_cube.pmx";

        [SetUp]
        public void SetUp()
        {
            MmdTestTempScope.DeleteAssetFolder(TempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            MmdTestTempScope.DeleteAssetFolder(TempDirectory);
        }

        [Test]
        public void LoadPmxIntoSceneCreatesSkinnedSceneObjectFromFixture()
        {
            MmdUnityModelInstance? instance = null;
            try
            {
                string pmxPath = MmdTestFixtures.FixtureAssetPath("test_1bone_cube.pmx");

                instance = MmdEditorPmxLoader.LoadPmxIntoScene(pmxPath);

                Assert.That(instance.Root, Is.Not.Null);
                GameObject root = instance.Root!;
                Assert.That(root.scene.IsValid(), Is.True);
                Assert.That(instance.SkinnedMeshRenderer, Is.Not.Null);
                SkinnedMeshRenderer renderer = instance.SkinnedMeshRenderer!;
                Assert.That(renderer.sharedMesh, Is.EqualTo(instance.Mesh));
                Assert.That(instance.VertexCount, Is.GreaterThan(0));
                Assert.That(instance.IndexCount, Is.GreaterThanOrEqualTo(3));
                Assert.That(instance.BoneTransforms, Has.Length.GreaterThan(0));
                Assert.That(instance.SourceContext, Is.Not.Null);
                MmdUnityModelSourceContext sourceContext = instance.SourceContext!;
                Assert.That(sourceContext.SourcePath, Is.EqualTo(Path.GetFullPath(pmxPath)));

                MmdUnityPlaybackController controller = root.GetComponent<MmdUnityPlaybackController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.HasModelSource, Is.True);
                Assert.That(controller.ModelSourceId, Is.EqualTo(Path.GetFullPath(pmxPath)));

                MmdRuntimeImporterComponent importer = controller.GetComponent<MmdRuntimeImporterComponent>();
                Assert.That(importer, Is.Not.Null);
                Assert.That(importer.ModelPath, Is.EqualTo(Path.GetFullPath(pmxPath)));
                Assert.That(importer.MotionPath, Is.Empty);
            }
            finally
            {
                MmdTestInstanceScope.DestroyInstance(instance);
            }
        }

        [Test]
        public void VerificationFacadePmxLoadReportsInputValidationStageForMissingFile()
        {
            string missingPath = Path.Combine(Path.GetTempPath(), "missing-mmd-fixture.pmx");

            MmdEditorVerificationException ex = Assert.Throws<MmdEditorVerificationException>(
                () => MmdEditorVerificationFacade.LoadPmxIntoScene(missingPath));

            Assert.That(ex.Stage, Is.EqualTo(MmdEditorVerificationFacade.InputValidationStage));
        }

        [Test]
        public void RuntimeTextureResolverLoadsBmpBackedSpaSphereTexture()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "yohawing-mmd-unity-tests",
                Path.GetRandomFileName());
            MmdRuntimeTextureResolution? resolution = null;
            try
            {
                Directory.CreateDirectory(directory);
                string modelPath = Path.Combine(directory, "model.pmx");
                string spherePath = Path.Combine(directory, "h.spa");
                File.WriteAllText(modelPath, "placeholder");
                File.WriteAllBytes(spherePath, CreateOnePixelBmp());

                var descriptor = new MmdRenderingDescriptor();
                descriptor.materials.Add(new MmdMaterialDescriptor
                {
                    materialIndex = 0,
                    name = "mat",
                    sphereTexture = "h.spa",
                    vertexCount = 3
                });

                resolution = MmdRuntimeTextureResolver.ResolveDiffuseTextures(
                    descriptor,
                    MmdUnityModelSourceContext.FromOptionalPath(modelPath));

                Assert.That(resolution.SphereTextures, Has.Count.EqualTo(1));
                Assert.That(resolution.Diagnostics.LoadedSphereTextureCount, Is.EqualTo(1));
                Assert.That(resolution.Diagnostics.UnsupportedTextureReferenceCount, Is.EqualTo(0));
                Assert.That(resolution.Diagnostics.TextureReferences, Has.Count.EqualTo(1));
                Assert.That(resolution.Diagnostics.TextureReferences[0].materialIndex, Is.EqualTo(0));
                Assert.That(resolution.Diagnostics.TextureReferences[0].usage, Is.EqualTo("sphere"));
                Assert.That(resolution.Diagnostics.TextureReferences[0].reference, Is.EqualTo("h.spa"));
                Assert.That(resolution.Diagnostics.TextureReferences[0].resolvedPath, Is.EqualTo("h.spa"));
                Assert.That(resolution.Diagnostics.TextureReferences[0].status, Is.EqualTo("loaded"));
                Assert.That(resolution.SphereTextures[0].Texture.width, Is.EqualTo(1));
                Assert.That(resolution.SphereTextures[0].Texture.height, Is.EqualTo(1));
            }
            finally
            {
                if (resolution != null)
                {
                    foreach (MmdResolvedTexture texture in resolution.SphereTextures)
                    {
                        Object.DestroyImmediate(texture.Texture);
                    }
                }

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }

        private static byte[] CreateOnePixelBmp()
        {
            return new byte[]
            {
                0x42, 0x4D, 0x3A, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x36, 0x00, 0x00, 0x00, 0x28, 0x00,
                0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00,
                0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x13, 0x0B,
                0x00, 0x00, 0x13, 0x0B, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0xFF
            };
        }
        [Test]
        public void D1_TryResolveMmdPmxAssetFromMainGameObjectReturnsMetadataForImportedPmxGo()
        {
            // D1: When the .pmx main object is a GameObject, TryResolveMmdPmxAssetFromMainGameObject
            // must return the metadata MmdPmxAsset sub-asset.
            MmdTestFixtures.CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            GameObject? mainGo = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);
            Assert.That(mainGo, Is.Not.Null, "precondition: imported .pmx must have a GameObject main object");

            MmdPmxAsset? resolved = MmdEditorPmxLoader.TryResolveMmdPmxAssetFromMainGameObject(mainGo);
            Assert.That(resolved, Is.Not.Null,
                "TryResolveMmdPmxAssetFromMainGameObject must resolve the MmdPmxAsset sub-asset");

            MmdPmxAsset? direct = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(TempPmxPath);
            Assert.That(resolved, Is.SameAs(direct),
                "resolved asset must match LoadAssetAtPath<MmdPmxAsset>");
        }

        [Test]
        public void D1_TryResolveMmdPmxAssetFromMainGameObjectReturnsNullForNonPmxGo()
        {
            GameObject nonPmxGo = new("NonPmxObject");
            try
            {
                MmdPmxAsset? resolved = MmdEditorPmxLoader.TryResolveMmdPmxAssetFromMainGameObject(nonPmxGo);
                Assert.That(resolved, Is.Null,
                    "TryResolveMmdPmxAssetFromMainGameObject must return null for non-.pmx GameObject");
            }
            finally
            {
                Object.DestroyImmediate(nonPmxGo);
            }
        }

        [Test]
        public void D1_TryResolveMmdPmxAssetFromMainGameObjectReturnsNullForNullInput()
        {
            MmdPmxAsset? resolved = MmdEditorPmxLoader.TryResolveMmdPmxAssetFromMainGameObject(null);
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void D1_TryResolveMmdPmxAssetFromMainGameObjectReturnsNullForNonGameObject()
        {
            MmdPmxAsset nonGo = ScriptableObject.CreateInstance<MmdPmxAsset>();
            try
            {
                MmdPmxAsset? resolved = MmdEditorPmxLoader.TryResolveMmdPmxAssetFromMainGameObject(nonGo);
                Assert.That(resolved, Is.Null,
                    "TryResolveMmdPmxAssetFromMainGameObject must return null for non-GameObject selection");
            }
            finally
            {
                Object.DestroyImmediate(nonGo);
            }
        }

        [Test]
        public void D1_PmxAssetResolverAcceptsGameObjectMain()
        {
            MmdTestFixtures.CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            GameObject? mainGo = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);
            Assert.That(mainGo, Is.Not.Null, "precondition: imported .pmx must have a GameObject main object");

            Assert.That(MmdEditorPmxLoader.TryResolveMmdPmxAssetFromMainGameObject(mainGo), Is.Not.Null,
                "selected .pmx GameObject main object must resolve to the PMX metadata asset");
        }
    }
}
