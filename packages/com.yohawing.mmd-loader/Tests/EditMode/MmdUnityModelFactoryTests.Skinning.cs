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
        public void ApplyColumnMajorWorldMatricesScalesPositionOnlyWithImportScale()
        {
            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            using var scaleOneScope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model, sourcePath: null, importScale: 1.0f));
            MmdUnityModelInstance scaleOneInstance = scaleOneScope.Instance;
            using var scalePointOneScope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model, sourcePath: null, importScale: 0.1f));
            MmdUnityModelInstance scalePointOneInstance = scalePointOneScope.Instance;
            Quaternion rootMmdRotation = Quaternion.Euler(0.0f, 45.0f, 0.0f);
            Quaternion childMmdRotation = Quaternion.Euler(15.0f, 30.0f, 10.0f);
            float[] worldMatrices = Concatenate(
                CreateColumnMajorWorldMatrix(new Vector3(1.0f, 2.0f, 3.0f), rootMmdRotation),
                CreateColumnMajorWorldMatrix(new Vector3(-2.0f, 4.0f, 5.0f), childMmdRotation));

            MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(scaleOneInstance, worldMatrices);
            MmdUnityWorldMatrixFrameApplier.ApplyColumnMajorWorldMatrices(scalePointOneInstance, worldMatrices);

            Assert.That(Vector3.Distance(scaleOneInstance.BoneTransforms[0].position, new Vector3(-1.0f, 2.0f, -3.0f)), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(scaleOneInstance.BoneTransforms[1].position, new Vector3(2.0f, 4.0f, -5.0f)), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(scalePointOneInstance.BoneTransforms[0].position, scaleOneInstance.BoneTransforms[0].position * 0.1f), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(scalePointOneInstance.BoneTransforms[1].position, scaleOneInstance.BoneTransforms[1].position * 0.1f), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(scalePointOneInstance.BoneTransforms[0].rotation, scaleOneInstance.BoneTransforms[0].rotation), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(scalePointOneInstance.BoneTransforms[1].rotation, scaleOneInstance.BoneTransforms[1].rotation), Is.LessThan(0.0001f));
            Assert.That(scalePointOneInstance.BoneTransforms[0].localScale, Is.EqualTo(Vector3.one));
            Assert.That(scalePointOneInstance.BoneTransforms[1].localScale, Is.EqualTo(Vector3.one));
        }
        [Test]
        public void ApplyFrameRejectsBoneIndexWithoutUnityTransform()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            var ex = Assert.Throws<ArgumentException>(() =>
                MmdUnityFrameApplier.ApplyFrame(instance, CreateFrame(CreateBonePose(2, "missing", 0.0f, 0.0f, 0.0f))));

            Assert.That(ex.Message, Does.Contain("no Unity bone transform"));
        }
        [Test]
        public void PlaybackBindingBuildsSnapshotAndAppliesFrameToSkinnedModel()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadPlaybackFixturePair();

            MmdUnityPlaybackBinding binding = MmdUnityPlaybackBinding.CreateSkinned(
                model,
                motion,
                "test_1bone_cube.pmx",
                "test_1bone_cube_motion.vmd");
            using var bindingScope = new MmdTestInstanceScope(binding.Instance);
            MmdPlaybackSnapshot snapshot = binding.ApplyFrame(frame: 9, frameRate: 30.0f);

            Assert.That(snapshot.model, Is.EqualTo("test_1bone_cube.pmx"));
            Assert.That(snapshot.motion, Is.EqualTo("test_1bone_cube_motion.vmd"));
            Assert.That(snapshot.frame.frame, Is.EqualTo(9));
            Assert.That(snapshot.rendering, Is.SameAs(binding.Instance.RenderingDescriptor));
            Assert.That(binding.Instance.SkinnedMeshRenderer, Is.Not.Null);
            Assert.That(snapshot.frame.bones, Has.Count.GreaterThan(0));
            Quaternion expectedRotation = ToUnityModelRotation(new Quaternion(
                snapshot.frame.bones[0].localRotation[0],
                snapshot.frame.bones[0].localRotation[1],
                snapshot.frame.bones[0].localRotation[2],
                snapshot.frame.bones[0].localRotation[3]));
            Assert.That(Quaternion.Angle(binding.Instance.BoneTransforms[0].localRotation, expectedRotation), Is.LessThan(0.0001f));
            Assert.That(binding.Instance.Root.transform.localScale, Is.EqualTo(Vector3.one));
        }
        [Test]
        public void PlaybackBindingAppliesVertexMorphWeightsToSkinnedMeshWithoutAccumulation()
        {
            (MmdModelDefinition model, MmdMotionDefinition motion) = LoadVertexMorphFixturePair();
            MmdUnityPlaybackBinding binding = MmdUnityPlaybackBinding.CreateSkinned(
                model,
                motion,
                "test_vertex_morph.pmx",
                "test_vertex_morph_motion.vmd");
            using var scope = new MmdTestInstanceScope(binding.Instance);

            Assert.That(binding.Instance.SkinnedMeshRenderer, Is.Not.Null);
            SkinnedMeshRenderer renderer = binding.Instance.SkinnedMeshRenderer!;
            int blinkShapeIndex = binding.Instance.Mesh.GetBlendShapeIndex("blink");
            Assert.That(blinkShapeIndex, Is.GreaterThanOrEqualTo(0));

            binding.ApplyFrame(frame: 10, frameRate: 30.0f);
            float morphedWeight = renderer.GetBlendShapeWeight(blinkShapeIndex);
            Bounds morphedBounds = renderer.localBounds;

            binding.ApplyFrame(frame: 10, frameRate: 30.0f);
            float repeatedWeight = renderer.GetBlendShapeWeight(blinkShapeIndex);

            binding.ApplyFrame(frame: 0, frameRate: 30.0f);
            float restoredWeight = renderer.GetBlendShapeWeight(blinkShapeIndex);

            Assert.That(morphedWeight, Is.EqualTo(100f).Within(0.001f));
            Assert.That(morphedBounds.Contains(new Vector3(-1.0f, 2.0f, 0.0f)), Is.True);
            Assert.That(repeatedWeight, Is.EqualTo(morphedWeight).Within(0.001f));
            Assert.That(restoredWeight, Is.EqualTo(0f).Within(0.001f));
            Assert.That(binding.Instance.RenderingDescriptor.vertices[1].position[1], Is.EqualTo(0.0f).Within(0.00001f));
        }
        [Test]
        public void CreateSkinnedModelSplitsSharedVerticesPerSubmeshAndDuplicatesMorphOffsets()
        {

            MmdModelDefinition model = CreateSharedVertexTwoSubmeshMorphModel();

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Mesh.vertexCount, Is.EqualTo(6));
            Assert.That(instance.Mesh.subMeshCount, Is.EqualTo(2));
            Assert.That(instance.Mesh.GetIndices(0), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(instance.Mesh.GetIndices(1), Is.EqualTo(new[] { 3, 4, 5 }));
            Assert.That(instance.RenderingDescriptor.vertices.Select(vertex => vertex.vertexIndex), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            Assert.That(instance.RenderingDescriptor.skinning[0].skinningMode, Is.EqualTo("sdef"));
            Assert.That(instance.RenderingDescriptor.skinning[0].supportStatus, Is.EqualTo(MmdSkinningDescriptorBuilder.LinearFallbackStatus));
            Assert.That(instance.RenderingDescriptor.skinning[0].linearFallbackToBoneWeights, Is.True);
            Assert.That(instance.RenderingDescriptor.skinning[3].skinningMode, Is.EqualTo("sdef"));
            Assert.That(instance.RenderingDescriptor.skinning[3].supportStatus, Is.EqualTo(MmdSkinningDescriptorBuilder.LinearFallbackStatus));
            Assert.That(instance.RenderingDescriptor.skinning[3].linearFallbackToBoneWeights, Is.True);
            Assert.That(instance.RenderingDescriptor.vertexMorphs[0].offsets.Select(offset => offset.vertexIndex), Is.EqualTo(new[] { 0, 3 }));
            Assert.That(instance.Mesh.blendShapeCount, Is.EqualTo(1));

            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "shared-up", weight = 1.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            int sharedUpIndex = instance.Mesh.GetBlendShapeIndex("shared-up");
            Assert.That(sharedUpIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(instance.SkinnedMeshRenderer, Is.Not.Null);
            SkinnedMeshRenderer renderer = instance.SkinnedMeshRenderer!;
            Vector3[] deltaVertices = new Vector3[instance.Mesh.vertexCount];
            Vector3[] deltaNormals = new Vector3[instance.Mesh.vertexCount];
            Vector3[] deltaTangents = new Vector3[instance.Mesh.vertexCount];
            instance.Mesh.GetBlendShapeFrameVertices(sharedUpIndex, 0, deltaVertices, deltaNormals, deltaTangents);
            Assert.That(deltaVertices[0], Is.EqualTo(new Vector3(0.0f, 1.0f, 0.0f)));
            Assert.That(deltaVertices[3], Is.EqualTo(new Vector3(0.0f, 1.0f, 0.0f)));
            Assert.That(renderer.GetBlendShapeWeight(sharedUpIndex), Is.EqualTo(100f).Within(0.001f));
            Assert.That(instance.Mesh.vertices[0], Is.EqualTo(new Vector3(0.0f, 0.0f, 0.0f)));
            Assert.That(instance.Mesh.vertices[3], Is.EqualTo(new Vector3(0.0f, 0.0f, 0.0f)));
        }
        [Test]
        public void VertexOnlyBlendShapeMorphFrameDoesNotUploadMeshVertices()
        {

            MmdModelDefinition model = CreateSharedVertexTwoSubmeshMorphModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            int sharedUpIndex = instance.Mesh.GetBlendShapeIndex("shared-up");
            Assert.That(sharedUpIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(instance.SkinnedMeshRenderer, Is.Not.Null);
            SkinnedMeshRenderer renderer = instance.SkinnedMeshRenderer!;

            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "shared-up", weight = 1.0f });

            MmdUnityMorphApplyTimingSummary timing = MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);

            Assert.That(timing.blendShapePathUsed, Is.True);
            Assert.That(timing.meshUploadRequired, Is.False);
            Assert.That(timing.setVerticesMs, Is.EqualTo(0.0));
            Assert.That(timing.setUvsMs, Is.EqualTo(0.0));
            Assert.That(timing.recalculateBoundsMs, Is.EqualTo(0.0));
            Assert.That(renderer.GetBlendShapeWeight(sharedUpIndex), Is.EqualTo(100f).Within(0.001f));
            Assert.That(instance.Mesh.vertices[0], Is.EqualTo(new Vector3(0.0f, 0.0f, 0.0f)));
        }
        [Test]
        public void BlendShapeVertexMorphWithTextureUvMorphReportsUvMeshUpload()
        {

            MmdModelDefinition model = CreateTextureUvMorphTriangleModel();
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
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
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "uv-shift", weight = 1.0f });

            MmdUnityMorphApplyTimingSummary timing = MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);

            Assert.That(timing.blendShapePathUsed, Is.True);
            Assert.That(timing.meshUploadRequired, Is.True);
            Assert.That(timing.setVerticesMs, Is.EqualTo(0.0));
            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Assert.That(renderer.GetBlendShapeWeight(instance.BlendShapeIndexMap["blink"]), Is.EqualTo(100f).Within(0.001f));
        }
        [Test]
        public void BlendShapeBoundsUseLocalBoundsWithoutRecalculateForResolvedWeightsAboveOne()
        {

            MmdModelDefinition model = CreateGroupMorphTriangleModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "smile", weight = 1.0f });
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 0.5f });

            MmdUnityMorphApplyTimingSummary timing = MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);

            Assert.That(timing.blendShapePathUsed, Is.True);
            Assert.That(timing.localBoundsAssigned, Is.True);
            Assert.That(timing.localBoundsSkipped, Is.False);
            Assert.That(timing.recalculateBoundsMs, Is.EqualTo(0.0));
            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Assert.That(renderer.localBounds.Contains(new Vector3(-1.0f, 2.5f, 0.0f)), Is.True);
        }
        [Test]
        public void BlendShapeLocalBoundsSkipWhenResolvedWeightsAreUnchanged()
        {

            MmdModelDefinition model = CreateGroupMorphTriangleModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "smile", weight = 1.0f });
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 0.5f });

            MmdUnityMorphApplyTimingSummary first = MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);
            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Bounds firstBounds = renderer.localBounds;
            MmdUnityMorphApplyTimingSummary second = MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);
            Bounds secondBounds = renderer.localBounds;

            Assert.That(first.localBoundsAssigned, Is.True);
            Assert.That(first.localBoundsSkipped, Is.False);
            Assert.That(second.localBoundsAssigned, Is.False);
            Assert.That(second.localBoundsSkipped, Is.True);
            Assert.That(second.localBoundsAssignMs, Is.EqualTo(0.0));
            Assert.That(secondBounds.center, Is.EqualTo(firstBounds.center));
            Assert.That(secondBounds.size, Is.EqualTo(firstBounds.size));
        }
        [Test]
        public void BlendShapeLocalBoundsRecalculateWhenResolvedWeightsChangeAfterSkip()
        {

            MmdModelDefinition model = CreateGroupMorphTriangleModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "smile", weight = 1.0f });
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 0.5f });

            MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);
            MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, frame);

            MmdEvaluatedFrame changed = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            changed.morphs.Add(new MmdEvaluatedMorphWeight { name = "smile", weight = 1.0f });
            changed.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 1.0f });
            MmdUnityMorphApplyTimingSummary timing = MmdUnityFrameApplier.ApplyMorphsWithTiming(instance, changed);

            Assert.That(timing.localBoundsAssigned, Is.True);
            Assert.That(timing.localBoundsSkipped, Is.False);
            Assert.That(timing.recalculateBoundsMs, Is.EqualTo(0.0));
            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Assert.That(renderer.localBounds.Contains(new Vector3(-1.0f, 3.0f, 0.0f)), Is.True);
        }
        [Test]
        public void DuplicateNameVertexMorphsBakeDistinctBlendShapesAndShareResolvedWeight()
        {

            MmdModelDefinition model = CreateDuplicateNameVertexMorphModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Mesh.blendShapeCount, Is.EqualTo(2));
            Assert.That(instance.VertexMorphBlendShapes.Select(binding => binding.BlendShapeName), Is.EqualTo(new[] { "0:duplicate", "1:duplicate" }));

            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "duplicate", weight = 0.5f });

            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Assert.That(renderer.GetBlendShapeWeight(instance.VertexMorphBlendShapes[0].BlendShapeIndex), Is.EqualTo(50f).Within(0.001f));
            Assert.That(renderer.GetBlendShapeWeight(instance.VertexMorphBlendShapes[1].BlendShapeIndex), Is.EqualTo(50f).Within(0.001f));
        }
        [Test]
        public void DuplicateVertexMorphOffsetsAggregateIntoBakedBlendShapeDelta()
        {

            MmdModelDefinition model = CreateDuplicateOffsetVertexMorphModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            int blendShapeIndex = instance.Mesh.GetBlendShapeIndex("stacked");
            Assert.That(blendShapeIndex, Is.GreaterThanOrEqualTo(0));

            Vector3[] deltaVertices = new Vector3[instance.Mesh.vertexCount];
            Vector3[] deltaNormals = new Vector3[instance.Mesh.vertexCount];
            Vector3[] deltaTangents = new Vector3[instance.Mesh.vertexCount];
            instance.Mesh.GetBlendShapeFrameVertices(blendShapeIndex, 0, deltaVertices, deltaNormals, deltaTangents);

            Assert.That(deltaVertices[1], Is.EqualTo(new Vector3(0.0f, 1.0f, 0.0f)));
        }
        [Test]
        public void ApplyFrameAppliesTextureUvMorphToMeshUv()
        {

            MmdModelDefinition model = CreateTextureUvMorphTriangleModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            // Step 1: Apply texture UV morph at weight 1.0.
            MmdEvaluatedFrame frame1 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame1.morphs.Add(new MmdEvaluatedMorphWeight { name = "uv-shift", weight = 1.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame1);
            Vector2[] uv1 = instance.Mesh.uv;

            // Vertex 1 source UV moves by (0.25, 0.5), then is converted to Unity viewport UV.
            Assert.That(uv1[1].x, Is.EqualTo(1.25f).Within(0.00001f));
            Assert.That(uv1[1].y, Is.EqualTo(0.5f).Within(0.00001f));
            // Unmorphed vertices keep base viewport UV.
            Assert.That(uv1[0].x, Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(uv1[0].y, Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(uv1[2].x, Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(uv1[2].y, Is.EqualTo(0.0f).Within(0.00001f));

            // Step 2: Apply the same frame again; UVs must not accumulate.
            MmdUnityFrameApplier.ApplyFrame(instance, frame1);
            Vector2[] uv2 = instance.Mesh.uv;
            Assert.That(uv2[1].x, Is.EqualTo(uv1[1].x).Within(0.00001f));
            Assert.That(uv2[1].y, Is.EqualTo(uv1[1].y).Within(0.00001f));

            // Step 3: Apply zero-weight frame to restore base UV.
            MmdEvaluatedFrame frame0 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame0.morphs.Add(new MmdEvaluatedMorphWeight { name = "uv-shift", weight = 0.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame0);
            Vector2[] uv0 = instance.Mesh.uv;

            Assert.That(uv0[1].x, Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(uv0[1].y, Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(uv0[0].x, Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(uv0[0].y, Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(uv0[2].x, Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(uv0[2].y, Is.EqualTo(0.0f).Within(0.00001f));

            // Step 4: Underlying descriptor base UVs are unchanged.
            Assert.That(instance.RenderingDescriptor.vertices[1].uv[0], Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(instance.RenderingDescriptor.vertices[1].uv[1], Is.EqualTo(0.0f).Within(0.00001f));
        }
        [Test]
        public void ApplyTextureUvMorphToSplitSkinnedModelMovesBothCopies()
        {

            MmdModelDefinition model = CreateSharedVertexTwoSubmeshTextureUvMorphModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            // Confirm split structure and duplicated UV morph offsets.
            Assert.That(instance.Mesh.vertexCount, Is.EqualTo(6));
            Assert.That(instance.Mesh.subMeshCount, Is.EqualTo(2));
            Assert.That(
                instance.RenderingDescriptor.uvMorphs[0].offsets.Select(offset => offset.vertexIndex),
                Is.EqualTo(new[] { 0, 3 }));

            // Apply texture UV morph at weight 1.0.
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "uv-shift", weight = 1.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame);
            Vector2[] uv = instance.Mesh.uv;

            // Both split copies of source vertex 0 should receive the same UV delta.
            Assert.That(uv[0].x, Is.EqualTo(0.25f).Within(0.00001f));
            Assert.That(uv[0].y, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(uv[3].x, Is.EqualTo(0.25f).Within(0.00001f));
            Assert.That(uv[3].y, Is.EqualTo(0.5f).Within(0.00001f));
        }
        [Test]
        public void ExistingSkinnedModelRebindAssignsRuntimeOwnedMeshBeforeVertexMorphApplication()
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

            using var sceneScope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance sceneInstance = sceneScope.Instance;
            Mesh originalMesh = sceneInstance.Mesh;
            Vector3 originalVertex = originalMesh.vertices[1];
            MmdUnityModelInstance reboundInstance = MmdUnityModelFactory.CreateExistingSkinnedModelInstance(sceneInstance.Root, model, sourcePath: null);
            Mesh reboundMesh = reboundInstance.Mesh;
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "blink", weight = 1.0f });

            MmdUnityFrameApplier.ApplyFrame(reboundInstance, frame);

            // Existing mesh is preserved (not rebuilt) when it is already valid.
            Assert.That(reboundMesh, Is.SameAs(originalMesh),
                "existing valid mesh must be preserved, not rebuilt with a new mesh");
            SkinnedMeshRenderer sceneRenderer = RequireSkinnedRenderer(sceneInstance);
            Assert.That(sceneRenderer.sharedMesh, Is.SameAs(reboundMesh));
            int blinkIndex = reboundMesh.GetBlendShapeIndex("blink");
            Assert.That(blinkIndex, Is.GreaterThanOrEqualTo(0));
            SkinnedMeshRenderer reboundRenderer = RequireSkinnedRenderer(reboundInstance);
            Assert.That(reboundRenderer.GetBlendShapeWeight(blinkIndex), Is.EqualTo(100f).Within(0.001f));
            Assert.That(originalMesh.vertices[1], Is.EqualTo(originalVertex));
        }
        [Test]
        public void ExistingSkinnedModelRebindAllowsRendererWithoutSharedMesh()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            using var sceneScope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance sceneInstance = sceneScope.Instance;
            Mesh originalMesh = sceneInstance.Mesh;
            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(sceneInstance);
            renderer.sharedMesh = null;

            MmdUnityModelInstance reboundInstance = MmdUnityModelFactory.CreateExistingSkinnedModelInstance(sceneInstance.Root, model, sourcePath: null);
            Mesh reboundMesh = reboundInstance.Mesh;

            Assert.That(reboundMesh, Is.Not.Null);
            Assert.That(reboundMesh, Is.Not.SameAs(originalMesh));
            Assert.That(renderer.sharedMesh, Is.SameAs(reboundMesh));
            Assert.That(reboundInstance.Root, Is.SameAs(sceneInstance.Root));
            Assert.That(reboundInstance.SkinnedMeshRenderer, Is.SameAs(renderer));

            if (reboundMesh != null && reboundMesh != originalMesh)
            {
                UnityEngine.Object.DestroyImmediate(reboundMesh);
            }
        }
        [Test]
        public void ExistingSkinnedModelRebindCollectsPhysicsBodiesFromControllerRoot()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
            {
                index = 0,
                name = "child sphere",
                boneIndex = 1,
                boneName = "child",
                shapeType = "sphere",
                size = new[] { 0.25f, 0.0f, 0.0f },
                position = new[] { 0.0f, 1.0f, 0.0f },
                rotation = new[] { 0.0f, 0.0f, 0.0f },
                mass = 1.0f,
                linearDamping = 0.0f,
                angularDamping = 0.0f,
                friction = 0.0f,
                restitution = 0.0f,
                group = 0,
                mask = 0,
                physicsKind = "dynamic"
            });

            using var sceneScope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance sceneInstance = sceneScope.Instance;
            SkinnedMeshRenderer originalRenderer = RequireSkinnedRenderer(sceneInstance);
            GameObject rendererObject = new GameObject("Renderer");
            rendererObject.transform.SetParent(originalRenderer.transform, worldPositionStays: false);
            SkinnedMeshRenderer movedRenderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
            movedRenderer.sharedMesh = originalRenderer.sharedMesh;
            movedRenderer.sharedMaterials = originalRenderer.sharedMaterials;
            movedRenderer.bones = originalRenderer.bones;
            movedRenderer.rootBone = originalRenderer.rootBone;
            UnityEngine.Object.DestroyImmediate(originalRenderer);

            MmdUnityModelInstance reboundInstance = MmdUnityModelFactory.CreateExistingSkinnedModelInstance(sceneInstance.Root, model, sourcePath: null);

            Assert.That(reboundInstance.SkinnedMeshRenderer, Is.SameAs(movedRenderer));
            Assert.That(reboundInstance.PhysicsBodies, Has.Length.EqualTo(1));
            Assert.That(reboundInstance.PhysicsBodies[0].BodyIndex, Is.EqualTo(0));
        }
    }
}
