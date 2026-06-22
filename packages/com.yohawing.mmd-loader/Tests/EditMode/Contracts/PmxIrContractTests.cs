#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Mmd.Parser;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class PmxIrContractTests
    {
        [Test]
        public void PackageModelManifestDeclaresPmxFixturesAndGoldenOutputs()
        {
            List<ModelFixtureEntry> fixtures = MmdTestFixtures.LoadPackageModelFixtures().ToList();

            Assert.That(fixtures, Is.Not.Empty);
            Assert.That(fixtures.Select(fixture => fixture.format), Does.Contain("pmx"));
            foreach (ModelFixtureEntry fixture in fixtures)
            {
                Assert.That(fixture.id, Is.Not.Empty, "fixture id");
                Assert.That(fixture.ModelPath, Does.Exist, fixture.Context("model path"));
                Assert.That(Path.IsPathRooted(fixture.GoldenPath), Is.True, fixture.Context("golden path rooted"));
                Assert.That(fixture.expected, Is.Not.Null, fixture.Context("expected coverage"));
            }
        }

        public static IEnumerable<TestCaseData> ModelFixtures()
        {
            foreach (ModelFixtureEntry fixture in MmdTestFixtures.LoadPackageModelFixtures())
            {
                yield return new TestCaseData(fixture).SetName("PMX IR fixture " + fixture.id);
            }

            foreach (ModelFixtureEntry fixture in MmdTestFixtures.LoadLocalCorpusModelFixtures().Where(fixture => fixture.format == "pmx"))
            {
                yield return new TestCaseData(fixture).SetName("PMX IR local fixture " + fixture.id);
            }
        }

        [TestCaseSource(nameof(ModelFixtures))]
        public void ModelFixtureMatchesGoldenPmxIr(ModelFixtureEntry fixture)
        {
            MmdTestFixtures.GenerateModelGoldenIfMissing(fixture);

            MmdModelDefinition actual = MmdTestFixtures.ParseModel(fixture);
            MmdModelDefinition golden = MmdTestFixtures.LoadModelGolden(fixture);

            Assert.That(MmdModelValidator.ValidateStructuralModel(actual), Is.Empty);
            AssertExpectedCoverage(fixture, actual);
            AssertModelMatchesGolden(fixture, actual, golden);
        }

        [Test]
        public void StructuralValidatorRejectsMissingTopLevelCollections()
        {
            var model = new MmdModelDefinition
            {
                vertices = null!,
                indices = null!,
                bones = null!,
                morphs = null!,
                materials = null!,
                ik = null!
            };

            IReadOnlyList<string> errors = MmdModelValidator.ValidateStructuralModel(model);

            AssertHasErrorContaining(errors, "vertices");
            AssertHasErrorContaining(errors, "indices");
            AssertHasErrorContaining(errors, "bones");
            AssertHasErrorContaining(errors, "morphs");
            AssertHasErrorContaining(errors, "materials");
            AssertHasErrorContaining(errors, "ik");
        }

        [Test]
        public void StructuralValidatorRejectsInvalidReferencesAndTriangleRanges()
        {
            var model = new MmdModelDefinition();
            model.vertices.Add(new MmdVertexDefinition
            {
                index = 0,
                position = new[] { 0.0f, 0.0f, 0.0f },
                normal = new[] { 0.0f, 1.0f, 0.0f },
                uv = new[] { 0.0f, 0.0f },
                boneIndices = new[] { 99 },
                boneWeights = new[] { 1.0f }
            });
            model.indices.AddRange(new[] { 0, 99 });
            model.bones.Add(new MmdBoneDefinition { index = 0, name = "root", parentIndex = 42, appendParentIndex = 43, origin = new[] { 0.0f, 0.0f, 0.0f } });
            model.ik.Add(new MmdIkDefinition
            {
                boneIndex = 44,
                targetBoneIndex = 45,
                iterationCount = -1,
                angleLimit = -1.0f,
                links =
                {
                    new MmdIkLinkDefinition { boneIndex = 46, hasLimit = true, minimumAngle = new[] { 0.0f }, maximumAngle = new[] { 0.0f } }
                }
            });
            model.materials.Add(new MmdMaterialDefinition { index = 0, name = "mat", vertexCount = 3 });

            IReadOnlyList<string> errors = MmdModelValidator.ValidateStructuralModel(model);

            AssertHasErrorContaining(errors, "vertex bone index");
            AssertHasErrorContaining(errors, "multiple of 3");
            AssertHasErrorContaining(errors, "existing vertex");
            AssertHasErrorContaining(errors, "parentIndex");
            AssertHasErrorContaining(errors, "appendParentIndex");
            AssertHasErrorContaining(errors, "ik boneIndex");
            AssertHasErrorContaining(errors, "ik targetBoneIndex");
            AssertHasErrorContaining(errors, "iterationCount");
            AssertHasErrorContaining(errors, "angleLimit");
            AssertHasErrorContaining(errors, "ik link boneIndex");
            AssertHasErrorContaining(errors, "minimumAngle");
            AssertHasErrorContaining(errors, "maximumAngle");
            AssertHasErrorContaining(errors, "material vertexCount sum");
        }

        [Test]
        public void StructuralValidatorRejectsInvalidMorphPayloads()
        {
            var model = new MmdModelDefinition();
            model.vertices.Add(new MmdVertexDefinition
            {
                index = 0,
                position = new[] { 0.0f, 0.0f, 0.0f },
                normal = new[] { 0.0f, 1.0f, 0.0f },
                uv = new[] { 0.0f, 0.0f },
                boneIndices = new[] { 0 },
                boneWeights = new[] { 1.0f }
            });
            model.bones.Add(new MmdBoneDefinition { index = 0, name = "root", origin = new[] { 0.0f, 0.0f, 0.0f } });
            model.materials.Add(new MmdMaterialDefinition { index = 0, name = "mat", vertexCount = 0 });
            model.morphs.Add(new MmdMorphDefinition
            {
                index = 0,
                name = "bad",
                type = "vertex",
                vertexOffsets =
                {
                    new MmdVertexMorphOffsetDefinition { vertexIndex = 99, positionDelta = new[] { 0.0f } }
                },
                groupOffsets =
                {
                    new MmdGroupMorphOffsetDefinition { morphIndex = 0, weight = float.NaN }
                },
                materialOffsets =
                {
                    new MmdMaterialMorphOffsetDefinition { materialIndex = 99, operation = "bad" }
                },
                boneOffsets =
                {
                    new MmdBoneMorphOffsetDefinition { boneIndex = 99, translation = new[] { 0.0f }, orientation = new[] { 0.0f } }
                },
                flipOffsets =
                {
                    new MmdFlipMorphOffsetDefinition { morphIndex = 0, weight = float.PositiveInfinity }
                },
                impulseOffsets =
                {
                    new MmdImpulseMorphOffsetDefinition { rigidbodyIndex = 99, velocity = new[] { 0.0f }, torque = new[] { 0.0f } }
                }
            });

            IReadOnlyList<string> errors = MmdModelValidator.ValidateStructuralModel(model);

            AssertHasErrorContaining(errors, "vertex offset");
            AssertHasErrorContaining(errors, "positionDelta");
            AssertHasErrorContaining(errors, "group offset self-reference");
            AssertHasErrorContaining(errors, "group offset weight");
            AssertHasErrorContaining(errors, "material offset materialIndex");
            AssertHasErrorContaining(errors, "operation");
            AssertHasErrorContaining(errors, "bone offset boneIndex");
            AssertHasErrorContaining(errors, "bone offset translation");
            AssertHasErrorContaining(errors, "bone offset orientation");
            AssertHasErrorContaining(errors, "flip offset self-reference");
            AssertHasErrorContaining(errors, "flip offset weight");
            AssertHasErrorContaining(errors, "impulse offset rigidbodyIndex");
            AssertHasErrorContaining(errors, "impulse offset velocity");
            AssertHasErrorContaining(errors, "impulse offset torque");
        }

        [Test]
        public void StructuralValidatorAllowsDuplicateMorphNamesButRejectsDuplicateBoneNames()
        {
            var model = new MmdModelDefinition();
            model.bones.Add(new MmdBoneDefinition { index = 0, name = "dup", origin = new[] { 0.0f, 0.0f, 0.0f } });
            model.bones.Add(new MmdBoneDefinition { index = 1, name = "dup", origin = new[] { 0.0f, 0.0f, 0.0f } });
            model.morphs.Add(new MmdMorphDefinition { index = 0, name = "dupMorph", type = "vertex" });
            model.morphs.Add(new MmdMorphDefinition { index = 1, name = "dupMorph", type = "vertex" });

            IReadOnlyList<string> errors = MmdModelValidator.ValidateStructuralModel(model);

            Assert.That(errors.Where(error => error.Contains("duplicate bone name", StringComparison.Ordinal)), Is.Not.Empty);
            Assert.That(errors.Any(error => error.Contains("duplicate morph", StringComparison.Ordinal)), Is.False);
        }

        private static void AssertExpectedCoverage(ModelFixtureEntry fixture, MmdModelDefinition model)
        {
            Assert.That(model.name, Is.Not.Null, fixture.Context("name"));
            Assert.That(model.vertices, Has.Count.GreaterThanOrEqualTo(fixture.expected.minVertices), fixture.Context("vertices"));
            Assert.That(model.indices, Has.Count.GreaterThanOrEqualTo(fixture.expected.minIndices), fixture.Context("indices"));
            Assert.That(model.bones, Has.Count.GreaterThanOrEqualTo(fixture.expected.minBones), fixture.Context("bones"));
            Assert.That(model.materials, Has.Count.GreaterThanOrEqualTo(fixture.expected.minMaterials), fixture.Context("materials"));
        }

        private static void AssertModelMatchesGolden(ModelFixtureEntry fixture, MmdModelDefinition actual, MmdModelDefinition golden)
        {
            Assert.That(actual.name, Is.EqualTo(golden.name), fixture.Context("name"));
            Assert.That(actual.vertices, Has.Count.EqualTo(golden.vertices.Count), fixture.Context("vertices.Count"));
            Assert.That(actual.indices, Has.Count.EqualTo(golden.indices.Count), fixture.Context("indices.Count"));
            Assert.That(actual.bones, Has.Count.EqualTo(golden.bones.Count), fixture.Context("bones.Count"));
            Assert.That(actual.materials, Has.Count.EqualTo(golden.materials.Count), fixture.Context("materials.Count"));
            Assert.That(actual.morphs, Has.Count.EqualTo(golden.morphs.Count), fixture.Context("morphs.Count"));
            Assert.That(actual.ik, Has.Count.EqualTo(golden.ik.Count), fixture.Context("ik.Count"));

            if (actual.vertices.Count > 0)
            {
                AssertVertexMatches(fixture, "vertices[0]", actual.vertices[0], golden.vertices[0]);
                AssertVertexMatches(fixture, "vertices[last]", actual.vertices[actual.vertices.Count - 1], golden.vertices[golden.vertices.Count - 1]);
            }

            if (actual.indices.Count > 0)
            {
                Assert.That(actual.indices[0], Is.EqualTo(golden.indices[0]), fixture.Context("indices[0]"));
                Assert.That(actual.indices[actual.indices.Count - 1], Is.EqualTo(golden.indices[golden.indices.Count - 1]), fixture.Context("indices[last]"));
            }

            if (actual.bones.Count > 0)
            {
                AssertBoneMatches(fixture, "bones[0]", actual.bones[0], golden.bones[0]);
                AssertBoneMatches(fixture, "bones[last]", actual.bones[actual.bones.Count - 1], golden.bones[golden.bones.Count - 1]);
            }

            if (actual.materials.Count > 0)
            {
                AssertMaterialMatches(fixture, "materials[0]", actual.materials[0], golden.materials[0]);
                AssertMaterialMatches(fixture, "materials[last]", actual.materials[actual.materials.Count - 1], golden.materials[golden.materials.Count - 1]);
            }

            if (actual.morphs.Count > 0)
            {
                AssertMorphMatches(fixture, "morphs[0]", actual.morphs[0], golden.morphs[0]);
                AssertMorphMatches(fixture, "morphs[last]", actual.morphs[actual.morphs.Count - 1], golden.morphs[golden.morphs.Count - 1]);
            }

            if (actual.ik.Count > 0)
            {
                AssertIkMatches(fixture, "ik[0]", actual.ik[0], golden.ik[0]);
                AssertIkMatches(fixture, "ik[last]", actual.ik[actual.ik.Count - 1], golden.ik[golden.ik.Count - 1]);
            }
        }

        private static void AssertVertexMatches(ModelFixtureEntry fixture, string field, MmdVertexDefinition actual, MmdVertexDefinition golden)
        {
            Assert.That(actual.index, Is.EqualTo(golden.index), fixture.Context(field + ".index"));
            AssertFloatArray(fixture, field + ".position", actual.position, golden.position);
            AssertFloatArray(fixture, field + ".normal", actual.normal, golden.normal);
            AssertFloatArray(fixture, field + ".uv", actual.uv, golden.uv);
            CollectionAssert.AreEqual(golden.boneIndices, actual.boneIndices, fixture.Context(field + ".boneIndices"));
            AssertFloatArray(fixture, field + ".boneWeights", actual.boneWeights, golden.boneWeights);
        }

        private static void AssertBoneMatches(ModelFixtureEntry fixture, string field, MmdBoneDefinition actual, MmdBoneDefinition golden)
        {
            Assert.That(actual.index, Is.EqualTo(golden.index), fixture.Context(field + ".index"));
            Assert.That(actual.name, Is.EqualTo(golden.name), fixture.Context(field + ".name"));
            Assert.That(actual.parentIndex, Is.EqualTo(golden.parentIndex), fixture.Context(field + ".parentIndex"));
            Assert.That(actual.transformOrder, Is.EqualTo(golden.transformOrder), fixture.Context(field + ".transformOrder"));
            AssertFloatArray(fixture, field + ".origin", actual.origin, golden.origin);
            Assert.That(actual.appendParentIndex, Is.EqualTo(golden.appendParentIndex), fixture.Context(field + ".appendParentIndex"));
            Assert.That(actual.appendRatio, Is.EqualTo(golden.appendRatio), fixture.Context(field + ".appendRatio"));
            Assert.That(actual.appendRotation, Is.EqualTo(golden.appendRotation), fixture.Context(field + ".appendRotation"));
            Assert.That(actual.appendTranslation, Is.EqualTo(golden.appendTranslation), fixture.Context(field + ".appendTranslation"));
            Assert.That(actual.deformAfterPhysics, Is.EqualTo(golden.deformAfterPhysics), fixture.Context(field + ".deformAfterPhysics"));
        }

        private static void AssertMaterialMatches(ModelFixtureEntry fixture, string field, MmdMaterialDefinition actual, MmdMaterialDefinition golden)
        {
            Assert.That(actual.index, Is.EqualTo(golden.index), fixture.Context(field + ".index"));
            Assert.That(actual.name, Is.EqualTo(golden.name), fixture.Context(field + ".name"));
            Assert.That(actual.texture, Is.EqualTo(golden.texture), fixture.Context(field + ".texture"));
            Assert.That(actual.sphereTexture, Is.EqualTo(golden.sphereTexture), fixture.Context(field + ".sphereTexture"));
            Assert.That(actual.toonTexture, Is.EqualTo(golden.toonTexture), fixture.Context(field + ".toonTexture"));
            Assert.That(actual.vertexCount, Is.EqualTo(golden.vertexCount), fixture.Context(field + ".vertexCount"));
        }

        private static void AssertMorphMatches(ModelFixtureEntry fixture, string field, MmdMorphDefinition actual, MmdMorphDefinition golden)
        {
            Assert.That(actual.index, Is.EqualTo(golden.index), fixture.Context(field + ".index"));
            Assert.That(actual.name, Is.EqualTo(golden.name), fixture.Context(field + ".name"));
            Assert.That(actual.type, Is.EqualTo(golden.type), fixture.Context(field + ".type"));
            Assert.That(actual.panel, Is.EqualTo(golden.panel), fixture.Context(field + ".panel"));
            Assert.That(actual.vertexOffsets, Has.Count.EqualTo(golden.vertexOffsets.Count), fixture.Context(field + ".vertexOffsets.Count"));
            if (actual.vertexOffsets.Count > 0)
            {
                AssertMorphOffsetMatches(fixture, field + ".vertexOffsets[0]", actual.vertexOffsets[0], golden.vertexOffsets[0]);
                AssertMorphOffsetMatches(fixture, field + ".vertexOffsets[last]", actual.vertexOffsets[actual.vertexOffsets.Count - 1], golden.vertexOffsets[golden.vertexOffsets.Count - 1]);
            }
        }

        private static void AssertMorphOffsetMatches(ModelFixtureEntry fixture, string field, MmdVertexMorphOffsetDefinition actual, MmdVertexMorphOffsetDefinition golden)
        {
            Assert.That(actual.vertexIndex, Is.EqualTo(golden.vertexIndex), fixture.Context(field + ".vertexIndex"));
            AssertFloatArray(fixture, field + ".positionDelta", actual.positionDelta, golden.positionDelta);
        }

        private static void AssertIkMatches(ModelFixtureEntry fixture, string field, MmdIkDefinition actual, MmdIkDefinition golden)
        {
            Assert.That(actual.boneIndex, Is.EqualTo(golden.boneIndex), fixture.Context(field + ".boneIndex"));
            Assert.That(actual.targetBoneIndex, Is.EqualTo(golden.targetBoneIndex), fixture.Context(field + ".targetBoneIndex"));
            Assert.That(actual.iterationCount, Is.EqualTo(golden.iterationCount), fixture.Context(field + ".iterationCount"));
            Assert.That(actual.angleLimit, Is.EqualTo(golden.angleLimit).Within(0.000001f), fixture.Context(field + ".angleLimit"));
            Assert.That(actual.links, Has.Count.EqualTo(golden.links.Count), fixture.Context(field + ".links.Count"));
            if (actual.links.Count > 0)
            {
                AssertIkLinkMatches(fixture, field + ".links[0]", actual.links[0], golden.links[0]);
                AssertIkLinkMatches(fixture, field + ".links[last]", actual.links[actual.links.Count - 1], golden.links[golden.links.Count - 1]);
            }
        }

        private static void AssertIkLinkMatches(ModelFixtureEntry fixture, string field, MmdIkLinkDefinition actual, MmdIkLinkDefinition golden)
        {
            Assert.That(actual.boneIndex, Is.EqualTo(golden.boneIndex), fixture.Context(field + ".boneIndex"));
            Assert.That(actual.hasLimit, Is.EqualTo(golden.hasLimit), fixture.Context(field + ".hasLimit"));
            AssertFloatArray(fixture, field + ".minimumAngle", actual.minimumAngle, golden.minimumAngle);
            AssertFloatArray(fixture, field + ".maximumAngle", actual.maximumAngle, golden.maximumAngle);
        }

        private static void AssertFloatArray(ModelFixtureEntry fixture, string field, float[] actual, float[] golden)
        {
            Assert.That(actual, Has.Length.EqualTo(golden.Length), fixture.Context(field + ".Length"));
            for (int i = 0; i < actual.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(golden[i]).Within(0.000001f), fixture.Context(field + "[" + i + "]"));
            }
        }

        private static void AssertHasErrorContaining(IReadOnlyList<string> errors, string expected)
        {
            Assert.That(errors.Any(error => error.Contains(expected, StringComparison.Ordinal)), Is.True, expected);
        }
    }
}
