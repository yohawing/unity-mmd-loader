using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Yohawing.MmdUnity.Editor;
using Yohawing.MmdUnity.Rendering;
using Yohawing.MmdUnity.UnityIntegration;
using Object = UnityEngine.Object;

namespace Yohawing.MmdUnity.Tests
{
    public sealed class MmdEditorPmxLoaderTests
    {
        private const string TempDirectory = "Assets/__MmdEditorPmxLoaderTests";
        private const string TempPmxPath = TempDirectory + "/test_1bone_cube.pmx";

        [SetUp]
        public void SetUp()
        {
            DeleteTempDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteTempDirectory();
        }

        private static void DeleteTempDirectory()
        {
            AssetDatabase.DeleteAsset(TempDirectory);
            string fullTempDirectory = Path.Combine(ProjectRoot, TempDirectory);
            if (Directory.Exists(fullTempDirectory))
            {
                foreach (string path in Directory.EnumerateFileSystemEntries(fullTempDirectory, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }

                Directory.Delete(fullTempDirectory, recursive: true);
            }

            string metaPath = fullTempDirectory + ".meta";
            if (File.Exists(metaPath))
            {
                File.SetAttributes(metaPath, FileAttributes.Normal);
                File.Delete(metaPath);
            }

            AssetDatabase.Refresh();
        }

        private static void CopyFixtureToAssetDatabase(string fixtureName, string destinationAssetPath)
        {
            string source = Path.Combine(RepositoryRoot, "packages", "com.yohawing.mmd-unity", "Tests", "Fixtures", "Assets", fixtureName);
            Directory.CreateDirectory(Path.Combine(ProjectRoot, TempDirectory));
            File.Copy(source, Path.Combine(ProjectRoot, destinationAssetPath), overwrite: true);
            AssetDatabase.ImportAsset(destinationAssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string RepositoryRoot => Path.GetFullPath(Path.Combine(ProjectRoot, ".."));
        [Test]
        public void LoadPmxIntoSceneCreatesSkinnedSceneObjectFromFixture()
        {
            MmdUnityModelInstance instance = null;
            try
            {
                string pmxPath = ResolvePackageFixture("test_1bone_cube.pmx");

                instance = MmdEditorPmxLoader.LoadPmxIntoScene(pmxPath);

                Assert.That(instance.Root, Is.Not.Null);
                Assert.That(instance.Root.scene.IsValid(), Is.True);
                Assert.That(instance.SkinnedMeshRenderer, Is.Not.Null);
                Assert.That(instance.SkinnedMeshRenderer.sharedMesh, Is.EqualTo(instance.Mesh));
                Assert.That(instance.VertexCount, Is.GreaterThan(0));
                Assert.That(instance.IndexCount, Is.GreaterThanOrEqualTo(3));
                Assert.That(instance.BoneTransforms, Has.Length.GreaterThan(0));
                Assert.That(instance.SourceContext, Is.Not.Null);
                Assert.That(instance.SourceContext.SourcePath, Is.EqualTo(Path.GetFullPath(pmxPath)));

                MmdUnityPlaybackController controller = instance.Root.GetComponent<MmdUnityPlaybackController>();
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
                DestroyInstance(instance);
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
            MmdRuntimeTextureResolution resolution = null;
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
                Assert.That(resolution.Diagnostics.TextureReferences[0].resolvedPath, Is.EqualTo(Path.GetFullPath(spherePath)));
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

        private static string ResolvePackageFixture(string fileName)
        {
            return MmdTestFixtures.FixtureAssetPath(fileName);
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

        private static void DestroyInstance(MmdUnityModelInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.Root != null)
            {
                Object.DestroyImmediate(instance.Root);
            }

            if (instance.Mesh != null)
            {
                Object.DestroyImmediate(instance.Mesh);
            }

            if (instance.Materials == null)
            {
                return;
            }

            foreach (Material material in instance.Materials)
            {
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }

            foreach (Texture2D texture in instance.OwnedTextures)
            {
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }
        [Test]
        public void D1_TryResolveMmdPmxAssetFromMainGameObjectReturnsMetadataForImportedPmxGo()
        {
            // D1: When the .pmx main object is a GameObject, TryResolveMmdPmxAssetFromMainGameObject
            // must return the metadata MmdPmxAsset sub-asset.
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

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
        public void D1_LoadSelectedPmxAssetMenuValidationAcceptsGameObjectMain()
        {
            CopyFixtureToAssetDatabase("test_1bone_cube.pmx", TempPmxPath);

            GameObject? mainGo = AssetDatabase.LoadAssetAtPath<GameObject>(TempPmxPath);
            Assert.That(mainGo, Is.Not.Null, "precondition: imported .pmx must have a GameObject main object");

            Object? previousSelection = Selection.activeObject;
            try
            {
                Selection.activeObject = mainGo;

                Assert.That(MmdEditorPmxLoader.ValidateLoadSelectedPmxAssetIntoSceneFromMenu(), Is.True,
                    "selected .pmx GameObject main object must be accepted by the PMX load menu compatibility layer");
            }
            finally
            {
                Selection.activeObject = previousSelection;
            }
        }
    }
}
