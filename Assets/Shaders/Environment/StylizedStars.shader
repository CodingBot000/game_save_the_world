Shader "TitanDestroyer/Environment/StylizedStars"
{
    Properties
    {
        [HDR] _Tint ("Tint", Color) = (0.78, 0.86, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _ScrollOffset ("Scroll Offset", Vector) = (0, 0, 0, 0)
        _Density ("Density", Range(1, 128)) = 72
        _Intensity ("Intensity", Range(0, 8)) = 1.3
        _TwinkleSpeed ("Twinkle Speed", Range(0, 8)) = 1.8
        _BandCenter ("Horizon Fade", Range(0, 1)) = 0.35
        _BandWidth ("Fade Width", Range(0.01, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
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

            static const float TwoPi = 6.28318530718;

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
                half4 _Tint;
                float _Opacity;
                float4 _ScrollOffset;
                float _Density;
                float _Intensity;
                float _TwinkleSpeed;
                float _BandCenter;
                float _BandWidth;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 SphericalUv(float3 direction)
            {
                float3 dir = normalize(direction);
                float u = atan2(dir.x, dir.z) / TwoPi + 0.5;
                float v = saturate(dir.y * 0.5 + 0.5);
                return float2(u, v);
            }

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
                float2 uv = SphericalUv(input.directionOS) + _ScrollOffset.xy;
                float2 starUv = uv * _Density;
                float2 cell = floor(starUv);
                float2 localUv = frac(starUv) - 0.5;

                float seed = Hash21(cell);
                float starMask = step(0.986, seed);
                float starSize = lerp(0.18, 0.04, saturate(seed * 1.8));
                float starShape = 1.0 - smoothstep(starSize, starSize + 0.03, length(localUv));
                float twinkle = lerp(0.62, 1.0, 0.5 + 0.5 * sin(_Time.y * _TwinkleSpeed + seed * TwoPi));
                float horizonFade = smoothstep(_BandCenter, _BandCenter + _BandWidth, uv.y);

                float alpha = starMask * starShape * twinkle * horizonFade * _Opacity;
                clip(alpha - 0.001);

                half3 color = _Tint.rgb * (_Intensity * twinkle);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
