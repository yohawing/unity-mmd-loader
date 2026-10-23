#nullable enable

using System;
using UnityEngine.Playables;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    [Serializable]
    public sealed class MmdHumanoidAnimationMixerBehaviour : PlayableBehaviour
    {
        private MmdUnityPlaybackController? controller;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            controller = playerData as MmdUnityPlaybackController;

            if (!HasActiveClipInput(playable))
            {
                controller?.NotifyHumanoidTimelinePaused(GetTimelineTime(playable));
                return;
            }

            if (controller != null)
            {
                controller.ApplyHumanoidRetargetFromTimeline(
                    GetTimelineTime(playable),
                    info.effectivePlayState == PlayState.Playing,
                    info.seekOccurred,
                    info.deltaTime);
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            controller?.NotifyHumanoidTimelinePaused(GetTimelineTime(playable));
        }

        public override void OnGraphStop(Playable playable)
        {
            controller?.NotifyHumanoidTimelinePaused(GetTimelineTime(playable));
        }

        private static double GetTimelineTime(Playable playable)
        {
            // With multiple Timeline outputs, a track mixer can remain at time zero while
            // its clips advance. Physics needs the owning Timeline's clock, not mixer time.
            var director = playable.GetGraph().GetResolver() as PlayableDirector;
            return director != null ? director.time : playable.GetTime();
        }

        private static bool HasActiveClipInput(Playable playable)
        {
            int count = playable.GetInputCount();
            for (int i = 0; i < count; i++)
            {
                if (playable.GetInputWeight(i) > 0f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
