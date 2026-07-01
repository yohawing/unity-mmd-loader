Shader "MMD Basic URP Toon"
{
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
        _MmdReceiveShadows ("MMD Receive Shadows", Float) = 1
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
                half _MmdReceiveShadows;
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Color;
                half _Alpha;
                half4 _DiagnosticColor;
                half4 _AmbientColor;
                half4 _MmdLightDirection;
                half4 _MmdLightColor;
                half _MmdReceiveShadows;
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
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
            };

            Varyings ForwardVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = TransformWorldToShadowCoord(positionWS);
                return output;
            }

            half4 ForwardFragment(Varyings input) : SV_Target
            {
                clip(_BodyVisible - 0.5h);
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                // Alpha follows the original albedo alpha chain (baseMap * _BaseColor.a * _DiagnosticColor.a).
                half albedoAlpha = baseMap.a * _BaseColor.a * _DiagnosticColor.a;
                clip(albedoAlpha - _AlphaClipThreshold);

                Light mainLight = GetMainLight(input.shadowCoord);
                half3 lightDirection = dot(_MmdLightDirection.xyz, _MmdLightDirection.xyz) > 0.0h
                    ? normalize(_MmdLightDirection.xyz)
                    : mainLight.direction;
                half shadowAttenuation = lerp(1.0h, mainLight.shadowAttenuation, saturate(_MmdReceiveShadows));
                half3 normalWS = normalize(input.normalWS);
                half ndotl = saturate(dot(normalWS, lightDirection));
                half toonCoord = saturate(dot(normalWS, lightDirection) * 0.5h + 0.5h);
                // Match the MMD toon self-shadow model used by three-mmd-loader: shadow map
                // visibility does not darken the direct light color. It pushes the toon ramp
                // coordinate toward the dark side instead.
                toonCoord = lerp(0.22h, toonCoord, shadowAttenuation);
                // MMD only applies N.L shading through the toon ramp. A material with no toon
                // texture (u_ToonTexMode == 0 in saba) is rendered completely flat: the toon
                // multiplier is white (1.0). So the fallback remains flat white; self-shadow
                // visibly affects materials through a bound toon ramp, not by dimming albedo.
                half3 fallbackToon = 1.0h.xxx;
                half3 mappedToon = SAMPLE_TEXTURE2D(_ToonMap, sampler_ToonMap, float2(0.5, toonCoord)).rgb;
                half3 toonSample = lerp(fallbackToon, mappedToon, saturate(_ToonMapBound));
                half3 toonLight = lerp(ndotl.xxx, toonSample, _ToonStrength);

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
                half _MmdReceiveShadows;
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
