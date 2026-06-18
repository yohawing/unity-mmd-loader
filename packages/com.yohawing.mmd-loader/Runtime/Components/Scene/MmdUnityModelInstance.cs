#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Mmd.Rendering;

namespace Mmd.UnityIntegration
{
    public sealed class MmdUnityModelInstance
    {
        internal MmdUnityModelInstance(
            GameObject root,
            Mesh mesh,
            Material[] materials,
            MmdRenderingDescriptor renderingDescriptor,
            Transform[] boneTransforms,
            MmdUnityPhysicsBody[] physicsBodies,
            MeshRenderer? meshRenderer,
            SkinnedMeshRenderer? skinnedMeshRenderer,
            MmdUnityModelSourceContext? sourceContext,
            Texture2D[] ownedTextures,
            MmdTextureBindingDiagnostics textureDiagnostics,
            MmdShaderBindingDiagnostics shaderDiagnostics,
            float importScale = 1.0f)
        {
            Root = root;
            Mesh = mesh;
            Materials = materials;
            RenderingDescriptor = renderingDescriptor;
            BoneTransforms = boneTransforms;
            PhysicsBodies = physicsBodies;
            MeshRenderer = meshRenderer;
            SkinnedMeshRenderer = skinnedMeshRenderer;
            SourceContext = sourceContext;
            OwnedTextures = ownedTextures;
            TextureDiagnostics = textureDiagnostics;
            ShaderDiagnostics = shaderDiagnostics;
            MaterialBindingDiagnostics = BuildMaterialBindingDiagnostics(renderingDescriptor, materials);
            ImportScale = (float.IsFinite(importScale) && importScale > 0.0f) ? importScale : 1.0f;
            BindLocalPositions = new Vector3[boneTransforms.Length];
            BindLocalRotations = new Quaternion[boneTransforms.Length];
            for (int i = 0; i < boneTransforms.Length; i++)
            {
                BindLocalPositions[i] = boneTransforms[i].localPosition;
                BindLocalRotations[i] = boneTransforms[i].localRotation;
            }

            VertexCount = mesh.vertexCount;
            IndexCount = mesh.GetIndexCountAllSubmeshes();
            SubmeshCount = mesh.subMeshCount;
            LoadedDiffuseTextureCount = textureDiagnostics.LoadedDiffuseTextureCount;
            LoadedSphereTextureCount = textureDiagnostics.LoadedSphereTextureCount;
            LoadedToonTextureCount = textureDiagnostics.LoadedToonTextureCount;
            MissingTextureReferenceCount = textureDiagnostics.MissingTextureReferenceCount;
            UnsupportedTextureReferenceCount = textureDiagnostics.UnsupportedTextureReferenceCount;
            SkippedSphereTextureReferenceCount = textureDiagnostics.SkippedSphereTextureReferenceCount;
            SkippedToonTextureReferenceCount = textureDiagnostics.SkippedToonTextureReferenceCount;
            SkippedTextureReferenceCount = textureDiagnostics.SkippedTextureReferenceCount;

            // NOTE: MaterialBindingDiagnostics snapshot is taken at construction from the provided
            // materials. For importer post-bind texture assignment (BindProjectTextureAssetsToMaterials
            // mutates the sub-asset materials after cache instance creation), call
            // RefreshMaterialBindingDiagnostics() to update the snapshot for parity diagnostics.

            var blendShapeMap = new Dictionary<string, int>(mesh.blendShapeCount, StringComparer.Ordinal);
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string blendShapeName = mesh.GetBlendShapeName(i);
                if (!blendShapeMap.ContainsKey(blendShapeName))
                {
                    blendShapeMap[blendShapeName] = i;
                }
            }

            BlendShapeIndexMap = blendShapeMap;
            VertexMorphBlendShapes = BuildVertexMorphBlendShapeBindings(renderingDescriptor, mesh);
            LastBlendShapeBoundsWeights = CreateUninitializedBlendShapeWeights(VertexMorphBlendShapes.Count);
        }

        public GameObject Root { get; }

        public Mesh Mesh { get; }

        public Material[] Materials { get; }

        public MmdRenderingDescriptor RenderingDescriptor { get; }

        public Transform[] BoneTransforms { get; }

        public MmdUnityPhysicsBody[] PhysicsBodies { get; }

        public Vector3[] BindLocalPositions { get; }

        public Quaternion[] BindLocalRotations { get; }

        public MeshRenderer? MeshRenderer { get; }

        public SkinnedMeshRenderer? SkinnedMeshRenderer { get; }

        public MmdUnityModelSourceContext? SourceContext { get; }

        public float ImportScale { get; }

