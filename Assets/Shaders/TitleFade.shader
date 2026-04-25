Shader "MunCraft/TitleFade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Elapsed ("Elapsed Time", Float) = 0
        _FadeDuration ("Fade Duration", Float) = 5
        _MaxDelay ("Max Delay", Float) = 20
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Cull Off
            Lighting Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float _Elapsed;
            float _FadeDuration;
            float _MaxDelay;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float brightness = (c.r + c.g + c.b) / 3.0;
                float delay = (1.0 - brightness) * _MaxDelay;
                float progress = saturate((_Elapsed - delay) / _FadeDuration);
                return float4(1, 1, 1, c.a * progress);
            }
            ENDCG
        }
    }
}
