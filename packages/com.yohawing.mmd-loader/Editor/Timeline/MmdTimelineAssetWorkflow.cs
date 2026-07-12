#nullable enable

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd;
using Mmd.Parser;
using Mmd.Timeline;
using Mmd.UnityIntegration;

namespace Mmd.Editor.Timeline
{
    internal static class MmdTimelineAssetWorkflow
    {
        public const string DefaultTrackName = "MMD VMD";

        public const string DefaultHumanoidTrackName = "MMD Humanoid";

        // Alternative C authoring: create the single MmdHumanoidAnimationTrack bound to the
        // controller. The proxy Animator is resolved automatically from the controller's GameObject
        // when a clip is added, so the user never wires a second track or an Animator reference.
        public static MmdHumanoidAnimationTrack CreateHumanoidAnimationTrack(
            TimelineAsset timelineAsset,
            PlayableDirector director,
            MmdUnityPlaybackController controller,
            string trackName = DefaultHumanoidTrackName)
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

            if (controller.GetComponent<Animator>() == null)
            {
                throw new InvalidOperationException(
                    "A Timeline humanoid track requires the MMD playback controller's GameObject to have a Humanoid Animator (the proxy avatar).");
            }

            MmdHumanoidAnimationTrack track = timelineAsset.CreateTrack<MmdHumanoidAnimationTrack>(
                null,
                string.IsNullOrWhiteSpace(trackName) ? DefaultHumanoidTrackName : trackName);
            director.SetGenericBinding(track, controller);
            return track;
        }

        // Add a standard Humanoid AnimationClip to the single humanoid track and auto-wire the proxy
        // Animator ExposedReference (the Animator on the controller's GameObject). Rejects clips that
        // a Humanoid proxy avatar cannot drive (legacy / non-human-motion), enforcing the "import a
        // real Humanoid clip" condition.
        public static TimelineClip CreateHumanoidAnimationClip(
            MmdHumanoidAnimationTrack track,
            AnimationClip animationClip,
            MmdUnityPlaybackController controller,
            PlayableDirector director,
            double startSeconds = 0.0)
        {
            if (track == null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            if (animationClip == null)
            {
                throw new ArgumentNullException(nameof(animationClip));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (director == null)
            {
                throw new ArgumentNullException(nameof(director));
            }

            MmdPlaybackTime.ValidateTime((float)startSeconds);

            if (animationClip.legacy)
            {
                throw new InvalidOperationException(
                    "The humanoid track requires a Humanoid (Mecanim) AnimationClip; a Legacy clip cannot drive the proxy avatar.");
            }

            if (!animationClip.humanMotion)
            {
                throw new InvalidOperationException(
                    "The humanoid track requires a Humanoid (muscle-space) AnimationClip; a Generic/transform clip does not retarget onto the proxy avatar's bones.");
            }

            Animator animator = controller.GetComponent<Animator>();
            if (animator == null)
            {
                throw new InvalidOperationException(
                    "A Timeline humanoid clip requires the MMD playback controller's GameObject to have a Humanoid Animator (the proxy avatar).");
            }

            TimelineClip clip = track.CreateClip<MmdHumanoidAnimationClip>();
            clip.start = startSeconds;
            clip.duration = animationClip.length > 0.0f ? animationClip.length : 1.0;
            clip.displayName = string.IsNullOrWhiteSpace(animationClip.name) ? "Humanoid Motion" : animationClip.name;

            var humanoidClip = (MmdHumanoidAnimationClip)clip.asset;
            humanoidClip.clip = animationClip;

            director.playableAsset = null;
            director.playableAsset = track.timelineAsset;
            director.RebuildGraph();

            return clip;
        }

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

            double duration = CalculateClipDurationSeconds(motionAsset.MaxFrame, frameRate);

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
