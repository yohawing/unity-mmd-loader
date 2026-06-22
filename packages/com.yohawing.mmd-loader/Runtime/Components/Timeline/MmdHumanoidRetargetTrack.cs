#nullable enable

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    [Obsolete(
        "Deprecated: use MmdHumanoidAnimationTrack (a single track carrying a standard Humanoid " +
        "AnimationClip) instead of pairing a standard AnimationTrack with MmdHumanoidRetargetTrack. " +
        "Kept functional for existing timelines; see docs/design/humanoid-retarget-ux-alternatives.md.")]
    [TrackClipType(typeof(MmdHumanoidRetargetClip))]
    [TrackBindingType(typeof(MmdUnityPlaybackController))]
    public sealed class MmdHumanoidRetargetTrack : TrackAsset
    {
        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            MmdUnityPlaybackController? controller =
                director.GetGenericBinding(this) as MmdUnityPlaybackController;
            if (controller == null)
            {
                return;
            }

            foreach (MmdHumanoidRetargetBinding entry in controller.HumanoidRetargetEntries)
            {
                Transform? nativeTransform = entry.NativeTransform;
                if (nativeTransform != null)
                {
                    GameObject nativeObject = nativeTransform.gameObject;
                    driver.AddFromName<Transform>(nativeObject, "m_LocalRotation.x");
                    driver.AddFromName<Transform>(nativeObject, "m_LocalRotation.y");
                    driver.AddFromName<Transform>(nativeObject, "m_LocalRotation.z");
                    driver.AddFromName<Transform>(nativeObject, "m_LocalRotation.w");
                }

                if (!entry.CopyLocalPosition || entry.TranslationTargetTransform == null)
                {
                    continue;
                }

                GameObject translationTargetObject = entry.TranslationTargetTransform.gameObject;
                driver.AddFromName<Transform>(translationTargetObject, "m_LocalPosition.x");
                driver.AddFromName<Transform>(translationTargetObject, "m_LocalPosition.y");
                driver.AddFromName<Transform>(translationTargetObject, "m_LocalPosition.z");
            }

            foreach (MmdHumanoidAppendTransformBinding entry in controller.HumanoidAppendEntries)
            {
                Transform? targetTransform = entry.TargetTransform;
                if (targetTransform == null)
                {
                    continue;
                }

                GameObject targetObject = targetTransform.gameObject;
                if (entry.AppendRotation)
                {
                    driver.AddFromName<Transform>(targetObject, "m_LocalRotation.x");
                    driver.AddFromName<Transform>(targetObject, "m_LocalRotation.y");
                    driver.AddFromName<Transform>(targetObject, "m_LocalRotation.z");
                    driver.AddFromName<Transform>(targetObject, "m_LocalRotation.w");
                }

                if (entry.AppendTranslation)
                {
                    driver.AddFromName<Transform>(targetObject, "m_LocalPosition.x");
                    driver.AddFromName<Transform>(targetObject, "m_LocalPosition.y");
                    driver.AddFromName<Transform>(targetObject, "m_LocalPosition.z");
                }
            }
        }
    }
}
