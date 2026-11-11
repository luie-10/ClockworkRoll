// 무대 위 2.5D 캐릭터용 스프라이트 셰이더.
//   - 무대 조명(가로등 · 불 · 네온)의 색과 거리 감쇠를 받는다 → 캐릭터가 배경에 '붙여 넣은' 느낌이 사라진다
//   - 종이 인형이라 빛의 방향은 약하게만 반영한다 (뒤/위에서 오는 빛도 물든다)
//   - _SelfLight 만큼은 빛이 없어도 항상 보인다 (어두운 무대에서도 캐릭터를 읽을 수 있게)
//   - ShadowCaster 패스: 그림 모양 그대로 바닥/벽에 진짜 그림자를 드리운다
// SpriteCharacter가 Resources.Load로 불러 쓴다. (없거나 지원되지 않으면 기본 스프라이트 셰이더 그대로)
Shader "ClockworkRoll/StageSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SelfLight ("Self Light (빛이 없어도 보이는 밝기)", Range(0, 1)) = 0.6
        _LightGain ("Light Gain (무대 조명을 받는 정도)", Range(0, 2)) = 0.55
        _Wrap ("Light Wrap (0 = 방향 그대로, 1 = 방향 무시)", Range(0, 1)) = 0.75
        _ShadowCutoff ("Shadow Alpha Cutoff", Range(0, 1)) = 0.4
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Card vertex:vert nofog nolightmap nodynlightmap keepalpha noinstancing
        #pragma multi_compile_local _ PIXELSNAP_ON
        #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
        #include "UnitySprites.cginc"

        fixed _SelfLight;
        fixed _LightGain;
        fixed _Wrap;

        struct Input
        {
            float2 uv_MainTex;
            fixed4 color;
        };

        // 종이 인형 조명: 방향은 _Wrap만큼 무시하고, 색 · 거리 감쇠 · 그림자만 받는다
        half4 LightingCard (SurfaceOutput s, half3 lightDir, half atten)
        {
            half ndl = saturate(dot(s.Normal, lightDir));
            half wrapped = lerp(ndl, 1.0, _Wrap);
            half3 light = min(_LightColor0.rgb * (wrapped * atten), 1.5);   // 가로등 바로 밑에서 하얗게 타지 않게
            half4 c;
            c.rgb = s.Albedo * light;
            c.a = s.Alpha;
            return c;
        }

        void vert (inout appdata_full v, out Input o)
        {
            v.vertex = UnityFlipSprite(v.vertex, _Flip);

            #if defined(PIXELSNAP_ON)
            v.vertex = UnityPixelSnap(v.vertex);
            #endif

            v.normal = float3(0, 0, -1);   // 스프라이트는 항상 카메라 쪽을 본다

            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.color = v.color * _Color * _RendererColor;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = SampleSpriteTexture(IN.uv_MainTex) * IN.color;
            fixed3 rgb = c.rgb * c.a;   // 미리 곱한 알파
            o.Albedo = rgb * _LightGain;
            o.Emission = rgb * _SelfLight;
            o.Alpha = c.a;
        }
        ENDCG

        // 그림 모양대로 그림자를 드리운다 (알파가 _ShadowCutoff보다 낮은 곳은 뚫림)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Flip;
            fixed4 _RendererColor;
            fixed _ShadowCutoff;

            struct v2fShadow
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            v2fShadow vertShadow (appdata_base v)
            {
                v2fShadow o;
                v.vertex.xy *= _Flip.xy;
                v.normal = float3(0, 0, -1);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = v.texcoord.xy;
                return o;
            }

            float4 fragShadow (v2fShadow i) : SV_Target
            {
                fixed a = tex2D(_MainTex, i.uv).a * _RendererColor.a;
                clip(a - _ShadowCutoff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
