#nullable enable

using UnityEngine;

namespace Mmd
{
    public sealed class MmdPmxPrefabProvenance : MonoBehaviour
    {
        public const string ExportOperationPolicy = "prefab-mesh-material-assets-explicit-export-only";
        public const string TextureCopyPolicy = "do-not-copy-source-textures-by-default";

        [SerializeField] private MmdPmxAsset? sourceAsset;
        [SerializeField] private string sourceId = string.Empty;
        [SerializeField] private string sourcePath = string.Empty;
        [SerializeField] private string prefabAssetPath = string.Empty;
        [SerializeField] private string[] meshAssetPaths = System.Array.Empty<string>();
        [SerializeField] private string[] materialAssetPaths = System.Array.Empty<string>();
        [SerializeField] private string[] textureAssetPaths = System.Array.Empty<string>();
        [SerializeField] private string exportOperationPolicy = ExportOperationPolicy;
        [SerializeField] private string textureCopyPolicy = TextureCopyPolicy;
        [SerializeField] private int vertexCount;
        [SerializeField] private int materialCount;
        [SerializeField] private int loadedDiffuseTextureCount;
        [SerializeField] private int loadedSphereTextureCount;
        [SerializeField] private int loadedToonTextureCount;
        [SerializeField] private int missingTextureCount;
        [SerializeField] private int unsupportedTextureCount;

        public MmdPmxAsset? SourceAsset => sourceAsset;

        public string SourceId => sourceId;

        public string SourcePath => sourcePath;

        public string PrefabAssetPath => prefabAssetPath;

        public string[] MeshAssetPaths => meshAssetPaths;

        public string[] MaterialAssetPaths => materialAssetPaths;

        public string[] TextureAssetPaths => textureAssetPaths;

        public string CurrentExportOperationPolicy => exportOperationPolicy;

        public string CurrentTextureCopyPolicy => textureCopyPolicy;

        public int VertexCount => vertexCount;

        public int MaterialCount => materialCount;

        public int LoadedDiffuseTextureCount => loadedDiffuseTextureCount;

        public int LoadedSphereTextureCount => loadedSphereTextureCount;

        public int LoadedToonTextureCount => loadedToonTextureCount;

        public int MissingTextureCount => missingTextureCount;

        public int UnsupportedTextureCount => unsupportedTextureCount;

        public void Initialize(
            MmdPmxAsset pmxAsset,
            string prefabPath,
            string[] exportedMeshAssetPaths,
            string[] exportedMaterialAssetPaths,
            int exportedVertexCount,
            int exportedMaterialCount,
            int exportedLoadedDiffuseTextureCount,
            int exportedLoadedSphereTextureCount,
            int exportedLoadedToonTextureCount,
            int exportedMissingTextureCount,
            int exportedUnsupportedTextureCount)
        {
            sourceAsset = pmxAsset;
            sourceId = pmxAsset != null ? pmxAsset.SourceId : string.Empty;
            sourcePath = pmxAsset != null ? pmxAsset.SourcePath : string.Empty;
            prefabAssetPath = prefabPath ?? string.Empty;
            meshAssetPaths = exportedMeshAssetPaths ?? System.Array.Empty<string>();
            materialAssetPaths = exportedMaterialAssetPaths ?? System.Array.Empty<string>();
            textureAssetPaths = System.Array.Empty<string>();
            exportOperationPolicy = ExportOperationPolicy;
            textureCopyPolicy = TextureCopyPolicy;
            vertexCount = exportedVertexCount;
            materialCount = exportedMaterialCount;
            loadedDiffuseTextureCount = exportedLoadedDiffuseTextureCount;
            loadedSphereTextureCount = exportedLoadedSphereTextureCount;
            loadedToonTextureCount = exportedLoadedToonTextureCount;
            missingTextureCount = exportedMissingTextureCount;
            unsupportedTextureCount = exportedUnsupportedTextureCount;
        }
    }
}
