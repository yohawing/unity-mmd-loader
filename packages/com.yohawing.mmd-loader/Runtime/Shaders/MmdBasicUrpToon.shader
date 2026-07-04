Shader "MMD Basic URP Toon"
{
    // Shadow ownership boundary:
    // - URP standard shadows are only for casting MMD characters onto the scene/environment.
    // - MMD character shadow receive comes only from the dedicated MMD self-shadow map.
    // - ForwardLit intentionally does not sample URP main-light shadow attenuation.
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

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "MmdOutline" }

            Cull Front
            ZWrite [_OutlineZWrite]
            ZTest [_OutlineZTest]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Color.hlsl supplies LinearToSRGB for the _GammaTarget outline path (the ForwardLit
            // pass gets it transitively via Lighting.hlsl, but this pass only includes Core.hlsl).
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

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
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output;
                float3 normalOS = normalize(input.normalOS);
                half edgeScale = input.uv1.y > 0.5h ? input.uv1.x : 1.0h;
                float scaledWidth = _OutlineWidth * edgeScale * 2.0; // HighDPI fix 2.0
                float4 meshNormalPositionCS = TransformObjectToHClip(input.positionOS.xyz + normalOS * scaledWidth);
                float4 basePositionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
                float2 screenNormal = normalVS.xy;
                screenNormal.y = -screenNormal.y;
                float screenNormalLength = length(screenNormal);
                screenNormal = screenNormalLength > 0.0 ? screenNormal / screenNormalLength : float2(0.0, 0.0);
                float4 screenPositionCS = basePositionCS;
                screenPositionCS.xy += screenNormal / (_ScreenParams.xy * 0.25) * scaledWidth * basePositionCS.w;
                output.positionCS = lerp(meshNormalPositionCS, screenPositionCS, saturate(_OutlineScreenSpaceWeight));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                clip(_OutlineVisible - 0.5h);
                // saba's mmd_edge.frag emits the edge color verbatim and does NOT clip on the diffuse
                // texture alpha. So the edge follows the geometry silhouette regardless of texture
                // cutouts/transparency (e.g. a cutout hole still shows the black edge hull behind it,
                // and a hair sheet keeps its full border). Do not clip the edge by baseMap.a.
                half4 color = _OutlineColor;
                // When compositing into a gamma (non-sRGB) target, the hardware stores the
                // fragment value verbatim, so emit the sRGB-encoded outline color to match the
                // gamma-space alpha-over used by real MMD. (_OutlineColor is uploaded linear.)
                color.rgb = _GammaTarget > 0.5h ? LinearToSRGB(color.rgb) : color.rgb;
                return color;
            }
            ENDHLSL
        }

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_SphereMap);
            SAMPLER(sampler_SphereMap);
            TEXTURE2D(_ToonMap);
            SAMPLER(sampler_ToonMap);
            TEXTURE2D(_MmdSelfShadowMap);
            SAMPLER(sampler_MmdSelfShadowMap);

            float4x4 _MmdSelfShadowWorldToShadow;
            float4 _MmdSelfShadowParams;

            UNITY_INSTANCING_BUFFER_START(MmdPerRenderer)
                UNITY_DEFINE_INSTANCED_PROP(float, _MmdSelfShadowReceive)
            UNITY_INSTANCING_BUFFER_END(MmdPerRenderer)

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
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
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

                half sampledDepth = SAMPLE_TEXTURE2D(_MmdSelfShadowMap, sampler_MmdSelfShadowMap, shadowCoord.xy).r;
                return ComputeMmdSelfShadowVisibility(shadowCoord.z, sampledDepth);
            }

            half4 ForwardFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(_BodyVisible - 0.5h);
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                // Alpha follows the original albedo alpha chain (baseMap * _BaseColor.a * _DiagnosticColor.a).
                half albedoAlpha = baseMap.a * _BaseColor.a * _DiagnosticColor.a;
                clip(albedoAlpha - _AlphaClipThreshold);

                Light mainLight = GetMainLight();
                half3 lightDirection = dot(_MmdLightDirection.xyz, _MmdLightDirection.xyz) > 0.0h
                    ? normalize(_MmdLightDirection.xyz)
                    : mainLight.direction;
                half selfShadowReceive = (half)UNITY_ACCESS_INSTANCED_PROP(MmdPerRenderer, _MmdSelfShadowReceive);
                half selfShadowVisibility = SampleMmdSelfShadow(input.positionWS, selfShadowReceive);

                half3 normalWS = normalize(input.normalWS);
                half ndotl = saturate(dot(normalWS, lightDirection));
                half lightVisibility = saturate(dot(normalWS, lightDirection) * 3.0h);
                half toonVisibility = min(selfShadowVisibility, lightVisibility);
                // MMD's traced self-shadow pixel shader receives ToonColor as a constant, not
                // as a ramp sample. Unity's built-in shared toon strips put the very dark MMD
                // band at v=0, so do not use the traced custom-texture bottom row for every material.
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

                // MMD performs its fixed-function lighting in gamma (sRGB) space, so the diffuse,
                // ambient, light and toon are combined on the sRGB-valued colors — not on the
                // linear values Unity uploads. Convert each back to sRGB, apply saba's mix
                // (base = clamp(diffuse*lightColor + ambient, 0, 1); base *= texture; base *= toon),
                // then convert the result to linear for the sRGB render target. (_MmdLightColor
                // defaults to white, so runtime/GameView output is unchanged.)
                half3 baseSRGB = saturate(LinearToSRGB(_BaseColor.rgb) * LinearToSRGB(_MmdLightColor.rgb) + LinearToSRGB(_AmbientColor.rgb));
                half3 albedoSRGB = baseSRGB * LinearToSRGB(baseMap.rgb) * LinearToSRGB(_Color.rgb) * LinearToSRGB(_DiagnosticColor.rgb);

                // Sphere (matcap) texture, applied after the diffuse texture and before the
                // toon ramp, matching saba's mmd.frag. The sphere UV is the view-space normal
                // projected to [0,1] (spUV.x = nx*0.5+0.5, spUV.y = 1 - (ny*0.5+0.5)).
                // Mode 1 = multiply (.sph), mode 2 = additive (.spa). Sampled in sRGB so the
                // composite stays in gamma space like the rest of this shader.
                if (_SphereMode > 0.5h)
                {
                    half3 normalVS = normalize(mul((float3x3)UNITY_MATRIX_V, normalWS));
                    float2 sphereUV = float2(normalVS.x * 0.5h + 0.5h, 1.0h - (normalVS.y * 0.5h + 0.5h));
                    half3 sphereSRGB = LinearToSRGB(SAMPLE_TEXTURE2D(_SphereMap, sampler_SphereMap, sphereUV).rgb);
                    albedoSRGB = _SphereMode < 1.5h ? albedoSRGB * sphereSRGB : saturate(albedoSRGB + sphereSRGB);
                }

                half3 toonSRGB = LinearToSRGB(toonLight);
                half3 litSRGB = saturate(albedoSRGB * toonSRGB);
                litSRGB = lerp(litSRGB, saturate(albedoSRGB * _TextureFlatLightingValue), saturate(_BaseMapBound * _TextureFlatLightingWeight));

                // MMD composites transparency in gamma (sRGB) space, so the parity harness
                // renders into a non-sRGB target and sets _GammaTarget=1: emit litSRGB verbatim
                // so the fixed-function blend operates on sRGB-encoded values (gamma-space
                // alpha-over). The runtime GameView keeps _GammaTarget=0 and converts to linear
                // for its sRGB framebuffer, so live output is unchanged.
                half4 color;
                color.rgb = _GammaTarget > 0.5h ? litSRGB : SRGBToLinear(litSRGB);
                color.a = lerp(_Alpha, albedoAlpha, saturate(_TextureAlphaOutputWeight));
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "MmdSelfShadowCaster"
            Tags { "LightMode" = "MmdSelfShadowCaster" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask R

            HLSLPROGRAM
            #pragma vertex MmdSelfShadowVertex
            #pragma fragment MmdSelfShadowFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float4x4 _MmdSelfShadowWorldToShadow;

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
            CBUFFER_END

            struct MmdSelfShadowAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct MmdSelfShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 shadowCoord : TEXCOORD1;
            };

            MmdSelfShadowVaryings MmdSelfShadowVertex(MmdSelfShadowAttributes input)
            {
                MmdSelfShadowVaryings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.shadowCoord = mul(_MmdSelfShadowWorldToShadow, float4(positionWS, 1.0));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 MmdSelfShadowFragment(MmdSelfShadowVaryings input) : SV_Target
            {
                clip(_BodyVisible - 0.5h);
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half albedoAlpha = baseMap.a * _BaseColor.a * _DiagnosticColor.a;
                clip(albedoAlpha - _ShadowAlphaClipThreshold);
                float shadowDepth = input.shadowCoord.z / max(input.shadowCoord.w, 1e-5);
                return half4((half)shadowDepth, 0.0h, 0.0h, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float3 _LightDirection;
            float3 _LightPosition;

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
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFragment(Varyings input) : SV_Target
            {
                clip(_BodyVisible - 0.5h);
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half albedoAlpha = baseMap.a * _BaseColor.a * _DiagnosticColor.a;
                clip(albedoAlpha - _ShadowAlphaClipThreshold);
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
