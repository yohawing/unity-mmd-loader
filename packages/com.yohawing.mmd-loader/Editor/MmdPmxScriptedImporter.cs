#nullable enable

using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using Mmd.Parser;
using Mmd.UnityIntegration;
using Mmd.Rendering;
using Mmd;

namespace Mmd.Editor
{
    public enum MmdPmxModelPreset
    {
        Custom = 0,
        Character = 1,
        Stage = 2
    }

    public enum MmdPmxMeshGenerationMode
    {
        SingleMesh = 0,
        SplitByMaterial = 1
    }

    public enum MmdPmxMaterialTexturePolicy
    {
        ResolveReferencesOnly = 0
    }

    public enum MmdPmxAnimationType
    {
        None = 0,
        Generic = 1,
        Humanoid = 2
    }

    public enum MmdPmxShaderPreset
    {
        MmdBasicUrpToon = 0,
        UrpLit = 1,
        MmdToonLit = 2
    }

    [ScriptedImporter(28, "pmx")]
    public sealed class MmdPmxScriptedImporter : ScriptedImporter
    {
        [SerializeField] private float importScale = MmdPmxAsset.DefaultImportScale;
        [SerializeField] private MmdPmxModelPreset modelPreset = MmdPmxModelPreset.Custom;
        [SerializeField] private bool modelPresetAutoAssigned;
        // Migration-only fields retained so existing .pmx.meta files still deserialize cleanly.
#pragma warning disable CS0414
        [SerializeField, HideInInspector] private MmdPmxMeshGenerationMode meshGenerationMode = MmdPmxMeshGenerationMode.SingleMesh;
        [SerializeField, HideInInspector] private MmdPmxMaterialTexturePolicy materialTexturePolicy = MmdPmxMaterialTexturePolicy.ResolveReferencesOnly;
#pragma warning restore CS0414
        [SerializeField] private MmdPmxAnimationType animationType = MmdPmxAnimationType.Generic;
        [SerializeField] private MmdPmxShaderPreset shaderPreset = MmdPmxShaderPreset.MmdBasicUrpToon;
        [SerializeField] private MmdMaterialOverrideAsset? materialOverrideAsset;
        [SerializeField] private Material[] materialRemaps = System.Array.Empty<Material>();
        [SerializeField] private MmdHumanoidBoneMappingOverride[] humanoidBoneMappingOverrides =
            System.Array.Empty<MmdHumanoidBoneMappingOverride>();
        [SerializeField] private float upperArmTwist = MmdHumanoidRetargetQualitySettings.DefaultUpperArmTwist;
        [SerializeField] private float lowerArmTwist = MmdHumanoidRetargetQualitySettings.DefaultLowerArmTwist;
        [SerializeField] private float upperLegTwist = MmdHumanoidRetargetQualitySettings.DefaultUpperLegTwist;
        [SerializeField] private float lowerLegTwist = MmdHumanoidRetargetQualitySettings.DefaultLowerLegTwist;
        [SerializeField] private float armStretch = MmdHumanoidRetargetQualitySettings.DefaultArmStretch;
        [SerializeField] private float legStretch = MmdHumanoidRetargetQualitySettings.DefaultLegStretch;
        [SerializeField] private float feetSpacing = MmdHumanoidRetargetQualitySettings.DefaultFeetSpacing;
        [SerializeField] private bool hasTranslationDoF = MmdHumanoidRetargetQualitySettings.DefaultHasTranslationDoF;

        public float ImportScale => NormalizeImportScale(importScale);

        public MmdPmxModelPreset ModelPreset => modelPreset;

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public MmdPmxMeshGenerationMode MeshGenerationMode => MmdPmxMeshGenerationMode.SingleMesh;

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public MmdPmxMaterialTexturePolicy MaterialTexturePolicy => MmdPmxMaterialTexturePolicy.ResolveReferencesOnly;

        public MmdPmxAnimationType AnimationType => animationType;

        public MmdPmxShaderPreset ShaderPreset => shaderPreset;

        public Material[] MaterialRemaps => materialRemaps;

        public MmdHumanoidBoneMappingOverride[] HumanoidBoneMappingOverrides => humanoidBoneMappingOverrides;

        public MmdHumanoidRetargetQualitySettings HumanoidRetargetQualitySettings =>
            new MmdHumanoidRetargetQualitySettings(
                upperArmTwist,
                lowerArmTwist,
                upperLegTwist,
                lowerLegTwist,
                armStretch,
                legStretch,
                feetSpacing,
                hasTranslationDoF);

