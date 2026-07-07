#nullable enable

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Mmd;
using Mmd.Parser;
using Mmd.Physics;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    public static class MmdEditorVerificationFacade
    {
        public const string InputValidationStage = "input-validation";
        public const string PmxReadStage = "pmx-read";
        public const string PmxParseStage = "pmx-parse";
        public const string PmxValidationStage = "pmx-validation";
        public const string VmdReadStage = "vmd-read";
        public const string VmdParseStage = "vmd-parse";
        public const string VmdValidationStage = "vmd-validation";
        public const string UnityInstantiationStage = "unity-instantiation";
        public const string RuntimeApplyFrameStage = "runtime-apply-frame";
        public const string CompleteStage = "complete";

        public static MmdEditorPmxSceneLoadResult LoadPmxIntoScene(string pmxPath)
        {
            string fullPmxPath = ValidateInputFile(pmxPath, "PMX", nameof(pmxPath));
            byte[] pmxBytes = RunStage(PmxReadStage, () => File.ReadAllBytes(fullPmxPath));
            var parser = new NativeMmdParser();
            MmdModelDefinition model = RunStage(PmxParseStage, () => parser.LoadModel(pmxBytes));
            RunStage(PmxValidationStage, () => MmdModelValidator.ThrowIfInvalid(model));
            MmdUnityModelInstance instance = RunStage(
                UnityInstantiationStage,
                () => CreatePmxSceneModel(model, fullPmxPath));
            return new MmdEditorPmxSceneLoadResult(model, instance, fullPmxPath);
        }

        public static MmdEditorPmxSceneLoadResult LoadPmxIntoScene(MmdPmxAsset pmxAsset)
        {
            if (pmxAsset == null)
            {
                throw new MmdEditorVerificationException(
                    InputValidationStage,
                    "PMX asset is required.",
                    new ArgumentNullException(nameof(pmxAsset)));
            }

            var parser = new NativeMmdParser();
            MmdModelDefinition model = RunStage(PmxParseStage, () => pmxAsset.LoadModel(parser));
            RunStage(PmxValidationStage, () => MmdModelValidator.ThrowIfInvalid(model));
            float importScale = pmxAsset.ImportScale;
            string? sourcePath = string.IsNullOrWhiteSpace(pmxAsset.SourcePath) ? null : pmxAsset.SourcePath;

            MmdUnityModelInstance instance;
            GameObject? importedRoot = pmxAsset.ImportedRoot;
            if (importedRoot != null)
            {
                // Slice B: wrap the imported hierarchy directly without rebuilding Mesh or Materials.
                instance = RunStage(
                    UnityInstantiationStage,
                    () => MmdUnityModelFactory.CreateFromImportedHierarchy(
                        importedRoot,
                        model,
                        sourcePath,
                        importScale,
                        MmdPmxModelPresetPolicy.AllowsAutomaticSelfShadowTarget(pmxAsset.ModelPreset),
                        pmxAsset.MaterialOverrideAsset));
                // Apply material remaps on the Slice B path (consistent with UseImportedPmxAssetReferences).
                instance = ApplyMaterialRemapsToInstance(instance, pmxAsset);
            }
            else
            {
                // Slice A fallback: create runtime scene model then rebind importer sub-assets.
                instance = RunStage(
                    UnityInstantiationStage,
                    () => UseImportedPmxAssetReferences(
                        CreatePmxSceneModel(
                            model,
                            sourcePath,
                            importScale,
                            MmdPmxModelPresetPolicy.AllowsAutomaticSelfShadowTarget(pmxAsset.ModelPreset)),
                        pmxAsset));
            }
            return new MmdEditorPmxSceneLoadResult(
                model,
                instance,
                string.IsNullOrWhiteSpace(pmxAsset.SourceId) ? pmxAsset.name : pmxAsset.SourceId);
        }

        public static MmdEditorPlaybackSceneLoadResult LoadPlaybackIntoScene(
            string pmxPath,
            string vmdPath,
            float frameRate = 30.0f,
            int initialFrame = 0,
            bool playOnStart = true)
        {
            string fullPmxPath = ValidateInputFile(pmxPath, "PMX", nameof(pmxPath));
            string fullVmdPath = ValidateInputFile(vmdPath, "VMD", nameof(vmdPath));
            ValidateFrameRate(frameRate);
            if (initialFrame < 0)
            {
                throw new MmdEditorVerificationException(
                    InputValidationStage,
                    "Initial frame must not be negative.",
                    new ArgumentOutOfRangeException(nameof(initialFrame)));
            }

            var parser = new NativeMmdParser();
            byte[] pmxBytes = RunStage(PmxReadStage, () => File.ReadAllBytes(fullPmxPath));
            MmdModelDefinition model = RunStage(PmxParseStage, () => parser.LoadModel(pmxBytes));
            RunStage(PmxValidationStage, () => MmdModelValidator.ThrowIfInvalid(model));

            byte[] vmdBytes = RunStage(VmdReadStage, () => File.ReadAllBytes(fullVmdPath));
            MmdMotionDefinition motion = RunStage(VmdParseStage, () => parser.LoadMotion(vmdBytes));
            RunStage(VmdValidationStage, () => MmdMotionValidator.ThrowIfInvalid(motion));

            MmdUnityPlaybackBinding? binding = null;
            try
            {
                binding = RunStage(
                    UnityInstantiationStage,
                    () => MmdUnityPlaybackBinding.CreateSkinned(
                        model,
                        motion,
                        fullPmxPath,
                        fullVmdPath,
                        fullPmxPath));
                MmdUnityPlaybackController controller = binding.Instance.Root.AddComponent<MmdUnityPlaybackController>();
                // Editor verification/diagnostics arbitrary-frame snapshot path: force physics Off locally.
                // Normal controller default remains Live for forward PlayMode playback; Live cannot support
                // non-zero initialFrame or random-access evaluation (see MmdUnityPlaybackBinding/MmdUnityPlaybackController Live guards).
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                controller.Configure(binding, frameRate, playOnStart);
                MmdRuntimeImporterComponent runtimeImporter = binding.Instance.Root.AddComponent<MmdRuntimeImporterComponent>();
                runtimeImporter.ConfigurePaths(fullPmxPath, fullVmdPath, frameRate, initialFrame, playOnStart);
                MmdPlaybackSnapshot snapshot = RunStage(
                    RuntimeApplyFrameStage,
                    () => controller.ApplyFrame(initialFrame));
                return new MmdEditorPlaybackSceneLoadResult(binding.Instance, binding, controller, model, motion, fullPmxPath, fullVmdPath, snapshot);
            }
            catch
            {
                DestroyBinding(binding);
                throw;
            }
        }

        public static MmdEditorPlaybackSceneLoadResult LoadPlaybackIntoScene(
            MmdPmxAsset pmxAsset,
            MmdVmdAsset vmdAsset,
            float frameRate = 30.0f,
            int initialFrame = 0,
            bool playOnStart = true)
        {
            if (pmxAsset == null)
            {
                throw new MmdEditorVerificationException(
                    InputValidationStage,
                    "PMX asset is required.",
                    new ArgumentNullException(nameof(pmxAsset)));
            }

            if (vmdAsset == null)
            {
                throw new MmdEditorVerificationException(
                    InputValidationStage,
                    "VMD asset is required.",
                    new ArgumentNullException(nameof(vmdAsset)));
            }

            // Bundled PMX+VMD asset path stores source references on MmdUnityPlaybackController.

            ValidateFrameRate(frameRate);
            if (initialFrame < 0)
            {
                throw new MmdEditorVerificationException(
                    InputValidationStage,
                    "Initial frame must not be negative.",
                    new ArgumentOutOfRangeException(nameof(initialFrame)));
            }

            var parser = new NativeMmdParser();
            MmdModelDefinition model = RunStage(PmxParseStage, () => pmxAsset.LoadModel(parser));
            RunStage(PmxValidationStage, () => MmdModelValidator.ThrowIfInvalid(model));
            MmdMotionDefinition motion = RunStage(VmdParseStage, () => vmdAsset.LoadMotion(parser));
            RunStage(VmdValidationStage, () => MmdMotionValidator.ThrowIfInvalid(motion));

            MmdUnityPlaybackBinding? binding = null;
            MmdUnityModelInstance? placedInstance = null;
            try
            {
                float importScale = pmxAsset.ImportScale;
                // Create scene model using imported PMX Mesh/Material sub-assets (preferred for asset path),
                // then create playback binding over that existing instance so importer materials are used
                // (no switch to runtime-generated materials). Raw string path remains runtime/raw behavior.
                MmdUnityModelInstance transient = RunStage(
                    UnityInstantiationStage,
                    () => CreatePmxSceneModel(
                        model,
                        string.IsNullOrWhiteSpace(pmxAsset.SourcePath) ? null : pmxAsset.SourcePath,
                        importScale,
                        MmdPmxModelPresetPolicy.AllowsAutomaticSelfShadowTarget(pmxAsset.ModelPreset)));
                placedInstance = RunStage(
                    UnityInstantiationStage,
                    () => UseImportedPmxAssetReferences(transient, pmxAsset));

                binding = RunStage(
                    UnityInstantiationStage,
                    () => MmdUnityPlaybackBinding.CreateSkinned(
                        placedInstance,
                        pmxAsset,
                        vmdAsset,
                        motion));
                MmdUnityPlaybackController controller = placedInstance.Root.AddComponent<MmdUnityPlaybackController>();
                // Editor verification/diagnostics arbitrary-frame snapshot path: force physics Off locally.
                // Normal controller default remains Live for forward PlayMode playback; Live cannot support
                // non-zero initialFrame or random-access evaluation (see MmdUnityPlaybackBinding/MmdUnityPlaybackController Live guards).
                controller.SetPhysicsMode(MmdPhysicsMode.Off);
                controller.Configure(binding, frameRate, playOnStart);
                controller.ConfigureModelAsset(pmxAsset);
                controller.ConfigureMotionAsset(vmdAsset);
                MmdPlaybackSnapshot snapshot = RunStage(
                    RuntimeApplyFrameStage,
                    () => controller.ApplyFrame(initialFrame));
                return new MmdEditorPlaybackSceneLoadResult(
                    placedInstance,
                    binding,
                    controller,
                    model,
                    motion,
                    string.IsNullOrWhiteSpace(pmxAsset.SourceId) ? pmxAsset.name : pmxAsset.SourceId,
                    string.IsNullOrWhiteSpace(vmdAsset.SourceId) ? vmdAsset.name : vmdAsset.SourceId,
                    snapshot);
            }
            catch
            {
                DestroyBinding(binding);
                // placedInstance may be the rebound one; transient destroyed inside Use if rebound happened.
                // Safe destroy of root if binding did not take ownership in error path.
                if (placedInstance != null && (binding == null || !ReferenceEquals(placedInstance, binding.Instance)))
                {
                    if (placedInstance.Root != null)
                    {
                        UnityEngine.Object.DestroyImmediate(placedInstance.Root);
                    }
                }
                throw;
            }
        }

        private static string ValidateInputFile(string path, string label, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new MmdEditorVerificationException(
                    InputValidationStage,
                    label + " path is required.",
                    new ArgumentException(label + " path is required.", parameterName));
            }

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new MmdEditorVerificationException(
                    InputValidationStage,
                    label + " file was not found: " + fullPath,
                    new FileNotFoundException(label + " file was not found.", fullPath));
            }

            return fullPath;
        }

        private static void ValidateFrameRate(float frameRate)
        {
            if (frameRate <= 0.0f || float.IsNaN(frameRate) || float.IsInfinity(frameRate))
            {
                throw new MmdEditorVerificationException(
                    InputValidationStage,
                    "Frame rate must be a finite positive value.",
                    new ArgumentOutOfRangeException(nameof(frameRate)));
            }
        }

        private static MmdUnityModelInstance CreatePmxSceneModel(MmdModelDefinition model, string? sourcePath)
        {
            return CreatePmxSceneModel(model, sourcePath, importScale: 1.0f, includeSelfShadowTarget: true);
        }

        private static MmdUnityModelInstance CreatePmxSceneModel(
            MmdModelDefinition model,
            string? sourcePath,
            float importScale,
            bool includeSelfShadowTarget = true)
        {
            float scale = importScale;
            if (model.bones != null && model.bones.Count > 0)
            {
                return MmdUnityModelFactory.CreateSkinnedModel(model, sourcePath, scale, includeSelfShadowTarget);
            }

            return MmdUnityModelFactory.CreateStaticModel(model, sourcePath, scale, includeSelfShadowTarget);
        }

        private static void RunStage(string stage, Action action)
        {
            RunStage<object?>(
                stage,
                () =>
                {
                    action();
                    return null;
                });
        }

        private static T RunStage<T>(string stage, Func<T> action)
        {
            try
            {
                return action();
            }
            catch (MmdEditorVerificationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MmdEditorVerificationException(stage, ex.Message, ex);
            }
        }

        private static void DestroyBinding(MmdUnityPlaybackBinding? binding)
        {
            if (binding == null)
            {
                return;
            }

            MmdUnityModelInstance instance = binding.Instance;
            binding.Dispose();
            if (instance.Root != null)
            {
                UnityEngine.Object.DestroyImmediate(instance.Root);
            }

            if (instance.Mesh != null)
            {
                UnityEngine.Object.DestroyImmediate(instance.Mesh);
            }

            foreach (Material material in instance.Materials.Where(material => material != null).Distinct())
            {
                UnityEngine.Object.DestroyImmediate(material);
            }

            foreach (Texture2D texture in instance.OwnedTextures.Where(texture => texture != null).Distinct())
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static MmdUnityModelInstance UseImportedPmxAssetReferences(
            MmdUnityModelInstance instance,
            MmdPmxAsset pmxAsset)
        {
            Mesh? importedMesh = pmxAsset.ImportedMesh;
            if (importedMesh == null)
            {
                return instance;
            }

            SkinnedMeshRenderer? skinned = instance.SkinnedMeshRenderer;
            MeshRenderer? meshRenderer = instance.MeshRenderer;
            if (skinned == null && meshRenderer == null)
            {
                return instance;
            }

            Mesh transientMesh = instance.Mesh;
            Material[] sceneMaterials = ResolveSceneMaterials(instance, pmxAsset);
            Texture2D[] retainedRuntimeTextures = RetainReferencedRuntimeTextures(instance.OwnedTextures, sceneMaterials);

            if (skinned != null)
            {
                skinned.sharedMesh = importedMesh;
                skinned.sharedMaterials = sceneMaterials;
            }
            else if (meshRenderer != null)
            {
                MeshFilter? filter = meshRenderer.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    filter.sharedMesh = importedMesh;
                }

                meshRenderer.sharedMaterials = sceneMaterials;
            }

            var rebound = new MmdUnityModelInstance(
                instance.Root,
                importedMesh,
                sceneMaterials,
                instance.RenderingDescriptor,
                instance.BoneTransforms,
                instance.PhysicsBodies,
                meshRenderer,
                skinned,
                instance.SourceContext,
                retainedRuntimeTextures,
                instance.TextureDiagnostics,
                instance.ShaderDiagnostics,
                instance.ImportScale);

            DestroyGeneratedObject(transientMesh);
            DestroyReplacedRuntimeMaterials(instance.Materials, sceneMaterials);
            DestroyUnretainedRuntimeTextures(instance.OwnedTextures, retainedRuntimeTextures);

            return rebound;
        }

        private static Material[] ResolveSceneMaterials(MmdUnityModelInstance instance, MmdPmxAsset pmxAsset)
        {
            Material[] runtimeMaterials = instance.Materials ?? Array.Empty<Material>();
            Material[] importedMaterials = pmxAsset.ImportedMaterials ?? Array.Empty<Material>();
            int slotCount = instance.RenderingDescriptor.materials.Count;
            if (slotCount <= 0)
            {
                slotCount = Math.Max(runtimeMaterials.Length, importedMaterials.Length);
            }

            if (slotCount <= 0)
            {
                return ApplyMaterialRemaps(runtimeMaterials, pmxAsset.MaterialRemaps);
            }

            Material[] sceneMaterials = new Material[slotCount];
            for (int i = 0; i < sceneMaterials.Length; i++)
            {
                Material? imported = i < importedMaterials.Length ? importedMaterials[i] : null;
                Material? fallback = i < runtimeMaterials.Length ? runtimeMaterials[i] : null;
                sceneMaterials[i] = imported != null
                    ? imported
                    : fallback!;
            }

            return ApplyMaterialRemaps(sceneMaterials, pmxAsset.MaterialRemaps);
        }

        private static Material[] ApplyMaterialRemaps(Material[] sceneMaterials, Material[] materialRemaps)
        {
            if (sceneMaterials == null || sceneMaterials.Length == 0)
            {
                return Array.Empty<Material>();
            }

            if (materialRemaps == null || materialRemaps.Length == 0)
            {
                return sceneMaterials;
            }

            Material[] resolvedMaterials = (Material[])sceneMaterials.Clone();
            int count = Math.Min(resolvedMaterials.Length, materialRemaps.Length);
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                if (materialRemaps[i] == null)
                {
                    continue;
                }

                resolvedMaterials[i] = materialRemaps[i];
                changed = true;
            }

            return changed ? resolvedMaterials : sceneMaterials;
        }

        private static void DestroyReplacedRuntimeMaterials(Material[] runtimeMaterials, Material[] resolvedMaterials)
        {
            if (runtimeMaterials == resolvedMaterials)
            {
                return;
            }

            foreach (Material material in runtimeMaterials.Where(material => material != null).Distinct())
            {
                if (resolvedMaterials.Contains(material) || AssetDatabase.Contains(material))
                {
                    continue;
                }

                DestroyGeneratedObject(material);
            }
        }

        private static Texture2D[] RetainReferencedRuntimeTextures(Texture2D[] runtimeTextures, Material[] sceneMaterials)
        {
            if (runtimeTextures == null || runtimeTextures.Length == 0)
            {
                return Array.Empty<Texture2D>();
            }

            return runtimeTextures
                .Where(texture => texture != null && !AssetDatabase.Contains(texture) && MaterialsReferenceTexture(sceneMaterials, texture))
                .Distinct()
                .ToArray();
        }

        private static bool MaterialsReferenceTexture(Material[] materials, Texture texture)
        {
            foreach (Material material in materials.Where(material => material != null))
            {
                if (MaterialReferencesTexture(material, texture, "_BaseMap") ||
                    MaterialReferencesTexture(material, texture, "_MainTex") ||
                    MaterialReferencesTexture(material, texture, "_SphereMap") ||
                    MaterialReferencesTexture(material, texture, "_ToonMap"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MaterialReferencesTexture(Material material, Texture texture, string propertyName)
        {
            return material.HasProperty(propertyName) && material.GetTexture(propertyName) == texture;
        }

        private static void DestroyUnretainedRuntimeTextures(Texture2D[] runtimeTextures, Texture2D[] retainedRuntimeTextures)
        {
            foreach (Texture2D texture in runtimeTextures.Where(texture => texture != null).Distinct())
            {
                if (retainedRuntimeTextures.Contains(texture) || AssetDatabase.Contains(texture))
                {
                    continue;
                }

                DestroyGeneratedObject(texture);
            }
        }

        private static MmdUnityModelInstance ApplyMaterialRemapsToInstance(
            MmdUnityModelInstance instance,
            MmdPmxAsset pmxAsset)
        {
            Material[] remaps = pmxAsset.MaterialRemaps;
            if (remaps == null || remaps.Length == 0)
            {
                return instance;
            }

            return MmdUnityModelFactory.ApplyMaterialRemaps(instance, remaps);
        }

        private static void DestroyGeneratedObject(UnityEngine.Object? generated)
        {
            if (generated == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(generated);
        }
    }

    public sealed class MmdEditorVerificationException : Exception
    {
        public MmdEditorVerificationException(string stage, string message, Exception innerException)
            : base(message, innerException)
        {
            if (string.IsNullOrWhiteSpace(stage))
            {
                throw new ArgumentException("Verification stage is required.", nameof(stage));
            }

            Stage = stage;
        }

        public string Stage { get; }
    }

    public sealed class MmdEditorPmxSceneLoadResult
    {
        public MmdEditorPmxSceneLoadResult(
            MmdModelDefinition model,
            MmdUnityModelInstance instance,
            string modelPath)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            ModelPath = string.IsNullOrWhiteSpace(modelPath)
                ? throw new ArgumentException("Model path is required.", nameof(modelPath))
                : Path.GetFullPath(modelPath);
        }

        public MmdModelDefinition Model { get; }

        public MmdUnityModelInstance Instance { get; }

        public string ModelPath { get; }
    }
}
