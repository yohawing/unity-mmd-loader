Shader "MMD Toon Lit"
{
    // S1a is opt-in. Keep the Legacy MMD Toon shader byte-for-byte intact; this shader only
    // changes the ForwardLit main-light input and reuses the Legacy outline/self-shadow/caster passes.
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
        [PerRendererData] [HideInInspector] _MmdSelfShadowReceive ("MMD Self Shadow Receive", Float) = 0
        _ToonStrength ("Toon Strength", Range(0, 1)) = 1
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
        _MmdNormalMapBound ("Normal Map Bound", Float) = 0
        _AlphaClipThreshold ("Alpha Clip Threshold", Range(0, 1)) = 0
        _ShadowAlphaClipThreshold ("Shadow Alpha Clip Threshold", Range(0, 1)) = 0
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
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        UsePass "MMD Basic URP Toon/Outline"

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex ForwardVertex
            #pragma fragment ForwardFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SphereMap);
            SAMPLER(sampler_SphereMap);
            TEXTURE2D(_ToonMap);
            SAMPLER(sampler_ToonMap);
            TEXTURE2D(_MmdSelfShadowMap);
            SAMPLER(sampler_MmdSelfShadowMap);
            TEXTURE2D(_MmdNormalMap);
            SAMPLER(sampler_MmdNormalMap);

            float4x4 _MmdSelfShadowWorldToShadow;
            float4 _MmdSelfShadowParams;
            half _MmdSelfShadowReceive;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Color;
                half _Alpha;
                half4 _DiagnosticColor;
                half4 _AmbientColor;
                half4 _MmdLightDirection;
                half4 _MmdLightColor;
                half _ToonStrength;
                half _ToonMapBound;
                half _SphereMode;
                half4 _OutlineColor;
                half _OutlineWidth;
                half _OutlineScreenSpaceWeight;
                half _BodyVisible;
                half _OutlineVisible;
                half _TextureAlphaOutputWeight;
                half _BaseMapBound;
                half _TextureFlatLightingWeight;
                half _TextureFlatLightingValue;
                half _AlphaClipThreshold;
                half _ShadowAlphaClipThreshold;
                half _GammaTarget;
                half _MmdNormalMapBound;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ForwardVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                float3 tangentWS = normalize(TransformObjectToWorldDir(input.tangentOS.xyz));
                output.tangentWS = tangentWS;
                output.bitangentWS = cross(output.normalWS, tangentWS) * input.tangentOS.w;
                return output;
            }

            half ComputeMmdSelfShadowVisibility(float receiverDepth, half sampledDepth)
            {
            #if UNITY_REVERSED_Z
                float occluderDepthDelta = sampledDepth - receiverDepth - _MmdSelfShadowParams.y;
            #else
                float occluderDepthDelta = receiverDepth - sampledDepth - _MmdSelfShadowParams.y;
            #endif
                if (occluderDepthDelta <= 0.0)
                {
                    return 1.0h;
                }

                return 1.0h - saturate(occluderDepthDelta * 1500.0h - 0.3h);
            }

            static const float2 PoissonDisk[8] = {
                float2(-0.7071, 0.7071), float2(0.0, -1.0),
                float2(0.7071, 0.7071), float2(-1.0, 0.0),
                float2(0.3536, -0.3536), float2(-0.3536, -0.3536),
                float2(0.3536, 0.3536), float2(-0.3536, 0.3536)
            };

            half SampleMmdSelfShadow(float3 positionWS, half selfShadowReceive)
            {
                if (_MmdSelfShadowParams.x <= 0.5 || selfShadowReceive <= 0.5h)
                {
                    return 1.0h;
                }

                float4 shadowCoord = mul(_MmdSelfShadowWorldToShadow, float4(positionWS, 1.0));
                shadowCoord.xyz /= max(shadowCoord.w, 1e-5);
                if (any(shadowCoord.xyz < 0.0) || any(shadowCoord.xyz > 1.0))
                {
                    return 1.0h;
                }

                if (_MmdSelfShadowParams.z <= 1.5)
                {
                    half sampledDepth = SAMPLE_TEXTURE2D(_MmdSelfShadowMap, sampler_MmdSelfShadowMap, shadowCoord.xy).r;
                    return ComputeMmdSelfShadowVisibility(shadowCoord.z, sampledDepth);
                }

                int tapCount = _MmdSelfShadowParams.z <= 4.5 ? 4 : 8;
                half visibility = 0.0h;
                for (int i = 0; i < tapCount; i++)
                {
                    float2 sampleUv = shadowCoord.xy + PoissonDisk[i] * _MmdSelfShadowParams.w;
                    if (any(sampleUv < 0.0) || any(sampleUv > 1.0))
                    {
                        visibility += 1.0h;
                        continue;
                    }

                    half sampledDepth = SAMPLE_TEXTURE2D(_MmdSelfShadowMap, sampler_MmdSelfShadowMap, sampleUv).r;
                    visibility += ComputeMmdSelfShadowVisibility(shadowCoord.z, sampledDepth);
                }

                return visibility / tapCount;
            }

            half4 ForwardFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(_BodyVisible - 0.5h);
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half albedoAlpha = baseMap.a * _BaseColor.a * _DiagnosticColor.a;
                clip(albedoAlpha - _AlphaClipThreshold);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lightDirection = dot(_MmdLightDirection.xyz, _MmdLightDirection.xyz) > 0.0h
                    ? normalize(_MmdLightDirection.xyz)
                    : mainLight.direction;
                half3 mainLightSrgb = LinearToSRGB(_MmdLightColor.rgb) * LinearToSRGB(mainLight.color)
                    * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half selfShadowVisibility = SampleMmdSelfShadow(input.positionWS, _MmdSelfShadowReceive);

                half3 normalWS;
                if (_MmdNormalMapBound > 0.5h)
                {
                    half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_MmdNormalMap, sampler_MmdNormalMap, input.uv));
                    float3 tangentWS = normalize(input.tangentWS);
                    float3 bitangentWS = normalize(input.bitangentWS);
                    float3 geometricNormalWS = normalize(input.normalWS);
                    normalWS = normalize(tangentWS * normalTS.x + bitangentWS * normalTS.y + geometricNormalWS * normalTS.z);
                }
                else
                {
                    normalWS = normalize(input.normalWS);
                }

                half ndotl = saturate(dot(normalWS, lightDirection));
                half lightVisibility = saturate(dot(normalWS, lightDirection) * 3.0h);
                half toonVisibility = min(selfShadowVisibility, lightVisibility);
                half3 fallbackSelfShadowToon = half3(1.0h, 1.0h, 1.0h);
                half3 mappedSelfShadowToon = SAMPLE_TEXTURE2D(_ToonMap, sampler_ToonMap, float2(0.5, 0.22)).rgb;
                half3 selfShadowToon = lerp(fallbackSelfShadowToon, mappedSelfShadowToon, saturate(_ToonMapBound));
                half3 mmdToonLight = lerp(selfShadowToon, half3(1.0h, 1.0h, 1.0h), lightVisibility);
                half3 toonLight = lerp(ndotl.xxx, mmdToonLight, _ToonStrength);
                if (selfShadowVisibility < 0.999h)
                {
                    half3 selfShadowMmdToonLight = lerp(selfShadowToon, half3(1.0h, 1.0h, 1.0h), toonVisibility);
                    half3 selfShadowToonLight = lerp(ndotl.xxx, selfShadowMmdToonLight, _ToonStrength);
                    toonLight = min(toonLight, selfShadowToonLight);
                }

                half3 baseSrgb = saturate(LinearToSRGB(_BaseColor.rgb) * mainLightSrgb + LinearToSRGB(_AmbientColor.rgb));
                half3 albedoSrgb = baseSrgb * LinearToSRGB(baseMap.rgb) * LinearToSRGB(_Color.rgb) * LinearToSRGB(_DiagnosticColor.rgb);
                if (_SphereMode > 0.5h)
                {
                    half3 normalVS = normalize(mul((float3x3)UNITY_MATRIX_V, normalWS));
                    float2 sphereUV = float2(normalVS.x * 0.5h + 0.5h, 1.0h - (normalVS.y * 0.5h + 0.5h));
                    half3 sphereSrgb = LinearToSRGB(SAMPLE_TEXTURE2D(_SphereMap, sampler_SphereMap, sphereUV).rgb);
                    albedoSrgb = _SphereMode < 1.5h ? albedoSrgb * sphereSrgb : saturate(albedoSrgb + sphereSrgb);
                }

                half3 litSrgb = saturate(albedoSrgb * LinearToSRGB(toonLight));
                litSrgb = lerp(litSrgb, saturate(albedoSrgb * _TextureFlatLightingValue), saturate(_BaseMapBound * _TextureFlatLightingWeight));
                half4 color;
                color.rgb = _GammaTarget > 0.5h ? litSrgb : SRGBToLinear(litSrgb);
                color.a = lerp(_Alpha, albedoAlpha, saturate(_TextureAlphaOutputWeight));
                return color;
            }
            ENDHLSL
        }

        UsePass "MMD Basic URP Toon/MmdSelfShadowCaster"
        UsePass "MMD Basic URP Toon/ShadowCaster"
    }
}
