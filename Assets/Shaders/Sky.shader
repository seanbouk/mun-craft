Shader "MunCraft/Sky"
{
    Properties
    {
        [Header(Gradient)]
        _AzureColor ("Azure (bottom)", Color) = (0.35, 0.55, 0.75, 1)
        _DeepBlueColor ("Deep Blue (top)", Color) = (0.01, 0.01, 0.06, 1)
        _GradientScale ("Gradient Scale", Range(0.5, 2.0)) = 1.1

        [Header(Stars)]
        _StarBrightness ("Star Brightness", Range(0, 3)) = 1.2
        _StarDensitySmall ("Small Star Density", Range(0, 1)) = 0.06
        _StarDensityMed ("Medium Star Density", Range(0, 1)) = 0.015
        _StarDensityBright ("Bright Star Density", Range(0, 1)) = 0.003
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
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
                float3 viewDir : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _AzureColor;
                float4 _DeepBlueColor;
                float _GradientScale;
                float _StarBrightness;
                float _StarDensitySmall;
                float _StarDensityMed;
                float _StarDensityBright;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // For skybox: object-space position IS the view direction
                output.viewDir = input.positionOS.xyz;
                return output;
            }

            // ---- Hash functions for stars ----

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(443.897, 441.423, 437.195));
                p3 += dot(p3, p3.yzx + 19.19);
                return frac((p3.x + p3.y) * p3.z);
            }

            // ---- Star layer: grid-based with neighbor check ----

            float3 StarLayer(float2 uv, float gridSize, float density, float brightness, float falloff)
            {
                float3 color = 0;
                float2 cellBase = floor(uv * gridSize);

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        float2 cell = cellBase + float2(dx, dy);

                        // Does this cell have a star?
                        float h = hash21(cell);
                        if (h > density) continue;

                        // Star position within cell (0-1)
                        float2 starPos = float2(hash21(cell + 17.1), hash21(cell + 31.3));
                        float2 delta = uv * gridSize - cell - starPos;
                        float dist = length(delta);

                        // Gaussian point — sharper falloff = smaller star
                        float intensity = brightness * exp(-dist * dist * falloff);

                        // Slight colour variation: warm / cool / white
                        float hue = hash21(cell + 7.7);
                        float3 tint;
                        if (hue < 0.15)
                            tint = float3(1.0, 0.82, 0.65);    // warm (yellow-ish)
                        else if (hue < 0.30)
                            tint = float3(0.70, 0.82, 1.0);    // cool (blue-ish)
                        else if (hue < 0.38)
                            tint = float3(0.90, 0.70, 0.90);   // faint purple
                        else
                            tint = float3(1.0, 1.0, 1.0);      // white

                        color += intensity * tint;
                    }
                }

                return color;
            }

            // ---- Stars: three layers at different densities/sizes ----

            float3 Stars(float3 dir)
            {
                // Spherical UV — direction to 2D coordinate
                float phi = atan2(dir.z, dir.x);               // -PI to PI
                float theta = acos(clamp(dir.y, -1.0, 1.0));   // 0 to PI
                float2 uv = float2(phi / 6.28318530718 + 0.5, theta / 3.14159265359);

                float3 color = 0;

                // Many faint pinpricks
                color += StarLayer(uv, 200.0, _StarDensitySmall, 0.4, 800.0);

                // Fewer medium stars (slightly larger via lower falloff)
                color += StarLayer(uv, 100.0, _StarDensityMed, 0.8, 400.0);

                // Rare bright stars (a bit of spread)
                color += StarLayer(uv, 50.0, _StarDensityBright, 1.5, 200.0);

                return color;
            }

            // ---- Fragment: gradient + stars ----

            float4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.viewDir);

                // --- Layer 1: screen-space radial gradient ---
                // Origin at bottom-centre of screen, expanding toward top corners.
                float2 screenUV = input.positionCS.xy / _ScreenParams.xy;
                float2 gradOrigin = float2(0.5, 0.0); // bottom centre
                float gradDist = distance(screenUV, gradOrigin);
                float gradFactor = saturate(gradDist / _GradientScale);
                float3 gradColor = lerp(_AzureColor.rgb, _DeepBlueColor.rgb, gradFactor);

                // --- Layer 2: stars (additive over gradient) ---
                float3 starColor = Stars(dir) * _StarBrightness;

                return float4(gradColor + starColor, 1.0);
            }
            ENDHLSL
        }
    }
}
