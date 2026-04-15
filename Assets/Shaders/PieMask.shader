Shader "MunCraft/PieMask"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _FillAmount ("Fill Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "PieMask"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _FillAmount;
                float3 _Center;
                float3 _CamRight;
                float3 _CamUp;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Project fragment-from-center onto camera's right/up plane
                float3 toFrag = input.positionWS - _Center;
                float r = dot(toFrag, _CamRight);
                float u = dot(toFrag, _CamUp);

                // Angle from "up" direction, going clockwise (0 to 2*PI)
                float angle = atan2(r, u);
                if (angle < 0) angle += 6.28318530718;
                float angleNorm = angle / 6.28318530718;

                // Discard if outside the filled sector
                if (angleNorm > _FillAmount) discard;

                return _Color;
            }
            ENDHLSL
        }
    }
}
