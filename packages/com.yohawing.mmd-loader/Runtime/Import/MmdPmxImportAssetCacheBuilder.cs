#nullable enable

using UnityEngine;
using Mmd.Parser;
using Mmd.Rendering;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// Editor-independent helper for constructing the PMX importer's asset cache
    /// (the MmdUnityModelInstance carrying Mesh / Materials / hierarchy root).
    /// Always forces sourcePath: null into the underlying ModelFactory so that
    /// importer-persistent materials do not embed decoded runtime textures.
    /// The separate BindProjectTextureAssetsToMaterials (Editor-only) step later
    /// binds project Texture2D assets by resolved path.
    /// </summary>
    public static class MmdPmxImportAssetCacheBuilder
    {
        /// <summary>
        /// Creates the importer asset cache (mesh, materials, hierarchy) for the given model.
        /// sourcePath is intentionally forced to null for the importer material sub-asset path.
        /// Mesh and Material assets receive hideFlags=None and stable names based on modelName.
        /// </summary>
        public static MmdUnityModelInstance CreateImportedAssetCache(
            MmdModelDefinition model,
            float importScale,
            bool includeSelfShadowTarget = true,
            MmdMaterialPreset preset = MmdMaterialPreset.MmdToon)
        {
            if (model == null)
            {
                throw new System.ArgumentNullException(nameof(model));
            }

            float scale = importScale;

            MmdUnityModelInstance generatedAssets;
            if (model.bones != null && model.bones.Count > 0)
            {
                generatedAssets = MmdUnityModelFactory.CreateSkinnedModel(
                    model,
                    sourcePath: null,
                    scale,
                    preset,
                    includeSelfShadowTarget);
            }
            else
            {
                generatedAssets = MmdUnityModelFactory.CreateStaticModel(
                    model,
                    sourcePath: null,
                    scale,
                    preset,
                    includeSelfShadowTarget);
            }

            PrepareImportedMeshAsset(generatedAssets.Mesh, model.name);
            PrepareImportedMaterialAssets(generatedAssets.Materials);

            return generatedAssets;
        }

        private static void PrepareImportedMeshAsset(Mesh mesh, string modelName)
        {
            mesh.hideFlags = HideFlags.None;
            mesh.name = string.IsNullOrWhiteSpace(modelName)
                ? "PMX Mesh"
                : modelName + " Mesh";
        }

        private static void PrepareImportedMaterialAssets(Material[] materials)
        {
            if (materials == null)
            {
                return;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    continue;
                }
                material.hideFlags = HideFlags.None;
                if (string.IsNullOrWhiteSpace(material.name))
                {
                    material.name = "PMX Material " + i;
                }
            }
        }
    }
}
