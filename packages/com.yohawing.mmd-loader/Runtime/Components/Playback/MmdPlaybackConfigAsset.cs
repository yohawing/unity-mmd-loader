#nullable enable

using UnityEngine;

namespace Mmd
{
    // CreateAssetMenu removed: this is an advanced/deferred runtime-read helper, not a primary creation surface.
    public sealed class MmdPlaybackConfigAsset : ScriptableObject
    {
        [SerializeField] private float frameRate = 30.0f;
        [SerializeField] private int initialFrame;
        [SerializeField] private bool playOnStart = true;

        public float FrameRate => frameRate;

        public int InitialFrame => initialFrame;

        public bool PlayOnStart => playOnStart;

        public MmdPlaybackConfig ToConfig()
        {
            return new MmdPlaybackConfig(frameRate, initialFrame, playOnStart);
        }

        public void Initialize(
            float playbackFrameRate = 30.0f,
            int startFrame = 0,
            bool shouldPlayOnStart = true)
        {
            MmdPlaybackConfig config = new(playbackFrameRate, startFrame, shouldPlayOnStart);
            frameRate = config.FrameRate;
            initialFrame = config.InitialFrame;
            playOnStart = config.PlayOnStart;
        }
    }
}
