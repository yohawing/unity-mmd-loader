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
        public static MmdUnityModelInstance CreateFromImportedHierarchy(
            GameObject importedRoot,
            MmdModelDefinition model,
            string? sourcePath,
            float importScale)
        {
            return CreateFromImportedHierarchy(
                importedRoot,
                model,
                sourcePath,
                importScale,
                includeSelfShadowTarget: true);
        }

        internal static MmdUnityModelInstance CreateFromImportedHierarchy(
            GameObject importedRoot,
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            bool includeSelfShadowTarget)
        {
            if (importedRoot == null)
            {
                throw new ArgumentNullException(nameof(importedRoot));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            float scale = NormalizeImportScale(importScale);

            // Instantiate the imported hierarchy to create a dedicated scene copy.
            GameObject instanceRoot = UnityEngine.Object.Instantiate(importedRoot);
            instanceRoot.name = importedRoot.name;

            // Clear non-scene hideFlags on the instantiated hierarchy to ensure scene-visibility.
            ClearNonSceneHideFlags(instanceRoot.transform);

            return CreateFromInstantiatedImportedHierarchy(
                instanceRoot,
                model,
                sourcePath,
                importScale,
                includeSelfShadowTarget);
        }

        internal static MmdUnityModelInstance CreateFromInstantiatedImportedHierarchy(
            GameObject instanceRoot,
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            bool includeSelfShadowTarget = true)
        {
            if (instanceRoot == null)
            {
                throw new ArgumentNullException(nameof(instanceRoot));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            float scale = NormalizeImportScale(importScale);
            ClearNonSceneHideFlags(instanceRoot.transform);

            MmdRenderingDescriptor descriptor = BuildRuntimeRenderingDescriptor(model);
            ValidateDescriptor(descriptor);

            if (model.bones != null && model.bones.Count > 0)
            {
                return CreateFromImportedSkinnedHierarchy(
                    instanceRoot,
                    model,
                    descriptor,
                    sourcePath,
                    scale,
                    includeSelfShadowTarget);
            }

            return CreateFromImportedStaticHierarchy(
                instanceRoot,
                model,
                descriptor,
                sourcePath,
                scale,
                includeSelfShadowTarget);
        }

        private static MmdUnityModelInstance CreateFromImportedSkinnedHierarchy(
            GameObject instanceRoot,
            MmdModelDefinition model,
            MmdRenderingDescriptor descriptor,
            string? sourcePath,
            float scale,
            bool includeSelfShadowTarget)
        {
            Transform modelRoot = FindModelRoot(instanceRoot.transform);
            SkinnedMeshRenderer renderer = modelRoot.GetComponent<SkinnedMeshRenderer>()
                ?? instanceRoot.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true)
                ?? throw new InvalidOperationException("Imported PMX hierarchy must contain a SkinnedMeshRenderer.");

            Mesh sharedMesh = renderer.sharedMesh;
            if (sharedMesh == null)
            {
                throw new InvalidOperationException("Imported PMX SkinnedMeshRenderer has no sharedMesh.");
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials.Length < descriptor.materials.Count)
            {
                throw new InvalidOperationException(
                    "Imported PMX SkinnedMeshRenderer material slots do not match the PMX material descriptor.");
            }

            Transform[] boneTransforms = renderer.bones;
            IReadOnlyList<MmdBoneDefinition> orderedBones = CreateOrderedBones(model.bones);
            if (boneTransforms == null || boneTransforms.Length != orderedBones.Count)
            {
                throw new InvalidOperationException(
                    "Imported PMX SkinnedMeshRenderer bones do not match the PMX bone descriptor.");
            }

            ResetExistingBoneTransformsToBindPose(orderedBones, boneTransforms, scale);
            renderer.rootBone = boneTransforms.Length > 0 ? boneTransforms[0] : modelRoot;

            MmdUnityPhysicsBody[] physicsBodies = instanceRoot.GetComponentsInChildren<MmdUnityPhysicsBody>(includeInactive: true);
            MmdShaderBindingDiagnostics shaderDiagnostics = MmdUnityMaterialBuilder.BuildExistingShaderDiagnostics(renderer);
            ApplySelfShadowTargetPolicy(instanceRoot, modelRoot, includeSelfShadowTarget);

            return new MmdUnityModelInstance(
                instanceRoot,
                sharedMesh,
                materials,
                descriptor,
                boneTransforms,
                physicsBodies,
                meshRenderer: null,
                renderer,
                MmdUnityModelSourceContext.FromOptionalPath(sourcePath),
                Array.Empty<Texture2D>(),
                new MmdTextureBindingDiagnostics(),
                shaderDiagnostics,
                scale);
        }

        private static MmdUnityModelInstance CreateFromImportedStaticHierarchy(
            GameObject instanceRoot,
            MmdModelDefinition model,
            MmdRenderingDescriptor descriptor,
            string? sourcePath,
            float scale,
            bool includeSelfShadowTarget)
        {
            Transform modelRoot = FindModelRoot(instanceRoot.transform);
            MeshRenderer meshRenderer = modelRoot.GetComponent<MeshRenderer>()
                ?? instanceRoot.GetComponentInChildren<MeshRenderer>(includeInactive: true)
                ?? throw new InvalidOperationException("Imported PMX hierarchy must contain a MeshRenderer.");

            MeshFilter meshFilter = modelRoot.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                throw new InvalidOperationException("Imported PMX hierarchy must contain a MeshFilter with a sharedMesh.");
            }

            Mesh sharedMesh = meshFilter.sharedMesh;
            Material[] materials = meshRenderer.sharedMaterials;
            if (materials.Length < descriptor.materials.Count)
            {
                throw new InvalidOperationException(
                    "Imported PMX MeshRenderer material slots do not match the PMX material descriptor.");
            }

            MmdShaderBindingDiagnostics shaderDiagnostics = BuildMeshRendererShaderDiagnostics(meshRenderer);
            ApplySelfShadowTargetPolicy(instanceRoot, modelRoot, includeSelfShadowTarget);

            return new MmdUnityModelInstance(
                instanceRoot,
                sharedMesh,
                materials,
                descriptor,
                Array.Empty<Transform>(),
                Array.Empty<MmdUnityPhysicsBody>(),
                meshRenderer,
                skinnedMeshRenderer: null,
                MmdUnityModelSourceContext.FromOptionalPath(sourcePath),
                Array.Empty<Texture2D>(),
                new MmdTextureBindingDiagnostics(),
                shaderDiagnostics,
                scale);
        }

        private static MmdShaderBindingDiagnostics BuildMeshRendererShaderDiagnostics(MeshRenderer renderer)
        {
            string resolvedShaderName = string.Empty;
            Material material = renderer.sharedMaterial;
            if (material != null && material.shader != null)
            {
                resolvedShaderName = material.shader.name;
            }

            return new MmdShaderBindingDiagnostics
            {
                resolvedShaderName = resolvedShaderName,
                fallbackCandidates = Array.Empty<string>()
            };
        }

        private static void ClearNonSceneHideFlags(Transform root)
        {
            root.gameObject.hideFlags = HideFlags.None;
            root.hideFlags = HideFlags.None;
            foreach (Transform child in root)
            {
                ClearNonSceneHideFlags(child);
            }
        }
    }
}
