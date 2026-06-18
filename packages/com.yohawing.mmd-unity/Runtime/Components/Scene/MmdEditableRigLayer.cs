#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yohawing.MmdUnity.UnityIntegration
{
    [DisallowMultipleComponent]
    public sealed class MmdEditableRigLayer : MonoBehaviour
    {
        [SerializeField] private bool editableRigEnabled;
        [SerializeField] private float layerWeight = 1.0f;
        [SerializeField] private List<MmdEditableRigBoneCorrection> boneCorrections = new();
        [SerializeField] private List<MmdEditableRigManualIkTarget> manualIkTargets = new();
        [SerializeField] private List<MmdEditableRigPosePreset> posePresets = new();
        [SerializeField] private bool bypassPosePresets;
        [SerializeField] private List<MmdEditableRigLookAtTarget> lookAtTargets = new();
        [SerializeField] private List<MmdEditableRigSpaceSwitchTarget> spaceSwitchTargets = new();
        [SerializeField] private List<MmdEditableRigContactCorrectionTarget> contactCorrectionTargets = new();

        public bool EditableRigEnabled
        {
            get => editableRigEnabled;
            set => editableRigEnabled = value;
        }

        public float LayerWeight
        {
            get => layerWeight;
            set => layerWeight = ValidateWeight(value);
        }

        public IReadOnlyList<MmdEditableRigBoneCorrection> BoneCorrections => boneCorrections;

        public IReadOnlyList<MmdEditableRigManualIkTarget> ManualIkTargets => manualIkTargets;

        public IReadOnlyList<MmdEditableRigPosePreset> PosePresets => posePresets;

        public bool BypassPosePresets
        {
            get => bypassPosePresets;
            set => bypassPosePresets = value;
        }

        public IReadOnlyList<MmdEditableRigLookAtTarget> LookAtTargets => lookAtTargets;

        public IReadOnlyList<MmdEditableRigSpaceSwitchTarget> SpaceSwitchTargets => spaceSwitchTargets;

        public IReadOnlyList<MmdEditableRigContactCorrectionTarget> ContactCorrectionTargets => contactCorrectionTargets;

        public void ClearBoneCorrections()
        {
            boneCorrections.Clear();
        }

        public void ClearManualIkTargets()
        {
            manualIkTargets.Clear();
        }

        public void AddPosePreset(MmdEditableRigPosePreset preset)
        {
            if (preset == null)
            {
                throw new ArgumentNullException(nameof(preset));
            }

            posePresets.Add(preset);
        }

        public void ClearPosePresets()
        {
            posePresets.Clear();
        }

        public void AddLookAtTarget(
            Transform? target,
            string boneName,
            int boneIndex,
            Vector3 localForwardAxis,
            float weight = 1.0f,
            float maxAngleDegrees = 90.0f,
            bool enabled = true)
        {
            lookAtTargets.Add(new MmdEditableRigLookAtTarget(target, boneName, boneIndex, localForwardAxis, weight, maxAngleDegrees, enabled));
        }

        public void ClearLookAtTargets()
        {
            lookAtTargets.Clear();
        }

        public void AddSpaceSwitchTarget(
            Transform? source,
            string boneName,
            int boneIndex,
            MmdEditableRigSpaceSwitchSourceSpace sourceSpace,
            bool maintainOffset,
            Vector3 localPositionOffset,
            Quaternion localRotationOffset,
            float weight = 1.0f,
            bool enabled = true)
        {
            spaceSwitchTargets.Add(new MmdEditableRigSpaceSwitchTarget(
                source, boneName, boneIndex, sourceSpace, maintainOffset,
                localPositionOffset, localRotationOffset, weight, enabled));
        }

        public void ClearSpaceSwitchTargets()
        {
            spaceSwitchTargets.Clear();
        }

        public void AddContactCorrectionTarget(
            Transform? contactSurface,
            string boneName,
            int boneIndex,
            Vector3 worldOffset,
            float weight = 1.0f,
            float maxCorrectionDistance = float.MaxValue,
            bool enabled = true)
        {
            contactCorrectionTargets.Add(new MmdEditableRigContactCorrectionTarget(
                contactSurface, boneName, boneIndex, worldOffset, weight, maxCorrectionDistance, enabled));
        }

        public void ClearContactCorrectionTargets()
        {
            contactCorrectionTargets.Clear();
        }

        public void AddBoneCorrection(
            string boneName,
            int boneIndex,
            Vector3 localPositionDelta,
            Quaternion localRotationDelta,
            Vector3 localScaleDelta,
            float weight = 1.0f)
        {
            boneCorrections.Add(new MmdEditableRigBoneCorrection(
                boneName,
                boneIndex,
                localPositionDelta,
                localRotationDelta,
                localScaleDelta,
                weight));
        }

        public void AddManualIkTarget(
            Transform target,
            string effectorBoneName,
            int effectorBoneIndex,
            IReadOnlyList<string> chainBoneNames,
            IReadOnlyList<int> chainBoneIndices,
            float weight = 1.0f,
            int iterationLimit = 8,
            bool enabled = true)
        {
            manualIkTargets.Add(new MmdEditableRigManualIkTarget(
                target,
                effectorBoneName,
                effectorBoneIndex,
                chainBoneNames,
                chainBoneIndices,
                weight,
                iterationLimit,
                enabled));
        }

        public MmdEditableRigLayerDiagnostics ApplyAfterRuntimePose(
            MmdUnityModelInstance instance,
            string executionStage)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (string.IsNullOrWhiteSpace(executionStage))
            {
                throw new ArgumentException("Execution stage is required.", nameof(executionStage));
            }

            float validatedWeight = ValidateWeight(layerWeight);
            layerWeight = validatedWeight;
            bool shouldApply = isActiveAndEnabled && editableRigEnabled && validatedWeight > 0.0f;
            if (!shouldApply)
            {
                return new MmdEditableRigLayerDiagnostics
                {
                    layerFound = true,
                    componentEnabled = isActiveAndEnabled,
                    editableRigEnabled = editableRigEnabled,
                    layerWeight = validatedWeight,
                    executionStage = executionStage,
                    transformState = "native-only",
                    noOpReason = ResolveNoOpReason(isActiveAndEnabled, editableRigEnabled, validatedWeight),
                    correctedBoneCount = 0,
                    solvedManualIkTargetCount = 0,
                    skippedManualIkTargetCount = 0,
                    worstManualIkDistance = 0.0f,
                    manualIkSkippedReasons = string.Empty,
                    solvedLookAtTargetCount = 0,
                    skippedLookAtTargetCount = 0,
                    solvedSpaceSwitchTargetCount = 0,
                    skippedSpaceSwitchTargetCount = 0,
                    solvedContactCorrectionTargetCount = 0,
                    skippedContactCorrectionTargetCount = 0,
                    maxLayerDelta = 0.0f,
                    meanLayerDelta = 0.0f
                };
            }

            return ApplyEnabledCorrections(instance, executionStage, validatedWeight);
        }

        private MmdEditableRigLayerDiagnostics ApplyEnabledCorrections(
            MmdUnityModelInstance instance,
            string executionStage,
            float validatedLayerWeight)
        {
            var usedBoneSlots = new HashSet<int>(boneCorrections.Count);
            int correctedCount = 0;
            float maxDelta = 0.0f;
            double sumDelta = 0.0;
            ApplyBoneCorrections(instance, validatedLayerWeight, usedBoneSlots, ref correctedCount, ref maxDelta, ref sumDelta);
            if (!bypassPosePresets)
            {
                ApplyPosePresets(instance, validatedLayerWeight, ref correctedCount, ref maxDelta, ref sumDelta);
            }

            MmdEditableRigManualIkDiagnostics manualIk = ApplyManualIkTargets(instance, validatedLayerWeight);
            MmdEditableRigLookAtDiagnostics lookAt = ApplyLookAtTargets(instance, validatedLayerWeight);
            MmdEditableRigSpaceSwitchDiagnostics spaceSwitch = ApplySpaceSwitchTargets(instance, validatedLayerWeight);
            MmdEditableRigContactCorrectionDiagnostics contactCorrection = ApplyContactCorrectionTargets(instance, validatedLayerWeight);

            return new MmdEditableRigLayerDiagnostics
            {
                layerFound = true,
                componentEnabled = isActiveAndEnabled,
                editableRigEnabled = editableRigEnabled,
                layerWeight = validatedLayerWeight,
                executionStage = executionStage,
                transformState = "post-editable-rig",
                noOpReason = correctedCount == 0 && manualIk.solvedTargetCount == 0 && lookAt.solvedTargetCount == 0 && spaceSwitch.solvedTargetCount == 0 && contactCorrection.solvedTargetCount == 0 ? "no-corrections" : string.Empty,
                correctedBoneCount = correctedCount,
                solvedManualIkTargetCount = manualIk.solvedTargetCount,
                skippedManualIkTargetCount = manualIk.skippedTargetCount,
                worstManualIkDistance = manualIk.worstDistance,
                manualIkSkippedReasons = manualIk.skippedReasons,
                solvedLookAtTargetCount = lookAt.solvedTargetCount,
                skippedLookAtTargetCount = lookAt.skippedTargetCount,
                solvedSpaceSwitchTargetCount = spaceSwitch.solvedTargetCount,
                skippedSpaceSwitchTargetCount = spaceSwitch.skippedTargetCount,
                solvedContactCorrectionTargetCount = contactCorrection.solvedTargetCount,
                skippedContactCorrectionTargetCount = contactCorrection.skippedTargetCount,
                maxLayerDelta = maxDelta,
                meanLayerDelta = correctedCount == 0 ? 0.0f : (float)(sumDelta / correctedCount)
            };
        }

        private void ApplyBoneCorrections(
            MmdUnityModelInstance instance,
            float validatedLayerWeight,
            HashSet<int> usedBoneSlots,
            ref int correctedCount,
            ref float maxDelta,
            ref double sumDelta)
        {
            for (int i = 0; i < boneCorrections.Count; i++)
            {
                MmdEditableRigBoneCorrection correction = boneCorrections[i];
                correction.Validate(i);
                int slot = ResolveBoneSlot(instance, correction, i);
                if (!usedBoneSlots.Add(slot))
                {
                    throw new InvalidOperationException($"editable-rig-duplicate-bone-correction: correction {i} targets duplicate bone slot {slot}.");
                }

                Transform bone = instance.BoneTransforms[slot];
                Vector3 beforePosition = bone.localPosition;
                Quaternion beforeRotation = bone.localRotation;
                Vector3 beforeScale = bone.localScale;
                float effectiveWeight = validatedLayerWeight * correction.Weight;
                bone.localPosition += correction.LocalPositionDelta * effectiveWeight;
                bone.localRotation *= Quaternion.Slerp(Quaternion.identity, correction.LocalRotationDelta, effectiveWeight);
                bone.localScale += correction.LocalScaleDelta * effectiveWeight;

                float positionDelta = Vector3.Distance(beforePosition, bone.localPosition);
                float rotationDelta = Quaternion.Angle(beforeRotation, bone.localRotation) / 180.0f;
                float scaleDelta = Vector3.Distance(beforeScale, bone.localScale);
                float boneDelta = Mathf.Max(positionDelta, rotationDelta, scaleDelta);
                maxDelta = Mathf.Max(maxDelta, boneDelta);
                sumDelta += boneDelta;
                correctedCount++;
            }
        }

        private void ApplyPosePresets(
            MmdUnityModelInstance instance,
            float validatedLayerWeight,
            ref int correctedCount,
            ref float maxDelta,
            ref double sumDelta)
        {
            for (int pi = 0; pi < posePresets.Count; pi++)
            {
                MmdEditableRigPosePreset preset = posePresets[pi];
                preset.Validate(pi);
                if (!preset.Enabled || preset.Weight <= 0.0f)
                {
                    continue;
                }

                float presetWeight = validatedLayerWeight * preset.Weight;
                IReadOnlyList<MmdEditableRigPosePresetBoneEntry> entries = preset.BoneEntries;
                for (int ei = 0; ei < entries.Count; ei++)
                {
                    MmdEditableRigPosePresetBoneEntry entry = entries[ei];
                    entry.Validate(pi, ei);
                    if (entry.Weight <= 0.0f)
                    {
                        continue;
                    }

                    int slot = ResolveBoneSlot(
                        instance.BoneTransforms,
                        entry.BoneName,
                        entry.BoneIndex,
                        $"pose preset {pi} entry {ei}");
                    Transform bone = instance.BoneTransforms[slot];
                    Vector3 beforePosition = bone.localPosition;
                    Quaternion beforeRotation = bone.localRotation;
                    Vector3 beforeScale = bone.localScale;
                    float effectiveWeight = presetWeight * entry.Weight;
                    bone.localPosition += entry.LocalPositionDelta * effectiveWeight;
                    bone.localRotation *= Quaternion.Slerp(Quaternion.identity, entry.LocalRotationDelta, effectiveWeight);
                    bone.localScale += entry.LocalScaleDelta * effectiveWeight;

                    float positionDelta = Vector3.Distance(beforePosition, bone.localPosition);
                    float rotationDelta = Quaternion.Angle(beforeRotation, bone.localRotation) / 180.0f;
                    float scaleDelta = Vector3.Distance(beforeScale, bone.localScale);
                    float boneDelta = Mathf.Max(positionDelta, rotationDelta, scaleDelta);
                    maxDelta = Mathf.Max(maxDelta, boneDelta);
                    sumDelta += boneDelta;
                    correctedCount++;
                }
            }
        }

        private MmdEditableRigManualIkDiagnostics ApplyManualIkTargets(
            MmdUnityModelInstance instance,
            float validatedLayerWeight)
        {
            var usedEffectorSlots = new HashSet<int>(manualIkTargets.Count);
            var skippedReasons = new List<string>(manualIkTargets.Count);
            int solvedCount = 0;
            int skippedCount = 0;
            float worstDistance = 0.0f;
            for (int i = 0; i < manualIkTargets.Count; i++)
            {
                MmdEditableRigManualIkTarget target = manualIkTargets[i];
                if (!target.Enabled)
                {
                    skippedCount++;
                    skippedReasons.Add($"target-{i}:disabled");
                    continue;
                }

                if (target.Target == null)
                {
                    skippedCount++;
                    skippedReasons.Add($"target-{i}:missing-target");
                    continue;
                }

                if (float.IsNaN(target.Weight) || float.IsInfinity(target.Weight) || target.Weight < 0.0f || target.Weight > 1.0f)
                {
                    target.Validate(i);
                }

                float effectiveWeight = validatedLayerWeight * target.Weight;
                if (effectiveWeight <= 0.0f)
                {
                    skippedCount++;
                    skippedReasons.Add($"target-{i}:zero-weight");
                    continue;
                }

                target.Validate(i);
                int effectorSlot = ResolveBoneSlot(
                    instance.BoneTransforms,
                    target.EffectorBoneName,
                    target.EffectorBoneIndex,
                    $"manual IK target {i} effector");
                if (!usedEffectorSlots.Add(effectorSlot))
                {
                    throw new InvalidOperationException($"editable-rig-duplicate-manual-ik-target: target {i} duplicates effector bone slot {effectorSlot}.");
                }

                int[] chainSlots = ResolveManualIkChain(instance.BoneTransforms, target, i);
                float distance = SolveManualIkTarget(
                    instance.BoneTransforms,
                    target.Target.position,
                    effectorSlot,
                    chainSlots,
                    target.IterationLimit,
                    effectiveWeight);
                worstDistance = Mathf.Max(worstDistance, distance);
                solvedCount++;
            }

            return new MmdEditableRigManualIkDiagnostics
            {
                solvedTargetCount = solvedCount,
                skippedTargetCount = skippedCount,
                worstDistance = worstDistance,
                skippedReasons = string.Join(";", skippedReasons)
            };
        }

        private MmdEditableRigLookAtDiagnostics ApplyLookAtTargets(
            MmdUnityModelInstance instance,
            float validatedLayerWeight)
        {
            int solvedCount = 0;
            int skippedCount = 0;
            for (int i = 0; i < lookAtTargets.Count; i++)
            {
                MmdEditableRigLookAtTarget lookAt = lookAtTargets[i];
                if (!lookAt.Enabled)
                {
                    skippedCount++;
                    continue;
                }

                if (lookAt.Target == null)
                {
                    skippedCount++;
                    continue;
                }

                float effectiveWeight = validatedLayerWeight * lookAt.Weight;
                if (effectiveWeight <= 0.0f)
                {
                    skippedCount++;
                    continue;
                }

                lookAt.Validate(i);
                int slot = ResolveBoneSlot(instance.BoneTransforms, lookAt.BoneName, lookAt.BoneIndex, $"look-at target {i}");
                Transform bone = instance.BoneTransforms[slot];
                ApplySingleLookAt(bone, lookAt.Target.position, lookAt.LocalForwardAxis, lookAt.MaxAngleDegrees, effectiveWeight);
                solvedCount++;
            }

            return new MmdEditableRigLookAtDiagnostics { solvedTargetCount = solvedCount, skippedTargetCount = skippedCount };
        }

        private MmdEditableRigSpaceSwitchDiagnostics ApplySpaceSwitchTargets(
            MmdUnityModelInstance instance,
            float validatedLayerWeight)
        {
            int solvedCount = 0;
            int skippedCount = 0;
            for (int i = 0; i < spaceSwitchTargets.Count; i++)
            {
                MmdEditableRigSpaceSwitchTarget target = spaceSwitchTargets[i];
                if (!target.Enabled)
                {
                    skippedCount++;
                    continue;
                }

                if (target.Source == null)
                {
                    skippedCount++;
                    continue;
                }

                float effectiveWeight = validatedLayerWeight * target.Weight;
                if (effectiveWeight <= 0.0f)
                {
                    skippedCount++;
                    continue;
                }

                target.Validate(i);
                int slot = ResolveBoneSlot(instance.BoneTransforms, target.BoneName, target.BoneIndex, $"space switch target {i}");
                Transform bone = instance.BoneTransforms[slot];
                ApplySingleSpaceSwitch(
                    bone,
                    target.Source,
                    target.SourceSpace,
                    target.MaintainOffset,
                    target.LocalPositionOffset,
                    target.LocalRotationOffset,
                    effectiveWeight);
                solvedCount++;
            }

            return new MmdEditableRigSpaceSwitchDiagnostics { solvedTargetCount = solvedCount, skippedTargetCount = skippedCount };
        }

        private MmdEditableRigContactCorrectionDiagnostics ApplyContactCorrectionTargets(
            MmdUnityModelInstance instance,
            float validatedLayerWeight)
        {
            int solvedCount = 0;
            int skippedCount = 0;
            for (int i = 0; i < contactCorrectionTargets.Count; i++)
            {
                MmdEditableRigContactCorrectionTarget target = contactCorrectionTargets[i];
                if (!target.Enabled)
                {
                    skippedCount++;
                    continue;
                }

                if (target.ContactSurface == null)
                {
                    skippedCount++;
                    continue;
                }

                float effectiveWeight = validatedLayerWeight * target.Weight;
                if (effectiveWeight <= 0.0f)
                {
                    skippedCount++;
                    continue;
                }

                target.Validate(i);
                int slot = ResolveBoneSlot(instance.BoneTransforms, target.BoneName, target.BoneIndex, $"contact correction target {i}");
                Transform bone = instance.BoneTransforms[slot];
                Vector3 targetWorldPos = target.ContactSurface.position + target.WorldOffset;
                Vector3 currentWorldPos = bone.position;
                Vector3 displacement = targetWorldPos - currentWorldPos;
                float distanceMag = displacement.magnitude;
                if (distanceMag > target.MaxCorrectionDistance && distanceMag > 0.0f)
                {
                    displacement = displacement * (target.MaxCorrectionDistance / distanceMag);
                }

                bone.position = currentWorldPos + displacement * effectiveWeight;
                solvedCount++;
            }

            return new MmdEditableRigContactCorrectionDiagnostics { solvedTargetCount = solvedCount, skippedTargetCount = skippedCount };
        }

        private static void ApplySingleSpaceSwitch(
            Transform bone,
            Transform source,
            MmdEditableRigSpaceSwitchSourceSpace sourceSpace,
            bool maintainOffset,
            Vector3 localPositionOffset,
            Quaternion localRotationOffset,
            float effectiveWeight)
        {
            switch (sourceSpace)
            {
                case MmdEditableRigSpaceSwitchSourceSpace.Local:
                {
                    Vector3 targetLocalPos = source.localPosition;
                    Quaternion targetLocalRot = source.localRotation;
                    if (maintainOffset)
                    {
                        targetLocalPos += localPositionOffset;
                        targetLocalRot *= localRotationOffset;
                    }

                    bone.localPosition = Vector3.Lerp(bone.localPosition, targetLocalPos, effectiveWeight);
                    bone.localRotation = Quaternion.Slerp(bone.localRotation, targetLocalRot, effectiveWeight);
                    break;
                }

                case MmdEditableRigSpaceSwitchSourceSpace.World:
                {
                    bone.position = Vector3.Lerp(bone.position, source.position, effectiveWeight);
                    bone.rotation = Quaternion.Slerp(bone.rotation, source.rotation, effectiveWeight);
                    if (maintainOffset)
                    {
                        bone.localPosition += localPositionOffset;
                        bone.localRotation *= localRotationOffset;
                    }

                    break;
                }

                case MmdEditableRigSpaceSwitchSourceSpace.Parent:
                {
                    // Express source transform in driven bone's parent space.
                    // If bone is parentless, fall back to world-position behavior.
                    Transform? parent = bone.parent;
                    Vector3 targetLocalPos;
                    Quaternion targetLocalRot;
                    if (parent != null)
                    {
                        targetLocalPos = parent.InverseTransformPoint(source.position);
                        targetLocalRot = Quaternion.Inverse(parent.rotation) * source.rotation;
                    }
                    else
                    {
                        targetLocalPos = source.position;
                        targetLocalRot = source.rotation;
                    }

                    if (maintainOffset)
                    {
                        targetLocalPos += localPositionOffset;
                        targetLocalRot *= localRotationOffset;
                    }

                    bone.localPosition = Vector3.Lerp(bone.localPosition, targetLocalPos, effectiveWeight);
                    bone.localRotation = Quaternion.Slerp(bone.localRotation, targetLocalRot, effectiveWeight);
                    break;
                }
            }
        }

        private static void ApplySingleLookAt(
            Transform bone,
            Vector3 targetWorldPosition,
            Vector3 localForwardAxis,
            float maxAngleDegrees,
            float effectiveWeight)
        {
            Vector3 normalizedForward = localForwardAxis.normalized;
            Vector3 currentWorldForward = bone.TransformDirection(normalizedForward);
            Vector3 toTarget = targetWorldPosition - bone.position;
            if (toTarget.sqrMagnitude < 0.0000001f)
            {
                return;
            }

            Vector3 desiredWorldForward = toTarget.normalized;
            Quaternion worldDelta = Quaternion.FromToRotation(currentWorldForward, desiredWorldForward);
            float angle = Quaternion.Angle(Quaternion.identity, worldDelta);
            if (angle > maxAngleDegrees)
            {
                worldDelta = Quaternion.Slerp(Quaternion.identity, worldDelta, maxAngleDegrees / angle);
            }

            bone.rotation = Quaternion.Slerp(Quaternion.identity, worldDelta, effectiveWeight) * bone.rotation;
        }

        private void OnValidate()
        {
            if (float.IsNaN(layerWeight) || float.IsInfinity(layerWeight))
            {
                layerWeight = 0.0f;
                return;
            }

            layerWeight = Mathf.Clamp01(layerWeight);
        }

        private static int ResolveBoneSlot(MmdUnityModelInstance instance, MmdEditableRigBoneCorrection correction, int correctionIndex)
        {
            return ResolveBoneSlot(
                instance.BoneTransforms,
                correction.BoneName,
                correction.BoneIndex,
                $"correction {correctionIndex}");
        }

        private static int ResolveBoneSlot(Transform[] bones, string boneName, int boneIndex, string label)
        {
            if (boneIndex >= 0)
            {
                if (boneIndex >= bones.Length)
                {
                    throw new InvalidOperationException($"editable-rig-unknown-bone: {label} boneIndex {boneIndex} is out of range.");
                }

                if (!string.IsNullOrWhiteSpace(boneName)
                    && !string.Equals(bones[boneIndex].name, boneName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"editable-rig-bone-mismatch: {label} boneName '{boneName}' does not match boneIndex {boneIndex} name '{bones[boneIndex].name}'.");
                }

                return boneIndex;
            }

            for (int i = 0; i < bones.Length; i++)
            {
                if (string.Equals(bones[i].name, boneName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            throw new InvalidOperationException($"editable-rig-unknown-bone: {label} boneName '{boneName}' was not found.");
        }

        private static int[] ResolveManualIkChain(Transform[] bones, MmdEditableRigManualIkTarget target, int targetIndex)
        {
            bool useIndices = target.ChainBoneIndices.Count > 0;
            bool useNames = target.ChainBoneNames.Count > 0;
            if (!useIndices && !useNames)
            {
                throw new InvalidOperationException($"editable-rig-invalid-manual-ik-chain: target {targetIndex} must specify at least one chain bone.");
            }

            if (useIndices && useNames && target.ChainBoneIndices.Count != target.ChainBoneNames.Count)
            {
                throw new InvalidOperationException($"editable-rig-invalid-manual-ik-chain: target {targetIndex} chain bone name/index counts must match.");
            }

            int count = useIndices ? target.ChainBoneIndices.Count : target.ChainBoneNames.Count;
            var result = new int[count];
            var usedSlots = new HashSet<int>(count);
            for (int i = 0; i < count; i++)
            {
                string boneName = useNames && i < target.ChainBoneNames.Count ? target.ChainBoneNames[i] : string.Empty;
                int boneIndex = useIndices ? target.ChainBoneIndices[i] : -1;
                int slot = ResolveBoneSlot(bones, boneName, boneIndex, $"manual IK target {targetIndex} chain {i}");
                if (!usedSlots.Add(slot))
                {
                    throw new InvalidOperationException($"editable-rig-invalid-manual-ik-chain: target {targetIndex} has duplicate chain bone slot {slot}.");
                }

                result[i] = slot;
            }

            return result;
        }

        private static float SolveManualIkTarget(
            Transform[] bones,
            Vector3 targetPosition,
            int effectorSlot,
            int[] chainSlots,
            int iterationLimit,
            float effectiveWeight)
        {
            Transform effector = bones[effectorSlot];
            int iterations = Mathf.Clamp(iterationLimit, 1, 32);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                for (int i = chainSlots.Length - 1; i >= 0; i--)
                {
                    Transform link = bones[chainSlots[i]];
                    Vector3 linkPosition = link.position;
                    Vector3 toEffector = effector.position - linkPosition;
                    Vector3 toTarget = targetPosition - linkPosition;
                    if (toEffector.sqrMagnitude < 0.0000001f || toTarget.sqrMagnitude < 0.0000001f)
                    {
                        continue;
                    }

                    Quaternion delta = Quaternion.FromToRotation(toEffector, toTarget);
                    link.rotation = Quaternion.Slerp(Quaternion.identity, delta, effectiveWeight) * link.rotation;
                }
            }

            return Vector3.Distance(effector.position, targetPosition);
        }

        private static float ValidateWeight(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Layer weight must be finite.");
            }

            if (value < 0.0f || value > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Layer weight must be in [0, 1].");
            }

            return value;
        }

        private static string ResolveNoOpReason(bool componentEnabled, bool layerEnabled, float weight)
        {
            if (!componentEnabled)
            {
                return "component-disabled";
            }

            if (!layerEnabled)
            {
                return "layer-disabled";
            }

            if (weight <= 0.0f)
            {
                return "zero-weight";
            }

            return string.Empty;
        }
    }

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

    internal sealed class MmdEditableRigLookAtDiagnostics
    {
        public int solvedTargetCount;
        public int skippedTargetCount;
    }

    internal sealed class MmdEditableRigSpaceSwitchDiagnostics
    {
        public int solvedTargetCount;
        public int skippedTargetCount;
    }

    internal sealed class MmdEditableRigContactCorrectionDiagnostics
    {
        public int solvedTargetCount;
        public int skippedTargetCount;
    }

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

    internal sealed class MmdEditableRigManualIkDiagnostics
    {
        public int solvedTargetCount;
        public int skippedTargetCount;
        public float worstDistance;
        public string skippedReasons = string.Empty;
    }

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

    [Serializable]
    public sealed class MmdEditableRigPosePresetBoneEntry
    {
        [SerializeField] private string boneName = string.Empty;
        [SerializeField] private int boneIndex = -1;
        [SerializeField] private Vector3 localPositionDelta;
        [SerializeField] private Quaternion localRotationDelta = Quaternion.identity;
        [SerializeField] private Vector3 localScaleDelta;
        [SerializeField] private float weight = 1.0f;

        public MmdEditableRigPosePresetBoneEntry()
        {
        }

        public MmdEditableRigPosePresetBoneEntry(
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

        public void Validate(int presetIndex, int entryIndex)
        {
            if (string.IsNullOrWhiteSpace(boneName) && boneIndex < 0)
            {
                throw new InvalidOperationException($"editable-rig-invalid-pose-preset: preset {presetIndex} entry {entryIndex} must specify boneName or boneIndex.");
            }

            if (boneIndex < -1)
            {
                throw new InvalidOperationException($"editable-rig-invalid-pose-preset: preset {presetIndex} entry {entryIndex} boneIndex must be >= -1.");
            }

            ValidateFinite(localPositionDelta, presetIndex, entryIndex, nameof(localPositionDelta));
            ValidateFinite(localRotationDelta, presetIndex, entryIndex, nameof(localRotationDelta));
            ValidateFinite(localScaleDelta, presetIndex, entryIndex, nameof(localScaleDelta));
            if (!IsFinite(weight) || weight < 0.0f || weight > 1.0f)
            {
                throw new InvalidOperationException($"editable-rig-invalid-pose-preset: preset {presetIndex} entry {entryIndex} weight must be finite and in [0, 1].");
            }
        }

        private static void ValidateFinite(Vector3 value, int presetIndex, int entryIndex, string field)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
            {
                throw new InvalidOperationException($"editable-rig-invalid-pose-preset: preset {presetIndex} entry {entryIndex} {field} must contain only finite values.");
            }
        }

        private static void ValidateFinite(Quaternion value, int presetIndex, int entryIndex, string field)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z) || !IsFinite(value.w))
            {
                throw new InvalidOperationException($"editable-rig-invalid-pose-preset: preset {presetIndex} entry {entryIndex} {field} must contain only finite values.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

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

    [Serializable]
    public sealed class MmdEditableRigLayerDiagnostics
    {
        public bool layerFound;
        public bool componentEnabled;
        public bool editableRigEnabled;
        public float layerWeight;
        public string executionStage = string.Empty;
        public string transformState = string.Empty;
        public string noOpReason = string.Empty;
        public int correctedBoneCount;
        public int solvedManualIkTargetCount;
        public int skippedManualIkTargetCount;
        public float worstManualIkDistance;
        public string manualIkSkippedReasons = string.Empty;
        public int solvedLookAtTargetCount;
        public int skippedLookAtTargetCount;
        public int solvedSpaceSwitchTargetCount;
        public int skippedSpaceSwitchTargetCount;
        public int solvedContactCorrectionTargetCount;
        public int skippedContactCorrectionTargetCount;
        public float maxLayerDelta;
        public float meanLayerDelta;

        public static MmdEditableRigLayerDiagnostics NotFound(string executionStage)
        {
            return new MmdEditableRigLayerDiagnostics
            {
                layerFound = false,
                componentEnabled = false,
                editableRigEnabled = false,
                layerWeight = 0.0f,
                executionStage = executionStage,
                transformState = "native-only",
                noOpReason = "layer-missing",
                correctedBoneCount = 0,
                solvedManualIkTargetCount = 0,
                skippedManualIkTargetCount = 0,
                worstManualIkDistance = 0.0f,
                manualIkSkippedReasons = string.Empty,
                solvedLookAtTargetCount = 0,
                skippedLookAtTargetCount = 0,
                solvedSpaceSwitchTargetCount = 0,
                skippedSpaceSwitchTargetCount = 0,
                solvedContactCorrectionTargetCount = 0,
                skippedContactCorrectionTargetCount = 0,
                maxLayerDelta = 0.0f,
                meanLayerDelta = 0.0f
            };
        }
    }

    public enum MmdEditableRigSpaceSwitchSourceSpace
    {
        Local = 0,
        World = 1,
        Parent = 2
    }

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
