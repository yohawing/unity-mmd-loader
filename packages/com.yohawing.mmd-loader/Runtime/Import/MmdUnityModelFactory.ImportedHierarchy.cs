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
            float importScale,
            MmdMaterialOverrideAsset? materialOverride = null)
        {
            return CreateFromImportedHierarchy(
                importedRoot,
                model,
                sourcePath,
                importScale,
                includeSelfShadowTarget: true,
                materialOverride);
        }

        internal static MmdUnityModelInstance CreateFromImportedHierarchy(
            GameObject importedRoot,
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            bool includeSelfShadowTarget,
            MmdMaterialOverrideAsset? materialOverride = null,
            MmdMaterialRenderingTargets[]? materialRenderingTargets = null)
        {
            if (importedRoot == null)
            {
                throw new ArgumentNullException(nameof(importedRoot));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            // Instantiate the imported hierarchy to create a dedicated scene copy. Scene-visibility
            // hideFlags are cleared by CreateFromInstantiatedImportedHierarchy.
            GameObject instanceRoot = UnityEngine.Object.Instantiate(importedRoot);
            instanceRoot.name = importedRoot.name;
            try
            {
                return CreateFromInstantiatedImportedHierarchy(
                    instanceRoot,
                    model,
                    sourcePath,
                    importScale,
                    includeSelfShadowTarget,
                    materialOverride,
                    materialRenderingTargets);
            }
            catch
            {
                // This method owns the copy it instantiated; do not leave it in the scene.
                DestroyGeneratedObject(instanceRoot);
                throw;
            }
        }

        internal static MmdUnityModelInstance CreateFromInstantiatedImportedHierarchy(
            GameObject instanceRoot,
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            bool includeSelfShadowTarget = true,
            MmdMaterialOverrideAsset? materialOverride = null,
            MmdMaterialRenderingTargets[]? materialRenderingTargets = null)
        {
            return CreateFromInstantiatedImportedHierarchy(
                instanceRoot,
                model,
                sourcePath,
                importScale,
                includeSelfShadowTarget,
                materialOverride,
                materialRenderingTargets,
                metadataOnlyDescriptor: false);
        }

        /// <summary>
        /// Scene drag-and-drop entry. The dropped instance is only used for its root, so its
        /// rendering descriptor carries metadata only (no geometry, skinning or morph offsets).
        /// <paramref name="model"/> must already be validated (the PMX asset's synchronous playback
        /// cache validates it) and is only read, never modified.
        /// </summary>
        internal static MmdUnityModelInstance CreateFromInstantiatedImportedHierarchyForSceneDrop(
            GameObject instanceRoot,
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            bool includeSelfShadowTarget,
            MmdMaterialOverrideAsset? materialOverride,
            MmdMaterialRenderingTargets[]? materialRenderingTargets)
        {
            return CreateFromInstantiatedImportedHierarchy(
                instanceRoot,
                model,
                sourcePath,
                importScale,
                includeSelfShadowTarget,
                materialOverride,
                materialRenderingTargets,
                metadataOnlyDescriptor: true);
        }

        private static MmdUnityModelInstance CreateFromInstantiatedImportedHierarchy(
            GameObject instanceRoot,
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            bool includeSelfShadowTarget,
            MmdMaterialOverrideAsset? materialOverride,
            MmdMaterialRenderingTargets[]? materialRenderingTargets,
            bool metadataOnlyDescriptor)
        {
            if (instanceRoot == null)
            {
                throw new ArgumentNullException(nameof(instanceRoot));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            float scale = MmdCoordinateSpace.NormalizeImportScale(importScale);
            ClearNonSceneHideFlags(instanceRoot.transform);

            MmdRenderingDescriptor descriptor;
            if (metadataOnlyDescriptor)
            {
                // The imported hierarchy already owns the mesh (geometry, skinning, vertex-morph
                // blend shapes) and this path never rebuilds it, so build only the metadata instead
                // of the full split descriptor. The scene-drop caller passes a validated model.
                descriptor = BuildRuntimePlaybackRenderingDescriptorFromValidated(model, MmdMaterialPreset.MmdToon);
                MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(materialOverride, descriptor);
            }
            else
            {
                descriptor = BuildRuntimeRenderingDescriptor(model);
                MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(materialOverride, descriptor);
                ValidateDescriptor(descriptor);
            }

            if (model.bones != null && model.bones.Count > 0)
            {
                return CreateFromImportedSkinnedHierarchy(
                    instanceRoot,
                    model,
                    descriptor,
                    sourcePath,
                    scale,
                    includeSelfShadowTarget,
                    materialOverride,
                    materialRenderingTargets);
            }

            return CreateFromImportedStaticHierarchy(
                instanceRoot,
                model,
                descriptor,
                sourcePath,
                scale,
                includeSelfShadowTarget,
                materialOverride,
                materialRenderingTargets);
        }

        private static MmdUnityModelInstance CreateFromImportedSkinnedHierarchy(
            GameObject instanceRoot,
            MmdModelDefinition model,
            MmdRenderingDescriptor descriptor,
            string? sourcePath,
            float scale,
            bool includeSelfShadowTarget,
            MmdMaterialOverrideAsset? materialOverride,
            MmdMaterialRenderingTargets[]? materialRenderingTargets)
        {
            Transform modelRoot = FindModelRoot(instanceRoot.transform);
            SkinnedMeshRenderer renderer = FindImportedRenderer<SkinnedMeshRenderer>(modelRoot, instanceRoot)
                ?? throw new InvalidOperationException("Imported PMX hierarchy must contain a SkinnedMeshRenderer.");

            Mesh sharedMesh = renderer.sharedMesh;
            if (sharedMesh == null)
            {
                throw new InvalidOperationException("Imported PMX SkinnedMeshRenderer has no sharedMesh.");
            }

            Material[] sourceMaterials = renderer.sharedMaterials;
            if (sourceMaterials.Length < descriptor.materials.Count)
            {
                throw new InvalidOperationException(
                    "Imported PMX SkinnedMeshRenderer material slots do not match the PMX material descriptor.");
            }

            // Validate everything that can reject the hierarchy before cloning override materials,
            // so a rejected hierarchy never carries clones.
            Transform[] boneTransforms = renderer.bones;
            IReadOnlyList<MmdBoneDefinition> orderedBones = CreateOrderedBones(model.bones);
            if (boneTransforms == null || boneTransforms.Length != orderedBones.Count)
            {
                throw new InvalidOperationException(
                    "Imported PMX SkinnedMeshRenderer bones do not match the PMX bone descriptor.");
            }

            Material[] materials = CloneMaterialsForOverride(sourceMaterials, materialOverride);
            try
            {
                renderer.sharedMaterials = materials;
                MmdMaterialOverrideApplier.Apply(materialOverride, materials);

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
                    scale,
                    NormalizeMaterialRenderingTargets(materials.Length, materialRenderingTargets));
            }
            catch
            {
                renderer.sharedMaterials = sourceMaterials;
                DestroyMaterialClones(materials, sourceMaterials);
                throw;
            }
        }

        private static MmdUnityModelInstance CreateFromImportedStaticHierarchy(
            GameObject instanceRoot,
            MmdModelDefinition model,
            MmdRenderingDescriptor descriptor,
            string? sourcePath,
            float scale,
            bool includeSelfShadowTarget,
            MmdMaterialOverrideAsset? materialOverride,
            MmdMaterialRenderingTargets[]? materialRenderingTargets)
        {
            Transform modelRoot = FindModelRoot(instanceRoot.transform);
            MeshRenderer meshRenderer = FindImportedRenderer<MeshRenderer>(modelRoot, instanceRoot)
                ?? throw new InvalidOperationException("Imported PMX hierarchy must contain a MeshRenderer.");

            // The mesh drawn by the chosen renderer lives on that renderer's GameObject.
            MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                throw new InvalidOperationException("Imported PMX hierarchy must contain a MeshFilter with a sharedMesh.");
            }

            Mesh sharedMesh = meshFilter.sharedMesh;
            Material[] sourceMaterials = meshRenderer.sharedMaterials;
            if (sourceMaterials.Length < descriptor.materials.Count)
            {
                throw new InvalidOperationException(
                    "Imported PMX MeshRenderer material slots do not match the PMX material descriptor.");
            }

            Material[] materials = CloneMaterialsForOverride(sourceMaterials, materialOverride);
            try
            {
                meshRenderer.sharedMaterials = materials;
                MmdMaterialOverrideApplier.Apply(materialOverride, materials);

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
                    scale,
                    NormalizeMaterialRenderingTargets(materials.Length, materialRenderingTargets));
            }
            catch
            {
                meshRenderer.sharedMaterials = sourceMaterials;
                DestroyMaterialClones(materials, sourceMaterials);
                throw;
            }
        }

        // GetComponent returns Unity's fake-null placeholder in the Editor, which the C# `??`
        // operator treats as non-null; compare through UnityEngine.Object equality instead.
        private static T? FindImportedRenderer<T>(Transform modelRoot, GameObject instanceRoot)
            where T : Renderer
        {
            T renderer = modelRoot.GetComponent<T>();
            if (renderer != null)
            {
                return renderer;
            }

            renderer = instanceRoot.GetComponentInChildren<T>(includeInactive: true);
            return renderer != null ? renderer : null;
        }

        private static void DestroyMaterialClones(Material[] materials, Material[] sourceMaterials)
        {
            if (ReferenceEquals(materials, sourceMaterials))
            {
                return;
            }

            foreach (Material material in materials)
            {
                DestroyGeneratedObject(material);
            }
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
