#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Mmd.Parser;

namespace Mmd.Tests
{
    /// <summary>
    /// Characterization (golden) tests for <see cref="MmdModelValidator.ValidateStructuralModel"/>.
    /// Expected diagnostics record the current implementation output verbatim, including order and count,
    /// so that splitting the validator into phases (REF-B2) cannot silently change them.
    /// Each case starts from <see cref="CreateValidModel"/> and breaks it in one or a few places.
    /// Do not edit expectations to match a changed implementation without an explicit compatibility decision.
    /// </summary>
    [TestFixture]
    public sealed class MmdModelValidatorCharacterizationTests
    {
        [Test]
        public void ValidBaselineModelProducesNoDiagnostics()
        {
            AssertDiagnostics(CreateValidModel(), Array.Empty<string>());
            Assert.DoesNotThrow(() => MmdModelValidator.ThrowIfInvalid(CreateValidModel()));
        }

        [Test]
        public void NullModelReturnsSingleDiagnostic()
        {
            AssertDiagnostics(null, new[] { "model is null" });
        }

        [Test]
        public void MissingPhysicsCollectionsAreReportedWithoutThrowing()
        {
            // physics, its rigidbodies and its joints are required like the other model collections;
            // a missing rigidbody list is treated as empty so impulse offsets report unresolved references.
            const string unresolvedImpulse = "morph impulse offset rigidbodyIndex does not reference an existing rigidbody: 6[0] -> 0";

            MmdModelDefinition withoutPhysics = CreateValidModel();
            withoutPhysics.physics = null!;
            AssertDiagnostics(withoutPhysics, new[] { "model physics must not be null", unresolvedImpulse });

            MmdModelDefinition withoutRigidbodies = CreateValidModel();
            withoutRigidbodies.physics.rigidbodies = null!;
            AssertDiagnostics(withoutRigidbodies, new[] { "model physics rigidbodies must not be null", unresolvedImpulse });

            MmdModelDefinition withoutJoints = CreateValidModel();
            withoutJoints.physics.joints = null!;
            AssertDiagnostics(withoutJoints, new[] { "model physics joints must not be null" });
        }

        [TestCaseSource(nameof(Cases))]
        public void DiagnosticsMatchRecordedOutput(Action<MmdModelDefinition> breakModel, string[] expected)
        {
            MmdModelDefinition model = CreateValidModel();
            breakModel(model);

            AssertDiagnostics(model, expected);
        }

        [Test]
        public void DiagnosticsAreNotCappedOrTruncated()
        {
            MmdModelDefinition model = CreateValidModel();
            model.indices.Clear();
            for (int i = 0; i < 300; i++)
            {
                model.indices.Add(99);
            }

            var expected = new List<string>();
            for (int i = 0; i < 300; i++)
            {
                expected.Add($"model index does not reference an existing vertex: {i} -> 99");
            }

            expected.Add("material vertexCount sum must match model indices count: 3 != 300");

            AssertDiagnostics(model, expected.ToArray());
        }

        [Test]
        public void ThrowIfInvalidJoinsDiagnosticsInOrder()
        {
            MmdModelDefinition model = CreateValidModel();
            BreakOnePerPhase(model);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => MmdModelValidator.ThrowIfInvalid(model))!;

            Assert.That(exception.Message, Is.EqualTo(
                "vertex uv must have 2 values: 0; " +
                "bone origin must contain only finite values: 1; " +
                "ik iterationCount must not be negative: 0; " +
                "morph name is required: 4; " +
                "material alpha must be finite and between 0 and 1: 0"));
        }

