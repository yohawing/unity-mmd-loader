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
