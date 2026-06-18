#nullable enable

using System;
using System.IO;
using UnityEditor.AssetImporters;
using Mmd;
using Mmd.Parser;

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
                var parser = new NativeMmdParser();
                MmdMotionDefinition motion = parser.LoadMotion(bytes);
                diagnostics = MmdMotionValidator.ValidateStructuralMotion(motion);

                int constraintStateCount = ComputeConstraintStateCount(motion);

                summary = new MmdVmdParseSummary(
                    motion.targetModelName,
                    motion.maxFrame,
                    motion.boneKeyframes?.Count ?? 0,
                    motion.morphKeyframes?.Count ?? 0,
                    motion.modelKeyframes?.Count ?? 0,
                    constraintStateCount,
                    motion.cameraKeyframeCount,
                    motion.lightKeyframeCount,
                    motion.selfShadowKeyframeCount);
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

        private static int ComputeConstraintStateCount(MmdMotionDefinition motion)
        {
            int count = 0;
            if (motion?.modelKeyframes != null)
            {
                for (int i = 0; i < motion.modelKeyframes.Count; i++)
                {
                    var mk = motion.modelKeyframes[i];
                    if (mk?.constraintStates != null)
                    {
                        count += mk.constraintStates.Count;
                    }
                }
            }
            return count;
        }
    }
}
