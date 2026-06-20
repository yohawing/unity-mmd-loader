#nullable enable

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    [TrackClipType(typeof(MmdHumanoidRetargetClip))]
    [TrackBindingType(typeof(MmdHumanoidRuntimeRetargeter))]
    public sealed class MmdHumanoidRetargetTrack : TrackAsset
    {
        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            MmdHumanoidRuntimeRetargeter? retargeter =
                director.GetGenericBinding(this) as MmdHumanoidRuntimeRetargeter;
            if (retargeter == null)
            {
                return;
            }

            foreach (MmdHumanoidRetargetBinding entry in retargeter.Entries)
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
        }
    }
}
