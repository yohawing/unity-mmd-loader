using System;
using System.Collections.Generic;
using Mmd.Parser;

namespace Mmd.Motion
{
    public static class VmdBoneSampler
    {
        private static readonly byte[] LinearInterpolation = { 20, 20, 107, 107 };

        public static MmdBonePoseSample SamplePose(IReadOnlyList<MmdBoneKeyframeDefinition> keyframes, string boneName, float frame)
        {
            if (keyframes == null)
            {
                throw new ArgumentNullException(nameof(keyframes));
            }

            if (string.IsNullOrWhiteSpace(boneName))
            {
                throw new ArgumentException("Bone name is required.", nameof(boneName));
            }

            if (float.IsNaN(frame) || float.IsInfinity(frame))
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Frame must be finite.");
            }

            MmdBoneKeyframeDefinition? previous = null;
            MmdBoneKeyframeDefinition? next = null;

            for (int i = 0; i < keyframes.Count; i++)
            {
                MmdBoneKeyframeDefinition candidate = keyframes[i];
                if (candidate.boneName != boneName)
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
                return MmdBonePoseSample.Identity;
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
            float normalizedFrame = (frame - previous.frame) / span;

            return new MmdBonePoseSample(
                new[]
                {
                    Lerp(Component(previous.translation, 0), Component(next.translation, 0), Interpolate(next.interpolation.translationX, normalizedFrame)),
                    Lerp(Component(previous.translation, 1), Component(next.translation, 1), Interpolate(next.interpolation.translationY, normalizedFrame)),
                    Lerp(Component(previous.translation, 2), Component(next.translation, 2), Interpolate(next.interpolation.translationZ, normalizedFrame))
                },
                Slerp(
                    QuaternionOrIdentity(previous.rotation),
                    QuaternionOrIdentity(next.rotation),
                    Interpolate(next.interpolation.rotation, normalizedFrame)));
        }

        public static MmdBonePoseSample SampleSortedPose(IReadOnlyList<MmdBoneKeyframeDefinition> keyframes, string boneName, float frame)
        {
            if (keyframes == null)
            {
                throw new ArgumentNullException(nameof(keyframes));
            }

            if (string.IsNullOrWhiteSpace(boneName))
            {
                throw new ArgumentException("Bone name is required.", nameof(boneName));
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
            MmdBoneKeyframeDefinition? previous = previousIndex >= 0 ? keyframes[previousIndex] : null;
            MmdBoneKeyframeDefinition? next = nextIndex < keyframes.Count ? keyframes[nextIndex] : null;

            if (previous == null && next == null)
            {
                return MmdBonePoseSample.Identity;
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
            float normalizedFrame = (frame - previous.frame) / span;

            return new MmdBonePoseSample(
                new[]
                {
                    Lerp(Component(previous.translation, 0), Component(next.translation, 0), Interpolate(next.interpolation.translationX, normalizedFrame)),
                    Lerp(Component(previous.translation, 1), Component(next.translation, 1), Interpolate(next.interpolation.translationY, normalizedFrame)),
                    Lerp(Component(previous.translation, 2), Component(next.translation, 2), Interpolate(next.interpolation.translationZ, normalizedFrame))
                },
                Slerp(
                    QuaternionOrIdentity(previous.rotation),
                    QuaternionOrIdentity(next.rotation),
                    Interpolate(next.interpolation.rotation, normalizedFrame)));
        }

        private static int LowerBoundFrame(IReadOnlyList<MmdBoneKeyframeDefinition> keyframes, float frame)
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

        private static MmdBonePoseSample FromKeyframe(MmdBoneKeyframeDefinition keyframe)
        {
            return new MmdBonePoseSample(
                new[]
                {
                    Component(keyframe.translation, 0),
                    Component(keyframe.translation, 1),
                    Component(keyframe.translation, 2)
                },
                QuaternionOrIdentity(keyframe.rotation));
        }

        private static float Interpolate(byte[] controlPoints, float progress)
        {
            byte[] points = controlPoints is { Length: 4 } ? controlPoints : LinearInterpolation;
            return VmdBezier.Evaluate(points, progress);
        }

        private static float Component(float[] values, int index)
        {
            return values != null && values.Length > index ? values[index] : 0.0f;
        }

        private static float[] QuaternionOrIdentity(float[] values)
        {
            if (values == null || values.Length != 4)
            {
                return new[] { 0.0f, 0.0f, 0.0f, 1.0f };
            }

            return NormalizeQuaternion(values[0], values[1], values[2], values[3]);
        }

        private static float[] Slerp(float[] from, float[] to, float t)
        {
            float dot = from[0] * to[0] + from[1] * to[1] + from[2] * to[2] + from[3] * to[3];
            float tx = to[0];
            float ty = to[1];
            float tz = to[2];
            float tw = to[3];

            if (dot < 0.0f)
            {
                dot = -dot;
                tx = -tx;
                ty = -ty;
                tz = -tz;
                tw = -tw;
            }

            if (dot > 0.9995f)
            {
                return NormalizeQuaternion(
                    Lerp(from[0], tx, t),
                    Lerp(from[1], ty, t),
                    Lerp(from[2], tz, t),
                    Lerp(from[3], tw, t));
            }

            dot = Math.Clamp(dot, -1.0f, 1.0f);
            float theta0 = MathF.Acos(dot);
            float theta = theta0 * t;
            float sinTheta = MathF.Sin(theta);
            float sinTheta0 = MathF.Sin(theta0);
            float scaleFrom = MathF.Cos(theta) - dot * sinTheta / sinTheta0;
            float scaleTo = sinTheta / sinTheta0;

            return NormalizeQuaternion(
                scaleFrom * from[0] + scaleTo * tx,
                scaleFrom * from[1] + scaleTo * ty,
                scaleFrom * from[2] + scaleTo * tz,
                scaleFrom * from[3] + scaleTo * tw);
        }

        private static float[] NormalizeQuaternion(float x, float y, float z, float w)
        {
            float length = MathF.Sqrt(x * x + y * y + z * z + w * w);
            if (length <= 0.0f)
            {
                return new[] { 0.0f, 0.0f, 0.0f, 1.0f };
            }

            float inverse = 1.0f / length;
            return new[] { x * inverse, y * inverse, z * inverse, w * inverse };
        }

        private static float Lerp(float from, float to, float t)
        {
            return from + (to - from) * t;
        }
    }
}
