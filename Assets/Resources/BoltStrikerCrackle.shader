// Task 58 — Bolt Striker permanent electric-crackle emission.
//
// A hand-written URP shader (ShaderLab + HLSL, NOT Shader Graph) that adds an always-on, gold/yellow electric
// crackle across the whole Bolt Striker model. Like Task 55's Frost Warden glow it is an ADDITIVE overlay drawn
// as a second material element on each renderer, so the developer's base material is untouched and the effect
// is Bolt-Striker-only (the base PolygonFantasyCharacters materials are SHARED across Synty characters).
//
// Unlike Frost Warden's UNIFORM intensity pulse, this reads as MOVING erratic arcs: thin bright lines along an
// ANIMATED Voronoi cell boundary (the cell feature points orbit over time, so the lines crackle/travel rather
// than scroll uniformly), with TWO overlapping layers at different scales/speeds for unpredictable movement, plus
// a fast flicker. All driven by _Time in-shader — no C#, zero gameplay coupling, always on. Gold palette matches
// the Task 046 ability VFX. Separate from Task 55 / the Task 044 enemy frost shader — do not merge.
Shader "Wavekeep/BoltStrikerCrackle"
{
    Properties
    {
        [HDR] _CrackleColor ("Crackle Color", Color) = (1.0, 0.8, 0.2, 1.0)
        _CrackleSpeed   ("Crackle Speed", Float) = 1.5
        _CrackleScale   ("Crackle Scale (cell density across surface)", Float) = 8.0
        _CrackleDensity ("Crackle Density (line width / coverage)", Range(0,1)) = 0.5
        _ArcIntensity   ("Peak Arc Intensity", Float) = 1.6
        _BaseEmission   ("Idle Sheen Intensity", Range(0,1)) = 0.1
        _FlickerSpeed   ("Arc Flicker Speed", Float) = 14.0

        // Runtime-driven overall visibility [0..1], default 0 (OFF). BoltStrikerCracklePresenter pushes this via
        // a MaterialPropertyBlock so the crackle only appears as a brief flare while Bolt Striker attacks, instead
        // of covering the model permanently.
        _CrackleAmount  ("Crackle Amount (runtime)", Range(0,1)) = 0

        // Optional alpha-cutout match (copied from the base albedo) so arcs don't bleed onto cut-out holes.
        // Default white (alpha 1) clips nothing, so it's safe if left unassigned.
        _BaseMap ("Base Map (alpha cutout match)", 2D) = "white" {}
        _Cutoff  ("Alpha Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One One   // additive glow over the opaque base body
        ZWrite Off
        ZTest LEqual
        Cull Back

        Pass
        {
            Name "BoltStrikerCrackle"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _CrackleColor;
                float  _CrackleSpeed;
                float  _CrackleScale;
                float  _CrackleDensity;
                float  _ArcIntensity;
                float  _BaseEmission;
                float  _FlickerSpeed;
                float  _CrackleAmount;
                float4 _BaseMap_ST;
                float  _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            float3 hash33(float3 p)
            {
                p = float3(dot(p, float3(127.1, 311.7, 74.7)),
                           dot(p, float3(269.5, 183.3, 246.1)),
                           dot(p, float3(113.5, 271.9, 124.6)));
                return frac(sin(p) * 43758.5453);
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // Animated Voronoi: feature points ORBIT over time (phase = time + per-cell random), so cell borders
            // shift erratically — the source of the "moving arcs". Returns the gap between the nearest two cell
            // distances (small near a border → a thin line).
            float voronoiEdge(float3 p, float time)
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
                    float3 rnd = hash33(g + o);
                    float3 pnt = o + 0.5 + 0.5 * sin(time + 6.2831853 * rnd); // orbiting feature point
                    float3 r = pnt - f;
                    float d = dot(r, r);
                    if (d < d1) { d2 = d1; d1 = d; }
                    else if (d < d2) { d2 = d; }
                }
                return sqrt(d2) - sqrt(d1);
            }

            // One crackle layer → thin bright lines along the animated cell borders.
            float crackleLayer(float3 wp, float scale, float time, float width)
            {
                float edge = voronoiEdge(wp * scale, time);
                return 1.0 - smoothstep(0.0, width, edge);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Fully off (e.g. between attacks) → skip everything.
                if (_CrackleAmount <= 0.001) return half4(0, 0, 0, 0);

                // Respect the base material's alpha cut-outs (no-op with the default white map).
                half baseAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a;
                clip(baseAlpha - _Cutoff);

                float t = _Time.y;
                float3 wp = IN.positionWS;
                float width = lerp(0.02, 0.3, saturate(_CrackleDensity)); // density widens/multiplies the lines

                // Two overlapping layers at different scales/speeds/phases → erratic, non-uniform movement.
                float a1 = crackleLayer(wp, _CrackleScale,        t * _CrackleSpeed,         width);
                float a2 = crackleLayer(wp, _CrackleScale * 1.7,  t * _CrackleSpeed * 1.35 + 13.0, width * 0.8);
                float arcs = max(a1, a2);

                // Fast erratic flicker so the arcs spark rather than glide steadily.
                float flicker = 0.55 + 0.45 * sin(t * _FlickerSpeed + hash13(floor(wp * _CrackleScale)) * 6.2831853);

                float intensity = (_BaseEmission + arcs * _ArcIntensity * flicker) * _CrackleAmount;
                float3 emission = _CrackleColor.rgb * intensity;

                return half4(emission, 1.0); // additive (Blend One One) — alpha ignored
            }
            ENDHLSL
        }
    }

    Fallback Off
}
