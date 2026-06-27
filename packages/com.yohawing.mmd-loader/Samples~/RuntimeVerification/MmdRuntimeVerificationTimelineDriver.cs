#nullable enable

using System;
using System.Collections;
using Mmd.Timeline;
using Mmd.UnityIntegration;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Mmd.Samples.RuntimeVerification
{
    public sealed class MmdRuntimeVerificationTimelineDriver : IDisposable
    {
        private TimelineAsset? timelineAsset;

        public IEnumerator Play(
            PlayableDirector director,
            MmdUnityPlaybackController controller,
            float durationSeconds,
            float frameRate)
        {
            if (director == null)
            {
                throw new ArgumentNullException(nameof(director));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            timelineAsset = ScriptableObject.CreateInstance<TimelineAsset>();
            timelineAsset.name = "Runtime Verification Timeline";
            MmdVmdTimelineTrack track = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(
                null,
                "MMD VMD");
            TimelineClip clip = track.CreateClip<MmdVmdTimelineClip>();
            clip.displayName = "Runtime VMD";
            clip.start = 0.0;
            clip.duration = Math.Max(1.0 / Math.Max(1.0f, frameRate), durationSeconds);

            if (clip.asset is MmdVmdTimelineClip mmdClip)
            {
                mmdClip.FrameRate = frameRate;
                mmdClip.StartOffsetSeconds = 0.0f;
                mmdClip.LoopPolicy = MmdVmdTimelineLoopPolicy.None;
            }

            director.playableAsset = timelineAsset;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            director.extrapolationMode = DirectorWrapMode.None;
            director.SetGenericBinding(track, controller);
            director.time = 0.0;
            director.Play();

            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < durationSeconds)
            {
                yield return null;
            }

            director.Stop();
        }

        public void Dispose()
        {
            if (timelineAsset == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(timelineAsset);
            timelineAsset = null;
        }
    }
}
