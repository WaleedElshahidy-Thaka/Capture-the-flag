// Soft glowing rim, layered on top of an object as an extra material (e.g. the power-up vase), so the
// object's own material is untouched. Additive fresnel: bright at the silhouette, faint in the middle,
// slowly breathing. HDR colour, so the camera's Bloom turns it into a gentle glow.
//
// Cost: one extra draw of the mesh, no textures.
Shader "CTF/RimGlow"
{
    Properties
    {
        [HDR] _Color      ("Glow Colour", Color) = (2.2, 1.4, 0.35, 1)
        _RimPower         ("Rim Sharpness", Range(0.5, 8)) = 2.5
        _BaseGlow         ("Face Glow", Range(0, 1)) = 0.06
        _Intensity        ("Intensity", Range(0, 4)) = 1
        _PulseAmount      ("Pulse Amount", Range(0, 1)) = 0.3
        _PulseSpeed       ("Pulse Speed", Range(0, 10)) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "RimGlow"
            Tags { "LightMode"="UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half  _RimPower;
                half  _BaseGlow;
                half  _Intensity;
                half  _PulseAmount;
                half  _PulseSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0h - saturate(dot(n, v)), _RimPower);
                half pulse = 1.0h + _PulseAmount * sin(_Time.y * _PulseSpeed);
                return half4(_Color.rgb * (rim + _BaseGlow) * _Intensity * pulse, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
