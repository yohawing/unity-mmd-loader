#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mmd.UnityIntegration;
using Object = UnityEngine.Object;

namespace Mmd.Editor
{
    public static class MmdPmxPrefabExporter
    {
        public const string MenuPath = "Assets/MMD Loader/Create Prefab from PMX";

        private const string MeshSuffix = "_mesh.asset";
        private const string MaterialSuffix = "_material";

        [MenuItem(MenuPath)]
        public static void CreateSelectedPmxPrefabFromMenu()
        {
            if (Selection.activeObject is not MmdPmxAsset pmxAsset)
            {
                EditorUtility.DisplayDialog("MMD PMX Prefab Export Failed", "Select one imported PMX asset.", "OK");
                return;
            }

            CreatePrefabWithFeedback(pmxAsset);
        }

        public static MmdPmxPrefabExportResult? CreatePrefabWithFeedback(MmdPmxAsset pmxAsset)
        {
            try
            {
                MmdPmxPrefabExportResult result = CreatePrefab(pmxAsset, GetDefaultPrefabPath(pmxAsset));
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabAssetPath);
                Debug.LogFormat(
                    "Created MMD PMX prefab: {0}; meshAssets={1}; materialAssets={2}; textureCopyPolicy={3}",
                    result.PrefabAssetPath,
                    result.MeshAssetPaths.Length,
                    result.MaterialAssetPaths.Length,
                    result.TextureCopyPolicy);
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to create MMD PMX prefab:" + Environment.NewLine + ex);
                EditorUtility.DisplayDialog("MMD PMX Prefab Export Failed", ex.Message, "OK");
                return null;
            }
        }

        [MenuItem(MenuPath, true)]
        public static bool ValidateCreateSelectedPmxPrefabFromMenu()
        {
            return Selection.activeObject is MmdPmxAsset;
        }

