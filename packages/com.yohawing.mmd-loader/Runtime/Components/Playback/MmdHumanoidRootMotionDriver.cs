#nullable enable

using UnityEngine;

namespace Mmd.UnityIntegration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class MmdHumanoidRootMotionDriver : MonoBehaviour
    {
        [SerializeField] private float clipRootVerticalOffset;

        private Animator? cachedAnimator;
        private bool timelineEvaluationActive;
        private Vector3 baselinePosition;
        private bool hasAppliedRootPose;
        private float originalVerticalOffset;

        internal bool IsTimelineEvaluationActive => timelineEvaluationActive;

        internal void BeginTimelineEvaluation()
        {
            timelineEvaluationActive = true;
            baselinePosition = transform.position;
            hasAppliedRootPose = false;
            originalVerticalOffset = clipRootVerticalOffset;
            PrepareTimelineSample();
        }

        internal void PrepareTimelineSample()
        {
            // Only converted VMD clips animate this property. Clear it before sampling so
            // ordinary Humanoid clips keep Unity's vertical root motion, including after a switch.
            clipRootVerticalOffset = float.NaN;
        }

        internal void EndTimelineEvaluation()
        {
            timelineEvaluationActive = false;
            hasAppliedRootPose = false;
            clipRootVerticalOffset = originalVerticalOffset;
        }

        private void OnAnimatorMove()
        {
            if (!timelineEvaluationActive)
            {
                return;
            }

            Animator animator = cachedAnimator != null
                ? cachedAnimator
                : cachedAnimator = GetComponent<Animator>();
            Vector3 sourceRootPosition = animator.rootPosition;
            Quaternion sourceRootRotation = animator.rootRotation;
            Vector3 rootPosition = sourceRootPosition;
            if (float.IsFinite(clipRootVerticalOffset))
            {
                rootPosition.y = baselinePosition.y + clipRootVerticalOffset * animator.humanScale;
            }
            Quaternion rootRotation = sourceRootRotation;
            transform.SetPositionAndRotation(rootPosition, rootRotation);
            hasAppliedRootPose = true;
        }

        private void LateUpdate()
        {
            if (!timelineEvaluationActive || !hasAppliedRootPose || !float.IsFinite(clipRootVerticalOffset))
            {
                return;
            }

            Animator animator = cachedAnimator != null
                ? cachedAnimator
                : cachedAnimator = GetComponent<Animator>();
            Vector3 rootPosition = transform.position;
            rootPosition.y = baselinePosition.y + clipRootVerticalOffset * animator.humanScale;
            transform.position = rootPosition;
        }
    }
}
