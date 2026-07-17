#nullable enable

using UnityEngine;
using UnityEngine.Playables;

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
        private PlayableDirector? timelineDirector;
        private double lastTimelineTime;
        private bool hasAppliedRootPose;
        private Vector3 lastRootPosition;
        private Quaternion lastRootRotation = Quaternion.identity;
        private Vector3 lastSourceRootPosition;
        private Quaternion lastSourceRootRotation = Quaternion.identity;

        internal bool IsTimelineEvaluationActive => timelineEvaluationActive;

        internal void BeginTimelineEvaluation(PlayableDirector? director)
        {
            timelineEvaluationActive = true;
            baselinePosition = transform.position;
            timelineDirector = director;
            hasAppliedRootPose = false;
        }

        internal void EndTimelineEvaluation()
        {
            timelineEvaluationActive = false;
            timelineDirector = null;
            hasAppliedRootPose = false;
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
            double timelineTime = timelineDirector != null ? timelineDirector.time : double.NaN;
            if (hasAppliedRootPose &&
                !double.IsNaN(timelineTime) &&
                System.Math.Abs(timelineTime - lastTimelineTime) <= 1e-7 &&
                Vector3.Distance(sourceRootPosition, lastSourceRootPosition) <= 1e-5f &&
                Quaternion.Angle(sourceRootRotation, lastSourceRootRotation) <= 0.01f)
            {
                transform.SetPositionAndRotation(lastRootPosition, lastRootRotation);
                return;
            }

            Vector3 rootPosition = sourceRootPosition;
            rootPosition.y = baselinePosition.y + clipRootVerticalOffset * animator.humanScale;
            Quaternion rootRotation = sourceRootRotation;
            transform.SetPositionAndRotation(rootPosition, rootRotation);
            lastTimelineTime = timelineTime;
            lastRootPosition = rootPosition;
            lastRootRotation = rootRotation;
            lastSourceRootPosition = sourceRootPosition;
            lastSourceRootRotation = sourceRootRotation;
            hasAppliedRootPose = true;
        }

        private void LateUpdate()
        {
            if (!timelineEvaluationActive || !hasAppliedRootPose)
            {
                return;
            }

            Animator animator = cachedAnimator != null
                ? cachedAnimator
                : cachedAnimator = GetComponent<Animator>();
            Vector3 rootPosition = transform.position;
            rootPosition.y = baselinePosition.y + clipRootVerticalOffset * animator.humanScale;
            transform.position = rootPosition;
            lastRootPosition.y = rootPosition.y;
        }
    }
}