        public static string GetDefaultPrefabPath(MmdPmxAsset pmxAsset)
        {
            if (pmxAsset == null)
            {
                throw new ArgumentNullException(nameof(pmxAsset));
            }

            string assetPath = AssetDatabase.GetAssetPath(pmxAsset);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException("PMX asset must be imported under the AssetDatabase.", nameof(pmxAsset));
            }

            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "Assets";
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            return AssetDatabase.GenerateUniqueAssetPath(directory + "/" + fileName + ".prefab");
        }

        public static MmdPmxPrefabExportResult CreatePrefab(MmdPmxAsset pmxAsset, string prefabPath)
        {
            if (pmxAsset == null)
            {
                throw new ArgumentNullException(nameof(pmxAsset));
            }

            string normalizedPrefabPath = NormalizePrefabPath(prefabPath);
            string outputDirectory = Path.GetDirectoryName(normalizedPrefabPath)?.Replace('\\', '/') ?? "Assets";
            EnsureAssetDirectory(outputDirectory);

            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene exportScene = EditorSceneManager.NewPreviewScene();

            MmdUnityModelInstance? instance = null;
            try
            {
                MmdEditorPmxSceneLoadResult loadResult = MmdEditorVerificationFacade.LoadPmxIntoScene(pmxAsset);
                instance = loadResult.Instance;
                SceneManager.MoveGameObjectToScene(instance.Root, exportScene);
                ConfigurePrefabModelSource(instance.Root, pmxAsset);
                string baseName = Path.GetFileNameWithoutExtension(normalizedPrefabPath);
                string[] meshAssetPaths = CreateMeshAssets(instance, outputDirectory, baseName);
                string[] materialAssetPaths = CreateMaterialAssets(instance, outputDirectory, baseName);
                RebindPersistentAssets(instance, meshAssetPaths, materialAssetPaths);

                var provenance = instance.Root.GetComponent<MmdPmxPrefabProvenance>()
                    ?? instance.Root.AddComponent<MmdPmxPrefabProvenance>();
                provenance.Initialize(
                    pmxAsset,
                    normalizedPrefabPath,
                    meshAssetPaths,
                    materialAssetPaths,
                    instance.VertexCount,
                    instance.Materials.Length,
                    instance.LoadedDiffuseTextureCount,
                    instance.LoadedSphereTextureCount,
                    instance.LoadedToonTextureCount,
                    instance.MissingTextureReferenceCount,
                    instance.UnsupportedTextureReferenceCount);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance.Root, normalizedPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException("Prefab export failed: " + normalizedPrefabPath);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(normalizedPrefabPath, ImportAssetOptions.ForceUpdate);

                return new MmdPmxPrefabExportResult(
                    normalizedPrefabPath,
                    outputDirectory,
                    meshAssetPaths,
                    materialAssetPaths,
                    Array.Empty<string>(),
                    MmdPmxPrefabProvenance.TextureCopyPolicy,
                    pmxAsset.SourceId,
                    pmxAsset.SourcePath,
                    instance.VertexCount,
                    instance.Materials.Length,
                    instance.LoadedDiffuseTextureCount,
                    instance.LoadedSphereTextureCount,
                    instance.LoadedToonTextureCount,
                    instance.MissingTextureReferenceCount,
                    instance.UnsupportedTextureReferenceCount);
            }
            finally
            {
                if (instance?.Root != null)
                {
                    Object.DestroyImmediate(instance.Root);
                }

                if (previousActiveScene.IsValid())
                {
                    EditorSceneManager.SetActiveScene(previousActiveScene);
                }

                if (exportScene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(exportScene);
                }
            }
        }

        private static void ConfigurePrefabModelSource(GameObject root, MmdPmxAsset pmxAsset)
        {
            MmdUnityPlaybackController controller = root.GetComponent<MmdUnityPlaybackController>()
                ?? root.AddComponent<MmdUnityPlaybackController>();
            controller.ConfigureModelAsset(pmxAsset);
        }

        private static string[] CreateMeshAssets(MmdUnityModelInstance instance, string outputDirectory, string baseName)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(outputDirectory + "/" + baseName + MeshSuffix);
            Mesh mesh = Object.Instantiate(instance.Mesh);
            mesh.name = baseName + "_mesh";
            mesh.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return new[] { path };
        }

        private static string[] CreateMaterialAssets(MmdUnityModelInstance instance, string outputDirectory, string baseName)
        {
            var paths = new string[instance.Materials.Length];
            for (int i = 0; i < instance.Materials.Length; i++)
            {
                Material material = new(instance.Materials[i])
                {
                    name = baseName + MaterialSuffix + "_" + i,
                    hideFlags = HideFlags.None
                };
                ClearNonAssetTextures(material);
                string path = AssetDatabase.GenerateUniqueAssetPath(outputDirectory + "/" + material.name + ".mat");
                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                paths[i] = path;
            }

            return paths;
        }

        private static void RebindPersistentAssets(
            MmdUnityModelInstance instance,
            string[] meshAssetPaths,
            string[] materialAssetPaths)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPaths[0]);
            if (mesh == null)
            {
                throw new InvalidOperationException("Created mesh asset was not found: " + meshAssetPaths[0]);
            }

            var materials = new Material[materialAssetPaths.Length];
            for (int i = 0; i < materialAssetPaths.Length; i++)
            {
                materials[i] = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPaths[i]);
                if (materials[i] == null)
                {
                    throw new InvalidOperationException("Created material asset was not found: " + materialAssetPaths[i]);
                }
            }

            if (instance.SkinnedMeshRenderer != null)
            {
                instance.SkinnedMeshRenderer.sharedMesh = mesh;
                instance.SkinnedMeshRenderer.sharedMaterials = materials;
            }

            if (instance.MeshRenderer != null)
            {
                MeshFilter? meshFilter = instance.MeshRenderer.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    meshFilter.sharedMesh = mesh;
                }

                instance.MeshRenderer.sharedMaterials = materials;
            }
        }

        private static void ClearNonAssetTextures(Material material)
        {
            ClearNonAssetTexture(material, "_BaseMap");
            ClearNonAssetTexture(material, "_MainTex");
            ClearNonAssetTexture(material, "_SphereMap");
            ClearNonAssetTexture(material, "_ToonMap");
        }

        private static void ClearNonAssetTexture(Material material, string propertyName)
        {
            if (!material.HasProperty(propertyName))
            {
                return;
            }

            Texture? texture = material.GetTexture(propertyName);
            if (texture != null && !AssetDatabase.Contains(texture))
            {
                material.SetTexture(propertyName, null);
            }
        }

        private static string NormalizePrefabPath(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                throw new ArgumentException("Prefab path is required.", nameof(prefabPath));
            }

            if (Path.IsPathRooted(prefabPath))
            {
                throw new ArgumentException("Prefab path must be a project-relative Assets/*.prefab path.", nameof(prefabPath));
            }

            string normalized = prefabPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) ||
                !normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Prefab path must be an Assets/*.prefab path.", nameof(prefabPath));
            }

            string fullProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullAssetsRoot = Path.GetFullPath(Application.dataPath);
            string fullPath = Path.GetFullPath(Path.Combine(fullProjectRoot, normalized));
            if (!fullPath.StartsWith(fullAssetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Prefab path must stay inside the Unity Assets directory.", nameof(prefabPath));
            }

            return normalized;
        }

        private static void EnsureAssetDirectory(string assetDirectory)
        {
            if (AssetDatabase.IsValidFolder(assetDirectory))
            {
                return;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Directory.CreateDirectory(Path.Combine(projectRoot, assetDirectory));
            AssetDatabase.Refresh();
        }
    }

    public sealed class MmdPmxPrefabExportResult
    {
        internal MmdPmxPrefabExportResult(
            string prefabAssetPath,
            string outputDirectory,
            string[] meshAssetPaths,
            string[] materialAssetPaths,
            string[] textureAssetPaths,
            string textureCopyPolicy,
            string pmxSourceId,
            string pmxSourcePath,
            int vertexCount,
            int materialCount,
            int loadedDiffuseTextureCount,
            int loadedSphereTextureCount,
            int loadedToonTextureCount,
            int missingTextureCount,
            int unsupportedTextureCount)
        {
            PrefabAssetPath = prefabAssetPath;
            OutputDirectory = outputDirectory;
            MeshAssetPaths = meshAssetPaths;
            MaterialAssetPaths = materialAssetPaths;
            TextureAssetPaths = textureAssetPaths;
            TextureCopyPolicy = textureCopyPolicy;
            PmxSourceId = pmxSourceId;
            PmxSourcePath = pmxSourcePath;
            VertexCount = vertexCount;
            MaterialCount = materialCount;
            LoadedDiffuseTextureCount = loadedDiffuseTextureCount;
            LoadedSphereTextureCount = loadedSphereTextureCount;
            LoadedToonTextureCount = loadedToonTextureCount;
            MissingTextureCount = missingTextureCount;
            UnsupportedTextureCount = unsupportedTextureCount;
        }

        public string PrefabAssetPath { get; }

        public string OutputDirectory { get; }

        public string[] MeshAssetPaths { get; }

        public string[] MaterialAssetPaths { get; }

        public string[] TextureAssetPaths { get; }

        public string TextureCopyPolicy { get; }

        public string PmxSourceId { get; }

        public string PmxSourcePath { get; }

        public int VertexCount { get; }

        public int MaterialCount { get; }

        public int LoadedDiffuseTextureCount { get; }

        public int LoadedSphereTextureCount { get; }

        public int LoadedToonTextureCount { get; }

        public int MissingTextureCount { get; }

        public int UnsupportedTextureCount { get; }
    }
}
