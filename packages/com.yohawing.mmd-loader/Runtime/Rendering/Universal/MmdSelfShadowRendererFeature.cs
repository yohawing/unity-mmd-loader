#nullable enable

using Mmd.UnityIntegration;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Mmd.Rendering.Universal
{
    public sealed class MmdSelfShadowRendererFeature : ScriptableRendererFeature
    {
        internal const float DefaultShadowDepthBias = 0.0025f;
        internal static readonly Vector3 DefaultShadowDirection = new(0.35f, -1.0f, 0.35f);
        private const float MaxShadowDepthBias = 0.1f;

        [SerializeField]
        private int shadowMapSize = 1024;

        [SerializeField]
        [Tooltip("Fallback self-shadow direction, not a scene Light binding. Used only when no scene self-shadow light, target light, VMD light, or RenderSettings.sun direction is available.")]
        private Vector3 shadowDirection = DefaultShadowDirection;

        [SerializeField]
        private float shadowDepthBias = DefaultShadowDepthBias;

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

        public float ShadowDepthBias
        {
            get => SanitizeShadowDepthBias(shadowDepthBias);
            set => shadowDepthBias = SanitizeShadowDepthBias(value);
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
            if (pass.Setup(shadowMapSize, shadowDirection, ShadowDepthBias))
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

        private static float SanitizeShadowDepthBias(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0.0f;
            }

            return Mathf.Clamp(value, 0.0f, MaxShadowDepthBias);
        }
    }
}
