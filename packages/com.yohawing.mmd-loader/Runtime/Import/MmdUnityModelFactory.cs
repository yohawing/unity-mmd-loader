#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Mmd.Parser;
using Mmd.Rendering;

namespace Mmd.UnityIntegration
{
    public static partial class MmdUnityModelFactory
    {
        private const string DefaultModelName = "MMD Model";

        public static MmdUnityModelInstance CreateStaticModel(MmdModelDefinition model)
        {
            return CreateStaticModel(model, sourcePath: null);
        }

        public static MmdUnityModelInstance CreateStaticModel(MmdModelDefinition model, string? sourcePath)
        {
            return CreateStaticModel(model, sourcePath, importScale: 1.0f);
        }

        public static MmdUnityModelInstance CreateStaticModel(MmdModelDefinition model, string? sourcePath, float importScale)
        {
            return CreateStaticModel(model, sourcePath, importScale, MmdMaterialPreset.MmdToon);
        }

        public static MmdUnityModelInstance CreateStaticModel(
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            MmdMaterialPreset preset)
        {
            return CreateStaticModel(model, sourcePath, importScale, preset, materialOverride: null);
        }

        public static MmdUnityModelInstance CreateStaticModel(
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            MmdMaterialPreset preset,
            MmdMaterialOverrideAsset? materialOverride)
        {
            return CreateStaticModel(model, sourcePath, importScale, preset, includeSelfShadowTarget: true, materialOverride: materialOverride);
        }

        internal static MmdUnityModelInstance CreateStaticModel(
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            bool includeSelfShadowTarget)
        {
            return CreateStaticModel(model, sourcePath, importScale, MmdMaterialPreset.MmdToon, includeSelfShadowTarget);
        }

        internal static MmdUnityModelInstance CreateStaticModel(
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            MmdMaterialPreset preset,
            bool includeSelfShadowTarget)
        {
            return CreateStaticModel(model, sourcePath, importScale, preset, includeSelfShadowTarget, materialOverride: null);
        }

        internal static MmdUnityModelInstance CreateStaticModel(
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            MmdMaterialPreset preset,
            bool includeSelfShadowTarget,
            MmdMaterialOverrideAsset? materialOverride)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            float scale = NormalizeImportScale(importScale);
            return CreateStaticModel(
                BuildRuntimeRenderingDescriptor(model, preset),
                model.name,
                model.bones,
                model.physics,
                MmdUnityModelSourceContext.FromOptionalPath(sourcePath),
                scale,
                includeSelfShadowTarget,
                materialOverride);
        }

        public static MmdUnityModelInstance CreateStaticModel(MmdRenderingDescriptor descriptor, string modelName)
        {
            return CreateStaticModel(
                descriptor,
                modelName,
                bones: null,
                physics: null,
                sourceContext: null,
                importScale: 1.0f,
                includeSelfShadowTarget: true);
        }

        public static MmdUnityModelInstance CreateSkinnedModel(MmdModelDefinition model)
        {
            return CreateSkinnedModel(model, sourcePath: null);
        }

        public static MmdUnityModelInstance CreateSkinnedModel(MmdModelDefinition model, string? sourcePath)
        {
            return CreateSkinnedModel(model, sourcePath, importScale: 1.0f);
        }

        public static MmdUnityModelInstance CreateSkinnedModel(MmdModelDefinition model, string? sourcePath, float importScale)
        {
            return CreateSkinnedModel(model, sourcePath, importScale, MmdMaterialPreset.MmdToon);
        }

        public static MmdUnityModelInstance CreateSkinnedModel(
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            MmdMaterialPreset preset)
        {
            return CreateSkinnedModel(model, sourcePath, importScale, preset, materialOverride: null);
        }

        public static MmdUnityModelInstance CreateSkinnedModel(
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            MmdMaterialPreset preset,
            MmdMaterialOverrideAsset? materialOverride)
        {
            return CreateSkinnedModel(model, sourcePath, importScale, preset, includeSelfShadowTarget: true, materialOverride: materialOverride);
        }

