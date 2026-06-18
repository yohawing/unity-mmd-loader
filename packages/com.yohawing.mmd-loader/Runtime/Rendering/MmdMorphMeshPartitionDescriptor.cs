#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Mmd.Rendering
{
    [Serializable]
    public sealed class MmdMorphMeshPartitionSummaryDescriptor
    {
        public int vertexCount;
        public int indexCount;
        public int materialCount;
        public int submeshCount;
        public int vertexMorphCount;
        public int crossSubmeshMorphCount;
        public int crossMaterialMorphCount;
        public int transparentTouchedMorphCount;
        public int outlineTouchedMorphCount;
        public int unmappedVertexOffsetCount;
        public int maxTouchedSubmeshCount;
        public List<MmdMorphMeshPartitionMaterialDescriptor> materials = new();
        public List<MmdMorphMeshPartitionSubmeshDescriptor> submeshes = new();
        public List<MmdVertexMorphMeshPartitionDescriptor> vertexMorphPartitions = new();
    }

    [Serializable]
    public sealed class MmdMorphMeshPartitionMaterialDescriptor
    {
        public int materialIndex;
        public int vertexStart;
        public int vertexCount;
        public float alpha;
        public bool isTransparent;
        public string renderOrderBucket = string.Empty;
        public string cullingPolicy = string.Empty;
        public float edgeSize;
        public bool outlineEligible;
        public bool drawEdgeFlagExposed;
        public bool drawEdgeFlag;
    }

    [Serializable]
    public sealed class MmdMorphMeshPartitionSubmeshDescriptor
    {
        public int submeshIndex;
        public int materialIndex;
        public int indexStart;
        public int indexCount;
        public int uniqueReferencedVertexCount;
        public int minReferencedVertexIndex;
        public int maxReferencedVertexIndex;
    }

    [Serializable]
    public sealed class MmdVertexMorphMeshPartitionDescriptor
    {
        public int morphIndex;
        public string morphName = string.Empty;
        public int offsetCount;
        public int uniqueVertexCount;
        public int minVertexIndex;
        public int maxVertexIndex;
        public List<int> touchedSubmeshIndices = new();
        public List<int> touchedMaterialIndices = new();
        public bool crossesSubmeshRange;
        public bool crossesMaterialRange;
        public bool touchesOpaqueMaterial;
        public bool touchesTransparentMaterial;
        public bool touchesOutlineEligibleMaterial;
        public int unmappedVertexOffsetCount;
        public string partitionRecommendation = string.Empty;
        public List<MmdVertexMorphMeshPartitionCoverageDescriptor> coverage = new();
    }

    [Serializable]
    public sealed class MmdVertexMorphMeshPartitionCoverageDescriptor
    {
        public int submeshIndex;
        public int materialIndex;
        public int offsetVertexCount;
        public int referencedTriangleIndexCount;
    }

    public static class MmdMorphMeshPartitionDescriptorBuilder
    {
        public const string SingleSubmeshRecommendation = "single-submesh-local";
        public const string CrossSubmeshRecommendation = "cross-submesh-partition-required";
        public const string UnmappedVertexRecommendation = "inspect-unmapped-vertex-offsets";
        public const string NoRenderedCoverageRecommendation = "no-rendered-vertex-coverage";

        public static MmdMorphMeshPartitionSummaryDescriptor Build(MmdRenderingDescriptor rendering)
        {
            if (rendering == null)
            {
                throw new ArgumentNullException(nameof(rendering));
            }

            var materials = rendering.materials
                .OrderBy(material => material.materialIndex)
                .ToList();
            var submeshes = rendering.submeshes
                .OrderBy(submesh => submesh.submeshIndex)
                .ToList();
            var materialByIndex = materials.ToDictionary(material => material.materialIndex, material => material);
            List<int> indices = rendering.indices ?? new List<int>();
            var submeshVertexSets = BuildSubmeshVertexSets(indices, submeshes);
            var vertexToSubmeshes = BuildVertexToSubmeshes(submeshVertexSets);
            var vertexMorphPartitions = (rendering.vertexMorphs ?? new List<MmdVertexMorphDescriptor>())
                .OrderBy(morph => morph.morphIndex)
                .Select(morph => BuildMorphPartition(morph, indices, submeshes, materialByIndex, submeshVertexSets, vertexToSubmeshes))
                .ToList();
            int crossSubmeshMorphCount = 0;
            int crossMaterialMorphCount = 0;
            int transparentTouchedMorphCount = 0;
            int outlineTouchedMorphCount = 0;
            int unmappedVertexOffsetCount = 0;
            int maxTouchedSubmeshCount = 0;
            foreach (MmdVertexMorphMeshPartitionDescriptor partition in vertexMorphPartitions)
            {
                if (partition.crossesSubmeshRange)
                {
                    crossSubmeshMorphCount++;
                }

                if (partition.crossesMaterialRange)
                {
                    crossMaterialMorphCount++;
                }

                if (partition.touchesTransparentMaterial)
                {
                    transparentTouchedMorphCount++;
                }

                if (partition.touchesOutlineEligibleMaterial)
                {
                    outlineTouchedMorphCount++;
                }

                unmappedVertexOffsetCount += partition.unmappedVertexOffsetCount;
                if (partition.touchedSubmeshIndices.Count > maxTouchedSubmeshCount)
                {
                    maxTouchedSubmeshCount = partition.touchedSubmeshIndices.Count;
                }
            }

            return new MmdMorphMeshPartitionSummaryDescriptor
            {
                vertexCount = rendering.vertices?.Count ?? 0,
                indexCount = indices.Count,
                materialCount = materials.Count,
                submeshCount = submeshes.Count,
                vertexMorphCount = vertexMorphPartitions.Count,
                crossSubmeshMorphCount = crossSubmeshMorphCount,
                crossMaterialMorphCount = crossMaterialMorphCount,
                transparentTouchedMorphCount = transparentTouchedMorphCount,
                outlineTouchedMorphCount = outlineTouchedMorphCount,
                unmappedVertexOffsetCount = unmappedVertexOffsetCount,
                maxTouchedSubmeshCount = maxTouchedSubmeshCount,
                materials = materials.Select(BuildMaterialSummary).ToList(),
                submeshes = submeshes.Select(submesh => BuildSubmeshSummary(submesh, submeshVertexSets[submesh.submeshIndex])).ToList(),
                vertexMorphPartitions = vertexMorphPartitions
            };
        }

        private static Dictionary<int, HashSet<int>> BuildSubmeshVertexSets(IReadOnlyList<int> indices, IReadOnlyList<MmdSubmeshDescriptor> submeshes)
        {
            var result = new Dictionary<int, HashSet<int>>(submeshes.Count);
            foreach (MmdSubmeshDescriptor submesh in submeshes)
            {
                var vertices = new HashSet<int>();
                int end = Math.Min(indices.Count, submesh.indexStart + submesh.indexCount);
                for (int i = Math.Max(0, submesh.indexStart); i < end; i++)
                {
                    vertices.Add(indices[i]);
                }

                result[submesh.submeshIndex] = vertices;
            }

            return result;
        }

        private static Dictionary<int, SortedSet<int>> BuildVertexToSubmeshes(IReadOnlyDictionary<int, HashSet<int>> submeshVertexSets)
        {
            var result = new Dictionary<int, SortedSet<int>>();
            foreach (KeyValuePair<int, HashSet<int>> entry in submeshVertexSets)
            {
                foreach (int vertexIndex in entry.Value)
                {
                    if (!result.TryGetValue(vertexIndex, out SortedSet<int>? submeshIndices))
                    {
                        submeshIndices = new SortedSet<int>();
                        result[vertexIndex] = submeshIndices;
                    }

                    submeshIndices.Add(entry.Key);
                }
            }

            return result;
        }

        private static MmdVertexMorphMeshPartitionDescriptor BuildMorphPartition(
            MmdVertexMorphDescriptor morph,
            IReadOnlyList<int> indices,
            IReadOnlyList<MmdSubmeshDescriptor> submeshes,
            IReadOnlyDictionary<int, MmdMaterialDescriptor> materialByIndex,
            IReadOnlyDictionary<int, HashSet<int>> submeshVertexSets,
            IReadOnlyDictionary<int, SortedSet<int>> vertexToSubmeshes)
        {
            int[] uniqueVertices = (morph.offsets ?? new List<MmdVertexMorphOffsetDescriptor>())
                .Select(offset => offset.vertexIndex)
                .Distinct()
                .OrderBy(vertexIndex => vertexIndex)
                .ToArray();
            var touchedSubmeshes = new SortedSet<int>();
            int unmapped = 0;
            foreach (int vertexIndex in uniqueVertices)
            {
                if (!vertexToSubmeshes.TryGetValue(vertexIndex, out SortedSet<int>? submeshIndices) || submeshIndices.Count == 0)
                {
                    unmapped++;
                    continue;
                }

                foreach (int submeshIndex in submeshIndices)
                {
                    touchedSubmeshes.Add(submeshIndex);
                }
            }

            MmdSubmeshDescriptor[] touchedSubmeshDescriptors = submeshes
                .Where(submesh => touchedSubmeshes.Contains(submesh.submeshIndex))
                .OrderBy(submesh => submesh.submeshIndex)
                .ToArray();
            int[] touchedMaterialIndices = touchedSubmeshDescriptors
                .Select(submesh => submesh.materialIndex)
                .Distinct()
                .OrderBy(index => index)
                .ToArray();
            MmdMaterialDescriptor[] touchedMaterials = touchedMaterialIndices
                .Where(materialByIndex.ContainsKey)
                .Select(index => materialByIndex[index])
                .ToArray();
            var uniqueVertexSet = uniqueVertices.ToHashSet();
            List<MmdVertexMorphMeshPartitionCoverageDescriptor> coverage = touchedSubmeshDescriptors
                .Select(submesh => BuildCoverage(submesh, indices, submeshVertexSets[submesh.submeshIndex], uniqueVertexSet))
                .ToList();

            return new MmdVertexMorphMeshPartitionDescriptor
            {
                morphIndex = morph.morphIndex,
                morphName = morph.morphName,
                offsetCount = morph.offsets?.Count ?? 0,
                uniqueVertexCount = uniqueVertices.Length,
                minVertexIndex = uniqueVertices.Length == 0 ? -1 : uniqueVertices[0],
                maxVertexIndex = uniqueVertices.Length == 0 ? -1 : uniqueVertices[uniqueVertices.Length - 1],
                touchedSubmeshIndices = touchedSubmeshDescriptors.Select(submesh => submesh.submeshIndex).ToList(),
                touchedMaterialIndices = touchedMaterialIndices.ToList(),
                crossesSubmeshRange = touchedSubmeshes.Count > 1,
                crossesMaterialRange = touchedMaterialIndices.Length > 1,
                touchesOpaqueMaterial = touchedMaterials.Any(material => material.alpha >= 1.0f),
                touchesTransparentMaterial = touchedMaterials.Any(material => material.alpha < 1.0f),
                touchesOutlineEligibleMaterial = touchedMaterials.Any(material => material.drawEdgeFlag && material.edgeSize > 0.0f),
                unmappedVertexOffsetCount = unmapped,
                partitionRecommendation = ResolveRecommendation(touchedSubmeshes.Count, unmapped),
                coverage = coverage
            };
        }

        private static MmdVertexMorphMeshPartitionCoverageDescriptor BuildCoverage(
            MmdSubmeshDescriptor submesh,
            IReadOnlyList<int> indices,
            HashSet<int> submeshVertices,
            HashSet<int> morphVertices)
        {
            int offsetVertexCount = morphVertices.Count(submeshVertices.Contains);
            int referencedIndexCount = 0;
            int end = Math.Min(indices.Count, submesh.indexStart + submesh.indexCount);
            for (int i = Math.Max(0, submesh.indexStart); i < end; i++)
            {
                if (morphVertices.Contains(indices[i]))
                {
                    referencedIndexCount++;
                }
            }

            return new MmdVertexMorphMeshPartitionCoverageDescriptor
            {
                submeshIndex = submesh.submeshIndex,
                materialIndex = submesh.materialIndex,
                offsetVertexCount = offsetVertexCount,
                referencedTriangleIndexCount = referencedIndexCount
            };
        }

        private static MmdMorphMeshPartitionMaterialDescriptor BuildMaterialSummary(MmdMaterialDescriptor material)
        {
            bool isTransparent = material.alpha < 1.0f;
            return new MmdMorphMeshPartitionMaterialDescriptor
            {
                materialIndex = material.materialIndex,
                vertexStart = material.vertexStart,
                vertexCount = material.vertexCount,
                alpha = material.alpha,
                isTransparent = isTransparent,
                renderOrderBucket = isTransparent ? "alphaBlend" : "opaque",
                cullingPolicy = material.cullingPolicy,
                edgeSize = material.edgeSize,
                outlineEligible = material.drawEdgeFlag && material.edgeSize > 0.0f,
                drawEdgeFlagExposed = true,
                drawEdgeFlag = material.drawEdgeFlag
            };
        }

        private static MmdMorphMeshPartitionSubmeshDescriptor BuildSubmeshSummary(
            MmdSubmeshDescriptor submesh,
            IReadOnlyCollection<int> referencedVertices)
        {
            return new MmdMorphMeshPartitionSubmeshDescriptor
            {
                submeshIndex = submesh.submeshIndex,
                materialIndex = submesh.materialIndex,
                indexStart = submesh.indexStart,
                indexCount = submesh.indexCount,
                uniqueReferencedVertexCount = referencedVertices.Count,
                minReferencedVertexIndex = referencedVertices.Count == 0 ? -1 : referencedVertices.Min(),
                maxReferencedVertexIndex = referencedVertices.Count == 0 ? -1 : referencedVertices.Max()
            };
        }

        private static string ResolveRecommendation(int touchedSubmeshCount, int unmappedVertexOffsetCount)
        {
            if (unmappedVertexOffsetCount > 0)
            {
                return UnmappedVertexRecommendation;
            }

            if (touchedSubmeshCount == 0)
            {
                return NoRenderedCoverageRecommendation;
            }

            return touchedSubmeshCount == 1 ? SingleSubmeshRecommendation : CrossSubmeshRecommendation;
        }
    }
}
