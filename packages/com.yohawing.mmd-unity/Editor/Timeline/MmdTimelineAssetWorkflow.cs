#nullable enable

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Yohawing.MmdUnity;
using Yohawing.MmdUnity.Parser;
using Yohawing.MmdUnity.Timeline;
using Yohawing.MmdUnity.UnityIntegration;

namespace Yohawing.MmdUnity.Editor.Timeline
{
    public static class MmdTimelineAssetWorkflow
    {
        public const string DefaultTrackName = "MMD VMD";

        public static bool TryGetPlaybackController(GameObject? droppedObject, out MmdUnityPlaybackController? controller)
        {
            controller = droppedObject == null ? null : droppedObject.GetComponent<MmdUnityPlaybackController>();
            return controller != null && controller.HasModelSource;
        }

        public static MmdVmdTimelineTrack CreateVmdTrack(
            TimelineAsset timelineAsset,
            PlayableDirector director,
            MmdUnityPlaybackController controller,
            string trackName = DefaultTrackName)
        {
            if (timelineAsset == null)
            {
                throw new ArgumentNullException(nameof(timelineAsset));
            }

            if (director == null)
            {
                throw new ArgumentNullException(nameof(director));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (!controller.HasModelSource)
            {
                throw new InvalidOperationException("A Timeline VMD track requires an MMD playback controller with a PMX model source.");
            }

            MmdVmdTimelineTrack track = timelineAsset.CreateTrack<MmdVmdTimelineTrack>(
                null,
                string.IsNullOrWhiteSpace(trackName) ? DefaultTrackName : trackName);
            director.SetGenericBinding(track, controller);
            return track;
        }

        public static TimelineClip CreateVmdClip(
            MmdVmdTimelineTrack track,
            MmdVmdAsset motionAsset,
            MmdUnityPlaybackController controller,
            double startSeconds = 0.0,
            float frameRate = 30.0f,
            PlayableDirector? director = null)
        {
            if (track == null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            if (motionAsset == null)
            {
                throw new ArgumentNullException(nameof(motionAsset));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            MmdPlaybackTime.ValidateTime((float)startSeconds);
            MmdPlaybackTime.ValidateFrameRate(frameRate);
            if (!controller.HasModelSource)
            {
                throw new InvalidOperationException("A Timeline VMD clip requires an MMD playback controller with a PMX model source.");
            }

            MmdMotionDefinition motion = motionAsset.LoadMotion();
            double duration = CalculateClipDurationSeconds(motion, frameRate);

            TimelineClip clip = track.CreateClip<MmdVmdTimelineClip>();
            clip.start = startSeconds;
            clip.duration = duration;
            clip.displayName = string.IsNullOrWhiteSpace(motionAsset.name) ? "VMD Motion" : motionAsset.name;

            var mmdClip = (MmdVmdTimelineClip)clip.asset;
            mmdClip.MotionAsset = motionAsset;
            mmdClip.ModelSourceId = controller.ModelSourceId;
            mmdClip.MotionSourceId = string.IsNullOrWhiteSpace(motionAsset.SourceId) ? motionAsset.name : motionAsset.SourceId;
            mmdClip.FrameRate = frameRate;
            mmdClip.StartOffsetSeconds = 0.0f;
            mmdClip.LoopPolicy = MmdVmdTimelineLoopPolicy.None;

            if (director != null)
            {
                var exposedName = new PropertyName(Guid.NewGuid().ToString("N"));
                mmdClip.Controller = new ExposedReference<MmdUnityPlaybackController>
                {
                    exposedName = exposedName,
                    defaultValue = controller
                };
                director.SetReferenceValue(exposedName, controller);
                director.playableAsset = null;
                director.playableAsset = track.timelineAsset;
                director.RebuildGraph();
            }

            return clip;
        }

        public static double CalculateClipDurationSeconds(MmdMotionDefinition motion, float frameRate)
        {
            if (motion == null)
            {
                throw new ArgumentNullException(nameof(motion));
            }

            return CalculateClipDurationSeconds(motion.maxFrame, frameRate);
        }

        /// <summary>
        /// Clip duration in seconds from a (cached) max frame, so callers that already have the
        /// import-time summary value can avoid re-parsing the VMD just to size a clip.
        /// </summary>
        public static double CalculateClipDurationSeconds(int maxFrame, float frameRate)
        {
            MmdPlaybackTime.ValidateFrameRate(frameRate);
            return (Math.Max(0, maxFrame) + 1) / (double)frameRate;
        }

        public static MmdVmdTimelineTrack? FindFirstMmdVmdTrack(TimelineAsset timelineAsset)
        {
            if (timelineAsset == null)
            {
                throw new ArgumentNullException(nameof(timelineAsset));
            }

            return timelineAsset.GetOutputTracks().OfType<MmdVmdTimelineTrack>().FirstOrDefault();
        }
    }
}
