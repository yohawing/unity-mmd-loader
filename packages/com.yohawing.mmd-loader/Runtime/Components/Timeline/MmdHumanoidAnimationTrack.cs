#nullable enable

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mmd.UnityIntegration;

namespace Mmd.Timeline
{
    // A single Timeline track that both animates the proxy avatar (standard Humanoid
    // AnimationClip) and drives the native MMD model (humanoid retarget side-effect).
    // Each clip's CreatePlayable returns an AnimationClipPlayable connected to the mixer
    // tree, so the graph auto-advances clip time during both scrub and continuous play.
    [TrackClipType(typeof(MmdHumanoidAnimationClip))]
    [TrackBindingType(typeof(MmdUnityPlaybackController))]
    public sealed class MmdHumanoidAnimationTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<MmdHumanoidAnimationMixerBehaviour>.Create(graph, inputCount);
        }

        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            MmdUnityPlaybackController? controller =
                director.GetGenericBinding(this) as MmdUnityPlaybackController;
            if (controller == null)
            {
                return;
            }

            GameObject controllerObject = controller.gameObject;
            driver.AddFromName<Transform>(controllerObject, "m_LocalPosition.x");
            driver.AddFromName<Transform>(controllerObject, "m_LocalPosition.y");
            driver.AddFromName<Transform>(controllerObject, "m_LocalPosition.z");
            driver.AddFromName<Transform>(controllerObject, "m_LocalRotation.x");
            driver.AddFromName<Transform>(controllerObject, "m_LocalRotation.y");
            driver.AddFromName<Transform>(controllerObject, "m_LocalRotation.z");
            driver.AddFromName<Transform>(controllerObject, "m_LocalRotation.w");

            foreach (MmdHumanoidRetargetBinding entry in controller.HumanoidRetargetEntries)
            {
                Transform? proxyTransform = entry.ProxyTransform;
                if (proxyTransform != null)
                {
                    GameObject proxyObject = proxyTransform.gameObject;
                    driver.AddFromName<Transform>(proxyObject, "m_LocalPosition.x");
                    driver.AddFromName<Transform>(proxyObject, "m_LocalPosition.y");
                    driver.AddFromName<Transform>(proxyObject, "m_LocalPosition.z");
                    driver.AddFromName<Transform>(proxyObject, "m_LocalRotation.x");
                    driver.AddFromName<Transform>(proxyObject, "m_LocalRotation.y");
                    driver.AddFromName<Transform>(proxyObject, "m_LocalRotation.z");
                    driver.AddFromName<Transform>(proxyObject, "m_LocalRotation.w");
                }

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
