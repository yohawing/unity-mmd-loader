using System;
using System.Collections.Generic;
using NUnit.Framework;
using Mmd.Motion;
using Mmd.Parser;

namespace Mmd.Tests
{
    public sealed class VmdCameraSamplerTests
    {
        private static MmdCameraKeyframeDefinition Keyframe(
            int frame, float distance, float[] position, float[] rotation, int viewAngle, bool perspective)
        {
            return new MmdCameraKeyframeDefinition
            {
                frame = frame,
                distance = distance,
                position = position,
                rotation = rotation,
                viewAngle = viewAngle,
                perspective = perspective,
                interpolation = new byte[24]
            };
        }

        [Test]
        public void EmptyTrackReturnsDefault()
        {
            MmdCameraState state = VmdCameraSampler.Sample(new List<MmdCameraKeyframeDefinition>(), 5.0f);

            Assert.That(state.Distance, Is.EqualTo(0.0f));
            Assert.That(state.ViewAngle, Is.EqualTo(30.0f));
            Assert.That(state.Perspective, Is.True);
            CollectionAssert.AreEqual(new[] { 0.0f, 0.0f, 0.0f }, state.Position);
            CollectionAssert.AreEqual(new[] { 0.0f, 0.0f, 0.0f }, state.Rotation);
        }

        [Test]
        public void SingleKeyframeIsReturnedAtAnyFrame()
        {
            var keyframes = new List<MmdCameraKeyframeDefinition>
            {
                Keyframe(10, -45.0f, new[] { 1.0f, 10.0f, 0.0f }, new[] { 0.1f, 0.2f, 0.3f }, 30, false)
            };

            foreach (float frame in new[] { 0.0f, 10.0f, 99.0f })
            {
                MmdCameraState state = VmdCameraSampler.Sample(keyframes, frame);
                Assert.That(state.Distance, Is.EqualTo(-45.0f), $"frame {frame}");
                Assert.That(state.ViewAngle, Is.EqualTo(30.0f), $"frame {frame}");
                Assert.That(state.Perspective, Is.False, $"frame {frame}");
                CollectionAssert.AreEqual(new[] { 1.0f, 10.0f, 0.0f }, state.Position);
                CollectionAssert.AreEqual(new[] { 0.1f, 0.2f, 0.3f }, state.Rotation);
            }
        }

        [Test]
        public void ExactFramesReturnKeyframeValues()
        {
            var keyframes = TwoKeyframeTrack();

            MmdCameraState a = VmdCameraSampler.Sample(keyframes, 0.0f);
            Assert.That(a.Distance, Is.EqualTo(-40.0f));
            Assert.That(a.ViewAngle, Is.EqualTo(20.0f));
            CollectionAssert.AreEqual(new[] { 0.0f, 10.0f, 0.0f }, a.Position);

            MmdCameraState b = VmdCameraSampler.Sample(keyframes, 10.0f);
            Assert.That(b.Distance, Is.EqualTo(-20.0f));
            Assert.That(b.ViewAngle, Is.EqualTo(40.0f));
            CollectionAssert.AreEqual(new[] { 2.0f, 20.0f, -4.0f }, b.Position);
        }

        [Test]
        public void MidpointInterpolatesLinearly()
        {
            var keyframes = TwoKeyframeTrack();

            MmdCameraState mid = VmdCameraSampler.Sample(keyframes, 5.0f);

            // Linear midpoint of (-40 -> -20), (20 -> 40), position, rotation.
            Assert.That(mid.Distance, Is.EqualTo(-30.0f).Within(0.0001f));
            Assert.That(mid.ViewAngle, Is.EqualTo(30.0f).Within(0.0001f));
            Assert.That(mid.Position[0], Is.EqualTo(1.0f).Within(0.0001f));
            Assert.That(mid.Position[1], Is.EqualTo(15.0f).Within(0.0001f));
            Assert.That(mid.Position[2], Is.EqualTo(-2.0f).Within(0.0001f));
            Assert.That(mid.Rotation[0], Is.EqualTo(0.05f).Within(0.0001f));
        }

        [Test]
        public void PerspectiveIsSteppedFromPreviousKeyframe()
        {
            var keyframes = new List<MmdCameraKeyframeDefinition>
            {
                Keyframe(0, -40.0f, new[] { 0.0f, 0.0f, 0.0f }, new[] { 0.0f, 0.0f, 0.0f }, 30, true),
                Keyframe(10, -20.0f, new[] { 0.0f, 0.0f, 0.0f }, new[] { 0.0f, 0.0f, 0.0f }, 30, false)
            };

            // Between the keyframes the perspective flag holds the previous keyframe's value.
            Assert.That(VmdCameraSampler.Sample(keyframes, 5.0f).Perspective, Is.True);
            Assert.That(VmdCameraSampler.Sample(keyframes, 10.0f).Perspective, Is.False);
        }

