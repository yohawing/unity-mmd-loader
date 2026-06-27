#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Mmd.Rendering;

namespace Mmd.UnityIntegration
{
    public static class MmdUnityFrameApplier
    {
        public static void ApplyFrame(MmdUnityModelInstance instance, MmdEvaluatedFrame frame)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            if (frame.bones == null)
            {
                throw new ArgumentException("Evaluated frame bones are required.", nameof(frame));
            }

            foreach (MmdEvaluatedBonePose bonePose in frame.bones)
            {
                if (bonePose == null)
                {
                    throw new ArgumentException("Evaluated frame bone entries must not be null.", nameof(frame));
                }

                int slot = bonePose.index;
                if (slot < 0 || slot >= instance.BoneTransforms.Length)
                {
                    throw new ArgumentException($"Evaluated bone index {slot} has no Unity bone transform.", nameof(frame));
                }

                Transform boneTransform = instance.BoneTransforms[slot];
                Vector3 translationDelta = ToUnityPosition(ValidateVector3(bonePose.localPosition, slot, "localPosition"));
                boneTransform.localPosition = instance.BindLocalPositions[slot] + translationDelta * instance.ImportScale;
                boneTransform.localRotation = instance.BindLocalRotations[slot] * ToUnityRotation(ValidateQuaternion(bonePose.localRotation, slot));
                boneTransform.localScale = ToUnityScale(ValidateVector3(bonePose.localScale, slot, "localScale"));
            }

            ApplyMorphs(instance, frame);
        }

        internal static void ApplyMorphs(MmdUnityModelInstance instance, MmdEvaluatedFrame frame)
        {
            ApplyMorphsCore(instance, frame, null, groupMorphsResolvedExternally: false);
        }

        /// <summary>
        /// Applies morph weights to the instance. When <paramref name="groupMorphsResolvedExternally"/>
        /// is true the caller (e.g. the native fast runtime) has ALREADY expanded group morph weights
        /// into their member morphs, so group expansion is skipped here to avoid double-applying each
        /// group's contribution. Flip morphs are still resolved because the native runtime does not
        /// expand them.
        /// </summary>
        internal static void ApplyMorphs(
            MmdUnityModelInstance instance,
            MmdEvaluatedFrame frame,
            bool groupMorphsResolvedExternally)
        {
            ApplyMorphsCore(instance, frame, null, groupMorphsResolvedExternally);
        }

        internal static MmdUnityMorphApplyTimingSummary ApplyMorphsWithTiming(MmdUnityModelInstance instance, MmdEvaluatedFrame frame)
        {
            var timing = new MmdUnityMorphApplyTimingSummary();
            ApplyMorphsCore(instance, frame, timing, groupMorphsResolvedExternally: false);
            return timing;
        }

        private static readonly List<MmdGroupMorphDescriptor> EmptyGroupMorphs = new();

