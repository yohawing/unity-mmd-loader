#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Mmd.Parser;
using Mmd.Rendering.Universal;
using Mmd.UnityIntegration;

namespace Mmd.Editor
{
    internal static partial class MmdAssetInspectorUtility
    {
        internal static MmdVmdMotionSummary GetVmdMotionSummary(MmdVmdAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (asset.ByteLength == 0)
            {
                throw new InvalidOperationException("VMD asset has no imported bytes.");
            }

            // Read exclusively from import-time cached summary. Never call LoadMotion()
            // here: selection or summary display must not trigger full VMD parse.
            return new MmdVmdMotionSummary(
                asset.TargetModelName,
                asset.MaxFrame,
                asset.BoneKeyframeCount,
                asset.MorphKeyframeCount,
                asset.ModelKeyframeCount,
                asset.ConstraintStateCount,
                asset.CameraKeyframeCount,
                asset.LightKeyframeCount,
                asset.SelfShadowKeyframeCount);
        }

        internal static IReadOnlyList<string> GetVmdStructuralDiagnostics(MmdVmdAsset asset)
        {
            // Explicit revalidation path for focused tests and diagnostic tooling that deliberately
            // exercise full parse. Asset selection paths must not invoke this.
            MmdMotionDefinition motion = asset.LoadMotion();
            return MmdMotionValidator.ValidateStructuralMotion(motion);
        }

        internal static MmdVmdTimelineReadiness GetVmdTimelineReadiness(MmdVmdAsset? asset)
        {
            if (asset == null || asset.ByteLength <= 0)
            {
                return new MmdVmdTimelineReadiness(
                    maxFrame: 0,
                    cameraKeyframeCount: 0,
                    lightKeyframeCount: 0,
                    selfShadowKeyframeCount: 0,
                    clipDurationSource: "Unavailable",
                    sceneMotionStatus: "No VMD asset",
                    selfShadowSceneMotionStatus: "No VMD asset",
                    clipCreationRequirement: "PMX model source and Timeline are required for VMD Clip creation.");
            }

            // Read EXCLUSIVELY from import-time cached summary on MmdVmdAsset.
            // Never call LoadMotion(), parser, or any full parse path here.
            // This guarantees Inspector selection / readiness display does not parse bytes.
            int cam = asset.CameraKeyframeCount;
            int lit = asset.LightKeyframeCount;
            int shd = asset.SelfShadowKeyframeCount;
            int mf = asset.MaxFrame;

            string sceneStatus = (cam > 0 || lit > 0)
                ? string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Camera/light scene motion present (camera:{0}, light:{1})", cam, lit)
                : "Camera/light scene motion: none";

            string selfShadowStatus = shd > 0
                ? string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Self-shadow scene state present (selfShadow:{0}; MmdSceneEnvironmentBinding records sampled MMD self-shadow state)", shd)
                : "Self-shadow scene motion: none";

            string durationSrc = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Cached VMD MaxFrame ({0})", mf);

            return new MmdVmdTimelineReadiness(
                mf,
                cam,
                lit,
                shd,
                durationSrc,
                sceneStatus,
                selfShadowStatus,
                "PMX model source and Timeline are required for VMD Clip creation.");
        }

    }
}
