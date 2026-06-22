#nullable enable

using System;

namespace Mmd.Motion
{
    public readonly struct MmdBonePoseSample
    {
        public MmdBonePoseSample(float[]? translation, float[]? rotation)
        {
            if (translation == null || translation.Length != 3)
            {
                throw new ArgumentException("Translation must contain exactly three values.", nameof(translation));
            }

            if (HasNonFinite(translation))
            {
                throw new ArgumentException("Translation must contain only finite values.", nameof(translation));
            }

            if (rotation == null || rotation.Length != 4)
            {
                throw new ArgumentException("Rotation must contain exactly four values.", nameof(rotation));
            }

            if (HasNonFinite(rotation))
            {
                throw new ArgumentException("Rotation must contain only finite values.", nameof(rotation));
            }

            Translation = translation;
            Rotation = rotation;
        }

        public float[] Translation { get; }

        public float[] Rotation { get; }

        public static MmdBonePoseSample Identity { get; } = new(
            new[] { 0.0f, 0.0f, 0.0f },
            new[] { 0.0f, 0.0f, 0.0f, 1.0f });

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
