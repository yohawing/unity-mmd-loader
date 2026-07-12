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
        UrpLit = 1
    }

    [ScriptedImporter(25, "pmx")]
    public sealed class MmdPmxScriptedImporter : ScriptedImporter
    {
        [SerializeField] private float importScale = MmdPmxAsset.DefaultImportScale;
        [SerializeField] private MmdPmxModelPreset modelPreset = MmdPmxModelPreset.Custom;
        [SerializeField] private bool modelPresetAutoAssigned;
        [SerializeField] private MmdPmxMeshGenerationMode meshGenerationMode = MmdPmxMeshGenerationMode.SingleMesh;
        [SerializeField] private MmdPmxMaterialTexturePolicy materialTexturePolicy = MmdPmxMaterialTexturePolicy.ResolveReferencesOnly;
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

        public MmdPmxMeshGenerationMode MeshGenerationMode => meshGenerationMode;

        public MmdPmxMaterialTexturePolicy MaterialTexturePolicy => materialTexturePolicy;

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

            MmdUnityModelInstance? generatedAssets = null;
            bool hierarchyAdded = false;
            try
            {
                generatedAssets = MmdPmxImportAssetCacheBuilder.CreateImportedAssetCache(
                    model,
                    ImportScale,
                    MmdPmxModelPresetAutoDetector.IsCharacter(effectiveModelPreset),
                    MapMaterialPreset(shaderPreset),
                    materialOverrideAsset);
                Mesh importedMesh = generatedAssets.Mesh;
                Material[] importedMaterials = generatedAssets.Materials;

                MmdPmxProjectTextureBindingSummary textureBindingSummary =
                    MmdPmxProjectTextureBinder.BindProjectTextureAssetsToMaterials(
                        model,
                        ctx.assetPath,
                        importedMaterials,
                        generatedAssets.RenderingDescriptor,
                        ctx);

                if (shaderPreset == MmdPmxShaderPreset.UrpLit)
                {
                    MmdPbrTextureConventionScanner.ApplyScannedMaterialOverrides(
                        ctx,
                        model,
                        ctx.assetPath,
                        importedMaterials);
                }

                ApplyMaterialOverrideAsset(ctx, importedMaterials);

                MmdPmxAsset asset = MmdPmxImportedAssetBuilder.CreateAndInitializeImportedAsset(
                    bytes,
                    ctx.assetPath,
                    resolvedSourcePath,
                    ImportScale,
                    effectiveModelPreset.ToString(),
                    meshGenerationMode.ToString(),
                    materialTexturePolicy.ToString(),
                    shaderPreset.ToString(),
                    parseSummary,
                    generatedAssets,
                    materialRemaps,
                    animationType.ToString(),
                    materialOverrideAsset);
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
                        retargetQualitySettings: HumanoidRetargetQualitySettings);
                Avatar? importedAvatar = avatarImport.Avatar;
                GameObject? importedHumanoidProxyRoot = avatarImport.ProxyRoot;
                string avatarReadiness = avatarImport.Readiness;
                string avatarDiagnostic = avatarImport.Diagnostic;

                if (animationType == MmdPmxAnimationType.Humanoid && importedHumanoidProxyRoot != null)
                {
                    importedHumanoidProxyRoot.transform.SetParent(
                        generatedAssets.Root.transform,
                        worldPositionStays: false);
                    ClearImportHierarchyHideFlags(importedHumanoidProxyRoot.transform);
                }

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

                if (animationType == MmdPmxAnimationType.Generic && genericAvatar == null)
                {
                    ctx.LogImportWarning(genericAvatarImport.Diagnostic);
                }

                if (textureBindingSummary.MissingReferenceCount > 0)
                {
                    ctx.LogImportWarning(
                        $"PMX import has {textureBindingSummary.MissingReferenceCount} unresolved texture reference(s). See the PMX asset Material Reference Summary for the first sample.");
                }

                ctx.AddObjectToAsset("PMX", asset);
                ctx.AddObjectToAsset("Mesh", importedMesh);
                for (int i = 0; i < importedMaterials.Length; i++)
                {
                    ctx.AddObjectToAsset("Material_" + i, importedMaterials[i]);
                }
                ctx.AddObjectToAsset("Hierarchy", generatedAssets.Root);

                if (importedAvatar != null)
                {
                    ctx.AddObjectToAsset("Avatar", importedAvatar);
                }

                if (genericAvatar != null)
                {
                    ctx.AddObjectToAsset("GenericAvatar", genericAvatar);
                }

                ctx.SetMainObject(generatedAssets.Root);

                hierarchyAdded = true;
            }
            finally
            {
                if (generatedAssets?.Root != null && !hierarchyAdded)
                {
                    Object.DestroyImmediate(generatedAssets.Root);
                }
            }
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
                _ => MmdMaterialPreset.MmdToon
            };
        }

        private void ApplyMaterialOverrideAsset(AssetImportContext ctx, Material[] importedMaterials)
        {
            if (materialOverrideAsset == null)
            {
                return;
            }

            string overrideAssetPath = AssetDatabase.GetAssetPath(materialOverrideAsset);
            if (string.IsNullOrEmpty(overrideAssetPath))
            {
                return;
            }

            ctx.DependsOnSourceAsset(overrideAssetPath);
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
                || !string.Equals(avatarReadiness, MmdHumanoidSetupAsset.ReadyReadiness, System.StringComparison.Ordinal))
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
