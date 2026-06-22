#nullable enable

using System;
using UnityEngine.Playables;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    [Obsolete(
        "Deprecated: superseded by MmdHumanoidAnimationMixerBehaviour (MmdHumanoidAnimationTrack). " +
        "Kept functional for existing timelines; see docs/design/humanoid-retarget-ux-alternatives.md.")]
    [Serializable]
    public sealed class MmdHumanoidRetargetBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (info.effectiveWeight <= 0.0f)
            {
                return;
            }

            MmdUnityPlaybackController? controller = playerData as MmdUnityPlaybackController;
            if (controller == null)
            {
                return;
            }

            controller.ApplyHumanoidRetargetFromTimeline();
        }
    }
}
