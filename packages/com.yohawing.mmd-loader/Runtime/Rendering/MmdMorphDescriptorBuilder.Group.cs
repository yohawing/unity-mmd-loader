#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Mmd.Parser;

namespace Mmd.Rendering
{
    public static partial class MmdMorphDescriptorBuilder
    {
        public static IReadOnlyList<MmdGroupMorphDescriptor> BuildGroupMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);

            var morphIndexToName = new Dictionary<int, string>(model.morphs.Count);
            foreach (MmdMorphDefinition morph in model.morphs)
            {
                morphIndexToName[morph.index] = morph.name;
            }

            return model.morphs
                .Where(morph => string.Equals(NormalizeMorphType(morph.type), "group", StringComparison.Ordinal))
                .OrderBy(morph => morph.index)
                .Select(morph => new MmdGroupMorphDescriptor
                {
                    morphIndex = morph.index,
                    morphName = morph.name,
                    offsets = (morph.groupOffsets ?? new List<MmdGroupMorphOffsetDefinition>())
                        .OrderBy(offset => offset.morphIndex)
                        .Select(offset =>
                        {
                            string targetName = morphIndexToName.TryGetValue(offset.morphIndex, out string? name)
                                ? name
                                : string.Empty;
                            return new MmdGroupMorphOffsetDescriptor
                            {
                                targetMorphIndex = offset.morphIndex,
                                targetMorphName = targetName,
                                weight = offset.weight
                            };
                        })
                        .ToList()
                })
                .ToList();
        }
    }
}