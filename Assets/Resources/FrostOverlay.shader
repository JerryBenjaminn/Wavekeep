// Task 44 — Frost status VFX (Slow vs. Freeze).
//
// A hand-written URP overlay shader (ShaderLab + HLSL, NOT Shader Graph, per the task brief) applied
// to a thin duplicate ("overlay") of an enemy's mesh, sitting just outside the base surface. It renders
// nothing at _FrostAmount = 0 and escalates through two visually distinct tiers as _FrostMode goes 0 → 1:
//
//   * Slow tier  (_FrostMode = 0): a subtle bluish-white frost mist — a soft fresnel rim plus a gently
//     scrolling value-noise haze. Low coverage / low alpha (Elden-Ring-style ambient cold aura).
//   * Freeze tier (_FrostMode = 1): a clearly stronger crystallization — a Voronoi (cellular) ice-crack
//     pattern covering most of the surface, a heavier blue/white tint, and a brief "snap" flash + scale
//     pulse driven by _FrostPulse on the moment freeze first applies.
//
// All per-enemy state (_FrostAmount, _FrostMode, _FrostPulse) is fed through a MaterialPropertyBlock by
// FrostVfxController, so a single SHARED material instance serves every enemy independently — there is no
// global parameter that would tint all enemies at once (a Task 44 reviewer-blocking concern).
Shader "Wavekeep/FrostOverlay"
{
    Properties
    {
        _FrostColor  ("Slow Frost Tint",  Color) = (0.62, 0.84, 1.0, 1.0)
        _FreezeColor ("Freeze Tint",      Color) = (0.80, 0.94, 1.0, 1.0)
        _FlashColor  ("Snap Flash Color", Color) = (0.95, 0.99, 1.0, 1.0)

        // Per-instance (set via MaterialPropertyBlock); defaults keep the editor preview inert.
        _FrostAmount ("Frost Amount",            Range(0,1)) = 0
        _FrostMode   ("Frost Mode (0=Slow,1=Freeze)", Range(0,1)) = 0
        _FrostPulse  ("Freeze Snap Pulse",       Range(0,1)) = 0

        _RimPower    ("Rim Power",        Range(0.5, 8)) = 2.5
        _MistScale   ("Mist Noise Scale", Float) = 5.0
        _MistSpeed   ("Mist Scroll Speed",Float) = 0.5
        _IceScale    ("Ice Crystal Scale",Float) = 11.0
        _CrackWidth  ("Ice Crack Width",  Range(0.01, 0.5)) = 0.12

        _SlowAlpha   ("Slow Max Alpha",   Range(0,1)) = 0.7
        _FreezeAlpha ("Freeze Max Alpha", Range(0,1)) = 0.95

        _Inflate      ("Base Inflation",  Float) = 0.025
        _PulseInflate ("Pulse Inflation", Float) = 0.14
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
        Cull Back

        Pass
        {
            Name "FrostOverlay"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FrostColor;
                float4 _FreezeColor;
                float4 _FlashColor;
                float  _FrostAmount;
                float  _FrostMode;
                float  _FrostPulse;
                float  _RimPower;
                float  _MistScale;
                float  _MistSpeed;
                float  _IceScale;
                float  _CrackWidth;
                float  _SlowAlpha;
                float  _FreezeAlpha;
                float  _Inflate;
                float  _PulseInflate;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            // --- Hash / noise helpers (stylized, cheap — prototype quality per the task brief) ---

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

            // Trilinear value noise in [0,1].
            float valueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash13(i + float3(0, 0, 0));
                float n100 = hash13(i + float3(1, 0, 0));
                float n010 = hash13(i + float3(0, 1, 0));
                float n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1));
                float n101 = hash13(i + float3(1, 0, 1));
                float n011 = hash13(i + float3(0, 1, 1));
                float n111 = hash13(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
            }

            // Returns x = distance to the nearest cell point (for crack edges),
            //         y = distance to the second nearest (edge sharpening).
            float2 voronoi(float3 p)
            {
                float3 g = floor(p);
                float3 f = frac(p);
                float d1 = 8.0;
                float d2 = 8.0;
                [unroll]
                for (int x = -1; x <= 1; x++)
                [unroll]
                for (int y = -1; y <= 1; y++)
                [unroll]
                for (int z = -1; z <= 1; z++)
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

                // Push the overlay shell outward along the surface normal so it sits just OUTSIDE the base
                // mesh (avoids z-fighting) and "snaps" larger on the freeze pulse — the visible scale punch.
                float inflate = _Inflate * _FrostAmount + _PulseInflate * _FrostPulse;
                float3 inflatedOS = IN.positionOS.xyz + IN.normalOS * inflate;

                OUT.positionWS = TransformObjectToWorld(inflatedOS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float amount = saturate(_FrostAmount);
                if (amount <= 0.001 && _FrostPulse <= 0.001)
                    return half4(0, 0, 0, 0);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float rim = pow(saturate(1.0 - saturate(dot(N, V))), _RimPower);

                // --- Slow tier: soft scrolling mist concentrated on the rim. ---
                float t = _Time.y;
                float mist = valueNoise(IN.positionWS * _MistScale + float3(0.0, t * _MistSpeed, t * _MistSpeed * 0.5));
                mist = mist * 0.6 + 0.4;
                // Keep a body-wide base so the mist reads even head-on, with the rim adding the cold "aura".
                float slowMask = saturate(0.35 + 0.65 * rim) * mist;
                float slowAlpha = slowMask * _SlowAlpha;

                // --- Freeze tier: Voronoi ice cracks + broad surface frost. ---
                float2 vor = voronoi(IN.positionWS * _IceScale);
                float edge = vor.y - vor.x;                       // small near cell borders → the cracks
                float cracks = 1.0 - smoothstep(0.0, _CrackWidth, edge);
                float surface = saturate(0.55 + 0.45 * rim);      // most of the body, brightening at the rim
                float freezeMask = saturate(surface + cracks);
                float freezeAlpha = freezeMask * _FreezeAlpha;

                float mode = saturate(_FrostMode);
                float3 tint = lerp(_FrostColor.rgb, _FreezeColor.rgb, mode);
                float baseAlpha = lerp(slowAlpha, freezeAlpha, mode) * amount;

                // Crystalline sparkle on the freeze tier only (cell centres glint).
                float sparkle = saturate(1.0 - vor.x * 2.0) * mode;
                float3 color = tint + sparkle * 0.35;

                // Snap flash: a brief additive white burst at the instant freeze applies.
                float flash = _FrostPulse * _FrostPulse; // ease the falloff
                color += _FlashColor.rgb * flash;
                float alpha = saturate(baseAlpha + flash * 0.8);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
