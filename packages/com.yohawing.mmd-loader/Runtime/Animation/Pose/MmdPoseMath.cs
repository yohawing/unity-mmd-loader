using System;

namespace Mmd.Pose
{
    public static class MmdPoseMath
    {
        public static float[] LocalMatrix(float[] translation, float[] rotation, float[] scale)
        {
            float tx = Component(translation, 0, 0.0f);
            float ty = Component(translation, 1, 0.0f);
            float tz = Component(translation, 2, 0.0f);
            float sx = Component(scale, 0, 1.0f);
            float sy = Component(scale, 1, 1.0f);
            float sz = Component(scale, 2, 1.0f);
            float[] q = NormalizeQuaternion(rotation);

            float x = q[0];
            float y = q[1];
            float z = q[2];
            float w = q[3];
            float xx = x * x;
            float yy = y * y;
            float zz = z * z;
            float xy = x * y;
            float xz = x * z;
            float yz = y * z;
            float wx = w * x;
            float wy = w * y;
            float wz = w * z;

            return new[]
            {
                (1.0f - 2.0f * (yy + zz)) * sx, (2.0f * (xy - wz)) * sy, (2.0f * (xz + wy)) * sz, tx,
                (2.0f * (xy + wz)) * sx, (1.0f - 2.0f * (xx + zz)) * sy, (2.0f * (yz - wx)) * sz, ty,
                (2.0f * (xz - wy)) * sx, (2.0f * (yz + wx)) * sy, (1.0f - 2.0f * (xx + yy)) * sz, tz,
                0.0f, 0.0f, 0.0f, 1.0f
            };
        }

        public static float[] Multiply(float[] left, float[] right)
        {
            if (left == null || left.Length != 16)
            {
                throw new ArgumentException("Left matrix must contain exactly 16 values.", nameof(left));
            }

            if (HasNonFinite(left))
            {
                throw new ArgumentException("Left matrix must contain only finite values.", nameof(left));
            }

            if (right == null || right.Length != 16)
            {
                throw new ArgumentException("Right matrix must contain exactly 16 values.", nameof(right));
            }

            if (HasNonFinite(right))
            {
                throw new ArgumentException("Right matrix must contain only finite values.", nameof(right));
            }

            var result = new float[16];
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    result[row * 4 + column] =
                        left[row * 4 + 0] * right[column + 0]
                        + left[row * 4 + 1] * right[column + 4]
                        + left[row * 4 + 2] * right[column + 8]
                        + left[row * 4 + 3] * right[column + 12];
                }
            }

            return result;
        }

        private static float Component(float[] values, int index, float fallback)
        {
            if (values == null || values.Length <= index)
            {
                return fallback;
            }

            if (float.IsNaN(values[index]) || float.IsInfinity(values[index]))
            {
                throw new ArgumentException("Pose vector values must be finite.", nameof(values));
            }

            return values[index];
        }

        private static float[] NormalizeQuaternion(float[] values)
        {
            float x = Component(values, 0, 0.0f);
            float y = Component(values, 1, 0.0f);
            float z = Component(values, 2, 0.0f);
            float w = Component(values, 3, 1.0f);
            float length = MathF.Sqrt(x * x + y * y + z * z + w * w);
            if (length <= 0.0f)
            {
                return new[] { 0.0f, 0.0f, 0.0f, 1.0f };
            }

            float inverse = 1.0f / length;
            return new[] { x * inverse, y * inverse, z * inverse, w * inverse };
        }

        private static bool HasNonFinite(float[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
