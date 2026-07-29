#nullable enable

using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using Mmd.Motion;
using Mmd.Parser;

namespace Mmd.Pose
{
    internal sealed class MmdTopologyPlan
    {
        private MmdTopologyPlan(
            MmdModelDefinition model,
            MmdBoneDefinition?[] indexedBones,
            float[][] bindOffsets,
            ulong sourceFingerprint)
        {
            Model = model;
            IndexedBones = indexedBones;
            BindOffsets = bindOffsets;
            SourceFingerprint = sourceFingerprint;
        }

        internal MmdModelDefinition Model { get; }
        internal MmdBoneDefinition?[] IndexedBones { get; }
        internal float[][] BindOffsets { get; }
        private ulong SourceFingerprint { get; }

        internal static MmdTopologyPlan CreateFromValidatedModel(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            int maxIndex = -1;
            for (int i = 0; i < model.bones.Count; i++)
            {
                int index = model.bones[i].index;
                if (index < 0)
                {
                    throw new InvalidOperationException($"Bone index must be non-negative: {index}.");
                }

                maxIndex = Math.Max(maxIndex, index);
            }

            if (maxIndex == int.MaxValue)
            {
                throw new InvalidOperationException("Bone index is too large to compile topology.");
            }

            long maximumSlotCount = Math.Max(4096L, (long)model.bones.Count * 16L);
            if ((long)maxIndex + 1L > maximumSlotCount)
            {
                throw new InvalidOperationException(
                    $"Bone index range is too sparse to compile safely: count={model.bones.Count}, maxIndex={maxIndex}.");
            }

            var indexedBones = new MmdBoneDefinition?[maxIndex + 1];
            for (int i = 0; i < model.bones.Count; i++)
            {
                MmdBoneDefinition bone = model.bones[i];
                indexedBones[bone.index] = bone;
            }

            var bindOffsets = new float[indexedBones.Length][];
            for (int index = 0; index < indexedBones.Length; index++)
            {
                bindOffsets[index] = new[] { 0.0f, 0.0f, 0.0f };
            }

            for (int index = 0; index < indexedBones.Length; index++)
            {
                MmdBoneDefinition? bone = indexedBones[index];
                if (bone == null)
                {
                    continue;
                }

                int parentIndex = bone.parentIndex;
                float[] origin = OriginOrZero(bone);
                float[] parentOrigin = parentIndex >= 0 ? OriginOrZero(indexedBones[parentIndex]) : Zero();
                bindOffsets[index] = new[]
                {
                    origin[0] - parentOrigin[0],
                    origin[1] - parentOrigin[1],
                    origin[2] - parentOrigin[2]
                };
            }

            ValidateAcyclic(indexedBones);
            return new MmdTopologyPlan(model, indexedBones, bindOffsets, ComputeSourceFingerprint(model));
        }

        internal void EnsureModel(MmdModelDefinition model)
        {
            if (!ReferenceEquals(Model, model))
            {
                throw new ArgumentException("Topology plan belongs to a different model.", nameof(model));
            }


            if (ComputeSourceFingerprint(model) != SourceFingerprint)
            {
                throw new InvalidOperationException("Model topology changed after the plan was compiled; recreate the runtime session.");
            }
        }

        private static ulong ComputeSourceFingerprint(MmdModelDefinition model)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            Add(ref hash, model.bones.Count, prime);
            for (int i = 0; i < model.bones.Count; i++)
            {
                MmdBoneDefinition bone = model.bones[i];
                Add(ref hash, RuntimeHelpers.GetHashCode(bone), prime);
                Add(ref hash, bone.index, prime);
                Add(ref hash, bone.parentIndex, prime);
                Add(ref hash, bone.origin?.Length ?? -1, prime);
                if (bone.origin != null)
                {
                    for (int component = 0; component < bone.origin.Length; component++)
                    {
                        Add(ref hash, BitConverter.SingleToInt32Bits(bone.origin[component]), prime);
                    }
                }
            }

            return hash;
        }

        private static void Add(ref ulong hash, int value, ulong prime)
        {
            hash ^= unchecked((uint)value);
            hash *= prime;
        }

        private static void ValidateAcyclic(IReadOnlyList<MmdBoneDefinition?> indexedBones)
        {
            var states = new byte[indexedBones.Count];
            for (int index = 0; index < indexedBones.Count; index++)
            {
                Visit(index, indexedBones, states);
            }
        }

        private static void Visit(int index, IReadOnlyList<MmdBoneDefinition?> indexedBones, byte[] states)
        {
            if (indexedBones[index] == null || states[index] == 2)
            {
                return;
            }

            if (states[index] == 1)
            {
                throw new InvalidOperationException($"Bone parent cycle detected at index {index}.");
            }

            states[index] = 1;
            int parentIndex = indexedBones[index]!.parentIndex;
            if (parentIndex >= 0)
            {
                Visit(parentIndex, indexedBones, states);
            }

            states[index] = 2;
        }

        private static float[] OriginOrZero(MmdBoneDefinition? bone)
        {
            return bone?.origin != null && bone.origin.Length == 3 ? bone.origin : Zero();
        }

