#nullable enable

using Mmd.UnityIntegration;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Mmd.Rendering.Universal
{
    public sealed class MmdSelfShadowRendererFeature : ScriptableRendererFeature
    {
        [SerializeField]
        private int shadowMapSize = 1024;

        [SerializeField]
        private Vector3 shadowDirection = new(0.35f, -1.0f, 0.35f);

        private MmdSelfShadowRenderPass? selfShadowPass;

        public int ShadowMapSize
        {
            get => shadowMapSize;
            set => shadowMapSize = Mathf.Clamp(value, 128, 4096);
        }

        public Vector3 ShadowDirection
        {
            get => shadowDirection;
            set => shadowDirection = value;
        }

        public override void Create()
        {
            selfShadowPass = new MmdSelfShadowRenderPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingShadows
            };

            if (!isActive)
            {
                DisableSelfShadowState();
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            MmdSelfShadowTarget.SetReceiverGateAvailableForRendering(false);
            MmdSelfShadowRenderPass pass = selfShadowPass ?? new MmdSelfShadowRenderPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingShadows
            };

            selfShadowPass = pass;
            if (pass.Setup(shadowMapSize, shadowDirection))
            {
                MmdSelfShadowTarget.SetReceiverGateAvailableForRendering(true);
                renderer.EnqueuePass(pass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            DisableSelfShadowState();
        }

        private static void DisableSelfShadowState()
        {
            MmdSelfShadowRenderPass.PublishDisabledGlobals();
            MmdSelfShadowTarget.DisableAllReceiverGates();
        }
    }
}
