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
                controller?.NotifyHumanoidTimelinePaused(playable.GetTime());
                return;
            }

            if (controller != null)
            {
                controller.ApplyHumanoidRetargetFromTimeline(
                    playable.GetTime(),
                    info.effectivePlayState == PlayState.Playing,
                    info.seekOccurred,
                    info.deltaTime);
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            controller?.NotifyHumanoidTimelinePaused(playable.GetTime());
        }

        public override void OnGraphStop(Playable playable)
        {
            controller?.NotifyHumanoidTimelinePaused(playable.GetTime());
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
