Shader "Hidden/MainMenuTransitionBlur"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _BlurSize ("Blur Size", Range(0, 4)) = 1
        _Darkness ("Darkness", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        GrabPass { "_MainMenuTransitionGrab" }

        Pass
        {
            ZWrite Off
            ZTest [unity_GUIZTestMode]
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _MainMenuTransitionGrab;
            float4 _MainMenuTransitionGrab_TexelSize;
            fixed4 _Color;
            float _BlurSize;
            float _Darkness;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 grabPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.grabPos = ComputeGrabScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.grabPos.xy / i.grabPos.w;
                float2 offset = _MainMenuTransitionGrab_TexelSize.xy * _BlurSize;

                fixed4 color = tex2D(_MainMenuTransitionGrab, uv) * 0.20;
                color += tex2D(_MainMenuTransitionGrab, uv + float2(offset.x, 0)) * 0.12;
                color += tex2D(_MainMenuTransitionGrab, uv - float2(offset.x, 0)) * 0.12;
                color += tex2D(_MainMenuTransitionGrab, uv + float2(0, offset.y)) * 0.12;
                color += tex2D(_MainMenuTransitionGrab, uv - float2(0, offset.y)) * 0.12;
                color += tex2D(_MainMenuTransitionGrab, uv + offset) * 0.08;
                color += tex2D(_MainMenuTransitionGrab, uv - offset) * 0.08;
                color += tex2D(_MainMenuTransitionGrab, uv + float2(offset.x, -offset.y)) * 0.08;
                color += tex2D(_MainMenuTransitionGrab, uv + float2(-offset.x, offset.y)) * 0.08;
                color.rgb *= (1.0 - _Darkness);
                color.a = _Color.a;
                return color;
            }
            ENDCG
        }
    }
}
