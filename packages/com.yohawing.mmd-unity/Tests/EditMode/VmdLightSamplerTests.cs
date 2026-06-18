using System;
using System.Collections.Generic;
using NUnit.Framework;
using Yohawing.MmdUnity.Motion;
using Yohawing.MmdUnity.Parser;

namespace Yohawing.MmdUnity.Tests
{
    public sealed class VmdLightSamplerTests
    {
        private static MmdLightKeyframeDefinition Keyframe(int frame, float[] color, float[] direction)
        {
            return new MmdLightKeyframeDefinition
            {
                frame = frame,
                color = color,
                direction = direction
            };
        }

        [Test]
        public void EmptyTrackReturnsDefault()
        {
            MmdLightState state = VmdLightSampler.Sample(new List<MmdLightKeyframeDefinition>(), 5.0f);

            CollectionAssert.AreEqual(new[] { 0.6f, 0.6f, 0.6f }, state.Color);
            CollectionAssert.AreEqual(new[] { -0.5f, -1.0f, 0.5f }, state.Direction);
        }

        [Test]
        public void SingleKeyframeIsReturnedAtAnyFrame()
        {
            var keyframes = new List<MmdLightKeyframeDefinition>
            {
                Keyframe(10, new[] { 0.0f, 0.5f, 1.0f }, new[] { -0.5f, -1.0f, 0.5f })
            };

            foreach (float frame in new[] { 0.0f, 10.0f, 99.0f })
            {
                MmdLightState state = VmdLightSampler.Sample(keyframes, frame);
                CollectionAssert.AreEqual(new[] { 0.0f, 0.5f, 1.0f }, state.Color);
                CollectionAssert.AreEqual(new[] { -0.5f, -1.0f, 0.5f }, state.Direction);
            }
        }

        [Test]
        public void ExactFramesReturnKeyframeValues()
        {
            var keyframes = TwoKeyframeTrack();

            MmdLightState a = VmdLightSampler.Sample(keyframes, 0.0f);
            CollectionAssert.AreEqual(new[] { 0.0f, 0.5f, 1.0f }, a.Color);
            CollectionAssert.AreEqual(new[] { -0.5f, -1.0f, 0.5f }, a.Direction);

            MmdLightState b = VmdLightSampler.Sample(keyframes, 10.0f);
            CollectionAssert.AreEqual(new[] { 1.0f, 0.0f, 0.5f }, b.Color);
            CollectionAssert.AreEqual(new[] { 0.5f, 0.0f, -0.5f }, b.Direction);
        }

        [Test]
        public void MidpointInterpolatesLinearly()
        {
            var keyframes = TwoKeyframeTrack();

            MmdLightState mid = VmdLightSampler.Sample(keyframes, 5.0f);
            Assert.That(mid.Color[0], Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(mid.Color[1], Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(mid.Color[2], Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(mid.Direction[0], Is.EqualTo(0.0f).Within(0.0001f));
            Assert.That(mid.Direction[1], Is.EqualTo(-0.5f).Within(0.0001f));
            Assert.That(mid.Direction[2], Is.EqualTo(0.0f).Within(0.0001f));
        }

        [Test]
        public void FramesOutsideRangeClampToEndpoints()
        {
            var keyframes = TwoKeyframeTrack();

            CollectionAssert.AreEqual(new[] { 0.0f, 0.5f, 1.0f }, VmdLightSampler.Sample(keyframes, -5.0f).Color);
            CollectionAssert.AreEqual(new[] { 1.0f, 0.0f, 0.5f }, VmdLightSampler.Sample(keyframes, 50.0f).Color);
        }

        [Test]
        public void UnsortedKeyframesAreInterpolatedByFrame()
        {
            var keyframes = new List<MmdLightKeyframeDefinition>
            {
                Keyframe(10, new[] { 1.0f, 0.0f, 0.5f }, new[] { 0.5f, 0.0f, -0.5f }),
                Keyframe(0, new[] { 0.0f, 0.5f, 1.0f }, new[] { -0.5f, -1.0f, 0.5f })
            };

            MmdLightState mid = VmdLightSampler.Sample(keyframes, 5.0f);
            Assert.That(mid.Color[0], Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(mid.Direction[1], Is.EqualTo(-0.5f).Within(0.0001f));
        }

        [Test]
        public void NullKeyframeElementsAreSkipped()
        {
            var keyframes = new List<MmdLightKeyframeDefinition>
            {
                null,
                Keyframe(0, new[] { 0.0f, 0.5f, 1.0f }, new[] { -0.5f, -1.0f, 0.5f }),
                null,
                Keyframe(10, new[] { 1.0f, 0.0f, 0.5f }, new[] { 0.5f, 0.0f, -0.5f })
            };

            MmdLightState mid = VmdLightSampler.Sample(keyframes, 5.0f);
            Assert.That(mid.Color[0], Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void MissingArraysDefaultToZeroWithoutThrowing()
        {
            var keyframes = new List<MmdLightKeyframeDefinition>
            {
                new MmdLightKeyframeDefinition
                {
                    frame = 0,
                    color = Array.Empty<float>(),
                    direction = null
                }
            };

            MmdLightState state = VmdLightSampler.Sample(keyframes, 0.0f);
            CollectionAssert.AreEqual(new[] { 0.0f, 0.0f, 0.0f }, state.Color);
            CollectionAssert.AreEqual(new[] { 0.0f, 0.0f, 0.0f }, state.Direction);
        }

        [Test]
        public void NonFiniteFrameThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => VmdLightSampler.Sample(TwoKeyframeTrack(), float.NaN));
        }

        [Test]
        public void NullKeyframesThrows()
        {
            Assert.Throws<ArgumentNullException>(() => VmdLightSampler.Sample(null, 0.0f));
        }

        private static List<MmdLightKeyframeDefinition> TwoKeyframeTrack()
        {
            return new List<MmdLightKeyframeDefinition>
            {
                Keyframe(0, new[] { 0.0f, 0.5f, 1.0f }, new[] { -0.5f, -1.0f, 0.5f }),
                Keyframe(10, new[] { 1.0f, 0.0f, 0.5f }, new[] { 0.5f, 0.0f, -0.5f })
            };
        }
    }
}
