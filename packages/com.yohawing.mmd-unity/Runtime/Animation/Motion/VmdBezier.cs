using System;

namespace Yohawing.MmdUnity.Motion
{
    public static class VmdBezier
    {
        private const float ControlPointScale = 1.0f / 127.0f;
        private const int SolveIterations = 24;

        public static float Evaluate(byte x1, byte y1, byte x2, byte y2, float progress)
        {
            if (float.IsNaN(progress) || float.IsInfinity(progress))
            {
                throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be finite.");
            }

            if (progress <= 0.0f)
            {
                return 0.0f;
            }

            if (progress >= 1.0f)
            {
                return 1.0f;
            }

            float cx1 = ClampControlPoint(x1);
            float cy1 = ClampControlPoint(y1);
            float cx2 = ClampControlPoint(x2);
            float cy2 = ClampControlPoint(y2);
            float t = SolveTForX(cx1, cx2, progress);
            return CubicBezier(cy1, cy2, t);
        }

        public static float Evaluate(byte[] controlPoints, float progress)
        {
            if (controlPoints == null)
            {
                throw new ArgumentNullException(nameof(controlPoints));
            }

            if (controlPoints.Length != 4)
            {
                throw new ArgumentException("VMD Bezier control point array must contain exactly four bytes.", nameof(controlPoints));
            }

            return Evaluate(controlPoints[0], controlPoints[1], controlPoints[2], controlPoints[3], progress);
        }

        private static float ClampControlPoint(byte value)
        {
            return Math.Clamp(value, (byte)0, (byte)127) * ControlPointScale;
        }

        private static float SolveTForX(float x1, float x2, float x)
        {
            float low = 0.0f;
            float high = 1.0f;
            float t = x;

            for (int i = 0; i < SolveIterations; i++)
            {
                t = (low + high) * 0.5f;
                float currentX = CubicBezier(x1, x2, t);
                if (currentX < x)
                {
                    low = t;
                }
                else
                {
                    high = t;
                }
            }

            return t;
        }

        private static float CubicBezier(float p1, float p2, float t)
        {
            float inverse = 1.0f - t;
            return 3.0f * inverse * inverse * t * p1
                + 3.0f * inverse * t * t * p2
                + t * t * t;
        }
    }
}
