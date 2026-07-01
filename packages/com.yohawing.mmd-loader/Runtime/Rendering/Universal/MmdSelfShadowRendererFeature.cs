#nullable enable

using Mmd.UnityIntegration;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mmd.Rendering.Universal
{
    public sealed class MmdSelfShadowRendererFeature : ScriptableRendererFeature
    {
        internal const float DefaultShadowDepthBias = 0.0025f;
        internal const float DefaultDebugDepthPreviewContrast = 64.0f;
        internal static readonly Vector3 DefaultShadowDirection = new(0.35f, -1.0f, 0.35f);
        private const float MaxShadowDepthBias = 0.1f;
        private const float MaxDebugDepthPreviewContrast = 4096.0f;
        private const string DebugDepthPreviewShaderName = "Hidden/MMD/SelfShadowDepthDebug";

        [SerializeField]
        private int shadowMapSize = 1024;

        [SerializeField]
        [Tooltip("Fallback self-shadow direction, not a scene Light binding. Used only when no scene self-shadow light, target light, VMD light, or RenderSettings.sun direction is available.")]
        private Vector3 shadowDirection = DefaultShadowDirection;

        [SerializeField]
        private float shadowDepthBias = DefaultShadowDepthBias;

        [SerializeField]
        [Tooltip("Opt-in Frame Debugger preview. Creates a color texture from the depth-only MMD self-shadow map without changing the receiver path.")]
        private bool debugDepthPreview;

        [SerializeField]
        private float debugDepthPreviewContrast = DefaultDebugDepthPreviewContrast;

        private MmdSelfShadowRenderPass? selfShadowPass;
        private Material? debugDepthPreviewMaterial;

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

        public bool DebugDepthPreview
        {
            get => debugDepthPreview;
            set => debugDepthPreview = value;
        }

        public float DebugDepthPreviewContrast
        {
            get => SanitizeDebugDepthPreviewContrast(debugDepthPreviewContrast);
            set => debugDepthPreviewContrast = SanitizeDebugDepthPreviewContrast(value);
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
            Material? debugMaterial = debugDepthPreview ? GetDebugDepthPreviewMaterial() : null;
            if (pass.Setup(shadowMapSize, shadowDirection, ShadowDepthBias, debugDepthPreview, DebugDepthPreviewContrast, debugMaterial))
            {
                MmdSelfShadowTarget.SetReceiverGateAvailableForRendering(true);
                renderer.EnqueuePass(pass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            DisableSelfShadowState();
            CoreUtils.Destroy(debugDepthPreviewMaterial);
            debugDepthPreviewMaterial = null;
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

        private Material? GetDebugDepthPreviewMaterial()
        {
            if (debugDepthPreviewMaterial != null)
            {
                return debugDepthPreviewMaterial;
            }

            Shader shader = Shader.Find(DebugDepthPreviewShaderName);
            if (shader == null)
            {
                return null;
            }

            debugDepthPreviewMaterial = CoreUtils.CreateEngineMaterial(shader);
            return debugDepthPreviewMaterial;
        }

        private static float SanitizeDebugDepthPreviewContrast(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return DefaultDebugDepthPreviewContrast;
            }

            return Mathf.Clamp(value, 1.0f, MaxDebugDepthPreviewContrast);
        }
    }
}
