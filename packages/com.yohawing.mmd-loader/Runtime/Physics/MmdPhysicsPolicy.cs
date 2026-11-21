#nullable enable

using System;

namespace Mmd.Physics
{
    internal static class MmdPhysicsPolicy
    {
        public static void ValidateLiveStepInput(int frame, float deltaTime)
        {
            MmdPlaybackTime.ValidateFrame(frame);
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Physics live deltaTime must be a non-negative finite value.");
            }
        }

    }
}
