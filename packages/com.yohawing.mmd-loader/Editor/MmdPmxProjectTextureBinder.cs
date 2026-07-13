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
        internal static MmdPmxProjectTextureBindingSummary BindProjectTextureAssetsToMaterials(MmdModelDefinition model, string pmxAssetPath, Material[] materials, MmdRenderingDescriptor? descriptor = null, AssetImportContext? ctx = null)
        {
            if (model?.materials == null || materials == null)
            {
                return MmdPmxProjectTextureBindingSummary.Empty;
            }

            // Shared toon ramps are reused across materials; cache the decoded sub-asset per index
            // so a model with many shared-toon materials adds at most one texture per ramp.
            var sharedToonSubAssets = new Dictionary<int, Texture2D>();
            var summary = new MmdPmxProjectTextureBindingSummaryBuilder();
            var ownedSubAssets = new List<MmdPmxOwnedTextureSubAsset>();
            try
            {
                int count = System.Math.Min(model.materials.Count, materials.Length);
                for (int i = 0; i < count; i++)
                {
                    MmdMaterialDefinition matDef = model.materials[i];
                    Material mat = materials[i];
                    if (mat == null)
                    {
                        continue;
                    }

                summary.Record(i, "diffuse", matDef.texture, BindOneTextureReference(matDef.texture, pmxAssetPath, mat, ctx, "_BaseMap", "_MainTex"));
                MmdUnityMaterialBuilder.ApplyDiffuseBoundSideEffects(mat);

                summary.Record(i, "sphere", matDef.sphereTexture, BindOneTextureReference(matDef.sphereTexture, pmxAssetPath, mat, ctx, "_SphereMap"));

                TextureReferenceBindStatus toonStatus = BindOneTextureReference(matDef.toonTexture, pmxAssetPath, mat, ctx, "_ToonMap");
                bool toonBound = toonStatus == TextureReferenceBindStatus.Resolved;
                if (!toonBound &&
                    ctx != null &&
                    matDef.toonShared &&
                    MmdSharedToonTextures.IsSharedToonIndex(matDef.sharedToonIndex))
                {
                    // MMD shared toon (toon01..toon10) carries only an index, not a path, so the
                    // path-based bind above never resolves it. Persist the built-in GoldenOracle
                    // ramp as a sub-asset so the imported material shades through the toon ramp
                    // instead of falling back to flat lighting.
                    Texture2D? sharedToon = GetOrCreateSharedToonSubAsset(
                        matDef.sharedToonIndex,
                        sharedToonSubAssets,
                        ownedSubAssets);
                    if (sharedToon != null && mat.HasProperty("_ToonMap"))
                    {
                        mat.SetTexture("_ToonMap", sharedToon);
                        toonBound = true;
                    }
                }

                summary.Record(i, "toon", matDef.toonTexture, toonBound ? TextureReferenceBindStatus.Resolved : toonStatus);

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
                            byte[] texBytes = MmdTextureDecodeBudget.Default.ReadFileBytes(resolvedAssetPath);
                            decodedAlphaTexture = MmdRuntimeTextureResolver.DecodeTextureBytes(
                                texBytes,
                                System.IO.Path.GetExtension(resolvedAssetPath),
                                System.IO.Path.GetFileNameWithoutExtension(resolvedAssetPath));
                        }
                        catch (System.Exception ex) when (ex is System.IO.IOException || ex is System.ArgumentException)
                        {
                            decodedAlphaTexture = null;
                        }
                    }

                    try
                    {
                        MmdUnityMaterialBuilder.ReapplyImportedMaterialTransparency(
                            mat, descriptor, source, i, matDef.texture, diffuseAssetPath, decodedAlphaTexture);
                    }
                    finally
                    {
                        if (decodedAlphaTexture != null)
                        {
                            UnityEngine.Object.DestroyImmediate(decodedAlphaTexture);
                        }
                    }
                }
                }

                return summary.Build(ownedSubAssets);
            }
            catch
            {
                foreach (MmdPmxOwnedTextureSubAsset ownedSubAsset in ownedSubAssets)
                {
                    if (ownedSubAsset.Texture != null)
                    {
                        Object.DestroyImmediate(ownedSubAsset.Texture);
                    }
                }

                throw;
            }
        }

        private static Texture2D? GetOrCreateSharedToonSubAsset(
            int sharedToonIndex,
            Dictionary<int, Texture2D> cache,
            List<MmdPmxOwnedTextureSubAsset> ownedSubAssets)
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
            cache[sharedToonIndex] = texture;
            ownedSubAssets.Add(new MmdPmxOwnedTextureSubAsset(
                $"SharedToon_{sharedToonIndex:00}",
                texture));
            return texture;
        }

        private static TextureReferenceBindStatus BindOneTextureReference(string? reference, string pmxAssetPath, Material material, AssetImportContext? ctx, params string[] propertyNames)
        {
            if (string.IsNullOrWhiteSpace(reference) || material == null)
            {
                return TextureReferenceBindStatus.NoReference;
            }

            string resolvedReference = reference!;
            if (!MmdAssetPathUtility.TryResolveProjectRelativeAssetPathCandidate(pmxAssetPath, resolvedReference, out string assetPath))
            {
                return TextureReferenceBindStatus.Missing;
            }

            if (ctx != null)
            {
                ctx.DependsOnSourceAsset(assetPath);
            }

            Texture2D? tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
            {
                return TextureReferenceBindStatus.Missing;
            }

            foreach (string prop in propertyNames)
            {
                if (material.HasProperty(prop))
                {
                    material.SetTexture(prop, tex);
                }
            }

            return TextureReferenceBindStatus.Resolved;
        }

        private enum TextureReferenceBindStatus
        {
            NoReference,
            Resolved,
            Missing
        }

        private sealed class MmdPmxProjectTextureBindingSummaryBuilder
        {
            private int resolvedCount;
            private int missingCount;
            private string missingSample = string.Empty;

            public void Record(int materialIndex, string slot, string? reference, TextureReferenceBindStatus status)
            {
                switch (status)
                {
                    case TextureReferenceBindStatus.Resolved:
                        resolvedCount++;
                        break;
                    case TextureReferenceBindStatus.Missing:
                        missingCount++;
                        if (string.IsNullOrEmpty(missingSample))
                        {
                            missingSample = $"material {materialIndex} {slot}: {reference ?? string.Empty}";
                        }
                        break;
                }
            }

            public MmdPmxProjectTextureBindingSummary Build(IReadOnlyList<MmdPmxOwnedTextureSubAsset> ownedSubAssets)
            {
                return new MmdPmxProjectTextureBindingSummary(
                    resolvedCount,
                    missingCount,
                    missingSample,
                    ownedSubAssets);
            }
        }
    }

    internal readonly struct MmdPmxProjectTextureBindingSummary
    {
        public static readonly MmdPmxProjectTextureBindingSummary Empty = new MmdPmxProjectTextureBindingSummary(
            0,
            0,
            string.Empty,
            System.Array.Empty<MmdPmxOwnedTextureSubAsset>());

        public MmdPmxProjectTextureBindingSummary(
            int resolvedReferenceCount,
            int missingReferenceCount,
            string? missingReferenceSample,
            IReadOnlyList<MmdPmxOwnedTextureSubAsset>? ownedSubAssets = null)
        {
            ResolvedReferenceCount = System.Math.Max(0, resolvedReferenceCount);
            MissingReferenceCount = System.Math.Max(0, missingReferenceCount);
            MissingReferenceSample = missingReferenceSample ?? string.Empty;
            OwnedSubAssets = ownedSubAssets ?? System.Array.Empty<MmdPmxOwnedTextureSubAsset>();
        }

        public int ResolvedReferenceCount { get; }

        public int MissingReferenceCount { get; }

        public string MissingReferenceSample { get; }

        public IReadOnlyList<MmdPmxOwnedTextureSubAsset> OwnedSubAssets { get; }
    }

    internal readonly struct MmdPmxOwnedTextureSubAsset
    {
        public MmdPmxOwnedTextureSubAsset(string identifier, Texture2D texture)
        {
            Identifier = identifier;
            Texture = texture;
        }

        public string Identifier { get; }

        public Texture2D Texture { get; }
    }
}
