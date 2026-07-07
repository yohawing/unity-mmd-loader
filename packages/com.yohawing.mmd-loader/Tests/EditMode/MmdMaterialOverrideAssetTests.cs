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
        public void UrpLitMaterialOverrideByIndexAppliesPbrTextureMaps()
        {
            MmdModelDefinition model = CreateTwoMaterialModel();
            MmdMaterialOverrideAsset? overrideAsset = null;
            MmdUnityModelInstance? instance = null;
            Texture2D? metallicMap = null;
            Texture2D? roughnessMap = null;
            Texture2D? occlusionMap = null;

            try
            {
                metallicMap = CreateColorTexture(new Color(1.0f, 0.0f, 0.0f, 1.0f));
                roughnessMap = CreateColorTexture(new Color(0.0f, 1.0f, 0.0f, 1.0f));
                occlusionMap = CreateColorTexture(new Color(0.0f, 0.0f, 1.0f, 1.0f));
                overrideAsset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
                overrideAsset.entries = new[]
                {
                    new MmdMaterialOverrideEntry
                    {
                        materialIndex = 1,
                        hasMetallicMap = true,
                        metallicMap = metallicMap,
                        metallicMapIncludesSmoothness = true,
                        hasRoughnessMap = true,
                        roughnessMap = roughnessMap,
                        hasOcclusionMap = true,
                        occlusionMap = occlusionMap
                    }
                };

                instance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.UrpLit,
                    materialOverride: overrideAsset);

                Material material = instance.Materials[1];
                Assert.That(ReadMaterialTexture(material, MmdMaterialPropertyNames.MetallicGlossMap), Is.SameAs(metallicMap));
                Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
                Assert.That(ReadMaterialTexture(material, MmdMaterialPropertyNames.OcclusionMap), Is.SameAs(occlusionMap));
                Assert.That(material.IsKeywordEnabled("_OCCLUSIONMAP"), Is.True);
            }
            finally
            {
                if (metallicMap != null)
                {
                    UnityEngine.Object.DestroyImmediate(metallicMap);
                }

                if (roughnessMap != null)
                {
                    UnityEngine.Object.DestroyImmediate(roughnessMap);
                }

                if (occlusionMap != null)
                {
                    UnityEngine.Object.DestroyImmediate(occlusionMap);
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
        public void MmdToonMaterialOverrideByIndexAppliesMaterialMorphWriteSet()
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
                        hasBaseColor = true,
                        baseColor = new Color(0.2f, 0.4f, 0.6f, 0.45f),
                        hasColor = true,
                        color = new Color(0.9f, 0.8f, 0.7f, 0.45f),
                        hasAlpha = true,
                        alpha = 0.45f,
                        hasAmbientColor = true,
                        ambientColor = new Color(0.1f, 0.2f, 0.3f, 1.0f),
                        hasOutlineColor = true,
                        outlineColor = new Color(0.7f, 0.6f, 0.5f, 0.4f),
                        hasOutlineWidth = true,
                        outlineWidth = 1.75f
                    }
                };

                instance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.MmdToon,
                    materialOverride: overrideAsset);

                Material material = instance.Materials[0];
                AssertMaterialColor(material, MmdMaterialPropertyNames.BaseColor, new Color(0.2f, 0.4f, 0.6f, 0.45f));
                AssertMaterialColor(material, MmdMaterialPropertyNames.Color, new Color(0.9f, 0.8f, 0.7f, 0.45f));
                AssertMaterialColor(material, MmdMaterialPropertyNames.AmbientColor, new Color(0.1f, 0.2f, 0.3f, 1.0f));
                AssertMaterialColor(material, MmdMaterialPropertyNames.OutlineColor, new Color(0.7f, 0.6f, 0.5f, 0.4f));
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.OutlineWidth), Is.EqualTo(1.75f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.OutlineVisible), Is.EqualTo(1.0f).Within(0.00001f));
                MmdUrpMaterialBindingDescriptor binding = instance.RenderingDescriptor.urpMaterialBindings[0];
                Assert.That(binding.diffuseColor[0], Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(binding.diffuseColor[1], Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(binding.diffuseColor[2], Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(binding.alpha, Is.EqualTo(0.45f).Within(0.00001f));
                Assert.That(binding.ambientColor[0], Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(binding.ambientColor[1], Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(binding.ambientColor[2], Is.EqualTo(0.3f).Within(0.00001f));
                Assert.That(binding.edgeColor[0], Is.EqualTo(0.7f).Within(0.00001f));
                Assert.That(binding.edgeColor[1], Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(binding.edgeColor[2], Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(binding.edgeColor[3], Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(binding.edgeSize, Is.EqualTo(1.75f).Within(0.00001f));
                Assert.That(binding.drawEdgeFlag, Is.True);
                Assert.That(binding.isTransparent, Is.True);
                Assert.That(binding.transparencyMode, Is.EqualTo("alphaBlend"));
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
        public void UrpLitMaterialOverrideAppliesAlphaClipAndAlphaBlendSurfaceState()
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
                        hasBaseColor = true,
                        baseColor = new Color(0.2f, 0.4f, 0.6f, 0.45f),
                        hasColor = true,
                        color = new Color(0.9f, 0.8f, 0.7f, 0.45f),
                        hasAlpha = true,
                        alpha = 0.45f,
                        hasAlphaClipThreshold = true,
                        alphaClipThreshold = 0.35f,
                        hasSurfaceMode = true,
                        surfaceMode = MmdMaterialOverrideSurfaceMode.AlphaBlend
                    }
                };

                instance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.UrpLit,
                    materialOverride: overrideAsset);

                Material material = instance.Materials[0];
                AssertMaterialColor(material, MmdMaterialPropertyNames.BaseColor, new Color(0.2f, 0.4f, 0.6f, 0.45f));
                AssertMaterialColor(material, MmdMaterialPropertyNames.Color, new Color(0.9f, 0.8f, 0.7f, 0.45f));
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.Cutoff), Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_Surface"), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_SrcBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.SrcAlpha).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_DstBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha).Within(0.00001f));
                Assert.That(material.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
                MmdUrpMaterialBindingDescriptor binding = instance.RenderingDescriptor.urpMaterialBindings[0];
                Assert.That(binding.alpha, Is.EqualTo(0.45f).Within(0.00001f));
                Assert.That(binding.isTransparent, Is.True);
                Assert.That(binding.transparencyMode, Is.EqualTo("alphaBlend"));
                Assert.That(binding.renderOrderBucket, Is.EqualTo("alphaBlend"));
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
        public void MmdToonAlphaOnlyOverridePreservesExistingAlphaBlendSurfacePolicy()
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
                        hasAlphaClipThreshold = true,
                        alphaClipThreshold = 0.35f,
                        hasSurfaceMode = true,
                        surfaceMode = MmdMaterialOverrideSurfaceMode.AlphaBlend
                    },
                    new MmdMaterialOverrideEntry
                    {
                        materialIndex = 0,
                        hasAlpha = true,
                        alpha = 1.0f
                    }
                };

                instance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.MmdToon,
                    materialOverride: overrideAsset);

                Material material = instance.Materials[0];
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.Alpha), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.AlphaClipThreshold), Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.ShadowAlphaClipThreshold), Is.EqualTo(0.35f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.TextureAlphaOutputWeight), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_SrcBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.SrcAlpha).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_DstBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha).Within(0.00001f));
                Assert.That(material.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
                MmdUrpMaterialBindingDescriptor binding = instance.RenderingDescriptor.urpMaterialBindings[0];
                Assert.That(binding.alpha, Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(binding.isTransparent, Is.True);
                Assert.That(binding.transparencyMode, Is.EqualTo("alphaBlend"));
                Assert.That(binding.renderOrderBucket, Is.EqualTo("alphaBlend"));
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
        public void MmdToonAlphaOnlyOverridePreservesExistingOpaqueSurfacePolicy()
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
                        hasSurfaceMode = true,
                        surfaceMode = MmdMaterialOverrideSurfaceMode.Opaque
                    },
                    new MmdMaterialOverrideEntry
                    {
                        materialIndex = 0,
                        hasAlpha = true,
                        alpha = 0.45f
                    }
                };

                instance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.MmdToon,
                    materialOverride: overrideAsset);

                Material material = instance.Materials[0];
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.Alpha), Is.EqualTo(0.45f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.TextureAlphaOutputWeight), Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_SrcBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.One).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_DstBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.Zero).Within(0.00001f));
                Assert.That(material.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Geometry));
                MmdUrpMaterialBindingDescriptor binding = instance.RenderingDescriptor.urpMaterialBindings[0];
                Assert.That(binding.alpha, Is.EqualTo(0.45f).Within(0.00001f));
                Assert.That(binding.isTransparent, Is.False);
                Assert.That(binding.transparencyMode, Is.EqualTo("opaque"));
                Assert.That(binding.renderOrderBucket, Is.EqualTo("opaque"));
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
        public void MmdToonAlphaResetOverridePreservesAutoAlphaBlendSurfacePolicy()
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
                        hasAlpha = true,
                        alpha = 0.45f
                    },
                    new MmdMaterialOverrideEntry
                    {
                        materialIndex = 0,
                        hasAlpha = true,
                        alpha = 1.0f
                    }
                };

                instance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.MmdToon,
                    materialOverride: overrideAsset);

                Material material = instance.Materials[0];
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.Alpha), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.TextureAlphaOutputWeight), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_SrcBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.SrcAlpha).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_DstBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha).Within(0.00001f));
                Assert.That(material.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
                MmdUrpMaterialBindingDescriptor binding = instance.RenderingDescriptor.urpMaterialBindings[0];
                Assert.That(binding.alpha, Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(binding.isTransparent, Is.True);
                Assert.That(binding.transparencyMode, Is.EqualTo("alphaBlend"));
                Assert.That(binding.renderOrderBucket, Is.EqualTo("alphaBlend"));
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
        public void MmdToonSurfacePreserveOverrideDoesNotReclassifyAlpha()
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
                        hasAlpha = true,
                        alpha = 0.45f,
                        hasSurfaceMode = true,
                        surfaceMode = MmdMaterialOverrideSurfaceMode.Preserve
                    }
                };

                instance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.MmdToon,
                    materialOverride: overrideAsset);

                Material material = instance.Materials[0];
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.Alpha), Is.EqualTo(0.45f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.TextureAlphaOutputWeight), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_SrcBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.SrcAlpha).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_DstBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha).Within(0.00001f));
                Assert.That(material.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
                MmdUrpMaterialBindingDescriptor binding = instance.RenderingDescriptor.urpMaterialBindings[0];
                Assert.That(binding.alpha, Is.EqualTo(0.45f).Within(0.00001f));
                Assert.That(binding.isTransparent, Is.False);
                Assert.That(binding.transparencyMode, Is.EqualTo("opaque"));
                Assert.That(binding.renderOrderBucket, Is.EqualTo("opaque"));
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
        public void MmdToonAlphaClipOnlyOverrideClassifiesDescriptorAndMaterialAsAlphaTest()
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
                        hasAlphaClipThreshold = true,
                        alphaClipThreshold = 0.35f
                    }
                };

                instance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.MmdToon,
                    materialOverride: overrideAsset);

                Material material = instance.Materials[0];
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.AlphaClipThreshold), Is.EqualTo(0.35f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.ShadowAlphaClipThreshold), Is.EqualTo(0.35f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.TextureAlphaOutputWeight), Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_SrcBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.One).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_DstBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.Zero).Within(0.00001f));
                Assert.That(material.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Geometry));
                MmdUrpMaterialBindingDescriptor binding = instance.RenderingDescriptor.urpMaterialBindings[0];
                Assert.That(binding.alpha, Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(binding.isTransparent, Is.False);
                Assert.That(binding.transparencyMode, Is.EqualTo("alphaTest"));
                Assert.That(binding.renderOrderBucket, Is.EqualTo("opaque"));
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
        public void MmdToonAlphaClipOnlyOverrideClassifiesExistingTransparentMaterialAsAlphaTest()
        {
            MmdModelDefinition model = CreateTwoMaterialModel();
            model.materials[0].alpha = 0.45f;
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
                        hasAlphaClipThreshold = true,
                        alphaClipThreshold = 0.35f
                    }
                };

                instance = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.MmdToon,
                    materialOverride: overrideAsset);

                Material material = instance.Materials[0];
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.AlphaClipThreshold), Is.EqualTo(0.35f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.TextureAlphaOutputWeight), Is.EqualTo(0.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_SrcBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.One).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_DstBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.Zero).Within(0.00001f));
                Assert.That(material.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Geometry));
                MmdUrpMaterialBindingDescriptor binding = instance.RenderingDescriptor.urpMaterialBindings[0];
                Assert.That(binding.alpha, Is.EqualTo(0.45f).Within(0.00001f));
                Assert.That(binding.isTransparent, Is.False);
                Assert.That(binding.transparencyMode, Is.EqualTo("alphaTest"));
                Assert.That(binding.renderOrderBucket, Is.EqualTo("opaque"));
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
        public void ImportedAssetCacheAppliesAlphaOverrideBeforeMaterialGeneration()
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
                        hasBaseColor = true,
                        baseColor = new Color(0.2f, 0.4f, 0.6f, 0.45f)
                    }
                };

                instance = MmdPmxImportAssetCacheBuilder.CreateImportedAssetCache(
                    model,
                    importScale: 1.0f,
                    includeSelfShadowTarget: true,
                    preset: MmdMaterialPreset.MmdToon,
                    materialOverride: overrideAsset);

                Material material = instance.Materials[0];
                AssertMaterialColor(material, MmdMaterialPropertyNames.BaseColor, new Color(0.2f, 0.4f, 0.6f, 0.45f));
                Assert.That(ReadMaterialFloat(material, MmdMaterialPropertyNames.TextureAlphaOutputWeight), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_SrcBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.SrcAlpha).Within(0.00001f));
                Assert.That(ReadMaterialFloat(material, "_DstBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha).Within(0.00001f));
                Assert.That(material.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
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

        private static void AssertMaterialColor(Material material, string propertyName, Color expected)
        {
            Color actual = ReadMaterialColor(material, propertyName);
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.00001f), propertyName + ".r");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.00001f), propertyName + ".g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.00001f), propertyName + ".b");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.00001f), propertyName + ".a");
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
