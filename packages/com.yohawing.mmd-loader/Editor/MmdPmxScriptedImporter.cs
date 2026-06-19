#nullable enable

using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using Mmd.Parser;
using Mmd.UnityIntegration;
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
        MmdBasicUrpToon = 0
    }

    [ScriptedImporter(11, "pmx")]
    public sealed class MmdPmxScriptedImporter : ScriptedImporter
    {
        [SerializeField] private float importScale = 1.0f;
        [SerializeField] private MmdPmxModelPreset modelPreset = MmdPmxModelPreset.Custom;
        [SerializeField] private MmdPmxMeshGenerationMode meshGenerationMode = MmdPmxMeshGenerationMode.SingleMesh;
        [SerializeField] private MmdPmxMaterialTexturePolicy materialTexturePolicy = MmdPmxMaterialTexturePolicy.ResolveReferencesOnly;
        [SerializeField] private MmdPmxAnimationType animationType = MmdPmxAnimationType.Generic;
        [SerializeField] private MmdPmxShaderPreset shaderPreset = MmdPmxShaderPreset.MmdBasicUrpToon;
        [SerializeField] private Material[] materialRemaps = System.Array.Empty<Material>();
        [SerializeField] private MmdHumanoidBoneMappingOverride[] humanoidBoneMappingOverrides =
            System.Array.Empty<MmdHumanoidBoneMappingOverride>();

        public float ImportScale => NormalizeImportScale(importScale);

        public MmdPmxModelPreset ModelPreset => modelPreset;

        public MmdPmxMeshGenerationMode MeshGenerationMode => meshGenerationMode;

        public MmdPmxMaterialTexturePolicy MaterialTexturePolicy => materialTexturePolicy;

        public MmdPmxAnimationType AnimationType => animationType;

        public MmdPmxShaderPreset ShaderPreset => shaderPreset;

        public Material[] MaterialRemaps => materialRemaps;

        public MmdHumanoidBoneMappingOverride[] HumanoidBoneMappingOverrides => humanoidBoneMappingOverrides;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            byte[] bytes = File.ReadAllBytes(ctx.assetPath);
            string resolvedSourcePath = MmdAssetPathUtility.ResolveAssetSourcePath(ctx.assetPath);
            MmdPmxParsePayload payload = MmdPmxParsePayload.FromBytes(bytes);
            MmdModelDefinition model = payload.Model;
            MmdPmxParseSummary parseSummary = payload.ParseSummary;

            MmdUnityModelInstance? generatedAssets = null;
            bool hierarchyAdded = false;
            try
            {
                generatedAssets = MmdPmxImportAssetCacheBuilder.CreateImportedAssetCache(model, ImportScale);
                Mesh importedMesh = generatedAssets.Mesh;
                Material[] importedMaterials = generatedAssets.Materials;

                MmdPmxProjectTextureBinder.BindProjectTextureAssetsToMaterials(model, ctx.assetPath, importedMaterials, generatedAssets.RenderingDescriptor, ctx);

                MmdPmxAsset asset = MmdPmxImportedAssetBuilder.CreateAndInitializeImportedAsset(
                    bytes,
                    ctx.assetPath,
                    resolvedSourcePath,
                    ImportScale,
                    modelPreset.ToString(),
                    meshGenerationMode.ToString(),
                    materialTexturePolicy.ToString(),
                    shaderPreset.ToString(),
                    parseSummary,
                    generatedAssets,
                    materialRemaps,
                    animationType.ToString());

                MmdPmxHumanoidAvatarImportBuilder.MmdPmxHumanoidAvatarImportResult avatarImport =
                    MmdPmxHumanoidAvatarImportBuilder.TryBuildHumanoidAvatar(
                        asset,
                        model.name,
                        shouldBuildHumanoid: animationType == MmdPmxAnimationType.Humanoid,
                        animationTypeLabel: animationType.ToString(),
                        mappingOverrides: humanoidBoneMappingOverrides);
                Avatar? importedAvatar = avatarImport.Avatar;
                string avatarReadiness = avatarImport.Readiness;
                string avatarDiagnostic = avatarImport.Diagnostic;

                asset.ApplyHumanoidAvatarImportSummary(
                    animationType.ToString(),
                    importedAvatar,
                    avatarReadiness,
                    avatarDiagnostic,
                    avatarImport.MappingDiagnostics);

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
            return float.IsFinite(value) && value > 0.0f ? value : 1.0f;
        }
    }
}
