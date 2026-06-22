#nullable enable

using System;

namespace Mmd.Pose
{
    public static class MmdQuaternionMath
    {
        public static float[] Multiply(float[]? left, float[]? right)
        {
            float lx = Component(left, 0, 0.0f);
            float ly = Component(left, 1, 0.0f);
            float lz = Component(left, 2, 0.0f);
            float lw = Component(left, 3, 1.0f);
            float rx = Component(right, 0, 0.0f);
            float ry = Component(right, 1, 0.0f);
            float rz = Component(right, 2, 0.0f);
            float rw = Component(right, 3, 1.0f);

            return Normalize(
                lw * rx + lx * rw + ly * rz - lz * ry,
                lw * ry - lx * rz + ly * rw + lz * rx,
                lw * rz + lx * ry - ly * rx + lz * rw,
                lw * rw - lx * rx - ly * ry - lz * rz);
        }

        public static float[] Slerp(float[]? from, float[]? to, float t)
        {
            if (float.IsNaN(t) || float.IsInfinity(t))
            {
                throw new ArgumentOutOfRangeException(nameof(t), "Interpolation factor must be finite.");
            }

            float[] a = Normalize(from);
            float[] b = Normalize(to);
            float dot = a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3];

            if (dot < 0.0f)
            {
                dot = -dot;
                b = new[] { -b[0], -b[1], -b[2], -b[3] };
            }

            if (dot > 0.9995f)
            {
                return Normalize(
                    Lerp(a[0], b[0], t),
                    Lerp(a[1], b[1], t),
                    Lerp(a[2], b[2], t),
                    Lerp(a[3], b[3], t));
            }

            dot = Math.Clamp(dot, -1.0f, 1.0f);
            float theta0 = MathF.Acos(dot);
            float theta = theta0 * t;
            float sinTheta = MathF.Sin(theta);
            float sinTheta0 = MathF.Sin(theta0);
            float scaleFrom = MathF.Cos(theta) - dot * sinTheta / sinTheta0;
            float scaleTo = sinTheta / sinTheta0;

            return Normalize(
                scaleFrom * a[0] + scaleTo * b[0],
                scaleFrom * a[1] + scaleTo * b[1],
                scaleFrom * a[2] + scaleTo * b[2],
                scaleFrom * a[3] + scaleTo * b[3]);
        }

        private static float[] Normalize(float[]? values)
        {
            return Normalize(
                Component(values, 0, 0.0f),
                Component(values, 1, 0.0f),
                Component(values, 2, 0.0f),
                Component(values, 3, 1.0f));
        }

        private static float[] Normalize(float x, float y, float z, float w)
        {
            float length = MathF.Sqrt(x * x + y * y + z * z + w * w);
            if (length <= 0.0f)
            {
                return new[] { 0.0f, 0.0f, 0.0f, 1.0f };
            }

            float inverse = 1.0f / length;
            return new[] { x * inverse, y * inverse, z * inverse, w * inverse };
        }

        private static float Component(float[]? values, int index, float fallback)
        {
            if (values == null || values.Length <= index)
            {
                return fallback;
            }

            if (float.IsNaN(values[index]) || float.IsInfinity(values[index]))
            {
                throw new ArgumentException("Quaternion values must be finite.", nameof(values));
            }

            return values[index];
        }

        private static float Lerp(float from, float to, float t)
        {
            return from + (to - from) * t;
        }
    }
}
