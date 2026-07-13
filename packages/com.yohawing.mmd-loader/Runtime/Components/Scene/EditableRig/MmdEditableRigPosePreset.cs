#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    [Serializable]
    public sealed class MmdEditableRigPosePreset
    {
        [SerializeField] private string presetName = string.Empty;
        [SerializeField] private string sourceModelId = string.Empty;
        [SerializeField] private bool enabled = true;
        [SerializeField] private float weight = 1.0f;
        [SerializeField] private List<MmdEditableRigPosePresetBoneEntry> boneEntries = new();

        public MmdEditableRigPosePreset()
        {
        }

        public MmdEditableRigPosePreset(string presetName, string sourceModelId)
        {
            this.presetName = presetName ?? string.Empty;
            this.sourceModelId = sourceModelId ?? string.Empty;
        }

        public string PresetName => presetName;

        public string SourceModelId => sourceModelId;

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        public float Weight
        {
            get => weight;
            set => weight = ValidateWeight(value);
        }

        public IReadOnlyList<MmdEditableRigPosePresetBoneEntry> BoneEntries => boneEntries;

        public void AddBoneEntry(
            string boneName,
            int boneIndex,
            Vector3 localPositionDelta,
            Quaternion localRotationDelta,
            Vector3 localScaleDelta,
            float weight = 1.0f)
        {
            boneEntries.Add(new MmdEditableRigPosePresetBoneEntry(
                boneName, boneIndex, localPositionDelta, localRotationDelta, localScaleDelta, weight));
        }

        public void Validate(int presetIndex)
        {
            if (!IsFinite(weight) || weight < 0.0f || weight > 1.0f)
            {
                throw new InvalidOperationException($"editable-rig-invalid-pose-preset: preset {presetIndex} weight must be finite and in [0, 1].");
            }
        }

        private static float ValidateWeight(float value)
        {
            if (!IsFinite(value) || value < 0.0f || value > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Pose preset weight must be finite and in [0, 1].");
            }

            return value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

}
