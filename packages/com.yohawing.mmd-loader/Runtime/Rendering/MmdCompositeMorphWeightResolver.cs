#nullable enable

using System;
using System.Collections.Generic;

namespace Mmd.Rendering
{
    /// <summary>
    /// Resolves composite group/flip morph weights by distributing each active
    /// composite morph's weight to its target morphs. Recursive expansion through
    /// referenced group and flip morphs is allowed; cycles throw
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    public static class MmdCompositeMorphWeightResolver
    {
        private const string CycleDetectionMessage = "Composite morph cycle detected: {0}";

        /// <summary>
        /// Resolves frame morph weights by expanding active composite morphs (and
        /// recursively following group or flip morph targets) into additive
        /// contributions to downstream morph weights.
        /// </summary>
        /// <param name="frameWeights">Raw frame morph weights (before group/flip expansion).</param>
        /// <param name="groupMorphs">Group morph descriptors for composite target lookup.</param>
        /// <param name="flipMorphs">Flip morph descriptors for composite target lookup.</param>
        /// <returns>Resolved weight map with directly active composite morph weights consumed.</returns>
        public static IReadOnlyDictionary<string, float> Resolve(
            IReadOnlyDictionary<string, float> frameWeights,
            IReadOnlyList<MmdGroupMorphDescriptor> groupMorphs,
            IReadOnlyList<MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor> flipMorphs)
        {
            if (frameWeights == null)
            {
                throw new ArgumentNullException(nameof(frameWeights));
            }

            if (groupMorphs == null)
            {
                throw new ArgumentNullException(nameof(groupMorphs));
            }

            if (flipMorphs == null)
            {
                throw new ArgumentNullException(nameof(flipMorphs));
            }

            if (groupMorphs.Count == 0 && flipMorphs.Count == 0)
            {
                return CopyAndValidateFrameWeights(frameWeights);
            }

            // Build a unified composite morph lookup from both group and flip descriptors.
            var compositeByName = new Dictionary<string, CompositeMorphEntry>(
                groupMorphs.Count + flipMorphs.Count,
                StringComparer.Ordinal);

            foreach (MmdGroupMorphDescriptor groupMorph in groupMorphs)
            {
                if (!string.IsNullOrWhiteSpace(groupMorph.morphName))
                {
                    compositeByName[groupMorph.morphName] = new CompositeMorphEntry(
                        morphName: groupMorph.morphName,
                        isGroup: true,
                        groupOffsets: groupMorph.offsets);
                }
            }

            foreach (MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor flipMorph in flipMorphs)
            {
                if (!string.IsNullOrWhiteSpace(flipMorph.morphName))
                {
                    compositeByName[flipMorph.morphName] = new CompositeMorphEntry(
                        morphName: flipMorph.morphName,
                        isGroup: false,
                        flipOffsets: flipMorph.offsets);
                }
            }

            // Start with a copy of the frame weights.
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

            // Expand each active composite morph (group or flip).
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, float> entry in frameWeights)
            {
                string morphName = entry.Key;
                float compositeWeight = entry.Value;

                if (compositeWeight == 0.0f)
                {
                    continue;
                }

                if (!compositeByName.TryGetValue(morphName, out CompositeMorphEntry? entryDef) || entryDef == null)
                {
                    continue;
                }

                // Consume the composite morph weight so it does not reach the evaluator.
                result[morphName] = 0.0f;

                visited.Clear();
                visited.Add(morphName);
                ExpandComposite(entryDef, compositeWeight, compositeByName, result, visited);
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

        private static void ExpandComposite(
            CompositeMorphEntry entry,
            float compositeWeight,
            Dictionary<string, CompositeMorphEntry> compositeByName,
            Dictionary<string, float> result,
            HashSet<string> visited)
        {
            if (entry.IsGroup)
            {
                ExpandGroupOffsets(entry.GroupOffsets, entry.MorphName, compositeWeight, compositeByName, result, visited);
            }
            else
            {
                ExpandFlipOffsets(entry.FlipOffsets, entry.MorphName, compositeWeight, compositeByName, result, visited);
            }
        }

        private static void ExpandGroupOffsets(
            IReadOnlyList<MmdGroupMorphOffsetDescriptor>? offsets,
            string morphName,
            float compositeWeight,
            Dictionary<string, CompositeMorphEntry> compositeByName,
            Dictionary<string, float> result,
            HashSet<string> visited)
        {
            if (offsets == null)
            {
                return;
            }

            for (int offsetIndex = 0; offsetIndex < offsets.Count; offsetIndex++)
            {
                MmdGroupMorphOffsetDescriptor offset = offsets[offsetIndex];
                if (offset.weight == 0.0f)
                {
                    continue;
                }

                if (float.IsNaN(offset.weight) || float.IsInfinity(offset.weight))
                {
                    throw new InvalidOperationException(
                        $"Group morph offset weight must be finite: {morphName} -> target index {offset.targetMorphIndex}");
                }

                float contribution = compositeWeight * offset.weight;

                // Look up the target morph name for offset accumulation.
                string targetName = string.IsNullOrWhiteSpace(offset.targetMorphName)
                    ? offset.targetMorphIndex.ToString()
                    : offset.targetMorphName;

                // Add the contribution to the target morph weight.
                float existingWeight = result.TryGetValue(targetName, out float current) ? current : 0.0f;
                result[targetName] = existingWeight + contribution;

                // If the target is itself a composite morph, recurse.
                if (compositeByName.TryGetValue(targetName, out CompositeMorphEntry? targetEntry) && targetEntry != null)
                {
                    if (!visited.Add(targetName))
                    {
                        throw new InvalidOperationException(
                            string.Format(CycleDetectionMessage, FormatCyclePath(visited, targetName)));
                    }

                    ExpandComposite(targetEntry, contribution, compositeByName, result, visited);
                    visited.Remove(targetName);
                }
            }
        }

        private static void ExpandFlipOffsets(
            IReadOnlyList<MmdMorphDescriptorBuilder.MmdFlipMorphOffsetDescriptor>? offsets,
            string morphName,
            float compositeWeight,
            Dictionary<string, CompositeMorphEntry> compositeByName,
            Dictionary<string, float> result,
            HashSet<string> visited)
        {
            if (offsets == null)
            {
                return;
            }

            for (int offsetIndex = 0; offsetIndex < offsets.Count; offsetIndex++)
            {
                MmdMorphDescriptorBuilder.MmdFlipMorphOffsetDescriptor offset = offsets[offsetIndex];
                if (offset.weight == 0.0f)
                {
                    continue;
                }

                if (!offset.finiteWeight)
                {
                    throw new InvalidOperationException(
                        $"Flip morph offset weight must be finite: {morphName} -> target index {offset.targetMorphIndex}");
                }

                float contribution = compositeWeight * offset.weight;

                // Look up the target morph name for offset accumulation.
                string targetName = string.IsNullOrWhiteSpace(offset.targetMorphName)
                    ? offset.targetMorphIndex.ToString()
                    : offset.targetMorphName;

                // Add the contribution to the target morph weight.
                float existingWeight = result.TryGetValue(targetName, out float current) ? current : 0.0f;
                result[targetName] = existingWeight + contribution;

                // If the target is itself a composite morph (group or flip), recurse.
                if (compositeByName.TryGetValue(targetName, out CompositeMorphEntry? targetEntry) && targetEntry != null)
                {
                    if (!visited.Add(targetName))
                    {
                        throw new InvalidOperationException(
                            string.Format(CycleDetectionMessage, FormatCyclePath(visited, targetName)));
                    }

                    ExpandComposite(targetEntry, contribution, compositeByName, result, visited);
                    visited.Remove(targetName);
                }
            }
        }

        private sealed class CompositeMorphEntry
        {
            public string MorphName { get; }
            public bool IsGroup { get; }
            public IReadOnlyList<MmdGroupMorphOffsetDescriptor> GroupOffsets { get; }
            public IReadOnlyList<MmdMorphDescriptorBuilder.MmdFlipMorphOffsetDescriptor> FlipOffsets { get; }

            public CompositeMorphEntry(
                string morphName,
                bool isGroup,
                IReadOnlyList<MmdGroupMorphOffsetDescriptor>? groupOffsets = null,
                IReadOnlyList<MmdMorphDescriptorBuilder.MmdFlipMorphOffsetDescriptor>? flipOffsets = null)
            {
                MorphName = morphName;
                IsGroup = isGroup;
                GroupOffsets = groupOffsets ?? Array.Empty<MmdGroupMorphOffsetDescriptor>();
                FlipOffsets = flipOffsets ?? Array.Empty<MmdMorphDescriptorBuilder.MmdFlipMorphOffsetDescriptor>();
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