        private static void ApplyMorphsCore(
            MmdUnityModelInstance instance,
            MmdEvaluatedFrame frame,
            MmdUnityMorphApplyTimingSummary? timing,
            bool groupMorphsResolvedExternally)
        {
            MmdRenderingDescriptor descriptor = instance.RenderingDescriptor;
            IReadOnlyList<MmdMeshVertexDescriptor>? vertices = descriptor.vertices;
            IReadOnlyList<MmdVertexMorphDescriptor>? vertexMorphs = descriptor.vertexMorphs;
            IReadOnlyList<MmdGroupMorphDescriptor>? groupMorphs = descriptor.groupMorphs;
            IReadOnlyList<MmdMorphDescriptorBuilder.MmdUvMorphDescriptor>? uvMorphs = descriptor.uvMorphs;
            IReadOnlyList<MmdMorphDescriptorBuilder.MmdMaterialMorphDescriptor>? materialMorphs = descriptor.materialMorphs;
            IReadOnlyList<MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor>? flipMorphs = descriptor.flipMorphs;
            if (timing != null)
            {
                timing.vertexCount = vertices?.Count ?? 0;
                timing.morphWeightCount = frame.morphs?.Count ?? 0;
            }

            long totalStart = timing != null ? Stopwatch.GetTimestamp() : 0L;
            bool hasVertexMorphs = vertexMorphs != null && vertexMorphs.Count > 0;
            bool hasTextureUvMorphs = HasTextureUvMorphs(uvMorphs);
            bool hasMaterialMorphs = materialMorphs != null && materialMorphs.Count > 0;
            bool hasAnyMorph = hasVertexMorphs || hasTextureUvMorphs || hasMaterialMorphs;
            if (timing != null)
            {
                timing.hasVertexMorphs = hasVertexMorphs;
                timing.hasTextureUvMorphs = hasTextureUvMorphs;
                timing.hasMaterialMorphs = hasMaterialMorphs;
                timing.hasAnyMorph = hasAnyMorph;
            }

            if (!hasAnyMorph)
            {
                if (timing != null)
                {
                    timing.totalMs = ToMilliseconds(Stopwatch.GetTimestamp() - totalStart);
                }

                return;
            }

            if (frame.morphs == null)
            {
                throw new ArgumentException("Evaluated frame morph weights are required when morph descriptors exist.", nameof(frame));
            }

            long stageStart = timing != null ? Stopwatch.GetTimestamp() : 0L;
            Dictionary<string, float> morphWeightMap = BuildMorphWeightMap(frame);
            if (timing != null)
            {
                timing.buildWeightMapMs = ToMilliseconds(Stopwatch.GetTimestamp() - stageStart);
                timing.nonZeroMorphWeightCount = CountNonZeroWeights(morphWeightMap);
            }

            stageStart = timing != null ? Stopwatch.GetTimestamp() : 0L;
            IReadOnlyList<MmdGroupMorphDescriptor> groupMorphsForResolve = groupMorphsResolvedExternally
                ? EmptyGroupMorphs
                : (groupMorphs ?? EmptyGroupMorphs);
            IReadOnlyDictionary<string, float> resolvedWeights = MmdCompositeMorphWeightResolver.Resolve(
                morphWeightMap,
                groupMorphsForResolve,
                flipMorphs ?? Array.Empty<MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor>());
            if (timing != null)
            {
                timing.resolveCompositeWeightsMs = ToMilliseconds(Stopwatch.GetTimestamp() - stageStart);
                timing.resolvedWeightCount = resolvedWeights.Count;
                timing.nonZeroResolvedWeightCount = CountNonZeroWeights(resolvedWeights);
            }

            SkinnedMeshRenderer? blendShapeRenderer = hasVertexMorphs && instance.VertexMorphBlendShapes.Count > 0
                ? instance.SkinnedMeshRenderer
                : null;
            bool useBlendShapeWeights = blendShapeRenderer != null;

            IReadOnlyList<MmdMeshVertexDescriptor>? morphedVertices = vertices;
            if (hasVertexMorphs && !useBlendShapeWeights)
            {
                IReadOnlyList<MmdMeshVertexDescriptor> baseVertices = vertices
                    ?? throw new InvalidOperationException("Rendering descriptor vertices are required for vertex morph evaluation.");
                IReadOnlyList<MmdVertexMorphDescriptor> activeVertexMorphs = vertexMorphs
                    ?? throw new InvalidOperationException("Rendering descriptor vertex morphs are required for vertex morph evaluation.");
                stageStart = timing != null ? Stopwatch.GetTimestamp() : 0L;
                morphedVertices = MmdVertexMorphEvaluator.ApplyVertexMorphs(
                    baseVertices,
                    activeVertexMorphs,
                    resolvedWeights);
                if (timing != null)
                {
                    timing.vertexMorphEvalMs = ToMilliseconds(Stopwatch.GetTimestamp() - stageStart);
                }
            }

            if (hasTextureUvMorphs)
            {
                IReadOnlyList<MmdMeshVertexDescriptor> baseForUv = useBlendShapeWeights
                    ? (vertices ?? throw new InvalidOperationException("Rendering descriptor vertices are required for texture UV morph evaluation."))
                    : (morphedVertices ?? throw new InvalidOperationException("Morphed vertices are required for texture UV morph evaluation."));
                IReadOnlyList<MmdMorphDescriptorBuilder.MmdUvMorphDescriptor> activeUvMorphs = uvMorphs
                    ?? throw new InvalidOperationException("Rendering descriptor UV morphs are required for texture UV morph evaluation.");
                stageStart = timing != null ? Stopwatch.GetTimestamp() : 0L;
                morphedVertices = MmdTextureUvMorphEvaluator.ApplyTextureUvMorphs(
                    baseForUv,
                    activeUvMorphs,
                    resolvedWeights);
                if (timing != null)
                {
                    timing.textureUvMorphEvalMs = ToMilliseconds(Stopwatch.GetTimestamp() - stageStart);
                }
            }

            if (hasMaterialMorphs)
            {
                stageStart = timing != null ? Stopwatch.GetTimestamp() : 0L;
                ApplyMaterialMorphs(instance, descriptor, resolvedWeights);
                if (timing != null)
                {
                    timing.materialMorphApplyMs = ToMilliseconds(Stopwatch.GetTimestamp() - stageStart);
                }
            }

            if (blendShapeRenderer != null)
            {
                stageStart = timing != null ? Stopwatch.GetTimestamp() : 0L;
                bool blendShapeBoundsWeightsChanged = false;
                int bindingSlot = 0;
                foreach (MmdUnityVertexMorphBlendShapeBinding binding in instance.VertexMorphBlendShapes)
                {
                    float resolvedWeight = resolvedWeights.TryGetValue(binding.MorphName, out float w) ? w : 0f;
                    if (!IsFinite(resolvedWeight))
                    {
                        throw new InvalidOperationException($"BlendShape morph weight must be finite: {binding.MorphName}");
                    }

                    if (bindingSlot >= instance.LastBlendShapeBoundsWeights.Length)
                    {
                        throw new InvalidOperationException("BlendShape bounds weight cache does not match BlendShape bindings.");
                    }

                    if (resolvedWeight != instance.LastBlendShapeBoundsWeights[bindingSlot])
                    {
                        blendShapeBoundsWeightsChanged = true;
                        instance.LastBlendShapeBoundsWeights[bindingSlot] = resolvedWeight;
                    }

                    blendShapeRenderer.SetBlendShapeWeight(binding.BlendShapeIndex, resolvedWeight * 100f);
                    bindingSlot++;
                }

                if (timing != null)
                {
                    timing.setBlendShapeWeightMs = ToMilliseconds(Stopwatch.GetTimestamp() - stageStart);
                    timing.blendShapePathUsed = true;
                }

                if (hasTextureUvMorphs)
                {
                    IReadOnlyList<MmdMeshVertexDescriptor> uvMorphedVertices = morphedVertices
                        ?? throw new InvalidOperationException("Morphed vertices are required for texture UV upload.");
                    if (timing != null)
                    {
                        timing.meshUploadRequired = true;
                    }

                    stageStart = timing != null ? Stopwatch.GetTimestamp() : 0L;
                    var unityUvsOnly = new List<Vector2>(uvMorphedVertices.Count);
                    for (int i = 0; i < uvMorphedVertices.Count; i++)
                    {
                        float[] uv = ValidateMorphedVector2(uvMorphedVertices[i].uv, i, "uv");
                        float[] viewportUv = MmdTextureOrientationDescriptorBuilder.ToViewportUv(uv);
                        unityUvsOnly.Add(new Vector2(viewportUv[0], viewportUv[1]));
                    }

                    instance.Mesh.SetUVs(0, unityUvsOnly);
                    if (timing != null)
                    {
                        timing.setUvsMs = ToMilliseconds(Stopwatch.GetTimestamp() - stageStart);
                    }
                }

                if (blendShapeBoundsWeightsChanged)
                {
                    stageStart = timing != null ? Stopwatch.GetTimestamp() : 0L;
                    blendShapeRenderer.localBounds = CalculateBlendShapeMorphedBounds(descriptor, resolvedWeights, instance.ImportScale);
                    if (timing != null)
                    {
                        timing.localBoundsAssignMs = ToMilliseconds(Stopwatch.GetTimestamp() - stageStart);
                        timing.localBoundsAssigned = true;
                    }
                }
                else if (timing != null)
                {
                    timing.localBoundsSkipped = true;
                }

                if (timing != null)
                {
                    timing.totalMs = ToMilliseconds(Stopwatch.GetTimestamp() - totalStart);
                }

                return;
            }

            if (!hasVertexMorphs && !hasTextureUvMorphs)
            {
                if (timing != null)
                {
                    timing.totalMs = ToMilliseconds(Stopwatch.GetTimestamp() - totalStart);
                }

                return;
            }

            IReadOnlyList<MmdMeshVertexDescriptor> uploadVertices = morphedVertices
                ?? throw new InvalidOperationException("Morphed vertices are required for mesh upload.");

            if (timing != null)
            {
                timing.meshUploadRequired = true;
            }

            stageStart = timing != null ? Stopwatch.GetTimestamp() : 0L;
            var unityVertices = new List<Vector3>(uploadVertices.Count);
            var unityUvs = new List<Vector2>(uploadVertices.Count);
            for (int i = 0; i < uploadVertices.Count; i++)
            {
                Vector3 morphedPos = ToUnityPosition(ValidateMorphedVector3(uploadVertices[i].position, i, "position"));
                unityVertices.Add(morphedPos * instance.ImportScale);
                float[] uv = ValidateMorphedVector2(uploadVertices[i].uv, i, "uv");
                float[] viewportUv = MmdTextureOrientationDescriptorBuilder.ToViewportUv(uv);
                unityUvs.Add(new Vector2(viewportUv[0], viewportUv[1]));
            }
            if (timing != null)
            {
                timing.unityBufferBuildMs = ToMilliseconds(Stopwatch.GetTimestamp() - stageStart);
            }

            Mesh mesh = instance.Mesh;
            stageStart = timing != null ? Stopwatch.GetTimestamp() : 0L;
            mesh.SetVertices(unityVertices);
            if (timing != null)
            {
                timing.setVerticesMs = ToMilliseconds(Stopwatch.GetTimestamp() - stageStart);
            }

            stageStart = timing != null ? Stopwatch.GetTimestamp() : 0L;
            mesh.SetUVs(0, unityUvs);
            if (timing != null)
            {
                timing.setUvsMs = ToMilliseconds(Stopwatch.GetTimestamp() - stageStart);
            }

            stageStart = timing != null ? Stopwatch.GetTimestamp() : 0L;
            mesh.RecalculateBounds();
            if (timing != null)
            {
                timing.recalculateBoundsMs = ToMilliseconds(Stopwatch.GetTimestamp() - stageStart);
            }

            SkinnedMeshRenderer? meshUploadRenderer = instance.SkinnedMeshRenderer;
            if (meshUploadRenderer != null)
            {
                stageStart = timing != null ? Stopwatch.GetTimestamp() : 0L;
                meshUploadRenderer.localBounds = mesh.bounds;
                if (timing != null)
                {
                    timing.localBoundsAssignMs = ToMilliseconds(Stopwatch.GetTimestamp() - stageStart);
                }
            }

            if (timing != null)
            {
                timing.totalMs = ToMilliseconds(Stopwatch.GetTimestamp() - totalStart);
            }
        }