        internal static MmdUnityModelInstance CreateSkinnedModel(
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            bool includeSelfShadowTarget)
        {
            return CreateSkinnedModel(model, sourcePath, importScale, MmdMaterialPreset.MmdToon, includeSelfShadowTarget);
        }

        internal static MmdUnityModelInstance CreateSkinnedModel(
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            MmdMaterialPreset preset,
            bool includeSelfShadowTarget)
        {
            return CreateSkinnedModel(model, sourcePath, importScale, preset, includeSelfShadowTarget, materialOverride: null);
        }

        internal static MmdUnityModelInstance CreateSkinnedModel(
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            MmdMaterialPreset preset,
            bool includeSelfShadowTarget,
            MmdMaterialOverrideAsset? materialOverride)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (model.bones == null || model.bones.Count == 0)
            {
                throw new ArgumentException("Skinned MMD model instantiation requires at least one bone.", nameof(model));
            }

            float scale = NormalizeImportScale(importScale);
            return CreateSkinnedModel(
                BuildRuntimeRenderingDescriptor(model, preset),
                model.name,
                model.bones,
                model.physics,
                MmdUnityModelSourceContext.FromOptionalPath(sourcePath),
                scale,
                includeSelfShadowTarget,
                materialOverride);
        }

        public static MmdUnityModelInstance CreateExistingSkinnedModelInstance(
            GameObject root,
            MmdModelDefinition model,
            string? sourcePath)
        {
            return CreateExistingSkinnedModelInstance(root, model, sourcePath, importScale: 1.0f);
        }

        public static MmdUnityModelInstance CreateExistingSkinnedModelInstance(
            GameObject root,
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            bool includeSelfShadowTarget = true)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (model.bones == null || model.bones.Count == 0)
            {
                throw new ArgumentException("Existing skinned MMD model rebinding requires at least one bone.", nameof(model));
            }

            float scale = NormalizeImportScale(importScale);
            MmdRenderingDescriptor descriptor = BuildRuntimeRenderingDescriptor(model);
            ValidateDescriptor(descriptor);

            Transform modelRoot = FindExistingSkinnedModelRoot(root.transform);
            SkinnedMeshRenderer renderer = modelRoot.GetComponent<SkinnedMeshRenderer>()
                ?? root.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true)
                ?? throw new InvalidOperationException("Existing PMX scene object must contain a SkinnedMeshRenderer.");
            Mesh? sharedMesh = renderer.sharedMesh;
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length < descriptor.materials.Count)
            {
                throw new InvalidOperationException("Existing PMX scene SkinnedMeshRenderer material slots do not match the PMX material descriptor.");
            }

            Transform[] boneTransforms = renderer.bones;
            IReadOnlyList<MmdBoneDefinition> orderedBones = CreateOrderedBones(model.bones);
            if (boneTransforms == null || boneTransforms.Length != orderedBones.Count)
            {
                throw new InvalidOperationException("Existing PMX scene SkinnedMeshRenderer bones do not match the PMX bone descriptor.");
            }

            ResetExistingBoneTransformsToBindPose(orderedBones, boneTransforms, scale);
            renderer.rootBone = boneTransforms.Length > 0 ? boneTransforms[0] : modelRoot;

            // When the existing scene model is an imported hierarchy instance (Slice B),
            // the SMR already carries the importer-owned Mesh sub-asset. Preserve it instead
            // of rebuilding with "Split Runtime" naming, which would break the importer
            // ownership chain across PlayMode domain reloads.
            bool useExistingMesh = sharedMesh != null && sharedMesh.vertexCount > 0;
            Mesh mesh;
            if (useExistingMesh)
            {
                mesh = sharedMesh!;
            }
            else
            {
                mesh = BuildMesh(descriptor, scale);
                ApplySkinning(mesh, descriptor, orderedBones, boneTransforms, modelRoot);
                BakeVertexMorphBlendShapes(mesh, descriptor, scale);
                mesh.name = sharedMesh == null || string.IsNullOrWhiteSpace(sharedMesh.name)
                    ? "MMD Rebound Mesh"
                    : sharedMesh.name + " Split Runtime";
                renderer.sharedMesh = mesh;
            }
            MmdShaderBindingDiagnostics shaderDiagnostics = MmdUnityMaterialBuilder.BuildExistingShaderDiagnostics(renderer);
            ApplySelfShadowTargetPolicy(root, modelRoot, includeSelfShadowTarget);

            MmdUnityPhysicsBody[] physicsBodies = root.GetComponentsInChildren<MmdUnityPhysicsBody>(includeInactive: true);
            return new MmdUnityModelInstance(
                root,
                mesh,
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

        private static MmdRenderingDescriptor BuildRuntimeRenderingDescriptor(
            MmdModelDefinition model,
            MmdMaterialPreset preset = MmdMaterialPreset.MmdToon)
        {
            return MmdRenderingMeshSplitter.SplitBySubmesh(MmdRenderingDescriptorBuilder.Build(model, preset)).rendering;
        }

        private static MmdUnityModelInstance CreateStaticModel(
            MmdRenderingDescriptor descriptor,
            string modelName,
            IReadOnlyList<MmdBoneDefinition>? bones,
            MmdPhysicsDefinition? physics,
            MmdUnityModelSourceContext? sourceContext,
            float importScale,
            bool includeSelfShadowTarget,
            MmdMaterialOverrideAsset? materialOverride = null)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            ValidateDescriptor(descriptor);

            var root = new GameObject(ResolveModelName(modelName));
            Transform modelRoot = CreateModelRoot(root.transform);
            var mesh = BuildMesh(descriptor, importScale);
            MmdRuntimeTextureResolution textureResolution = MmdRuntimeTextureResolver.ResolveDiffuseTextures(descriptor, sourceContext);
            Material[] materials = MmdUnityMaterialBuilder.BuildMaterials(descriptor, textureResolution, out MmdShaderBindingDiagnostics shaderDiagnostics);
            MmdMaterialOverrideApplier.Apply(materialOverride, materials);
            Transform[] boneTransforms = BuildBoneTransforms(modelRoot, bones, importScale);
            MmdUnityPhysicsBody[] physicsBodies = BuildPhysicsBodies(modelRoot, bones, boneTransforms, physics, importScale);

            var meshFilter = modelRoot.gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = modelRoot.gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = materials;
            ApplyRendererShadowPolicy(meshRenderer);
            ApplySelfShadowTargetPolicy(root, modelRoot, includeSelfShadowTarget);

            return new MmdUnityModelInstance(
                root,
                mesh,
                materials,
                descriptor,
                boneTransforms,
                physicsBodies,
                meshRenderer,
                skinnedMeshRenderer: null,
                sourceContext,
                GetOwnedTextures(textureResolution),
                textureResolution.Diagnostics,
                shaderDiagnostics,
                importScale);
        }

        private static MmdUnityModelInstance CreateSkinnedModel(
            MmdRenderingDescriptor descriptor,
            string modelName,
            IReadOnlyList<MmdBoneDefinition> bones,
            MmdPhysicsDefinition? physics,
            MmdUnityModelSourceContext? sourceContext,
            float importScale,
            bool includeSelfShadowTarget,
            MmdMaterialOverrideAsset? materialOverride = null)
        {
            ValidateDescriptor(descriptor);

            var root = new GameObject(ResolveModelName(modelName));
            Transform modelRoot = CreateModelRoot(root.transform);
            Transform[] boneTransforms = BuildBoneTransforms(modelRoot, bones, importScale);
            MmdUnityPhysicsBody[] physicsBodies = BuildPhysicsBodies(modelRoot, bones, boneTransforms, physics, importScale);
            var mesh = BuildMesh(descriptor, importScale);
            ApplySkinning(mesh, descriptor, bones, boneTransforms, modelRoot);
            BakeVertexMorphBlendShapes(mesh, descriptor, importScale);
            MmdRuntimeTextureResolution textureResolution = MmdRuntimeTextureResolver.ResolveDiffuseTextures(descriptor, sourceContext);
            Material[] materials = MmdUnityMaterialBuilder.BuildMaterials(descriptor, textureResolution, out MmdShaderBindingDiagnostics shaderDiagnostics);
            MmdMaterialOverrideApplier.Apply(materialOverride, materials);

            var renderer = modelRoot.gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
            renderer.bones = boneTransforms;
            renderer.rootBone = boneTransforms.Length > 0 ? boneTransforms[0] : modelRoot;
            ApplyRendererShadowPolicy(renderer);
            ApplySelfShadowTargetPolicy(root, modelRoot, includeSelfShadowTarget);

            return new MmdUnityModelInstance(
                root,
                mesh,
                materials,
                descriptor,
                boneTransforms,
                physicsBodies,
                meshRenderer: null,
                skinnedMeshRenderer: renderer,
                sourceContext,
                GetOwnedTextures(textureResolution),
                textureResolution.Diagnostics,
                shaderDiagnostics,
                importScale);
        }

        private static Transform CreateModelRoot(Transform root)
        {
            var modelObject = new GameObject("Model");
            modelObject.transform.SetParent(root, worldPositionStays: false);
            modelObject.transform.localPosition = Vector3.zero;
            modelObject.transform.localRotation = Quaternion.identity;
            modelObject.transform.localScale = Vector3.one;
            return modelObject.transform;
        }

        private static void ApplyRendererShadowPolicy(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static void ApplySelfShadowTargetPolicy(GameObject root, Transform modelRoot, bool includeSelfShadowTarget)
        {
            if (includeSelfShadowTarget)
            {
                MmdSelfShadowTarget.EnsureHiddenTarget(root, modelRoot);
                return;
            }

            MmdSelfShadowTarget existingTarget = root.GetComponent<MmdSelfShadowTarget>();
            if (existingTarget == null)
            {
                return;
            }

            existingTarget.enabled = false;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(existingTarget);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(existingTarget);
            }
        }

        private static Transform FindModelRoot(Transform root)
        {
            Transform modelRoot = root.Find("Model");
            return modelRoot != null ? modelRoot : root;
        }

        private static Transform FindExistingSkinnedModelRoot(Transform root)
        {
            Transform modelRoot = FindModelRoot(root);
            if (modelRoot.GetComponent<SkinnedMeshRenderer>() != null)
            {
                return modelRoot;
            }

            SkinnedMeshRenderer renderer = root.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true);
            return renderer != null ? renderer.transform : modelRoot;
        }

        private static Texture2D[] GetOwnedTextures(MmdRuntimeTextureResolution textureResolution)
        {
            var textures = new Texture2D[
                textureResolution.DiffuseTextures.Count
                + textureResolution.SphereTextures.Count
                + textureResolution.ToonTextures.Count];
            int index = 0;
            for (int i = 0; i < textureResolution.DiffuseTextures.Count; i++)
            {
                textures[index++] = textureResolution.DiffuseTextures[i].Texture;
            }

            for (int i = 0; i < textureResolution.SphereTextures.Count; i++)
            {
                textures[index++] = textureResolution.SphereTextures[i].Texture;
            }

            for (int i = 0; i < textureResolution.ToonTextures.Count; i++)
            {
                textures[index++] = textureResolution.ToonTextures[i].Texture;
            }

            return textures;
        }

        private static string ResolveModelName(string modelName)
        {
            return string.IsNullOrWhiteSpace(modelName) ? DefaultModelName : modelName;
        }

    }
}
