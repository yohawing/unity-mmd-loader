#nullable enable

using System;
using System.Collections.Generic;

namespace Mmd.Rendering
{
    public static class MmdGroupMorphWeightResolver
    {
        public static IReadOnlyDictionary<string, float> Resolve(
            IReadOnlyDictionary<string, float> frameWeights,
            IReadOnlyList<MmdGroupMorphDescriptor> groupMorphs)
        {
            return MmdCompositeMorphWeightResolver.Resolve(
                frameWeights,
                groupMorphs,
                Array.Empty<MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor>());
        }
    }
}
