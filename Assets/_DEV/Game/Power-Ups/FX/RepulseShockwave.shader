// Repulse Pulse ground shockwave. One-shot: driven by _Progress 0 -> 1 from RepulseShockwaveFX.
//
// The waves start from the car's OUTLINE (a rounded rectangle = the body collider), not its centre,
// and grow rounder as they travel out to the push radius. 3 staggered waves, each with a bright
// leading edge and a soft trail, plus a flash hugging the car at the start.
//
// Cost: one flat quad, additive, no textures, no particles. Only drawn while the wave plays.
// SRP Batcher compatible (all properties in UnityPerMaterial).
Shader "CTF/RepulseShockwave"
{
    Properties
    {
        [HDR] _Color     ("Wave Color", Color) = (0.55, 0.35, 2.4, 1)
        [HDR] _CoreColor ("Leading Edge Color", Color) = (2.2, 2.0, 3.5, 1)
        _Intensity  ("Intensity", Range(0, 4)) = 1

        [Header(Waves)]
        _WaveDelay  ("Delay Between Waves", Range(0, 0.3)) = 0.14
        _WaveWidth  ("Leading Edge Width (m)", Range(0.02, 1)) = 0.18
        _TrailLength("Trail Length (m)", Range(0.05, 3)) = 0.9
        _Distort    ("Wobble (m)", Range(0, 0.5)) = 0.06
        _Flash      ("Start Flash", Range(0, 4)) = 1.5

        [Header(Driven by script)]
        _Progress   ("Progress", Range(0, 1)) = 0.3
        _Extent     ("Quad Half Size (m)", Float) = 4
        _BoxHalf    ("Car Half Size XZ (m)", Vector) = (0.5, 0.6, 0, 0)
        _Corner     ("Car Corner Radius (m)", Float) = 0.25
        _MaxDist    ("Wave Travel (m)", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "RepulseShockwave"
            Tags { "LightMode"="UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                half4  _CoreColor;
                half   _Intensity;
                half   _WaveDelay;
                half   _WaveWidth;
                half   _TrailLength;
                half   _Distort;
                half   _Flash;
                half   _Progress;
                float  _Extent;
                float4 _BoxHalf;
                float  _Corner;
                float  _MaxDist;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 p : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.p = (v.uv * 2.0 - 1.0) * _Extent;   // metres; x = car right, y = car forward
                return o;
            }

            // Signed distance to a rounded rectangle (negative inside).
            float SdRoundBox (float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - (halfSize - r);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float d = SdRoundBox(i.p, _BoxHalf.xy, _Corner);   // metres from the car's outline
                if (d < 0.0 || d > _MaxDist + _WaveWidth * 3.0) discard;

                float prog = _Progress;

                // Energy wobble: grows with distance, so the wave leaves the car clean and gets wild.
                float ang = atan2(i.p.y, i.p.x);
                float wob = (sin(ang * 5.0 + prog * 9.0) + 0.4 * sin(ang * 11.0 - prog * 17.0)) * _Distort;
                float dd  = d + wob * saturate(d / max(_MaxDist, 0.01) * 2.0);

                half lead = 0.0h;
                half body = 0.0h;
                float span = max(1.0 - 2.0 * _WaveDelay, 0.05);

                [unroll]
                for (int k = 0; k < 3; k++)
                {
                    float t = saturate((prog - k * _WaveDelay) / span);
                    if (t <= 0.0) continue;

                    float front = t * _MaxDist;
                    float x     = dd - front;                        // > 0 ahead of the wave, < 0 behind
                    float w     = _WaveWidth * (0.6 + 0.8 * t);       // edge widens as it travels
                    half  fade  = (half)pow(1.0 - t, 1.5) * (1.0h - 0.3h * k);

                    half edge  = (half)exp(-(x * x) / (w * w));
                    half trail = x < 0.0 ? (half)exp(x / _TrailLength) * 0.35h : 0.0h;

                    lead += edge * fade;
                    body += (edge + trail) * fade;
                }

                // Flash hugging the car's outline right when it fires.
                half flash = (half)exp(-d * 3.0) * (half)pow(1.0 - prog, 4.0) * _Flash;

                half3 col = _Color.rgb * body + _CoreColor.rgb * (lead * 0.6h + flash);
                return half4(col * _Intensity, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
