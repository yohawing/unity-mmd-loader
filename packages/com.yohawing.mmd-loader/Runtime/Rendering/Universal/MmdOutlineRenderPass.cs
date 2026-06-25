#nullable enable

using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

namespace Mmd.Rendering.Universal
{
    public sealed class MmdOutlineRenderPass : ScriptableRenderPass
    {
        private static readonly ShaderTagId MmdOutlineShaderTagId = new("MmdOutline");

        private sealed class PassData
        {
            public RendererListHandle RendererList;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();

            RendererListDesc rendererListDesc = CreateRendererListDesc(cameraData, renderingData);
            RendererListHandle rendererList = renderGraph.CreateRendererList(rendererListDesc);

            using RasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(
                "MMD Outline Pass",
                out PassData passData);

            passData.RendererList = rendererList;
            builder.UseRendererList(rendererList);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                context.cmd.DrawRendererList(data.RendererList);
            });
        }

        private static RendererListDesc CreateRendererListDesc(
            UniversalCameraData cameraData,
            UniversalRenderingData renderingData)
        {
            return new RendererListDesc(MmdOutlineShaderTagId, renderingData.cullResults, cameraData.camera)
            {
                renderQueueRange = RenderQueueRange.all,
                layerMask = cameraData.camera.cullingMask,
                sortingCriteria = SortingCriteria.CommonTransparent
            };
        }
    }
}
