#nullable enable

namespace Mmd.Motion
{
    public readonly struct MmdLightState
    {
        public MmdLightState(float[] color, float[] direction)
        {
            Color = color ?? new[] { 0.0f, 0.0f, 0.0f };
            Direction = direction ?? new[] { 0.0f, 0.0f, 0.0f };
        }

        public float[] Color { get; }

        public float[] Direction { get; }

        public static MmdLightState Default =>
            new MmdLightState(new[] { 0.6f, 0.6f, 0.6f }, new[] { -0.5f, -1.0f, 0.5f });
    }
}
