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
        internal static MmdSelfShadowRendererSetupReadiness EvaluateMmdSelfShadowRendererSetupForCurrentPipeline(
            Camera? targetCamera = null)
        {
            RenderPipelineAsset pipeline = QualitySettings.renderPipeline != null
                ? QualitySettings.renderPipeline
                : GraphicsSettings.currentRenderPipeline;
            return EvaluateMmdSelfShadowRendererSetup(pipeline as UniversalRenderPipelineAsset, targetCamera);
        }

        internal static MmdSelfShadowRendererSetupReadiness EvaluateMmdSelfShadowRendererSetup(
            UniversalRenderPipelineAsset? pipeline,
            Camera? targetCamera = null)
        {
            if (pipeline == null)
            {
                return MmdSelfShadowRendererSetupReadiness.NoUrpAsset;
            }

            var pipelineSo = new SerializedObject(pipeline);
            var rendererDataList = pipelineSo.FindProperty("m_RendererDataList");
            if (rendererDataList == null)
            {
                return new MmdSelfShadowRendererSetupReadiness(
                    hasUrpAsset: true,
                    rendererDataCount: 0,
                    featureCount: 0,
                    enabledFeatureCount: 0,
                    activeRendererDataIndex: -1,
                    activeFeatureCount: 0,
                    activeEnabledFeatureCount: 0);
            }

            int activeRendererDataIndex = GetMmdSelfShadowActiveRendererDataIndex(
                pipelineSo,
                rendererDataList,
                targetCamera);
            int rendererDataCount = 0;
            int featureCount = 0;
            int enabledFeatureCount = 0;
            int activeFeatureCount = 0;
            int activeEnabledFeatureCount = 0;
            for (int i = 0; i < rendererDataList.arraySize; i++)
            {
                var rendererDataRef = rendererDataList.GetArrayElementAtIndex(i);
                if (rendererDataRef.objectReferenceValue == null)
                {
                    continue;
                }

                rendererDataCount++;
                var rendererDataSo = new SerializedObject(rendererDataRef.objectReferenceValue);
                var features = rendererDataSo.FindProperty("m_RendererFeatures");
                if (features == null)
                {
                    continue;
                }

                for (int j = 0; j < features.arraySize; j++)
                {
                    if (features.GetArrayElementAtIndex(j).objectReferenceValue is not MmdSelfShadowRendererFeature feature)
                    {
                        continue;
                    }

                    featureCount++;
                    if (i == activeRendererDataIndex)
                    {
                        activeFeatureCount++;
                    }

                    if (feature.isActive)
                    {
                        enabledFeatureCount++;
                        if (i == activeRendererDataIndex)
                        {
                            activeEnabledFeatureCount++;
                        }
                    }
                }
            }

            return new MmdSelfShadowRendererSetupReadiness(
                hasUrpAsset: true,
                rendererDataCount,
                featureCount,
                enabledFeatureCount,
                activeRendererDataIndex,
                activeFeatureCount,
                activeEnabledFeatureCount);
        }

        private static int GetMmdSelfShadowActiveRendererDataIndex(
            SerializedObject pipelineSo,
            SerializedProperty rendererDataList,
            Camera? targetCamera)
        {
            int defaultIndex = 0;
            var defaultIndexProperty = pipelineSo.FindProperty("m_DefaultRendererIndex");
            if (defaultIndexProperty != null)
            {
                defaultIndex = defaultIndexProperty.intValue;
            }

            int requestedIndex = -1;
            if (targetCamera != null &&
                targetCamera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalCameraData))
            {
                var cameraSo = new SerializedObject(additionalCameraData);
                var rendererIndexProperty = cameraSo.FindProperty("m_RendererIndex");
                if (rendererIndexProperty != null)
                {
                    requestedIndex = rendererIndexProperty.intValue;
                }
            }

            int resolvedIndex = requestedIndex >= 0 ? requestedIndex : defaultIndex;
            if (IsValidRendererDataIndex(rendererDataList, resolvedIndex))
            {
                return resolvedIndex;
            }

            return IsValidRendererDataIndex(rendererDataList, defaultIndex) ? defaultIndex : -1;
        }

        private static bool IsValidRendererDataIndex(SerializedProperty rendererDataList, int index)
        {
            return index >= 0 &&
                   index < rendererDataList.arraySize &&
                   rendererDataList.GetArrayElementAtIndex(index).objectReferenceValue != null;
        }

        internal static string GetSelfShadowRendererSetupWarning(
            MmdSceneEnvironmentBinding? binding,
            MmdSelfShadowRendererSetupReadiness readiness)
        {
            if (binding == null || !binding.SelfShadowEnabled)
            {
                return string.Empty;
            }

            if (!readiness.HasUrpAsset)
            {
                return "SelfShadow is enabled, but no active URP Asset is configured. " +
                       "Assign a Universal Render Pipeline Asset before adding the MmdSelfShadowRendererFeature.";
            }

            if (!readiness.FeaturePresentOnActiveRendererData)
            {
                return "SelfShadow is enabled, but MmdSelfShadowRendererFeature is not configured on the Renderer Data " +
                       "used by the target Camera or default renderer. Add or move the feature to that active Renderer Data.";
            }

            if (!readiness.FeatureEnabledOnActiveRendererData)
            {
                return "SelfShadow is enabled, but the MmdSelfShadowRendererFeature on the Renderer Data used by " +
                       "the target Camera or default renderer is disabled. Enable the feature on that active Renderer Data.";
            }

            return string.Empty;
        }

    }
}
