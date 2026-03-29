Shader "TitanDestroyer/Environment/StylizedSkyGradient"
{
    Properties
    {
        [HDR] _TopColor ("Top Color", Color) = (0.43, 0.66, 0.88, 1)
        [HDR] _HorizonColor ("Horizon Color", Color) = (0.86, 0.92, 0.97, 1)
        [HDR] _BottomColor ("Bottom Color", Color) = (0.62, 0.76, 0.89, 1)
        _HorizonOffset ("Horizon Offset", Range(-0.5, 0.5)) = -0.06
        _HorizonSoftness ("Horizon Softness", Range(0.01, 1)) = 0.26
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry-100"
            "RenderType" = "Opaque"
        }

        Cull Front
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 directionOS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                float _HorizonOffset;
                float _HorizonSoftness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.directionOS = normalize(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float height01 = saturate(input.directionOS.y * 0.5 + 0.5 + _HorizonOffset);
                float lowerBlend = smoothstep(0.15, 0.5 + _HorizonSoftness, height01);
                float upperBlend = smoothstep(0.48 - _HorizonSoftness, 0.95, height01);

                half3 lowerColor = lerp(_BottomColor.rgb, _HorizonColor.rgb, lowerBlend);
                half3 finalColor = lerp(lowerColor, _TopColor.rgb, upperBlend);
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
