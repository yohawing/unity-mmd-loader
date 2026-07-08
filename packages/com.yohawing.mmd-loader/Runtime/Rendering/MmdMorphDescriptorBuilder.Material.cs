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
        public sealed class MmdMaterialMorphDescriptor
        {
            public int morphIndex;
            public string morphName = string.Empty;
            public List<MmdMaterialMorphOffsetDescriptor> offsets = new();
        }

        [Serializable]
        public sealed class MmdMaterialMorphOffsetDescriptor
        {
            public int materialIndex = -1;
            public string operation = "unknown";
            public float[] diffuseColor = Array.Empty<float>();
            public float diffuseOpacity = 1.0f;
            public float[] ambientColor = Array.Empty<float>();
            public float[] edgeColor = Array.Empty<float>();
            public float edgeOpacity = 1.0f;
            public float edgeSize;
            public bool allOffsetsFinite;
        }

        public static IReadOnlyList<MmdMaterialMorphDescriptor> BuildMaterialMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);

            return model.morphs
                .Where(morph => string.Equals(NormalizeMorphType(morph.type), "material", StringComparison.Ordinal))
                .OrderBy(morph => morph.index)
                .Select(morph =>
                {
                    IReadOnlyList<MmdMaterialMorphOffsetDefinition> offsets = morph.materialOffsets ?? new List<MmdMaterialMorphOffsetDefinition>();
                    return new MmdMaterialMorphDescriptor
                    {
                        morphIndex = morph.index,
                        morphName = morph.name,
                        offsets = offsets
                            .OrderBy(offset => offset.materialIndex)
                            .Select(offset =>
                            {
                                float[] diffuseColor = offset.diffuseColor ?? Array.Empty<float>();
                                float[] ambientColor = offset.ambientColor ?? Array.Empty<float>();
                                float[] edgeColor = offset.edgeColor ?? Array.Empty<float>();
                                bool allFinite =
                                    diffuseColor.Length == 3 &&
                                    diffuseColor.All(float.IsFinite) &&
                                    float.IsFinite(offset.diffuseOpacity) &&
                                    ambientColor.Length == 3 &&
                                    ambientColor.All(float.IsFinite) &&
                                    edgeColor.Length == 3 &&
                                    edgeColor.All(float.IsFinite) &&
                                    float.IsFinite(offset.edgeOpacity) &&
                                    float.IsFinite(offset.edgeSize);
                                return new MmdMaterialMorphOffsetDescriptor
                                {
                                    materialIndex = offset.materialIndex,
                                    operation = offset.operation,
                                    diffuseColor = diffuseColor.Length == 3
                                        ? new[] { diffuseColor[0], diffuseColor[1], diffuseColor[2] }
                                        : Array.Empty<float>(),
                                    diffuseOpacity = offset.diffuseOpacity,
                                    ambientColor = ambientColor.Length == 3
                                        ? new[] { ambientColor[0], ambientColor[1], ambientColor[2] }
                                        : Array.Empty<float>(),
                                    edgeColor = edgeColor.Length >= 3
                                        ? new[] { edgeColor[0], edgeColor[1], edgeColor[2] }
                                        : Array.Empty<float>(),
                                    edgeOpacity = offset.edgeOpacity,
                                    edgeSize = offset.edgeSize,
                                    allOffsetsFinite = allFinite
                                };
                            })
                            .ToList()
                    };
                })
                .ToList();
        }
    }
}