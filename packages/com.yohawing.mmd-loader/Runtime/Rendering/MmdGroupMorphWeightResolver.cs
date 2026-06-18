using System;
using System.Collections.Generic;

namespace Mmd.Rendering
{
    public static class MmdGroupMorphWeightResolver
    {
        private const string CycleDetectionMessage = "Group morph cycle detected: {0}";

        public static IReadOnlyDictionary<string, float> Resolve(
            IReadOnlyDictionary<string, float> frameWeights,
            IReadOnlyList<MmdGroupMorphDescriptor> groupMorphs)
        {
            if (frameWeights == null)
            {
                throw new ArgumentNullException(nameof(frameWeights));
            }

            if (groupMorphs == null)
            {
                throw new ArgumentNullException(nameof(groupMorphs));
            }

            if (groupMorphs.Count == 0)
            {
                return CopyAndValidateFrameWeights(frameWeights);
            }

            // Build a name-to-group-morph lookup for quick access.
            var groupByName = new Dictionary<string, MmdGroupMorphDescriptor>(
                groupMorphs.Count,
                StringComparer.Ordinal);
            foreach (MmdGroupMorphDescriptor groupMorph in groupMorphs)
            {
                if (!string.IsNullOrWhiteSpace(groupMorph.morphName))
                {
                    groupByName[groupMorph.morphName] = groupMorph;
                }
            }

            // Start with a copy of the frame weights. Group morph entries will
            // be removed after expansion so they are not passed downstream.
            var result = new Dictionary<string, float>(frameWeights, StringComparer.Ordinal);

            // Validate all input weights are finite.
            foreach (KeyValuePair<string, float> entry in result)
            {
                if (float.IsNaN(entry.Value) || float.IsInfinity(entry.Value))
                {
                    throw new InvalidOperationException(
                        $"Frame morph weight must be finite: {entry.Key}");
                }
            }

            // Expand each active group morph.
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, float> entry in frameWeights)
            {
                string morphName = entry.Key;
                float groupWeight = entry.Value;

                if (groupWeight == 0.0f)
                {
                    continue;
                }

                if (!groupByName.TryGetValue(morphName, out MmdGroupMorphDescriptor? groupMorph))
                {
                    continue;
                }

                // Consume the group morph weight so it does not reach the evaluator.
                result[morphName] = 0.0f;

                visited.Clear();
                visited.Add(morphName);
                ExpandGroupMorph(groupMorph, groupWeight, groupByName, result, visited);
            }

            return result;
        }

        private static Dictionary<string, float> CopyAndValidateFrameWeights(IReadOnlyDictionary<string, float> frameWeights)
        {
            var result = new Dictionary<string, float>(frameWeights, StringComparer.Ordinal);

            foreach (KeyValuePair<string, float> entry in result)
            {
                if (float.IsNaN(entry.Value) || float.IsInfinity(entry.Value))
                {
                    throw new InvalidOperationException(
                        $"Frame morph weight must be finite: {entry.Key}");
                }
            }

            return result;
        }

        private static void ExpandGroupMorph(
            MmdGroupMorphDescriptor groupMorph,
            float groupWeight,
            Dictionary<string, MmdGroupMorphDescriptor> groupByName,
            Dictionary<string, float> result,
            HashSet<string> visited)
        {
            if (groupMorph.offsets == null)
            {
                return;
            }

            for (int offsetIndex = 0; offsetIndex < groupMorph.offsets.Count; offsetIndex++)
            {
                MmdGroupMorphOffsetDescriptor offset = groupMorph.offsets[offsetIndex];
                if (offset.weight == 0.0f)
                {
                    continue;
                }

                if (float.IsNaN(offset.weight) || float.IsInfinity(offset.weight))
                {
                    throw new InvalidOperationException(
                        $"Group morph offset weight must be finite: {groupMorph.morphName} -> target index {offset.targetMorphIndex}");
                }

                float contribution = groupWeight * offset.weight;

                // Look up the target morph name for offset accumulation.
                string targetName = string.IsNullOrWhiteSpace(offset.targetMorphName)
                    ? offset.targetMorphIndex.ToString()
                    : offset.targetMorphName;

                // Add the group contribution to the target morph weight.
                float existingWeight = result.TryGetValue(targetName, out float current) ? current : 0.0f;
                result[targetName] = existingWeight + contribution;

                // If the target is itself a group morph, recurse.
                if (groupByName.TryGetValue(targetName, out MmdGroupMorphDescriptor? targetGroup))
                {
                    if (!visited.Add(targetName))
                    {
                        throw new InvalidOperationException(
                            string.Format(CycleDetectionMessage, FormatCyclePath(visited, targetName)));
                    }

                    ExpandGroupMorph(targetGroup, contribution, groupByName, result, visited);
                    visited.Remove(targetName);
                }
            }
        }

        private static string FormatCyclePath(HashSet<string> visited, string targetName)
        {
            var path = new List<string>(visited.Count + 1);
            foreach (string name in visited)
            {
                path.Add(name);
            }

            path.Add(targetName);
            return string.Join(" -> ", path);
        }
    }
}
