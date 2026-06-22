#nullable enable

using System;
using System.Collections.Generic;
using static Mmd.Rendering.MmdMorphDescriptorBuilder;

namespace Mmd.Rendering
{
    /// <summary>
    /// Evaluates PMX material morph offsets and returns modified material descriptors
    /// with the offsets applied.
    ///
    /// Supported operations:
    /// - "add": result = base + offset * weight  (clamped to [0,1] for colors, [0,inf) for edge size)
    /// - "multiply": result = base * (1 + (offset - 1) * weight)  (clamped to [0,1] for colors, [0,inf) for edge size)
    ///
    /// Unknown operations are silently ignored.
    ///
    /// Scope: diffuse color/opacity, ambient color, edge color/opacity, edge size.
    /// Other PMX material morph fields (specular, texture blend) are preserved in the
    /// descriptor but not evaluated here.
    /// </summary>
    public static class MmdMaterialMorphEvaluator
    {
        /// <summary>
        /// Applies material morph offsets to a copy of the base material descriptors.
        /// Repeated evaluation starts from the base descriptors each time and does not
        /// accumulate deltas.
        /// </summary>
        /// <param name="baseMaterials">Base material descriptors from the rendering descriptor.</param>
        /// <param name="materialMorphs">Material morph descriptors built by <see cref="MmdMorphDescriptorBuilder.BuildMaterialMorphs"/>.</param>
        /// <param name="morphWeights">Sampled morph weights keyed by morph name, after group weight resolution.</param>
        /// <returns>New material descriptors with morph offsets applied.</returns>
        public static IReadOnlyList<MmdMaterialDescriptor> ApplyMaterialMorphs(
            IReadOnlyList<MmdMaterialDescriptor> baseMaterials,
            IReadOnlyList<MmdMaterialMorphDescriptor> materialMorphs,
            IReadOnlyDictionary<string, float> morphWeights)
        {
            if (baseMaterials == null)
            {
                throw new ArgumentNullException(nameof(baseMaterials));
            }

            if (materialMorphs == null)
            {
                throw new ArgumentNullException(nameof(materialMorphs));
            }

            if (morphWeights == null)
            {
                throw new ArgumentNullException(nameof(morphWeights));
            }

            // Clone base materials and stable-sort by materialIndex.
            var indexedMaterials = new IndexedMaterialDescriptor[baseMaterials.Count];
            for (int i = 0; i < baseMaterials.Count; i++)
            {
                indexedMaterials[i] = new IndexedMaterialDescriptor(baseMaterials[i], i);
            }

            Array.Sort(indexedMaterials, CompareMaterials);

            var results = new List<MmdMaterialDescriptor>(baseMaterials.Count);
            for (int i = 0; i < indexedMaterials.Length; i++)
            {
                results.Add(CloneMaterial(indexedMaterials[i].Material));
            }

            // Filter null/blank morphs and stable-sort by morphIndex.
            var sortedMorphs = new List<IndexedMorphDescriptor>(materialMorphs.Count);
            for (int i = 0; i < materialMorphs.Count; i++)
            {
                MmdMaterialMorphDescriptor m = materialMorphs[i];
                if (m == null || string.IsNullOrWhiteSpace(m.morphName))
                {
                    continue;
                }

                sortedMorphs.Add(new IndexedMorphDescriptor(m, i));
            }

            sortedMorphs.Sort(CompareMorphDescriptors);

            for (int mi = 0; mi < sortedMorphs.Count; mi++)
            {
                MmdMaterialMorphDescriptor morph = sortedMorphs[mi].Morph;

                if (!morphWeights.TryGetValue(morph.morphName, out float weight) || weight == 0.0f)
                {
                    continue;
                }

                if (float.IsNaN(weight) || float.IsInfinity(weight))
                {
                    throw new InvalidOperationException(
                        $"Material morph weight must be finite: {morph.morphName}");
                }

                if (morph.offsets == null || morph.offsets.Count == 0)
                {
                    continue;
                }

                for (int oi = 0; oi < morph.offsets.Count; oi++)
                {
                    MmdMaterialMorphOffsetDescriptor offset = morph.offsets[oi];
                    if (offset == null)
                    {
                        continue;
                    }

                    string operation = offset.operation ?? "unknown";
                    ApplyOffsetToTargets(results, offset, weight, operation);
                }
            }

            return results;
        }