        public override void OnImportAsset(AssetImportContext ctx)
        {
            byte[] bytes = File.ReadAllBytes(ctx.assetPath);
            string resolvedSourcePath = MmdAssetPathUtility.ResolveAssetSourcePath(ctx.assetPath);
            MmdPmxParsePayload payload = MmdPmxParsePayload.FromBytes(bytes);
            MmdModelDefinition model = payload.Model;
            MmdPmxParseSummary parseSummary = payload.ParseSummary;
            MmdPmxModelPreset effectiveModelPreset = ResolveModelPresetForImport(model);

            using var transaction = new MmdImportObjectTransaction();
            MmdUnityModelInstance generatedAssets = MmdPmxImportAssetCacheBuilder.CreateImportedAssetCache(
                model,
                ImportScale,
                MmdPmxModelPresetAutoDetector.IsCharacter(effectiveModelPreset),
                MapMaterialPreset(shaderPreset),
                materialOverride: null);
            Material[] generatedMaterials = generatedAssets.Materials;
            transaction.Track(generatedAssets.Root, hierarchyRoot: true);
            transaction.Track(generatedAssets.Mesh);
            foreach (Material material in generatedMaterials)
            {
                transaction.Track(material);
            }
            foreach (Texture2D texture in generatedAssets.OwnedTextures)
            {
                transaction.Track(texture);
            }
            MmdPmxImportFaultInjection.ThrowIfRequested(ctx.assetPath, MmdPmxImportStage.AssetCacheCreated);

            Mesh importedMesh = generatedAssets.Mesh;
            Material[] importedMaterials = generatedMaterials;

            MmdPmxProjectTextureBindingSummary textureBindingSummary =
                MmdPmxProjectTextureBinder.BindProjectTextureAssetsToMaterials(
                    model,
                    ctx.assetPath,
                    importedMaterials,
                    generatedAssets.RenderingDescriptor,
                    ctx);
            foreach (MmdPmxOwnedTextureSubAsset ownedTexture in textureBindingSummary.OwnedSubAssets)
            {
                transaction.Track(ownedTexture.Texture);
            }
            MmdPmxImportFaultInjection.ThrowIfRequested(ctx.assetPath, MmdPmxImportStage.ProjectTexturesBound);

            if (shaderPreset == MmdPmxShaderPreset.UrpLit)
            {
                MmdPbrTextureConventionScanner.ApplyScannedMaterialOverrides(
                    ctx,
                    model,
                    ctx.assetPath,
                    importedMaterials);
            }

            MmdMmeFxMaterialOverrideBuilder.ApplyScannedMaterialOverrides(
                ctx,
                resolvedSourcePath,
                generatedAssets.RenderingDescriptor,
                importedMaterials);

            ApplyMaterialOverrideAsset(ctx, generatedAssets.RenderingDescriptor, importedMaterials);

            generatedAssets = MmdUnityModelFactory.ApplyMaterialRemaps(generatedAssets, materialRemaps);
            importedMaterials = generatedAssets.Materials;
            for (int i = 0; i < generatedMaterials.Length; i++)
            {
                if (HasMaterialRemap(materialRemaps, i))
                {
                    transaction.Discard(generatedMaterials[i]);
                }
            }

            MmdPmxAsset asset = MmdPmxImportedAssetBuilder.CreateAndInitializeImportedAsset(
                bytes,
                ctx.assetPath,
                resolvedSourcePath,
                ImportScale,
                effectiveModelPreset.ToString(),
                GetShaderPresetDisplayName(shaderPreset),
                parseSummary,
                generatedAssets,
                materialRemaps,
                animationType.ToString(),
                materialOverrideAsset);
            asset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            transaction.Track(asset);
            MmdPmxImportFaultInjection.ThrowIfRequested(ctx.assetPath, MmdPmxImportStage.ImportedAssetCreated);
            asset.ApplyProjectTextureBindingSummary(
                textureBindingSummary.ResolvedReferenceCount,
                textureBindingSummary.MissingReferenceCount,
                textureBindingSummary.MissingReferenceSample);

            MmdPmxHumanoidAvatarImportBuilder.MmdPmxHumanoidAvatarImportResult avatarImport =
                MmdPmxHumanoidAvatarImportBuilder.TryBuildHumanoidAvatar(
                    asset,
                    model.name,
                    shouldBuildHumanoid: animationType == MmdPmxAnimationType.Humanoid,
                    animationTypeLabel: animationType.ToString(),
                    mappingOverrides: humanoidBoneMappingOverrides,
                    model: model,
                    retargetQualitySettings: HumanoidRetargetQualitySettings,
                    avatarRoot: generatedAssets.Root);
            Avatar? importedAvatar = avatarImport.Avatar;
            GameObject? importedHumanoidProxyRoot = avatarImport.ProxyRoot;
            transaction.Track(importedAvatar);
            transaction.Track(importedHumanoidProxyRoot, hierarchyRoot: true);
            MmdPmxImportFaultInjection.ThrowIfRequested(ctx.assetPath, MmdPmxImportStage.HumanoidAvatarCreated);
            string avatarReadiness = avatarImport.Readiness;
            string avatarDiagnostic = avatarImport.Diagnostic;

            if (animationType == MmdPmxAnimationType.Humanoid && importedHumanoidProxyRoot != null)
            {
                importedHumanoidProxyRoot.transform.SetParent(
                    generatedAssets.Root.transform,
                    worldPositionStays: false);
                ClearImportHierarchyHideFlags(importedHumanoidProxyRoot.transform);
                transaction.AdoptIntoHierarchy(importedHumanoidProxyRoot);
            }
            MmdPmxImportFaultInjection.ThrowIfRequested(ctx.assetPath, MmdPmxImportStage.HumanoidProxyParented);

            asset.ApplyHumanoidAvatarImportSummary(
                animationType.ToString(),
                importedAvatar,
                avatarReadiness,
                avatarDiagnostic,
                avatarImport.MappingDiagnostics);

            MmdPmxGenericAvatarImportBuilder.MmdPmxGenericAvatarImportResult genericAvatarImport =
                MmdPmxGenericAvatarImportBuilder.TryBuildGenericAvatar(
                    generatedAssets.Root,
                    model.name,
                    shouldBuildGeneric: animationType == MmdPmxAnimationType.Generic,
                    animationTypeLabel: animationType.ToString(),
                    rootMotionTransformName: string.Empty);
            Avatar? genericAvatar = genericAvatarImport.Avatar;
            transaction.Track(genericAvatar);
            MmdPmxImportFaultInjection.ThrowIfRequested(ctx.assetPath, MmdPmxImportStage.GenericAvatarCreated);

            ConfigureImportedAnimator(
                generatedAssets.Root,
                animationType,
                importedAvatar,
                genericAvatar);
            ConfigureImportedPlaybackController(
                generatedAssets.Root,
                asset,
                animationType,
                importedAvatar,
                importedHumanoidProxyRoot,
                avatarReadiness,
                avatarImport.RetargetBindings,
                avatarImport.AppendBindings);
            MmdPmxImportFaultInjection.ThrowIfRequested(ctx.assetPath, MmdPmxImportStage.HierarchyConfigured);

            if (animationType == MmdPmxAnimationType.Generic && genericAvatar == null)
            {
                ctx.LogImportWarning(genericAvatarImport.Diagnostic);
            }

            if (textureBindingSummary.MissingReferenceCount > 0)
            {
                ctx.LogImportWarning(
                    $"PMX import has {textureBindingSummary.MissingReferenceCount} unresolved texture reference(s). See the PMX asset Material Reference Summary for the first sample.");
            }

            MmdPmxImportFaultInjection.ThrowIfRequested(ctx.assetPath, MmdPmxImportStage.BeforeSubAssetRegistration);
            transaction.TransferToContext(ctx, "PMX", asset);
            MmdPmxImportFaultInjection.ThrowIfRequested(ctx.assetPath, MmdPmxImportStage.PmxSubAssetRegistered);
            transaction.TransferToContext(ctx, "Mesh", importedMesh);
            for (int i = 0; i < importedMaterials.Length; i++)
            {
                if (!HasMaterialRemap(materialRemaps, i))
                {
                    transaction.TransferToContext(ctx, "Material_" + i, importedMaterials[i]);
                }
            }
            for (int i = 0; i < textureBindingSummary.OwnedSubAssets.Count; i++)
            {
                transaction.TransferToContext(
                    ctx,
                    textureBindingSummary.OwnedSubAssets[i].Identifier,
                    textureBindingSummary.OwnedSubAssets[i].Texture);
            }
            MmdPmxImportFaultInjection.ThrowIfRequested(ctx.assetPath, MmdPmxImportStage.MaterialsRegistered);
            transaction.TransferToContext(ctx, "Hierarchy", generatedAssets.Root);
            MmdPmxImportFaultInjection.ThrowIfRequested(ctx.assetPath, MmdPmxImportStage.HierarchyRegistered);

            if (importedAvatar != null)
            {
                transaction.TransferToContext(ctx, "Avatar", importedAvatar);
            }

            if (genericAvatar != null)
            {
                transaction.TransferToContext(ctx, "GenericAvatar", genericAvatar);
            }

            ctx.SetMainObject(generatedAssets.Root);
            MmdPmxImportFaultInjection.ThrowIfRequested(ctx.assetPath, MmdPmxImportStage.MainObjectSet);

            transaction.Complete();
        }