        private static void ApplyMaterialMorphs(
            MmdUnityModelInstance instance,
            MmdRenderingDescriptor descriptor,
            IReadOnlyDictionary<string, float> resolvedWeights)
        {
            IReadOnlyList<MmdMaterialDescriptor> materials = descriptor.materials
                ?? throw new InvalidOperationException("Rendering descriptor materials are required for material morph evaluation.");
            IReadOnlyList<MmdMorphDescriptorBuilder.MmdMaterialMorphDescriptor> materialMorphs = descriptor.materialMorphs
                ?? throw new InvalidOperationException("Rendering descriptor material morphs are required for material morph evaluation.");
            IReadOnlyList<MmdMaterialDescriptor> modifiedDescriptors = MmdMaterialMorphEvaluator.ApplyMaterialMorphs(
                materials,
                materialMorphs,
                resolvedWeights);

            for (int i = 0; i < instance.Materials.Length && i < modifiedDescriptors.Count; i++)
            {
                MmdMaterialDescriptor modified = modifiedDescriptors[i];
                Material material = instance.Materials[i];
                if (material == null)
                {
                    continue;
                }

                // Diffuse color with alpha.
                Color diffuse = ToUnityColor(modified.diffuseColor, modified.alpha, Color.white);
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", diffuse);
                }

                // Ambient color.
                Color ambient = ToUnityColor(modified.ambientColor, 1.0f, new Color(0.25f, 0.25f, 0.25f, 1.0f));
                if (material.HasProperty("_AmbientColor"))
                {
                    material.SetColor("_AmbientColor", ambient);
                }

                // Edge color with alpha.
                float edgeAlpha = modified.edgeColor != null && modified.edgeColor.Length > 3
                    ? modified.edgeColor[3]
                    : 1.0f;
                Color edge = ToUnityColor(modified.edgeColor, edgeAlpha, Color.black);
                if (material.HasProperty("_OutlineColor"))
                {
                    material.SetColor("_OutlineColor", edge);
                }

                // Edge width matches initial creation: the raw PMX edgeSize is the screen-space
                // pixel width consumed by the outline shader (no object-space scale). A material
                // with the draw-edge flag off collapses to zero so no silhouette ring appears.
                if (material.HasProperty("_OutlineWidth"))
                {
                    material.SetFloat("_OutlineWidth", modified.drawEdgeFlag ? modified.edgeSize : 0.0f);
                }

                // Apply alpha to _BaseColor and _Color if the diffuse alpha changed.
                if (material.HasProperty("_BaseColor"))
                {
                    Color c = material.GetColor("_BaseColor");
                    c.a = modified.alpha;
                    material.SetColor("_BaseColor", c);
                }

                if (material.HasProperty("_Color"))
                {
                    Color c = material.GetColor("_Color");
                    c.a = modified.alpha;
                    material.SetColor("_Color", c);
                }

                if (material.HasProperty("_Alpha"))
                {
                    material.SetFloat("_Alpha", modified.alpha);
                }
            }
        }

        private static Color ToUnityColor(float[]? values, float alpha, Color fallback)
        {
            if (values == null)
            {
                return fallback;
            }

            float r = values.Length > 0 && float.IsFinite(values[0]) ? Mathf.Clamp01(values[0]) : fallback.r;
            float g = values.Length > 1 && float.IsFinite(values[1]) ? Mathf.Clamp01(values[1]) : fallback.g;
            float b = values.Length > 2 && float.IsFinite(values[2]) ? Mathf.Clamp01(values[2]) : fallback.b;
            float a = float.IsFinite(alpha) ? Mathf.Clamp01(alpha) : fallback.a;
            return new Color(r, g, b, a);
        }

        private static Dictionary<string, float> BuildMorphWeightMap(MmdEvaluatedFrame frame)
        {
            var result = new Dictionary<string, float>(frame.morphs.Count, StringComparer.Ordinal);
            for (int i = 0; i < frame.morphs.Count; i++)
            {
                MmdEvaluatedMorphWeight morph = frame.morphs[i];
                if (morph == null)
                {
                    throw new ArgumentException($"Evaluated morph entry {i} must not be null.", nameof(frame));
                }

                if (string.IsNullOrWhiteSpace(morph.name))
                {
                    throw new ArgumentException($"Evaluated morph entry {i} name is required.", nameof(frame));
                }

                if (result.ContainsKey(morph.name))
                {
                    throw new ArgumentException($"Duplicate evaluated morph weight: {morph.name}", nameof(frame));
                }

                result[morph.name] = morph.weight;
            }

            return result;
        }

        private static bool HasTextureUvMorphs(IReadOnlyList<MmdMorphDescriptorBuilder.MmdUvMorphDescriptor>? uvMorphs)
        {
            if (uvMorphs == null || uvMorphs.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < uvMorphs.Count; i++)
            {
                if (string.Equals(uvMorphs[i].morphType, "texture", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountNonZeroWeights(IReadOnlyDictionary<string, float> weights)
        {
            int count = 0;
            foreach (float weight in weights.Values)
            {
                if (weight != 0.0f)
                {
                    count++;
                }
            }

            return count;
        }

        private static Bounds CalculateBlendShapeMorphedBounds(
            MmdRenderingDescriptor descriptor,
            IReadOnlyDictionary<string, float> resolvedWeights,
            float importScale)
        {
            float scale = (float.IsFinite(importScale) && importScale > 0.0f) ? importScale : 1.0f;
            IReadOnlyList<MmdMeshVertexDescriptor>? vertices = descriptor.vertices;
            if (vertices == null || vertices.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            IReadOnlyList<MmdVertexMorphDescriptor> vertexMorphs = descriptor.vertexMorphs
                ?? throw new InvalidOperationException("Rendering descriptor vertex morphs are required for BlendShape bounds evaluation.");
            var vertexSlotsByIndex = new Dictionary<int, int>(vertices.Count);
            var positions = new Vector3[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                MmdMeshVertexDescriptor vertex = vertices[i];
                vertexSlotsByIndex[vertex.vertexIndex] = i;
                positions[i] = ToUnityPosition(ValidateMorphedVector3(vertex.position, i, "position")) * scale;
            }

            foreach (MmdVertexMorphDescriptor morph in vertexMorphs)
            {
                if (string.IsNullOrWhiteSpace(morph.morphName))
                {
                    continue;
                }

                float resolvedWeight = resolvedWeights.TryGetValue(morph.morphName, out float weight) ? weight : 0.0f;
                if (resolvedWeight == 0.0f)
                {
                    continue;
                }

                if (!IsFinite(resolvedWeight))
                {
                    throw new InvalidOperationException($"BlendShape morph weight must be finite: {morph.morphName}");
                }

                foreach (MmdVertexMorphOffsetDescriptor offset in morph.offsets)
                {
                    if (!vertexSlotsByIndex.TryGetValue(offset.vertexIndex, out int slot))
                    {
                        continue;
                    }

                    Vector3 delta = ToUnityPosition(ValidateMorphedVector3(offset.positionDelta, offset.vertexIndex, "positionDelta")) * scale;
                    positions[slot] += delta * resolvedWeight;
                }
            }

            var bounds = new Bounds(positions[0], Vector3.zero);
            for (int i = 1; i < positions.Length; i++)
            {
                bounds.Encapsulate(positions[i]);
            }

            return bounds;
        }

        private static double ToMilliseconds(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }

        private static float[] ValidateVector3(float[] values, int boneIndex, string field)
        {
            if (values == null || values.Length < 3)
            {
                throw new ArgumentException($"Evaluated bone {boneIndex} {field} must contain at least 3 values.");
            }

            if (!IsFinite(values[0]) || !IsFinite(values[1]) || !IsFinite(values[2]))
            {
                throw new ArgumentException($"Evaluated bone {boneIndex} {field} contains a non-finite value.");
            }

            return values;
        }

        private static float[] ValidateMorphedVector3(float[] values, int vertexIndex, string field)
        {
            if (values == null || values.Length < 3)
            {
                throw new ArgumentException($"Morphed vertex {vertexIndex} {field} must contain at least 3 values.");
            }

            if (!IsFinite(values[0]) || !IsFinite(values[1]) || !IsFinite(values[2]))
            {
                throw new ArgumentException($"Morphed vertex {vertexIndex} {field} contains a non-finite value.");
            }

            return values;
        }

        private static float[] ValidateMorphedVector2(float[] values, int vertexIndex, string field)
        {
            if (values == null || values.Length < 2)
            {
                throw new ArgumentException($"Morphed vertex {vertexIndex} {field} must contain at least 2 values.");
            }

            if (!IsFinite(values[0]) || !IsFinite(values[1]))
            {
                throw new ArgumentException($"Morphed vertex {vertexIndex} {field} contains a non-finite value.");
            }

            return values;
        }

        private static float[] ValidateQuaternion(float[] values, int boneIndex)
        {
            if (values == null || values.Length < 4)
            {
                throw new ArgumentException($"Evaluated bone {boneIndex} localRotation must contain at least 4 values.");
            }

            if (!IsFinite(values[0]) || !IsFinite(values[1]) || !IsFinite(values[2]) || !IsFinite(values[3]))
            {
                throw new ArgumentException($"Evaluated bone {boneIndex} localRotation contains a non-finite value.");
            }

            return values;
        }

        private static Vector3 ToUnityPosition(float[] position)
        {
            return ToUnityPosition(position, importScale: 1.0f);
        }

        private static Vector3 ToUnityPosition(float[] position, float importScale)
        {
            float scale = (float.IsFinite(importScale) && importScale > 0.0f) ? importScale : 1.0f;
            return new Vector3(-position[0], position[1], -position[2]) * scale;
        }

        private static Quaternion ToUnityRotation(float[] rotation)
        {
            return new Quaternion(-rotation[0], rotation[1], -rotation[2], rotation[3]);
        }

        private static Vector3 ToUnityScale(float[] scale)
        {
            return new Vector3(scale[0], scale[1], scale[2]);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    internal sealed class MmdUnityMorphApplyTimingSummary
    {
        public bool hasAnyMorph;
        public bool hasVertexMorphs;
        public bool hasTextureUvMorphs;
        public bool hasMaterialMorphs;
        public bool meshUploadRequired;
        public bool blendShapePathUsed;
        public int vertexCount;
        public int morphWeightCount;
        public int resolvedWeightCount;
        public int nonZeroMorphWeightCount;
        public int nonZeroResolvedWeightCount;
        public double totalMs;
        public double buildWeightMapMs;
        public double resolveCompositeWeightsMs;
        public double vertexMorphEvalMs;
        public double textureUvMorphEvalMs;
        public double materialMorphApplyMs;
        public double unityBufferBuildMs;
        public double setBlendShapeWeightMs;
        public double setVerticesMs;
        public double setUvsMs;
        public double recalculateBoundsMs;
        public double localBoundsAssignMs;
        public bool localBoundsAssigned;
        public bool localBoundsSkipped;
    }
}