        public Texture2D[] OwnedTextures { get; }

        public MmdTextureBindingDiagnostics TextureDiagnostics { get; }

        public MmdShaderBindingDiagnostics ShaderDiagnostics { get; }

        public MmdUnityMaterialBindingDiagnostic[] MaterialBindingDiagnostics { get; private set; }

        public IReadOnlyDictionary<string, int> BlendShapeIndexMap { get; }

        public IReadOnlyList<MmdUnityVertexMorphBlendShapeBinding> VertexMorphBlendShapes { get; }

        internal float[] LastBlendShapeBoundsWeights { get; }

        public int VertexCount { get; }

        public int IndexCount { get; }

        public int SubmeshCount { get; }

        public int LoadedDiffuseTextureCount { get; }

        public int LoadedSphereTextureCount { get; }

        public int LoadedToonTextureCount { get; }

        public int MissingTextureReferenceCount { get; }

        public int UnsupportedTextureReferenceCount { get; }

        public int SkippedSphereTextureReferenceCount { get; }

        public int SkippedToonTextureReferenceCount { get; }

        public int SkippedTextureReferenceCount { get; }

        private static MmdUnityMaterialBindingDiagnostic[] BuildMaterialBindingDiagnostics(
            MmdRenderingDescriptor descriptor,
            Material[] materials)
        {
            var diagnostics = new MmdUnityMaterialBindingDiagnostic[descriptor.materials.Count];
            for (int i = 0; i < diagnostics.Length; i++)
            {
                MmdMaterialDescriptor materialDescriptor = descriptor.materials[i];
                MmdUrpMaterialBindingDescriptor? binding = FindBinding(descriptor, materialDescriptor.materialIndex);
                MmdSubmeshDescriptor? submesh = FindSubmesh(descriptor, materialDescriptor.materialIndex);
                Material? material = i < materials.Length ? materials[i] : null;
                string transparencyMode = ResolveTransparencyMode(material, materialDescriptor);
                bool transparent = transparencyMode != "opaque";
                int transparentOrder = transparencyMode == "alphaBlend" && material != null && material.renderQueue >= 3000
                    ? material.renderQueue - 3000
                    : -1;
                diagnostics[i] = new MmdUnityMaterialBindingDiagnostic
                {
                    materialIndex = materialDescriptor.materialIndex,
                    materialSlot = i,
                    submeshIndex = submesh?.submeshIndex ?? -1,
                    name = materialDescriptor.name ?? string.Empty,
                    shaderName = binding?.shaderName ?? string.Empty,
                    resolvedShaderName = material != null && material.shader != null ? material.shader.name : string.Empty,
                    baseMapTexture = binding?.baseMapTexture ?? string.Empty,
                    baseMapBound = HasBoundTexture(material, "_BaseMap"),
                    mainTexBound = HasBoundTexture(material, "_MainTex"),
                    sphereMapBound = HasBoundTexture(material, "_SphereMap"),
                    toonMapBound = HasBoundTexture(material, "_ToonMap"),
                    diffuseColor = CopyColor(materialDescriptor.diffuseColor, 3, new[] { 1.0f, 1.0f, 1.0f }),
                    ambientColor = CopyColor(materialDescriptor.ambientColor, 3, new[] { 0.25f, 0.25f, 0.25f }),
                    edgeColor = CopyColor(materialDescriptor.edgeColor, 4, new[] { 0.0f, 0.0f, 0.0f, 1.0f }),
                    baseColorProperty = ReadColor(material, "_BaseColor"),
                    colorProperty = ReadColor(material, "_Color"),
                    ambientColorProperty = ReadColor(material, "_AmbientColor"),
                    outlineColorProperty = ReadColor(material, "_OutlineColor"),
                    alpha = materialDescriptor.alpha,
                    edgeSize = materialDescriptor.edgeSize,
                    isTransparent = transparent,
                    transparencyMode = transparencyMode,
                    renderOrderBucket = transparencyMode,
                    materialRenderOrder = i,
                    outlineRenderOrder = descriptor.materials.Count + i,
                    transparentOrder = transparentOrder,
                    renderQueueOffset = transparentOrder,
                    sortingPriority = transparentOrder,
                    transparentPolicy = ResolveTransparentPolicy(transparencyMode),
                    renderQueue = material != null ? material.renderQueue : -1,
                    zWrite = ReadFloat(material, "_ZWrite"),
                    srcBlend = ReadFloat(material, "_SrcBlend"),
                    dstBlend = ReadFloat(material, "_DstBlend"),
                    alphaClipThreshold = ReadFloat(material, "_AlphaClipThreshold"),
                    outlineWidth = ReadFloat(material, "_OutlineWidth"),
                    cull = ReadFloat(material, "_Cull"),
                    cullingPolicy = ResolveCullingPolicy(binding, materialDescriptor),
                    sphereTexture = binding?.sphereTexture ?? string.Empty,
                    toonTexture = binding?.toonTexture ?? string.Empty,
                    sphereTextureModeHint = binding?.sphereTextureModeHint ?? string.Empty,
                    toonTextureSourceHint = binding?.toonTextureSourceHint ?? string.Empty
                };
            }

            return diagnostics;
        }

