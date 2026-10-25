Shader "Recorded/PhaseWorld"
{
    Properties
    {
        _BaseColor ("Surface", Color) = (0.025, 0.08, 0.11, 1)
        _LineColor ("Scan / Edge", Color) = (0.18, 0.7, 0.85, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _LineColor;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 view = normalize(_WorldSpaceCameraPos - input.positionWS);
                float edge = pow(1.0 - saturate(dot(normal, view)), 3.0);
                float scan = 1.0 - smoothstep(0.015, 0.07, abs(frac(input.positionWS.y * 3.0 - _TimeParameters.x * 0.4) - 0.5));
                float3 cell = abs(frac(input.positionWS * 2.0) - 0.5);
                float grid = 1.0 - smoothstep(0.006, 0.025, min(cell.x, min(cell.y, cell.z)));
                float facing = 0.65 + 0.35 * abs(normal.y);
                return half4(_BaseColor.rgb * facing + _LineColor.rgb * (edge * 0.35 + scan * 0.24 + grid * 0.06), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
