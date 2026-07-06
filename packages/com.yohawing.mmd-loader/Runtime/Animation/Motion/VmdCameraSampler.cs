#nullable enable

namespace Mmd.Motion
{
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

        public static MmdCameraState Default =>
            new MmdCameraState(0.0f, new[] { 0.0f, 0.0f, 0.0f }, new[] { 0.0f, 0.0f, 0.0f }, 30.0f, true);
    }
}
