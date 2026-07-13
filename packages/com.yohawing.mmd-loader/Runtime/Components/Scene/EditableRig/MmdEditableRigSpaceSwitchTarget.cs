#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    [Serializable]
    public sealed class MmdEditableRigSpaceSwitchTarget
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private Transform? source;
        [SerializeField] private string boneName = string.Empty;
        [SerializeField] private int boneIndex = -1;
        [SerializeField] private MmdEditableRigSpaceSwitchSourceSpace sourceSpace;
        [SerializeField] private bool maintainOffset;
        [SerializeField] private Vector3 localPositionOffset;
        [SerializeField] private Quaternion localRotationOffset = Quaternion.identity;
        [SerializeField] private float weight = 1.0f;

        public MmdEditableRigSpaceSwitchTarget()
        {
        }

        public MmdEditableRigSpaceSwitchTarget(
            Transform? source,
            string boneName,
            int boneIndex,
            MmdEditableRigSpaceSwitchSourceSpace sourceSpace,
            bool maintainOffset,
            Vector3 localPositionOffset,
            Quaternion localRotationOffset,
            float weight,
            bool enabled)
        {
            this.source = source;
            this.boneName = boneName ?? string.Empty;
            this.boneIndex = boneIndex;
            this.sourceSpace = sourceSpace;
            this.maintainOffset = maintainOffset;
            this.localPositionOffset = localPositionOffset;
            this.localRotationOffset = localRotationOffset;
            this.weight = weight;
            this.enabled = enabled;
        }

        public bool Enabled => enabled;

        public Transform? Source => source;

        public string BoneName => boneName;

        public int BoneIndex => boneIndex;

        public MmdEditableRigSpaceSwitchSourceSpace SourceSpace => sourceSpace;

        public bool MaintainOffset => maintainOffset;

        public Vector3 LocalPositionOffset => localPositionOffset;

        public Quaternion LocalRotationOffset => localRotationOffset;

        public float Weight => weight;

        public void Validate(int targetIndex)
        {
            if (string.IsNullOrWhiteSpace(boneName) && boneIndex < 0)
            {
                throw new InvalidOperationException($"editable-rig-invalid-space-switch-target: target {targetIndex} must specify boneName or boneIndex.");
            }

            if (boneIndex < -1)
            {
                throw new InvalidOperationException($"editable-rig-invalid-space-switch-target: target {targetIndex} boneIndex must be >= -1.");
            }

            if (!IsFinite(weight) || weight < 0.0f || weight > 1.0f)
            {
                throw new InvalidOperationException($"editable-rig-invalid-space-switch-target: target {targetIndex} weight must be finite and in [0, 1].");
            }

            if (!Enum.IsDefined(typeof(MmdEditableRigSpaceSwitchSourceSpace), sourceSpace))
            {
                throw new InvalidOperationException($"editable-rig-invalid-space-switch-target: target {targetIndex} sourceSpace must be Local, World, or Parent.");
            }

            if (!IsFinite(localPositionOffset.x) || !IsFinite(localPositionOffset.y) || !IsFinite(localPositionOffset.z))
            {
                throw new InvalidOperationException($"editable-rig-invalid-space-switch-target: target {targetIndex} localPositionOffset must contain only finite values.");
            }

            if (!IsFinite(localRotationOffset.x) || !IsFinite(localRotationOffset.y) || !IsFinite(localRotationOffset.z) || !IsFinite(localRotationOffset.w))
            {
                throw new InvalidOperationException($"editable-rig-invalid-space-switch-target: target {targetIndex} localRotationOffset must contain only finite values.");
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