        [Test]
        public void FramesOutsideRangeClampToEndpoints()
        {
            var keyframes = TwoKeyframeTrack();

            Assert.That(VmdCameraSampler.Sample(keyframes, -5.0f).Distance, Is.EqualTo(-40.0f));
            Assert.That(VmdCameraSampler.Sample(keyframes, 50.0f).Distance, Is.EqualTo(-20.0f));
        }

        [Test]
        public void UnsortedKeyframesAreInterpolatedByFrame()
        {
            // Same two keyframes as TwoKeyframeTrack but in reverse list order.
            var keyframes = new List<MmdCameraKeyframeDefinition>
            {
                Keyframe(10, -20.0f, new[] { 2.0f, 20.0f, -4.0f }, new[] { 0.1f, 0.1f, 0.1f }, 40, true),
                Keyframe(0, -40.0f, new[] { 0.0f, 10.0f, 0.0f }, new[] { 0.0f, 0.0f, 0.0f }, 20, true)
            };

            MmdCameraState mid = VmdCameraSampler.Sample(keyframes, 5.0f);
            Assert.That(mid.Distance, Is.EqualTo(-30.0f).Within(0.0001f));
            Assert.That(mid.ViewAngle, Is.EqualTo(30.0f).Within(0.0001f));
        }

        [Test]
        public void NonFiniteFrameThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => VmdCameraSampler.Sample(TwoKeyframeTrack(), float.NaN));
        }

        [Test]
        public void NullKeyframesThrows()
        {
            Assert.Throws<ArgumentNullException>(() => VmdCameraSampler.Sample(null, 0.0f));
        }

        [Test]
        public void NullKeyframeElementsAreSkipped()
        {
            var keyframes = new List<MmdCameraKeyframeDefinition>
            {
                null,
                Keyframe(0, -40.0f, new[] { 0.0f, 10.0f, 0.0f }, new[] { 0.0f, 0.0f, 0.0f }, 20, true),
                null,
                Keyframe(10, -20.0f, new[] { 2.0f, 20.0f, -4.0f }, new[] { 0.1f, 0.1f, 0.1f }, 40, true)
            };

            MmdCameraState mid = VmdCameraSampler.Sample(keyframes, 5.0f);
            Assert.That(mid.Distance, Is.EqualTo(-30.0f).Within(0.0001f));
        }

        [Test]
        public void MissingPositionAndRotationArraysDefaultToZeroWithoutThrowing()
        {
            var keyframes = new List<MmdCameraKeyframeDefinition>
            {
                new MmdCameraKeyframeDefinition
                {
                    frame = 0,
                    distance = -40.0f,
                    position = Array.Empty<float>(),
                    rotation = null,
                    viewAngle = 30,
                    perspective = true,
                    interpolation = Array.Empty<byte>()
                }
            };

            MmdCameraState state = VmdCameraSampler.Sample(keyframes, 0.0f);
            CollectionAssert.AreEqual(new[] { 0.0f, 0.0f, 0.0f }, state.Position);
            CollectionAssert.AreEqual(new[] { 0.0f, 0.0f, 0.0f }, state.Rotation);
            Assert.That(state.Distance, Is.EqualTo(-40.0f));
        }

        [Test]
        public void DuplicateFrameKeyframesResolveToLastInListOrder()
        {
            // Degenerate input: two keyframes share frame 0. Behavior is pinned to "last in list
            // wins", matching VmdBoneSampler's prev/next selection (frame >= previous.frame).
            var keyframes = new List<MmdCameraKeyframeDefinition>
            {
                Keyframe(0, -40.0f, new[] { 0.0f, 0.0f, 0.0f }, new[] { 0.0f, 0.0f, 0.0f }, 20, true),
                Keyframe(0, -25.0f, new[] { 1.0f, 1.0f, 1.0f }, new[] { 0.0f, 0.0f, 0.0f }, 35, false)
            };

            MmdCameraState state = VmdCameraSampler.Sample(keyframes, 0.0f);
            Assert.That(state.Distance, Is.EqualTo(-25.0f));
            Assert.That(state.ViewAngle, Is.EqualTo(35.0f));
            Assert.That(state.Perspective, Is.False);
        }

        private static List<MmdCameraKeyframeDefinition> TwoKeyframeTrack()
        {
            return new List<MmdCameraKeyframeDefinition>
            {
                Keyframe(0, -40.0f, new[] { 0.0f, 10.0f, 0.0f }, new[] { 0.0f, 0.0f, 0.0f }, 20, true),
                Keyframe(10, -20.0f, new[] { 2.0f, 20.0f, -4.0f }, new[] { 0.1f, 0.1f, 0.1f }, 40, true)
            };
        }
    }
}
