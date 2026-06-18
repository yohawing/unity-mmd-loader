#nullable enable

using System;

namespace Mmd
{
    [Serializable]
    public sealed class MmdPlaybackTimeMapping
    {
        public string frameSelectionPolicy = "floor-unclamped";
        public float time;
        public float frameRate;
        public int frame;
        public int motionMaxFrame;
        public bool beyondMotionMaxFrame;
    }

    public static class MmdPlaybackTime
    {
        public static int ToFrame(float time, float frameRate)
        {
            ValidateTime(time);
            ValidateFrameRate(frameRate);
            return (int)MathF.Floor(time * frameRate);
        }

        public static MmdPlaybackTimeMapping Map(float time, float frameRate, int motionMaxFrame)
        {
            ValidateTime(time);
            ValidateFrameRate(frameRate);
            ValidateFrame(motionMaxFrame);
            int frame = ToFrame(time, frameRate);
            return new MmdPlaybackTimeMapping
            {
                time = time,
                frameRate = frameRate,
                frame = frame,
                motionMaxFrame = motionMaxFrame,
                beyondMotionMaxFrame = frame > motionMaxFrame
            };
        }

        public static float ToTime(int frame, float frameRate)
        {
            ValidateFrame(frame);
            ValidateFrameRate(frameRate);
            return frame / frameRate;
        }

        public static void ValidateFrame(int frame)
        {
            if (frame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Frame index must not be negative.");
            }
        }

        public static void ValidateTime(float time)
        {
            if (time < 0.0f || float.IsNaN(time) || float.IsInfinity(time))
            {
                throw new ArgumentOutOfRangeException(nameof(time), "Time must be a non-negative finite value.");
            }
        }

        public static void ValidateFrameRate(float frameRate)
        {
            if (frameRate <= 0.0f || float.IsNaN(frameRate) || float.IsInfinity(frameRate))
            {
                throw new ArgumentOutOfRangeException(nameof(frameRate), "Frame rate must be a finite positive value.");
            }
        }
    }
}