        public static IEnumerable<TestCaseData> Cases()
        {
            // ---- top-level collections ----
            yield return Case(
                "TopLevel_AllCollectionsNull",
                model =>
                {
                    model.vertices = null!;
                    model.indices = null!;
                    model.bones = null!;
                    model.morphs = null!;
                    model.materials = null!;
                    model.ik = null!;
                },
                "model vertices must not be null",
                "model indices must not be null",
                "model bones must not be null",
                "model morphs must not be null",
                "model materials must not be null",
                "model ik must not be null");

            yield return Case(
                "TopLevel_VerticesNullCascadesToIndexAndMorphReferences",
                model => model.vertices = null!,
                "model vertices must not be null",
                "model index does not reference an existing vertex: 0 -> 0",
                "model index does not reference an existing vertex: 1 -> 1",
                "model index does not reference an existing vertex: 2 -> 2",
                "morph vertex offset does not reference an existing vertex: 0[0] -> 0",
                "morph UV offset does not reference an existing vertex: 3[0] -> 2");

            yield return Case(
                "TopLevel_BonesNullCascadesToVertexIkAndMorphReferences",
                model => model.bones = null!,
                "model bones must not be null",
                "vertex bone index does not reference an existing bone: 0[0] -> 0",
                "vertex bone index does not reference an existing bone: 1[0] -> 0",
                "vertex bone index does not reference an existing bone: 2[0] -> 0",
                "ik boneIndex does not reference an existing bone: 0 -> 2",
                "ik targetBoneIndex does not reference an existing bone: 0 -> 1",
                "ik link boneIndex does not reference an existing bone: 0[0] -> 1",
                "morph bone offset boneIndex does not reference an existing bone: 4[0] -> 1");

            // ---- vertex phase ----
            yield return Case(
                "Vertex_DuplicateIndex",
                model => model.vertices[1].index = 0,
                "duplicate vertex index: 0",
                "model index does not reference an existing vertex: 1 -> 1");

            yield return Case(
                "Vertex_AttributeShapesAndFiniteness",
                model =>
                {
                    model.vertices[0].position = new[] { 0.0f, 0.0f };
                    model.vertices[0].normal = new[] { float.NaN, 0.0f, 0.0f };
                    model.vertices[0].uv = null!;
                    model.vertices[0].boneWeights = new[] { float.PositiveInfinity };
                    model.vertices[2].position = new[] { 0.0f, float.NegativeInfinity, 0.0f };
                    model.vertices[2].normal = null!;
                    model.vertices[2].uv = new[] { 0.0f, float.NaN };
                },
                "vertex position must have 3 values: 0",
                "vertex normal must contain only finite values: 0",
                "vertex uv must have 2 values: 0",
                "vertex boneWeights must contain only finite values: 0",
                "vertex position must contain only finite values: 2",
                "vertex normal must have 3 values: 2",
                "vertex uv must contain only finite values: 2");

            yield return Case(
                "Vertex_BoneIndexWeightLengthMismatch",
                model =>
                {
                    model.vertices[1].boneIndices = new[] { 0, 1 };
                    model.vertices[1].boneWeights = new[] { 1.0f };
                    model.vertices[2].boneWeights = null!;
                },
                "vertex bone index/weight arrays must have the same length: 1",
                "vertex bone index/weight arrays must have the same length: 2");

            yield return Case(
                "Vertex_SdefParameters",
                model =>
                {
                    model.vertices[0].sdefR1 = new[] { 1.0f };
                    model.vertices[1].hasSdefParameters = true;
                    model.vertices[1].skinningMode = " SDEF ";
                    model.vertices[1].sdefC = new[] { 0.0f, 0.0f, 0.0f };
                    model.vertices[1].sdefR0 = new[] { 0.0f, 0.0f, 0.0f };
                    model.vertices[1].sdefR1 = new[] { 0.0f, 0.0f, 0.0f };
                    model.vertices[2].hasSdefParameters = true;
                    model.vertices[2].skinningMode = "bdef2";
                    model.vertices[2].sdefR0 = new[] { 0.0f, 0.0f, float.NaN };
                    model.vertices[2].sdefR1 = new[] { 0.0f, 0.0f, 0.0f };
                },
                "vertex sdefR1 must be empty when SDEF parameters are absent: 0",
                "vertex SDEF parameters require sdef skinningMode: 2",
                "vertex sdefC: 2 must have 3 values",
                "vertex sdefR0: 2 must contain only finite values");

            yield return Case(
                "Vertex_IndicesCountAndReferences",
                model => model.indices.Add(5),
                "model indices count must be a multiple of 3",
                "model index does not reference an existing vertex: 3 -> 5",
                "material vertexCount sum must match model indices count: 3 != 4");

            yield return Case(
                "Vertex_BoneReferenceEmittedAfterBoneLoop",
                model =>
                {
                    model.vertices[0].boneIndices = new[] { 7 };
                    model.vertices[2].boneIndices = new[] { 0, 8 };
                    model.vertices[2].boneWeights = new[] { 0.5f, 0.5f };
                },
                "vertex bone index does not reference an existing bone: 0[0] -> 7",
                "vertex bone index does not reference an existing bone: 2[1] -> 8");

            // ---- bone phase ----
            yield return Case(
                "Bone_DuplicateIndexAndName",
                model => model.bones.Add(new MmdBoneDefinition { index = 1, name = "child", parentIndex = 0, origin = Vec3() }),
                "duplicate bone index: 1",
                "duplicate bone name: child");

            yield return Case(
                "Bone_OriginAndOptionalAxes",
                model =>
                {
                    model.bones[0].origin = new[] { 0.0f, 0.0f };
                    model.bones[0].fixedAxisVector = new[] { float.NaN };
                    model.bones[1].fixedAxis = true;
                    model.bones[1].fixedAxisVector = Array.Empty<float>();
                    model.bones[2].localAxes = true;
                    model.bones[2].localXAxis = new[] { float.NaN, 0.0f, 0.0f };
                    model.bones[2].localZAxis = null!;
                },
                "bone origin must have 3 values: 0",
                "bone fixedAxisVector: 1 must have 3 values when enabled",
                "bone localXAxis: 2 must contain only finite values when enabled",
                "bone localZAxis: 2 must have 3 values when enabled");

            yield return Case(
                "Bone_ParentAndAppendReferences",
                model =>
                {
                    model.bones[1].parentIndex = 9;
                    model.bones[2].appendParentIndex = 8;
                },
                "bone parentIndex does not reference an existing bone: 1 -> 9",
                "bone appendParentIndex does not reference an existing bone: 2 -> 8");

            yield return Case(
                "Bone_ParentCycleReportedPerEntryPath",
                model =>
                {
                    model.bones[0].parentIndex = 1;
                    model.bones[1].parentIndex = 0;
                },
                "bone parent cycle detected at index 0",
                "bone parent cycle detected at index 0");

            // ---- IK phase ----
            yield return Case(
                "Ik_ReferencesAndScalars",
                model =>
                {
                    model.ik[0].boneIndex = 20;
                    model.ik[0].targetBoneIndex = 21;
                    model.ik[0].iterationCount = -1;
                    model.ik[0].angleLimit = -0.5f;
                },
                "ik boneIndex does not reference an existing bone: 0 -> 20",
                "ik targetBoneIndex does not reference an existing bone: 0 -> 21",
                "ik iterationCount must not be negative: 0",
                "ik angleLimit must not be negative: 0");

            yield return Case(
                "Ik_LinksNull",
                model => model.ik[0].links = null!,
                "ik links must not be null: 0");

            yield return Case(
                "Ik_LinkReferencesAndLimits",
                model =>
                {
                    model.ik[0].links[0].boneIndex = 30;
                    model.ik[0].links[0].minimumAngle = new[] { 0.0f, 0.0f };
                    model.ik[0].links[0].maximumAngle = new[] { 0.0f, float.NaN, 0.0f };
                    model.ik[0].links.Add(new MmdIkLinkDefinition { boneIndex = 0, hasLimit = false, minimumAngle = null!, maximumAngle = null! });
                    model.ik.Add(new MmdIkDefinition
                    {
                        boneIndex = 2,
                        targetBoneIndex = 1,
                        iterationCount = 1,
                        angleLimit = 0.1f,
                        links =
                        {
                            new MmdIkLinkDefinition { boneIndex = 0, hasLimit = true, minimumAngle = null!, maximumAngle = Array.Empty<float>() }
                        }
                    });
                },
                "ik link boneIndex does not reference an existing bone: 0[0] -> 30",
                "ik link minimumAngle must have 3 values: 0[0]",
                "ik link maximumAngle must contain only finite values: 0[0]",
                "ik link minimumAngle must have 3 values: 1[0]",
                "ik link maximumAngle must have 3 values: 1[0]");

            // ---- morph phase ----
            yield return Case(
                "Morph_FirstPassFields",
                model =>
                {
                    model.morphs[3].index = 0;
                    model.morphs[4].name = " ";
                    model.morphs[4].type = string.Empty;
                    model.morphs[6].vertexOffsets = null!;
                    model.morphs[6].groupOffsets = null!;
                    model.morphs[6].materialOffsets = null!;
                    model.morphs[6].uvOffsets = null!;
                },
                "duplicate morph index: 0",
                "morph name is required: 4",
                "morph type is required: 4",
                "morph vertexOffsets must not be null: 6",
                "morph groupOffsets must not be null: 6",
                "morph materialOffsets must not be null: 6",
                "morph uvOffsets must not be null: 6");

            yield return Case(
                "Morph_VertexOffsets",
                model =>
                {
                    model.morphs[0].vertexOffsets.Add(new MmdVertexMorphOffsetDefinition { vertexIndex = 9, positionDelta = new[] { 1.0f } });
                    model.morphs[0].vertexOffsets.Add(new MmdVertexMorphOffsetDefinition { vertexIndex = 0, positionDelta = new[] { float.NaN, 0.0f, 0.0f } });
                },
                "morph vertex offset does not reference an existing vertex: 0[1] -> 9",
                "morph vertex offset positionDelta must have 3 values: 0[1]",
                "morph vertex offset positionDelta must contain only finite values: 0[2]");

            yield return Case(
                "Morph_GroupOffsets",
                model =>
                {
                    model.morphs[1].groupOffsets.Add(new MmdGroupMorphOffsetDefinition { morphIndex = 40, weight = 1.0f });
                    model.morphs[1].groupOffsets.Add(new MmdGroupMorphOffsetDefinition { morphIndex = 1, weight = float.NaN });
                },
                "morph group offset target morph index does not exist: 1[1] -> 40",
                "morph group offset self-reference is not allowed: 1[2]",
                "morph group offset weight must be finite: 1[2]");

            yield return Case(
                "Morph_MaterialOffsets",
                model =>
                {
                    MmdMaterialMorphOffsetDefinition first = ValidMaterialOffset(5);
                    first.operation = "replace";
                    first.diffuseColor = new[] { 1.0f, 1.0f };
                    model.morphs[2].materialOffsets.Add(first);

                    MmdMaterialMorphOffsetDefinition second = ValidMaterialOffset(-1);
                    second.ambientColor = new[] { 0.0f, float.NaN, 0.0f };
                    second.sphereTextureBlend = new[] { 0.0f, 0.0f, 0.0f };
                    second.toonTextureBlend = new[] { 0.0f, 0.0f, 0.0f, float.PositiveInfinity };
                    second.diffuseOpacity = float.NaN;
                    second.specularPower = float.PositiveInfinity;
                    second.edgeOpacity = float.NaN;
                    second.edgeSize = float.NegativeInfinity;
                    model.morphs[2].materialOffsets.Add(second);
                },
                "morph material offset materialIndex does not reference an existing material: 2[1] -> 5",
                "morph material offset operation must be add, multiply, or unknown: 2[1] -> replace",
                "morph material offset diffuseColor: 2[1] must have 3 values",
                "morph material offset ambientColor: 2[2] must contain only finite values",
                "morph material offset sphereTextureBlend: 2[2] must have 4 values",
                "morph material offset toonTextureBlend: 2[2] must contain only finite values",
                "morph material offset diffuseOpacity must be finite: 2[2]",
                "morph material offset specularPower must be finite: 2[2]",
                "morph material offset edgeOpacity must be finite: 2[2]",
                "morph material offset edgeSize must be finite: 2[2]");

            yield return Case(
                "Morph_UvOffsets",
                model =>
                {
                    model.morphs[3].uvOffsets.Add(new MmdUvMorphOffsetDefinition { vertexIndex = 8, positionDelta = Vec3() });
                    model.morphs[3].uvOffsets.Add(new MmdUvMorphOffsetDefinition { vertexIndex = 0, positionDelta = new[] { 0.0f, 0.0f, 0.0f, float.NaN } });
                },
                "morph UV offset does not reference an existing vertex: 3[1] -> 8",
                "morph UV offset positionDelta must have 4 values: 3[1]",
                "morph UV offset positionDelta must contain only finite values: 3[2]");

            yield return Case(
                "Morph_BoneOffsets",
                model =>
                {
                    model.morphs[0].boneOffsets = null!;
                    model.morphs[4].boneOffsets.Add(new MmdBoneMorphOffsetDefinition
                    {
                        boneIndex = 11,
                        translation = new[] { 0.0f, 0.0f },
                        orientation = new[] { 0.0f, 0.0f, 0.0f, float.NaN }
                    });
                },
                "morph boneOffsets must not be null: 0",
                "morph bone offset boneIndex does not reference an existing bone: 4[1] -> 11",
                "morph bone offset translation must have 3 values: 4[1]",
                "morph bone offset orientation must contain only finite values: 4[1]");

            yield return Case(
                "Morph_FlipOffsets",
                model =>
                {
                    model.morphs[0].flipOffsets = null!;
                    model.morphs[5].flipOffsets.Add(new MmdFlipMorphOffsetDefinition { morphIndex = 50, weight = float.PositiveInfinity });
                    model.morphs[5].flipOffsets.Add(new MmdFlipMorphOffsetDefinition { morphIndex = 5, weight = 1.0f });
                },
                "morph flipOffsets must not be null: 0",
                "morph flip offset target morph index does not exist: 5[1] -> 50",
                "morph flip offset weight must be finite: 5[1]",
                "morph flip offset self-reference is not allowed: 5[2]");

            yield return Case(
                "Morph_ImpulseOffsets",
                model =>
                {
                    model.morphs[0].impulseOffsets = null!;
                    model.morphs[6].impulseOffsets.Add(new MmdImpulseMorphOffsetDefinition
                    {
                        rigidbodyIndex = 3,
                        velocity = new[] { 0.0f },
                        torque = new[] { 0.0f, float.NaN, 0.0f }
                    });
                },
                "morph impulseOffsets must not be null: 0",
                "morph impulse offset rigidbodyIndex does not reference an existing rigidbody: 6[1] -> 3",
                "morph impulse offset velocity must have 3 values: 6[1]",
                "morph impulse offset torque must contain only finite values: 6[1]");

            yield return Case(
                "Morph_PassMajorOrderAcrossMorphs",
                model =>
                {
                    MmdMorphDefinition morph = model.morphs[0];
                    morph.vertexOffsets.Add(new MmdVertexMorphOffsetDefinition { vertexIndex = 9, positionDelta = Vec3() });
                    morph.groupOffsets.Add(new MmdGroupMorphOffsetDefinition { morphIndex = 77, weight = 1.0f });
                    morph.materialOffsets.Add(ValidMaterialOffset(9));
                    morph.uvOffsets.Add(new MmdUvMorphOffsetDefinition { vertexIndex = 9, positionDelta = Vec4() });
                    morph.boneOffsets.Add(new MmdBoneMorphOffsetDefinition { boneIndex = 9, translation = Vec3(), orientation = Vec4() });
                    morph.flipOffsets.Add(new MmdFlipMorphOffsetDefinition { morphIndex = 77, weight = 1.0f });
                    morph.impulseOffsets.Add(new MmdImpulseMorphOffsetDefinition { rigidbodyIndex = 9, velocity = Vec3(), torque = Vec3() });
                    model.morphs[6].vertexOffsets.Add(new MmdVertexMorphOffsetDefinition { vertexIndex = 99, positionDelta = Vec3() });
                },
                "morph vertex offset does not reference an existing vertex: 0[1] -> 9",
                "morph vertex offset does not reference an existing vertex: 6[0] -> 99",
                "morph group offset target morph index does not exist: 0[0] -> 77",
                "morph material offset materialIndex does not reference an existing material: 0[0] -> 9",
                "morph UV offset does not reference an existing vertex: 0[0] -> 9",
                "morph bone offset boneIndex does not reference an existing bone: 0[0] -> 9",
                "morph flip offset target morph index does not exist: 0[0] -> 77",
                "morph impulse offset rigidbodyIndex does not reference an existing rigidbody: 0[0] -> 9");

            // ---- material phase ----
            yield return Case(
                "Material_FieldChecks",
                model => model.materials.Add(new MmdMaterialDefinition
                {
                    index = 0,
                    name = " ",
                    vertexCount = -3,
                    alpha = 1.5f,
                    sphereTextureMode = "  ",
                    cullingPolicy = "front"
                }),
                "duplicate material index: 0",
                "material vertexCount must not be negative: 0",
                "material name is required: 0",
                "material alpha must be finite and between 0 and 1: 0",
                "material sphereTextureMode must be non-blank when present: 0",
                "material cullingPolicy must be unknown, double-sided, or backface-culling: 0",
                "material vertexCount sum must match model indices count: 0 != 3");

            yield return Case(
                "Material_VertexCountMultipleAndSum",
                model =>
                {
                    model.materials[0].vertexCount = 4;
                    model.materials.Add(new MmdMaterialDefinition { index = 1, name = "second", vertexCount = 0, alpha = float.NaN, cullingPolicy = string.Empty });
                },
                "material vertexCount must be zero or a multiple of 3: 0",
                "material alpha must be finite and between 0 and 1: 1",
                "material vertexCount sum must match model indices count: 4 != 3");

            yield return Case(
                "Material_SumSkippedWithoutIndices",
                model => model.indices.Clear());

            yield return Case(
                "Material_SumSkippedWithoutMaterials",
                model => model.materials.Clear(),
                "morph material offset materialIndex does not reference an existing material: 2[0] -> 0");

            // ---- composite (multiple phases broken at once) ----
            yield return Case(
                "Composite_OnePerPhaseInPhaseOrder",
                BreakOnePerPhase,
                "vertex uv must have 2 values: 0",
                "bone origin must contain only finite values: 1",
                "ik iterationCount must not be negative: 0",
                "morph name is required: 4",
                "material alpha must be finite and between 0 and 1: 0");

            yield return Case(
                "Composite_VertexIndexBoneInterleaving",
                model =>
                {
                    model.vertices[1].normal = null!;
                    model.vertices[2].boneIndices = new[] { 6 };
                    model.indices.Add(7);
                    model.bones[2].name = "root";
                    model.bones[1].parentIndex = 12;
                },
                "vertex normal must have 3 values: 1",
                "model indices count must be a multiple of 3",
                "model index does not reference an existing vertex: 3 -> 7",
                "duplicate bone name: root",
                "vertex bone index does not reference an existing bone: 2[0] -> 6",
                "bone parentIndex does not reference an existing bone: 1 -> 12",
                "material vertexCount sum must match model indices count: 3 != 4");

            yield return Case(
                "Composite_RenumberedTargetsCascadeAcrossPhases",
                model =>
                {
                    model.bones[1].index = 5;
                    model.materials[0].index = 4;
                    model.physics.rigidbodies[0].index = 1;
                },
                "ik targetBoneIndex does not reference an existing bone: 0 -> 1",
                "ik link boneIndex does not reference an existing bone: 0[0] -> 1",
                "morph material offset materialIndex does not reference an existing material: 2[0] -> 0",
                "morph bone offset boneIndex does not reference an existing bone: 4[0] -> 1",
                "morph impulse offset rigidbodyIndex does not reference an existing rigidbody: 6[0] -> 0");
        }