        private static void ApplyOffsetToTargets(
            List<MmdMaterialDescriptor> results,
            MmdMaterialMorphOffsetDescriptor offset,
            float weight,
            string operation)
        {
            if (offset.materialIndex == -1)
            {
                for (int s = 0; s < results.Count; s++)
                {
                    ApplyOperationToSlot(results[s], offset, weight, operation);
                }
            }
            else
            {
                for (int s = 0; s < results.Count; s++)
                {
                    if (results[s].materialIndex == offset.materialIndex)
                    {
                        ApplyOperationToSlot(results[s], offset, weight, operation);
                        break;
                    }
                }
                // Missing specific materialIndex is silently skipped.
            }
        }

        private static void ApplyOperationToSlot(
            MmdMaterialDescriptor target,
            MmdMaterialMorphOffsetDescriptor offset,
            float weight,
            string operation)
        {
            if (string.Equals(operation, "add", StringComparison.Ordinal))
            {
                ApplyAddOffset(target, offset, weight);
            }
            else if (string.Equals(operation, "multiply", StringComparison.Ordinal))
            {
                ApplyMultiplyOffset(target, offset, weight);
            }
            // Unknown operations are silently ignored per brief.
        }

        private static int CompareMaterials(IndexedMaterialDescriptor left, IndexedMaterialDescriptor right)
        {
            int cmp = left.Material.materialIndex.CompareTo(right.Material.materialIndex);
            return cmp != 0 ? cmp : left.SourceIndex.CompareTo(right.SourceIndex);
        }

        private static int CompareMorphDescriptors(IndexedMorphDescriptor left, IndexedMorphDescriptor right)
        {
            int cmp = left.Morph.morphIndex.CompareTo(right.Morph.morphIndex);
            return cmp != 0 ? cmp : left.SourceIndex.CompareTo(right.SourceIndex);
        }

        private static MmdMaterialDescriptor CloneMaterial(MmdMaterialDescriptor source)
        {
            return new MmdMaterialDescriptor
            {
                materialIndex = source.materialIndex,
                name = source.name,
                texture = source.texture,
                sphereTexture = source.sphereTexture,
                toonTexture = source.toonTexture,
                alpha = source.alpha,
                diffuseColor = CopyVector(source.diffuseColor),
                ambientColor = CopyVector(source.ambientColor),
                edgeColor = CopyVector(source.edgeColor),
                edgeSize = source.edgeSize,
                sphereTextureMode = source.sphereTextureMode,
                toonShared = source.toonShared,
                sharedToonIndex = source.sharedToonIndex,
                cullingPolicy = source.cullingPolicy,
                drawEdgeFlag = source.drawEdgeFlag,
                vertexStart = source.vertexStart,
                vertexCount = source.vertexCount
            };
        }

        private static float[] CopyVector(float[] source)
        {
            var copy = new float[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static void ApplyAddOffset(
            MmdMaterialDescriptor target,
            MmdMaterialMorphOffsetDescriptor offset,
            float weight)
        {
            // Diffuse color (RGB) + diffuse opacity (A) -> combined in _BaseColor later.
            if (offset.diffuseColor.Length == 3)
            {
                for (int c = 0; c < 3; c++)
                {
                    target.diffuseColor[c] = Clamp01(target.diffuseColor[c] + offset.diffuseColor[c] * weight);
                }
            }

            target.alpha = Clamp01(target.alpha + offset.diffuseOpacity * weight);

            // Ambient color (RGB).
            if (offset.ambientColor.Length == 3)
            {
                for (int c = 0; c < 3; c++)
                {
                    target.ambientColor[c] = Clamp01(target.ambientColor[c] + offset.ambientColor[c] * weight);
                }
            }

            // Edge color (RGB) + edge opacity (A).
            if (offset.edgeColor.Length == 3)
            {
                // edgeColor[3] is used for alpha; it's stored in the 4th element.
                if (target.edgeColor.Length >= 4)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        target.edgeColor[c] = Clamp01(target.edgeColor[c] + offset.edgeColor[c] * weight);
                    }
                }
                else
                {
                    // edgeColor has fewer than 4 elements; pad and apply.
                    var newEdge = new float[4];
                    for (int c = 0; c < target.edgeColor.Length && c < 3; c++)
                    {
                        newEdge[c] = Clamp01(target.edgeColor[c] + offset.edgeColor[c] * weight);
                    }

                    for (int c = target.edgeColor.Length; c < 3; c++)
                    {
                        newEdge[c] = Clamp01(0.0f + offset.edgeColor[c] * weight);
                    }

                    newEdge[3] = target.edgeColor.Length > 3 ? target.edgeColor[3] : 1.0f;
                    target.edgeColor = newEdge;
                }
            }

            if (target.edgeColor.Length >= 4)
            {
                target.edgeColor[3] = Clamp01(target.edgeColor[3] + offset.edgeOpacity * weight);
            }

            // Edge size.
            target.edgeSize = Math.Max(0.0f, target.edgeSize + offset.edgeSize * weight);
        }

