#nullable enable

using System;
using System.Collections.Generic;
using Yohawing.MmdUnity.Parser;

namespace Yohawing.MmdUnity.Motion
{
    /// <summary>
    /// An interpolated MMD camera state in MMD camera space (look-at + distance rig).
    /// <see cref="Position"/> is the look-at target (x, y, z); <see cref="Rotation"/> is the
    /// Euler orientation in radians; <see cref="Distance"/> is the orbit distance; and
    /// <see cref="ViewAngle"/> is the field of view in degrees. Conversion to a Unity Camera
    /// transform/FOV is a later slice; this is the engine-agnostic sampled value.
    /// </summary>
    public readonly struct MmdCameraState
    {
        public MmdCameraState(float distance, float[] position, float[] rotation, float viewAngle, bool perspective)
        {
            Distance = distance;
            Position = position ?? new[] { 0.0f, 0.0f, 0.0f };
            Rotation = rotation ?? new[] { 0.0f, 0.0f, 0.0f };
            ViewAngle = viewAngle;
            Perspective = perspective;
        }

        public float Distance { get; }

        public float[] Position { get; }

        public float[] Rotation { get; }

        public float ViewAngle { get; }

        public bool Perspective { get; }

        /// <summary>Neutral camera used when a track has no keyframes (MMD default 30° FOV, perspective on).</summary>
        public static MmdCameraState Default =>
            new MmdCameraState(0.0f, new[] { 0.0f, 0.0f, 0.0f }, new[] { 0.0f, 0.0f, 0.0f }, 30.0f, true);
    }

    /// <summary>
    /// Samples a VMD camera track (<see cref="MmdCameraKeyframeDefinition"/>) at an arbitrary frame.
    ///
    /// Interpolation is LINEAR between the surrounding keyframes (and exact at keyframes). MMD's
    /// per-channel bezier easing for the camera interpolation block is NOT applied yet: the 24-byte
    /// camera interpolation layout differs from the bone layout, and applying it without a golden
    /// camera fixture to verify the channel byte order risks a silently-wrong curve. Bezier easing
    /// is a dedicated follow-up slice (verified against a real camera VMD). <see cref="MmdCameraState.Perspective"/>
    /// is a step value taken from the previous keyframe (it is a flag, not interpolated).
    /// </summary>
    public static class VmdCameraSampler
    {
        public static MmdCameraState Sample(IReadOnlyList<MmdCameraKeyframeDefinition> keyframes, float frame)
        {
            if (keyframes == null)
            {
                throw new ArgumentNullException(nameof(keyframes));
            }

            if (float.IsNaN(frame) || float.IsInfinity(frame))
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Frame must be finite.");
            }

            MmdCameraKeyframeDefinition? previous = null;
            MmdCameraKeyframeDefinition? next = null;

            for (int i = 0; i < keyframes.Count; i++)
            {
                MmdCameraKeyframeDefinition candidate = keyframes[i];
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
                return MmdCameraState.Default;
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

            return new MmdCameraState(
                Lerp(previous.distance, next.distance, t),
                new[]
                {
                    Lerp(Component(previous.position, 0), Component(next.position, 0), t),
                    Lerp(Component(previous.position, 1), Component(next.position, 1), t),
                    Lerp(Component(previous.position, 2), Component(next.position, 2), t)
                },
                new[]
                {
                    Lerp(Component(previous.rotation, 0), Component(next.rotation, 0), t),
                    Lerp(Component(previous.rotation, 1), Component(next.rotation, 1), t),
                    Lerp(Component(previous.rotation, 2), Component(next.rotation, 2), t)
                },
                Lerp(previous.viewAngle, next.viewAngle, t),
                previous.perspective);
        }

        private static MmdCameraState FromKeyframe(MmdCameraKeyframeDefinition keyframe)
        {
            return new MmdCameraState(
                keyframe.distance,
                new[]
                {
                    Component(keyframe.position, 0),
                    Component(keyframe.position, 1),
                    Component(keyframe.position, 2)
                },
                new[]
                {
                    Component(keyframe.rotation, 0),
                    Component(keyframe.rotation, 1),
                    Component(keyframe.rotation, 2)
                },
                keyframe.viewAngle,
                keyframe.perspective);
        }

        private static float Component(float[] values, int index)
        {
            return values != null && values.Length > index ? values[index] : 0.0f;
        }

        private static float Lerp(float from, float to, float t)
        {
            return from + (to - from) * t;
        }
    }
}
