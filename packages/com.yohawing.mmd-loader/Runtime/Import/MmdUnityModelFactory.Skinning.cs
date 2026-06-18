#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Mmd.Parser;
using Mmd.Rendering;

namespace Mmd.UnityIntegration
{
    public static partial class MmdUnityModelFactory
    {
        private static void ApplySkinning(
            Mesh mesh,
            MmdRenderingDescriptor descriptor,
            IReadOnlyList<MmdBoneDefinition> bones,
            Transform[] boneTransforms,
            Transform root)
        {
            if (descriptor.skinning == null)
            {
                throw new ArgumentException("Rendering descriptor skinning is required.", nameof(descriptor));
            }

            if (descriptor.skinning.Count != descriptor.vertices.Count)
            {
                throw new ArgumentException("Rendering descriptor skinning must contain one record per vertex.", nameof(descriptor));
            }

            Dictionary<int, int> boneSlotsByPmxIndex = BuildBoneSlotMap(bones);
            var boneWeights = new BoneWeight[descriptor.vertices.Count];
            foreach (MmdSkinningDescriptor skinning in descriptor.skinning)
            {
                if (skinning.vertexIndex < 0 || skinning.vertexIndex >= boneWeights.Length)
                {
                    throw new ArgumentException($"Skinning vertex index {skinning.vertexIndex} points outside the mesh.", nameof(descriptor));
                }

                boneWeights[skinning.vertexIndex] = ToUnityBoneWeight(skinning, boneSlotsByPmxIndex);
            }

            mesh.boneWeights = boneWeights;

            var bindposes = new Matrix4x4[boneTransforms.Length];
            for (int i = 0; i < boneTransforms.Length; i++)
            {
                bindposes[i] = boneTransforms[i].worldToLocalMatrix * root.localToWorldMatrix;
            }

            mesh.bindposes = bindposes;
        }

        private static Dictionary<int, int> BuildBoneSlotMap(IReadOnlyList<MmdBoneDefinition> bones)
        {
            var orderedBones = new List<MmdBoneDefinition>(bones);
            orderedBones.Sort((left, right) => left.index.CompareTo(right.index));
            var result = new Dictionary<int, int>(orderedBones.Count);
            for (int i = 0; i < orderedBones.Count; i++)
            {
                result[orderedBones[i].index] = i;
            }

            return result;
        }

        private static BoneWeight ToUnityBoneWeight(
            MmdSkinningDescriptor skinning,
            IReadOnlyDictionary<int, int> boneSlotsByPmxIndex)
        {
            if (skinning.boneIndices == null || skinning.boneWeights == null)
            {
                throw new ArgumentException($"Skinning vertex {skinning.vertexIndex} bone arrays are required.");
            }

            if (skinning.boneIndices.Length != skinning.boneWeights.Length)
            {
                throw new ArgumentException($"Skinning vertex {skinning.vertexIndex} bone indices and weights differ in length.");
            }

            var slots = new int[4];
            var weights = new float[4];
            float totalWeight = 0.0f;
            int limit = Math.Min(4, skinning.boneIndices.Length);
            for (int i = 0; i < limit; i++)
            {
                if (!boneSlotsByPmxIndex.TryGetValue(skinning.boneIndices[i], out int slot))
                {
                    throw new ArgumentException($"Skinning vertex {skinning.vertexIndex} references unknown PMX bone {skinning.boneIndices[i]}.");
                }

                float weight = skinning.boneWeights[i];
                if (!IsFinite(weight) || weight < 0.0f)
                {
                    throw new ArgumentException($"Skinning vertex {skinning.vertexIndex} contains an invalid bone weight.");
                }

                slots[i] = slot;
                weights[i] = weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0.0f)
            {
                weights[0] = 1.0f;
            }
            else
            {
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] /= totalWeight;
                }
            }

            return new BoneWeight
            {
                boneIndex0 = slots[0],
                weight0 = weights[0],
                boneIndex1 = slots[1],
                weight1 = weights[1],
                boneIndex2 = slots[2],
                weight2 = weights[2],
                boneIndex3 = slots[3],
                weight3 = weights[3]
            };
        }

        private static void BakeVertexMorphBlendShapes(Mesh mesh, MmdRenderingDescriptor descriptor)
        {
            BakeVertexMorphBlendShapes(mesh, descriptor, importScale: 1.0f);
        }

        private static void BakeVertexMorphBlendShapes(Mesh mesh, MmdRenderingDescriptor descriptor, float importScale)
        {
            if (descriptor.vertexMorphs == null || descriptor.vertexMorphs.Count == 0)
            {
                return;
            }

            float scale = NormalizeImportScale(importScale);
            int vertexCount = mesh.vertexCount;
            Vector3[] baseVertices = mesh.vertices;
            Bounds expandedBounds = baseVertices.Length > 0
                ? new Bounds(baseVertices[0], Vector3.zero)
                : new Bounds(Vector3.zero, Vector3.zero);
            foreach (Vector3 vertex in baseVertices)
            {
                expandedBounds.Encapsulate(vertex);
            }

            var deltaVertices = new Vector3[vertexCount];
            var zeroNormals = new Vector3[vertexCount];
            var zeroTangents = new Vector3[vertexCount];
            IReadOnlyDictionary<string, int> morphNameCounts = MmdUnityBlendShapeNames.CountMorphNames(descriptor.vertexMorphs);

            foreach (MmdVertexMorphDescriptor morph in descriptor.vertexMorphs)
            {
                if (string.IsNullOrWhiteSpace(morph.morphName))
                {
                    continue;
                }

                Array.Clear(deltaVertices, 0, vertexCount);

                foreach (MmdVertexMorphOffsetDescriptor offset in morph.offsets)
                {
                    int vertexIndex = offset.vertexIndex;
                    if (vertexIndex < 0 || vertexIndex >= vertexCount)
                    {
                        continue;
                    }

                    if (offset.positionDelta != null && offset.positionDelta.Length >= 3)
                    {
                        deltaVertices[vertexIndex] += ToUnityPosition(offset.positionDelta, scale);
                    }
                }

                for (int i = 0; i < vertexCount && i < baseVertices.Length; i++)
                {
                    expandedBounds.Encapsulate(baseVertices[i] + deltaVertices[i]);
                }

                mesh.AddBlendShapeFrame(
                    MmdUnityBlendShapeNames.ResolveVertexMorphBlendShapeName(morph, morphNameCounts),
                    100f,
                    deltaVertices,
                    zeroNormals,
                    zeroTangents);
            }

            mesh.bounds = expandedBounds;
        }
    }
}
