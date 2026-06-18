using System;
using System.Collections.Generic;

namespace Mmd.Parser
{
    public static class MmdMotionValidator
    {
        public static IReadOnlyList<string> ValidateStructuralMotion(MmdMotionDefinition motion)
        {
            var errors = new List<string>();
            if (motion == null)
            {
                errors.Add("motion is null");
                return errors;
            }

            if (motion.maxFrame < 0)
            {
                errors.Add("motion maxFrame must not be negative");
            }

            IReadOnlyList<MmdBoneKeyframeDefinition> boneKeyframes = motion.boneKeyframes != null ? motion.boneKeyframes : Array.Empty<MmdBoneKeyframeDefinition>();
            IReadOnlyList<MmdMorphKeyframeDefinition> morphKeyframes = motion.morphKeyframes != null ? motion.morphKeyframes : Array.Empty<MmdMorphKeyframeDefinition>();
            IReadOnlyList<MmdModelKeyframeDefinition> modelKeyframes = motion.modelKeyframes != null ? motion.modelKeyframes : Array.Empty<MmdModelKeyframeDefinition>();
            if (motion.boneKeyframes == null)
            {
                errors.Add("motion boneKeyframes must not be null");
            }

            if (motion.morphKeyframes == null)
            {
                errors.Add("motion morphKeyframes must not be null");
            }

            if (motion.modelKeyframes == null)
            {
                errors.Add("motion modelKeyframes must not be null");
            }

            for (int i = 0; i < boneKeyframes.Count; i++)
            {
                MmdBoneKeyframeDefinition keyframe = boneKeyframes[i];
                if (string.IsNullOrWhiteSpace(keyframe.boneName))
                {
                    errors.Add($"bone keyframe name is required: {i}");
                }

                if (keyframe.frame < 0)
                {
                    errors.Add($"bone keyframe frame must not be negative: {i}");
                }

                if (keyframe.translation == null || keyframe.translation.Length != 3)
                {
                    errors.Add($"bone keyframe translation must have 3 values: {i}");
                }
                else if (HasNonFinite(keyframe.translation))
                {
                    errors.Add($"bone keyframe translation must contain only finite values: {i}");
                }

                if (keyframe.rotation == null || keyframe.rotation.Length != 4)
                {
                    errors.Add($"bone keyframe rotation must have 4 values: {i}");
                }
                else if (HasNonFinite(keyframe.rotation))
                {
                    errors.Add($"bone keyframe rotation must contain only finite values: {i}");
                }

                if (keyframe.interpolation == null)
                {
                    errors.Add($"bone keyframe interpolation is required: {i}");
                }
                else
                {
                    ValidateInterpolationChannel(errors, keyframe.interpolation.translationX, "translationX", i);
                    ValidateInterpolationChannel(errors, keyframe.interpolation.translationY, "translationY", i);
                    ValidateInterpolationChannel(errors, keyframe.interpolation.translationZ, "translationZ", i);
                    ValidateInterpolationChannel(errors, keyframe.interpolation.rotation, "rotation", i);
                }
            }

            for (int i = 0; i < morphKeyframes.Count; i++)
            {
                MmdMorphKeyframeDefinition keyframe = morphKeyframes[i];
                if (string.IsNullOrWhiteSpace(keyframe.morphName))
                {
                    errors.Add($"morph keyframe name is required: {i}");
                }

                if (keyframe.frame < 0)
                {
                    errors.Add($"morph keyframe frame must not be negative: {i}");
                }

                if (float.IsNaN(keyframe.weight) || float.IsInfinity(keyframe.weight))
                {
                    errors.Add($"morph keyframe weight must be finite: {i}");
                }
            }

            for (int i = 0; i < modelKeyframes.Count; i++)
            {
                MmdModelKeyframeDefinition keyframe = modelKeyframes[i];
                if (keyframe.frame < 0)
                {
                    errors.Add($"model keyframe frame must not be negative: {i}");
                }

                IReadOnlyList<MmdModelConstraintStateDefinition> constraintStates = keyframe.constraintStates != null
                    ? keyframe.constraintStates
                    : Array.Empty<MmdModelConstraintStateDefinition>();
                if (keyframe.constraintStates == null)
                {
                    errors.Add($"model keyframe constraintStates must not be null: {i}");
                }

                for (int stateIndex = 0; stateIndex < constraintStates.Count; stateIndex++)
                {
                    if (string.IsNullOrWhiteSpace(constraintStates[stateIndex].boneName))
                    {
                        errors.Add($"model keyframe constraint state boneName is required: {i}[{stateIndex}]");
                    }
                }
            }

            return errors;
        }

        public static void ThrowIfInvalid(MmdMotionDefinition motion)
        {
            IReadOnlyList<string> errors = ValidateStructuralMotion(motion);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("; ", errors));
            }
        }

        private static void ValidateInterpolationChannel(List<string> errors, byte[] channel, string channelName, int keyframeIndex)
        {
            if (channel == null || channel.Length != 4)
            {
                errors.Add($"bone keyframe interpolation {channelName} must have 4 values: {keyframeIndex}");
            }
        }

        private static bool HasNonFinite(float[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
