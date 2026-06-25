#nullable enable

using UnityEngine.Rendering.Universal;

namespace Mmd.Rendering.Universal
{
    public sealed class MmdOutlineRendererFeature : ScriptableRendererFeature
    {
        private MmdOutlineRenderPass? outlinePass;

        public override void Create()
        {
            outlinePass = new MmdOutlineRenderPass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            MmdOutlineRenderPass pass = outlinePass ?? new MmdOutlineRenderPass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents
            };

            outlinePass = pass;
            renderer.EnqueuePass(pass);
        }
    }
}
