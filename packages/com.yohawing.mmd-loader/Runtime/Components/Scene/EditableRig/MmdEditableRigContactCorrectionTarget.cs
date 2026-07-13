#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    [Serializable]
    public sealed class MmdEditableRigContactCorrectionTarget
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private Transform? contactSurface;
        [SerializeField] private string boneName = string.Empty;
        [SerializeField] private int boneIndex = -1;
        [SerializeField] private Vector3 worldOffset;
        [SerializeField] private float weight = 1.0f;
        [SerializeField] private float maxCorrectionDistance = float.MaxValue;

        public MmdEditableRigContactCorrectionTarget()
        {
        }

        public MmdEditableRigContactCorrectionTarget(
            Transform? contactSurface,
            string boneName,
            int boneIndex,
            Vector3 worldOffset,
            float weight,
            float maxCorrectionDistance,
            bool enabled)
        {
            this.contactSurface = contactSurface;
            this.boneName = boneName ?? string.Empty;
            this.boneIndex = boneIndex;
            this.worldOffset = worldOffset;
            this.weight = weight;
            this.maxCorrectionDistance = maxCorrectionDistance;
            this.enabled = enabled;
        }

        public bool Enabled => enabled;

        public Transform? ContactSurface => contactSurface;

        public string BoneName => boneName;

        public int BoneIndex => boneIndex;

        public Vector3 WorldOffset => worldOffset;

        public float Weight => weight;

        public float MaxCorrectionDistance => maxCorrectionDistance;

        public void Validate(int targetIndex)
        {
            if (string.IsNullOrWhiteSpace(boneName) && boneIndex < 0)
            {
                throw new InvalidOperationException($"editable-rig-invalid-contact-correction-target: target {targetIndex} must specify boneName or boneIndex.");
            }

            if (boneIndex < -1)
            {
                throw new InvalidOperationException($"editable-rig-invalid-contact-correction-target: target {targetIndex} boneIndex must be >= -1.");
            }

            if (!IsFinite(weight) || weight < 0.0f || weight > 1.0f)
            {
                throw new InvalidOperationException($"editable-rig-invalid-contact-correction-target: target {targetIndex} weight must be finite and in [0, 1].");
            }

            if (!IsFinite(maxCorrectionDistance) || maxCorrectionDistance < 0.0f)
            {
                throw new InvalidOperationException($"editable-rig-invalid-contact-correction-target: target {targetIndex} maxCorrectionDistance must be finite and non-negative.");
            }

            if (!IsFinite(worldOffset.x) || !IsFinite(worldOffset.y) || !IsFinite(worldOffset.z))
            {
                throw new InvalidOperationException($"editable-rig-invalid-contact-correction-target: target {targetIndex} worldOffset must contain only finite values.");
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

}
