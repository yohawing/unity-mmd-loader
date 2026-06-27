#nullable enable

using System;
using System.Collections.Generic;
using Mmd.Parser;

namespace Mmd.Motion
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
    /// Interpolation uses the next keyframe's 24-byte VMD camera block as six per-channel bezier
    /// curves: position X/Y/Z, rotation, distance, and view angle. <see cref="MmdCameraState.Perspective"/>
    /// is a step value taken from the previous keyframe (it is a flag, not interpolated).
    /// </summary>
    public static class VmdCameraSampler
    {
        private const int CameraInterpolationLength = 24;
        private const int PositionXChannel = 0;
        private const int PositionYChannel = 1;
        private const int PositionZChannel = 2;
        private const int RotationChannel = 3;
        private const int DistanceChannel = 4;
        private const int ViewAngleChannel = 5;

        private static readonly byte[] LinearInterpolation = { 20, 20, 107, 107 };

        public static MmdCameraState Sample(IReadOnlyList<MmdCameraKeyframeDefinition>? keyframes, float frame)
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
            float distanceT = Interpolate(next.interpolation, DistanceChannel, t);
            float positionXT = Interpolate(next.interpolation, PositionXChannel, t);
            float positionYT = Interpolate(next.interpolation, PositionYChannel, t);
            float positionZT = Interpolate(next.interpolation, PositionZChannel, t);
            float rotationT = Interpolate(next.interpolation, RotationChannel, t);
            float viewAngleT = Interpolate(next.interpolation, ViewAngleChannel, t);

            return new MmdCameraState(
                Lerp(previous.distance, next.distance, distanceT),
                new[]
                {
                    Lerp(Component(previous.position, 0), Component(next.position, 0), positionXT),
                    Lerp(Component(previous.position, 1), Component(next.position, 1), positionYT),
                    Lerp(Component(previous.position, 2), Component(next.position, 2), positionZT)
                },
                new[]
                {
                    Lerp(Component(previous.rotation, 0), Component(next.rotation, 0), rotationT),
                    Lerp(Component(previous.rotation, 1), Component(next.rotation, 1), rotationT),
                    Lerp(Component(previous.rotation, 2), Component(next.rotation, 2), rotationT)
                },
                Lerp(previous.viewAngle, next.viewAngle, viewAngleT),
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

        private static float Interpolate(byte[]? interpolation, int channel, float progress)
        {
            if (interpolation is not { Length: >= CameraInterpolationLength })
            {
                return VmdBezier.Evaluate(LinearInterpolation, progress);
            }

            return VmdBezier.Evaluate(
                interpolation[channel],
                interpolation[channel + 6],
                interpolation[channel + 12],
                interpolation[channel + 18],
                progress);
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
