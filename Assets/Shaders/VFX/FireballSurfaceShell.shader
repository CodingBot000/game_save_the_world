Shader "Titan Destroyer/VFX/Fireball Surface Shell"
{
    Properties
    {
        _FireFrame ("Fire Frame", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 0.72, 0.28, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.88
        _EmissionStrength ("Emission Strength", Range(0, 12)) = 3
        _FrontStart ("Front Mask Start", Range(-1, 1)) = -0.12
        _FrontEnd ("Front Mask End", Range(-1, 1)) = 0.64
        _DarkCutoff ("Dark Cutoff", Range(0, 1)) = 0.03
        _DarkSoftness ("Dark Softness", Range(0.001, 1)) = 0.14
        _EdgeFadeStart ("Projected Edge Fade Start", Range(0, 1)) = 0.84
        _EdgeFadeEnd ("Projected Edge Fade End", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "FireballSurfaceShell"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FireFrame);
            SAMPLER(sampler_FireFrame);

            CBUFFER_START(UnityPerMaterial)
                float4 _FireFrame_ST;
                half4 _Tint;
                half _Alpha;
                half _EmissionStrength;
                half _FrontStart;
                half _FrontEnd;
                half _DarkCutoff;
                half _DarkSoftness;
                half _EdgeFadeStart;
                half _EdgeFadeEnd;
                float4 _ProjectileForwardWS;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 forwardWS = _ProjectileForwardWS.xyz;
                forwardWS = dot(forwardWS, forwardWS) > 0.0001 ? normalize(forwardWS) : float3(0, 0, 1);

                float3 referenceUp = abs(forwardWS.y) > 0.96 ? float3(1, 0, 0) : float3(0, 1, 0);
                float3 rightWS = normalize(cross(referenceUp, forwardWS));
                float3 upWS = normalize(cross(forwardWS, rightWS));

                float2 projected = float2(dot(normalWS, rightWS), dot(normalWS, upWS));
                float2 uv = projected * 0.5 + 0.5;
                half4 frame = SAMPLE_TEXTURE2D(_FireFrame, sampler_FireFrame, TRANSFORM_TEX(uv, _FireFrame));

                half luminance = dot(frame.rgb, half3(0.2126, 0.7152, 0.0722));
                half darkMask = smoothstep(_DarkCutoff, _DarkCutoff + _DarkSoftness, luminance);
                half front = dot(normalWS, forwardWS);
                half frontMask = smoothstep(_FrontStart, _FrontEnd, front);
                half edgeMask = 1.0h - smoothstep(_EdgeFadeStart, _EdgeFadeEnd, length(projected));
                half alpha = frame.a * darkMask * frontMask * edgeMask * _Alpha;

                half3 color = frame.rgb * _Tint.rgb * _EmissionStrength;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
