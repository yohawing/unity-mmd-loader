#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            return CreateStaticModel(
                model,
                sourcePath,
                importScale,
                preset,
                materialOverride,
                MmdMaterialMapperSet.ForPreset(preset));
        }

        public static MmdUnityModelInstance CreateStaticModel(
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            MmdMaterialPreset preset,
            MmdMaterialOverrideAsset? materialOverride,
            MmdMaterialMapperSet materialMappers)
        {
            if (materialMappers == null)
            {
                throw new ArgumentNullException(nameof(materialMappers));
            }

            return CreateStaticModel(
                model,
                sourcePath,
                importScale,
                preset,
                includeSelfShadowTarget: true,
                materialOverride,
                materialMappers);
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
            MmdMaterialOverrideAsset? materialOverride,
            MmdMaterialMapperSet? materialMappers = null)
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
                materialOverride,
                materialMappers ?? MmdMaterialMapperSet.ForPreset(preset));
        }

        public static MmdUnityModelInstance CreateStaticModel(MmdRenderingDescriptor descriptor, string modelName)
        {
            return CreateStaticModel(descriptor, modelName, MmdMaterialMapperSet.BuiltIn);
        }

        public static MmdUnityModelInstance CreateStaticModel(
            MmdRenderingDescriptor descriptor,
            string modelName,
            MmdMaterialMapperSet materialMappers)
        {
            if (materialMappers == null)
            {
                throw new ArgumentNullException(nameof(materialMappers));
            }

            return CreateStaticModel(
                descriptor,
                modelName,
                bones: null,
                physics: null,
                sourceContext: null,
                importScale: 1.0f,
                includeSelfShadowTarget: true,
                materialMappers: materialMappers);
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
            return CreateSkinnedModel(
                model,
                sourcePath,
                importScale,
                preset,
                materialOverride,
                MmdMaterialMapperSet.ForPreset(preset));
        }

        public static MmdUnityModelInstance CreateSkinnedModel(
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            MmdMaterialPreset preset,
            MmdMaterialOverrideAsset? materialOverride,
            MmdMaterialMapperSet materialMappers)
        {
            if (materialMappers == null)
            {
                throw new ArgumentNullException(nameof(materialMappers));
            }

            return CreateSkinnedModel(
                model,
                sourcePath,
                importScale,
                preset,
                includeSelfShadowTarget: true,
                materialOverride,
                materialMappers);
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
            MmdMaterialOverrideAsset? materialOverride,
            MmdMaterialMapperSet? materialMappers = null)
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
                materialOverride,
                materialMappers ?? MmdMaterialMapperSet.ForPreset(preset));
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
            bool includeSelfShadowTarget = true,
            MmdMaterialOverrideAsset? materialOverride = null,
            bool preserveExistingSelfShadowTarget = false,
            MmdMaterialPreset materialPreset = MmdMaterialPreset.MmdToon,
            MmdRenderingDescriptor? existingPlaybackDescriptor = null)
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
            Transform modelRoot = FindExistingSkinnedModelRoot(root.transform);
            SkinnedMeshRenderer renderer = ResolveExistingSkinnedMeshRenderer(root, modelRoot);
            Mesh? sharedMesh = renderer.sharedMesh;
            bool useExistingMesh = sharedMesh != null && sharedMesh.vertexCount > 0;
            MmdRenderingDescriptor descriptor = useExistingMesh
                ? existingPlaybackDescriptor ?? BuildRuntimePlaybackRenderingDescriptor(model, materialPreset)
                : BuildRuntimeRenderingDescriptor(model, materialPreset);
            if (!useExistingMesh)
            {
                ValidateDescriptor(descriptor);
            }
            MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(materialOverride, descriptor);

            Material[] materials = renderer.sharedMaterials;
            if (materials.Length < descriptor.materials.Count)
            {
                throw new InvalidOperationException("Existing PMX scene SkinnedMeshRenderer material slots do not match the PMX material descriptor.");
            }

            MmdMaterialRenderingTargets[] materialRenderingTargets =
                BuildMaterialRenderingTargets(materials.Length, materialPreset);

            Transform[] boneTransforms = renderer.bones;
            IReadOnlyList<MmdBoneDefinition> orderedBones = CreateOrderedBones(model.bones);
            ValidateExistingBoneTransforms(orderedBones, boneTransforms);
            var rollback = new MmdExistingSceneRebindLease(root);
            try
            {

            materials = CloneMaterialsForOverride(materials, materialOverride);
            rollback.AdoptGeneratedMaterials(materials);
            MmdMaterialOverrideApplier.Apply(materialOverride, materials);
            renderer.sharedMaterials = materials;

            ResetExistingBoneTransformsToBindPose(orderedBones, boneTransforms, scale);
            renderer.rootBone = boneTransforms.Length > 0 ? boneTransforms[0] : modelRoot;

            // When the existing scene model is an imported hierarchy instance (Slice B),
            // the SMR already carries the importer-owned Mesh sub-asset. Preserve it instead
            // of rebuilding with "Split Runtime" naming, which would break the importer
            // ownership chain across PlayMode domain reloads.
            Mesh mesh;
            if (useExistingMesh)
            {
                mesh = sharedMesh!;
            }
            else
            {
                mesh = BuildMesh(descriptor, scale);
                rollback.AdoptGeneratedMesh(mesh);
                ApplySkinning(mesh, descriptor, orderedBones, boneTransforms, modelRoot);
                Bounds localBounds = BakeVertexMorphBlendShapes(mesh, descriptor, scale, orderedBones);
                mesh.name = sharedMesh == null || string.IsNullOrWhiteSpace(sharedMesh.name)
                    ? "MMD Rebound Mesh"
                    : sharedMesh.name + " Split Runtime";
                renderer.sharedMesh = mesh;
                renderer.localBounds = localBounds;
            }
            MmdShaderBindingDiagnostics shaderDiagnostics = MmdUnityMaterialBuilder.BuildExistingShaderDiagnostics(renderer);
            ApplySelfShadowTargetPolicy(root, modelRoot, includeSelfShadowTarget, preserveExistingSelfShadowTarget);

            MmdUnityPhysicsBody[] physicsBodies = root.GetComponentsInChildren<MmdUnityPhysicsBody>(includeInactive: true);
            var instance = new MmdUnityModelInstance(
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
                scale,
                materialRenderingTargets);
            rollback.Commit();
            return instance;
            }
            catch
            {
                rollback.RollbackFactoryFailure();
                throw;
            }
        }

        internal static SkinnedMeshRenderer ResolveExistingSkinnedMeshRenderer(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            return ResolveExistingSkinnedMeshRenderer(root, FindExistingSkinnedModelRoot(root.transform));
        }

        internal static void ValidateExistingSkinnedModelCompatibility(GameObject root, MmdModelDefinition model)
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

            SkinnedMeshRenderer renderer = ResolveExistingSkinnedMeshRenderer(root);
            if (renderer.sharedMaterials.Length < model.materials.Count)
            {
                throw new InvalidOperationException("Existing PMX scene SkinnedMeshRenderer material slots do not match the PMX material descriptor.");
            }

            ValidateExistingBoneTransforms(CreateOrderedBones(model.bones), renderer.bones);
        }

        private static SkinnedMeshRenderer ResolveExistingSkinnedMeshRenderer(GameObject root, Transform modelRoot)
        {
            return modelRoot.GetComponent<SkinnedMeshRenderer>()
                ?? root.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true)
                ?? throw new InvalidOperationException("Existing PMX scene object must contain a SkinnedMeshRenderer.");
        }

        private static void ValidateExistingBoneTransforms(
            IReadOnlyList<MmdBoneDefinition> orderedBones,
            Transform[]? boneTransforms)
        {
            if (boneTransforms == null || boneTransforms.Length != orderedBones.Count)
            {
                throw new InvalidOperationException("Existing PMX scene SkinnedMeshRenderer bones do not match the PMX bone descriptor.");
            }

            for (int i = 0; i < boneTransforms.Length; i++)
            {
                if (boneTransforms[i] == null)
                {
                    throw new InvalidOperationException($"Existing PMX scene bone at index {i} is missing.");
                }
            }
        }

        private static Material[] CloneMaterialsForOverride(Material[] materials, MmdMaterialOverrideAsset? materialOverride)
        {
            if (materialOverride == null || materials == null || materials.Length == 0)
            {
                return materials ?? Array.Empty<Material>();
            }

            var clones = new Material[materials.Length];
            try
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null)
                    {
                        continue;
                    }

                    clones[i] = new Material(source)
                    {
                        name = source.name
                    };
                }

                return clones;
            }
            catch
            {
                foreach (Material clone in clones.Where(material => material != null))
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(clone);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(clone);
                    }
                }

                throw;
            }
        }

        private static MmdRenderingDescriptor BuildRuntimeRenderingDescriptor(
            MmdModelDefinition model,
            MmdMaterialPreset preset = MmdMaterialPreset.MmdToon)
        {
            return MmdRenderingMeshSplitter.SplitBySubmesh(MmdRenderingDescriptorBuilder.Build(model, preset)).rendering;
        }

        internal static MmdRenderingDescriptor BuildRuntimePlaybackRenderingDescriptor(
            MmdModelDefinition model,
            MmdMaterialPreset preset)
        {
            // Existing imported meshes already own geometry, skinning and vertex-morph deltas.
            // Build only metadata still consumed by playback, in parallel over the immutable,
            // already-validated PMX definition.
            Task<List<MmdMaterialDescriptor>> materialsTask = Task.Run(
                () => MmdMaterialDescriptorBuilder.Build(model).ToList());
            Task<List<MmdGroupMorphDescriptor>> groupsTask = Task.Run(
                () => MmdMorphDescriptorBuilder.BuildGroupMorphs(model).ToList());
            Task<List<MmdMorphDescriptorBuilder.MmdMaterialMorphDescriptor>> materialMorphsTask = Task.Run(
                () => MmdMorphDescriptorBuilder.BuildMaterialMorphs(model).ToList());
            Task<List<MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor>> flipsTask = Task.Run(
                () => MmdMorphDescriptorBuilder.BuildFlipMorphs(model).ToList());
            Task.WaitAll(materialsTask, groupsTask, materialMorphsTask, flipsTask);

            List<MmdMaterialDescriptor> materials = materialsTask.Result;
            List<MmdVertexMorphDescriptor> vertexMorphs = model.morphs
                .Where(morph => MmdMorphDescriptorBuilder.NormalizeMorphType(morph.type) == "vertex")
                .OrderBy(morph => morph.index)
                .Select(morph => new MmdVertexMorphDescriptor
                {
                    morphIndex = morph.index,
                    morphName = morph.name
                })
                .ToList();
            List<MmdMorphDescriptorBuilder.MmdUvMorphDescriptor> uvMorphs = model.morphs
                .Where(morph =>
                {
                    string type = MmdMorphDescriptorBuilder.NormalizeMorphType(morph.type);
                    return type == "texture" || type == "uva1" || type == "uva2" ||
                           type == "uva3" || type == "uva4";
                })
                .OrderBy(morph => morph.index)
                .Select(morph => new MmdMorphDescriptorBuilder.MmdUvMorphDescriptor
                {
                    morphIndex = morph.index,
                    morphName = morph.name,
                    morphType = MmdMorphDescriptorBuilder.NormalizeMorphType(morph.type),
                    uvOffsetCount = morph.uvOffsets?.Count ?? 0
                })
                .ToList();
            return new MmdRenderingDescriptor
            {
                materials = materials,
                submeshes = MmdSubmeshDescriptorBuilder.Build(materials).ToList(),
                urpMaterialBindings = MmdUrpMaterialBindingDescriptorBuilder.Build(materials, preset).ToList(),
                vertexMorphs = vertexMorphs,
                uvMorphs = uvMorphs,
                groupMorphs = groupsTask.Result,
                materialMorphs = materialMorphsTask.Result,
                flipMorphs = flipsTask.Result,
                ikCount = model.ik.Count
            };
        }

        private static MmdMaterialRenderingTargets[] BuildMaterialRenderingTargets(
            int materialCount,
            MmdMaterialPreset preset)
        {
            MmdMaterialRenderingTargets defaultTargets =
                MmdMaterialMapperSet.ForPreset(preset).DefaultRenderingTargets;
            var targets = new MmdMaterialRenderingTargets[materialCount];
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] = defaultTargets;
            }

            return targets;
        }

        private static MmdUnityModelInstance CreateStaticModel(
            MmdRenderingDescriptor descriptor,
            string modelName,
            IReadOnlyList<MmdBoneDefinition>? bones,
            MmdPhysicsDefinition? physics,
            MmdUnityModelSourceContext? sourceContext,
            float importScale,
            bool includeSelfShadowTarget,
            MmdMaterialOverrideAsset? materialOverride = null,
            MmdMaterialMapperSet? materialMappers = null)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            ValidateDescriptor(descriptor);
            MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(materialOverride, descriptor);

            var root = new GameObject(ResolveModelName(modelName));
            Mesh? mesh = null;
            Material[] materials = Array.Empty<Material>();
            MmdMaterialRenderingTargets[] materialRenderingTargets = Array.Empty<MmdMaterialRenderingTargets>();
            MmdRuntimeTextureResolution? textureResolution = null;
            try
            {
                Transform modelRoot = CreateModelRoot(root.transform);
                mesh = BuildMesh(descriptor, importScale);
                textureResolution = MmdRuntimeTextureResolver.ResolveDiffuseTextures(descriptor, sourceContext);
                materials = MmdUnityMaterialBuilder.BuildMaterials(
                    descriptor,
                    textureResolution,
                    materialMappers ?? MmdMaterialMapperSet.BuiltIn,
                    out MmdShaderBindingDiagnostics shaderDiagnostics,
                    out materialRenderingTargets);
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
                    importScale,
                    materialRenderingTargets);
            }
            catch
            {
                DestroyGeneratedModelArtifacts(root, mesh, materials, textureResolution);
                throw;
            }
        }

        private static MmdUnityModelInstance CreateSkinnedModel(
            MmdRenderingDescriptor descriptor,
            string modelName,
            IReadOnlyList<MmdBoneDefinition> bones,
            MmdPhysicsDefinition? physics,
            MmdUnityModelSourceContext? sourceContext,
            float importScale,
            bool includeSelfShadowTarget,
            MmdMaterialOverrideAsset? materialOverride = null,
            MmdMaterialMapperSet? materialMappers = null)
        {
            ValidateDescriptor(descriptor);
            MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(materialOverride, descriptor);

            var root = new GameObject(ResolveModelName(modelName));
            Mesh? mesh = null;
            Material[] materials = Array.Empty<Material>();
            MmdMaterialRenderingTargets[] materialRenderingTargets = Array.Empty<MmdMaterialRenderingTargets>();
            MmdRuntimeTextureResolution? textureResolution = null;
            try
            {
                Transform modelRoot = CreateModelRoot(root.transform);
                Transform[] boneTransforms = BuildBoneTransforms(modelRoot, bones, importScale);
                MmdUnityPhysicsBody[] physicsBodies = BuildPhysicsBodies(modelRoot, bones, boneTransforms, physics, importScale);
                mesh = BuildMesh(descriptor, importScale);
                ApplySkinning(mesh, descriptor, bones, boneTransforms, modelRoot);
                Bounds localBounds = BakeVertexMorphBlendShapes(mesh, descriptor, importScale, bones);
                textureResolution = MmdRuntimeTextureResolver.ResolveDiffuseTextures(descriptor, sourceContext);
                materials = MmdUnityMaterialBuilder.BuildMaterials(
                    descriptor,
                    textureResolution,
                    materialMappers ?? MmdMaterialMapperSet.BuiltIn,
                    out MmdShaderBindingDiagnostics shaderDiagnostics,
                    out materialRenderingTargets);
                MmdMaterialOverrideApplier.Apply(materialOverride, materials);

                var renderer = modelRoot.gameObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.sharedMaterials = materials;
                renderer.bones = boneTransforms;
                renderer.rootBone = boneTransforms.Length > 0 ? boneTransforms[0] : modelRoot;
                renderer.localBounds = localBounds;
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
                    importScale,
                    materialRenderingTargets);
            }
            catch
            {
                DestroyGeneratedModelArtifacts(root, mesh, materials, textureResolution);
                throw;
            }
        }

        private static void DestroyGeneratedModelArtifacts(
            GameObject root,
            Mesh? mesh,
            Material[] materials,
            MmdRuntimeTextureResolution? textureResolution)
        {
            DestroyGeneratedObject(root);
            DestroyGeneratedObject(mesh);
            foreach (Material material in materials)
            {
                DestroyGeneratedObject(material);
            }

            if (textureResolution == null)
            {
                return;
            }

            var destroyedTextureIds = new HashSet<int>();
            foreach (Texture2D texture in GetOwnedTextures(textureResolution))
            {
                if (texture != null && destroyedTextureIds.Add(texture.GetInstanceID()))
                {
                    DestroyGeneratedObject(texture);
                }
            }
        }

        private static void DestroyGeneratedObject(UnityEngine.Object? value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(value);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
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

        private static void ApplySelfShadowTargetPolicy(
            GameObject root,
            Transform modelRoot,
            bool includeSelfShadowTarget,
            bool preserveExistingTarget = false)
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
            if (preserveExistingTarget)
            {
                return;
            }

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
            var textures = new List<Texture2D>();
            var seen = new HashSet<int>();
            for (int i = 0; i < textureResolution.DiffuseTextures.Count; i++)
            {
                Texture2D texture = textureResolution.DiffuseTextures[i].Texture;
                if (seen.Add(texture.GetInstanceID())) textures.Add(texture);
            }

            for (int i = 0; i < textureResolution.SphereTextures.Count; i++)
            {
                Texture2D texture = textureResolution.SphereTextures[i].Texture;
                if (seen.Add(texture.GetInstanceID())) textures.Add(texture);
            }

            for (int i = 0; i < textureResolution.ToonTextures.Count; i++)
            {
                Texture2D texture = textureResolution.ToonTextures[i].Texture;
                if (seen.Add(texture.GetInstanceID())) textures.Add(texture);
            }

            return textures.ToArray();
        }

        private static string ResolveModelName(string modelName)
        {
            return string.IsNullOrWhiteSpace(modelName) ? DefaultModelName : modelName;
        }

    }
}
