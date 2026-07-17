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
        internal static MmdOutlineReadiness GetOutlineReadiness(MmdPmxAsset? asset)
        {
            if (asset == null || asset.EdgeMaterialCount <= 0)
            {
                return new MmdOutlineReadiness(
                    outlineEligibleMaterialCount: 0,
                    runtimePath: "No PMX draw-edge materials",
                    releaseMode: "Not needed",
                    finalVisualParity: MmdOutlineReadiness.NotClaimed);
            }

            string runtimePath = string.Equals(asset.ShaderPreset, "MmdBasicUrpToon", System.StringComparison.Ordinal)
                ? "MmdOutlineRendererFeature (LightMode=MmdOutline)"
                : "Shader preset summary only";
            return new MmdOutlineReadiness(
                asset.EdgeMaterialCount,
                runtimePath,
                "Back-face mesh-normal extrusion",
                MmdOutlineReadiness.NotClaimed);
        }

        internal static bool IsMmdOutlineFeaturePresent()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
                return false;

            var pipelineSo = new SerializedObject(pipeline);
            var rendererDataList = pipelineSo.FindProperty("m_RendererDataList");
            if (rendererDataList == null)
                return false;

            for (int i = 0; i < rendererDataList.arraySize; i++)
            {
                var rendererDataRef = rendererDataList.GetArrayElementAtIndex(i);
                if (rendererDataRef.objectReferenceValue == null)
                    continue;

                var rendererDataSo = new SerializedObject(rendererDataRef.objectReferenceValue);
                var features = rendererDataSo.FindProperty("m_RendererFeatures");
                if (features == null)
                    continue;

                for (int j = 0; j < features.arraySize; j++)
                {
                    if (features.GetArrayElementAtIndex(j).objectReferenceValue is MmdOutlineRendererFeature)
                        return true;
                }
            }

            return false;
        }

        internal static MmdScaleAwarePhysicsReadiness GetScaleAwarePhysicsReadiness(MmdPmxAsset? asset)
        {
            if (asset == null)
            {
                return new MmdScaleAwarePhysicsReadiness(
                    importScale: MmdPmxAsset.DefaultImportScale,
                    hasPhysicsDescriptors: false,
                    gravityPolicy: "unavailable-no-pmx-asset",
                    backendReadbackSpace: MmdScaleAwarePhysicsReadiness.MmdSpaceReadback,
                    scaleAwareHandoffReadiness: MmdScaleAwarePhysicsReadiness.ScaleAwareHandoffReady,
                    requiredSmoke: MmdScaleAwarePhysicsReadiness.ScaleAwareSmokeCovered);
            }

            bool hasPhysicsDescriptors = asset.RigidbodyCount > 0 || asset.JointCount > 0;

            return new MmdScaleAwarePhysicsReadiness(
                asset.ImportScale,
                hasPhysicsDescriptors,
                "scale-aware-mmd-gravity-98",
                MmdScaleAwarePhysicsReadiness.MmdSpaceReadback,
                MmdScaleAwarePhysicsReadiness.ScaleAwareHandoffReady,
                MmdScaleAwarePhysicsReadiness.ScaleAwareSmokeCovered);
        }

        internal static MmdAnimationTimelineReadiness GetAnimationTimelineReadiness(MmdPmxAsset? asset)
        {
            if (asset == null || asset.ByteLength <= 0)
            {
                return new MmdAnimationTimelineReadiness(
                    timelineBindingTarget: "Unavailable",
                    vmdDropReadiness: "Unavailable",
                    playbackSource: "Scene component or Timeline clip, not PMX import side effect");
            }

            return new MmdAnimationTimelineReadiness(
                timelineBindingTarget: "Ready after Load PMX Into Scene",
                vmdDropReadiness: "Requires generated scene controller",
                playbackSource: "Scene component or Timeline clip, not PMX import side effect");
        }

    }
}
