#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd.Parser;

namespace Mmd.Rendering
{
    internal static partial class MmdMorphDescriptorBuilder
    {
        public static IReadOnlyList<MmdUvMorphDescriptor> BuildUvMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            return BuildUvMorphsFromValidated(model);
        }

        /// <summary>
        /// Same as <see cref="BuildUvMorphs"/> for a model the caller has already validated with
        /// <see cref="MmdModelValidator.ThrowIfInvalid"/>; skips the repeated validation.
        /// </summary>
        internal static IReadOnlyList<MmdUvMorphDescriptor> BuildUvMorphsFromValidated(MmdModelDefinition model)
        {
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
