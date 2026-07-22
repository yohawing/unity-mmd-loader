Shader "MMD URP Toon"
{
    // Public name for new materials. The old MMD Toon Lit name remains as a compatibility shader.
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _MainTex ("Main Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _SphereMap ("Diagnostic Sphere Map", 2D) = "black" {}
        _SphereMode ("Sphere Texture Mode", Float) = 0
        _ToonMap ("Toon Map", 2D) = "white" {}
        _ToonMapBound ("Toon Map Bound", Float) = 0
        _DiagnosticColor ("Diagnostic Color", Color) = (1, 1, 1, 1)
        _AmbientColor ("Ambient Color", Color) = (0.25, 0.25, 0.25, 1)
        _MmdLightDirection ("Light Direction Override", Vector) = (0, 0, 0, 0)
        _MmdLightColor ("MMD Light Color", Color) = (1, 1, 1, 1)
        // URP SSAO is opt-in for imported MMD character materials. Stage materials may
        // enable this per material from the Inspector when ambient occlusion is desired.
        [Toggle] _ReceiveSSAO ("Receive SSAO", Float) = 0
        [PerRendererData] [HideInInspector] _MmdSelfShadowReceive ("MMD Self Shadow Receive", Float) = 0
        _ToonStrength ("Toon Strength", Range(0, 1)) = 1
        _ToonBoundary ("Toon Boundary", Range(-1, 1)) = -1
        _ToonFeather ("Toon Boundary Feather", Range(-1, 1)) = -1
        _ToonBandCount ("Toon Band Count", Range(-1, 8)) = -1
        _StylizedSpecularColor ("Stylized Specular Color", Color) = (1, 1, 1, 1)
        _StylizedSpecularBoundary ("Stylized Specular Boundary", Range(-1, 1)) = -1
        _StylizedSpecularFeather ("Stylized Specular Feather", Range(-1, 1)) = -1
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimBoundary ("Rim Boundary", Range(-1, 1)) = -1
        _RimFeather ("Rim Feather", Range(-1, 1)) = -1
        _RimLightFollow ("Rim Light Follow", Range(0, 1)) = 0
        [HDR] _EmissionColor ("Emission Color", Color) = (1, 1, 1, 1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        _MmdEmissionIntensity ("MMD Emission Intensity", Range(-1, 8)) = -1
        _MmdEmissionMapBound ("MMD Emission Map Bound", Float) = 0
        _MmdEmissionMask ("MMD Emission Mask", 2D) = "white" {}
        _MmdEmissionMaskBound ("MMD Emission Mask Bound", Float) = 0
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Float) = 0
        _OutlineScreenSpaceWeight ("Outline Screen Space Weight", Float) = 0
        _BodyVisible ("Body Visible", Float) = 1
        _OutlineVisible ("Outline Visible", Float) = 1
        _TextureAlphaOutputWeight ("Texture Alpha Output Weight", Float) = 1
        _BaseMapBound ("Base Map Bound", Float) = 0
        _TextureFlatLightingWeight ("Texture Flat Lighting Weight", Float) = 0
        _TextureFlatLightingValue ("Texture Flat Lighting Value", Float) = 2.12
        _MmdNormalMap ("Normal Map", 2D) = "bump" {}
        _MmdNormalMapBound ("MMD Normal Map Bound", Float) = 0
        _ReflectionProbeWeight ("Reflection Probe Weight", Range(0, 1)) = 0
        _AlphaClipThreshold ("Alpha Clip Threshold", Range(0, 1)) = 0
        _ShadowAlphaClipThreshold ("Shadow Alpha Clip Threshold", Range(0, 1)) = 0
        [HideInInspector] _MmdAlphaClipThresholdBackup ("MMD Alpha Clip Threshold Backup", Float) = 0.01
        [HideInInspector] _MmdShadowAlphaClipThresholdBackup ("MMD Shadow Alpha Clip Threshold Backup", Float) = 0.01
        [HideInInspector] _MmdSphereModeBackup ("MMD Sphere Mode Backup", Float) = 1
        [HideInInspector] _MmdStylizedSpecularBoundaryBackup ("MMD Stylized Specular Boundary Backup", Float) = 0.5
        [HideInInspector] _MmdRimBoundaryBackup ("MMD Rim Boundary Backup", Float) = 0.5
        [HideInInspector] _MmdEmissionIntensityBackup ("MMD Emission Intensity Backup", Float) = 1
        _Cull ("Cull", Float) = 2
        _ZWrite ("ZWrite", Float) = 1
        _ZTest ("ZTest", Float) = 4
        _OutlineZTest ("Outline Z Test", Float) = 2
        _OutlineZWrite ("Outline ZWrite", Float) = 0
        _SrcBlend ("Src Blend", Float) = 1
        _DstBlend ("Dst Blend", Float) = 0
        _GammaTarget ("Gamma Composite Target", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        UsePass "MMD Basic URP Toon/Outline"
        UsePass "MMD Toon Lit/ForwardLit"
        UsePass "MMD Toon Lit/DepthOnly"
        UsePass "MMD Toon Lit/DepthNormals"
        UsePass "MMD Basic URP Toon/MmdSelfShadowCaster"
        UsePass "MMD Basic URP Toon/ShadowCaster"
    }

    Fallback "MMD Toon Lit"
    CustomEditor "Mmd.Editor.MmdToonLitShaderGUI"
}
