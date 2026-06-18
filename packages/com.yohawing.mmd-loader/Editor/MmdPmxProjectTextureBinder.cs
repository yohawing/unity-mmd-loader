#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using Mmd.Parser;
using Mmd.Rendering;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    /// <summary>Bind existing project Texture2D assets to importer-generated Material sub-assets.</summary>
    internal static class MmdPmxProjectTextureBinder
    {
        internal static void BindProjectTextureAssetsToMaterials(MmdModelDefinition model, string pmxAssetPath, Material[] materials, MmdRenderingDescriptor? descriptor = null, AssetImportContext? ctx = null)
        {
            if (model?.materials == null || materials == null)
            {
                return;
            }

            // Shared toon ramps are reused across materials; cache the decoded sub-asset per index
            // so a model with many shared-toon materials adds at most one texture per ramp.
            var sharedToonSubAssets = new Dictionary<int, Texture2D>();

            int count = System.Math.Min(model.materials.Count, materials.Length);
            for (int i = 0; i < count; i++)
            {
                MmdMaterialDefinition matDef = model.materials[i];
                Material mat = materials[i];
                if (mat == null)
                {
                    continue;
                }

                BindOneTextureReference(matDef.texture, pmxAssetPath, mat, ctx, "_BaseMap", "_MainTex");
                MmdUnityMaterialBuilder.ApplyDiffuseBoundSideEffects(mat);

                BindOneTextureReference(matDef.sphereTexture, pmxAssetPath, mat, ctx, "_SphereMap");

                bool toonBound = BindOneTextureReference(matDef.toonTexture, pmxAssetPath, mat, ctx, "_ToonMap");
                if (!toonBound &&
                    ctx != null &&
                    matDef.toonShared &&
                    MmdSharedToonTextures.IsSharedToonIndex(matDef.sharedToonIndex))
                {
                    // MMD shared toon (toon01..toon10) carries only an index, not a path, so the
                    // path-based bind above never resolves it. Persist the built-in GoldenOracle
                    // ramp as a sub-asset so the imported material shades through the toon ramp
                    // instead of falling back to flat lighting.
                    Texture2D? sharedToon = GetOrCreateSharedToonSubAsset(matDef.sharedToonIndex, ctx, sharedToonSubAssets);
                    if (sharedToon != null && mat.HasProperty("_ToonMap"))
                    {
                        mat.SetTexture("_ToonMap", sharedToon);
                        toonBound = true;
                    }
                }

                if (toonBound && mat.HasProperty("_ToonMapBound"))
                {
                    // The runtime builder sets this when it binds a toon texture; the importer's
                    // separate project-texture bind must do the same or the shader treats the
                    // material as having no toon ramp (flat lighting).
                    mat.SetFloat("_ToonMapBound", 1.0f);
                }

                // Re-run transparency classification using the on-disk diffuse texture alpha, so
                // that texture-alpha-driven transparency (alphaTest/alphaBlend) is preserved on
                // imported models even though sourcePath:null suppressed it during initial build.
                if (descriptor != null && i < descriptor.materials.Count)
                {
                    MmdMaterialDescriptor source = descriptor.materials[i];
                    string? diffuseAssetPath = null;
                    Texture2D? decodedAlphaTexture = null;
                    if (!string.IsNullOrWhiteSpace(matDef.texture) &&
                        MmdAssetPathUtility.TryResolveProjectRelativeAssetPathCandidate(pmxAssetPath, matDef.texture, out string resolvedAssetPath) &&
                        System.IO.File.Exists(resolvedAssetPath))
                    {
                        diffuseAssetPath = resolvedAssetPath;
                        try
                        {
                            byte[] texBytes = System.IO.File.ReadAllBytes(resolvedAssetPath);
                            decodedAlphaTexture = MmdRuntimeTextureResolver.DecodeTextureBytes(
                                texBytes,
                                System.IO.Path.GetExtension(resolvedAssetPath),
                                System.IO.Path.GetFileNameWithoutExtension(resolvedAssetPath));
                        }
                        catch (System.IO.IOException)
                        {
                            decodedAlphaTexture = null;
                        }
                    }

                    MmdUnityMaterialBuilder.ReapplyImportedMaterialTransparency(
                        mat, descriptor, source, i, matDef.texture, diffuseAssetPath, decodedAlphaTexture);

                    if (decodedAlphaTexture != null)
                    {
                        UnityEngine.Object.DestroyImmediate(decodedAlphaTexture);
                    }
                }
            }
        }

        private static Texture2D? GetOrCreateSharedToonSubAsset(
            int sharedToonIndex,
            AssetImportContext ctx,
            Dictionary<int, Texture2D> cache)
        {
            if (cache.TryGetValue(sharedToonIndex, out Texture2D existing))
            {
                return existing;
            }

            Texture2D? texture = MmdSharedToonTextures.TryCreateSharedToonTexture(sharedToonIndex);
            if (texture == null)
            {
                return null;
            }

            texture.hideFlags = HideFlags.None;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = $"MMD Shared Toon {sharedToonIndex + 1:00}";
            ctx.AddObjectToAsset($"SharedToon_{sharedToonIndex:00}", texture);
            cache[sharedToonIndex] = texture;
            return texture;
        }

        private static bool BindOneTextureReference(string? reference, string pmxAssetPath, Material material, AssetImportContext? ctx, params string[] propertyNames)
        {
            if (string.IsNullOrWhiteSpace(reference) || material == null)
            {
                return false;
            }

            string resolvedReference = reference!;
            if (!MmdAssetPathUtility.TryResolveProjectRelativeAssetPathCandidate(pmxAssetPath, resolvedReference, out string assetPath))
            {
                return false;
            }

            if (ctx != null)
            {
                ctx.DependsOnSourceAsset(assetPath);
            }

            Texture2D? tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
            {
                return false;
            }

            bool bound = false;
            foreach (string prop in propertyNames)
            {
                if (material.HasProperty(prop))
                {
                    material.SetTexture(prop, tex);
                    bound = true;
                }
            }

            return bound;
        }
    }
}
