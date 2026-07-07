#nullable enable

using System;
using System.IO;
using Mmd.Editor;
using Mmd.Mme;
using Mmd.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mmd.Tests
{
    public sealed class MmdMmeFxMaterialOverrideBuilderTests
    {
        private const string TempDirectory = "Assets/__MmdMmeFxMaterialOverrideBuilderTests";

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string ToAbsolutePath(string relativeAssetPath)
        {
            return Path.GetFullPath(Path.Combine(ProjectRoot, relativeAssetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

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
        public void BuildMaterialOverrides_MatchesBySourceBasenameUsingOrdinal()
        {
            MmdMaterialDescriptor[] materials =
            {
                new MmdMaterialDescriptor { materialIndex = 0, name = "Head" },
                new MmdMaterialDescriptor { materialIndex = 1, name = "body" }
            };
            MmeFxEffectDescriptor[] effectDescriptors =
            {
                new MmeFxEffectDescriptor
                {
                    sourcePath = Path.Combine(ProjectRoot, TempDirectory, "body.fx")
                }
            };

            MmdMaterialOverrideEntry[] entries = MmdMmeFxMaterialOverrideBuilder.BuildMaterialOverrides(materials, effectDescriptors);

            Assert.That(entries, Has.Length.EqualTo(1));
            Assert.That(entries[0].enabled, Is.True);
            Assert.That(entries[0].materialIndex, Is.EqualTo(1));
            Assert.That(entries[0].materialName, Is.EqualTo("body"));
            Assert.That(entries[0].matchMode, Is.EqualTo(MmdMaterialOverrideMatchMode.IndexThenName));
            Assert.That(entries[0].sourceKind, Is.EqualTo(MmdMaterialOverrideSourceKind.MmeFx));
            Assert.That(entries[0].sourcePath, Is.EqualTo(effectDescriptors[0].sourcePath));
        }

        [Test]
        public void BuildMaterialOverrides_SkipsUnmatchedDescriptor()
        {
            MmdMaterialDescriptor[] materials =
            {
                new MmdMaterialDescriptor { materialIndex = 0, name = "Eyes" }
            };
            MmeFxEffectDescriptor[] effectDescriptors =
            {
                new MmeFxEffectDescriptor
                {
                    sourcePath = Path.Combine(ProjectRoot, TempDirectory, "hair.fx")
                }
            };

            MmdMaterialOverrideEntry[] entries = MmdMmeFxMaterialOverrideBuilder.BuildMaterialOverrides(materials, effectDescriptors);

            Assert.That(entries, Is.Empty);
        }

        [Test]
        public void BuildMaterialOverrides_ResolvesNextToFxNormalMap()
        {
            string effectDirectory = Path.Combine(ToAbsolutePath(TempDirectory), "normal_map");
            Directory.CreateDirectory(effectDirectory);
            string normalMapAssetPath = $"Assets/__MmdMmeFxMaterialOverrideBuilderTests/normal_map/normal.png";
            string normalMapAbsolutePath = ToAbsolutePath(normalMapAssetPath);
            string effectSourcePath = Path.Combine(effectDirectory, "body.fx");

            WriteNormalMapTexture(normalMapAbsolutePath);
            AssetDatabase.ImportAsset(normalMapAssetPath, ImportAssetOptions.ForceSynchronousImport);
            File.WriteAllText(effectSourcePath, "mock");
            AssetDatabase.Refresh();

            MmdMaterialDescriptor[] materials =
            {
                new MmdMaterialDescriptor { materialIndex = 0, name = "body" }
            };
            MmeFxEffectDescriptor[] effectDescriptors =
            {
                new MmeFxEffectDescriptor
                {
                    sourcePath = effectSourcePath,
                    normalMapTexture = "normal.png",
                    useNormalMap = true,
                    normalMapResolution = 2.5f
                }
            };

            Texture2D? expectedNormalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalMapAssetPath);
            Assert.That(expectedNormalMap, Is.Not.Null);

            MmdMaterialOverrideEntry[] entries = MmdMmeFxMaterialOverrideBuilder.BuildMaterialOverrides(materials, effectDescriptors);

            Assert.That(entries, Has.Length.EqualTo(1));
            Assert.That(entries[0].hasNormalMap, Is.True);
            Assert.That(entries[0].normalMap, Is.SameAs(expectedNormalMap));
            Assert.That(entries[0].hasNormalScale, Is.False);
            Assert.That(entries[0].normalScale, Is.EqualTo(1.0f).Within(1e-6f));
        }

        [Test]
        public void BuildMaterialOverrides_SkipsAlternativeFullNormalMapWhenDisabled()
        {
            string effectDirectory = Path.Combine(ToAbsolutePath(TempDirectory), "disabled_normal_map");
            Directory.CreateDirectory(effectDirectory);
            string normalMapAssetPath = $"Assets/__MmdMmeFxMaterialOverrideBuilderTests/disabled_normal_map/normal.png";
            string effectSourcePath = Path.Combine(effectDirectory, "body.fx");

            WriteNormalMapTexture(ToAbsolutePath(normalMapAssetPath));
            AssetDatabase.ImportAsset(normalMapAssetPath, ImportAssetOptions.ForceSynchronousImport);
            File.WriteAllText(effectSourcePath, "mock");
            AssetDatabase.Refresh();

            MmdMaterialDescriptor[] materials =
            {
                new MmdMaterialDescriptor { materialIndex = 0, name = "body" }
            };
            MmeFxEffectDescriptor[] effectDescriptors =
            {
                new MmeFxEffectDescriptor
                {
                    sourcePath = effectSourcePath,
                    normalMapTexture = "normal.png",
                    useNormalMap = false
                }
            };

            MmdMaterialOverrideEntry[] entries = MmdMmeFxMaterialOverrideBuilder.BuildMaterialOverrides(materials, effectDescriptors);

            Assert.That(entries, Has.Length.EqualTo(1));
            Assert.That(entries[0].hasNormalMap, Is.False);
            Assert.That(entries[0].normalMap, Is.Null);
        }

        [Test]
        public void BuildMaterialOverrides_CopiesParsedPbrScalarsWithoutNormalMap()
        {
            MmdMaterialDescriptor[] materials =
            {
                new MmdMaterialDescriptor { materialIndex = 0, name = "body" }
            };
            MmeFxEffectDescriptor[] effectDescriptors =
            {
                new MmeFxEffectDescriptor
                {
                    sourcePath = Path.Combine(ProjectRoot, TempDirectory, "body.fx"),
                    effectType = "ray-mmd",
                    metalness = 0.25f,
                    smoothness = 0.75f
                }
            };

            MmdMaterialOverrideEntry[] entries = MmdMmeFxMaterialOverrideBuilder.BuildMaterialOverrides(materials, effectDescriptors);

            Assert.That(entries, Has.Length.EqualTo(1));
            Assert.That(entries[0].effectType, Is.EqualTo("ray-mmd"));
            Assert.That(entries[0].hasNormalMap, Is.False);
            Assert.That(entries[0].hasMetallic, Is.True);
            Assert.That(entries[0].metallic, Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(entries[0].hasSmoothness, Is.True);
            Assert.That(entries[0].smoothness, Is.EqualTo(0.75f).Within(1e-6f));
        }

        [Test]
        public void BuildMaterialOverrides_IgnoresEmdAssociationForNow()
        {
            MmdMaterialDescriptor[] materials =
            {
                new MmdMaterialDescriptor { materialIndex = 0, name = "Body" }
            };
            MmeFxEffectDescriptor[] effectDescriptors =
            {
                new MmeFxEffectDescriptor
                {
                    sourcePath = Path.Combine(ProjectRoot, TempDirectory, "Body.emd")
                }
            };

            MmdMaterialOverrideEntry[] entries = MmdMmeFxMaterialOverrideBuilder.BuildMaterialOverrides(materials, effectDescriptors);

            Assert.That(entries, Is.Empty);
        }

        private static void WriteNormalMapTexture(string absolutePngPath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixels(new[]
                {
                    Color.magenta,
                    Color.magenta,
                    Color.magenta,
                    Color.magenta
                });
                texture.Apply();
                File.WriteAllBytes(absolutePngPath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void DeleteTempDirectory()
        {
            AssetDatabase.DeleteAsset(TempDirectory);
            string absolutePath = ToAbsolutePath(TempDirectory);
            if (Directory.Exists(absolutePath))
            {
                foreach (string item in Directory.EnumerateFileSystemEntries(absolutePath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(item, FileAttributes.Normal);
                    }
                    catch (Exception)
                    {
                        // Ignore files that disappear while deleting.
                    }
                }

                Directory.Delete(absolutePath, recursive: true);
            }

            string metaPath = absolutePath + ".meta";
            if (File.Exists(metaPath))
            {
                File.SetAttributes(metaPath, FileAttributes.Normal);
                File.Delete(metaPath);
            }

            AssetDatabase.Refresh();
        }
    }
}
