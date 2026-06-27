#nullable enable

using System;
using System.Collections.Generic;
using Mmd.Parser;

namespace Mmd.Motion
{
    public static class VmdMorphSampler
    {
        public static float SampleWeight(IReadOnlyList<MmdMorphKeyframeDefinition>? keyframes, string morphName, float frame)
        {
            if (keyframes == null)
            {
                throw new ArgumentNullException(nameof(keyframes));
            }

            if (string.IsNullOrWhiteSpace(morphName))
            {
                throw new ArgumentException("Morph name is required.", nameof(morphName));
            }

            if (float.IsNaN(frame) || float.IsInfinity(frame))
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Frame must be finite.");
            }

            MmdMorphKeyframeDefinition? previous = null;
            MmdMorphKeyframeDefinition? next = null;

            for (int i = 0; i < keyframes.Count; i++)
            {
                MmdMorphKeyframeDefinition candidate = keyframes[i];
                if (candidate.morphName != morphName)
                {
                    continue;
                }

                if (candidate.frame <= frame && (previous == null || candidate.frame >= previous.frame))
                {
                    previous = candidate;
                }

                if (candidate.frame >= frame && (next == null || candidate.frame <= next.frame))
                {
                    next = candidate;
                }
            }

            if (previous == null && next == null)
            {
                return 0.0f;
            }

            if (previous == null)
            {
                return next!.weight;
            }

            if (next == null || previous.frame == next.frame)
            {
                return previous.weight;
            }

            float span = next.frame - previous.frame;
            float t = (frame - previous.frame) / span;
            return previous.weight + (next.weight - previous.weight) * t;
        }

        public static float SampleSortedWeight(IReadOnlyList<MmdMorphKeyframeDefinition>? keyframes, string morphName, float frame)
        {
            if (keyframes == null)
            {
                throw new ArgumentNullException(nameof(keyframes));
            }

            if (string.IsNullOrWhiteSpace(morphName))
            {
                throw new ArgumentException("Morph name is required.", nameof(morphName));
            }

            if (float.IsNaN(frame) || float.IsInfinity(frame))
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Frame must be finite.");
            }

            int nextIndex = LowerBoundFrame(keyframes, frame);
            int previousIndex = nextIndex;
            while (previousIndex < keyframes.Count && keyframes[previousIndex].frame <= frame)
            {
                previousIndex++;
            }

            previousIndex--;
            MmdMorphKeyframeDefinition? previous = previousIndex >= 0 ? keyframes[previousIndex] : null;
            MmdMorphKeyframeDefinition? next = nextIndex < keyframes.Count ? keyframes[nextIndex] : null;

            if (previous == null && next == null)
            {
                return 0.0f;
            }

            if (previous == null)
            {
                return next!.weight;
            }

            if (next == null || previous.frame == next.frame)
            {
                return previous.weight;
            }

            float span = next.frame - previous.frame;
            float t = (frame - previous.frame) / span;
            return previous.weight + (next.weight - previous.weight) * t;
        }

        private static int LowerBoundFrame(IReadOnlyList<MmdMorphKeyframeDefinition> keyframes, float frame)
        {
            int left = 0;
            int right = keyframes.Count;
            while (left < right)
            {
                int middle = left + ((right - left) / 2);
                if (keyframes[middle].frame < frame)
                {
                    left = middle + 1;
                }
                else
                {
                    right = middle;
                }
            }

            return left;
        }
    }
}