        /// <summary>
        /// Rebuilds MaterialBindingDiagnostics from current Materials state.
        /// Used after importer post-creation texture bind (BindProjectTextureAssetsToMaterials mutates
        /// the Material sub-asset slots) so that diagnostics reflect _BaseMapBound
        /// side effects and bound state for parity checks. Small local design to address pre/post-bind
        /// snapshot mismatch without changing creation contract for runtime paths.
        /// </summary>
        internal void RefreshMaterialBindingDiagnostics()
        {
            MaterialBindingDiagnostics = BuildMaterialBindingDiagnostics(RenderingDescriptor, Materials);
        }

        private static string ResolveTransparencyMode(Material? material, MmdMaterialDescriptor materialDescriptor)
        {
            if (materialDescriptor.alpha < 1.0f || (material != null && material.renderQueue >= 3000))
            {
                return "alphaBlend";
            }

            float alphaClipThreshold = ReadFloat(material, "_AlphaClipThreshold");
            return alphaClipThreshold > 0.0f ? "alphaTest" : "opaque";
        }

        private static string ResolveTransparentPolicy(string transparencyMode)
        {
            return transparencyMode switch
            {
                "alphaBlend" => "mmd-material-order-queue-depth-write",
                "alphaTest" => "mmd-material-alpha-test-depth-write",
                _ => "opaque-geometry-queue"
            };
        }

        private static string ResolveCullingPolicy(
            MmdUrpMaterialBindingDescriptor? binding,
            MmdMaterialDescriptor materialDescriptor)
        {
            string value = binding?.cullingPolicy ?? materialDescriptor.cullingPolicy;
            if (string.Equals(value, "double-sided", StringComparison.Ordinal) ||
                string.Equals(value, "backface-culling", StringComparison.Ordinal))
            {
                return value;
            }

            return "unknown";
        }

        private static MmdUrpMaterialBindingDescriptor? FindBinding(
            MmdRenderingDescriptor descriptor,
            int materialIndex)
        {
            foreach (MmdUrpMaterialBindingDescriptor binding in descriptor.urpMaterialBindings)
            {
                if (binding.materialIndex == materialIndex)
                {
                    return binding;
                }
            }

            return null;
        }

        private static MmdSubmeshDescriptor? FindSubmesh(
            MmdRenderingDescriptor descriptor,
            int materialIndex)
        {
            foreach (MmdSubmeshDescriptor submesh in descriptor.submeshes)
            {
                if (submesh.materialIndex == materialIndex)
                {
                    return submesh;
                }
            }

            return null;
        }

        private static bool HasBoundTexture(Material? material, string propertyName)
        {
            return material != null && material.HasProperty(propertyName) && material.GetTexture(propertyName) != null;
        }

        private static float ReadFloat(Material? material, string propertyName)
        {
            return material != null && material.HasProperty(propertyName) ? material.GetFloat(propertyName) : -1.0f;
        }

        private static float[] ReadColor(Material? material, string propertyName)
        {
            if (material == null || !material.HasProperty(propertyName))
            {
                return Array.Empty<float>();
            }

            Color color = material.GetColor(propertyName);
            return new[] { color.r, color.g, color.b, color.a };
        }

