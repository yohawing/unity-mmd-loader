#nullable enable

using System;
using UnityEngine.Playables;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    [Serializable]
    public sealed class MmdHumanoidAnimationMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!HasActiveClipInput(playable))
            {
                return;
            }

            if (playerData is MmdUnityPlaybackController controller)
            {
                controller.ApplyHumanoidRetargetFromTimeline();
            }
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