        private static void ApplyMultiplyOffset(
            MmdMaterialDescriptor target,
            MmdMaterialMorphOffsetDescriptor offset,
            float weight)
        {
            // For "multiply": result = base * (1 + (offset - 1) * weight)
            // Equivalent to: lerp(base, base * offset, weight)
            // Clamped to [0, 1] for colors.

            if (offset.diffuseColor.Length == 3)
            {
                for (int c = 0; c < 3; c++)
                {
                    float factor = 1.0f + (offset.diffuseColor[c] - 1.0f) * weight;
                    target.diffuseColor[c] = Clamp01(target.diffuseColor[c] * factor);
                }
            }

            float opacityFactor = 1.0f + (offset.diffuseOpacity - 1.0f) * weight;
            target.alpha = Clamp01(target.alpha * opacityFactor);

            if (offset.ambientColor.Length == 3)
            {
                for (int c = 0; c < 3; c++)
                {
                    float factor = 1.0f + (offset.ambientColor[c] - 1.0f) * weight;
                    target.ambientColor[c] = Clamp01(target.ambientColor[c] * factor);
                }
            }

            if (offset.edgeColor.Length == 3)
            {
                if (target.edgeColor.Length >= 4)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        float factor = 1.0f + (offset.edgeColor[c] - 1.0f) * weight;
                        target.edgeColor[c] = Clamp01(target.edgeColor[c] * factor);
                    }
                }
                else
                {
                    var newEdge = new float[4];
                    for (int c = 0; c < target.edgeColor.Length && c < 3; c++)
                    {
                        float factor = 1.0f + (offset.edgeColor[c] - 1.0f) * weight;
                        newEdge[c] = Clamp01(target.edgeColor[c] * factor);
                    }

                    for (int c = target.edgeColor.Length; c < 3; c++)
                    {
                        float factor = 1.0f + (offset.edgeColor[c] - 1.0f) * weight;
                        newEdge[c] = Clamp01(1.0f * factor);
                    }

                    newEdge[3] = target.edgeColor.Length > 3 ? target.edgeColor[3] : 1.0f;
                    target.edgeColor = newEdge;
                }
            }

            if (target.edgeColor.Length >= 4)
            {
                float edgeAlphaFactor = 1.0f + (offset.edgeOpacity - 1.0f) * weight;
                target.edgeColor[3] = Clamp01(target.edgeColor[3] * edgeAlphaFactor);
            }

            float edgeSizeFactor = 1.0f + (offset.edgeSize - 1.0f) * weight;
            target.edgeSize = Math.Max(0.0f, target.edgeSize * edgeSizeFactor);
        }

        private static float Clamp01(float value)
        {
            if (value < 0.0f) return 0.0f;
            if (value > 1.0f) return 1.0f;
            return value;
        }

        private readonly struct IndexedMaterialDescriptor
        {
            public IndexedMaterialDescriptor(MmdMaterialDescriptor material, int sourceIndex)
            {
                Material = material;
                SourceIndex = sourceIndex;
            }

            public MmdMaterialDescriptor Material { get; }
            public int SourceIndex { get; }
        }

        private readonly struct IndexedMorphDescriptor
        {
            public IndexedMorphDescriptor(MmdMaterialMorphDescriptor morph, int sourceIndex)
            {
                Morph = morph;
                SourceIndex = sourceIndex;
            }

            public MmdMaterialMorphDescriptor Morph { get; }
            public int SourceIndex { get; }
        }
    }
}
