#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Mmd.Motion;
using Mmd.Parser;

namespace Mmd.Tests
{
    public sealed class VmdSelfShadowSamplerTests
    {
        private static MmdSelfShadowKeyframeDefinition Keyframe(int frame, byte mode, float distance)
        {
            return new MmdSelfShadowKeyframeDefinition
            {
                frame = frame,
                mode = mode,
                distance = distance
            };
        }

        [Test]
        public void EmptyTrackReturnsDisabledDefault()
        {
            MmdSelfShadowState state = VmdSelfShadowSampler.Sample(
                new List<MmdSelfShadowKeyframeDefinition>(),
                5.0f);

            Assert.That(state.Mode, Is.EqualTo(0));
            Assert.That(state.Distance, Is.EqualTo(0.0f));
        }

        [Test]
        public void SamplingStepsModeAndInterpolatesDistance()
        {
            var keyframes = new List<MmdSelfShadowKeyframeDefinition>
            {
                Keyframe(0, 1, 0.25f),
                Keyframe(10, 2, 0.75f)
            };

            MmdSelfShadowState before = VmdSelfShadowSampler.Sample(keyframes, -5.0f);
            MmdSelfShadowState middle = VmdSelfShadowSampler.Sample(keyframes, 5.0f);
            MmdSelfShadowState exactNext = VmdSelfShadowSampler.Sample(keyframes, 10.0f);
            MmdSelfShadowState after = VmdSelfShadowSampler.Sample(keyframes, 50.0f);

            Assert.That(before.Mode, Is.EqualTo(1));
            Assert.That(before.Distance, Is.EqualTo(0.25f));
            Assert.That(middle.Mode, Is.EqualTo(1));
            Assert.That(middle.Distance, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(exactNext.Mode, Is.EqualTo(2));
            Assert.That(exactNext.Distance, Is.EqualTo(0.75f));
            Assert.That(after.Mode, Is.EqualTo(2));
            Assert.That(after.Distance, Is.EqualTo(0.75f));
        }

        [Test]
        public void UnsortedAndNullKeyframesAreHandledByFrame()
        {
            var keyframes = new List<MmdSelfShadowKeyframeDefinition>
            {
                Keyframe(20, 2, 0.8f),
                null!,
                Keyframe(0, 1, 0.2f),
                Keyframe(10, 0, 0.4f)
            };

            MmdSelfShadowState state = VmdSelfShadowSampler.Sample(keyframes, 15.0f);

            Assert.That(state.Mode, Is.EqualTo(0));
            Assert.That(state.Distance, Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void NonFiniteFrameThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => VmdSelfShadowSampler.Sample(new[] { Keyframe(0, 1, 0.2f) }, float.NaN));
        }

        [Test]
        public void NullKeyframesThrows()
        {
            Assert.Throws<ArgumentNullException>(() => VmdSelfShadowSampler.Sample(null, 0.0f));
        }

        [Test]
        public void DefaultPolicyDoesNotApplyUnityShadowSettings()
        {
            MmdSelfShadowUnityShadowSettings settings = VmdSelfShadowSampler.MapToUnityShadowSettings(
                new MmdSelfShadowState(1, 0.4f),
                MmdSelfShadowMappingPolicy.Disabled);

            Assert.That(settings.RuntimeApplicationEnabled, Is.False);
            Assert.That(settings.CastShadows, Is.False);
            Assert.That(settings.Mode, Is.EqualTo(1));
            Assert.That(settings.ShadowDistance, Is.EqualTo(0.0f));
            Assert.That(settings.ShadowStrength, Is.EqualTo(0.0f));
        }

        [Test]
        public void OptInPolicyMapsEnabledModesAndClampsDistance()
        {
            var policy = new MmdSelfShadowMappingPolicy(
                enabled: true,
                distanceScale: 100.0f,
                minShadowDistance: 1.0f,
                maxShadowDistance: 20.0f,
                shadowStrength: 0.4f);

            MmdSelfShadowUnityShadowSettings settings = VmdSelfShadowSampler.MapToUnityShadowSettings(
                new MmdSelfShadowState(2, 0.4f),
                policy);

            Assert.That(settings.RuntimeApplicationEnabled, Is.True);
            Assert.That(settings.CastShadows, Is.True);
            Assert.That(settings.Mode, Is.EqualTo(2));
            Assert.That(settings.ShadowDistance, Is.EqualTo(20.0f));
            Assert.That(settings.ShadowStrength, Is.EqualTo(0.4f));
        }

        [Test]
        public void OptInPolicyKeepsModeZeroAndUnknownModesDisabled()
        {
            MmdSelfShadowUnityShadowSettings modeZero = VmdSelfShadowSampler.MapToUnityShadowSettings(
                new MmdSelfShadowState(0, 0.4f),
                MmdSelfShadowMappingPolicy.OptInDefault);
            MmdSelfShadowUnityShadowSettings unknown = VmdSelfShadowSampler.MapToUnityShadowSettings(
                new MmdSelfShadowState(99, 0.4f),
                MmdSelfShadowMappingPolicy.OptInDefault);

            Assert.That(modeZero.RuntimeApplicationEnabled, Is.True);
            Assert.That(modeZero.CastShadows, Is.False);
            Assert.That(unknown.RuntimeApplicationEnabled, Is.True);
            Assert.That(unknown.CastShadows, Is.False);
        }

        [Test]
        public void OptInPolicyNormalizesInvalidDistanceAndStrengthInputs()
        {
            var policy = new MmdSelfShadowMappingPolicy(
                enabled: true,
                distanceScale: float.NaN,
                minShadowDistance: 5.0f,
                maxShadowDistance: 1.0f,
                shadowStrength: 2.0f);

            MmdSelfShadowUnityShadowSettings settings = VmdSelfShadowSampler.MapToUnityShadowSettings(
                new MmdSelfShadowState(1, float.NaN),
                policy);

            Assert.That(settings.CastShadows, Is.True);
            Assert.That(settings.ShadowDistance, Is.EqualTo(5.0f));
            Assert.That(settings.ShadowStrength, Is.EqualTo(1.0f));
        }
    }
}
