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
        public sealed class MmdFlipMorphDescriptor
        {
            public int morphIndex;
            public string morphName = string.Empty;
            public int flipOffsetCount;
            public List<MmdFlipMorphOffsetDescriptor> offsets = new();
        }

        [Serializable]
        public sealed class MmdFlipMorphOffsetDescriptor
        {
            public int targetMorphIndex;
            public string targetMorphName = string.Empty;
            public string targetMorphType = string.Empty;
            public float weight;
            public bool finiteWeight;
        }

        public static IReadOnlyList<MmdFlipMorphDescriptor> BuildFlipMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);

            var morphIndexToName = new Dictionary<int, string>(model.morphs.Count);
            var morphIndexToType = new Dictionary<int, string>(model.morphs.Count);
            foreach (MmdMorphDefinition morph in model.morphs)
            {
                morphIndexToName[morph.index] = morph.name;
                morphIndexToType[morph.index] = NormalizeMorphType(morph.type);
            }

            return model.morphs
                .Where(morph => string.Equals(NormalizeMorphType(morph.type), "flip", StringComparison.Ordinal))
                .OrderBy(morph => morph.index)
                .Select(morph =>
                {
                    IReadOnlyList<MmdFlipMorphOffsetDefinition> offsets = morph.flipOffsets ?? new List<MmdFlipMorphOffsetDefinition>();
                    return new MmdFlipMorphDescriptor
                    {
                        morphIndex = morph.index,
                        morphName = morph.name,
                        flipOffsetCount = offsets.Count,
                        offsets = offsets
                            .Select(offset =>
                            {
                                string targetName = morphIndexToName.TryGetValue(offset.morphIndex, out string? name)
                                    ? name : string.Empty;
                                string targetType = morphIndexToType.TryGetValue(offset.morphIndex, out string? type)
                                    ? type : string.Empty;
                                return new MmdFlipMorphOffsetDescriptor
                                {
                                    targetMorphIndex = offset.morphIndex,
                                    targetMorphName = targetName,
                                    targetMorphType = targetType,
                                    weight = offset.weight,
                                    finiteWeight = float.IsFinite(offset.weight)
                                };
                            })
                            .ToList()
                    };
                })
                .ToList();
        }
    }
}