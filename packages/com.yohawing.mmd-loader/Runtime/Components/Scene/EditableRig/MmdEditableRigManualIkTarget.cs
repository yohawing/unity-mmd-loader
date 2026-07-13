#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    [Serializable]
    public sealed class MmdEditableRigManualIkTarget
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private Transform? target;
        [SerializeField] private string effectorBoneName = string.Empty;
        [SerializeField] private int effectorBoneIndex = -1;
        [SerializeField] private List<string> chainBoneNames = new();
        [SerializeField] private List<int> chainBoneIndices = new();
        [SerializeField] private float weight = 1.0f;
        [SerializeField] private int iterationLimit = 8;

        public MmdEditableRigManualIkTarget()
        {
        }

        public MmdEditableRigManualIkTarget(
            Transform target,
            string effectorBoneName,
            int effectorBoneIndex,
            IReadOnlyList<string> chainBoneNames,
            IReadOnlyList<int> chainBoneIndices,
            float weight,
            int iterationLimit,
            bool enabled)
        {
            this.target = target;
            this.effectorBoneName = effectorBoneName ?? string.Empty;
            this.effectorBoneIndex = effectorBoneIndex;
            this.chainBoneNames = chainBoneNames != null ? new List<string>(chainBoneNames) : new List<string>();
            this.chainBoneIndices = chainBoneIndices != null ? new List<int>(chainBoneIndices) : new List<int>();
            this.weight = weight;
            this.iterationLimit = iterationLimit;
            this.enabled = enabled;
        }

        public bool Enabled => enabled;

        public Transform? Target => target;

        public string EffectorBoneName => effectorBoneName;

        public int EffectorBoneIndex => effectorBoneIndex;

        public IReadOnlyList<string> ChainBoneNames => chainBoneNames;

        public IReadOnlyList<int> ChainBoneIndices => chainBoneIndices;

        public float Weight => weight;

        public int IterationLimit => iterationLimit;

        public void Validate(int targetIndex)
        {
            if (string.IsNullOrWhiteSpace(effectorBoneName) && effectorBoneIndex < 0)
            {
                throw new InvalidOperationException($"editable-rig-invalid-manual-ik-target: target {targetIndex} must specify effectorBoneName or effectorBoneIndex.");
            }

            if (effectorBoneIndex < -1)
            {
                throw new InvalidOperationException($"editable-rig-invalid-manual-ik-target: target {targetIndex} effectorBoneIndex must be >= -1.");
            }

            if (!IsFinite(weight) || weight < 0.0f || weight > 1.0f)
            {
                throw new InvalidOperationException($"editable-rig-invalid-manual-ik-target: target {targetIndex} weight must be finite and in [0, 1].");
            }

            if (iterationLimit < 1 || iterationLimit > 32)
            {
                throw new InvalidOperationException($"editable-rig-invalid-manual-ik-target: target {targetIndex} iterationLimit must be in [1, 32].");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

}
