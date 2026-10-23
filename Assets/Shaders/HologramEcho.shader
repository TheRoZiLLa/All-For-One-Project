Shader "Prologue/HologramEcho"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.25, 0.9, 1, 0.45)
        _Emission ("Emission", Color) = (0.15, 1.6, 2.4, 1)
        _Scan ("Scan Frequency", Float) = 28
        _Speed ("Speed", Float) = 1.4
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }
        Pass
        {
            Name "Hologram"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _Emission;
                float _Scan;
                float _Speed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float scan = frac(input.positionWS.y * _Scan - _TimeParameters.x * _Speed);
                float scanBand = smoothstep(0.0, 0.05, scan) * smoothstep(0.22, 0.05, scan);
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                float fresnel = pow(1.0 - saturate(dot(normalize(input.normalWS), viewDir)), 2.2);
                float alpha = _BaseColor.a * (0.18 + scanBand * 0.85 + fresnel * 0.65);
                float3 color = _BaseColor.rgb * 0.35 + _Emission.rgb * (0.45 + scanBand * 1.1 + fresnel);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
