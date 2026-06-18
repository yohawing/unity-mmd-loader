#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Yohawing.MmdUnity.Rendering;

namespace Yohawing.MmdUnity.Tests.Contracts
{
    public sealed class MorphEvaluationContractTests
    {
        [Test]
        public void VertexMorphEvaluatorAppliesWeightedMmdSpaceDeltasWithoutMutatingInputs()
        {
            MmdMeshVertexDescriptor[] vertices =
            {
                Vertex(2, 2.0f, 0.0f, 0.0f),
                Vertex(0, 0.0f, 0.0f, 0.0f),
                Vertex(1, 1.0f, 0.0f, 0.0f)
            };
            MmdVertexMorphDescriptor[] morphs =
            {
                Morph(1, "ignored-zero", Offset(0, 100.0f, 0.0f, 0.0f)),
                Morph(0, "smile", Offset(1, 0.0f, 2.0f, -4.0f), Offset(2, 1.0f, 0.0f, 0.0f))
            };
            var weights = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                ["smile"] = 0.5f,
                ["ignored-zero"] = 0.0f
            };

            IReadOnlyList<MmdMeshVertexDescriptor> result =
                MmdVertexMorphEvaluator.ApplyVertexMorphs(vertices, morphs, weights);

            Assert.That(result.Select(vertex => vertex.vertexIndex), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(result[0].position, Is.EqualTo(new[] { 0.0f, 0.0f, 0.0f }).Within(0.00001f));
            Assert.That(result[1].position, Is.EqualTo(new[] { 1.0f, 1.0f, -2.0f }).Within(0.00001f));
            Assert.That(result[2].position, Is.EqualTo(new[] { 2.5f, 0.0f, 0.0f }).Within(0.00001f));

            Assert.That(vertices[1].position, Is.EqualTo(new[] { 0.0f, 0.0f, 0.0f }).Within(0.00001f));
            Assert.That(result[1].normal, Is.EqualTo(vertices[2].normal).Within(0.00001f));
            Assert.That(result[1].uv, Is.EqualTo(vertices[2].uv).Within(0.00001f));
        }

        [Test]
        public void VertexMorphEvaluatorRejectsDuplicateVertexIndices()
        {
            MmdMeshVertexDescriptor[] vertices =
            {
                Vertex(0, 0.0f, 0.0f, 0.0f),
                Vertex(0, 1.0f, 0.0f, 0.0f)
            };

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                MmdVertexMorphEvaluator.ApplyVertexMorphs(
                    vertices,
                    Array.Empty<MmdVertexMorphDescriptor>(),
                    new Dictionary<string, float>()))!;

            Assert.That(ex.Message, Does.Contain("Duplicate vertex descriptor index"));
        }

        [Test]
        public void VertexMorphEvaluatorRejectsNonFiniteWeightsAndVectors()
        {
            InvalidOperationException nonFiniteWeight = Assert.Throws<InvalidOperationException>(() =>
                MmdVertexMorphEvaluator.ApplyVertexMorphs(
                    new[] { Vertex(0, 0.0f, 0.0f, 0.0f) },
                    new[] { Morph(0, "bad-weight", Offset(0, 1.0f, 0.0f, 0.0f)) },
                    new Dictionary<string, float> { ["bad-weight"] = float.NaN }))!;

            Assert.That(nonFiniteWeight.Message, Does.Contain("Morph weight must be finite"));

            InvalidOperationException nonFinitePosition = Assert.Throws<InvalidOperationException>(() =>
                MmdVertexMorphEvaluator.ApplyVertexMorphs(
                    new[] { Vertex(0, float.PositiveInfinity, 0.0f, 0.0f) },
                    Array.Empty<MmdVertexMorphDescriptor>(),
                    new Dictionary<string, float>()))!;

            Assert.That(nonFinitePosition.Message, Does.Contain("must contain only finite values"));
        }

        [Test]
        public void VertexMorphEvaluatorRejectsMissingMorphNameAndOffsetPayload()
        {
            InvalidOperationException missingName = Assert.Throws<InvalidOperationException>(() =>
                MmdVertexMorphEvaluator.ApplyVertexMorphs(
                    new[] { Vertex(0, 0.0f, 0.0f, 0.0f) },
                    new[] { Morph(0, " ", Offset(0, 1.0f, 0.0f, 0.0f)) },
                    new Dictionary<string, float> { [" "] = 1.0f }))!;

            Assert.That(missingName.Message, Does.Contain("Vertex morph name is required"));

            var missingOffsets = new MmdVertexMorphDescriptor
            {
                morphIndex = 0,
                morphName = "bad-offsets",
                offsets = null!
            };
            InvalidOperationException missingPayload = Assert.Throws<InvalidOperationException>(() =>
                MmdVertexMorphEvaluator.ApplyVertexMorphs(
                    new[] { Vertex(0, 0.0f, 0.0f, 0.0f) },
                    new[] { missingOffsets },
                    new Dictionary<string, float> { ["bad-offsets"] = 1.0f }))!;

            Assert.That(missingPayload.Message, Does.Contain("Vertex morph offsets are required"));
        }

        private static MmdMeshVertexDescriptor Vertex(int index, float x, float y, float z)
        {
            return new MmdMeshVertexDescriptor
            {
                vertexIndex = index,
                position = new[] { x, y, z },
                normal = new[] { 0.0f, 1.0f, 0.0f },
                uv = new[] { index, index + 0.5f }
            };
        }

        private static MmdVertexMorphDescriptor Morph(
            int index,
            string name,
            params MmdVertexMorphOffsetDescriptor[] offsets)
        {
            return new MmdVertexMorphDescriptor
            {
                morphIndex = index,
                morphName = name,
                offsets = offsets.ToList()
            };
        }

        private static MmdVertexMorphOffsetDescriptor Offset(int vertexIndex, float x, float y, float z)
        {
            return new MmdVertexMorphOffsetDescriptor
            {
                vertexIndex = vertexIndex,
                positionDelta = new[] { x, y, z }
            };
        }
    }
}
