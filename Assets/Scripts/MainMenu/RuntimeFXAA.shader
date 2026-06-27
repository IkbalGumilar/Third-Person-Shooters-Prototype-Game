Shader "Hidden/RuntimeFXAA"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _TexelSize;
            float _SpanMax;
            float _ReduceMin;
            float _ReduceMul;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 inverseScreen = _TexelSize.xy;
                float3 rgbNW = tex2D(_MainTex, i.uv + float2(-1.0, -1.0) * inverseScreen).rgb;
                float3 rgbNE = tex2D(_MainTex, i.uv + float2(1.0, -1.0) * inverseScreen).rgb;
                float3 rgbSW = tex2D(_MainTex, i.uv + float2(-1.0, 1.0) * inverseScreen).rgb;
                float3 rgbSE = tex2D(_MainTex, i.uv + float2(1.0, 1.0) * inverseScreen).rgb;
                float3 rgbM = tex2D(_MainTex, i.uv).rgb;

                float3 luma = float3(0.299, 0.587, 0.114);
                float lumaNW = dot(rgbNW, luma);
                float lumaNE = dot(rgbNE, luma);
                float lumaSW = dot(rgbSW, luma);
                float lumaSE = dot(rgbSE, luma);
                float lumaM = dot(rgbM, luma);

                float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
                float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));

                float2 direction;
                direction.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
                direction.y = ((lumaNW + lumaSW) - (lumaNE + lumaSE));

                float directionReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * (0.25 * _ReduceMul), _ReduceMin);
                float reciprocalDirectionMin = 1.0 / (min(abs(direction.x), abs(direction.y)) + directionReduce);
                direction = min(float2(_SpanMax, _SpanMax), max(float2(-_SpanMax, -_SpanMax), direction * reciprocalDirectionMin)) * inverseScreen;

                float3 rgbA = 0.5 * (
                    tex2D(_MainTex, i.uv + direction * (1.0 / 3.0 - 0.5)).rgb +
                    tex2D(_MainTex, i.uv + direction * (2.0 / 3.0 - 0.5)).rgb);
                float3 rgbB = rgbA * 0.5 + 0.25 * (
                    tex2D(_MainTex, i.uv + direction * -0.5).rgb +
                    tex2D(_MainTex, i.uv + direction * 0.5).rgb);

                float lumaB = dot(rgbB, luma);
                if (lumaB < lumaMin || lumaB > lumaMax)
                {
                    return fixed4(rgbA, 1.0);
                }

                return fixed4(rgbB, 1.0);
            }
            ENDCG
        }
    }
}
