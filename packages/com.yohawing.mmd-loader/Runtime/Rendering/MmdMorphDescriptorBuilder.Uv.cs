#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd.Parser;

namespace Mmd.Rendering
{
    public static partial class MmdMorphDescriptorBuilder
    {
        [Serializable]
        public sealed class MmdUvMorphDescriptor
        {
            public int morphIndex;
            public string morphName = string.Empty;
            public string morphType = string.Empty;
            public int uvOffsetCount;
            public List<MmdUvMorphOffsetDescriptor> offsets = new();
        }

        [Serializable]
        public sealed class MmdUvMorphOffsetDescriptor
        {
            public int vertexIndex;
            public float[] positionDelta = Array.Empty<float>();
            public bool targetVertexExists;
            public bool allPayloadFinite;
        }

        public static IReadOnlyList<MmdUvMorphDescriptor> BuildUvMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);

            var knownUvTypes = new HashSet<string>(StringComparer.Ordinal)
            {
                "texture", "uva1", "uva2", "uva3", "uva4"
            };

            var vertexIndices = new HashSet<int>(model.vertices.Count);
            foreach (MmdVertexDefinition vertex in model.vertices)
            {
                vertexIndices.Add(vertex.index);
            }

            return model.morphs
                .Where(morph => knownUvTypes.Contains(NormalizeMorphType(morph.type)))
                .OrderBy(morph => morph.index)
                .Select(morph =>
                {
                    string morphType = NormalizeMorphType(morph.type);
                    IReadOnlyList<MmdUvMorphOffsetDefinition> offsets = morph.uvOffsets ?? new List<MmdUvMorphOffsetDefinition>();
                    return new MmdUvMorphDescriptor
                    {
                        morphIndex = morph.index,
                        morphName = morph.name,
                        morphType = morphType,
                        uvOffsetCount = offsets.Count,
                        offsets = offsets
                            .OrderBy(offset => offset.vertexIndex)
                            .Select(offset =>
                            {
                                bool targetExists = vertexIndices.Contains(offset.vertexIndex);
                                float[]? positionDelta = offset.positionDelta;
                                bool allFinite = positionDelta != null &&
                                    positionDelta.Length == 4 &&
                                    positionDelta.All(float.IsFinite);
                                return new MmdUvMorphOffsetDescriptor
                                {
                                    vertexIndex = offset.vertexIndex,
                                    positionDelta = positionDelta != null && positionDelta.Length == 4
                                        ? new[] { positionDelta[0], positionDelta[1], positionDelta[2], positionDelta[3] }
                                        : new float[] { 0, 0, 0, 0 },
                                    targetVertexExists = targetExists,
                                    allPayloadFinite = allFinite
                                };
                            })
                            .ToList()
                    };
                })
                .ToList();
        }
    }
}