// Tyre tracks drawn by SkidmarkSystem (one shared world-space mesh for every car = one draw call).
//
// Per vertex (written by SkidmarkSystem):
//   color.a   - track strength (0 at the start of a mark, for a soft fade-in)
//   uv.x      - 0..1 across the tyre (soft side edges)
//   uv.y      - metres along the track (tread pattern)
//   uv2.x     - time the segment was laid (Time.timeSinceLevelLoad), so it fades out on the GPU
Shader "CTF/Skidmark"
{
    Properties
    {
        _Color         ("Colour (alpha = darkness)", Color) = (0.04, 0.035, 0.03, 0.6)
        _Lifetime      ("Lifetime (s)", Float) = 8
        _FadeSeconds   ("Fade Out (s)", Float) = 2
        _EdgeSoftness  ("Edge Softness", Range(0.01, 0.5)) = 0.25
        _TreadScale    ("Tread Lines per Metre", Range(0, 40)) = 10
        _TreadStrength ("Tread Strength", Range(0, 1)) = 0.12
    }

    SubShader
    {
        // Before other transparent FX (Grapple ring, shockwave), so those glow on top of the tracks.
        Tags { "RenderType"="Transparent" "Queue"="Transparent-50" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Skidmark"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -2, -2

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half  _Lifetime;
                half  _FadeSeconds;
                half  _EdgeSoftness;
                half  _TreadScale;
                half  _TreadStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   alpha      : TEXCOORD1;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;

                // Age-based fade, per vertex: no CPU work to age the tracks.
                float age  = _Time.y - v.uv2.x;
                half  fade = 1.0h - saturate((age - (_Lifetime - _FadeSeconds)) / max(_FadeSeconds, 0.01));
                o.alpha = v.color.a * fade;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half edge  = smoothstep(0.0h, _EdgeSoftness, i.uv.x) * smoothstep(0.0h, _EdgeSoftness, 1.0h - i.uv.x);
                half tread = 1.0h - _TreadStrength * step(0.5h, frac(i.uv.y * _TreadScale));
                half a = _Color.a * i.alpha * edge * tread;
                if (a <= 0.002h) discard;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
