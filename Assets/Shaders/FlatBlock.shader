Shader "MunCraft/FlatBlock"
{
    Properties
    {
        _Brightness ("Brightness", Range(0.5, 2.0)) = 1.0

        [Header(Distance Fog)]
        _FogColor ("Fog Color", Color) = (0.055, 0, 0.235, 1)
        _FogStart ("Fog Start", Float) = 3
        _FogEnd ("Fog End", Float) = 35
        _FogStrength ("Fog Strength", Range(0, 1)) = 1.0

        [Header(Angle Vignette)]
        _VignetteColor ("Vignette Color", Color) = (0.2, 0.031, 0, 1)
        _VignetteInnerDot ("Inner Dot (dead spot edge)", Range(0, 1)) = 0.682
        _VignetteOuterDot ("Outer Dot (full vignette)", Range(-0.5, 1)) = 0.38
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.709
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "FlatBlock"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float3 blockCenter : TEXCOORD0; // block world-space center (baked by ChunkMesher)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float fogFactor : TEXCOORD0;
                float vignetteFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float _Brightness;
                float4 _FogColor;
                float _FogStart;
                float _FogEnd;
                float _FogStrength;
                float4 _VignetteColor;
                float _VignetteInnerDot;
                float _VignetteOuterDot;
                float _VignetteStrength;
            CBUFFER_END

            // Set each frame from C# via Shader.SetGlobalVector
            float3 _MunPlayerPos;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(worldPos);
                output.color = input.color * _Brightness;

                // Both fog and vignette use the block's CENTER position (from UV0)
                // so every vertex of a block gets the same value → truly flat per-block tint.
                float3 blockCenter = input.blockCenter;

                // Distance fog: 0 = no fog, 1 = full fog
                float dist = distance(blockCenter, _MunPlayerPos);
                output.fogFactor = saturate((dist - _FogStart) / max(_FogEnd - _FogStart, 0.001));

                // Angle vignette: dot between block direction and camera forward.
                // 1 = straight ahead (no vignette), low = periphery (full vignette).
                float3 camPos = GetCameraPositionWS();
                float3 camFwd = -UNITY_MATRIX_V[2].xyz;
                float3 toBlock = normalize(blockCenter - camPos);
                float angleDot = dot(toBlock, camFwd);

                // Vignette — three zones:
                // angleDot >= innerDot  →  0 (dead spot, flat)
                // angleDot between inner and outer  →  0 to 1 (gradient)
                // angleDot <= outerDot  →  1 (full vignette)
                float vignetteRaw = (_VignetteInnerDot - angleDot) /
                    max(_VignetteInnerDot - _VignetteOuterDot, 0.001);
                output.vignetteFactor = saturate(vignetteRaw);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 col = input.color;

                // Distance fog
                col.rgb = lerp(col.rgb, _FogColor.rgb, input.fogFactor * _FogStrength);

                // Angle vignette
                col.rgb = lerp(col.rgb, _VignetteColor.rgb, input.vignetteFactor * _VignetteStrength);

                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
