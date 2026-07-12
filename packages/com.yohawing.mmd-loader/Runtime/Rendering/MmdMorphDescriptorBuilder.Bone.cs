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
        public sealed class MmdBoneMorphDescriptor
        {
            public int morphIndex;
            public string morphName = string.Empty;
            public int boneOffsetCount;
            public List<MmdBoneMorphOffsetDescriptor> offsets = new();
        }

        [Serializable]
        public sealed class MmdBoneMorphOffsetDescriptor
        {
            public int boneIndex;
            public string targetBoneName = string.Empty;
            public bool targetBoneExists;
            public float[] translation = Array.Empty<float>();
            public float[] orientation = Array.Empty<float>();
            public bool allPayloadFinite;
        }

        public static IReadOnlyList<MmdBoneMorphDescriptor> BuildBoneMorphs(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            MmdModelValidator.ThrowIfInvalid(model);

            var boneIndexToName = new Dictionary<int, string>(model.bones.Count);
            foreach (MmdBoneDefinition bone in model.bones)
            {
                boneIndexToName[bone.index] = bone.name;
            }

            var boneIndices = new HashSet<int>();
            foreach (MmdBoneDefinition bone in model.bones)
            {
                boneIndices.Add(bone.index);
            }

            return model.morphs
                .Where(morph => string.Equals(NormalizeMorphType(morph.type), "bone", StringComparison.Ordinal))
                .OrderBy(morph => morph.index)
                .Select(morph =>
                {
                    IReadOnlyList<MmdBoneMorphOffsetDefinition> offsets = morph.boneOffsets ?? new List<MmdBoneMorphOffsetDefinition>();
                    return new MmdBoneMorphDescriptor
                    {
                        morphIndex = morph.index,
                        morphName = morph.name,
                        boneOffsetCount = offsets.Count,
                        offsets = offsets
                            .Select(offset =>
                            {
                                string targetName = boneIndexToName.TryGetValue(offset.boneIndex, out string? name)
                                    ? name : string.Empty;
                                bool targetExists = boneIndices.Contains(offset.boneIndex);
                                float[]? translation = offset.translation;
                                float[]? orientation = offset.orientation;
                                bool allFinite = translation != null &&
                                    translation.Length == 3 &&
                                    translation.All(float.IsFinite) &&
                                    orientation != null &&
                                    orientation.Length == 4 &&
                                    orientation.All(float.IsFinite);
                                return new MmdBoneMorphOffsetDescriptor
                                {
                                    boneIndex = offset.boneIndex,
                                    targetBoneName = targetName,
                                    targetBoneExists = targetExists,
                                    translation = translation != null && translation.Length == 3
                                        ? new[] { translation[0], translation[1], translation[2] }
                                        : new float[] { 0, 0, 0 },
                                    orientation = orientation != null && orientation.Length == 4
                                        ? new[] { orientation[0], orientation[1], orientation[2], orientation[3] }
                                        : new float[] { 0, 0, 0, 1 },
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