Shader "Hidden/MMD/SelfShadowDepthDebug"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SelfShadowDepthDebug"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MmdSelfShadowMap);
            SAMPLER(sampler_MmdSelfShadowMap);

            float4 _MmdSelfShadowDebugParams;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vertex(uint vertexId : SV_VertexID)
            {
                Varyings output;
                output.uv = float2((vertexId << 1) & 2, vertexId & 2);
                output.positionCS = float4(output.uv * 2.0 - 1.0, 0.0, 1.0);
                output.positionCS.y = -output.positionCS.y;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float rawDepth = SAMPLE_TEXTURE2D(_MmdSelfShadowMap, sampler_MmdSelfShadowMap, input.uv).r;
                float clearDepth = _MmdSelfShadowDebugParams.x;
                float contrast = max(_MmdSelfShadowDebugParams.y, 1.0);
                float depthDelta = abs(rawDepth - clearDepth);
                float occupancy = step(1e-6, depthDelta);
#if UNITY_REVERSED_Z
                float depthValue = rawDepth;
#else
                float depthValue = 1.0 - rawDepth;
#endif
                float contrastedDepth = saturate(depthValue * contrast);
                float3 clearColor = float3(0.015, 0.015, 0.02);
                float3 occupiedColor = float3(contrastedDepth, occupancy, 1.0 - contrastedDepth);
                return half4(lerp(clearColor, occupiedColor, occupancy), 1.0);
            }
            ENDHLSL
        }
    }
}
