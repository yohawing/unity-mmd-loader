#nullable enable

using System;
using System.Collections.Generic;
using Mmd.Parser;

namespace Mmd.Motion
{
    /// <summary>
    /// An interpolated MMD light state in MMD light space (RGB color + forward direction vector).
    /// <see cref="Color"/> is the RGB intensity; <see cref="Direction"/> is the light direction.
    /// This value type keeps the sampled track data engine-agnostic.
    /// </summary>
    public readonly struct MmdLightState
    {
        public MmdLightState(float[] color, float[] direction)
        {
            Color = color ?? new[] { 0.0f, 0.0f, 0.0f };
            Direction = direction ?? new[] { 0.0f, 0.0f, 0.0f };
        }

        public float[] Color { get; }

        public float[] Direction { get; }

        /// <summary>Neutral light used when a track has no keyframes.</summary>
        public static MmdLightState Default =>
            new MmdLightState(new[] { 0.6f, 0.6f, 0.6f }, new[] { -0.5f, -1.0f, 0.5f });
    }

    /// <summary>
    /// Samples a VMD light track (<see cref="MmdLightKeyframeDefinition"/>) at an arbitrary frame.
    /// </summary>
    public static class VmdLightSampler
    {
        public static MmdLightState Sample(IReadOnlyList<MmdLightKeyframeDefinition>? keyframes, float frame)
        {
            if (keyframes == null)
            {
                throw new ArgumentNullException(nameof(keyframes));
            }

            if (float.IsNaN(frame) || float.IsInfinity(frame))
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Frame must be finite.");
            }

            MmdLightKeyframeDefinition? previous = null;
            MmdLightKeyframeDefinition? next = null;

            for (int i = 0; i < keyframes.Count; i++)
            {
                MmdLightKeyframeDefinition candidate = keyframes[i];
                if (candidate == null)
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
                return MmdLightState.Default;
            }

            if (previous == null)
            {
                return FromKeyframe(next!);
            }

            if (next == null || previous.frame == next.frame)
            {
                return FromKeyframe(previous);
            }

            float span = next.frame - previous.frame;
            float t = (frame - previous.frame) / span;

            return new MmdLightState(
                new[]
                {
                    Lerp(Component(previous.color, 0), Component(next.color, 0), t),
                    Lerp(Component(previous.color, 1), Component(next.color, 1), t),
                    Lerp(Component(previous.color, 2), Component(next.color, 2), t)
                },
                new[]
                {
                    Lerp(Component(previous.direction, 0), Component(next.direction, 0), t),
                    Lerp(Component(previous.direction, 1), Component(next.direction, 1), t),
                    Lerp(Component(previous.direction, 2), Component(next.direction, 2), t)
                });
        }

        private static MmdLightState FromKeyframe(MmdLightKeyframeDefinition keyframe)
        {
            return new MmdLightState(
                new[]
                {
                    Component(keyframe.color, 0),
                    Component(keyframe.color, 1),
                    Component(keyframe.color, 2)
                },
                new[]
                {
                    Component(keyframe.direction, 0),
                    Component(keyframe.direction, 1),
                    Component(keyframe.direction, 2)
                });
        }

        private static float Component(float[]? values, int index)
        {
            return values != null && values.Length > index ? values[index] : 0.0f;
        }

        private static float Lerp(float from, float to, float t)
        {
            return from + (to - from) * t;
        }
    }
}
