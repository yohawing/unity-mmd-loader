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

        private static Bounds BakeVertexMorphBlendShapes(
            Mesh mesh,
            MmdRenderingDescriptor descriptor,
            float importScale,
            IReadOnlyList<MmdBoneDefinition>? bones)
        {
            float scale = NormalizeImportScale(importScale);
            int vertexCount = mesh.vertexCount;
            Vector3[] baseVertices = mesh.vertices;

            if (descriptor.vertexMorphs != null && descriptor.vertexMorphs.Count > 0)
            {
                var deltaVertices = new Vector3[vertexCount];
                var zeroNormals = new Vector3[vertexCount];
                var zeroTangents = new Vector3[vertexCount];
                IReadOnlyDictionary<string, int> morphNameCounts = MmdUnityBlendShapeNames.CountMorphNames(descriptor.vertexMorphs);

                foreach (MmdVertexMorphDescriptor morph in descriptor.vertexMorphs)
                {
                    if (string.IsNullOrWhiteSpace(morph.morphName))
                        continue;

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

                    mesh.AddBlendShapeFrame(
                        MmdUnityBlendShapeNames.ResolveVertexMorphBlendShapeName(morph, morphNameCounts),
                        100f,
                        deltaVertices,
                        zeroNormals,
                        zeroTangents);
                }
            }

            return BuildConservativeSkinnedBounds(mesh, descriptor, bones, scale);
        }

        private static Bounds BuildConservativeSkinnedBounds(
            Mesh mesh,
            MmdRenderingDescriptor descriptor,
            IReadOnlyList<MmdBoneDefinition>? bones,
            float importScale)
        {
            float scale = NormalizeImportScale(importScale);
            Vector3[] baseVertices = mesh.vertices;
            IReadOnlyList<MmdBoneDefinition> orderedBones = bones == null || bones.Count == 0
                ? Array.Empty<MmdBoneDefinition>()
                : CreateOrderedBones(bones);

            Vector3 center = Vector3.zero;
            if (orderedBones.Count > 0 && TryToUnityPosition(orderedBones[0].origin, scale, out Vector3 rootOrigin))
            {
                center = rootOrigin;
            }

            var bonesByIndex = new Dictionary<int, MmdBoneDefinition>(orderedBones.Count);
            var boneOrigins = new Dictionary<int, Vector3>(orderedBones.Count);
            foreach (MmdBoneDefinition bone in orderedBones)
            {
                bonesByIndex[bone.index] = bone;
                if (TryToUnityPosition(bone.origin, scale, out Vector3 origin))
                {
                    boneOrigins[bone.index] = origin;
                }
            }

            var chainLengths = new Dictionary<int, float>(orderedBones.Count);
            var skinningByVertex = new Dictionary<int, MmdSkinningDescriptor>();
            if (descriptor.skinning != null)
            {
                foreach (MmdSkinningDescriptor skinning in descriptor.skinning)
                {
                    if (skinning != null)
                    {
                        skinningByVertex[skinning.vertexIndex] = skinning;
                    }
                }
            }

            Dictionary<string, double> morphBudgets = BuildVertexMorphBudgets(descriptor);
            Dictionary<int, double> morphRadiusByVertex = BuildMorphRadiusByVertex(descriptor, morphBudgets, scale);
            double maxRadius = 0.0;
            foreach (Vector3 vertex in baseVertices)
            {
                maxRadius = Math.Max(maxRadius, Vector3.Distance(vertex, center));
            }

            int vertexLimit = Math.Min(baseVertices.Length, descriptor.vertices?.Count ?? 0);
            for (int vertexSlot = 0; vertexSlot < vertexLimit; vertexSlot++)
            {
                Vector3 baseVertex = baseVertices[vertexSlot];
                int vertexIndex = descriptor.vertices[vertexSlot].vertexIndex;
                double skeletonRadius = Vector3.Distance(baseVertex, center);

                if (skinningByVertex.TryGetValue(vertexIndex, out MmdSkinningDescriptor? skinning) &&
                    skinning.boneIndices != null && skinning.boneWeights != null)
                {
                    int influenceLimit = Math.Min(skinning.boneIndices.Length, skinning.boneWeights.Length);
                    for (int influence = 0; influence < influenceLimit; influence++)
                    {
                        float weight = skinning.boneWeights[influence];
                        if (!IsFinite(weight) || weight <= 0.0f)
                        {
                            continue;
                        }

                        int boneIndex = skinning.boneIndices[influence];
                        if (!boneOrigins.TryGetValue(boneIndex, out Vector3 boneOrigin))
                        {
                            continue;
                        }

                        float chainLength = ComputeBindChainLength(
                            boneIndex,
                            bonesByIndex,
                            boneOrigins,
                            center,
                            chainLengths);
                        double candidate = chainLength + Vector3.Distance(baseVertex, boneOrigin);
                        if (candidate > skeletonRadius)
                        {
                            skeletonRadius = candidate;
                        }
                    }
                }

                double morphRadius = morphRadiusByVertex.TryGetValue(vertexIndex, out double precomputedMorphRadius)
                    ? precomputedMorphRadius
                    : 0.0;
                double candidateRadius = skeletonRadius + morphRadius;
                if (candidateRadius > maxRadius)
                {
                    maxRadius = candidateRadius;
                }
            }

            if (double.IsNaN(maxRadius) || double.IsInfinity(maxRadius))
            {
                maxRadius = float.MaxValue;
            }

            float radius = (float)Math.Min(maxRadius, float.MaxValue * 0.5);
            return new Bounds(center, new Vector3(radius * 2.0f, radius * 2.0f, radius * 2.0f));
        }

        private static float ComputeBindChainLength(
            int boneIndex,
            IReadOnlyDictionary<int, MmdBoneDefinition> bonesByIndex,
            IReadOnlyDictionary<int, Vector3> boneOrigins,
            Vector3 center,
            IDictionary<int, float> cache)
        {
            if (cache.TryGetValue(boneIndex, out float cached))
            {
                return cached;
            }

            float length = 0.0f;
            int currentIndex = boneIndex;
            var visited = new HashSet<int>();
            while (bonesByIndex.TryGetValue(currentIndex, out MmdBoneDefinition? currentBone) &&
                   boneOrigins.TryGetValue(currentIndex, out Vector3 currentOrigin))
            {
                if (!visited.Add(currentIndex))
                {
                    length += Vector3.Distance(center, currentOrigin);
                    break;
                }

                if (currentBone.parentIndex >= 0 &&
                    boneOrigins.TryGetValue(currentBone.parentIndex, out Vector3 parentOrigin))
                {
                    length += Vector3.Distance(currentOrigin, parentOrigin);
                    currentIndex = currentBone.parentIndex;
                    continue;
                }

                length += Vector3.Distance(center, currentOrigin);
                break;
            }

            cache[boneIndex] = length;
            return length;
        }

        private static Dictionary<string, double> BuildVertexMorphBudgets(MmdRenderingDescriptor descriptor)
        {
            var vertexMorphNames = new HashSet<string>(StringComparer.Ordinal);
            var rawMorphNames = new HashSet<string>(StringComparer.Ordinal);
            var compositeEdges = new Dictionary<string, List<KeyValuePair<string, double>>>(StringComparer.Ordinal);

            if (descriptor.vertexMorphs != null)
            {
                foreach (MmdVertexMorphDescriptor morph in descriptor.vertexMorphs)
                {
                    if (morph == null || string.IsNullOrWhiteSpace(morph.morphName))
                        continue;

                    vertexMorphNames.Add(morph.morphName);
                    rawMorphNames.Add(morph.morphName);
                }
            }

            if (descriptor.groupMorphs != null)
            {
                foreach (MmdGroupMorphDescriptor morph in descriptor.groupMorphs)
                {
                    if (morph == null || string.IsNullOrWhiteSpace(morph.morphName))
                    {
                        continue;
                    }

                    rawMorphNames.Add(morph.morphName);
                    compositeEdges[morph.morphName] = new List<KeyValuePair<string, double>>();
                    if (morph.offsets == null)
                        continue;

                    foreach (MmdGroupMorphOffsetDescriptor offset in morph.offsets)
                    {
                        AddCompositeMorphEdge(
                            compositeEdges,
                            morph.morphName,
                            ResolveMorphTargetName(offset.targetMorphName, offset.targetMorphIndex),
                            offset.weight,
                            IsFinite(offset.weight));
                    }
                }
            }

            if (descriptor.flipMorphs != null)
            {
                foreach (MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor morph in descriptor.flipMorphs)
                {
                    if (morph == null || string.IsNullOrWhiteSpace(morph.morphName))
                        continue;

                    rawMorphNames.Add(morph.morphName);
                    compositeEdges[morph.morphName] = new List<KeyValuePair<string, double>>();
                    if (morph.offsets == null)
                    {
                        continue;
                    }

                    foreach (MmdMorphDescriptorBuilder.MmdFlipMorphOffsetDescriptor offset in morph.offsets)
                    {
                        AddCompositeMorphEdge(
                            compositeEdges,
                            morph.morphName,
                            ResolveMorphTargetName(offset.targetMorphName, offset.targetMorphIndex),
                            offset.weight,
                            offset.finiteWeight && IsFinite(offset.weight));
                    }
                }
            }

            var budgets = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (string rawMorphName in rawMorphNames)
            {
                if (compositeEdges.ContainsKey(rawMorphName))
                {
                    AccumulateCompositeMorphBudget(
                        rawMorphName,
                        1.0,
                        vertexMorphNames,
                        compositeEdges,
                        budgets,
                        new HashSet<string>(StringComparer.Ordinal));
                }
                else if (vertexMorphNames.Contains(rawMorphName))
                {
                    budgets[rawMorphName] = budgets.TryGetValue(rawMorphName, out double current) ? current + 1.0 : 1.0;
                }
            }

            return budgets;
        }

        private static void AddCompositeMorphEdge(
            IDictionary<string, List<KeyValuePair<string, double>>> edges,
            string sourceName, string targetName, float weight, bool finiteWeight)
        {
            if (finiteWeight && weight != 0.0f)
            {
                edges[sourceName].Add(new KeyValuePair<string, double>(targetName, Math.Abs((double)weight)));
            }
        }

        private static void AccumulateCompositeMorphBudget(
            string morphName, double coefficient, ISet<string> vertexMorphNames,
            IReadOnlyDictionary<string, List<KeyValuePair<string, double>>> edges,
            IDictionary<string, double> budgets, ISet<string> path)
        {
            if (!IsFiniteDouble(coefficient) || !path.Add(morphName) || !edges.TryGetValue(morphName, out List<KeyValuePair<string, double>>? targets))
            {
                return;
            }

            foreach (KeyValuePair<string, double> edge in targets)
            {
                double contribution = coefficient * edge.Value;
                if (!IsFiniteDouble(contribution))
                {
                    continue;
                }

                if (vertexMorphNames.Contains(edge.Key))
                {
                    budgets[edge.Key] = budgets.TryGetValue(edge.Key, out double current)
                        ? current + contribution
                        : contribution;
                }

                if (edges.ContainsKey(edge.Key) && !path.Contains(edge.Key))
                {
                    AccumulateCompositeMorphBudget(edge.Key, contribution, vertexMorphNames, edges, budgets, path);
                }
            }

            path.Remove(morphName);
        }

        private static string ResolveMorphTargetName(string? targetName, int targetIndex) => string.IsNullOrWhiteSpace(targetName) ? targetIndex.ToString() : targetName;

        private static Dictionary<int, double> BuildMorphRadiusByVertex(
            MmdRenderingDescriptor descriptor,
            IReadOnlyDictionary<string, double> morphBudgets,
            float importScale)
        {
            var radiusByVertex = new Dictionary<int, double>();
            if (descriptor.vertexMorphs == null || descriptor.vertexMorphs.Count == 0)
                return radiusByVertex;

            foreach (MmdVertexMorphDescriptor morph in descriptor.vertexMorphs)
            {
                if (morph == null || string.IsNullOrWhiteSpace(morph.morphName) ||
                    !morphBudgets.TryGetValue(morph.morphName, out double budget) || budget <= 0.0)
                {
                    continue;
                }

                var aggregateDeltas = new Dictionary<int, Vector3>();
                if (morph.offsets != null)
                {
                    foreach (MmdVertexMorphOffsetDescriptor offset in morph.offsets)
                    {
                        if (TryToUnityPosition(offset.positionDelta, importScale, out Vector3 offsetPosition))
                        {
                            aggregateDeltas[offset.vertexIndex] = aggregateDeltas.TryGetValue(offset.vertexIndex, out Vector3 current)
                                ? current + offsetPosition
                                : offsetPosition;
                        }
                    }
                }

                foreach (KeyValuePair<int, Vector3> entry in aggregateDeltas)
                {
                    double contribution = budget * entry.Value.magnitude;
                    radiusByVertex[entry.Key] = radiusByVertex.TryGetValue(entry.Key, out double current)
                        ? current + contribution
                        : contribution;
                }
            }

            return radiusByVertex;
        }

        private static bool TryToUnityPosition(float[]? values, float importScale, out Vector3 position)
        {
            position = Vector3.zero;
            if (values == null || values.Length < 3 ||
                !IsFinite(values[0]) || !IsFinite(values[1]) || !IsFinite(values[2]))
            {
                return false;
            }

            position = ToUnityPosition(values, importScale);
            return true;
        }

        private static bool IsFiniteDouble(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
