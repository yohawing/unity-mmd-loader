#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Mmd.Parser;
using Mmd.Rendering;
using Mmd.UnityIntegration;

namespace Mmd.Tests
{
    public sealed partial class MmdUnityModelFactoryTests
    {
        [Test]
        public void ExistingSkinnedModelRebindAppliesMaterialOverrideWriteSetToMaterialMorphBase()
        {
            MmdModelDefinition model = CreateMaterialMorphMultiplyModel();
            MmdMaterialOverrideAsset? overrideAsset = null;
            Material? reboundMaterial = null;

            try
            {
                overrideAsset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
                overrideAsset.entries = new[]
                {
                    new MmdMaterialOverrideEntry
                    {
                        materialIndex = 0,
                        hasBaseColor = true,
                        baseColor = new Color(0.2f, 0.4f, 0.6f, 0.5f),
                        hasAmbientColor = true,
                        ambientColor = new Color(0.3f, 0.2f, 0.1f, 1.0f),
                        hasOutlineColor = true,
                        outlineColor = new Color(0.6f, 0.5f, 0.4f, 0.3f),
                        hasOutlineWidth = true,
                        outlineWidth = 1.25f
                    }
                };

                using var sceneScope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.MmdToon));
                Material originalMaterial = sceneScope.Instance.Materials[0];
                MmdUnityModelInstance reboundInstance = MmdUnityModelFactory.CreateExistingSkinnedModelInstance(
                    sceneScope.Instance.Root,
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    includeSelfShadowTarget: true,
                    materialOverride: overrideAsset);
                reboundMaterial = reboundInstance.Materials[0];

