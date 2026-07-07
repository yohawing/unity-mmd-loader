#nullable enable

using UnityEngine;
using Mmd.Parser;
using Mmd;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// Editor-independent helper for constructing and initializing the primary
    /// MmdPmxAsset from parsed PMX bytes, import settings, parse summary, and the
    /// generated asset cache (MmdUnityModelInstance from MmdPmxImportAssetCacheBuilder).
    /// Computes hierarchy/renderer/bone-binding readiness diagnostics from the
    /// importer-owned hierarchy before Initialize.
    /// </summary>
    /// <remarks>
    /// Project texture binding and sub-asset registration are intentionally kept
    /// in the importer shell.
    /// </remarks>
    public static class MmdPmxImportedAssetBuilder
    {
        /// <summary>
        /// Creates a fresh MmdPmxAsset (ScriptableObject), computes readiness from
        /// the generated hierarchy/root and bone count, then initializes it with
        /// the importer parameters. Returns the asset ready for sub-asset registration.
        /// </summary>
        public static MmdPmxAsset CreateAndInitializeImportedAsset(
            byte[] bytes,
            string assetPath,
            string resolvedSourcePath,
            float importScale,
            string modelPreset,
            string meshGenerationMode,
            string materialTexturePolicy,
            string shaderPreset,
            MmdPmxParseSummary parseSummary,
            MmdUnityModelInstance generatedAssets,
            Material[] materialRemaps,
            string animationType,
            MmdMaterialOverrideAsset? materialOverrideAsset = null)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new System.ArgumentException("PMX asset bytes are required.", nameof(bytes));
            }
            if (generatedAssets == null)
            {
                throw new System.ArgumentNullException(nameof(generatedAssets));
            }

            // Compute hierarchy/renderer/bone-binding readiness from generated assets
            // before passing to Initialize. This is real computed evidence, not hardcoded.
            MmdPmxAsset.ComputeHierarchyReadiness(
                generatedAssets.Root,
                parseSummary.BoneCount,
                out MmdImportReadiness hierarchyReadiness,
                out MmdImportReadiness rendererReadiness,
                out MmdImportReadiness boneBindingReadiness,
                out string hierarchyDiagnostic,
                out string rendererDiagnostic,
                out string boneBindingDiagnostic);

            MmdPmxAsset asset = MmdPmxAsset.CreateInstance<MmdPmxAsset>();
            asset.Initialize(
                bytes,
                assetPath,
                resolvedSourcePath,
                importScale,
                modelPreset,
                meshGenerationMode,
                materialTexturePolicy,
                shaderPreset,
                parseSummary,
                generatedAssets.Mesh,
                generatedAssets.Materials,
                materialRemaps,
                generatedAssets.Root,
                hierarchyReadiness,
                rendererReadiness,
                boneBindingReadiness,
                hierarchyDiagnostic,
                rendererDiagnostic,
                boneBindingDiagnostic,
                animationType,
                materialOverrideAsset);

            return asset;
        }
    }
}
