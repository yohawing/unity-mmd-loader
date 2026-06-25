#nullable enable

using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Mmd.Rendering.Universal
{
    public sealed class MmdOutlineRenderPass : ScriptableRenderPass
    {
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
        }
    }
}
