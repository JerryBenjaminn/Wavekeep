// Task 51 — Burn status VFX (per-enemy ember/fire surface).
//
// The fire counterpart to Task 44's FrostOverlay (hand-written URP ShaderLab + HLSL, NOT Shader Graph). It is
// applied to a thin inflated duplicate ("overlay") of a Burning enemy's mesh and renders an animated, upward-
// licking ember/flame skin instead of frost's static blue Voronoi ice-cracks — so the two status effects can
// never be confused at a glance (a Task 51 reviewer-blocking requirement):
//
//   * Palette is warm (deep red → orange → yellow-white), the opposite end of the spectrum from frost's
//     blue/white and visually apart from Bolt Striker's flat gold.
//   * The pattern is MOVING: fractal value-noise flames scroll UPWARD and flicker over time, with bright ember
//     speckles, where frost is a crisp, still crystal shell.
//
// Intensity is driven by a single per-instance _FireAmount in [0,1] (set via MaterialPropertyBlock by
// FireVfxController), which scales coverage, brightness AND the upward flame speed — so more Burn stacks read as
// a visibly more intense fire, not merely a longer-lasting one. All per-enemy state rides the MPB, so one SHARED
// material serves every enemy independently (no global parameter that would ignite all enemies at once).
Shader "Wavekeep/FireOverlay"
{
    Properties
    {
        _EmberColor ("Ember (low) Color",  Color) = (0.65, 0.12, 0.02, 1.0)
        _FlameColor ("Flame (mid) Color",  Color) = (1.0, 0.42, 0.06, 1.0)
        _HotColor   ("Hot (peak) Color",   Color) = (1.0, 0.86, 0.35, 1.0)

        // Per-instance (set via MaterialPropertyBlock); default keeps the editor preview inert.
        _FireAmount ("Fire Amount (0..1)", Range(0,1)) = 0

        _RimPower   ("Rim Power",          Range(0.5, 8)) = 2.2
        _FlameScale ("Flame Noise Scale",  Float) = 4.5
        _FlameSpeed ("Flame Rise Speed",   Float) = 1.6
        _EmberScale ("Ember Speckle Scale",Float) = 16.0
        _EmberSpeed ("Ember Flicker Speed",Float) = 5.0
        _MaxAlpha   ("Max Alpha",          Range(0,1)) = 0.85
        _Inflate    ("Base Inflation",     Float) = 0.02
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

        Blend SrcAlpha One   // additive-ish (glow): fire ADDS light to the surface beneath it
        ZWrite Off
        ZTest LEqual
        Cull Back

        Pass
        {
            Name "FireOverlay"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EmberColor;
                float4 _FlameColor;
                float4 _HotColor;
                float  _FireAmount;
                float  _RimPower;
                float  _FlameScale;
                float  _FlameSpeed;
                float  _EmberScale;
                float  _EmberSpeed;
                float  _MaxAlpha;
                float  _Inflate;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2; // object space — gives a stable "up" for the flame rise
            };

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
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

            // Two-octave fractal noise for a richer flame edge.
            float fbm(float3 p)
            {
                float n = valueNoise(p) * 0.65;
                n += valueNoise(p * 2.03 + 11.7) * 0.35;
                return n;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Slight outward shell (scaled by intensity) so the fire sits just outside the skin and a hotter
                // burn swells a touch — a subtle heat-bloom, NOT the hard scale-punch frost uses on freeze.
                float inflate = _Inflate * _FireAmount;
                float3 inflatedOS = IN.positionOS.xyz + IN.normalOS * inflate;

                OUT.positionOS = IN.positionOS.xyz;
                OUT.positionWS = TransformObjectToWorld(inflatedOS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float amount = saturate(_FireAmount);
                if (amount <= 0.001)
                    return half4(0, 0, 0, 0);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float rim = pow(saturate(1.0 - saturate(dot(N, V))), _RimPower);

                // Flames rise: scroll the noise field DOWN in object-Y over time so the pattern appears to lick
                // upward. Hotter burns rise faster (speed scales with amount), reading as more intense.
                float t = _Time.y;
                float rise = t * _FlameSpeed * (0.6 + amount);
                float3 fp = IN.positionOS * _FlameScale + float3(0.0, -rise, 0.0);
                float flame = fbm(fp);

                // Shape the noise into licking tongues and bias coverage by how hot the burn is.
                float tongues = saturate(flame * 1.4 - (0.7 - amount * 0.4));
                float body = saturate(0.25 + 0.75 * rim) * (0.4 + 0.6 * amount);
                float coverage = saturate(tongues + body * 0.6);

                // Colour ramp: ember red at the base → orange → hot yellow-white at the flame peaks/rim.
                float heat = saturate(flame * 0.6 + rim * 0.5 + amount * 0.25);
                float3 color = lerp(_EmberColor.rgb, _FlameColor.rgb, saturate(heat * 1.6));
                color = lerp(color, _HotColor.rgb, saturate((heat - 0.55) * 2.2));

                // Bright, fast-flickering ember speckles scattered over the surface.
                float ember = valueNoise(IN.positionOS * _EmberScale + float3(0.0, -t * _EmberSpeed, t * 0.7));
                float emberMask = saturate((ember - 0.78) * 6.0) * amount;
                color += _HotColor.rgb * emberMask;

                float alpha = saturate((coverage + emberMask) * amount) * _MaxAlpha;
                return half4(color * (0.8 + amount * 0.6), alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
