#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor.AssetImporters;
using UnityEngine;
using Mmd;

namespace Mmd.Editor
{
    [ScriptedImporter(2, "vmd")]
    public sealed class MmdVmdScriptedImporter : ScriptedImporter
    {
        private sealed class CachedSummary
        {
            public CachedSummary(
                string assetPath,
                int sourceByteLength,
                DateTime sourceLastWriteTimeUtc,
                byte[] sourceHash,
                MmdVmdParseSummary? summary,
                IReadOnlyList<string>? diagnostics)
            {
                AssetPath = assetPath;
                SourceByteLength = sourceByteLength;
                SourceLastWriteTimeUtc = sourceLastWriteTimeUtc;
                SourceHash = sourceHash;
                Summary = summary;
                Diagnostics = diagnostics;
            }

            public string AssetPath { get; }

            public int SourceByteLength { get; }

            public DateTime SourceLastWriteTimeUtc { get; }

            public byte[] SourceHash { get; }

            public MmdVmdParseSummary? Summary { get; }

            public IReadOnlyList<string>? Diagnostics { get; }

            public bool MatchesMetadata(string assetPath, int sourceByteLength, DateTime sourceLastWriteTimeUtc)
            {
                return string.Equals(AssetPath, assetPath, StringComparison.Ordinal) &&
                    SourceByteLength == sourceByteLength &&
                    SourceLastWriteTimeUtc == sourceLastWriteTimeUtc;
            }

            public bool MatchesContent(byte[] sourceHash)
            {
                if (SourceHash.Length != sourceHash.Length)
                {
                    return false;
                }

                for (int i = 0; i < SourceHash.Length; i++)
                {
                    if (SourceHash[i] != sourceHash[i])
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private static CachedSummary? cachedSummary;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            byte[] bytes = File.ReadAllBytes(ctx.assetPath);
            string resolvedSourcePath = MmdAssetPathUtility.ResolveAssetSourcePath(ctx.assetPath);
            (MmdVmdParseSummary? summary, IReadOnlyList<string>? diagnostics, bool contentUnchanged) = ReadSummaryWithCache(
                ctx.assetPath,
                bytes);
            TextAsset rawSource = FindReusableRawSource(ctx, contentUnchanged) ?? new TextAsset(bytes)
            {
                name = "VMDSource"
            };

            MmdVmdAsset asset = MmdVmdAsset.CreateInstance<MmdVmdAsset>();
            asset.InitializeImported(bytes, ctx.assetPath, resolvedSourcePath, rawSource, summary, diagnostics);
            ctx.AddObjectToAsset("VMDSource", rawSource);
            ctx.AddObjectToAsset("VMD", asset);
            ctx.SetMainObject(asset);
        }

        private static (MmdVmdParseSummary? Summary, IReadOnlyList<string>? Diagnostics, bool ContentUnchanged) ReadSummaryWithCache(
            string assetPath,
            byte[] bytes)
        {
            int sourceByteLength = bytes.Length;
            DateTime sourceLastWriteTimeUtc = File.GetLastWriteTimeUtc(assetPath);
            CachedSummary? cached = cachedSummary;
            byte[]? sourceHash = null;
            if (cached != null && cached.MatchesMetadata(assetPath, sourceByteLength, sourceLastWriteTimeUtc))
            {
                sourceHash = ComputeSourceHash(bytes);
                if (cached.MatchesContent(sourceHash))
                {
                    return (cached.Summary, cached.Diagnostics, true);
                }
            }

            MmdVmdParseSummary? summary = null;
            IReadOnlyList<string>? diagnostics = null;
            try
            {
                summary = MmdVmdBinarySummaryReader.Read(bytes);
            }
            catch (Exception ex)
            {
                // Parse failure must still produce a usable MmdVmdAsset with source bytes preserved.
                // Store failure diagnostic for inspector display; summary will be zeroed.
                diagnostics = new[] { "Failed to parse VMD during import: " + ex.Message };
                summary = new MmdVmdParseSummary(string.Empty, 0, 0, 0, 0, 0, 0, 0, 0);
            }

            if (summary.HasValue && diagnostics == null)
            {
                sourceHash ??= ComputeSourceHash(bytes);
                cachedSummary = new CachedSummary(
                    assetPath,
                    sourceByteLength,
                    sourceLastWriteTimeUtc,
                    sourceHash,
                    summary,
                    diagnostics);
            }
            else
            {
                cachedSummary = null;
            }

            return (summary, diagnostics, false);
        }

        private static TextAsset? FindReusableRawSource(AssetImportContext ctx, bool contentUnchanged)
        {
            if (!contentUnchanged)
            {
                return null;
            }

            var importedObjects = new List<UnityEngine.Object>();
            ctx.GetObjects(importedObjects);
            foreach (UnityEngine.Object importedObject in importedObjects)
            {
                if (importedObject is TextAsset textAsset && textAsset.name == "VMDSource")
                {
                    return textAsset;
                }
            }

            return null;
        }

        private static byte[] ComputeSourceHash(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            return sha256.ComputeHash(bytes);
        }
    }
}
