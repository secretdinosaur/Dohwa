Shader "Custom/URP_Leaf_Stencil_Unlit_SplitAlpha_LOD_ForceInstanced_Fade"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGB)", 2D) = "white" {}
        _AlphaMap("Alpha Map (Grayscale)", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [Header(Distance Fade)]
        _FadeStartDist("Fade Start Distance", Float) = 20.0
        _FadeEndDist("Fade End Distance", Float) = 50.0
        _FadeStrength("Fade Strength", Range(0.0, 3.0)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "TransparentCutout" 
            "Queue" = "AlphaTest" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        LOD 100

        Pass
        {
            Name "Unlit"
            
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            Cull Off
            ZWrite On
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_instancing
            // 절차적 인스턴싱 관련 옵션 제거
            #pragma instancing_options assumeuniformscaling force_concat_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            // 인스턴싱 버퍼 외부로 분리
            float4 _BaseMap_ST; 

            TEXTURE2D(_AlphaMap);
            SAMPLER(sampler_AlphaMap);

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(half, _Cutoff)
                UNITY_DEFINE_INSTANCED_PROP(float, _FadeStartDist)
                UNITY_DEFINE_INSTANCED_PROP(float, _FadeEndDist)
                UNITY_DEFINE_INSTANCED_PROP(float, _FadeStrength)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);

                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                half cutoff = UNITY_ACCESS_INSTANCED_PROP(Props, _Cutoff);
                float fadeStart = UNITY_ACCESS_INSTANCED_PROP(Props, _FadeStartDist);
                float fadeEnd = UNITY_ACCESS_INSTANCED_PROP(Props, _FadeEndDist);
                float fadeStrength = UNITY_ACCESS_INSTANCED_PROP(Props, _FadeStrength);

                half3 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half alpha = SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, input.uv).r;

                float dist = distance(_WorldSpaceCameraPos, input.positionWS);
                float fadeFactor = saturate((fadeEnd - dist) / max(0.001, (fadeEnd - fadeStart)));
                fadeFactor = lerp(1.0, fadeFactor, fadeStrength);

                alpha *= fadeFactor;
                clip(alpha - cutoff);

                return half4(texColor * baseColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}