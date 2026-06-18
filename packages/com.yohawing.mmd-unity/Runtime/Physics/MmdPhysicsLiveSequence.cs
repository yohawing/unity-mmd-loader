#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Yohawing.MmdUnity.Physics
{
    [Serializable]
    public sealed class MmdPhysicsLiveSequenceReport
    {
        public string physicsMode = "live";
        public string backendName = string.Empty;
        public bool backendAvailable = true;
        public string status = string.Empty;
        public int[] frames = Array.Empty<int>();
        public float frameRate;
        public float[] deltaTimes = Array.Empty<float>();
        public int resetCount;
        public int stepCount;
        public int rigidbodyCount;
        public int jointCount;
    }

    public static class MmdPhysicsLiveSequence
    {
        public static MmdPhysicsLiveSequenceReport Run(
            IMmdPhysicsBackend backend,
            IReadOnlyList<int> frames,
            float frameRate,
            Action<int, float>? beforeStep = null)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            if (frames.Count == 0)
            {
                throw new ArgumentException("At least one frame is required.", nameof(frames));
            }

            MmdPlaybackTime.ValidateFrameRate(frameRate);
            var seenFrames = new HashSet<int>(frames.Count);
            int previousFrame = -1;
            var normalizedFrames = new int[frames.Count];
            var deltaTimes = new float[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                int frame = frames[i];
                MmdPlaybackTime.ValidateFrame(frame);
                if (!seenFrames.Add(frame))
                {
                    throw new ArgumentException("Physics live frames must be unique.", nameof(frames));
                }

                if (i > 0 && frame <= previousFrame)
                {
                    throw new ArgumentException("Physics live frames must be ascending.", nameof(frames));
                }

                normalizedFrames[i] = frame;
                deltaTimes[i] = i == 0 ? 0.0f : (frame - previousFrame) / frameRate;
                previousFrame = frame;
            }

            backend.Reset();
            for (int i = 0; i < normalizedFrames.Length; i++)
            {
                beforeStep?.Invoke(normalizedFrames[i], deltaTimes[i]);
                backend.Step(normalizedFrames[i], deltaTimes[i]);
            }

            return new MmdPhysicsLiveSequenceReport
            {
                backendName = backend.Name,
                backendAvailable = true,
                status = "complete",
                frames = normalizedFrames,
                frameRate = frameRate,
                deltaTimes = deltaTimes,
                resetCount = 1,
                stepCount = normalizedFrames.Length
            };
        }
    }
}
