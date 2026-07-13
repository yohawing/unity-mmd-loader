#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    [Serializable]
    public sealed class MmdEditableRigBoneCorrection
    {
        [SerializeField] private string boneName = string.Empty;
        [SerializeField] private int boneIndex = -1;
        [SerializeField] private Vector3 localPositionDelta;
        [SerializeField] private Quaternion localRotationDelta = Quaternion.identity;
        [SerializeField] private Vector3 localScaleDelta;
        [SerializeField] private float weight = 1.0f;

        public MmdEditableRigBoneCorrection()
        {
        }

        public MmdEditableRigBoneCorrection(
            string boneName,
            int boneIndex,
            Vector3 localPositionDelta,
            Quaternion localRotationDelta,
            Vector3 localScaleDelta,
            float weight)
        {
            this.boneName = boneName ?? string.Empty;
            this.boneIndex = boneIndex;
            this.localPositionDelta = localPositionDelta;
            this.localRotationDelta = localRotationDelta;
            this.localScaleDelta = localScaleDelta;
            this.weight = weight;
        }

        public string BoneName => boneName;

        public int BoneIndex => boneIndex;

        public Vector3 LocalPositionDelta => localPositionDelta;

        public Quaternion LocalRotationDelta => localRotationDelta;

        public Vector3 LocalScaleDelta => localScaleDelta;

        public float Weight => weight;

        public void Validate(int correctionIndex)
        {
            if (string.IsNullOrWhiteSpace(boneName) && boneIndex < 0)
            {
                throw new InvalidOperationException($"editable-rig-invalid-correction: correction {correctionIndex} must specify boneName or boneIndex.");
            }

            if (boneIndex < -1)
            {
                throw new InvalidOperationException($"editable-rig-invalid-correction: correction {correctionIndex} boneIndex must be >= -1.");
            }

            ValidateFinite(localPositionDelta, correctionIndex, nameof(localPositionDelta));
            ValidateFinite(localRotationDelta, correctionIndex, nameof(localRotationDelta));
            ValidateFinite(localScaleDelta, correctionIndex, nameof(localScaleDelta));
            if (!IsFinite(weight) || weight < 0.0f || weight > 1.0f)
            {
                throw new InvalidOperationException($"editable-rig-invalid-correction: correction {correctionIndex} weight must be finite and in [0, 1].");
            }
        }

        private static void ValidateFinite(Vector3 value, int correctionIndex, string field)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
            {
                throw new InvalidOperationException($"editable-rig-invalid-correction: correction {correctionIndex} {field} must contain only finite values.");
            }
        }

        private static void ValidateFinite(Quaternion value, int correctionIndex, string field)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z) || !IsFinite(value.w))
            {
                throw new InvalidOperationException($"editable-rig-invalid-correction: correction {correctionIndex} {field} must contain only finite values.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

}
