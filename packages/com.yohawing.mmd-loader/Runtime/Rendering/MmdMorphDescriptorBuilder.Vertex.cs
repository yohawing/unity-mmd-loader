#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd.Parser;

namespace Mmd.Rendering
{
    public static partial class MmdMorphDescriptorBuilder
    {
        public static IReadOnlyList<MmdVertexMorphDescriptor> BuildVertexMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);
            return model.morphs
                .Where(morph => string.Equals(NormalizeMorphType(morph.type), "vertex", StringComparison.Ordinal))
                .OrderBy(morph => morph.index)
                .Select(morph => new MmdVertexMorphDescriptor
                {
                    morphIndex = morph.index,
                    morphName = morph.name,
                    offsets = morph.vertexOffsets
                        .OrderBy(offset => offset.vertexIndex)
                        .Select(offset => new MmdVertexMorphOffsetDescriptor
                        {
                            vertexIndex = offset.vertexIndex,
                            positionDelta = offset.positionDelta.ToArray()
                        })
                        .ToList()
                })
                .ToList();
        }
    }
}