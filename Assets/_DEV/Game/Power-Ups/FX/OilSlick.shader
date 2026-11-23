// Oil Slick puddle: glossy black liquid with a rainbow thin-film sheen, irregular edges and droplets.
//
// Drawn on a flat XZ quad (OilQuadXZ) whose puddle radius is 0.5 object units, so OilPatch's existing
// x/z scale (= 2 x Radius) makes the puddle exactly the networked slippery circle. The lobes stay
// inside that circle; only the small droplets sit just outside it (cosmetic).
//
// Every puddle looks different: the shape is seeded from where it lands. No textures. One draw call.
Shader "CTF/OilSlick"
{
    Properties
    {
        _DeepColor        ("Deep Colour", Color) = (0.012, 0.01, 0.02, 1)
        _Alpha            ("Opacity", Range(0, 1)) = 0.93

        [Header(Rainbow film)]
        _FilmStrength     ("Film Strength", Range(0, 2)) = 0.9
        _FilmBrightness   ("Film Brightness", Range(0, 3)) = 0.9
        _FilmScale        ("Film Swirl Scale", Range(0.2, 6)) = 1.6
        _FilmBands        ("Film Colour Bands", Range(0.2, 4)) = 1.6
        _FilmSpeed        ("Film Flow Speed", Range(0, 0.5)) = 0.04

        [Header(Gloss)]
        _Smoothness       ("Smoothness", Range(0, 1)) = 0.93
        _SpecularStrength ("Highlight Strength", Range(0, 6)) = 2.5
        _ReflectionStrength ("Reflection Strength", Range(0, 3)) = 1.1
        _FresnelPower     ("Fresnel Power", Range(1, 8)) = 4
        _SkyReflection    ("Minimum Sky Reflection", Color) = (0.32, 0.36, 0.45, 1)

        [Header(Shape)]
        _PuddleRadius     ("Puddle Radius (of quad)", Range(0.5, 1)) = 0.87
        _EdgeWobble       ("Edge Wobble", Range(0, 0.3)) = 0.13
        _EdgeFrequency    ("Edge Lobes", Range(0.5, 4)) = 1.7
        _BevelWidth       ("Rim Bevel Width", Range(0.005, 0.2)) = 0.05
        _BevelTilt        ("Rim Bevel Tilt", Range(0, 3)) = 1.2
        _Ripple           ("Surface Ripple", Range(0, 0.5)) = 0.08
        _Droplets         ("Droplets", Range(0, 1)) = 0.35
        _DepthBias        ("Depth Bias (m, toward camera)", Range(0, 0.3)) = 0.08
    }

    SubShader
    {
        // Above the skid marks (Transparent-50), below the glowing power-up FX.
        Tags { "RenderType"="Transparent" "Queue"="Transparent-40" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "OilSlick"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half  _Alpha;
                half  _FilmStrength;
                half  _FilmBrightness;
                half  _FilmScale;
                half  _FilmBands;
                half  _FilmSpeed;
                half  _Smoothness;
                half  _SpecularStrength;
                half  _ReflectionStrength;
                half  _FresnelPower;
                half4 _SkyReflection;
                half  _PuddleRadius;
                half  _EdgeWobble;
                half  _EdgeFrequency;
                half  _BevelWidth;
                half  _BevelTilt;
                half  _Ripple;
                half  _Droplets;
                half  _DepthBias;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 p          : TEXCOORD0;  // -1..1 across the quad
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;  // puddle plane frame (unit vectors, scale-free)
                half3  tangentWS  : TEXCOORD3;
                half3  bitangentWS: TEXCOORD4;
                // Not interpolated: tiny interpolation noise would scramble the droplet hashes into stripes.
                nointerpolation float seed : TEXCOORD5;
                half   fog        : TEXCOORD6;
            };

            float Hash21 (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + 1.0), u.x), u.y);
            }

            float Fbm (float2 p)
            {
                float v = 0.0, a = 0.5;
                [unroll] for (int k = 0; k < 3; k++) { v += a * ValueNoise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v / 0.875;
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionWS = pos.positionWS;
                // Decal trick: nudge the flat puddle toward the camera so a bumpy floor can't poke
                // through it (z-fighting stripes). Looks identical, only depth changes.
                float3 toCam = normalize(_WorldSpaceCameraPos - pos.positionWS);
                o.positionCS = TransformWorldToHClip(pos.positionWS + toCam * _DepthBias);
                o.p = v.uv * 2.0 - 1.0;

                // Plane frame from the object axes, normalised so OilPatch's x/z scale doesn't bend normals.
                o.normalWS    = normalize(TransformObjectToWorldDir(float3(0, 1, 0), false));
                o.tangentWS   = normalize(TransformObjectToWorldDir(float3(1, 0, 0), false));
                o.bitangentWS = normalize(TransformObjectToWorldDir(float3(0, 0, 1), false));

                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = frac(dot(origin.xz, float2(0.1031, 0.11369)) * 43.758) * 100.0;
                o.fog = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 p = i.p;
                float  r = length(p);
                float2 dir = r > 1e-4 ? p / r : float2(0, 0);
                float  t = _Time.y;

                // ---- Shape: wobbly main puddle (lobes stay inside the gameplay circle) ----
                float R = _PuddleRadius;
                float edgeNoise = Fbm(dir * _EdgeFrequency + i.seed);
                float rEdge = R * (1.0 - _EdgeWobble * 2.0 * (1.0 - edgeNoise));
                float dMain = (rEdge - r) / R;                   // > 0 inside, in puddle-radius units

                // ---- Droplets around the edge ----
                const float cell = 0.18;
                float2 cid = floor(p / cell);
                float  h0  = Hash21(cid + i.seed);
                float2 c   = (cid + 0.25 + 0.5 * float2(h0, Hash21(cid + i.seed + 3.7))) * cell;
                float  cr  = length(c);
                float  rad = lerp(0.015, 0.04, Hash21(cid + i.seed + 9.1));
                bool   dropCell = h0 < _Droplets && cr > R * 0.92 && cr < 1.0 - rad;
                float  dropDist = length(p - c);
                float  dDrop = dropCell ? (rad - dropDist) / R : -1.0;

                // Anti-alias with the derivative of r (continuous everywhere), not of d, which jumps at
                // droplet-cell borders and would leave stripes.
                float aa = max(fwidth(r) / R, 1e-4);
                float covMain = saturate(dMain / aa + 0.5);
                float covDrop = saturate(dDrop / aa + 0.5);
                float coverage = max(covMain, covDrop);
                if (coverage <= 0.0) discard;

                // Bevelled rim: the surface slopes outward near the edge (main puddle or droplet).
                float2 tilt;
                float  height;
                if (covDrop > covMain)
                {
                    float dn = dDrop * R / rad;                  // 0 at the droplet edge, 1 at its centre
                    tilt   = (p - c) / max(dropDist, 1e-4) * _BevelTilt * (1.0 - saturate(dn / 0.6));
                    height = saturate(dn / 0.6);
                }
                else
                {
                    tilt   = dir * _BevelTilt * (1.0 - saturate(dMain / _BevelWidth));
                    height = saturate(dMain / _BevelWidth);
                }

                // ---- Surface normal: bevelled rim + gentle ripples ----
                float2 rp = p * 6.0 + i.seed;
                tilt += (float2(ValueNoise(rp + t * 0.3), ValueNoise(rp + 7.3 - t * 0.3)) - 0.5) * _Ripple;
                half3 n = normalize(i.normalWS + tilt.x * i.tangentWS + tilt.y * i.bitangentWS);

                half3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                half  NdotV = saturate(dot(n, V));
                half  fres = lerp(0.04h, 1.0h, pow(1.0h - NdotV, _FresnelPower));

                // ---- Thin-film rainbow: domain-warped swirls, shifting with view angle ----
                float2 fp = p * _FilmScale + i.seed;
                float2 warp = float2(Fbm(fp * 0.8 + t * _FilmSpeed), Fbm(fp * 0.8 + 5.2 - t * _FilmSpeed));
                float  film = Fbm(fp + warp * 1.6 + t * _FilmSpeed);
                float  phase = film * _FilmBands + (1.0 - NdotV) * 0.6;
                half3  rainbow = 0.5h + 0.5h * cos(6.2831853 * (phase + float3(0.0, 0.33, 0.67)));
                // Rainbow only in swirling streaks; between them the oil stays dark.
                half   filmMask = (0.06h + smoothstep(0.45, 0.72, Fbm(fp * 0.6 + 11.0 + warp))) * _FilmStrength;
                rainbow = rainbow * rainbow;                          // richer, less pastel colours

                // ---- Lighting ----
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half  shadow = light.shadowAttenuation * light.distanceAttenuation;
                half  NdotL = saturate(dot(n, light.direction));
                half3 ambient = SampleSH(n);

                half3 H = normalize(light.direction + V);
                half  specPow = exp2(10.0h * _Smoothness + 1.0h);
                half  spec = pow(saturate(dot(n, H)), specPow) * _SpecularStrength * shadow;

                half3 R3 = reflect(-V, n);
                // Environment reflection, with a minimum "sky" so the puddle still reads as glossy in
                // arenas whose reflection probe is dark.
                half3 env = GlossyEnvironmentReflection(R3, 1.0h - _Smoothness, 1.0h);
                env = max(env, _SkyReflection.rgb * (0.4h + 0.6h * saturate(R3.y)));
                half3 tint = lerp(1.0h.xxx, rainbow * 1.6h, saturate(filmMask));

                half3 col = _DeepColor.rgb * (light.color * NdotL * shadow + ambient);
                col += rainbow * filmMask * _FilmBrightness * (0.5h * (ambient + 0.3h) + 0.5h * NdotL * shadow * light.color);
                col += env * tint * (0.15h + 0.85h * fres) * _ReflectionStrength;
                col += light.color * spec * lerp(1.0h.xxx, rainbow, 0.35h);
                col *= lerp(0.85h, 1.0h, height);                  // thinner, lighter liquid at the rim

                col = MixFog(col, i.fog);
                half alpha = coverage * lerp(_Alpha, 1.0h, fres);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
