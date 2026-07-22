#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Editor;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdPmxMaterialExtractorTests
    {
        private static readonly string TempDirectory =
            "Assets/__MmdPmxMaterialExtractorTests_" + Guid.NewGuid().ToString("N");
        private static readonly string MaterialSourceDirectory = TempDirectory + "/Sources";
        private static readonly string MaterialOutputDirectory = TempDirectory + "/Extracted";
        private static readonly string PmxPath = TempDirectory + "/model.pmx";

        [SetUp]
        public void SetUp()
        {
            DeleteTempDirectory();
            string directoryName = TempDirectory.Substring("Assets/".Length);
            AssetDatabase.CreateFolder("Assets", directoryName);
            AssetDatabase.CreateFolder(TempDirectory, "Sources");
            AssetDatabase.CreateFolder(TempDirectory, "Extracted");
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteTempDirectory();
        }

        [Test]
        public void ExtractsMaterialsInSlotOrderWithDeterministicNamesAndTextureReferences()
        {
            Material source0 = CreateMaterialAssetAtPath(MaterialSourceDirectory + "/Body0.mat", "Body");
            Material source1 = CreateMaterialAssetAtPath(MaterialSourceDirectory + "/Body1.mat", "Body");
            Texture2D texture = CreateTextureAsset();
            SetMainTexture(source0, texture);
            EditorUtility.SetDirty(source0);
            AssetDatabase.SaveAssets();

            MmdPmxMaterialExtractor.Result result = MmdPmxMaterialExtractor.TryExtract(
                new[] { source0, source1 },
                Array.Empty<Material>(),
                MaterialOutputDirectory,
                out Material[] remaps);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.CreatedAssetPaths, Is.EqualTo(new[]
            {
                MaterialOutputDirectory + "/Body.mat",
                MaterialOutputDirectory + "/Body_1.mat"
            }));
            Assert.That(remaps, Has.Length.EqualTo(2));
            Assert.That(remaps[0], Is.Not.SameAs(source0));
            Assert.That(remaps[1], Is.Not.SameAs(source1));
            Texture? extractedTexture = GetMainTexture(remaps[0]);
            Assert.That(extractedTexture, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(extractedTexture!),
                Is.EqualTo(AssetDatabase.GetAssetPath(texture)),
                "extraction must preserve project texture references instead of creating texture assets");
        }

        [Test]
        public void PreservesExistingRemapsAndExtractsOnlyUnmappedSlots()
        {
            Material source0 = CreateMaterialAssetAtPath(MaterialSourceDirectory + "/Body.mat", "Body");
            Material source1 = CreateMaterialAssetAtPath(MaterialSourceDirectory + "/Hair.mat", "Hair");
            Material existing = CreateMaterialAssetAtPath(MaterialSourceDirectory + "/Existing.mat", "Existing");

            MmdPmxMaterialExtractor.Result result = MmdPmxMaterialExtractor.TryExtract(
                new[] { source0, source1 },
                new[] { existing },
                MaterialOutputDirectory,
                out Material[] remaps);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.CreatedAssetPaths, Is.EqualTo(new[]
            {
                MaterialOutputDirectory + "/Hair.mat"
            }));
            Assert.That(remaps[0], Is.SameAs(existing));
            Assert.That(remaps[1], Is.Not.Null.And.Not.SameAs(source1));
        }

        [Test]
        public void ExistingPathIsNotOverwrittenAndInvalidNamesAreSafe()
        {
            Material existing = CreateMaterialAssetAtPath(MaterialOutputDirectory + "/Body.mat", "Existing");
            SetMainColor(existing, Color.red);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();

            Material source = CreateMaterialAssetAtPath(MaterialSourceDirectory + "/InvalidName.mat", "Body/Face");
            MmdPmxMaterialExtractor.Result result = MmdPmxMaterialExtractor.TryExtract(
                new[] { source },
                Array.Empty<Material>(),
                MaterialOutputDirectory,
                out Material[] remaps);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.CreatedAssetPaths, Is.EqualTo(new[]
            {
                MaterialOutputDirectory + "/Body_Face.mat"
            }));
            Material? existingAfter = AssetDatabase.LoadAssetAtPath<Material>(MaterialOutputDirectory + "/Body.mat");
            Assert.That(existingAfter, Is.Not.Null);
            Assert.That(GetMainColor(existingAfter!), Is.EqualTo(Color.red));
            Assert.That(AssetDatabase.GetAssetPath(remaps[0]), Is.EqualTo(result.CreatedAssetPaths[0]));
        }

        [Test]
        public void ExistingDirectoryPathIsTreatedAsCollision()
        {
            string directoryCollisionPath = MaterialOutputDirectory + "/Body.mat";
            string folderGuid = AssetDatabase.CreateFolder(MaterialOutputDirectory, "Body.mat");
            Assert.That(folderGuid, Is.Not.Null.And.Not.Empty);
            Assert.That(AssetDatabase.IsValidFolder(directoryCollisionPath), Is.True);

            Material source = CreateMaterialAssetAtPath(MaterialSourceDirectory + "/Body.mat", "Body");
            MmdPmxMaterialExtractor.Result result = MmdPmxMaterialExtractor.TryExtract(
                new[] { source },
                Array.Empty<Material>(),
                MaterialOutputDirectory,
                out Material[] remaps);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.CreatedAssetPaths, Is.EqualTo(new[]
            {
                MaterialOutputDirectory + "/Body_1.mat"
            }));
            Assert.That(AssetDatabase.IsValidFolder(directoryCollisionPath), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(remaps[0]), Is.EqualTo(result.CreatedAssetPaths[0]));
        }

        [Test]
        public void ReservedWindowsDeviceNamesArePrefixedDeterministically()
        {
            Material source = CreateMaterialAssetAtPath(MaterialSourceDirectory + "/Reserved.mat", "cOn.foo");

            MmdPmxMaterialExtractor.Result result = MmdPmxMaterialExtractor.TryExtract(
                new[] { source },
                Array.Empty<Material>(),
                MaterialOutputDirectory,
                out Material[] remaps);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.CreatedAssetPaths, Is.EqualTo(new[]
            {
                MaterialOutputDirectory + "/_cOn.foo.mat"
            }));
            Assert.That(AssetDatabase.GetAssetPath(remaps[0]), Is.EqualTo(result.CreatedAssetPaths[0]));
        }

        [Test]
        public void InvalidDestinationLeavesNoAssetsOrRemaps()
        {
            Material source = CreateMaterialAssetAtPath(MaterialSourceDirectory + "/Body.mat", "Body");

            MmdPmxMaterialExtractor.Result result = MmdPmxMaterialExtractor.TryExtract(
                new[] { source },
                Array.Empty<Material>(),
                TempDirectory + "/MissingFolder",
                out Material[] remaps);

            Assert.That(result.Success, Is.False);
            Assert.That(result.CreatedAssetPaths, Is.Empty);
            Assert.That(remaps, Is.Empty);
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(MaterialOutputDirectory + "/Body.mat"), Is.Null);
        }

        [Test]
        public void ExtractedRemapSurvivesPmxReimportAndEditedMaterialIsNotOverwritten()
        {
            string source = MmdTestFixtures.FixtureAssetPath("test_1bone_cube.pmx");
            Directory.CreateDirectory(Path.Combine(ProjectRoot, TempDirectory));
            File.Copy(source, Path.Combine(ProjectRoot, PmxPath), overwrite: true);
            AssetDatabase.ImportAsset(PmxPath, ImportAssetOptions.ForceUpdate);

            MmdPmxAsset? before = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(PmxPath);
            MmdPmxScriptedImporter? importer = AssetImporter.GetAtPath(PmxPath) as MmdPmxScriptedImporter;
            Assert.That(before, Is.Not.Null);
            Assert.That(importer, Is.Not.Null);
            Assert.That(before!.ImportedMaterials, Is.Not.Null.And.Not.Empty);

            MmdPmxMaterialExtractor.Result extraction = MmdPmxMaterialExtractor.TryExtractToSiblingMaterialsFolder(
                PmxPath,
                before.ImportedMaterials,
                importer!.MaterialRemaps,
                out Material[] remaps);
            Assert.That(extraction.Success, Is.True, extraction.Message);
            string materialsFolderPath = TempDirectory + "/Materials";
            Assert.That(AssetDatabase.IsValidFolder(materialsFolderPath), Is.True);
            string materialsFolderGuid = AssetDatabase.AssetPathToGUID(materialsFolderPath);
            Assert.That(extraction.CreatedAssetPaths[0], Does.StartWith(materialsFolderPath + "/"));
            SetImporterRemapsAndReimport(importer, remaps);

            MmdPmxAsset after = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(PmxPath);
            Material? extracted = AssetDatabase.LoadAssetAtPath<Material>(extraction.CreatedAssetPaths[0]);
            Assert.That(after, Is.Not.Null);
            Assert.That(extracted, Is.Not.Null);
            Assert.That(after!.MaterialRemaps[0], Is.SameAs(extracted));

            MmdPmxMaterialExtractor.Result reuse = MmdPmxMaterialExtractor.TryExtractToSiblingMaterialsFolder(
                PmxPath,
                after.ImportedMaterials,
                after.MaterialRemaps,
                out Material[] reusedRemaps);
            Assert.That(reuse.Success, Is.True, reuse.Message);
            Assert.That(reuse.CreatedAssetPaths, Is.Empty,
                "reusing an existing sibling Materials folder must not duplicate mapped materials");
            Assert.That(reusedRemaps[0], Is.SameAs(after.MaterialRemaps[0]));
            Assert.That(AssetDatabase.AssetPathToGUID(materialsFolderPath), Is.EqualTo(materialsFolderGuid));

            MmdUnityModelInstance? sceneInstance = null;
            try
            {
                sceneInstance = MmdEditorPmxLoader.LoadPmxIntoScene(after);
                Assert.That(sceneInstance.SkinnedMeshRenderer, Is.Not.Null);
                Assert.That(sceneInstance.SkinnedMeshRenderer!.sharedMaterials[0], Is.SameAs(extracted),
                    "scene renderer must resolve the importer remap to the extracted Material asset");
            }
            finally
            {
                if (sceneInstance?.Root != null)
                {
                    UnityEngine.Object.DestroyImmediate(sceneInstance.Root);
                }
            }

            SetMainColor(extracted!, Color.magenta);
            EditorUtility.SetDirty(extracted);
            AssetDatabase.SaveAssets();
            importer = AssetImporter.GetAtPath(PmxPath) as MmdPmxScriptedImporter;
            Assert.That(importer, Is.Not.Null);
            importer!.SaveAndReimport();

            Material? extractedAfterReimport = AssetDatabase.LoadAssetAtPath<Material>(extraction.CreatedAssetPaths[0]);
            Assert.That(GetMainColor(extractedAfterReimport!), Is.EqualTo(Color.magenta));
            MmdPmxAsset finalAsset = AssetDatabase.LoadAssetAtPath<MmdPmxAsset>(PmxPath);
            Assert.That(finalAsset.MaterialRemaps[0], Is.SameAs(extractedAfterReimport));
        }

        private static void SetImporterRemapsAndReimport(MmdPmxScriptedImporter importer, Material[] remaps)
        {
            SerializedObject serializedImporter = new SerializedObject(importer);
            SerializedProperty property = serializedImporter.FindProperty("materialRemaps");
            Assert.That(property, Is.Not.Null);
            property.arraySize = remaps.Length;
            for (int i = 0; i < remaps.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = remaps[i];
            }

            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
        }

        private static Material CreateMaterialAssetAtPath(string path, string name)
        {
            Shader? shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Assume.That(shader, Is.Not.Null, "test shader unavailable");
            Material material = new Material(shader!) { name = name };
            AssetDatabase.CreateAsset(material, path);
            material.name = name;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Texture2D CreateTextureAsset()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false)
            {
                name = "SharedTexture"
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, MaterialSourceDirectory + "/SharedTexture.asset");
            AssetDatabase.SaveAssets();
            return texture;
        }

        private static void SetMainTexture(Material material, Texture2D texture)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }
            else if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
        }

        private static Texture? GetMainTexture(Material material)
        {
            if (material.HasProperty("_BaseMap"))
            {
                return material.GetTexture("_BaseMap");
            }

            return material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        }

        private static void SetMainColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static Color GetMainColor(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            return material.HasProperty("_Color") ? material.GetColor("_Color") : Color.clear;
        }

        private static void DeleteTempDirectory()
        {
            AssetDatabase.DeleteAsset(TempDirectory);
            string fullPath = Path.Combine(ProjectRoot, TempDirectory);
            if (Directory.Exists(fullPath))
            {
                foreach (string path in Directory.EnumerateFileSystemEntries(fullPath, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }

                Directory.Delete(fullPath, recursive: true);
            }

            if (File.Exists(fullPath + ".meta"))
            {
                File.SetAttributes(fullPath + ".meta", FileAttributes.Normal);
                File.Delete(fullPath + ".meta");
            }

            AssetDatabase.Refresh();
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }
}