                Color initialColor = ReadMaterialColor(reboundInstance.Materials[0], "_BaseColor");
                Assert.That(reboundInstance.Materials[0], Is.Not.SameAs(originalMaterial));
                Assert.That(ReadMaterialColor(originalMaterial, "_BaseColor").a, Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(initialColor.r, Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(initialColor.g, Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(initialColor.b, Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(initialColor.a, Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(reboundInstance.Materials[0], "_OutlineWidth"), Is.EqualTo(1.25f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(reboundInstance.Materials[0], "_SrcBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.SrcAlpha).Within(0.00001f));
                Assert.That(ReadMaterialFloat(reboundInstance.Materials[0], "_DstBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha).Within(0.00001f));
                Assert.That(reboundInstance.Materials[0].renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));

                MmdEvaluatedFrame frame0 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame0.morphs.Add(new MmdEvaluatedMorphWeight { name = "multiply-change", weight = 0.0f });
                MmdUnityFrameApplier.ApplyFrame(reboundInstance, frame0);

                Color restoredColor = ReadMaterialColor(reboundInstance.Materials[0], "_BaseColor");
                Assert.That(restoredColor.r, Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(restoredColor.g, Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(restoredColor.b, Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(restoredColor.a, Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(reboundInstance.Materials[0], "_OutlineWidth"), Is.EqualTo(1.25f).Within(0.00001f));
            }
            finally
            {
                if (overrideAsset != null)
                {
                    UnityEngine.Object.DestroyImmediate(overrideAsset);
                }

                if (reboundMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(reboundMaterial);
                }
            }
        }
        [Test]
        public void ExistingSkinnedModelRebindSkipsNullMaterialOverrideSlot()
        {
            MmdModelDefinition model = CreateMaterialMorphMultiplyModel();
            MmdMaterialOverrideAsset? overrideAsset = null;

            try
            {
                overrideAsset = ScriptableObject.CreateInstance<MmdMaterialOverrideAsset>();
                overrideAsset.entries = new[]
                {
                    new MmdMaterialOverrideEntry
                    {
                        materialIndex = 0,
                        hasBaseColor = true,
                        baseColor = new Color(0.2f, 0.4f, 0.6f, 0.5f)
                    }
                };

                using var sceneScope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.MmdToon));
                SkinnedMeshRenderer renderer = RequireSkinnedRenderer(sceneScope.Instance);
                Material[] materials = renderer.sharedMaterials;
                materials[0] = null;
                renderer.sharedMaterials = materials;

                Assert.DoesNotThrow(() =>
                    MmdUnityModelFactory.CreateExistingSkinnedModelInstance(
                        sceneScope.Instance.Root,
                        model,
                        sourcePath: null,
                        importScale: 1.0f,
                        includeSelfShadowTarget: true,
                        materialOverride: overrideAsset));
            }
            finally
            {
                if (overrideAsset != null)
                {
                    UnityEngine.Object.DestroyImmediate(overrideAsset);
                }
            }
        }
        [Test]
        public void ApplyMaterialMorphAllMaterialAddTargetMutatesAllMaterials()
        {

            MmdModelDefinition model = CreateMaterialMorphAllMaterialAddModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Materials, Has.Length.EqualTo(2));

            // Apply add morph at weight 1.0 (materialIndex = -1 => all materials).
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "all-add", weight = 1.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            // Material 0: base(0.8,0.2,0.6) + offset(0.0,0.5,0.0) = (0.8,0.7,0.6)
            Color mat0Color = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            Assert.That(mat0Color.r, Is.EqualTo(0.8f).Within(0.00001f));
            Assert.That(mat0Color.g, Is.EqualTo(0.7f).Within(0.00001f));
            Assert.That(mat0Color.b, Is.EqualTo(0.6f).Within(0.00001f));
            // alpha: 1.0 + (-0.3) = 0.7
            Assert.That(mat0Color.a, Is.EqualTo(0.7f).Within(0.00001f));

            // Material 1: base(0.9,0.7,0.4) + offset(0.0,0.5,0.0) = (0.9,1.0,0.4)
            Color mat1Color = ReadMaterialColor(instance.Materials[1], "_BaseColor");
            Assert.That(mat1Color.r, Is.EqualTo(0.9f).Within(0.00001f));
            Assert.That(mat1Color.g, Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(mat1Color.b, Is.EqualTo(0.4f).Within(0.00001f));
            // alpha: 0.8 + (-0.3) = 0.5
            Assert.That(mat1Color.a, Is.EqualTo(0.5f).Within(0.00001f));

            // Both materials edge size changed: material 0: 1.0+2.0=3.0, material 1: 0.5+2.0=2.5
            // (_OutlineWidth = raw edgeSize)
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_OutlineWidth"), Is.EqualTo(3.0f).Within(0.00001f));
            Assert.That(ReadMaterialFloat(instance.Materials[1], "_OutlineWidth"), Is.EqualTo(2.5f).Within(0.00001f));
        }
        [Test]
        public void ApplyMaterialMorphMultiplyAllMaterialTargetMutatesAllMaterials()
        {

            MmdModelDefinition model = CreateMaterialMorphMultiplyAllMaterialModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Materials, Has.Length.EqualTo(2));

            // Apply multiply morph at weight 1.0 (materialIndex = -1 => all materials).
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "all-multiply", weight = 1.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            // Material 0: base(0.8,0.2,0.6) * offset(0.5,1.5,0.75) = (0.4,0.3,0.45)
            Color mat0Color = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            Assert.That(mat0Color.r, Is.EqualTo(0.4f).Within(0.00001f));
            Assert.That(mat0Color.g, Is.EqualTo(0.3f).Within(0.00001f));
            Assert.That(mat0Color.b, Is.EqualTo(0.45f).Within(0.00001f));
            // alpha: 1.0 * 0.5 = 0.5
            Assert.That(mat0Color.a, Is.EqualTo(0.5f).Within(0.00001f));

            // Material 1: base(0.9,0.7,0.4) * offset(0.5,1.5,0.75) = (0.45,1.0,0.3)
            Color mat1Color = ReadMaterialColor(instance.Materials[1], "_BaseColor");
            Assert.That(mat1Color.r, Is.EqualTo(0.45f).Within(0.00001f));
            Assert.That(mat1Color.g, Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(mat1Color.b, Is.EqualTo(0.3f).Within(0.00001f));
            // alpha: 0.8 * 0.5 = 0.4
            Assert.That(mat1Color.a, Is.EqualTo(0.4f).Within(0.00001f));

            // Material 0: base ambient(0.1,0.3,0.5) * offset(2.0,0.5,1.0) = (0.2,0.15,0.5)
            Color mat0Ambient = ReadMaterialColor(instance.Materials[0], "_AmbientColor");
            Assert.That(mat0Ambient.r, Is.EqualTo(0.2f).Within(0.00001f));
            Assert.That(mat0Ambient.g, Is.EqualTo(0.15f).Within(0.00001f));
            Assert.That(mat0Ambient.b, Is.EqualTo(0.5f).Within(0.00001f));

            // Material 1: base ambient(0.2,0.1,0.3) * offset(2.0,0.5,1.0) = (0.4,0.05,0.3)
            Color mat1Ambient = ReadMaterialColor(instance.Materials[1], "_AmbientColor");
            Assert.That(mat1Ambient.r, Is.EqualTo(0.4f).Within(0.00001f));
            Assert.That(mat1Ambient.g, Is.EqualTo(0.05f).Within(0.00001f));
            Assert.That(mat1Ambient.b, Is.EqualTo(0.3f).Within(0.00001f));

            // Both materials edge size changed (_OutlineWidth = raw edgeSize):
            // material 0: 1.0*2.0=2.0, material 1: 0.5*2.0=1.0
            Assert.That(ReadMaterialFloat(instance.Materials[0], "_OutlineWidth"), Is.EqualTo(2.0f).Within(0.00001f));
            Assert.That(ReadMaterialFloat(instance.Materials[1], "_OutlineWidth"), Is.EqualTo(1.0f).Within(0.00001f));

            // Material 0: edgeColor(0,0,0,1) * offset(0.5,0.5,0.5,0.2) = (0,0,0,0.2)
            Assert.That(ReadMaterialColor(instance.Materials[0], "_OutlineColor").a, Is.EqualTo(0.2f).Within(0.00001f));
            // Material 1: edgeColor(0,0,0,0.9) * offset(0.5,0.5,0.5,0.2) = (0,0,0,0.18)
            Assert.That(ReadMaterialColor(instance.Materials[1], "_OutlineColor").a, Is.EqualTo(0.18f).Within(0.00001f));
        }
        [Test]
        public void MultiplyAllMaterialMorphDoesNotAccumulateAndRestoresOnZeroWeight()
        {

            MmdModelDefinition model = CreateMaterialMorphMultiplyAllMaterialModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Materials, Has.Length.EqualTo(2));

            // Record base values.
            Color base0Color = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            Color base1Color = ReadMaterialColor(instance.Materials[1], "_BaseColor");
            float base0Outline = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

            // Step 1: Apply multiply all-material morph at weight 1.0.
            MmdEvaluatedFrame frame1 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame1.morphs.Add(new MmdEvaluatedMorphWeight { name = "all-multiply", weight = 1.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame1);

            Color firstMat0Color = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            Assert.That(firstMat0Color.r, Is.EqualTo(0.4f).Within(0.00001f), "First apply should morph material 0");

            // Step 2: apply the same frame again; it must not accumulate.
            MmdUnityFrameApplier.ApplyFrame(instance, frame1);
            Color secondMat0Color = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            Assert.That(secondMat0Color.r, Is.EqualTo(firstMat0Color.r).Within(0.00001f), "Repeated apply should not accumulate");
            Assert.That(secondMat0Color.g, Is.EqualTo(firstMat0Color.g).Within(0.00001f));
            Assert.That(secondMat0Color.b, Is.EqualTo(firstMat0Color.b).Within(0.00001f));
            Assert.That(secondMat0Color.a, Is.EqualTo(firstMat0Color.a).Within(0.00001f));

            // Step 3: Apply zero-weight frame to restore base values.
            MmdEvaluatedFrame frame0 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame0.morphs.Add(new MmdEvaluatedMorphWeight { name = "all-multiply", weight = 0.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame0);

            Color restored0Color = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            Color restored1Color = ReadMaterialColor(instance.Materials[1], "_BaseColor");
            float restored0Outline = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

            Assert.That(restored0Color.r, Is.EqualTo(base0Color.r).Within(0.00001f), "Zero weight should restore material 0 diffuse red");
            Assert.That(restored0Color.g, Is.EqualTo(base0Color.g).Within(0.00001f));
            Assert.That(restored0Color.b, Is.EqualTo(base0Color.b).Within(0.00001f));
            Assert.That(restored0Color.a, Is.EqualTo(base0Color.a).Within(0.00001f));
            Assert.That(restored1Color.r, Is.EqualTo(base1Color.r).Within(0.00001f), "Zero weight should restore material 1 diffuse red");
            Assert.That(restored1Color.a, Is.EqualTo(base1Color.a).Within(0.00001f), "Zero weight should restore material 1 alpha");
            Assert.That(restored0Outline, Is.EqualTo(base0Outline).Within(0.00001f), "Zero weight should restore material 0 outline width");
        }
        [Test]
        public void ApplyFrameExpandsFlipMorphWeightToTargetVertexMorph()
        {

            MmdModelDefinition model = CreateFlipMorphTriangleModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            // Frame with flip morph weight only, no direct vertex morph weight.
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "flip-smile", weight = 1.0f });

            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            // flipWeight(1.0) * offsetWeight(0.5) = resolved smile 0.5 -> BlendShape 50f
            int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
            Assert.That(smileIndex, Is.GreaterThanOrEqualTo(0));
            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Assert.That(renderer.GetBlendShapeWeight(smileIndex), Is.EqualTo(50f).Within(0.001f));
        }
        [Test]
        public void ApplyFrameExpandsFlipMorphToMaterialMorph()
        {

            MmdModelDefinition model = CreateFlipMaterialMorphModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            // Frame with flip morph weight only, no direct material morph weight.
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "flip-color", weight = 1.0f });

            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            // Flip morph "flip-color" targets "color-change" with weight 0.8.
            // Resolved "color-change" weight = 1.0 * 0.8 = 0.8.
            // diffuseColor: base(0.8,0.2,0.6) + offset(0.0,0.5,0.0)*0.8 = (0.8,0.6,0.6)
            Color morphedColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            Assert.That(morphedColor.r, Is.EqualTo(0.8f).Within(0.00001f));
            Assert.That(morphedColor.g, Is.EqualTo(0.6f).Within(0.00001f));
            Assert.That(morphedColor.b, Is.EqualTo(0.6f).Within(0.00001f));
            // alpha: 1.0 + (-0.3)*0.8 = 0.76
            Assert.That(morphedColor.a, Is.EqualTo(0.76f).Within(0.00001f));
        }
        [Test]
        public void ApplyFrameWithGroupToFlipRecursiveExpansion()
        {

            MmdModelDefinition model = CreateGroupFlipRecursiveModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            // Frame has group morph weight that targets a flip morph.
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "mood-group", weight = 1.0f });

            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            // mood-group(1.0) -> flip-smile(1.0) -> smile(0.5) = resolved smile 0.5 -> BlendShape 50f
            int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
            Assert.That(smileIndex, Is.GreaterThanOrEqualTo(0));
            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Assert.That(renderer.GetBlendShapeWeight(smileIndex), Is.EqualTo(50f).Within(0.001f));
        }
        [Test]
        public void FlipMorphCycleDetectionThrows()
        {

            MmdModelDefinition model = CreateCycleFlipMorphModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "loop-a", weight = 1.0f });

            var ex = Assert.Throws<InvalidOperationException>(() =>
                MmdUnityFrameApplier.ApplyFrame(instance, frame));

            Assert.That(ex.Message, Does.Contain("cycle"));
        }
        [Test]
        public void CreateSkinnedModelBakesBlendShapeFramesForVertexMorphs()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "blink",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 2.0f, 0.0f }
                    }
                }
            });

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Mesh.blendShapeCount, Is.EqualTo(1));
            Assert.That(instance.Mesh.GetBlendShapeName(0), Is.EqualTo("blink"));
            Assert.That(instance.BlendShapeIndexMap.ContainsKey("blink"), Is.True);
            Assert.That(instance.BlendShapeIndexMap["blink"], Is.EqualTo(0));
        }
        [Test]
        public void SkinnedModelZeroBlendShapeWeightReturnsToBaseThroughSetBlendShapeWeightPath()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "blink",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 2.0f, 0.0f }
                    }
                }
            });
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;
            int blinkIndex = instance.Mesh.GetBlendShapeIndex("blink");

            MmdEvaluatedFrame frame1 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame1.morphs.Add(new MmdEvaluatedMorphWeight { name = "blink", weight = 1.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame1);
            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Assert.That(renderer.GetBlendShapeWeight(blinkIndex), Is.EqualTo(100f).Within(0.001f));

            MmdEvaluatedFrame frame0 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame0.morphs.Add(new MmdEvaluatedMorphWeight { name = "blink", weight = 0.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame0);
            Assert.That(renderer.GetBlendShapeWeight(blinkIndex), Is.EqualTo(0f).Within(0.001f));

            Assert.That(instance.RenderingDescriptor.vertices[1].position[1], Is.EqualTo(0.0f).Within(0.00001f));
        }
        [Test]
        public void SplitVertexCopyReceivesMorphOffsetInBakedBlendShapeFrame()
        {

            MmdModelDefinition model = CreateSharedVertexTwoSubmeshMorphModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Mesh.blendShapeCount, Is.EqualTo(1));
            int shapeIndex = instance.Mesh.GetBlendShapeIndex("shared-up");
            Assert.That(shapeIndex, Is.GreaterThanOrEqualTo(0));

            var deltaVertices = new Vector3[instance.Mesh.vertexCount];
            var deltaNormals = new Vector3[instance.Mesh.vertexCount];
            var deltaTangents = new Vector3[instance.Mesh.vertexCount];
            instance.Mesh.GetBlendShapeFrameVertices(shapeIndex, 0, deltaVertices, deltaNormals, deltaTangents);

            Assert.That(deltaVertices[0], Is.EqualTo(new Vector3(0.0f, 1.0f, 0.0f)));
            Assert.That(deltaVertices[3], Is.EqualTo(new Vector3(0.0f, 1.0f, 0.0f)));
        }
        [Test]
        public void GroupOrFlipResolvedWeightReachesBlendShapeWeight()
        {

            MmdModelDefinition model = CreateGroupMorphTriangleModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;
            int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
            Assert.That(smileIndex, Is.GreaterThanOrEqualTo(0));

            // Group "happy-face" at 0.75 targeting "smile" with weight 0.5
            // => resolved smile = 0.75 * 0.5 = 0.375 -> BlendShape 37.5f
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 0.75f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Assert.That(renderer.GetBlendShapeWeight(smileIndex), Is.EqualTo(37.5f).Within(0.001f));
        }
        [Test]
        public void VertexOnlyApplyMorphsWithTimingReportsNoMeshUpload()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "blink",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 1.0f, 0.0f }
                    }
                }
            });
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "blink", weight = 1.0f });

            MmdUnityMorphApplyTimingSummary timing = MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);

            Assert.That(timing.hasVertexMorphs, Is.True);
            Assert.That(timing.hasTextureUvMorphs, Is.False);
            Assert.That(timing.meshUploadRequired, Is.False);
            Assert.That(timing.blendShapePathUsed, Is.True);
            Assert.That(timing.setVerticesMs, Is.EqualTo(0.0).Within(0.000001));
        }
        [Test]
        public void ReapplyImportedMaterialTransparencyReclassifiesByTextureAlpha()
        {
            // Prove that ReapplyImportedMaterialTransparency re-classifies and re-applies the
            // transparency mode using the decoded texture alpha without touching AssetDatabase.
            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            // Ensure PMX alpha is 1.0 so the test exercises the texture-alpha path, not PMX-alpha.
            model.materials[0].alpha = 1.0f;

            MmdRenderingDescriptor descriptor = MmdRenderingDescriptorBuilder.Build(model);

            Shader shader = Shader.Find("MMD Basic URP Toon")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? throw new InvalidOperationException("No fallback shader found.");

            Material material = new Material(shader);
            Texture2D? alphaTex = null;
            Texture2D? opaqueTex = null;
            try
            {
                // --- alphaBlend case: all pixels at alpha 128 (middle alpha) ---
                alphaTex = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false);
                var alphaPixels = new Color32[16];
                for (int k = 0; k < 16; k++)
                {
                    alphaPixels[k] = new Color32(255, 255, 255, 128);
                }
                alphaTex.SetPixels32(alphaPixels);
                alphaTex.Apply();

                MmdUnityMaterialBuilder.ReapplyImportedMaterialTransparency(
                    material, descriptor, descriptor.materials[0], 0, "tex.png", "tex.png", alphaTex);

                Assert.That(
                    material.GetFloat("_DstBlend"),
                    Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha).Within(0.00001f),
                    "alpha-128 texture should classify as alphaBlend (_DstBlend = OneMinusSrcAlpha)");
                Assert.That(
                    material.renderQueue,
                    Is.GreaterThanOrEqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent),
                    "alpha-128 texture should put material in Transparent queue");

                // --- opaque case: all pixels at alpha 255 ---
                opaqueTex = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false);
                var opaquePixels = new Color32[16];
                for (int k = 0; k < 16; k++)
                {
                    opaquePixels[k] = new Color32(255, 255, 255, 255);
                }
                opaqueTex.SetPixels32(opaquePixels);
                opaqueTex.Apply();

                MmdUnityMaterialBuilder.ReapplyImportedMaterialTransparency(
                    material, descriptor, descriptor.materials[0], 0, "tex.png", "tex.png", opaqueTex);

                Assert.That(
                    material.GetFloat("_DstBlend"),
                    Is.EqualTo((float)UnityEngine.Rendering.BlendMode.Zero).Within(0.00001f),
                    "alpha-255 texture should classify as opaque (_DstBlend = Zero)");
                Assert.That(
                    material.renderQueue,
                    Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Geometry),
                    "alpha-255 texture should put material in Geometry queue");
            }
            finally
            {
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (alphaTex != null) UnityEngine.Object.DestroyImmediate(alphaTex);
                if (opaqueTex != null) UnityEngine.Object.DestroyImmediate(opaqueTex);
            }
        }
    }
}
