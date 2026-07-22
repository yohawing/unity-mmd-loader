Shader "MMD Toon Lit"
{
    // MMD Toon Lit is opt-in. Keep the Legacy MMD Toon shader byte-for-byte intact; this shader
    // connects ForwardLit to URP lighting/fog and reuses the Legacy outline/self-shadow/caster passes.
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
        [Enum(MMD Toon Ramp, 0, Shade Colors, 1)] _ToonAuthoringMode ("Toon Authoring Mode", Float) = 0
        _ShadeBaseColor ("Base Shade Color", Color) = (1,1,1,1)
        _FirstShadeColor ("1st Shade Color", Color) = (0.5,0.5,0.5,1)
        _SecondShadeColor ("2nd Shade Color", Color) = (0.1,0.1,0.1,1)
        _BaseToFirstShadeBoundary ("Base / 1st Boundary", Range(0,1)) = 0.5
        _BaseToFirstShadeFeather ("Base / 1st Feather", Range(0,1)) = 0.1
        _FirstToSecondShadeBoundary ("1st / 2nd Boundary", Range(0,1)) = 0.25
        _FirstToSecondShadeFeather ("1st / 2nd Feather", Range(0,1)) = 0.1
        // Optional authoring controls. -1 keeps the current MMD visibility ramp exactly intact.
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
        _MmdNormalMapBound ("Normal Map Bound", Float) = 0
        // Reflection probes are opt-in so existing Toon Lit captures keep their current look.
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
            // This shader has no UniversalGBuffer pass, so Deferred renderers must route it
            // through the forward-only queue rather than fall back to an unrelated shader.
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex ForwardVertex
            #pragma fragment ForwardFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

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
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_MmdEmissionMask);
            SAMPLER(sampler_MmdEmissionMask);

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
                half _ReceiveSSAO;
                half _ToonStrength;
                half _ToonAuthoringMode;
                half4 _ShadeBaseColor;
                half4 _FirstShadeColor;
                half4 _SecondShadeColor;
                half _BaseToFirstShadeBoundary;
                half _BaseToFirstShadeFeather;
                half _FirstToSecondShadeBoundary;
                half _FirstToSecondShadeFeather;
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
                half _ReflectionProbeWeight;
                half _ToonBoundary;
                half _ToonFeather;
                half _ToonBandCount;
                half4 _StylizedSpecularColor;
                half _StylizedSpecularBoundary;
                half _StylizedSpecularFeather;
                half4 _RimColor;
                half _RimBoundary;
                half _RimFeather;
                half _RimLightFollow;
                half4 _EmissionColor;
                half _MmdEmissionIntensity;
                half _MmdEmissionMapBound;
                half _MmdEmissionMaskBound;
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
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ForwardVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
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

            half ApplyMmdToonBoundary(half visibility)
            {
                // The negative sentinel is intentionally fail-open: existing Toon Lit
                // materials retain the exact visibility ramp unless authoring opts in.
                if (_ToonBoundary < -0.5h)
                {
                    return visibility;
                }

                half boundary = saturate(_ToonBoundary);
                half feather = _ToonFeather < -0.5h ? 0.0h : saturate(_ToonFeather);
                if (feather <= 1e-4h)
                {
                    return step(boundary, visibility);
                }

                half lower = max(0.0h, boundary - feather);
                half upper = min(1.0h, boundary + feather);
                return smoothstep(lower, max(lower + 1e-4h, upper), visibility);
            }

            half ApplyMmdToonBandCount(half visibility)
            {
                // Negative is the compatibility sentinel. Explicit values quantize the
                // visibility ramp into a small, deterministic number of evenly spaced bands.
                if (_ToonBandCount < 0.5h)
                {
                    return visibility;
                }

                half bandCount = max(1.0h, min(8.0h, floor(_ToonBandCount + 0.5h)));
                if (bandCount <= 1.0h)
                {
                    return 0.0h;
                }

                return floor(saturate(visibility) * (bandCount - 1.0h) + 0.5h) / (bandCount - 1.0h);
            }

            half ApplyMmdShadeThreshold(half visibility, half boundary, half feather)
            {
                boundary = saturate(boundary);
                feather = saturate(feather);
                if (feather <= 1e-4h)
                {
                    return step(boundary, visibility);
                }

                half lower = max(0.0h, boundary - feather);
                half upper = min(1.0h, boundary + feather);
                return smoothstep(lower, max(lower + 1e-4h, upper), visibility);
            }

            half3 ApplyMmdShadeColors(half visibility)
            {
                half baseToFirst = ApplyMmdShadeThreshold(
                    visibility,
                    _BaseToFirstShadeBoundary,
                    _BaseToFirstShadeFeather);
                half firstToSecond = ApplyMmdShadeThreshold(
                    visibility,
                    _FirstToSecondShadeBoundary,
                    _FirstToSecondShadeFeather);
                half3 secondToFirst = lerp(_SecondShadeColor.rgb, _FirstShadeColor.rgb, firstToSecond);
                return lerp(secondToFirst, _ShadeBaseColor.rgb, baseToFirst);
            }

            half ComputeMmdStylizedSpecularMask(
                half3 normalWS,
                half3 lightDirection,
                half3 viewDirectionWS)
            {
                // A negative boundary is the sole compatibility sentinel: no specular
                // contribution is emitted unless authoring explicitly opts in.
                if (_StylizedSpecularBoundary < -0.5h)
                {
                    return 0.0h;
                }

                half3 halfVector = SafeNormalize(lightDirection + viewDirectionWS);
                half ndoth = saturate(dot(normalWS, halfVector));
                half boundary = saturate(_StylizedSpecularBoundary);
                half feather = _StylizedSpecularFeather < -0.5h
                    ? 0.0h
                    : saturate(_StylizedSpecularFeather);
                if (feather <= 1e-4h)
                {
                    return step(boundary, ndoth) * step(1e-4h, dot(normalWS, lightDirection));
                }

                half lower = max(0.0h, boundary - feather);
                half upper = min(1.0h, boundary + feather);
                return smoothstep(lower, max(lower + 1e-4h, upper), ndoth) *
                    step(1e-4h, dot(normalWS, lightDirection));
            }

            half ComputeMmdRimMask(half3 normalWS, half3 viewDirectionWS)
            {
                // The negative boundary is the sole compatibility sentinel.
                if (_RimBoundary < -0.5h)
                {
                    return 0.0h;
                }

                // Cull Off materials must not turn their entire back face into rim light.
                half rim = 1.0h - saturate(abs(dot(normalWS, viewDirectionWS)));
                half boundary = saturate(_RimBoundary);
                half feather = _RimFeather < -0.5h ? 0.0h : saturate(_RimFeather);
                if (feather <= 1e-4h)
                {
                    return step(boundary, rim);
                }

                half lower = max(0.0h, boundary - feather);
                half upper = min(1.0h, boundary + feather);
                return smoothstep(lower, max(lower + 1e-4h, upper), rim);
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
                // Keep light radiance (directional color and distance attenuation) separate
                // from shadow visibility. Shadow visibility enters the toon ramp below rather
                // than directly gray-multiplying the radiance.
                half3 mainLightSrgb = LinearToSRGB(_MmdLightColor.rgb) * LinearToSRGB(mainLight.color)
                    * mainLight.distanceAttenuation;
                // Some PMX textures use the Legacy flat-lighting compatibility path.  Keep
                // its white, intensity-1 result unchanged, but do not let it bypass Unity's
                // main-light radiance or attenuation in the opt-in Toon Lit profile.
                half3 unityMainLightSrgb = LinearToSRGB(mainLight.color)
                    * mainLight.distanceAttenuation;
                half mainLightShadowVisibility = saturate(mainLight.shadowAttenuation);
                half selfShadowVisibility = SampleMmdSelfShadow(input.positionWS, _MmdSelfShadowReceive);
                half combinedShadowVisibility = min(mainLightShadowVisibility, selfShadowVisibility);
                // MMD Ramp materials without a bound toon map have no authoring shade color
                // to receive the visibility ramp. Preserve the previous direct-shadow response
                // only for that compatibility case; bound toon maps keep shadow in toon visibility.
                if (_ToonAuthoringMode <= 0.5h && _ToonMapBound <= 0.5h)
                {
                    mainLightSrgb *= mainLightShadowVisibility;
                    unityMainLightSrgb *= mainLightShadowVisibility;
                }

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
                half toonRampVisibility = ApplyMmdToonBoundary(ApplyMmdToonBandCount(lightVisibility));
                half toonVisibility = ApplyMmdToonBoundary(
                    ApplyMmdToonBandCount(min(combinedShadowVisibility, lightVisibility)));
                half3 fallbackSelfShadowToon = half3(1.0h, 1.0h, 1.0h);
                half3 mappedSelfShadowToon = SAMPLE_TEXTURE2D(_ToonMap, sampler_ToonMap, float2(0.5, 0.22)).rgb;
                half3 selfShadowToon = lerp(fallbackSelfShadowToon, mappedSelfShadowToon, saturate(_ToonMapBound));
                half3 mmdToonLight = lerp(selfShadowToon, half3(1.0h, 1.0h, 1.0h), toonRampVisibility);
                half3 toonLight = lerp(ndotl.xxx, mmdToonLight, _ToonStrength);
                if (combinedShadowVisibility < 0.999h)
                {
                    half3 selfShadowMmdToonLight = lerp(selfShadowToon, half3(1.0h, 1.0h, 1.0h), toonVisibility);
                    half3 selfShadowToonLight = lerp(ndotl.xxx, selfShadowMmdToonLight, _ToonStrength);
                    toonLight = min(toonLight, selfShadowToonLight);
                }
                if (_ToonAuthoringMode > 0.5h)
                {
                    half shadeVisibility = min(combinedShadowVisibility, lightVisibility);
                    half3 shadeColorToonLight = ApplyMmdShadeColors(shadeVisibility);
                    toonLight = lerp(ndotl.xxx, shadeColorToonLight, _ToonStrength);
                }

                half3 ambientShSrgb = LinearToSRGB(SampleSH(normalWS));
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(
                        normalizedScreenSpaceUV,
                        half(1.0h));
                    // URP's AO factor is intentionally applied to indirect light only. The MMD
                    // direct/toon ramp and dedicated self-shadow remain unchanged when the
                    // Renderer Feature is toggled on or off.
                    if (_ReceiveSSAO > 0.5h)
                    {
                        ambientShSrgb *= aoFactor.indirectAmbientOcclusion;
                    }
                #endif
                half3 reflectionSrgb = half3(0.0h, 0.0h, 0.0h);
                #if !defined(_SURFACE_TYPE_TRANSPARENT)
                if (_ReflectionProbeWeight > 0.0h && _SphereMode <= 0.5h)
                {
                    half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    half3 reflectionWS = reflect(-viewDirectionWS, normalWS);
                    float2 reflectionScreenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    half3 reflectionLinear = GlossyEnvironmentReflection(
                        reflectionWS,
                        input.positionWS,
                        half(0.5h),
                        half(1.0h),
                        reflectionScreenUV);
                    reflectionSrgb = LinearToSRGB(reflectionLinear) * _ReflectionProbeWeight;
                }
                #endif
                // Keep direct and reflection radiance under the diffuse/base color. PMX ambient
                // color modulates URP SH ambient instead of being added as an unconditional
                // constant that can saturate and wash out directional-light color response.
                half3 diffuseSrgb = LinearToSRGB(_BaseColor.rgb) * (mainLightSrgb + reflectionSrgb);
                half3 ambientSrgb = ambientShSrgb * LinearToSRGB(_AmbientColor.rgb);
                half3 baseSrgb = saturate(diffuseSrgb + ambientSrgb);
                half3 albedoSrgb = baseSrgb * LinearToSRGB(baseMap.rgb) * LinearToSRGB(_Color.rgb) * LinearToSRGB(_DiagnosticColor.rgb);
                if (_SphereMode > 0.5h)
                {
                    half3 normalVS = normalize(mul((float3x3)UNITY_MATRIX_V, normalWS));
                    float2 sphereUV = float2(normalVS.x * 0.5h + 0.5h, 1.0h - (normalVS.y * 0.5h + 0.5h));
                    half3 sphereSrgb = LinearToSRGB(SAMPLE_TEXTURE2D(_SphereMap, sampler_SphereMap, sphereUV).rgb);
                    albedoSrgb = _SphereMode < 1.5h ? albedoSrgb * sphereSrgb : saturate(albedoSrgb + sphereSrgb);
                }

                half3 litSrgb = saturate(albedoSrgb * LinearToSRGB(toonLight));
                half3 flatLitSrgb = saturate(albedoSrgb * _TextureFlatLightingValue * unityMainLightSrgb);
                litSrgb = lerp(litSrgb, flatLitSrgb, saturate(_BaseMapBound * _TextureFlatLightingWeight));
                if (_StylizedSpecularBoundary >= -0.5h)
                {
                    half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    half specularMask = ComputeMmdStylizedSpecularMask(normalWS, lightDirection, viewDirectionWS);
                    half3 stylizedSpecularSrgb = LinearToSRGB(_StylizedSpecularColor.rgb) *
                        mainLightSrgb * combinedShadowVisibility * specularMask;
                    litSrgb = saturate(litSrgb + stylizedSpecularSrgb);
                }
                if (_RimBoundary >= -0.5h)
                {
                    half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    half rimMask = ComputeMmdRimMask(normalWS, viewDirectionWS);
                    half3 fixedRimSrgb = LinearToSRGB(_RimColor.rgb) * rimMask;
                    half lightFacing = saturate(dot(normalWS, lightDirection));
                    half3 followRimSrgb = LinearToSRGB(_RimColor.rgb) * mainLightSrgb *
                        lightFacing * combinedShadowVisibility * rimMask;
                    half3 rimSrgb = lerp(fixedRimSrgb, followRimSrgb, saturate(_RimLightFollow));
                    litSrgb = saturate(litSrgb + rimSrgb);
                }
                half3 emissionLinear = half3(0.0h, 0.0h, 0.0h);
                if (_MmdEmissionIntensity >= 0.0h)
                {
                    half3 emissionMap = _MmdEmissionMapBound > 0.5h
                        ? SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb
                        : half3(1.0h, 1.0h, 1.0h);
                    half emissionMask = _MmdEmissionMaskBound > 0.5h
                        ? SAMPLE_TEXTURE2D(_MmdEmissionMask, sampler_MmdEmissionMask, input.uv).r
                        : 1.0h;
                    emissionLinear = max(_EmissionColor.rgb, 0.0h) *
                        _MmdEmissionIntensity * emissionMap * emissionMask;
                }
                half3 foggedLinear = MixFog(SRGBToLinear(litSrgb) + emissionLinear, input.fogFactor);
                half4 color;
                color.rgb = _GammaTarget > 0.5h ? LinearToSRGB(foggedLinear) : foggedLinear;
                color.a = lerp(_Alpha, albedoAlpha, saturate(_TextureAlphaOutputWeight));
                return color;
            }
            ENDHLSL
        }

        // URP SSAO consumes the camera depth and normals textures. Keep these passes local to
        // the opt-in Toon Lit shader so Legacy MMD Toon remains byte-for-byte unchanged.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex MmdDepthOnlyVertex
            #pragma fragment MmdDepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _DiagnosticColor;
                half _AlphaClipThreshold;
                half _BodyVisible;
            CBUFFER_END

            struct MmdDepthOnlyAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct MmdDepthOnlyVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            MmdDepthOnlyVaryings MmdDepthOnlyVertex(MmdDepthOnlyAttributes input)
            {
                MmdDepthOnlyVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half MmdDepthOnlyFragment(MmdDepthOnlyVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a *
                    _BaseColor.a * _DiagnosticColor.a;
                clip(_BodyVisible - 0.5h);
                clip(alpha - _AlphaClipThreshold);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // URP's DepthNormals texture is the source consumed by the official SSAO Renderer
        // Feature. Match ForwardLit's normal-map binding and world-space tangent basis exactly.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex MmdDepthNormalsVertex
            #pragma fragment MmdDepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MmdNormalMap);
            SAMPLER(sampler_MmdNormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _DiagnosticColor;
                half _AlphaClipThreshold;
                half _BodyVisible;
                half _MmdNormalMapBound;
            CBUFFER_END

            struct MmdDepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct MmdDepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            MmdDepthNormalsVaryings MmdDepthNormalsVertex(MmdDepthNormalsAttributes input)
            {
                MmdDepthNormalsVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                float3 tangentWS = normalize(TransformObjectToWorldDir(input.tangentOS.xyz));
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.tangentWS = tangentWS;
                output.bitangentWS = cross(normalWS, tangentWS) * input.tangentOS.w;
                return output;
            }

            void MmdDepthNormalsFragment(
                MmdDepthNormalsVaryings input,
                out half4 outNormalWS : SV_Target0)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a *
                    _BaseColor.a * _DiagnosticColor.a;
                clip(_BodyVisible - 0.5h);
                clip(alpha - _AlphaClipThreshold);

                half3 normalWS = normalize(input.normalWS);
                if (_MmdNormalMapBound > 0.5h)
                {
                    half3 normalTS = UnpackNormal(
                        SAMPLE_TEXTURE2D(_MmdNormalMap, sampler_MmdNormalMap, input.uv));
                    normalWS = normalize(
                        normalize(input.tangentWS) * normalTS.x +
                        normalize(input.bitangentWS) * normalTS.y +
                        normalWS * normalTS.z);
                }

                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    outNormalWS = half4(packedNormalWS, 0.0h);
                #else
                    outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0h);
                #endif
            }
            ENDHLSL
        }

        UsePass "MMD Basic URP Toon/MmdSelfShadowCaster"
        UsePass "MMD Basic URP Toon/ShadowCaster"
    }

    CustomEditor "Mmd.Editor.MmdToonLitShaderGUI"
}
