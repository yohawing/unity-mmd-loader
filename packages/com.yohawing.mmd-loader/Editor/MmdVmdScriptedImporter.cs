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
                byte[] sourceBytes,
                MmdVmdParseSummary? summary,
                IReadOnlyList<string>? diagnostics)
            {
                AssetPath = assetPath;
                SourceByteLength = sourceByteLength;
                SourceLastWriteTimeUtc = sourceLastWriteTimeUtc;
                SourceHash = sourceHash;
                SourceBytes = sourceBytes;
                Summary = summary;
                Diagnostics = diagnostics;
            }

            public string AssetPath { get; }

            public int SourceByteLength { get; }

            public DateTime SourceLastWriteTimeUtc { get; }

            public byte[] SourceHash { get; }

            public byte[] SourceBytes { get; }

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
            (byte[] bytes, MmdVmdParseSummary? summary, IReadOnlyList<string>? diagnostics, bool contentUnchanged) =
                ReadSummaryWithCache(ctx.assetPath);
            string resolvedSourcePath = MmdAssetPathUtility.ResolveAssetSourcePath(ctx.assetPath);
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

        private static (byte[] Bytes, MmdVmdParseSummary? Summary, IReadOnlyList<string>? Diagnostics, bool ContentUnchanged) ReadSummaryWithCache(
            string assetPath)
        {
            FileInfo sourceInfo = new FileInfo(assetPath);
            int sourceByteLength = checked((int)sourceInfo.Length);
            DateTime sourceLastWriteTimeUtc = sourceInfo.LastWriteTimeUtc;
            CachedSummary? cached = cachedSummary;
            if (cached != null && cached.MatchesMetadata(assetPath, sourceByteLength, sourceLastWriteTimeUtc))
            {
                byte[] sourceHash = ComputeSourceHash(assetPath);
                if (cached.MatchesContent(sourceHash))
                {
                    return (cached.SourceBytes, cached.Summary, cached.Diagnostics, true);
                }
            }

            byte[] bytes = File.ReadAllBytes(assetPath);
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
                byte[] sourceHash = ComputeSourceHash(bytes);
                cachedSummary = new CachedSummary(
                    assetPath,
                    bytes.Length,
                    File.GetLastWriteTimeUtc(assetPath),
                    sourceHash,
                    bytes,
                    summary,
                    diagnostics);
            }
            else
            {
                cachedSummary = null;
            }

            return (bytes, summary, diagnostics, false);
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

        private static byte[] ComputeSourceHash(string assetPath)
        {
            using FileStream stream = new FileStream(
                assetPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.SequentialScan);
            using SHA256 sha256 = SHA256.Create();
            return sha256.ComputeHash(stream);
        }
    }
}
