// Grapple Field ground ring: glowing waves travelling outward from the robot, swirling arms,
// and a bright rim that pulses bigger/smaller (its biggest size = the steal reach). Turns hot (Alert colour) when the Crown holder is inside.
//
// Cost: one flat quad, additive, no textures, no particles, no CPU simulation. Pixels outside the
// circle are discarded. SRP Batcher compatible (all properties in UnityPerMaterial).
// Driven by GrappleFieldRingFX (_Reveal, _Alert, _Intensity).
Shader "CTF/GrappleRing"
{
    Properties
    {
        [HDR] _Color      ("Wave Color", Color) = (0.35, 0.6, 2.2, 1)
        [HDR] _EdgeColor  ("Edge Color", Color) = (0.8, 1.0, 3.2, 1)
        [HDR] _AlertColor ("Holder In Range Color", Color) = (3.0, 1.5, 0.25, 1)
        _Intensity     ("Intensity", Range(0, 4)) = 1

        [Header(Waves)]
        _WaveCount     ("Wave Count", Range(1, 12)) = 4
        _WaveSpeed     ("Wave Speed", Range(0, 5)) = 1.1
        _WaveSharpness ("Wave Sharpness", Range(1, 16)) = 5
        [Toggle] _WaveInward ("Waves Travel Inward", Float) = 0

        [Header(Edge Pulse)]
        _EdgePulseSpeed  ("Edge Pulse Speed", Range(0, 10)) = 3
        _EdgePulseAmount ("Edge Pulse Amount (how far it shrinks)", Range(0, 0.4)) = 0.12

        [Header(Swirl)]
        _SwirlCount    ("Swirl Arms", Range(0, 8)) = 3
        _SwirlSpeed    ("Swirl Speed", Range(-6, 6)) = 2
        _SwirlTwist    ("Swirl Twist", Range(0, 12)) = 5
        _SwirlAmount   ("Swirl Amount", Range(0, 1)) = 0.5

        [Header(Shape)]
        _EdgeWidth     ("Edge Width", Range(0.005, 0.25)) = 0.06
        _InnerHole     ("Inner Hole (under robot)", Range(0, 0.7)) = 0.25
        _Fill          ("Soft Fill", Range(0, 1)) = 0.12

        [Header(Driven by script)]
        _Reveal        ("Reveal", Range(0, 1)) = 1
        _Alert         ("Holder In Range", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "GrappleRing"
            Tags { "LightMode"="UniversalForward" }

            Blend One One     // additive glow
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1     // stay on top of the floor without z-fighting

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EdgeColor;
                half4 _AlertColor;
                half  _Intensity;
                half  _WaveCount;
                half  _WaveSpeed;
                half  _WaveSharpness;
                half  _WaveInward;
                half  _EdgePulseSpeed;
                half  _EdgePulseAmount;
                half  _SwirlCount;
                half  _SwirlSpeed;
                half  _SwirlTwist;
                half  _SwirlAmount;
                half  _EdgeWidth;
                half  _InnerHole;
                half  _Fill;
                half  _Reveal;
                half  _Alert;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 p          : TEXCOORD0; // -1..1 across the quad
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.p = v.uv * 2.0 - 1.0;
                return o;
            }

            // Triangle wave 0..1 peaking at frac == 0.5, sharpened into a thin glowing band.
            half Band (float x, half sharp)
            {
                half tri = 1.0h - abs(frac(x) * 2.0h - 1.0h);
                return pow(tri, sharp);
            }

            half4 frag (Varyings i) : SV_Target
            {
                float r = length(i.p);
                float outer = max(_Reveal, 1e-3);
                if (r > outer) discard;                 // nothing outside the current circle

                float rn = r / outer;                   // 0 centre .. 1 rim
                float t  = _Time.y;

                // Rim pulses: it grows to the full reach (1) and shrinks by _EdgePulseAmount.
                half rim = 1.0h - _EdgePulseAmount * (0.5h + 0.5h * sin(t * _EdgePulseSpeed));

                // Waves travel in one direction only (outward by default).
                half dir   = _WaveInward > 0.5h ? 1.0h : -1.0h;
                half waves = Band(rn * _WaveCount + dir * t * _WaveSpeed, _WaveSharpness);

                // Swirling arms that twist with distance (the spiral streaks in the reference).
                float ang   = atan2(i.p.y, i.p.x);
                half  swirl = 0.5h + 0.5h * sin(ang * _SwirlCount + rn * _SwirlTwist - t * _SwirlSpeed);
                swirl = swirl * swirl;
                waves *= lerp(1.0h, 0.35h + swirl * 1.3h, _SwirlAmount);

                // Keep the robot's footprint clear and make the field brighter toward the rim.
                half inner   = smoothstep(_InnerHole * 0.6h, _InnerHole + 0.15h, rn);
                half falloff = 0.3h + 0.7h * rn;
                half inside  = saturate((rim - rn) / max(fwidth(rn), 1e-4)); // waves stop at the rim
                half body    = (waves * falloff + _Fill * rn * rn) * inner * inside;

                // Bright rim (max size = the steal reach), riding the pulse. Anti-aliased outer edge.
                half edge = saturate(1.0h - abs(rn - (rim - _EdgeWidth)) / _EdgeWidth);
                edge = edge * edge;
                half aa = saturate((1.0h - rn) / max(fwidth(rn), 1e-4));

                // Holder inside: shift to the alert colour and pulse faster.
                half3 waveCol = lerp(_Color.rgb, _AlertColor.rgb, _Alert);
                half3 edgeCol = lerp(_EdgeColor.rgb, _AlertColor.rgb * 1.4h, _Alert);
                half  pulse   = 1.0h + _Alert * 0.45h * sin(t * 18.0h);

                half3 col = (waveCol * body + edgeCol * edge) * aa * pulse * _Intensity * saturate(_Reveal * 1.5h);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
