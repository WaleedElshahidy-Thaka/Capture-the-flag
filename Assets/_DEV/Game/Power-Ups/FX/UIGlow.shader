// UI glow: like UI/Default, but the Image colour is pushed into HDR so the camera's Bloom makes it glow,
// plus a soft halo around the sprite and an optional slow pulse.
//
// The tint still comes from Graphic.color (e.g. LobbySeatPreview sets each ring to its seat colour), so one
// shared material serves every seat. Needs HDR + Bloom on the camera (threshold below Glow Intensity).
// Supports UI masking/clipping like UI/Default.
Shader "CTF/UIGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Glow)]
        _GlowIntensity ("Glow Intensity (HDR)", Range(0.1, 10)) = 0.8
        _Saturation    ("Colour Saturation", Range(1, 3)) = 2.4
        _HaloSize      ("Halo Size (UV)", Range(0, 0.1)) = 0.035
        _HaloStrength  ("Halo Strength (0 = Bloom only)", Range(0, 2)) = 0

        [Header(Pulse)]
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.25
        _PulseSpeed  ("Pulse Speed", Range(0, 10)) = 2

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha   // premultiplied: the halo adds light without darkening what's behind
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            half _GlowIntensity;
            half _Saturation;
            half _HaloSize;
            half _HaloStrength;
            half _PulseAmount;
            half _PulseSpeed;

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);

                o.color = v.color * _Color;
                return o;
            }

            // Alpha with the near-zero noise of compressed textures removed, so glow can't light the quad.
            half CleanAlpha (float2 uv)
            {
                return saturate((tex2D(_MainTex, uv).a - 0.03h) * 1.031h);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.texcoord) + _TextureSampleAdd;
                half baseA = saturate((tex.a - 0.03h) * 1.031h);

                // Soft halo: average the sprite's alpha at 8 points around this pixel.
                half halo = 0;
                const half d = 0.7071h;
                halo += CleanAlpha(i.texcoord + half2( 1, 0) * _HaloSize);
                halo += CleanAlpha(i.texcoord + half2(-1, 0) * _HaloSize);
                halo += CleanAlpha(i.texcoord + half2( 0, 1) * _HaloSize);
                halo += CleanAlpha(i.texcoord + half2( 0,-1) * _HaloSize);
                halo += CleanAlpha(i.texcoord + half2( d, d) * _HaloSize);
                halo += CleanAlpha(i.texcoord + half2(-d, d) * _HaloSize);
                halo += CleanAlpha(i.texcoord + half2( d,-d) * _HaloSize);
                halo += CleanAlpha(i.texcoord + half2(-d,-d) * _HaloSize);
                halo = halo * 0.125h * _HaloStrength;

                half pulse = 1.0h + _PulseAmount * sin(_Time.y * _PulseSpeed);
                half glow  = _GlowIntensity * pulse;

                // Push saturation up first, so bright HDR values keep the seat's hue instead of going white.
                half  luma = dot(i.color.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half3 tint = max(0, lerp(luma.xxx, i.color.rgb, _Saturation));

                half a = i.color.a;
                half4 col;
                col.rgb = tint * tex.rgb * glow * baseA * a      // the ring itself, HDR
                        + tint * glow * halo * 0.5h * a;         // additive halo
                col.a   = baseA * a;

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.worldPosition.xy * 2 - _ClipRect.xy - _ClipRect.zw)) /
                                   (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + fwidth(i.worldPosition.xy)));
                col *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