        private static float[] Zero() => new[] { 0.0f, 0.0f, 0.0f };
    }

    public static class MmdPoseEvaluator
    {
        public static Dictionary<int, float[]> EvaluateWorldMatrices(MmdModelDefinition? model, MmdSampledMotion? sampledMotion)
        {
            if (model == null)
            {
                return new Dictionary<int, float[]>();
            }

            IReadOnlyList<MmdBoneDefinition> bones = BonesOrEmpty(model);
            var worldMatrices = new Dictionary<int, float[]>(bones.Count);
            var visiting = new HashSet<int>(bones.Count);
            for (int i = 0; i < bones.Count; i++)
            {
                visiting.Clear();
                EvaluateBone(model, sampledMotion, bones[i].index, worldMatrices, visiting);
            }

            return worldMatrices;
        }

        internal static Dictionary<int, float[]> EvaluateWorldMatrices(
            MmdTopologyPlan topology,
            MmdSampledMotion? sampledMotion)
        {
            if (topology == null)
            {
                throw new ArgumentNullException(nameof(topology));
            }

            var worldMatrices = new Dictionary<int, float[]>(topology.Model.bones.Count);
            var states = new byte[topology.IndexedBones.Length];
            for (int i = 0; i < topology.Model.bones.Count; i++)
            {
                EvaluateBone(topology, sampledMotion, topology.Model.bones[i].index, worldMatrices, states);
            }

            return worldMatrices;
        }

        private static float[] EvaluateBone(
            MmdTopologyPlan topology,
            MmdSampledMotion? sampledMotion,
            int boneIndex,
            Dictionary<int, float[]> worldMatrices,
            byte[] states)
        {
            MmdBoneDefinition? bone = topology.IndexedBones[boneIndex];
            if (bone == null)
            {
                throw new InvalidOperationException($"Topology plan is missing bone index {boneIndex}.");
            }

            if (worldMatrices.TryGetValue(boneIndex, out float[]? existing))
            {
                return existing;
            }

            if (states[boneIndex] == 1)
            {
                throw new InvalidOperationException($"Bone parent cycle detected at index {boneIndex}.");
            }

            states[boneIndex] = 1;
            float[] local = BuildLocalMatrix(bone, sampledMotion, topology.BindOffsets[boneIndex]);
            float[] world = bone.parentIndex >= 0
                ? MmdPoseMath.Multiply(EvaluateBone(topology, sampledMotion, bone.parentIndex, worldMatrices, states), local)
                : local;
            states[boneIndex] = 2;
            worldMatrices[boneIndex] = world;
            return world;
        }

        private static float[] EvaluateBone(
            MmdModelDefinition model,
            MmdSampledMotion? sampledMotion,
            int boneIndex,
            Dictionary<int, float[]> worldMatrices,
            HashSet<int> visiting)
        {
            if (worldMatrices.TryGetValue(boneIndex, out float[]? existing))
            {
                return existing;
            }

            if (!visiting.Add(boneIndex))
            {
                throw new InvalidOperationException($"Bone parent cycle detected at index {boneIndex}.");
            }

            MmdBoneDefinition? bone = null;
            IReadOnlyList<MmdBoneDefinition> bones = BonesOrEmpty(model);
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i].index == boneIndex)
                {
                    bone = bones[i];
                    break;
                }
            }
            if (bone == null)
            {
                float[] identity = MmdPoseMath.LocalMatrix(
                    new[] { 0.0f, 0.0f, 0.0f },
                    new[] { 0.0f, 0.0f, 0.0f, 1.0f },
                    new[] { 1.0f, 1.0f, 1.0f });
                worldMatrices[boneIndex] = identity;
                return identity;
            }

            float[] local = BuildLocalMatrix(bone, sampledMotion, GetBindOffset(model, bone));
            float[] world = bone.parentIndex >= 0
                ? MmdPoseMath.Multiply(EvaluateBone(model, sampledMotion, bone.parentIndex, worldMatrices, visiting), local)
                : local;

            worldMatrices[boneIndex] = world;
            visiting.Remove(boneIndex);
            return world;
        }

        public static float[] GetLocalTranslation(MmdModelDefinition model, MmdBoneDefinition bone, MmdBonePoseSample sample)
        {
            float[] bindOffset = GetBindOffset(model, bone);

            return new[]
            {
                bindOffset[0] + sample.Translation[0],
                bindOffset[1] + sample.Translation[1],
                bindOffset[2] + sample.Translation[2]
            };
        }

        private static float[] BuildLocalMatrix(
            MmdBoneDefinition bone,
            MmdSampledMotion? sampledMotion,
            float[] bindOffset)
        {
            MmdBonePoseSample sample = sampledMotion != null && sampledMotion.Bones.TryGetValue(bone.name, out MmdBonePoseSample found)
                ? found
                : MmdBonePoseSample.Identity;
            return MmdPoseMath.LocalMatrix(
                new[]
                {
                    bindOffset[0] + sample.Translation[0],
                    bindOffset[1] + sample.Translation[1],
                    bindOffset[2] + sample.Translation[2]
                },
                sample.Rotation,
                new[] { 1.0f, 1.0f, 1.0f });
        }

        private static float[] GetBindOffset(MmdModelDefinition model, MmdBoneDefinition bone)
        {
            return bone.parentIndex >= 0
                ? Subtract(OriginOrZero(bone), OriginOrZero(FindBone(model, bone.parentIndex)))
                : OriginOrZero(bone);
        }

        private static MmdBoneDefinition? FindBone(MmdModelDefinition model, int boneIndex)
        {
            IReadOnlyList<MmdBoneDefinition> bones = BonesOrEmpty(model);
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i].index == boneIndex)
                {
                    return bones[i];
                }
            }

            return null;
        }

        private static IReadOnlyList<MmdBoneDefinition> BonesOrEmpty(MmdModelDefinition model)
        {
            return model.bones != null ? model.bones : Array.Empty<MmdBoneDefinition>();
        }

        private static float[] OriginOrZero(MmdBoneDefinition? bone)
        {
            if (bone?.origin == null || bone.origin.Length != 3)
            {
                return new[] { 0.0f, 0.0f, 0.0f };
            }

            return bone.origin;
        }

        private static float[] Subtract(float[] left, float[] right)
        {
            return new[]
            {
                left[0] - right[0],
                left[1] - right[1],
                left[2] - right[2]
            };
        }
    }
}
