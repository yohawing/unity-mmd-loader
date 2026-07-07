#nullable enable

using System;
using NUnit.Framework;
using UnityEngine;
using Mmd.Parser;
using Mmd.Rendering;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed class MmdMaterialOverrideAssetTests
    {
        [Test]
        public void CreateStaticModelWithNullOrEmptyMaterialOverridePreservesUrpLitMaterialProperties()
        {
            MmdModelDefinition model = CreateTwoMaterialModel();
            MmdMaterialOverrideAsset? emptyOverride = null;
            MmdUnityModelInstance? baseline = null;
            MmdUnityModelInstance? nullOverride = null;
            MmdUnityModelInstance? emptyOverrideInstance = null;

            try
            {
                emptyOverride = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
                emptyOverride.entries = Array.Empty<MmdMaterialOverrideEntry>();

                baseline = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.UrpLit);
                nullOverride = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.UrpLit,
                    materialOverride: null);
                emptyOverrideInstance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.UrpLit,
                    materialOverride: emptyOverride);

                AssertMaterialFloatEqual(
                    baseline.Materials[0],
                    nullOverride.Materials[0],
                    MmdMaterialPropertyNames.Metallic);
                AssertMaterialFloatEqual(
                    baseline.Materials[0],
                    emptyOverrideInstance.Materials[0],
                    MmdMaterialPropertyNames.Metallic);
                AssertMaterialFloatEqual(
                    baseline.Materials[1],
                    nullOverride.Materials[1],
                    MmdMaterialPropertyNames.Smoothness);
                AssertMaterialFloatEqual(
                    baseline.Materials[1],
                    emptyOverrideInstance.Materials[1],
                    MmdMaterialPropertyNames.Smoothness);
            }
            finally
            {
                DestroyInstance(baseline);
                DestroyInstance(nullOverride);
                DestroyInstance(emptyOverrideInstance);
                if (emptyOverride != null)
                {
                    UnityEngine.Object.DestroyImmediate(emptyOverride);
                }
            }
        }

        [Test]
        public void UrpLitMaterialOverrideByIndexChangesOnlyTargetMaterialSlot()
        {
            MmdModelDefinition model = CreateTwoMaterialModel();
            MmdMaterialOverrideAsset? overrideAsset = null;
            MmdUnityModelInstance? instance = null;

            try
            {
                overrideAsset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
                overrideAsset.entries = new[]
                {
                    new MmdMaterialOverrideEntry
                    {
                        materialIndex = 1,
                        hasMetallic = true,
                        metallic = 0.75f,
                        hasSmoothness = true,
                        smoothness = 0.2f
                    }
                };

                instance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.UrpLit,
                    materialOverride: overrideAsset);

                Assert.That(ReadMaterialFloat(instance.Materials[0], MmdMaterialPropertyNames.Metallic), Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[0], MmdMaterialPropertyNames.Smoothness), Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[1], MmdMaterialPropertyNames.Metallic), Is.EqualTo(0.75f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[1], MmdMaterialPropertyNames.Smoothness), Is.EqualTo(0.2f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
                if (overrideAsset != null)
                {
                    UnityEngine.Object.DestroyImmediate(overrideAsset);
                }
            }
        }

        [Test]
        public void UrpLitEmissionOverrideEnablesEmissionKeywordAndClearsBlackGiFlag()
        {
            MmdModelDefinition model = CreateTwoMaterialModel();
            MmdMaterialOverrideAsset? overrideAsset = null;
            MmdUnityModelInstance? instance = null;

            try
            {
                overrideAsset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
                overrideAsset.entries = new[]
                {
                    new MmdMaterialOverrideEntry
                    {
                        materialIndex = 0,
                        hasEmissionColor = true,
                        emissionColor = new Color(0.1f, 0.6f, 0.3f, 1.0f)
                    }
                };

                instance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.UrpLit,
                    materialOverride: overrideAsset);

                Material material = instance.Materials[0];
                Color emissionColor = ReadMaterialColor(material, MmdMaterialPropertyNames.EmissionColor);
                Assert.That(emissionColor.r, Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(emissionColor.g, Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(emissionColor.b, Is.EqualTo(0.3f).Within(0.00001f));
                Assert.That(emissionColor.a, Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.True);
                Assert.That(
                    (material.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) != 0,
                    Is.False);
            }
            finally
            {
                DestroyInstance(instance);
                if (overrideAsset != null)
                {
                    UnityEngine.Object.DestroyImmediate(overrideAsset);
                }
            }
        }

        [Test]
        public void UrpLitMaterialOverrideByIndexAppliesNormalMapAndScale()
        {
            MmdModelDefinition model = CreateTwoMaterialModel();
            MmdMaterialOverrideAsset? overrideAsset = null;
            MmdUnityModelInstance? instance = null;
            Texture2D? normalMap = null;

            try
            {
                normalMap = CreateColorTexture(new Color(0.8f, 0.2f, 0.1f, 1.0f));
                overrideAsset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
                overrideAsset.entries = new[]
                {
                    new MmdMaterialOverrideEntry
                    {
                        materialIndex = 1,
                        hasNormalMap = true,
                        normalMap = normalMap,
                        hasNormalScale = true,
                        normalScale = 0.25f
                    }
                };

                instance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.UrpLit,
                    materialOverride: overrideAsset);

                Material material = instance.Materials[1];
                Assert.That(ReadMaterialTexture(material, MmdMaterialPropertyNames.BumpMap), Is.SameAs(normalMap));
                Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True);
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.BumpScale), Is.EqualTo(0.25f).Within(0.00001f));
            }
            finally
            {
                if (normalMap != null)
                {
                    UnityEngine.Object.DestroyImmediate(normalMap);
                }

                DestroyInstance(instance);
                if (overrideAsset != null)
                {
                    UnityEngine.Object.DestroyImmediate(overrideAsset);
                }
            }
        }

        [Test]
        public void MmdToonMaterialOverrideByIndexAppliesMmdNormalMapBound()
        {
            MmdModelDefinition model = CreateTwoMaterialModel();
            MmdMaterialOverrideAsset? overrideAsset = null;
            MmdUnityModelInstance? instance = null;
            Texture2D? normalMap = null;

            try
            {
                normalMap = CreateColorTexture(new Color(0.2f, 0.4f, 0.8f, 1.0f));
                overrideAsset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
                overrideAsset.entries = new[]
                {
                    new MmdMaterialOverrideEntry
                    {
                        materialIndex = 0,
                        hasNormalMap = true,
                        normalMap = normalMap
                    }
                };

                instance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.MmdToon,
                    materialOverride: overrideAsset);

                Material material = instance.Materials[0];
                Assert.That(ReadMaterialTexture(material, MmdMaterialPropertyNames.MmdNormalMap), Is.SameAs(normalMap));
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.MmdNormalMapBound), Is.EqualTo(1.0f).Within(0.00001f));
            }
            finally
            {
                if (normalMap != null)
                {
                    UnityEngine.Object.DestroyImmediate(normalMap);
                }

                DestroyInstance(instance);
                if (overrideAsset != null)
                {
                    UnityEngine.Object.DestroyImmediate(overrideAsset);
                }
            }
        }

        [Test]
        public void MaterialNameMatchOnlyAppliesWhenMaterialIndexIsInvalid()
        {
            MmdModelDefinition model = CreateTwoMaterialModel();
            MmdMaterialOverrideAsset? overrideAsset = null;
            MmdUnityModelInstance? instance = null;

            try
            {
                overrideAsset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
                overrideAsset.entries = new[]
                {
                    new MmdMaterialOverrideEntry
                    {
                        materialIndex = 0,
                        materialName = "second-material",
                        hasMetallic = true,
                        metallic = 0.6f
                    },
                    new MmdMaterialOverrideEntry
                    {
                        materialIndex = -1,
                        materialName = "second-material",
                        hasSmoothness = true,
                        smoothness = 0.15f
                    }
                };

                instance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.UrpLit,
                    materialOverride: overrideAsset);

                Assert.That(ReadMaterialFloat(instance.Materials[0], MmdMaterialPropertyNames.Metallic), Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[1], MmdMaterialPropertyNames.Metallic), Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[0], MmdMaterialPropertyNames.Smoothness), Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[1], MmdMaterialPropertyNames.Smoothness), Is.EqualTo(0.15f).Within(0.00001f));
            }
            finally
            {
                DestroyInstance(instance);
                if (overrideAsset != null)
                {
                    UnityEngine.Object.DestroyImmediate(overrideAsset);
                }
            }
        }

        private static void AssertMaterialFloatEqual(Material expected, Material actual, string propertyName)
        {
            Assert.That(actual.HasProperty(propertyName), Is.EqualTo(expected.HasProperty(propertyName)));
            if (expected.HasProperty(propertyName))
            {
                Assert.That(actual.GetFloat(propertyName), Is.EqualTo(expected.GetFloat(propertyName)).Within(0.00001f));
            }
        }

        private static float ReadMaterialFloat(Material material, string propertyName)
        {
            Assert.That(material.HasProperty(propertyName), Is.True, propertyName);
            return material.GetFloat(propertyName);
        }

        private static Texture ReadMaterialTexture(Material material, string propertyName)
        {
            Assert.That(material.HasProperty(propertyName), Is.True, propertyName);
            Texture? texture = material.GetTexture(propertyName);
            Assert.That(texture, Is.Not.Null);
            return texture;
        }

        private static Texture2D CreateColorTexture(Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        private static Color ReadMaterialColor(Material material, string propertyName)
        {
            Assert.That(material.HasProperty(propertyName), Is.True, propertyName);
            return material.GetColor(propertyName);
        }

        private static MmdModelDefinition CreateTwoMaterialModel()
        {
            var model = new MmdModelDefinition
            {
                name = "material-override-two-materials"
            };
            model.bones.Add(new MmdBoneDefinition
            {
                index = 0,
                name = "root",
                parentIndex = -1,
                transformOrder = 0,
                origin = new[] { 0.0f, 0.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.vertices.Add(CreateVertex(3, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(4, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(5, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2, 3, 4, 5 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "first-material",
                vertexCount = 3
            });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 1,
                name = "second-material",
                vertexCount = 3
            });
            return model;
        }

        private static MmdVertexDefinition CreateVertex(
            int index,
            float x,
            float y,
            float z,
            float u,
            float v)
        {
            return new MmdVertexDefinition
            {
                index = index,
                position = new[] { x, y, z },
                normal = new[] { 0.0f, 0.0f, 1.0f },
                uv = new[] { u, v },
                boneIndices = new[] { 0 },
                boneWeights = new[] { 1.0f }
            };
        }

        private static void DestroyInstance(MmdUnityModelInstance? instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.Root != null)
            {
                UnityEngine.Object.DestroyImmediate(instance.Root);
            }

            if (instance.Mesh != null)
            {
                UnityEngine.Object.DestroyImmediate(instance.Mesh);
            }

            foreach (Material material in instance.Materials)
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            foreach (Texture2D texture in instance.OwnedTextures)
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }
    }
}
