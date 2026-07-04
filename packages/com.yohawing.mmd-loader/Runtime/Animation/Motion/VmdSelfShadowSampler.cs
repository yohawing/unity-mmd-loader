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

    public enum MmdSelfShadowProjectionScope
    {
        CharacterOnly = 0,
        CharacterAndOptInBackground = 1
    }

    public readonly struct MmdSelfShadowProjectionBounds
    {
        public MmdSelfShadowProjectionBounds(
            float centerX,
            float centerY,
            float centerZ,
            float sizeX,
            float sizeY,
            float sizeZ)
        {
            CenterX = FiniteOrZero(centerX);
            CenterY = FiniteOrZero(centerY);
            CenterZ = FiniteOrZero(centerZ);
            SizeX = NonNegativeFiniteOrZero(sizeX);
            SizeY = NonNegativeFiniteOrZero(sizeY);
            SizeZ = NonNegativeFiniteOrZero(sizeZ);
        }

        public float CenterX { get; }

        public float CenterY { get; }

        public float CenterZ { get; }

        public float SizeX { get; }

        public float SizeY { get; }

        public float SizeZ { get; }

        private static float FiniteOrZero(float value)
        {
            return float.IsFinite(value) ? value : 0.0f;
        }

        private static float NonNegativeFiniteOrZero(float value)
        {
            return float.IsFinite(value) && value > 0.0f ? value : 0.0f;
        }
    }

    /// <summary>
    /// Pure projection/far policy for a future MMD-dedicated self-shadow texture. It does not mutate
    /// Unity Light, QualitySettings, RenderSettings, URP assets, or materials.
    /// </summary>
    public readonly struct MmdSelfShadowProjectionPolicy
    {
        public const float DefaultDistanceScale = 100.0f;
        public const float DefaultMinFarDistance = 1.0f;
        public const float DefaultMaxFarDistance = 100.0f;
        public const float DefaultBoundsPadding = 0.25f;

        public MmdSelfShadowProjectionPolicy(
            float distanceScale = DefaultDistanceScale,
            float minFarDistance = DefaultMinFarDistance,
            float maxFarDistance = DefaultMaxFarDistance,
            float boundsPadding = DefaultBoundsPadding,
            MmdSelfShadowProjectionScope scope = MmdSelfShadowProjectionScope.CharacterOnly,
            bool hasManualBoundsOverride = false,
            MmdSelfShadowProjectionBounds manualBoundsOverride = default)
        {
            DistanceScale = PositiveFiniteOrDefault(distanceScale, DefaultDistanceScale);
            MinFarDistance = PositiveFiniteOrDefault(minFarDistance, DefaultMinFarDistance);
            MaxFarDistance = PositiveFiniteOrDefault(maxFarDistance, DefaultMaxFarDistance);
            if (MaxFarDistance < MinFarDistance)
            {
                MaxFarDistance = MinFarDistance;
            }

            BoundsPadding = PositiveFiniteOrDefault(boundsPadding, DefaultBoundsPadding);
            Scope = NormalizeScope(scope);
            HasManualBoundsOverride = hasManualBoundsOverride;
            ManualBoundsOverride = manualBoundsOverride;
        }

        public float DistanceScale { get; }

        public float MinFarDistance { get; }

        public float MaxFarDistance { get; }

        public float BoundsPadding { get; }

        public MmdSelfShadowProjectionScope Scope { get; }

        public bool IncludesBackground => Scope == MmdSelfShadowProjectionScope.CharacterAndOptInBackground;

        public bool HasManualBoundsOverride { get; }

        public MmdSelfShadowProjectionBounds ManualBoundsOverride { get; }

        public static MmdSelfShadowProjectionPolicy Default => new MmdSelfShadowProjectionPolicy(
            DefaultDistanceScale,
            DefaultMinFarDistance,
            DefaultMaxFarDistance,
            DefaultBoundsPadding,
            MmdSelfShadowProjectionScope.CharacterOnly,
            false,
            default);

        public MmdSelfShadowProjectionState Evaluate(MmdSelfShadowState state)
        {
            float distanceScale = PositiveFiniteOrDefault(DistanceScale, DefaultDistanceScale);
            float minFarDistance = PositiveFiniteOrDefault(MinFarDistance, DefaultMinFarDistance);
            float maxFarDistance = PositiveFiniteOrDefault(MaxFarDistance, DefaultMaxFarDistance);
            if (maxFarDistance < minFarDistance)
            {
                maxFarDistance = minFarDistance;
            }

            float boundsPadding = PositiveFiniteOrDefault(BoundsPadding, DefaultBoundsPadding);
            MmdSelfShadowProjectionScope scope = NormalizeScope(Scope);
            bool active = state.Mode == 1 || state.Mode == 2;
            if (!active)
            {
                return new MmdSelfShadowProjectionState(
                    active: false,
                    mode: state.Mode,
                    farDistance: 0.0f,
                    scope: scope,
                    boundsPadding: boundsPadding,
                    hasManualBoundsOverride: HasManualBoundsOverride,
                    manualBoundsOverride: ManualBoundsOverride);
            }

            float scaledDistance = state.Distance * distanceScale;
            if (!float.IsFinite(scaledDistance) || scaledDistance < 0.0f)
            {
                scaledDistance = minFarDistance;
            }

            float farDistance = MmdSelfShadowMappingPolicy.Clamp(
                scaledDistance,
                minFarDistance,
                maxFarDistance);

            return new MmdSelfShadowProjectionState(
                active: true,
                mode: state.Mode,
                farDistance: farDistance,
                scope: scope,
                boundsPadding: boundsPadding,
                hasManualBoundsOverride: HasManualBoundsOverride,
                manualBoundsOverride: ManualBoundsOverride);
        }

        private static MmdSelfShadowProjectionScope NormalizeScope(MmdSelfShadowProjectionScope scope)
        {
            return scope == MmdSelfShadowProjectionScope.CharacterAndOptInBackground
                ? MmdSelfShadowProjectionScope.CharacterAndOptInBackground
                : MmdSelfShadowProjectionScope.CharacterOnly;
        }

        private static float PositiveFiniteOrDefault(float value, float fallback)
        {
            return float.IsFinite(value) && value > 0.0f ? value : fallback;
        }

    }

    public readonly struct MmdSelfShadowProjectionState
    {
        public MmdSelfShadowProjectionState(
            bool active,
            byte mode,
            float farDistance,
            MmdSelfShadowProjectionScope scope,
            float boundsPadding,
            bool hasManualBoundsOverride = false,
            MmdSelfShadowProjectionBounds manualBoundsOverride = default)
        {
            Active = active;
            Mode = mode;
            FarDistance = float.IsFinite(farDistance) && farDistance > 0.0f ? farDistance : 0.0f;
            Scope = scope == MmdSelfShadowProjectionScope.CharacterAndOptInBackground
                ? MmdSelfShadowProjectionScope.CharacterAndOptInBackground
                : MmdSelfShadowProjectionScope.CharacterOnly;
            BoundsPadding = float.IsFinite(boundsPadding) && boundsPadding >= 0.0f ? boundsPadding : 0.0f;
            HasManualBoundsOverride = hasManualBoundsOverride;
            ManualBoundsOverride = manualBoundsOverride;
        }

        public bool Active { get; }

        public byte Mode { get; }

        public float FarDistance { get; }

        public MmdSelfShadowProjectionScope Scope { get; }

        public bool IncludesBackground => Scope == MmdSelfShadowProjectionScope.CharacterAndOptInBackground;

        public float BoundsPadding { get; }

        public bool HasManualBoundsOverride { get; }

        public MmdSelfShadowProjectionBounds ManualBoundsOverride { get; }

        public static MmdSelfShadowProjectionState Inactive => new MmdSelfShadowProjectionState(
            active: false,
            mode: 0,
            farDistance: 0.0f,
            scope: MmdSelfShadowProjectionScope.CharacterOnly,
            boundsPadding: 0.0f);
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
