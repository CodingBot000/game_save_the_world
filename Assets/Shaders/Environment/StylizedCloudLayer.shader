Shader "TitanDestroyer/Environment/StylizedCloudLayer"
{
    Properties
    {
        [HDR] _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.5
        _ScrollOffset ("Scroll Offset", Vector) = (0, 0, 0, 0)
        _PatternScale ("Pattern Scale", Float) = 4
        _Coverage ("Coverage", Range(0, 1)) = 0.55
        _Softness ("Softness", Range(0.01, 0.8)) = 0.16
        _BandCenter ("Band Center", Range(0, 1)) = 0.65
        _BandWidth ("Band Width", Range(0.01, 1)) = 0.2
        _Intensity ("Intensity", Range(0, 8)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-100"
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
                float _PatternScale;
                float _Coverage;
                float _Softness;
                float _BandCenter;
                float _BandWidth;
                float _Intensity;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                [unroll(4)]
                for (int octave = 0; octave < 4; octave++)
                {
                    value += Noise(p) * amplitude;
                    p = p * 2.03 + 31.37;
                    amplitude *= 0.5;
                }

                return value;
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
                float2 uv = SphericalUv(input.directionOS);
                float2 noiseUv = uv * _PatternScale + _ScrollOffset.xy;
                float cloudNoise = Fbm(noiseUv);

                float cloudMask = smoothstep(_Coverage - _Softness, _Coverage + _Softness, cloudNoise);

                float bandInner = _BandCenter - _BandWidth;
                float bandOuter = _BandCenter + _BandWidth;
                float lowerBand = smoothstep(bandInner - _Softness, bandInner + _Softness, uv.y);
                float upperBand = 1.0 - smoothstep(bandOuter - _Softness, bandOuter + _Softness, uv.y);
                float bandMask = saturate(lowerBand * upperBand);

                float alpha = saturate(cloudMask * bandMask * _Opacity);
                clip(alpha - 0.001);

                half brightness = lerp(0.88, 1.08, cloudNoise) * _Intensity;
                return half4(_Tint.rgb * brightness, alpha);
            }
            ENDHLSL
        }
    }
}
