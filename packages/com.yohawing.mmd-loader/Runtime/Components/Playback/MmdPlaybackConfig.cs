#nullable enable

using System;

namespace Mmd
{
    public readonly struct MmdPlaybackConfig
    {
        public MmdPlaybackConfig(
            float frameRate,
            int initialFrame,
            bool playOnStart)
        {
            FrameRate = frameRate;
            InitialFrame = initialFrame;
            PlayOnStart = playOnStart;
            Validate();
        }

        public float FrameRate { get; }

        public int InitialFrame { get; }

        public bool PlayOnStart { get; }

        public static MmdPlaybackConfig Default => new MmdPlaybackConfig(
            frameRate: 30.0f,
            initialFrame: 0,
            playOnStart: true);

        public static MmdPlaybackConfig Headless(float frameRate = 30.0f, int initialFrame = 0)
        {
            return new MmdPlaybackConfig(
                frameRate,
                initialFrame,
                playOnStart: false);
        }

        public void Validate()
        {
            MmdPlaybackTime.ValidateFrameRate(FrameRate);
            if (InitialFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(InitialFrame), "Initial frame must not be negative.");
            }
        }
    }
}
