// Task 45 — Frost Warden ability VFX (shared frost-crystal look).
//
// A hand-written URP shader (ShaderLab + HLSL, not Shader Graph — consistent with Task 44) reused for all
// three of Frost Warden's ability effects by varying its material defaults:
//   * Frost Bolt Burst impact  — an expanding crystalline shell (sphere mesh scaled to the AoE radius).
//   * Frozen Ground patch       — a flat ice decal on the ground.
//   * Frost Zone band / pulse   — a low-intensity ambient mist over the full-width band, flashing on pulses.
//
// It reuses Task 44's blue/white Voronoi (cellular) ice-crack language so the whole kit reads cohesively.
// Per-instance, time-varying values (_Alpha master fade, _Emission flash) come from a MaterialPropertyBlock
// set by FrostVfxPresenter, so many simultaneous effects animate independently off ONE shared material.
Shader "Wavekeep/FrostFx"
{
    Properties
    {
        _Color        ("Tint",            Color) = (0.72, 0.9, 1.0, 1.0)
        _Alpha        ("Master Alpha",    Range(0,1)) = 1
        _Emission     ("Emission Boost",  Float) = 0
        _FillBase     ("Base Coverage",   Range(0,1)) = 0.25
        _VoronoiScale ("Ice Crystal Scale", Float) = 10
        _CrackWidth   ("Ice Crack Width", Range(0.01, 0.6)) = 0.18
        _CrackStrength("Ice Crack Strength", Range(0,2)) = 1
        _FresnelPower ("Rim Power",       Range(0.5, 8)) = 2.5
        _FresnelBoost ("Rim Boost",       Range(0, 2)) = 0.6
        _MistScale    ("Mist Scale",      Float) = 4
        _MistSpeed    ("Mist Speed",      Float) = 0.4
        _MistAmount   ("Mist Amount",     Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "FrostFx"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Alpha;
                float  _Emission;
                float  _FillBase;
                float  _VoronoiScale;
                float  _CrackWidth;
                float  _CrackStrength;
                float  _FresnelPower;
                float  _FresnelBoost;
                float  _MistScale;
                float  _MistSpeed;
                float  _MistAmount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float3 hash33(float3 p)
            {
                p = float3(dot(p, float3(127.1, 311.7, 74.7)),
                           dot(p, float3(269.5, 183.3, 246.1)),
                           dot(p, float3(113.5, 271.9, 124.6)));
                return frac(sin(p) * 43758.5453);
            }

            float valueNoise(float3 p)
            {
                float3 i = floor(p); float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash13(i + float3(0,0,0));
                float n100 = hash13(i + float3(1,0,0));
                float n010 = hash13(i + float3(0,1,0));
                float n110 = hash13(i + float3(1,1,0));
                float n001 = hash13(i + float3(0,0,1));
                float n101 = hash13(i + float3(1,0,1));
                float n011 = hash13(i + float3(0,1,1));
                float n111 = hash13(i + float3(1,1,1));
                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }

            // x = nearest cell distance, y = second-nearest (their gap forms the crack edges).
            float2 voronoi(float3 p)
            {
                float3 g = floor(p); float3 f = frac(p);
                float d1 = 8.0; float d2 = 8.0;
                [unroll] for (int x = -1; x <= 1; x++)
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int z = -1; z <= 1; z++)
                {
                    float3 o = float3(x, y, z);
                    float3 r = o + hash33(g + o) - f;
                    float d = dot(r, r);
                    if (d < d1) { d2 = d1; d1 = d; }
                    else if (d < d2) { d2 = d; }
                }
                return float2(sqrt(d1), sqrt(d2));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                if (_Alpha <= 0.001 && _Emission <= 0.001) return half4(0,0,0,0);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float rim = pow(saturate(1.0 - saturate(dot(N, V))), _FresnelPower);

                float2 vor = voronoi(IN.positionWS * _VoronoiScale);
                float edge = vor.y - vor.x;
                float cracks = (1.0 - smoothstep(0.0, _CrackWidth, edge)) * _CrackStrength;

                float coverage = saturate(_FillBase + cracks + rim * _FresnelBoost);

                // Optional scrolling haze (zone ambient); _MistAmount = 0 leaves the look crisp.
                if (_MistAmount > 0.001)
                {
                    float t = _Time.y;
                    float mist = valueNoise(IN.positionWS * _MistScale + float3(0, t * _MistSpeed, t * _MistSpeed * 0.5));
                    coverage *= lerp(1.0, mist * 0.7 + 0.3, _MistAmount);
                }

                float sparkle = saturate(1.0 - vor.x * 2.0); // cell-centre glints
                float3 color = _Color.rgb + sparkle * 0.25 + _Emission.xxx;
                float alpha = saturate(coverage * _Alpha + _Emission * 0.4);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
