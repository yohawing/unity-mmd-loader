#nullable enable

using Mmd.Motion;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    public enum MmdSelfShadowBoundsSource
    {
        None = 0,
        RendererCollection = 1,
        ManualOverride = 2
    }

    public readonly struct MmdSelfShadowBoundsResult
    {
        private MmdSelfShadowBoundsResult(
            bool hasBounds,
            Bounds bounds,
            MmdSelfShadowBoundsSource source,
            int includedRendererCount)
        {
            HasBounds = hasBounds;
            Bounds = bounds;
            Source = source;
            IncludedRendererCount = includedRendererCount;
        }

        public bool HasBounds { get; }

        public Bounds Bounds { get; }

        public Vector3 Center => Bounds.center;

        public Vector3 Size => Bounds.size;

        public MmdSelfShadowBoundsSource Source { get; }

        public int IncludedRendererCount { get; }

        public static MmdSelfShadowBoundsResult NoBounds => new MmdSelfShadowBoundsResult(
            hasBounds: false,
            bounds: default,
            source: MmdSelfShadowBoundsSource.None,
            includedRendererCount: 0);

        public static MmdSelfShadowBoundsResult FromBounds(
            Bounds bounds,
            MmdSelfShadowBoundsSource source,
            int includedRendererCount)
        {
            return new MmdSelfShadowBoundsResult(
                hasBounds: true,
                bounds: bounds,
                source: source,
                includedRendererCount: includedRendererCount < 0 ? 0 : includedRendererCount);
        }
    }

    public static class MmdSelfShadowBoundsCollector
    {
        public static MmdSelfShadowBoundsResult Collect(
            GameObject? characterRoot,
            MmdSelfShadowProjectionPolicy policy)
        {
            return Collect(characterRoot != null ? characterRoot.transform : null, policy);
        }

        public static MmdSelfShadowBoundsResult Collect(
            Transform? characterRoot,
            MmdSelfShadowProjectionPolicy policy)
        {
            float padding = NormalizePadding(policy.BoundsPadding);
            if (policy.HasManualBoundsOverride)
            {
                return MmdSelfShadowBoundsResult.FromBounds(
                    ApplyPadding(ToBounds(policy.ManualBoundsOverride), padding),
                    MmdSelfShadowBoundsSource.ManualOverride,
                    includedRendererCount: 0);
            }

            if (characterRoot == null || !characterRoot.gameObject.activeInHierarchy)
            {
                return MmdSelfShadowBoundsResult.NoBounds;
            }

            Renderer[] renderers = characterRoot.GetComponentsInChildren<Renderer>(includeInactive: false);
            bool hasBounds = false;
            Bounds aggregate = default;
            int includedRendererCount = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    aggregate = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    aggregate.Encapsulate(renderer.bounds);
                }

                includedRendererCount++;
            }

            if (!hasBounds)
            {
                return MmdSelfShadowBoundsResult.NoBounds;
            }

            return MmdSelfShadowBoundsResult.FromBounds(
                ApplyPadding(aggregate, padding),
                MmdSelfShadowBoundsSource.RendererCollection,
                includedRendererCount);
        }

        private static Bounds ToBounds(MmdSelfShadowProjectionBounds bounds)
        {
            return new Bounds(
                new Vector3(bounds.CenterX, bounds.CenterY, bounds.CenterZ),
                new Vector3(bounds.SizeX, bounds.SizeY, bounds.SizeZ));
        }

        private static Bounds ApplyPadding(Bounds bounds, float padding)
        {
            if (padding <= 0.0f)
            {
                return bounds;
            }

            bounds.Expand(padding * 2.0f);
            return bounds;
        }

        private static float NormalizePadding(float padding)
        {
            return float.IsFinite(padding) && padding >= 0.0f ? padding : 0.0f;
        }
    }
}
