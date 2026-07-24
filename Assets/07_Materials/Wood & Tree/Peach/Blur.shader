Shader "Custom/URP_FullScreenBlur_StencilTarget"
{
    Properties
    {
        _Blur("Blur Size", Range(0.0, 5.0)) = 1.0
        _Samples("Samples (Step)", Range(1, 8)) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "StencilTargetBlurPass"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            // 변경됨: 스텐실 번호 2번이 있는 곳에서만 이 블러 쉐이더가 작동합니다.
            Stencil
            {
                Ref 2
                Comp Equal
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Blur;
            float _Samples;

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texelSize = _BlitTexture_TexelSize.xy * _Blur; 
                
                half3 accumCol = half3(0, 0, 0);
                float totalWeight = 0.0;

                for (float x = -_Samples; x <= _Samples; x += 1.0)
                {
                    for (float y = -_Samples; y <= _Samples; y += 1.0)
                    {
                        float2 offset = float2(x, y) * texelSize;
                        accumCol += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset).rgb;
                        totalWeight += 1.0;
                    }
                }

                return half4(accumCol / totalWeight, 1.0);
            }
            ENDHLSL
        }
    }
}