        private static bool HasMaterialRemap(Material[]? remaps, int slot)
        {
            return remaps != null && slot >= 0 && slot < remaps.Length && remaps[slot] != null;
        }

        private static float NormalizeImportScale(float value)
        {
            return float.IsFinite(value) && value > 0.0f ? value : MmdPmxAsset.DefaultImportScale;
        }

        private static MmdMaterialPreset MapMaterialPreset(MmdPmxShaderPreset value)
        {
            return value switch
            {
                MmdPmxShaderPreset.MmdBasicUrpToon => MmdMaterialPreset.MmdToon,
                MmdPmxShaderPreset.UrpLit => MmdMaterialPreset.UrpLit,
                MmdPmxShaderPreset.MmdToonLit => MmdMaterialPreset.MmdToonLit,
                _ => MmdMaterialPreset.MmdToon
            };
        }

        private static string GetShaderPresetDisplayName(MmdPmxShaderPreset value)
        {
            return value switch
            {
                MmdPmxShaderPreset.MmdBasicUrpToon => "MMD Basic Toon",
                MmdPmxShaderPreset.MmdToonLit => "MMD URP Toon",
                _ => "URP Lit",
            };
        }

        private void ApplyMaterialOverrideAsset(
            AssetImportContext ctx,
            MmdRenderingDescriptor renderingDescriptor,
            Material[] importedMaterials)
        {
            if (materialOverrideAsset == null)
            {
                return;
            }

            string overrideAssetPath = AssetDatabase.GetAssetPath(materialOverrideAsset);
            if (!string.IsNullOrEmpty(overrideAssetPath))
            {
                ctx.DependsOnSourceAsset(overrideAssetPath);
            }

            MmdMaterialOverrideApplier.ApplyToRenderingDescriptor(materialOverrideAsset, renderingDescriptor);
            MmdMaterialOverrideApplier.Apply(materialOverrideAsset, importedMaterials);
        }

