#nullable enable

using System;
using UnityEngine.Playables;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    [Serializable]
    public sealed class MmdHumanoidRetargetBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (info.effectiveWeight <= 0.0f)
            {
                return;
            }

            MmdHumanoidRuntimeRetargeter? retargeter = playerData as MmdHumanoidRuntimeRetargeter;
            if (retargeter == null)
            {
                return;
            }

            retargeter.ApplyRetargetFromTimeline();
        }
    }
}
