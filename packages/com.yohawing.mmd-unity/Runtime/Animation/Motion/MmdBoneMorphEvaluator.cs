#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Yohawing.MmdUnity.Parser;
using Yohawing.MmdUnity.Pose;
using Yohawing.MmdUnity.Rendering;

namespace Yohawing.MmdUnity.Motion
{
    /// <summary>
    /// Evaluates bone morph offsets from the sampled motion's morph weights
    /// and produces a new <see cref="MmdSampledMotion"/> with adjusted bone transforms.
    ///
    /// Resolves composite group/flip morph weights before applying bone offsets
    /// so that group and flip morphs targeting bone morphs drive the transform pipeline.
    ///
    /// Preserves sampled motion morph weights and IK states.
    /// Does not mutate the input motion.
    /// </summary>
    public static class MmdBoneMorphEvaluator
    {
        private static readonly ConditionalWeakTable<MmdModelDefinition, BoneMorphPlan> _planCache = new();

        public static MmdSampledMotion ApplyBoneMorphs(
            MmdModelDefinition model,
            MmdSampledMotion sampledMotion)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (sampledMotion == null)
            {
                throw new ArgumentNullException(nameof(sampledMotion));
            }

            BoneMorphPlan plan = _planCache.GetValue(model, BuildPlan);
            if (plan.IsEmpty)
            {
                return sampledMotion;
            }

            // Resolve composite (group/flip) morph weights so that group/flip morphs
            // targeting bone morphs drive the transform pipeline.
            IReadOnlyDictionary<string, float> resolvedWeights = ResolveCompositeWeights(plan, sampledMotion.Morphs);
            if (!HasBoneMorphContribution(plan, resolvedWeights))
            {
                return sampledMotion;
            }

            // Copy-on-write: keep unmodified poses shared and replace only affected bones.
            MmdSampledMotion result = ShallowCopyMotion(sampledMotion);
            foreach (KeyValuePair<string, float> kvp in resolvedWeights)
            {
                string morphName = kvp.Key;
                float weight = kvp.Value;

                if (weight == 0.0f || float.IsNaN(weight) || float.IsInfinity(weight))
                {
                    continue;
                }

                if (!plan.BoneMorphOffsetsByName.TryGetValue(morphName, out List<BoneMorphOffset>? offsets))
                {
                    continue;
                }

                foreach (BoneMorphOffset offset in offsets)
                {
                    if (!result.Bones.TryGetValue(offset.BoneName, out MmdBonePoseSample bonePose))
                    {
                        continue;
                    }

                    float[] currentTranslation = bonePose.Translation;
                    float[] newTranslation =
                    {
                        currentTranslation[0] + offset.Translation[0] * weight,
                        currentTranslation[1] + offset.Translation[1] * weight,
                        currentTranslation[2] + offset.Translation[2] * weight
                    };

                    float[] weightedRotation = MmdQuaternionMath.Slerp(
                        MmdBonePoseSample.Identity.Rotation,
                        offset.Orientation,
                        weight);
                    float[] newRotation = MmdQuaternionMath.Multiply(bonePose.Rotation, weightedRotation);

                    result.Bones[offset.BoneName] = new MmdBonePoseSample(newTranslation, newRotation);
                }
            }

            return result;
        }

        /// <summary>
        /// Resolves composite group/flip morph weights into per-morph accumulated weights
        /// using the same expansion logic as <see cref="MmdCompositeMorphWeightResolver.Resolve"/>.
        /// Descriptors are built via <see cref="MmdMorphDescriptorBuilder"/> to avoid duplication.
        /// </summary>
        private static IReadOnlyDictionary<string, float> ResolveCompositeWeights(
            BoneMorphPlan plan,
            IReadOnlyDictionary<string, float> rawWeights)
        {
            return MmdCompositeMorphWeightResolver.Resolve(
                rawWeights,
                plan.GroupMorphs,
                plan.FlipMorphs);
        }

