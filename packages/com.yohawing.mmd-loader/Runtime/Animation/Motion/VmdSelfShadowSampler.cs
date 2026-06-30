#nullable enable

using System;
using System.Collections.Generic;
using Mmd.Parser;

namespace Mmd.Motion
{
    /// <summary>
    /// A sampled VMD self-shadow state. Mode/distance are preserved in VMD terms here; applying them
    /// to Unity lights remains an explicit opt-in policy boundary.
    /// </summary>
    public readonly struct MmdSelfShadowState
    {
        public MmdSelfShadowState(byte mode, float distance)
        {
            Mode = mode;
            Distance = distance;
        }

        public byte Mode { get; }

        public float Distance { get; }

        public static MmdSelfShadowState Default => new MmdSelfShadowState(0, 0.0f);
    }

    /// <summary>
    /// Opt-in mapping policy from VMD self-shadow state to Unity-standard shadow settings.
    /// The default policy is disabled so parsing/sampling self-shadow data never mutates scene
    /// lighting unless a caller explicitly opts in.
    /// </summary>
    public readonly struct MmdSelfShadowMappingPolicy
    {
        private const float DefaultDistanceScale = 100.0f;
        private const float DefaultMinShadowDistance = 1.0f;
        private const float DefaultMaxShadowDistance = 100.0f;
        private const float DefaultShadowStrength = 1.0f;

        public MmdSelfShadowMappingPolicy(
            bool enabled,
            float distanceScale = DefaultDistanceScale,
            float minShadowDistance = DefaultMinShadowDistance,
            float maxShadowDistance = DefaultMaxShadowDistance,
            float shadowStrength = DefaultShadowStrength)
        {
            Enabled = enabled;
            DistanceScale = PositiveFiniteOrDefault(distanceScale, DefaultDistanceScale);
            MinShadowDistance = PositiveFiniteOrDefault(minShadowDistance, DefaultMinShadowDistance);
            MaxShadowDistance = PositiveFiniteOrDefault(maxShadowDistance, DefaultMaxShadowDistance);
            if (MaxShadowDistance < MinShadowDistance)
            {
                MaxShadowDistance = MinShadowDistance;
            }

            ShadowStrength = Clamp01FiniteOrDefault(shadowStrength, DefaultShadowStrength);
        }

        public bool Enabled { get; }

        public float DistanceScale { get; }

        public float MinShadowDistance { get; }

        public float MaxShadowDistance { get; }

        public float ShadowStrength { get; }

        public static MmdSelfShadowMappingPolicy Disabled => new MmdSelfShadowMappingPolicy(false);

        public static MmdSelfShadowMappingPolicy OptInDefault => new MmdSelfShadowMappingPolicy(true);

        private static float PositiveFiniteOrDefault(float value, float fallback)
        {
            return float.IsFinite(value) && value > 0.0f ? value : fallback;
        }

        private static float Clamp01FiniteOrDefault(float value, float fallback)
        {
            if (!float.IsFinite(value))
            {
                return fallback;
            }

            return Clamp(value, 0.0f, 1.0f);
        }

        internal static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }

    public readonly struct MmdSelfShadowUnityShadowSettings
    {
        public MmdSelfShadowUnityShadowSettings(
            bool runtimeApplicationEnabled,
            bool castShadows,
            byte mode,
            float shadowDistance,
            float shadowStrength)
        {
            RuntimeApplicationEnabled = runtimeApplicationEnabled;
            CastShadows = castShadows;
            Mode = mode;
            ShadowDistance = shadowDistance;
            ShadowStrength = shadowStrength;
        }

        public bool RuntimeApplicationEnabled { get; }

        public bool CastShadows { get; }

        public byte Mode { get; }

        public float ShadowDistance { get; }

        public float ShadowStrength { get; }
    }

    public static class VmdSelfShadowSampler
    {
        /// <summary>
        /// Samples VMD self-shadow state using native runtime semantics: mode is a step value and
        /// distance is linearly interpolated between surrounding keyframes.
        /// </summary>
        public static MmdSelfShadowState Sample(IReadOnlyList<MmdSelfShadowKeyframeDefinition>? keyframes, float frame)
        {
            if (keyframes == null)
            {
                throw new ArgumentNullException(nameof(keyframes));
            }

            if (!float.IsFinite(frame))
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Frame must be finite.");
            }

            MmdSelfShadowKeyframeDefinition? previous = null;
            MmdSelfShadowKeyframeDefinition? next = null;

            for (int i = 0; i < keyframes.Count; i++)
            {
                MmdSelfShadowKeyframeDefinition candidate = keyframes[i];
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
                return MmdSelfShadowState.Default;
            }

            if (previous == null)
            {
                return FromKeyframe(next!);
            }

            if (next == null || previous.frame == next.frame)
            {
                return FromKeyframe(previous);
            }

            float t = (frame - previous.frame) / (next.frame - previous.frame);
            return new MmdSelfShadowState(
                previous.mode,
                previous.distance + (next.distance - previous.distance) * t);
        }

        public static MmdSelfShadowUnityShadowSettings MapToUnityShadowSettings(
            MmdSelfShadowState state,
            MmdSelfShadowMappingPolicy policy)
        {
            if (!policy.Enabled)
            {
                return new MmdSelfShadowUnityShadowSettings(
                    runtimeApplicationEnabled: false,
                    castShadows: false,
                    mode: state.Mode,
                    shadowDistance: 0.0f,
                    shadowStrength: 0.0f);
            }

            bool castShadows = state.Mode == 1 || state.Mode == 2;
            if (!castShadows)
            {
                return new MmdSelfShadowUnityShadowSettings(
                    runtimeApplicationEnabled: true,
                    castShadows: false,
                    mode: state.Mode,
                    shadowDistance: 0.0f,
                    shadowStrength: 0.0f);
            }

            float scaledDistance = state.Distance * policy.DistanceScale;
            if (!float.IsFinite(scaledDistance))
            {
                scaledDistance = policy.MinShadowDistance;
            }

            float shadowDistance = MmdSelfShadowMappingPolicy.Clamp(
                scaledDistance,
                policy.MinShadowDistance,
                policy.MaxShadowDistance);

            return new MmdSelfShadowUnityShadowSettings(
                runtimeApplicationEnabled: true,
                castShadows: true,
                mode: state.Mode,
                shadowDistance: shadowDistance,
                shadowStrength: policy.ShadowStrength);
        }

        private static MmdSelfShadowState FromKeyframe(MmdSelfShadowKeyframeDefinition keyframe)
        {
            return new MmdSelfShadowState(keyframe.mode, keyframe.distance);
        }
    }
}
