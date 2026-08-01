#nullable enable

using System;
using System.IO;
using UnityEditor.AssetImporters;
using Mmd;

namespace Mmd.Editor
{
    [ScriptedImporter(1, "vmd")]
    public sealed class MmdVmdScriptedImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            byte[] bytes = File.ReadAllBytes(ctx.assetPath);
            string resolvedSourcePath = MmdAssetPathUtility.ResolveAssetSourcePath(ctx.assetPath);

            MmdVmdParseSummary? summary = null;
            System.Collections.Generic.IReadOnlyList<string>? diagnostics = null;

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

            MmdVmdAsset asset = MmdVmdAsset.CreateInstance<MmdVmdAsset>();
            asset.Initialize(bytes, ctx.assetPath, resolvedSourcePath, summary, diagnostics);
            ctx.AddObjectToAsset("VMD", asset);
            ctx.SetMainObject(asset);
        }
    }
}