        private static bool HasBoneMorphContribution(
            BoneMorphPlan plan,
            IReadOnlyDictionary<string, float> resolvedWeights)
        {
            foreach (KeyValuePair<string, float> pair in resolvedWeights)
            {
                float weight = pair.Value;
                if (weight == 0.0f || float.IsNaN(weight) || float.IsInfinity(weight))
                {
                    continue;
                }

                if (plan.BoneMorphOffsetsByName.ContainsKey(pair.Key))
                {
                    return true;
                }
            }

            return false;
        }

        private static MmdSampledMotion ShallowCopyMotion(MmdSampledMotion source)
        {
            var copy = new MmdSampledMotion();

            foreach (KeyValuePair<string, MmdBonePoseSample> kvp in source.Bones)
            {
                copy.Bones[kvp.Key] = kvp.Value;
            }

            foreach (KeyValuePair<string, float> kvp in source.Morphs)
            {
                copy.Morphs[kvp.Key] = kvp.Value;
            }

            foreach (KeyValuePair<string, bool> kvp in source.IkStates)
            {
                copy.IkStates[kvp.Key] = kvp.Value;
            }

            return copy;
        }

        private static BoneMorphPlan BuildPlan(MmdModelDefinition model)
        {
            var boneIndexToName = new Dictionary<int, string>(model.bones.Count);
            foreach (MmdBoneDefinition bone in model.bones)
            {
                boneIndexToName[bone.index] = bone.name;
            }

            var offsetsByName = new Dictionary<string, List<BoneMorphOffset>>(model.morphs.Count, StringComparer.Ordinal);
            foreach (MmdMorphDefinition morph in model.morphs)
            {
                if (!string.Equals(morph.type, "bone", StringComparison.Ordinal))
                {
                    continue;
                }

                IReadOnlyList<MmdBoneMorphOffsetDefinition> boneOffsets = morph.boneOffsets != null
                    ? morph.boneOffsets
                    : Array.Empty<MmdBoneMorphOffsetDefinition>();
                foreach (MmdBoneMorphOffsetDefinition offset in boneOffsets)
                {
                    if (!boneIndexToName.TryGetValue(offset.boneIndex, out string? targetBoneName))
                    {
                        continue;
                    }

                    if (!offsetsByName.TryGetValue(morph.name, out List<BoneMorphOffset>? offsets))
                    {
                        offsets = new List<BoneMorphOffset>(boneOffsets.Count);
                        offsetsByName[morph.name] = offsets;
                    }

                    offsets.Add(new BoneMorphOffset(targetBoneName, offset.translation, offset.orientation));
                }
            }

            if (offsetsByName.Count == 0)
            {
                return new BoneMorphPlan(
                    offsetsByName,
                    Array.Empty<MmdGroupMorphDescriptor>(),
                    Array.Empty<MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor>());
            }

            return new BoneMorphPlan(
                offsetsByName,
                MmdMorphDescriptorBuilder.BuildGroupMorphs(model),
                MmdMorphDescriptorBuilder.BuildFlipMorphs(model));
        }

        private sealed class BoneMorphPlan
        {
            public BoneMorphPlan(
                IReadOnlyDictionary<string, List<BoneMorphOffset>> boneMorphOffsetsByName,
                IReadOnlyList<MmdGroupMorphDescriptor> groupMorphs,
                IReadOnlyList<MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor> flipMorphs)
            {
                BoneMorphOffsetsByName = boneMorphOffsetsByName;
                GroupMorphs = groupMorphs;
                FlipMorphs = flipMorphs;
            }

            public bool IsEmpty => BoneMorphOffsetsByName.Count == 0;

            public IReadOnlyDictionary<string, List<BoneMorphOffset>> BoneMorphOffsetsByName { get; }

            public IReadOnlyList<MmdGroupMorphDescriptor> GroupMorphs { get; }

            public IReadOnlyList<MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor> FlipMorphs { get; }
        }

        private readonly struct BoneMorphOffset
        {
            public BoneMorphOffset(string boneName, float[] translation, float[] orientation)
            {
                BoneName = boneName;
                Translation = translation;
                Orientation = orientation;
            }

            public string BoneName { get; }

            public float[] Translation { get; }

            public float[] Orientation { get; }
        }
    }
}
