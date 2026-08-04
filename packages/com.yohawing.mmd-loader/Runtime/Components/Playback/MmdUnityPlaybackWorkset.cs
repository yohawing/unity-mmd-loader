#nullable enable

using System;
using System.Collections.Generic;
using Mmd.Parser;
using Mmd.Rendering;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    internal static class MmdUnityPlaybackWorkset
    {
        public static int[] BuildBoneIndices(MmdModelDefinition model, MmdUnityModelInstance instance)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            int boneCount = instance.BoneTransforms.Length;
            if (boneCount == 0)
            {
                return Array.Empty<int>();
            }

            if (instance.SkinnedMeshRenderer == null)
            {
                return CreateAllIndices(boneCount);
            }

            IReadOnlyList<MmdBoneDefinition> orderedBones = CreateOrderedBones(model.bones);
            var slotsByPmxIndex = new Dictionary<int, int>(orderedBones.Count);
            int slotCount = Math.Min(orderedBones.Count, boneCount);
            for (int slot = 0; slot < slotCount; slot++)
            {
                slotsByPmxIndex[orderedBones[slot].index] = slot;
            }

            var required = new bool[boneCount];
            bool hasSkinningInfluence = false;
            foreach (MmdSkinningDescriptor skinning in instance.RenderingDescriptor.skinning)
            {
                if (skinning == null || skinning.boneIndices == null || skinning.boneIndices.Length == 0)
                {
                    continue;
                }

                bool markedVertex = false;
                int limit = Math.Min(skinning.boneIndices.Length, skinning.boneWeights?.Length ?? 0);
                for (int i = 0; i < limit; i++)
                {
                    if (skinning.boneWeights[i] <= 0.0f)
                    {
                        continue;
                    }

                    markedVertex |= TryMarkPmxBone(slotsByPmxIndex, required, skinning.boneIndices[i]);
                }

                if (!markedVertex)
                {
                    markedVertex = TryMarkPmxBone(slotsByPmxIndex, required, skinning.boneIndices[0]);
                }

                hasSkinningInfluence |= markedVertex;
            }

            if (!hasSkinningInfluence)
            {
                return CreateAllIndices(boneCount);
            }

            if (model.physics?.rigidbodies != null)
            {
                foreach (MmdRigidbodyDefinition body in model.physics.rigidbodies)
                {
                    TryMarkPmxBone(slotsByPmxIndex, required, body.boneIndex);
                }
            }

            foreach (MmdBoneDefinition bone in orderedBones)
            {
                if (bone.deformAfterPhysics)
                {
                    TryMarkPmxBone(slotsByPmxIndex, required, bone.index);
                }
            }

            AddBoneAncestors(instance, required);

            var result = new List<int>(boneCount);
            for (int i = 0; i < required.Length; i++)
            {
                if (required[i])
                {
                    result.Add(i);
                }
            }

            return result.ToArray();
        }

        public static int[] BuildMorphIndices(MmdModelDefinition model, MmdRenderingDescriptor descriptor)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (model.morphs == null || model.morphs.Count == 0) return Array.Empty<int>();

            var slotsByMorphIndex = new Dictionary<int, int>(model.morphs.Count);
            for (int slot = 0; slot < model.morphs.Count; slot++)
            {
                slotsByMorphIndex[model.morphs[slot].index] = slot;
            }

            var required = new bool[model.morphs.Count];
            foreach (MmdVertexMorphDescriptor morph in descriptor.vertexMorphs)
            {
                TryMarkMorph(slotsByMorphIndex, required, morph?.morphIndex ?? -1);
            }

            foreach (MmdMorphDescriptorBuilder.MmdUvMorphDescriptor morph in descriptor.uvMorphs)
            {
                TryMarkMorph(slotsByMorphIndex, required, morph?.morphIndex ?? -1);
            }

            foreach (MmdMorphDescriptorBuilder.MmdMaterialMorphDescriptor morph in descriptor.materialMorphs)
            {
                TryMarkMorph(slotsByMorphIndex, required, morph?.morphIndex ?? -1);
            }

            foreach (MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor morph in descriptor.flipMorphs)
            {
                if (morph == null)
                {
                    continue;
                }

                TryMarkMorph(slotsByMorphIndex, required, morph.morphIndex);
                if (morph.offsets == null)
                {
                    continue;
                }

                foreach (MmdMorphDescriptorBuilder.MmdFlipMorphOffsetDescriptor offset in morph.offsets)
                {
                    if (offset != null)
                    {
                        TryMarkMorph(slotsByMorphIndex, required, offset.targetMorphIndex);
                    }
                }
            }

            var result = new List<int>(model.morphs.Count);
            for (int i = 0; i < required.Length; i++)
            {
                if (required[i])
                {
                    result.Add(i);
                }
            }

            return result.ToArray();
        }

        public static int[] BuildAfterPhysicsBoneIndices(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (model.bones == null || model.bones.Count == 0)
            {
                return Array.Empty<int>();
            }

            var result = new List<int>();
            foreach (MmdBoneDefinition bone in model.bones)
            {
                if (bone.deformAfterPhysics)
                {
                    result.Add(bone.index);
                }
            }

            result.Sort();
            return result.ToArray();
        }

        private static bool TryMarkPmxBone(
            IReadOnlyDictionary<int, int> slotsByPmxIndex,
            bool[] required,
            int pmxIndex)
        {
            if (!slotsByPmxIndex.TryGetValue(pmxIndex, out int slot) ||
                slot < 0 || slot >= required.Length)
            {
                return false;
            }

            required[slot] = true;
            return true;
        }

        private static void TryMarkMorph(
            IReadOnlyDictionary<int, int> slotsByMorphIndex,
            bool[] required,
            int morphIndex)
        {
            if (slotsByMorphIndex.TryGetValue(morphIndex, out int slot) &&
                slot >= 0 && slot < required.Length)
            {
                required[slot] = true;
            }
        }

        private static void AddBoneAncestors(MmdUnityModelInstance instance, bool[] required)
        {
            var slotsByTransform = new Dictionary<Transform, int>(instance.BoneTransforms.Length);
            for (int i = 0; i < instance.BoneTransforms.Length; i++)
            {
                Transform bone = instance.BoneTransforms[i];
                if (bone != null)
                {
                    slotsByTransform[bone] = i;
                }
            }

            for (int i = 0; i < required.Length; i++)
            {
                if (!required[i])
                {
                    continue;
                }

                Transform? parent = instance.BoneTransforms[i]?.parent;
                while (parent != null)
                {
                    if (slotsByTransform.TryGetValue(parent, out int parentSlot))
                    {
                        required[parentSlot] = true;
                    }

                    parent = parent.parent;
                }
            }
        }

        private static IReadOnlyList<MmdBoneDefinition> CreateOrderedBones(IReadOnlyList<MmdBoneDefinition> bones)
        {
            var orderedBones = new List<MmdBoneDefinition>(bones);
            orderedBones.Sort((left, right) => left.index.CompareTo(right.index));
            return orderedBones;
        }

        private static int[] CreateAllIndices(int count)
        {
            var result = new int[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = i;
            }

            return result;
        }
    }
}
