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
        public void CreateStaticModelScalesMeshPositionsButNotNormalsOrRootScale()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.vertices[1].position = new[] { 1.0f, 0.0f, 2.0f };
            model.vertices[1].normal = new[] { 0.0f, 0.0f, 1.0f };

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateStaticModel(model, sourcePath: null, importScale: 2.5f));
            MmdUnityModelInstance instance = scope.Instance;

            Vector3[] vertices = instance.Mesh.vertices;
            Vector3[] normals = instance.Mesh.normals;
            // basis conv then * scale
            Assert.That(vertices[1], Is.EqualTo(new Vector3(-2.5f, 0.0f, -5.0f)));
            // normals only basis, no scale
            Assert.That(normals[1], Is.EqualTo(new Vector3(0.0f, 0.0f, -1.0f)));
            Assert.That(instance.Root.transform.localScale, Is.EqualTo(Vector3.one));
            Transform modelRoot = instance.Root.transform.Find("Model");
            Assert.That(modelRoot, Is.Not.Null);
            Assert.That(modelRoot.localScale, Is.EqualTo(Vector3.one));
            Assert.That(instance.ImportScale, Is.EqualTo(2.5f).Within(0.0001f));
        }
        [Test]
        public void CreateSkinnedModelBoneBindPositionsAndFrameTranslationDeltasUseSameImportScaleWithoutAccumulation()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            // child origin at (0,1,0) in MMD
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model, sourcePath: null, importScale: 0.5f));
            MmdUnityModelInstance instance = scope.Instance;

            // bind local for child: ToUnity( (0,1,0) - (0,0,0) ) * 0.5 = (0,0.5,0)
            Assert.That(instance.BoneTransforms[1].localPosition, Is.EqualTo(new Vector3(0.0f, 0.5f, 0.0f)));

            // apply delta (0,2,0) MMD -> basis (0,2,0)*0.5 = (0,1,0) added to bind
            MmdUnityFrameApplier.ApplyFrame(instance, CreateFrame(
                CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f),
                CreateBonePose(1, "child", 0.0f, 2.0f, 0.0f)));

            Assert.That(instance.BoneTransforms[1].localPosition, Is.EqualTo(new Vector3(0.0f, 1.5f, 0.0f)));

            // re-apply zero delta, back to bind (no accum)
            MmdUnityFrameApplier.ApplyFrame(instance, CreateFrame(
                CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f),
                CreateBonePose(1, "child", 0.0f, 0.0f, 0.0f)));

            Assert.That(instance.BoneTransforms[1].localPosition, Is.EqualTo(new Vector3(0.0f, 0.5f, 0.0f)));
            Assert.That(instance.ImportScale, Is.EqualTo(0.5f).Within(0.0001f));
        }
        [Test]
        public void CreateSkinnedModelVertexMorphAndBlendShapePositionDeltasScaleWithImportScale()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "up",
                type = "vertex",
                panel = "eye",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition
                    {
                        vertexIndex = 1,
                        positionDelta = new[] { 0.0f, 4.0f, 0.0f }
                    }
                }
            });

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model, sourcePath: null, importScale: 0.1f));
            MmdUnityModelInstance instance = scope.Instance;

            int upIndex = instance.Mesh.GetBlendShapeIndex("up");
            Assert.That(upIndex, Is.GreaterThanOrEqualTo(0));

            Vector3[] deltaVertices = new Vector3[instance.Mesh.vertexCount];
            Vector3[] deltaNormals = new Vector3[instance.Mesh.vertexCount];
            Vector3[] deltaTangents = new Vector3[instance.Mesh.vertexCount];
            instance.Mesh.GetBlendShapeFrameVertices(upIndex, 0, deltaVertices, deltaNormals, deltaTangents);

            // delta (0,4,0) MMD -> basis (0,4,0) * 0.1 = (0, 0.4, 0)
            Assert.That(deltaVertices[1], Is.EqualTo(new Vector3(0.0f, 0.4f, 0.0f)));

            // apply and check
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "up", weight = 1.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Assert.That(renderer.GetBlendShapeWeight(upIndex), Is.EqualTo(100f).Within(0.001f));
            // descriptor remains unscaled
            Assert.That(instance.RenderingDescriptor.vertices[1].position[1], Is.EqualTo(0.0f).Within(0.00001f));
        }
        [Test]
        public void CreateSkinnedModelPhysicsDebugBodyAndColliderScaleWhileDescriptorMetadataUnscaled()
        {

            MmdModelDefinition model = CreateMinimalTriangleModel(includeTextureReferences: false);
            model.physics.rigidbodies.Add(new MmdRigidbodyDefinition
            {
                index = 0,
                name = "test-body",
                boneIndex = 1,
                boneName = "child",
                shapeType = "sphere",
                size = new[] { 0.5f, 0.0f, 0.0f },
                position = new[] { 0.0f, 2.0f, 0.0f },
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

            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model, sourcePath: null, importScale: 0.2f));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.PhysicsBodies, Has.Length.EqualTo(1));
            MmdUnityPhysicsBody body = instance.PhysicsBodies[0];
            // local pos: body(0,2,0) - bone origin child(0,1,0) = (0,1,0) MMD basis -> (0,1,0)*0.2 = (0,0.2,0)
            Assert.That(body.transform.localPosition, Is.EqualTo(new Vector3(0.0f, 0.2f, 0.0f)));
            SphereCollider collider = body.GetComponent<SphereCollider>();
            Assert.That(collider.radius, Is.EqualTo(0.1f).Within(0.00001f)); // 0.5 * 0.2

            // descriptor metadata unscaled
            Assert.That(body.DescriptorSize, Is.EqualTo(new Vector3(0.5f, 0.0f, 0.0f)));
            Assert.That(body.DescriptorPosition, Is.EqualTo(new Vector3(0.0f, 2.0f, 0.0f)));
            Assert.That(instance.ImportScale, Is.EqualTo(0.2f).Within(0.0001f));
        }
        [Test]
        public void ApplyFrameExpandsGroupMorphWeightToTargetVertexMorph()
        {

            MmdModelDefinition model = CreateGroupMorphTriangleModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            // Frame with group morph weight only, no direct vertex morph weight.
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 1.0f });

            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            // groupWeight(1.0) * offsetWeight(0.5) = resolved smile weight 0.5 -> BlendShape 50f
            int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
            Assert.That(smileIndex, Is.GreaterThanOrEqualTo(0));
            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Assert.That(renderer.GetBlendShapeWeight(smileIndex), Is.EqualTo(50f).Within(0.001f));
        }
        [Test]
        public void FastRuntimeMorphFrameDoesNotReExpandNativeResolvedGroupMorphWeights()
        {

            MmdModelDefinition model = CreateGroupMorphTriangleModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
            Assert.That(smileIndex, Is.GreaterThanOrEqualTo(0));

            // Reproduce the native fast-runtime morph weight array for a VMD that drives the GROUP
            // morph "happy-face" at 1.0. RuntimeInstance::expand_group_morphs writes the expanded
            // member weight into "smile" (1.0 * 0.5 = 0.5) AND leaves the group morph's own weight
            // (1.0) in the array; the fast binding feeds BOTH into the applier.
            MmdEvaluatedFrame fastFrame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            fastFrame.morphs.Add(new MmdEvaluatedMorphWeight { name = "smile", weight = 0.5f });
            fastFrame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 1.0f });

            // Fast path: group morphs are already resolved by the native runtime, so the applier
            // must NOT expand them again. The native-resolved smile weight stays 0.5 -> BlendShape 50f.
            MmdUnityFrameApplier.ApplyMorphs(instance, fastFrame, groupMorphsResolvedExternally: true);
            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Assert.That(
                renderer.GetBlendShapeWeight(smileIndex),
                Is.EqualTo(50f).Within(0.001f),
                "Fast path must apply the native-resolved member weight as-is, not re-expand the group.");

            // Regression guard: the default (managed) resolution WOULD re-expand the residual group
            // weight, doubling "smile" to 1.0 -> BlendShape 100f. That over-driven blend shape is
            // exactly the bug the fast path must avoid.
            MmdUnityFrameApplier.ApplyMorphs(instance, fastFrame);
            Assert.That(
                renderer.GetBlendShapeWeight(smileIndex),
                Is.EqualTo(100f).Within(0.001f),
                "Managed group resolution double-applies an already-resolved group weight (documents the bug).");
        }
        [Test]
        public void ApplyFrameSumsDirectVertexWeightAndGroupContribution()
        {

            MmdModelDefinition model = CreateGroupMorphTriangleModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            // Frame with both direct vertex morph weight and group morph weight.
            // Direct "smile" at 1.0 + group "happy-face" at 0.5 targeting "smile" with offset 0.5.
            // Final smile weight = 1.0 + 0.5 * 0.5 = 1.25
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "smile", weight = 1.0f });
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 0.5f });

            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            // smile(1.0) + happy-face(0.5) * 0.5 = resolved smile 1.25 -> BlendShape 125f
            int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Assert.That(renderer.GetBlendShapeWeight(smileIndex), Is.EqualTo(125f).Within(0.001f));
            Assert.That(renderer.localBounds.Contains(new Vector3(-1.0f, 2.5f, 0.0f)), Is.True);
        }
        [Test]
        public void RepeatedApplyFrameDoesNotAccumulateGroupMorphDeltas()
        {

            MmdModelDefinition model = CreateGroupMorphTriangleModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 1.0f });

            int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
            Assert.That(smileIndex, Is.GreaterThanOrEqualTo(0));

            // First apply.
            MmdUnityFrameApplier.ApplyFrame(instance, frame);
            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            float firstWeight = renderer.GetBlendShapeWeight(smileIndex);

            // Second apply of the same frame.
            MmdUnityFrameApplier.ApplyFrame(instance, frame);
            float secondWeight = renderer.GetBlendShapeWeight(smileIndex);

            // groupWeight(1.0) * offsetWeight(0.5) = 0.5 -> BlendShape 50f; must not accumulate
            Assert.That(firstWeight, Is.EqualTo(50f).Within(0.001f));
            Assert.That(secondWeight, Is.EqualTo(firstWeight).Within(0.001f));
        }
        [Test]
        public void ZeroGroupMorphWeightRestoresBaseShape()
        {

            MmdModelDefinition model = CreateGroupMorphTriangleModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            int smileIndex = instance.Mesh.GetBlendShapeIndex("smile");
            Assert.That(smileIndex, Is.GreaterThanOrEqualTo(0));

            // Apply with group morph weight.
            MmdEvaluatedFrame frame1 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame1.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 1.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame1);
            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Assert.That(renderer.GetBlendShapeWeight(smileIndex), Is.EqualTo(50f).Within(0.001f));

            // Apply with zero group morph weight.
            MmdEvaluatedFrame frame0 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame0.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 0.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame0);

            Assert.That(renderer.GetBlendShapeWeight(smileIndex), Is.EqualTo(0f).Within(0.001f));
        }
        [Test]
        public void ApplyFrameWithGroupMorphOnSplitSkinnedModelMovesBothCopies()
        {

            MmdModelDefinition model = CreateSplitGroupMorphModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            Assert.That(instance.Mesh.vertexCount, Is.EqualTo(6));
            Assert.That(instance.Mesh.subMeshCount, Is.EqualTo(2));

            // Frame with group morph weight only.
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "happy-face", weight = 1.0f });

            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            // group "happy-face"(1.0) targets "shared-up"(1.0) -> resolved weight 1.0 -> BlendShape 100f
            int sharedUpIndex = instance.Mesh.GetBlendShapeIndex("shared-up");
            Assert.That(sharedUpIndex, Is.GreaterThanOrEqualTo(0));
            SkinnedMeshRenderer renderer = RequireSkinnedRenderer(instance);
            Assert.That(renderer.GetBlendShapeWeight(sharedUpIndex), Is.EqualTo(100f).Within(0.001f));
        }
        [Test]
        public void GroupMorphCycleDetectionThrows()
        {

            MmdModelDefinition model = CreateCycleGroupMorphModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "loop-a", weight = 1.0f });

            var ex = Assert.Throws<InvalidOperationException>(() =>
                MmdUnityFrameApplier.ApplyFrame(instance, frame));

            Assert.That(ex.Message, Does.Contain("cycle"));
        }
        [Test]
        public void ApplyFrameWithMaterialMorphMutatesMaterialProperties()
        {

            MmdModelDefinition model = CreateMaterialMorphTriangleModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            // Base values before morph application.
            Color baseColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            Color baseAmbient = ReadMaterialColor(instance.Materials[0], "_AmbientColor");
            Color baseOutline = ReadMaterialColor(instance.Materials[0], "_OutlineColor");
            float baseOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

            Assert.That(baseColor.r, Is.EqualTo(0.8f).Within(0.00001f));
            Assert.That(baseColor.g, Is.EqualTo(0.2f).Within(0.00001f));
            Assert.That(baseColor.a, Is.EqualTo(1.0f).Within(0.00001f));
            Assert.That(baseAmbient.r, Is.EqualTo(0.1f).Within(0.00001f));
            Assert.That(baseAmbient.g, Is.EqualTo(0.3f).Within(0.00001f));
            Assert.That(baseOutline.a, Is.EqualTo(1.0f).Within(0.00001f));
            // Outline width is the raw PMX edgeSize (screen-space pixel width), base edgeSize = 1.0.
            Assert.That(baseOutlineWidth, Is.EqualTo(1.0f).Within(0.00001f));

            // Apply material morph at weight 1.0.
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "color-change", weight = 1.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            Color morphedColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            Color morphedAmbient = ReadMaterialColor(instance.Materials[0], "_AmbientColor");
            Color morphedOutline = ReadMaterialColor(instance.Materials[0], "_OutlineColor");
            float morphedOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

            // diffuseColor: base(0.8,0.2,0.6) + offset(0.0,0.5,0.0)*1.0 = (0.8,0.7,0.6)
            Assert.That(morphedColor.r, Is.EqualTo(0.8f).Within(0.00001f));
            Assert.That(morphedColor.g, Is.EqualTo(0.7f).Within(0.00001f));
            Assert.That(morphedColor.b, Is.EqualTo(0.6f).Within(0.00001f));
            // alpha: base(1.0) + opacity(-0.3)*1.0 = 0.7
            Assert.That(morphedColor.a, Is.EqualTo(0.7f).Within(0.00001f));

            // ambientColor: base(0.1,0.3,0.5) + offset(0.0,0.0,0.4)*1.0 = (0.1,0.3,0.9)
            Assert.That(morphedAmbient.r, Is.EqualTo(0.1f).Within(0.00001f));
            Assert.That(morphedAmbient.g, Is.EqualTo(0.3f).Within(0.00001f));
            Assert.That(morphedAmbient.b, Is.EqualTo(0.9f).Within(0.00001f));

            // edgeColor: base(0.0,0.0,0.0,1.0) + offset(0.5,0.0,0.0)*1.0 + opacity(0.0)*1.0 = (0.5,0.0,0.0,1.0)
            Assert.That(morphedOutline.r, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(morphedOutline.g, Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(morphedOutline.b, Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(morphedOutline.a, Is.EqualTo(1.0f).Within(0.00001f));

            // edgeSize: base(1.0) + offset(2.0)*1.0 = 3.0 -> _OutlineWidth = raw edgeSize = 3.0
            Assert.That(morphedOutlineWidth, Is.EqualTo(3.0f).Within(0.00001f));
        }
        [Test]
        public void ApplyMaterialMorphTwiceDoesNotAccumulate()
        {

            MmdModelDefinition model = CreateMaterialMorphTriangleModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "color-change", weight = 1.0f });

            // First apply.
            MmdUnityFrameApplier.ApplyFrame(instance, frame);
            Color firstColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            float firstOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

            // Second apply of the same frame.
            MmdUnityFrameApplier.ApplyFrame(instance, frame);
            Color secondColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            float secondOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

            Assert.That(secondColor.r, Is.EqualTo(firstColor.r).Within(0.00001f));
            Assert.That(secondColor.g, Is.EqualTo(firstColor.g).Within(0.00001f));
            Assert.That(secondColor.b, Is.EqualTo(firstColor.b).Within(0.00001f));
            Assert.That(secondColor.a, Is.EqualTo(firstColor.a).Within(0.00001f));
            Assert.That(secondOutlineWidth, Is.EqualTo(firstOutlineWidth).Within(0.00001f));
        }
        [Test]
        public void ApplyMaterialMorphWithZeroWeightRestoresBaseMaterialValues()
        {

            MmdModelDefinition model = CreateMaterialMorphTriangleModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            // Record base values.
            Color baseColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            float baseOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

            // Apply morph at full weight.
            MmdEvaluatedFrame frame1 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame1.morphs.Add(new MmdEvaluatedMorphWeight { name = "color-change", weight = 1.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame1);

            Color morphedColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            float morphedOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");
            Assert.That(morphedColor.g, Is.EqualTo(0.7f).Within(0.00001f), "Material should be morphed before zero-weight apply");
            Assert.That(morphedOutlineWidth, Is.EqualTo(3.0f).Within(0.00001f), "Outline width should be morphed");

            // Apply morph at zero weight.
            MmdEvaluatedFrame frame0 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame0.morphs.Add(new MmdEvaluatedMorphWeight { name = "color-change", weight = 0.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame0);

            Color restoredColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            float restoredOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

            Assert.That(restoredColor.r, Is.EqualTo(baseColor.r).Within(0.00001f));
            Assert.That(restoredColor.g, Is.EqualTo(baseColor.g).Within(0.00001f));
            Assert.That(restoredColor.b, Is.EqualTo(baseColor.b).Within(0.00001f));
            Assert.That(restoredColor.a, Is.EqualTo(baseColor.a).Within(0.00001f));
            Assert.That(restoredOutlineWidth, Is.EqualTo(baseOutlineWidth).Within(0.00001f));
        }
        [Test]
        public void GroupMorphWeightExpansionDrivesMaterialMorph()
        {

            MmdModelDefinition model = CreateGroupMaterialMorphModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            // Frame has no direct material morph weight, only group morph weight.
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "mood-group", weight = 1.0f });

            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            // Group morph "mood-group" targets "color-change" with weight 0.8.
            // Resolved "color-change" weight = 1.0 * 0.8 = 0.8.
            // diffuseColor: base(0.8,0.2,0.6) + offset(0.0,0.5,0.0)*0.8 = (0.8,0.6,0.6)
            Color morphedColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            Assert.That(morphedColor.r, Is.EqualTo(0.8f).Within(0.00001f));
            Assert.That(morphedColor.g, Is.EqualTo(0.6f).Within(0.00001f));
            Assert.That(morphedColor.b, Is.EqualTo(0.6f).Within(0.00001f));
            // alpha: 1.0 + (-0.3)*0.8 = 0.76
            Assert.That(morphedColor.a, Is.EqualTo(0.76f).Within(0.00001f));

            // edgeSize: 1.0 + 2.0*0.8 = 2.6 -> _OutlineWidth = raw edgeSize = 2.6
            float morphedOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");
            Assert.That(morphedOutlineWidth, Is.EqualTo(2.6f).Within(0.00001f));
        }
        [Test]
        public void ApplyMaterialMorphMultiplyMutatesMaterialProperties()
        {

            MmdModelDefinition model = CreateMaterialMorphMultiplyModel();
            using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(model));
            MmdUnityModelInstance instance = scope.Instance;

            // Base values before morph application.
            Color baseColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            Assert.That(baseColor.r, Is.EqualTo(0.8f).Within(0.00001f));
            Assert.That(baseColor.g, Is.EqualTo(0.2f).Within(0.00001f));
            Assert.That(baseColor.b, Is.EqualTo(0.6f).Within(0.00001f));
            Assert.That(baseColor.a, Is.EqualTo(1.0f).Within(0.00001f));

            // Apply multiply morph at weight 1.0.
            MmdEvaluatedFrame frame = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
            frame.morphs.Add(new MmdEvaluatedMorphWeight { name = "multiply-change", weight = 1.0f });
            MmdUnityFrameApplier.ApplyFrame(instance, frame);

            Color morphedColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
            Color morphedAmbient = ReadMaterialColor(instance.Materials[0], "_AmbientColor");
            Color morphedOutline = ReadMaterialColor(instance.Materials[0], "_OutlineColor");
            float morphedOutlineWidth = ReadMaterialFloat(instance.Materials[0], "_OutlineWidth");

            // diffuseColor: base(0.8,0.2,0.6) * offset(0.5,2.0,0.75) = (0.4,0.4,0.45)
            Assert.That(morphedColor.r, Is.EqualTo(0.4f).Within(0.00001f));
            Assert.That(morphedColor.g, Is.EqualTo(0.4f).Within(0.00001f));
            Assert.That(morphedColor.b, Is.EqualTo(0.45f).Within(0.00001f));
            // alpha: 1.0 * 0.5 = 0.5
            Assert.That(morphedColor.a, Is.EqualTo(0.5f).Within(0.00001f));

            // ambientColor: base(0.1,0.3,0.5) * offset(2.0,0.5,1.0) = (0.2,0.15,0.5)
            Assert.That(morphedAmbient.r, Is.EqualTo(0.2f).Within(0.00001f));
            Assert.That(morphedAmbient.g, Is.EqualTo(0.15f).Within(0.00001f));
            Assert.That(morphedAmbient.b, Is.EqualTo(0.5f).Within(0.00001f));

            // edgeColor: base(0,0,0,1) * offset(0.5,0.5,0.5,0) = (0,0,0,0)
            Assert.That(morphedOutline.r, Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(morphedOutline.g, Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(morphedOutline.b, Is.EqualTo(0.0f).Within(0.00001f));
            Assert.That(morphedOutline.a, Is.EqualTo(0.0f).Within(0.00001f));

            // edgeSize: 1.0 * 2.0 = 2.0 -> _OutlineWidth = raw edgeSize = 2.0
            Assert.That(morphedOutlineWidth, Is.EqualTo(2.0f).Within(0.00001f));
        }
        [Test]
        public void MaterialOverrideWriteSetBecomesMaterialMorphBaseAndRestoresOnZeroWeight()
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
                        baseColor = new Color(0.2f, 0.4f, 0.6f, 0.5f),
                        hasAmbientColor = true,
                        ambientColor = new Color(0.3f, 0.2f, 0.1f, 1.0f),
                        hasOutlineColor = true,
                        outlineColor = new Color(0.6f, 0.5f, 0.4f, 0.3f),
                        hasOutlineWidth = true,
                        outlineWidth = 1.25f
                    }
                };

                using var scope = new MmdTestInstanceScope(MmdUnityModelFactory.CreateSkinnedModel(
                    model,
                    sourcePath: null,
                    importScale: 1.0f,
                    preset: MmdMaterialPreset.MmdToon,
                    materialOverride: overrideAsset));
                MmdUnityModelInstance instance = scope.Instance;

                Color overrideBaseColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Assert.That(overrideBaseColor.r, Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(overrideBaseColor.g, Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(overrideBaseColor.b, Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(overrideBaseColor.a, Is.EqualTo(0.5f).Within(0.00001f));
                Color overrideAmbient = ReadMaterialColor(instance.Materials[0], "_AmbientColor");
                Assert.That(overrideAmbient.r, Is.EqualTo(0.3f).Within(0.00001f));
                Assert.That(overrideAmbient.g, Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(overrideAmbient.b, Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(overrideAmbient.a, Is.EqualTo(1.0f).Within(0.00001f));
                Color overrideOutline = ReadMaterialColor(instance.Materials[0], "_OutlineColor");
                Assert.That(overrideOutline.r, Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(overrideOutline.g, Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(overrideOutline.b, Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(overrideOutline.a, Is.EqualTo(0.3f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_OutlineWidth"), Is.EqualTo(1.25f).Within(0.00001f));

                MmdEvaluatedFrame frame1 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame1.morphs.Add(new MmdEvaluatedMorphWeight { name = "multiply-change", weight = 1.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame1);

                Color morphedColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Assert.That(morphedColor.r, Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(morphedColor.g, Is.EqualTo(0.8f).Within(0.00001f));
                Assert.That(morphedColor.b, Is.EqualTo(0.45f).Within(0.00001f));
                Assert.That(morphedColor.a, Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_OutlineWidth"), Is.EqualTo(2.5f).Within(0.00001f));

                MmdEvaluatedFrame frame0 = CreateFrame(CreateBonePose(0, "root", 0.0f, 0.0f, 0.0f));
                frame0.morphs.Add(new MmdEvaluatedMorphWeight { name = "multiply-change", weight = 0.0f });
                MmdUnityFrameApplier.ApplyFrame(instance, frame0);

                Color restoredColor = ReadMaterialColor(instance.Materials[0], "_BaseColor");
                Assert.That(restoredColor.r, Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(restoredColor.g, Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(restoredColor.b, Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(restoredColor.a, Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(ReadMaterialFloat(instance.Materials[0], "_OutlineWidth"), Is.EqualTo(1.25f).Within(0.00001f));
            }
            finally
            {
                if (overrideAsset != null)
                {
                    UnityEngine.Object.DestroyImmediate(overrideAsset);
                }
            }
        }
        private static MmdModelDefinition CreateMaterialMorphMultiplyModel()
        {
            var model = new MmdModelDefinition
            {
                name = "material-morph-multiply"
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
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1,
                name = "child",
                parentIndex = 0,
                transformOrder = 0,
                origin = new[] { 0.0f, 1.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "multiply-target",
                alpha = 1.0f,
                diffuseColor = new[] { 0.8f, 0.2f, 0.6f },
                ambientColor = new[] { 0.1f, 0.3f, 0.5f },
                edgeColor = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                edgeSize = 1.0f,
                drawEdgeFlag = true,
                vertexCount = 3
            });
            // Material morph: "multiply-change" with operation "multiply".
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "multiply-change",
                type = "material",
                panel = "other",
                materialOffsets =
                {
                    new MmdMaterialMorphOffsetDefinition
                    {
                        materialIndex = 0,
                        operation = "multiply",
                        diffuseColor = new[] { 0.5f, 2.0f, 0.75f },
                        diffuseOpacity = 0.5f,
                        ambientColor = new[] { 2.0f, 0.5f, 1.0f },
                        specularColor = new[] { 1.0f, 1.0f, 1.0f },
                        specularPower = 0.0f,
                        edgeColor = new[] { 0.5f, 0.5f, 0.5f },
                        edgeOpacity = 0.0f,
                        edgeSize = 2.0f,
                        diffuseTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        sphereTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        toonTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f }
                    }
                }
            });
            return model;
        }
        private static MmdModelDefinition CreateMaterialMorphTriangleModel()
        {
            var model = new MmdModelDefinition
            {
                name = "material-morph-triangle"
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
            model.bones.Add(new MmdBoneDefinition
            {
                index = 1,
                name = "child",
                parentIndex = 0,
                transformOrder = 0,
                origin = new[] { 0.0f, 1.0f, 0.0f },
                isMovable = true,
                isRotatable = true
            });
            model.vertices.Add(CreateVertex(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            model.vertices.Add(CreateVertex(1, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f));
            model.vertices.Add(CreateVertex(2, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f));
            model.indices.AddRange(new[] { 0, 1, 2 });
            model.materials.Add(new MmdMaterialDefinition
            {
                index = 0,
                name = "morph-target-material",
                alpha = 1.0f,
                diffuseColor = new[] { 0.8f, 0.2f, 0.6f },
                ambientColor = new[] { 0.1f, 0.3f, 0.5f },
                edgeColor = new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                edgeSize = 1.0f,
                drawEdgeFlag = true,
                vertexCount = 3
            });
            // Material morph: "color-change" with operation "add".
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "color-change",
                type = "material",
                panel = "other",
                materialOffsets =
                {
                    new MmdMaterialMorphOffsetDefinition
                    {
                        materialIndex = 0,
                        operation = "add",
                        diffuseColor = new[] { 0.0f, 0.5f, 0.0f },
                        diffuseOpacity = -0.3f,
                        ambientColor = new[] { 0.0f, 0.0f, 0.4f },
                        specularColor = new[] { 1.0f, 1.0f, 1.0f },
                        specularPower = 0.0f,
                        edgeColor = new[] { 0.5f, 0.0f, 0.0f },
                        edgeOpacity = 0.0f,
                        edgeSize = 2.0f,
                        diffuseTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        sphereTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f },
                        toonTextureBlend = new[] { 0.0f, 0.0f, 0.0f, 0.0f }
                    }
                }
            });
            return model;
        }
    }
}
