#nullable enable

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mmd.Rendering;

namespace Mmd.Tests
{
    /// <summary>
    /// The morph descriptor types used to be nested in <c>MmdMorphDescriptorBuilder</c>.
    /// [SerializeReference] data saved under the nested names must still load via [MovedFrom].
    /// </summary>
    public sealed class MmdMorphDescriptorMovedFromCompatibilityTests
    {
        private const string TempDirectory = "Assets/__MmdMorphDescriptorMovedFromTests";
        private const string AssetPath = TempDirectory + "/holder.asset";
        private const string LegacyOuterClassName = "MmdMorphDescriptorBuilder";

        private static readonly string[] MovedTypeNames =
        {
            nameof(MmdFlipMorphDescriptor),
            nameof(MmdFlipMorphOffsetDescriptor),
            nameof(MmdMaterialMorphDescriptor),
            nameof(MmdMaterialMorphOffsetDescriptor),
            nameof(MmdUvMorphDescriptor),
            nameof(MmdUvMorphOffsetDescriptor),
        };

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

        [Test]
        public void SerializeReferenceSavedUnderNestedNamesLoadsAsTopLevelTypes()
        {
            try
            {
                string originalYaml = SaveHolderAsset();
                TestContext.WriteLine("Saved YAML:\n" + originalYaml);

                string legacyYaml = originalYaml;
                foreach (string typeName in MovedTypeNames)
                {
                    string current = $"class: {typeName}, ns: Mmd.Rendering, asm: Mmd.Runtime";
                    Assert.That(CountOccurrences(legacyYaml, current), Is.EqualTo(1), current);
                    legacyYaml = legacyYaml.Replace(
                        current,
                        $"class: {LegacyOuterClassName}/{typeName}, ns: Mmd.Rendering, asm: Mmd.Runtime");
                }

                TestContext.WriteLine("Legacy YAML:\n" + legacyYaml);
                MmdMovedFromSerializeReferenceHolder holder = ReimportWithYaml(legacyYaml);

                Assert.That(SerializationUtility.HasManagedReferencesWithMissingTypes(holder), Is.False);
                Assert.That(holder.values, Has.Count.EqualTo(MovedTypeNames.Length));

                var flip = holder.values[0] as MmdFlipMorphDescriptor;
                Assert.That(flip, Is.Not.Null);
                Assert.That(flip!.morphIndex, Is.EqualTo(11));
                Assert.That(flip.morphName, Is.EqualTo("flip"));
                Assert.That(flip.flipOffsetCount, Is.EqualTo(1));
                Assert.That(flip.offsets, Has.Count.EqualTo(1));
                Assert.That(flip.offsets[0].targetMorphName, Is.EqualTo("target"));

                var flipOffset = holder.values[1] as MmdFlipMorphOffsetDescriptor;
                Assert.That(flipOffset, Is.Not.Null);
                Assert.That(flipOffset!.targetMorphIndex, Is.EqualTo(3));
                Assert.That(flipOffset.weight, Is.EqualTo(0.5f));

                var material = holder.values[2] as MmdMaterialMorphDescriptor;
                Assert.That(material, Is.Not.Null);
                Assert.That(material!.morphIndex, Is.EqualTo(12));
                Assert.That(material.morphName, Is.EqualTo("material"));
                Assert.That(material.offsets, Has.Count.EqualTo(1));
                Assert.That(material.offsets[0].materialIndex, Is.EqualTo(2));

                var materialOffset = holder.values[3] as MmdMaterialMorphOffsetDescriptor;
                Assert.That(materialOffset, Is.Not.Null);
                Assert.That(materialOffset!.materialIndex, Is.EqualTo(4));
                Assert.That(materialOffset.operation, Is.EqualTo("add"));

                var uv = holder.values[4] as MmdUvMorphDescriptor;
                Assert.That(uv, Is.Not.Null);
                Assert.That(uv!.morphIndex, Is.EqualTo(13));
                Assert.That(uv.morphName, Is.EqualTo("uv"));
                Assert.That(uv.morphType, Is.EqualTo("uv"));
                Assert.That(uv.offsets, Has.Count.EqualTo(1));
                Assert.That(uv.offsets[0].vertexIndex, Is.EqualTo(7));

                var uvOffset = holder.values[5] as MmdUvMorphOffsetDescriptor;
                Assert.That(uvOffset, Is.Not.Null);
                Assert.That(uvOffset!.vertexIndex, Is.EqualTo(9));
                Assert.That(uvOffset.positionDelta, Is.EqualTo(new[] { 0.25f, -0.5f }));
            }
            finally
            {
                DeleteTempDirectory();
            }
        }

        private static string SaveHolderAsset()
        {
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(TempDirectory));

            var holder = ScriptableObject.CreateInstance<MmdMovedFromSerializeReferenceHolder>();
            holder.values.Add(new MmdFlipMorphDescriptor
            {
                morphIndex = 11,
                morphName = "flip",
                flipOffsetCount = 1,
                offsets = { new MmdFlipMorphOffsetDescriptor { targetMorphIndex = 1, targetMorphName = "target" } },
            });
            holder.values.Add(new MmdFlipMorphOffsetDescriptor { targetMorphIndex = 3, weight = 0.5f });
            holder.values.Add(new MmdMaterialMorphDescriptor
            {
                morphIndex = 12,
                morphName = "material",
                offsets = { new MmdMaterialMorphOffsetDescriptor { materialIndex = 2 } },
            });
            holder.values.Add(new MmdMaterialMorphOffsetDescriptor { materialIndex = 4, operation = "add" });
            holder.values.Add(new MmdUvMorphDescriptor
            {
                morphIndex = 13,
                morphName = "uv",
                morphType = "uv",
                uvOffsetCount = 1,
                offsets = { new MmdUvMorphOffsetDescriptor { vertexIndex = 7 } },
            });
            holder.values.Add(new MmdUvMorphOffsetDescriptor
            {
                vertexIndex = 9,
                positionDelta = new[] { 0.25f, -0.5f },
            });

            AssetDatabase.CreateAsset(holder, AssetPath);
            AssetDatabase.SaveAssets();
            return File.ReadAllText(FullPath(AssetPath));
        }

        private static MmdMovedFromSerializeReferenceHolder ReimportWithYaml(string yaml)
        {
            File.WriteAllText(FullPath(AssetPath), yaml);
            AssetDatabase.ImportAsset(
                AssetPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            MmdMovedFromSerializeReferenceHolder holder =
                AssetDatabase.LoadAssetAtPath<MmdMovedFromSerializeReferenceHolder>(AssetPath);
            Assert.That(holder, Is.Not.Null);
            return holder;
        }

        private static int CountOccurrences(string text, string value)
        {
            return Regex.Matches(text, Regex.Escape(value)).Count;
        }

        private static string FullPath(string assetPath)
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath)!, assetPath);
        }

        private static void DeleteTempDirectory()
        {
            AssetDatabase.DeleteAsset(TempDirectory);
            string fullPath = FullPath(TempDirectory);
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }

            if (File.Exists(fullPath + ".meta"))
            {
                File.Delete(fullPath + ".meta");
            }
        }
    }
}