        private static float[] CopyColor(float[]? values, int length, float[] fallback)
        {
            var result = new float[length];
            for (int i = 0; i < length; i++)
            {
                float fallbackValue = i < fallback.Length ? fallback[i] : 0.0f;
                result[i] = values != null && i < values.Length && IsFinite(values[i])
                    ? values[i]
                    : fallbackValue;
            }

            return result;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static IReadOnlyList<MmdUnityVertexMorphBlendShapeBinding> BuildVertexMorphBlendShapeBindings(
            MmdRenderingDescriptor descriptor,
            Mesh mesh)
        {
            var result = new List<MmdUnityVertexMorphBlendShapeBinding>(descriptor.vertexMorphs.Count);
            IReadOnlyDictionary<string, int> morphNameCounts = MmdUnityBlendShapeNames.CountMorphNames(descriptor.vertexMorphs);
            foreach (MmdVertexMorphDescriptor morph in descriptor.vertexMorphs)
            {
                if (string.IsNullOrWhiteSpace(morph.morphName))
                {
                    continue;
                }

                string blendShapeName = MmdUnityBlendShapeNames.ResolveVertexMorphBlendShapeName(morph, morphNameCounts);
                int blendShapeIndex = mesh.GetBlendShapeIndex(blendShapeName);
                if (blendShapeIndex < 0)
                {
                    continue;
                }

                result.Add(new MmdUnityVertexMorphBlendShapeBinding(
                    morph.morphIndex,
                    morph.morphName,
                    blendShapeName,
                    blendShapeIndex));
            }

            return result;
        }

        private static float[] CreateUninitializedBlendShapeWeights(int count)
        {
            var result = new float[count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = float.NaN;
            }

            return result;
        }
    }

    public sealed class MmdUnityVertexMorphBlendShapeBinding
    {
        internal MmdUnityVertexMorphBlendShapeBinding(
            int morphIndex,
            string morphName,
            string blendShapeName,
            int blendShapeIndex)
        {
            MorphIndex = morphIndex;
            MorphName = morphName;
            BlendShapeName = blendShapeName;
            BlendShapeIndex = blendShapeIndex;
        }

        public int MorphIndex { get; }

        public string MorphName { get; }

        public string BlendShapeName { get; }

        public int BlendShapeIndex { get; }
    }

    internal static class MmdUnityBlendShapeNames
    {
        public static IReadOnlyDictionary<string, int> CountMorphNames(IReadOnlyList<MmdVertexMorphDescriptor> morphs)
        {
            var result = new Dictionary<string, int>(morphs.Count, StringComparer.Ordinal);
            foreach (MmdVertexMorphDescriptor morph in morphs)
            {
                if (string.IsNullOrWhiteSpace(morph.morphName))
                {
                    continue;
                }

                result[morph.morphName] = result.TryGetValue(morph.morphName, out int count) ? count + 1 : 1;
            }

            return result;
        }

        public static string ResolveVertexMorphBlendShapeName(
            MmdVertexMorphDescriptor morph,
            IReadOnlyDictionary<string, int> morphNameCounts)
        {
            if (!morphNameCounts.TryGetValue(morph.morphName, out int count) || count <= 1)
            {
                return morph.morphName;
            }

            return $"{morph.morphIndex}:{morph.morphName}";
        }
    }

    [System.Serializable]
    public sealed class MmdUnityMaterialBindingDiagnostic
    {
        public int materialIndex;
        public int materialSlot;
        public int submeshIndex;
        public string name = string.Empty;
        public string shaderName = string.Empty;
        public string resolvedShaderName = string.Empty;
        public string baseMapTexture = string.Empty;
        public bool baseMapBound;
        public bool mainTexBound;
        public bool sphereMapBound;
        public bool toonMapBound;
        public float[] diffuseColor = Array.Empty<float>();
        public float[] ambientColor = Array.Empty<float>();
        public float[] edgeColor = Array.Empty<float>();
        public float[] baseColorProperty = Array.Empty<float>();
        public float[] colorProperty = Array.Empty<float>();
        public float[] ambientColorProperty = Array.Empty<float>();
        public float[] outlineColorProperty = Array.Empty<float>();
        public float alpha = 1.0f;
        public float edgeSize;
        public bool isTransparent;
        public string transparencyMode = string.Empty;
        public string renderOrderBucket = string.Empty;
        public int materialRenderOrder = -1;
        public int outlineRenderOrder = -1;
        public int transparentOrder = -1;
        public int renderQueueOffset = -1;
        public int sortingPriority = -1;
        public string transparentPolicy = string.Empty;
        public int renderQueue;
        public float zWrite = -1.0f;
        public float srcBlend = -1.0f;
        public float dstBlend = -1.0f;
        public float alphaClipThreshold = -1.0f;
        public float outlineWidth = -1.0f;
        public float cull = -1.0f;
        public string cullingPolicy = string.Empty;
        public string sphereTexture = string.Empty;
        public string toonTexture = string.Empty;
        public string sphereTextureModeHint = string.Empty;
        public string toonTextureSourceHint = string.Empty;
    }

    internal static class MmdMeshIndexCountExtensions
    {
        public static int GetIndexCountAllSubmeshes(this Mesh mesh)
        {
            int count = 0;
            for (int submeshIndex = 0; submeshIndex < mesh.subMeshCount; submeshIndex++)
            {
                count += (int)mesh.GetIndexCount(submeshIndex);
            }

            return count;
        }
    }
}