        private MmdPmxModelPreset ResolveModelPresetForImport(MmdModelDefinition model)
        {
            if (!modelPresetAutoAssigned && modelPreset == MmdPmxModelPreset.Custom)
            {
                modelPreset = MmdPmxModelPresetAutoDetector.Detect(model);
                modelPresetAutoAssigned = true;
            }

            return modelPreset;
        }

        private static Animator? ConfigureImportedAnimator(
            GameObject root,
            MmdPmxAnimationType importedAnimationType,
            Avatar? humanoidAvatar,
            Avatar? genericAvatar)
        {
            if (importedAnimationType == MmdPmxAnimationType.None)
            {
                return null;
            }

            Animator? animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                animator = root.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = null;
            animator.avatar = importedAnimationType == MmdPmxAnimationType.Humanoid
                ? humanoidAvatar
                : genericAvatar;
            animator.applyRootMotion = true;
            if (importedAnimationType == MmdPmxAnimationType.Humanoid &&
                root.GetComponent<MmdHumanoidRootMotionDriver>() == null)
            {
                root.AddComponent<MmdHumanoidRootMotionDriver>();
            }
            return animator;
        }

        private static MmdUnityPlaybackController ConfigureImportedPlaybackController(
            GameObject root,
            MmdPmxAsset pmxAsset,
            MmdPmxAnimationType importedAnimationType,
            Avatar? humanoidAvatar,
            GameObject? proxyRoot,
            string avatarReadiness,
            System.Collections.Generic.IReadOnlyList<MmdHumanoidRetargetBinding> retargetBindings,
            System.Collections.Generic.IReadOnlyList<MmdHumanoidAppendTransformBinding> appendBindings)
        {
            MmdUnityPlaybackController controller = root.GetComponent<MmdUnityPlaybackController>();
            if (controller == null)
            {
                controller = root.AddComponent<MmdUnityPlaybackController>();
            }

            controller.ConfigureModelAsset(pmxAsset);

            if (importedAnimationType != MmdPmxAnimationType.Humanoid
                || humanoidAvatar == null
                || !humanoidAvatar.isHuman
                || proxyRoot == null
                || !string.Equals(avatarReadiness, MmdHumanoidMappingReadiness.Ready, System.StringComparison.Ordinal))
            {
                return controller;
            }

            controller.ConfigureHumanoidRetarget(proxyRoot.transform, retargetBindings, appendBindings);
            return controller;
        }

        private static void ClearImportHierarchyHideFlags(Transform root)
        {
            root.gameObject.hideFlags = HideFlags.None;
            root.hideFlags = HideFlags.None;
            foreach (Transform child in root)
            {
                ClearImportHierarchyHideFlags(child);
            }
        }
    }
}
