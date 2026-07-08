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
        public sealed class MmdImpulseMorphDescriptor
        {
            public int morphIndex;
            public string morphName = string.Empty;
            public int impulseOffsetCount;
            public List<MmdImpulseMorphOffsetDescriptor> offsets = new();
        }

        [Serializable]
        public sealed class MmdImpulseMorphOffsetDescriptor
        {
            public int rigidbodyIndex = -1;
            public string rigidbodyName = string.Empty;
            public bool targetRigidbodyExists;
            public float[] velocity = Array.Empty<float>();
            public float[] torque = Array.Empty<float>();
            public bool local;
            public bool allPayloadFinite;
        }

        public static IReadOnlyList<MmdImpulseMorphDescriptor> BuildImpulseMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);

            var rigidbodyIndexToName = new Dictionary<int, string>(model.physics.rigidbodies.Count);
            var rigidbodyIndices = new HashSet<int>();
            foreach (MmdRigidbodyDefinition rb in model.physics.rigidbodies)
            {
                rigidbodyIndexToName[rb.index] = rb.name;
                rigidbodyIndices.Add(rb.index);
            }

            return model.morphs
                .Where(morph => string.Equals(NormalizeMorphType(morph.type), "impulse", StringComparison.Ordinal))
                .OrderBy(morph => morph.index)
                .Select(morph =>
                {
                    IReadOnlyList<MmdImpulseMorphOffsetDefinition> offsets = morph.impulseOffsets ?? new List<MmdImpulseMorphOffsetDefinition>();
                    return new MmdImpulseMorphDescriptor
                    {
                        morphIndex = morph.index,
                        morphName = morph.name,
                        impulseOffsetCount = offsets.Count,
                        offsets = offsets
                            .Select(offset =>
                            {
                                string targetName = rigidbodyIndexToName.TryGetValue(offset.rigidbodyIndex, out string? name)
                                    ? name : string.Empty;
                                bool targetExists = rigidbodyIndices.Contains(offset.rigidbodyIndex);
                                float[]? velocity = offset.velocity;
                                float[]? torque = offset.torque;
                                bool allFinite = velocity != null &&
                                    velocity.Length == 3 &&
                                    velocity.All(float.IsFinite) &&
                                    torque != null &&
                                    torque.Length == 3 &&
                                    torque.All(float.IsFinite);
                                return new MmdImpulseMorphOffsetDescriptor
                                {
                                    rigidbodyIndex = offset.rigidbodyIndex,
                                    rigidbodyName = targetName,
                                    targetRigidbodyExists = targetExists,
                                    velocity = velocity != null && velocity.Length == 3
                                        ? new[] { velocity[0], velocity[1], velocity[2] }
                                        : new float[] { 0, 0, 0 },
                                    torque = torque != null && torque.Length == 3
                                        ? new[] { torque[0], torque[1], torque[2] }
                                        : new float[] { 0, 0, 0 },
                                    local = offset.local,
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