#nullable enable

using System;
using System.IO;
using Mmd.Editor;
using Mmd.Parser;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mmd.Tests
{
    public sealed class MmdPbrTextureConventionScannerTests
    {
        private const string TempDirectory = "Assets/__MmdPbrTextureConventionScannerTests";

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [SetUp]
        public void SetUp()
        {
            DeleteTempDirectory();
            Directory.CreateDirectory(ToAbsolutePath(TempDirectory));
        }

        [TearDown]
        public void TearDown()
        {
            DeleteTempDirectory();
        }

        [Test]
        public void BuildMaterialOverrides_DetectsDiffuseBasenamePbrTextureSet()
        {
            string textureDirectory = TempDirectory + "/textures";
            Directory.CreateDirectory(ToAbsolutePath(textureDirectory));
            WritePng(textureDirectory + "/body.png", Color.white);
            WritePng(textureDirectory + "/body_normal.png", Color.magenta);
            WritePng(textureDirectory + "/body_metallic.png", Color.red);
            WritePng(textureDirectory + "/body_roughness.png", Color.white);
            WritePng(textureDirectory + "/body_ao.png", Color.blue);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            MmdModelDefinition model = CreateSingleMaterialModel("Body", "textures/body.png");
            string pmxAssetPath = TempDirectory + "/model.pmx";

            MmdMaterialOverrideEntry[] entries =
                MmdPbrTextureConventionScanner.BuildMaterialOverrides(model, pmxAssetPath);

            Assert.That(entries, Has.Length.EqualTo(1));
            Assert.That(entries[0].sourceKind, Is.EqualTo(MmdMaterialOverrideSourceKind.TextureScan));
            Assert.That(entries[0].effectType, Is.EqualTo("pbr-texture-scan"));
            Assert.That(entries[0].materialIndex, Is.EqualTo(0));
            Assert.That(entries[0].materialName, Is.EqualTo("Body"));
            Assert.That(entries[0].hasNormalMap, Is.True);
            Assert.That(entries[0].normalMap, Is.SameAs(LoadTexture(textureDirectory + "/body_normal.png")));
            Assert.That(entries[0].hasMetallicMap, Is.True);
            Assert.That(entries[0].metallicMap, Is.SameAs(LoadTexture(textureDirectory + "/body_metallic.png")));
            Assert.That(entries[0].metallicMapIncludesSmoothness, Is.True);
            Assert.That(entries[0].hasRoughnessMap, Is.True);
            Assert.That(entries[0].roughnessMap, Is.SameAs(LoadTexture(textureDirectory + "/body_roughness.png")));
            Assert.That(entries[0].hasSmoothness, Is.True);
            Assert.That(entries[0].smoothness, Is.EqualTo(0.0f).Within(1e-6f));
            Assert.That(entries[0].hasOcclusionMap, Is.True);
            Assert.That(entries[0].occlusionMap, Is.SameAs(LoadTexture(textureDirectory + "/body_ao.png")));
            Assert.That(entries[0].hasOcclusionStrength, Is.True);
            Assert.That(entries[0].occlusionStrength, Is.EqualTo(1.0f).Within(1e-6f));
        }

        [Test]
        public void BuildMaterialOverrides_FallsBackToMaterialNameWhenDiffuseIsBlank()
        {
            WritePng(TempDirectory + "/Hair_N.png", Color.magenta);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            MmdModelDefinition model = CreateSingleMaterialModel("Hair", string.Empty);

            MmdMaterialOverrideEntry[] entries =
                MmdPbrTextureConventionScanner.BuildMaterialOverrides(model, TempDirectory + "/model.pmx");

            Assert.That(entries, Has.Length.EqualTo(1));
            Assert.That(entries[0].materialName, Is.EqualTo("Hair"));
            Assert.That(entries[0].hasNormalMap, Is.True);
            Assert.That(entries[0].normalMap, Is.SameAs(LoadTexture(TempDirectory + "/Hair_N.png")));
        }

        [Test]
        public void EnumerateConventionAssetPathCandidates_IncludesMissingSidecarTextures()
        {
            MmdModelDefinition model = CreateSingleMaterialModel("Body", "textures/body.png");

            string[] candidates =
                MmdPbrTextureConventionScanner.EnumerateConventionAssetPathCandidates(
                    model,
                    TempDirectory + "/model.pmx");

            Assert.That(candidates, Does.Contain(TempDirectory + "/textures/body_normal.png"));
            Assert.That(candidates, Does.Contain(TempDirectory + "/textures/body_roughness.png"));
            Assert.That(candidates, Does.Contain(TempDirectory + "/textures/body_metallic.png"));
            Assert.That(candidates, Does.Contain(TempDirectory + "/textures/body_ao.png"));
        }

        [Test]
        public void BuildMaterialOverrides_UsesFallbackSmoothnessWhenMetallicMapHasNoRoughnessMap()
        {
            WritePng(TempDirectory + "/Body_metallic.png", Color.red);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            MmdModelDefinition model = CreateSingleMaterialModel("Body", string.Empty);

            MmdMaterialOverrideEntry[] entries =
                MmdPbrTextureConventionScanner.BuildMaterialOverrides(model, TempDirectory + "/model.pmx");

            Assert.That(entries, Has.Length.EqualTo(1));
            Assert.That(entries[0].hasMetallicMap, Is.True);
            Assert.That(entries[0].metallicMap, Is.SameAs(LoadTexture(TempDirectory + "/Body_metallic.png")));
            Assert.That(entries[0].metallicMapIncludesSmoothness, Is.True);
            Assert.That(entries[0].hasSmoothness, Is.True);
            Assert.That(entries[0].smoothness, Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void BuildMaterialOverrides_SkipsMaterialsWithoutConventionMatches()
        {
            MmdModelDefinition model = CreateSingleMaterialModel("Body", "textures/body.png");

            MmdMaterialOverrideEntry[] entries =
                MmdPbrTextureConventionScanner.BuildMaterialOverrides(model, TempDirectory + "/model.pmx");

            Assert.That(entries, Is.Empty);
        }

        private static MmdModelDefinition CreateSingleMaterialModel(string materialName, string texture)
        {
            var model = new MmdModelDefinition();
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = materialName,
                texture = texture,
                vertexCount = 3
            });
            return model;
        }

        private static Texture2D LoadTexture(string assetPath)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            Assert.That(texture, Is.Not.Null);
            return texture;
        }

        private static void WritePng(string assetPath, Color color)
        {
            string absolutePath = ToAbsolutePath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixels(new[] { color, color, color, color });
                texture.Apply();
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static void DeleteTempDirectory()
        {
            AssetDatabase.DeleteAsset(TempDirectory);
            string absolutePath = ToAbsolutePath(TempDirectory);
            if (Directory.Exists(absolutePath))
            {
                Directory.Delete(absolutePath, recursive: true);
            }

            string metaPath = absolutePath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            AssetDatabase.Refresh();
        }
    }
}
