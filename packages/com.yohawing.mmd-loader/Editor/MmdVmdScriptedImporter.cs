#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using Mmd;

namespace Mmd.Editor
{
    [ScriptedImporter(2, "vmd")]
    public sealed class MmdVmdScriptedImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            // No static summary cache: Unity reimports only when the source changed, and checking
            // content identity (a whole-file hash) cost more than the native summary parse it saved.
            byte[] bytes = File.ReadAllBytes(ctx.assetPath);
            (MmdVmdParseSummary? summary, IReadOnlyList<string>? diagnostics) = ReadSummary(bytes);
            string resolvedSourcePath = MmdAssetPathUtility.ResolveAssetSourcePath(ctx.assetPath);
            TextAsset rawSource = new TextAsset(bytes)
            {
                name = "VMDSource"
            };

            MmdVmdAsset asset = MmdVmdAsset.CreateInstance<MmdVmdAsset>();
            asset.InitializeImported(bytes, ctx.assetPath, resolvedSourcePath, rawSource, summary, diagnostics);
            ctx.AddObjectToAsset("VMDSource", rawSource);
            ctx.AddObjectToAsset("VMD", asset);
            ctx.SetMainObject(asset);
        }

        private static (MmdVmdParseSummary? Summary, IReadOnlyList<string>? Diagnostics) ReadSummary(byte[] bytes)
        {
            try
            {
                return (MmdVmdNativeSummaryAdapter.Read(bytes), null);
            }
            catch (Exception ex)
            {
                // Parse failure must still produce a usable MmdVmdAsset with source bytes preserved.
                // Store failure diagnostic for inspector display; summary will be zeroed.
                return (
                    new MmdVmdParseSummary(string.Empty, 0, 0, 0, 0, 0, 0, 0, 0),
                    new[] { "Failed to parse VMD during import: " + ex.Message });
            }
        }
    }
}
