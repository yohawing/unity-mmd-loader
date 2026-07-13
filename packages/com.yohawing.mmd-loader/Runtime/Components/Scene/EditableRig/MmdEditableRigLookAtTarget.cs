#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    [Serializable]
    public sealed class MmdEditableRigLookAtTarget
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private Transform? target;
        [SerializeField] private string boneName = string.Empty;
        [SerializeField] private int boneIndex = -1;
        [SerializeField] private Vector3 localForwardAxis = Vector3.forward;
        [SerializeField] private float weight = 1.0f;
        [SerializeField] private float maxAngleDegrees = 90.0f;

        public MmdEditableRigLookAtTarget()
        {
        }

        public MmdEditableRigLookAtTarget(
            Transform? target,
            string boneName,
            int boneIndex,
            Vector3 localForwardAxis,
            float weight,
            float maxAngleDegrees,
            bool enabled)
        {
            this.target = target;
            this.boneName = boneName ?? string.Empty;
            this.boneIndex = boneIndex;
            this.localForwardAxis = localForwardAxis;
            this.weight = weight;
            this.maxAngleDegrees = maxAngleDegrees;
            this.enabled = enabled;
        }

        public bool Enabled => enabled;

        public Transform? Target => target;

        public string BoneName => boneName;

        public int BoneIndex => boneIndex;

        public Vector3 LocalForwardAxis => localForwardAxis;

        public float Weight => weight;

        public float MaxAngleDegrees => maxAngleDegrees;

        public void Validate(int targetIndex)
        {
            if (string.IsNullOrWhiteSpace(boneName) && boneIndex < 0)
            {
                throw new InvalidOperationException($"editable-rig-invalid-look-at-target: target {targetIndex} must specify boneName or boneIndex.");
            }

            if (boneIndex < -1)
            {
                throw new InvalidOperationException($"editable-rig-invalid-look-at-target: target {targetIndex} boneIndex must be >= -1.");
            }

            if (!IsFinite(weight) || weight < 0.0f || weight > 1.0f)
            {
                throw new InvalidOperationException($"editable-rig-invalid-look-at-target: target {targetIndex} weight must be finite and in [0, 1].");
            }

            if (!IsFinite(maxAngleDegrees) || maxAngleDegrees <= 0.0f)
            {
                throw new InvalidOperationException($"editable-rig-invalid-look-at-target: target {targetIndex} maxAngleDegrees must be finite and positive.");
            }

            if (!IsFinite(localForwardAxis.x) || !IsFinite(localForwardAxis.y) || !IsFinite(localForwardAxis.z)
                || localForwardAxis.sqrMagnitude < 0.0001f)
            {
                throw new InvalidOperationException($"editable-rig-invalid-look-at-target: target {targetIndex} localForwardAxis must be finite and non-zero.");
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

}
