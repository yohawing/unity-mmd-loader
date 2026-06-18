#nullable enable

using System;
using System.Collections.Generic;
using Mmd.Parser;

namespace Mmd.Physics
{
    public static class MmdPhysicsDescriptorValidator
    {
        public static IReadOnlyList<string> Validate(MmdModelDefinition model)
        {
            var errors = new List<string>();
            if (model == null)
            {
                errors.Add("model is null");
                return errors;
            }

            MmdPhysicsDefinition? physics = model.physics;
            if (physics == null)
            {
                errors.Add("model physics must not be null");
                return errors;
            }

            IReadOnlyList<MmdRigidbodyDefinition> rigidbodies = physics.rigidbodies != null ? physics.rigidbodies : Array.Empty<MmdRigidbodyDefinition>();
            IReadOnlyList<MmdJointDefinition> joints = physics.joints != null ? physics.joints : Array.Empty<MmdJointDefinition>();
            if (physics.rigidbodies == null)
            {
                errors.Add("physics rigidbodies must not be null");
            }

            if (physics.joints == null)
            {
                errors.Add("physics joints must not be null");
            }

            IReadOnlyList<MmdBoneDefinition> bones = model.bones != null ? model.bones : Array.Empty<MmdBoneDefinition>();
            var boneIndices = new HashSet<int>(bones.Count);
            foreach (MmdBoneDefinition bone in bones)
            {
                boneIndices.Add(bone.index);
            }

            var rigidbodyIndices = new HashSet<int>(rigidbodies.Count);
            for (int i = 0; i < rigidbodies.Count; i++)
            {
                MmdRigidbodyDefinition body = rigidbodies[i];
                if (!rigidbodyIndices.Add(body.index))
                {
                    errors.Add($"duplicate rigidbody index: {body.index}");
                }

                if (string.IsNullOrWhiteSpace(body.name))
                {
                    errors.Add($"rigidbody name is required: {body.index}");
                }

                if (body.boneIndex != -1 && !boneIndices.Contains(body.boneIndex))
                {
                    errors.Add($"rigidbody boneIndex does not reference an existing bone: {body.index} -> {body.boneIndex}");
                }

                ValidateRigidbodyShapeType(errors, body.shapeType, body.index);
                ValidateVector(errors, body.size, 3, $"rigidbody size: {body.index}");
                ValidateVector(errors, body.position, 3, $"rigidbody position: {body.index}");
                ValidateVector(errors, body.rotation, 3, $"rigidbody rotation: {body.index}");
                ValidateFiniteNonNegative(errors, body.mass, $"rigidbody mass must be non-negative finite: {body.index}");
                ValidateFiniteNonNegative(errors, body.linearDamping, $"rigidbody linearDamping must be non-negative finite: {body.index}");
                ValidateFiniteNonNegative(errors, body.angularDamping, $"rigidbody angularDamping must be non-negative finite: {body.index}");
                ValidateFiniteNonNegative(errors, body.friction, $"rigidbody friction must be non-negative finite: {body.index}");
                ValidateFiniteNonNegative(errors, body.restitution, $"rigidbody restitution must be non-negative finite: {body.index}");
                if (body.group < 0 || body.group > 15)
                {
                    errors.Add($"rigidbody group must be between 0 and 15: {body.index}");
                }

                if (body.mask < 0 || body.mask > 65535)
                {
                    errors.Add($"rigidbody mask must be between 0 and 65535: {body.index}");
                }

                ValidateNonBlank(errors, body.physicsKind, $"rigidbody physicsKind is required: {body.index}");
            }

            var jointIndices = new HashSet<int>(joints.Count);
            for (int i = 0; i < joints.Count; i++)
            {
                MmdJointDefinition joint = joints[i];
                if (!jointIndices.Add(joint.index))
                {
                    errors.Add($"duplicate joint index: {joint.index}");
                }

                if (string.IsNullOrWhiteSpace(joint.name))
                {
                    errors.Add($"joint name is required: {joint.index}");
                }

                if (joint.rigidbodyAIndex == -1 && joint.rigidbodyBIndex == -1)
                {
                    errors.Add($"joint has both rigidbody endpoints set to -1 (unsupported world-anchored joint): {joint.index}");
                }

                if (joint.rigidbodyAIndex != -1 && !rigidbodyIndices.Contains(joint.rigidbodyAIndex))
                {
                    errors.Add($"joint rigidbodyAIndex does not reference an existing rigidbody: {joint.index} -> {joint.rigidbodyAIndex}");
                }

                if (joint.rigidbodyBIndex != -1 && !rigidbodyIndices.Contains(joint.rigidbodyBIndex))
                {
                    errors.Add($"joint rigidbodyBIndex does not reference an existing rigidbody: {joint.index} -> {joint.rigidbodyBIndex}");
                }

                ValidateVector(errors, joint.position, 3, $"joint position: {joint.index}");
                ValidateVector(errors, joint.rotation, 3, $"joint rotation: {joint.index}");
                ValidateVector(errors, joint.linearLowerLimit, 3, $"joint linearLowerLimit: {joint.index}");
                ValidateVector(errors, joint.linearUpperLimit, 3, $"joint linearUpperLimit: {joint.index}");
                ValidateVector(errors, joint.angularLowerLimit, 3, $"joint angularLowerLimit: {joint.index}");
                ValidateVector(errors, joint.angularUpperLimit, 3, $"joint angularUpperLimit: {joint.index}");
                ValidateVector(errors, joint.linearSpring, 3, $"joint linearSpring: {joint.index}");
                ValidateVector(errors, joint.angularSpring, 3, $"joint angularSpring: {joint.index}");
            }

            return errors;
        }

        public static void ThrowIfInvalid(MmdModelDefinition model)
        {
            IReadOnlyList<string> errors = Validate(model);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("; ", errors));
            }
        }

        private static void ValidateNonBlank(List<string> errors, string value, string message)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add(message);
            }
        }

        private static void ValidateRigidbodyShapeType(List<string> errors, string shapeType, int bodyIndex)
        {
            if (string.IsNullOrWhiteSpace(shapeType))
            {
                errors.Add($"rigidbody shapeType is required: {bodyIndex}");
                return;
            }

            if (!string.Equals(shapeType, "sphere", StringComparison.Ordinal) &&
                !string.Equals(shapeType, "box", StringComparison.Ordinal) &&
                !string.Equals(shapeType, "capsule", StringComparison.Ordinal))
            {
                errors.Add($"rigidbody shapeType is unsupported: {bodyIndex} -> {shapeType}");
            }
        }

        private static void ValidateVector(List<string> errors, float[] values, int length, string label)
        {
            if (values == null || values.Length != length)
            {
                errors.Add($"{label} must have {length} values");
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                {
                    errors.Add($"{label} must contain only finite values");
                    return;
                }
            }
        }

        private static void ValidateFiniteNonNegative(List<string> errors, float value, string message)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0.0f)
            {
                errors.Add(message);
            }
        }
    }
}
