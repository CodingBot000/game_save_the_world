Shader "Titan Destroyer/VFX/Air Pressure Distortion"
{
    Properties
    {
        _Tint ("Tint", Color) = (0, 0, 0, 0)
        _Alpha ("Alpha", Range(0, 1)) = 0.78
        _DistortionStrength ("Distortion Strength", Range(0, 0.08)) = 0.045
        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.92
        _FlowSpeed ("Wave Speed", Range(0, 8)) = 2.4
        _WaveStartTime ("Wave Start Time", Float) = 0
        _EdgePower ("Wave Width", Range(0.02, 0.45)) = 0.16
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "AirPressureDistortion"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Alpha;
                float _DistortionStrength;
                float _RippleStrength;
                float _FlowSpeed;
                float _WaveStartTime;
                float _EdgePower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float4 centerScreenPos : TEXCOORD1;
                float4 edgeScreenPos : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.screenPos = ComputeScreenPos(output.positionHCS);
                output.centerScreenPos = ComputeScreenPos(TransformObjectToHClip(float3(0.0, 0.0, 0.0)));
                output.edgeScreenPos = ComputeScreenPos(TransformObjectToHClip(float3(0.5, 0.0, 0.0)));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float2 centerUV = input.centerScreenPos.xy / max(input.centerScreenPos.w, 0.0001);
                float2 edgeUV = input.edgeScreenPos.xy / max(input.edgeScreenPos.w, 0.0001);
                float2 fromCenter = screenUV - centerUV;
                float projectedRadius = max(distance(edgeUV, centerUV), 0.0001);
                float normalizedRadius = saturate(length(fromCenter) / projectedRadius);
                float2 radialDirection = fromCenter / max(length(fromCenter), 0.0001);

                float waveFront = frac(max(0.0, _Time.y - _WaveStartTime) * _FlowSpeed);
                float waveDistance = abs(normalizedRadius - waveFront);
                float wave = 1.0 - smoothstep(0.0, max(_EdgePower, 0.001), waveDistance);
                wave *= smoothstep(0.02, 0.18, normalizedRadius) * (1.0 - smoothstep(0.86, 1.0, normalizedRadius));
                wave = lerp(wave * 0.35, wave, _RippleStrength);

                float2 distortedUV = screenUV + radialDirection * (_DistortionStrength * wave);
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, distortedUV);

                half alpha = saturate(wave * _Alpha);
                half3 color = lerp(sceneColor.rgb, sceneColor.rgb + _Tint.rgb, _Tint.a * wave);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
