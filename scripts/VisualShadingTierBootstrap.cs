using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class VisualShadingTierBootstrap
{
    public static void EnsureUniversalRenderPipeline()
    {
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)
        {
            return;
        }

        const string settingsDirectory = "Assets/Settings";
        if (!AssetDatabase.IsValidFolder(settingsDirectory))
        {
            AssetDatabase.CreateFolder("Assets", "Settings");
        }

        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(rendererData, settingsDirectory + "/VisualTierRenderer.asset");

        var outlineFeature = ScriptableObject.CreateInstance<Mmd.Rendering.Universal.MmdOutlineRendererFeature>();
        outlineFeature.name = "MmdOutlineRendererFeature";
        AssetDatabase.AddObjectToAsset(outlineFeature, rendererData);
        rendererData.rendererFeatures.Add(outlineFeature);

        // The Toon Lit SSAO delta test toggles an existing ScreenSpaceAmbientOcclusion feature
        // per capture and restores it afterwards. URP keeps that feature type internal, so add
        // it by name and leave it inactive: the golden capture must not see ambient occlusion.
        System.Type ssaoFeatureType = typeof(UniversalRendererData).Assembly.GetType(
            "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
        if (ssaoFeatureType == null)
        {
            throw new System.InvalidOperationException("URP ScreenSpaceAmbientOcclusion renderer feature type was not found.");
        }
        var ssaoFeature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssaoFeatureType);
        ssaoFeature.name = "ScreenSpaceAmbientOcclusion";
        ssaoFeature.SetActive(false);
        AssetDatabase.AddObjectToAsset(ssaoFeature, rendererData);
        rendererData.rendererFeatures.Add(ssaoFeature);
        EditorUtility.SetDirty(rendererData);

        UniversalRenderPipelineAsset pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
        AssetDatabase.CreateAsset(pipelineAsset, settingsDirectory + "/VisualTierRenderPipelineAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipelineAsset;
        QualitySettings.renderPipeline = pipelineAsset;

        EditorUtility.SetDirty(pipelineAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset))
        {
            throw new System.InvalidOperationException("Failed to assign the visual-tier Universal Render Pipeline asset.");
        }
    }
}
