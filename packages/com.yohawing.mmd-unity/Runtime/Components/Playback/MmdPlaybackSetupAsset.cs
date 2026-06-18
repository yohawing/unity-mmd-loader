#nullable enable

using System;
using UnityEngine;

namespace Yohawing.MmdUnity
{
    // CreateAssetMenu removed: this is an advanced/deferred runtime-read helper, not a primary creation surface.
    public sealed class MmdPlaybackSetupAsset : ScriptableObject
    {
        [SerializeField] private MmdPmxAsset? modelAsset;
        [SerializeField] private MmdVmdAsset? motionAsset;
        [SerializeField] private float frameRate = 30.0f;
        [SerializeField] private int initialFrame;
        [SerializeField] private bool playOnStart = true;

        public MmdPmxAsset? ModelAsset => modelAsset;

        public MmdVmdAsset? MotionAsset => motionAsset;

        public float FrameRate => frameRate;

        public int InitialFrame => initialFrame;

        public bool PlayOnStart => playOnStart;

        public MmdPlaybackConfig ToConfig()
        {
            return new MmdPlaybackConfig(frameRate, initialFrame, playOnStart);
        }

        public void Initialize(
            MmdPmxAsset model,
            MmdVmdAsset motion,
            float playbackFrameRate = 30.0f,
            int startFrame = 0,
            bool shouldPlayOnStart = true)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            MmdPlaybackTime.ValidateFrameRate(playbackFrameRate);
            if (startFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startFrame), "Initial frame must not be negative.");
            }

            modelAsset = model;
            motionAsset = motion;
            frameRate = playbackFrameRate;
            initialFrame = startFrame;
            playOnStart = shouldPlayOnStart;
        }
    }
}
