#nullable enable

using Mmd.UnityIntegration;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mmd.Rendering.Universal
{
    public enum MmdSelfShadowPcfSamples
    {
        One = 1,
        Four = 4,
        Eight = 8
    }

    public sealed class MmdSelfShadowRendererFeature : ScriptableRendererFeature
    {
        internal const float DefaultShadowDepthBias = 0.02f;
        internal static readonly Vector3 DefaultShadowDirection = new(0.35f, -1.0f, 0.35f);
        private const float MaxShadowDepthBias = 0.5f;

        [SerializeField]
        private int shadowMapSize = 1024;

        [SerializeField]
        [Tooltip("Fallback self-shadow direction, not a scene Light binding. Used only when no scene self-shadow light, target light, VMD light, or RenderSettings.sun direction is available.")]
        private Vector3 shadowDirection = DefaultShadowDirection;

        [SerializeField]
        [Tooltip("World-space MMD self-shadow depth bias in meters. The render pass normalizes this by the fitted character depth range.")]
        private float shadowDepthBias = DefaultShadowDepthBias;

        [SerializeField]
        [Tooltip("Number of PCF shadow samples. One = MMD real-machine parity (default). Higher values soften shadow edges.")]
        private MmdSelfShadowPcfSamples pcfSamples = MmdSelfShadowPcfSamples.One;

        [SerializeField]
        [Range(0f, 8f)]
        [Tooltip("PCF soft shadow radius in texels. 0 = sharp (default).")]
        private float softShadowRadius = 0f;

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

        public MmdSelfShadowPcfSamples PcfSamples
        {
            get => pcfSamples;
            set => pcfSamples = value;
        }

        public float SoftShadowRadius
        {
            get => softShadowRadius;
            set => softShadowRadius = Mathf.Clamp(value, 0f, 8f);
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
            MmdSelfShadowRenderPass pass = selfShadowPass ?? new MmdSelfShadowRenderPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingShadows
            };

            selfShadowPass = pass;
            if (pass.Setup(
                shadowMapSize,
                shadowDirection,
                ShadowDepthBias,
                (int)pcfSamples,
                softShadowRadius))
            {
                MmdSelfShadowTarget.SetReceiverGateAvailableForRendering(true);
                renderer.EnqueuePass(pass);
                return;
            }

            DisableSelfShadowState(MmdSelfShadowRenderPass.LastDiagnosticStatus);
        }

        protected override void Dispose(bool disposing)
        {
            DisableSelfShadowState();
        }

        private static void DisableSelfShadowState(
            MmdSceneSelfShadowDiagnosticStatus diagnosticStatus =
                MmdSceneSelfShadowDiagnosticStatus.NoRendererFeature)
        {
            MmdSelfShadowRenderPass.PublishDisabledGlobals(diagnosticStatus);
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