        private static void BreakOnePerPhase(MmdModelDefinition model)
        {
            model.materials[0].alpha = -1.0f;
            model.morphs[4].name = string.Empty;
            model.ik[0].iterationCount = -2;
            model.bones[1].origin = new[] { float.NaN, 0.0f, 0.0f };
            model.vertices[0].uv = new[] { 0.0f };
        }

        private static TestCaseData Case(string name, Action<MmdModelDefinition> breakModel, params string[] expected)
        {
            return new TestCaseData(breakModel, expected).SetName("ModelValidatorCharacterization_" + name);
        }

        private static void AssertDiagnostics(MmdModelDefinition? model, string[] expected)
        {
            IReadOnlyList<string> actual = MmdModelValidator.ValidateStructuralModel(model);
            Assert.That(
                actual,
                Is.EqualTo(expected),
                "actual diagnostics (" + actual.Count + "):\n" + string.Join("\n", actual));
        }

        /// <summary>
        /// A structurally valid model that exercises every validated collection:
        /// 3 vertices / 1 triangle, 3 bones (root, child, ik), 1 IK with a limited link,
        /// 1 material, 1 rigidbody, and one morph per offset kind
        /// (0 vertex, 1 group, 2 material, 3 uv, 4 bone, 5 flip, 6 impulse).
        /// </summary>
        private static MmdModelDefinition CreateValidModel()
        {
            var model = new MmdModelDefinition { name = "validator-characterization" };
            for (int i = 0; i < 3; i++)
            {
                model.vertices.Add(new MmdVertexDefinition
                {
                    index = i,
                    position = Vec3(),
                    normal = new[] { 0.0f, 1.0f, 0.0f },
                    uv = new[] { 0.0f, 0.0f },
                    skinningMode = "bdef1",
                    boneIndices = new[] { 0 },
                    boneWeights = new[] { 1.0f }
                });
            }

            model.indices.AddRange(new[] { 0, 1, 2 });

            model.bones.Add(new MmdBoneDefinition { index = 0, name = "root", parentIndex = -1, origin = Vec3() });
            model.bones.Add(new MmdBoneDefinition { index = 1, name = "child", parentIndex = 0, origin = Vec3() });
            model.bones.Add(new MmdBoneDefinition { index = 2, name = "ik", parentIndex = 0, origin = Vec3() });

            model.ik.Add(new MmdIkDefinition
            {
                boneIndex = 2,
                targetBoneIndex = 1,
                iterationCount = 10,
                angleLimit = 0.5f,
                links =
                {
                    new MmdIkLinkDefinition { boneIndex = 1, hasLimit = true, minimumAngle = new[] { -1.0f, 0.0f, 0.0f }, maximumAngle = Vec3() }
                }
            });

            model.materials.Add(new MmdMaterialDefinition { index = 0, name = "mat", vertexCount = 3 });
            model.physics.rigidbodies.Add(new MmdRigidbodyDefinition { index = 0, name = "rb", boneIndex = 0 });

            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "vertex",
                type = "vertex",
                vertexOffsets = { new MmdVertexMorphOffsetDefinition { vertexIndex = 0, positionDelta = new[] { 0.0f, 0.1f, 0.0f } } }
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 1,
                name = "group",
                type = "group",
                groupOffsets = { new MmdGroupMorphOffsetDefinition { morphIndex = 0, weight = 1.0f } }
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 2,
                name = "material",
                type = "material",
                materialOffsets = { ValidMaterialOffset(0) }
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 3,
                name = "uv",
                type = "uv",
                uvOffsets = { new MmdUvMorphOffsetDefinition { vertexIndex = 2, positionDelta = Vec4() } }
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 4,
                name = "bone",
                type = "bone",
                boneOffsets = { new MmdBoneMorphOffsetDefinition { boneIndex = 1, translation = Vec3(), orientation = new[] { 0.0f, 0.0f, 0.0f, 1.0f } } }
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 5,
                name = "flip",
                type = "flip",
                flipOffsets = { new MmdFlipMorphOffsetDefinition { morphIndex = 0, weight = 1.0f } }
            });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 6,
                name = "impulse",
                type = "impulse",
                impulseOffsets = { new MmdImpulseMorphOffsetDefinition { rigidbodyIndex = 0, velocity = Vec3(), torque = Vec3() } }
            });

            return model;
        }

        private static MmdMaterialMorphOffsetDefinition ValidMaterialOffset(int materialIndex)
        {
            return new MmdMaterialMorphOffsetDefinition
            {
                materialIndex = materialIndex,
                operation = "multiply",
                diffuseColor = Vec3(),
                ambientColor = Vec3(),
                specularColor = Vec3(),
                edgeColor = Vec3(),
                diffuseTextureBlend = Vec4(),
                sphereTextureBlend = Vec4(),
                toonTextureBlend = Vec4()
            };
        }

        private static float[] Vec3()
        {
            return new[] { 0.0f, 0.0f, 0.0f };
        }

        private static float[] Vec4()
        {
            return new[] { 0.0f, 0.0f, 0.0f, 0.0f };
        }
    }
}
