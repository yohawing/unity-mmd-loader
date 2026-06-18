#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Yohawing.MmdUnity.Parser;

namespace Yohawing.MmdUnity.Motion
{
    public static class VmdMotionSampler
    {
        private static readonly ConditionalWeakTable<MmdMotionDefinition, MmdMotionSamplingCache> SamplingCaches = new();

        public static MmdSampledMotion Sample(MmdMotionDefinition motion, float frame)
        {
            var sampled = new MmdSampledMotion();
            if (float.IsNaN(frame) || float.IsInfinity(frame))
            {
                throw new System.ArgumentOutOfRangeException(nameof(frame), "Frame must be finite.");
            }

            if (motion == null)
            {
                return sampled;
            }

            MmdMotionSamplingCache cache = SamplingCaches.GetValue(motion, BuildCache);

            foreach (string boneName in cache.BoneNames)
            {
                sampled.Bones[boneName] = VmdBoneSampler.SampleSortedPose(cache.BoneKeyframesByName[boneName], boneName, frame);
            }

            foreach (string morphName in cache.MorphNames)
            {
                sampled.Morphs[morphName] = VmdMorphSampler.SampleSortedWeight(cache.MorphKeyframesByName[morphName], morphName, frame);
            }

            foreach (MmdModelConstraintStateDefinition state in SampleConstraintStates(cache.ModelKeyframes, frame))
            {
                if (!string.IsNullOrWhiteSpace(state.boneName))
                {
                    sampled.IkStates[state.boneName] = state.enabled;
                }
            }

            return sampled;
        }

        private static MmdMotionSamplingCache BuildCache(MmdMotionDefinition motion)
        {
            var boneKeyframesByName = new Dictionary<string, List<MmdBoneKeyframeDefinition>>(System.StringComparer.Ordinal);
            if (motion.boneKeyframes != null)
            {
                foreach (MmdBoneKeyframeDefinition keyframe in motion.boneKeyframes)
                {
                    if (keyframe == null || string.IsNullOrWhiteSpace(keyframe.boneName))
                    {
                        continue;
                    }

                    string boneName = keyframe.boneName;
                    if (!boneKeyframesByName.TryGetValue(boneName, out List<MmdBoneKeyframeDefinition>? keyframes))
                    {
                        keyframes = new List<MmdBoneKeyframeDefinition>();
                        boneKeyframesByName[boneName] = keyframes;
                    }

                    keyframes.Add(keyframe);
                }
            }

            var morphKeyframesByName = new Dictionary<string, List<MmdMorphKeyframeDefinition>>(System.StringComparer.Ordinal);
            if (motion.morphKeyframes != null)
            {
                foreach (MmdMorphKeyframeDefinition keyframe in motion.morphKeyframes)
                {
                    if (keyframe == null || string.IsNullOrWhiteSpace(keyframe.morphName))
                    {
                        continue;
                    }

                    string morphName = keyframe.morphName;
                    if (!morphKeyframesByName.TryGetValue(morphName, out List<MmdMorphKeyframeDefinition>? keyframes))
                    {
                        keyframes = new List<MmdMorphKeyframeDefinition>();
                        morphKeyframesByName[morphName] = keyframes;
                    }

                    keyframes.Add(keyframe);
                }
            }

            return new MmdMotionSamplingCache(
                SortBoneKeyframes(boneKeyframesByName),
                SortMorphKeyframes(morphKeyframesByName),
                motion.modelKeyframes?.ToArray() ?? System.Array.Empty<MmdModelKeyframeDefinition>());
        }

        private static Dictionary<string, List<MmdBoneKeyframeDefinition>> SortBoneKeyframes(
            Dictionary<string, List<MmdBoneKeyframeDefinition>> keyframesByName)
        {
            var sorted = new Dictionary<string, List<MmdBoneKeyframeDefinition>>(keyframesByName.Count, System.StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<MmdBoneKeyframeDefinition>> pair in keyframesByName)
            {
                sorted[pair.Key] = pair.Value.OrderBy(keyframe => keyframe.frame).ToList();
            }

            return sorted;
        }

        private static Dictionary<string, List<MmdMorphKeyframeDefinition>> SortMorphKeyframes(
            Dictionary<string, List<MmdMorphKeyframeDefinition>> keyframesByName)
        {
            var sorted = new Dictionary<string, List<MmdMorphKeyframeDefinition>>(keyframesByName.Count, System.StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<MmdMorphKeyframeDefinition>> pair in keyframesByName)
            {
                sorted[pair.Key] = pair.Value.OrderBy(keyframe => keyframe.frame).ToList();
            }

            return sorted;
        }

        private static IReadOnlyList<MmdModelConstraintStateDefinition> SampleConstraintStates(
            IEnumerable<MmdModelKeyframeDefinition>? keyframes,
            float frame)
        {
            if (keyframes == null)
            {
                return System.Array.Empty<MmdModelConstraintStateDefinition>();
            }

            MmdModelKeyframeDefinition? selected = null;
            foreach (MmdModelKeyframeDefinition keyframe in keyframes)
            {
                if (keyframe == null || keyframe.frame > frame)
                {
                    continue;
                }

                if (selected == null || keyframe.frame >= selected.frame)
                {
                    selected = keyframe;
                }
            }

            return selected?.constraintStates != null
                ? selected.constraintStates
                : System.Array.Empty<MmdModelConstraintStateDefinition>();
        }

        private sealed class MmdMotionSamplingCache
        {
            public MmdMotionSamplingCache(
                Dictionary<string, List<MmdBoneKeyframeDefinition>> boneKeyframesByName,
                Dictionary<string, List<MmdMorphKeyframeDefinition>> morphKeyframesByName,
                IReadOnlyList<MmdModelKeyframeDefinition> modelKeyframes)
            {
                BoneKeyframesByName = boneKeyframesByName;
                MorphKeyframesByName = morphKeyframesByName;
                ModelKeyframes = modelKeyframes;
                BoneNames = boneKeyframesByName.Keys.OrderBy(name => name, System.StringComparer.Ordinal).ToArray();
                MorphNames = morphKeyframesByName.Keys.OrderBy(name => name, System.StringComparer.Ordinal).ToArray();
            }

            public IReadOnlyDictionary<string, List<MmdBoneKeyframeDefinition>> BoneKeyframesByName { get; }

            public IReadOnlyDictionary<string, List<MmdMorphKeyframeDefinition>> MorphKeyframesByName { get; }

            public IReadOnlyList<MmdModelKeyframeDefinition> ModelKeyframes { get; }

            public IReadOnlyList<string> BoneNames { get; }

            public IReadOnlyList<string> MorphNames { get; }
        }
    }
}
