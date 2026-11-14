// 타격 이펙트용 가산(빛을 더하는) 셰이더 — 불꽃 · 베기 자국 · 충격파 · 섬광.
//   - 겹칠수록 밝아지고, _Gain 으로 1보다 밝게 만들어 블룸이 번지게 한다
//   - 안개 속에서는 검정 쪽으로 사라진다 (가산 혼합이라 안개 색을 더하면 뿌옇게 뜬다)
// ImpactFX / ClashSpark 가 Resources.Load 로 불러 쓴다. (없으면 기본 스프라이트 셰이더로 대신)
Shader "ClockworkRoll/StageFx"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Gain ("Gain (1보다 크면 블룸이 번진다)", Range(0, 8)) = 1.6
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+5"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha One
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _Gain;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv) * i.color;
                c.rgb *= _Gain;
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0, 0, 0, 0));
                return c;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
