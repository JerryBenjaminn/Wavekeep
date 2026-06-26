// Task 51 — Pyromancer ability VFX (shared fire look).
//
// The fire counterpart to Task 45's FrostFx (hand-written URP ShaderLab + HLSL, not Shader Graph). One shader
// reused for every Pyromancer ability effect by varying its material defaults from FireVfxPresenter:
//   * Fireball projectile core   — a small, mostly-solid molten sphere with a trail.
//   * Fireball impact burst      — an expanding flame shell.
//   * Combustion / Spread bursts — small flame shells at the detonation / spread points.
//   * Firewall band              — a tall ambient wall of flame over the full-width Z-band, flaring on Inferno Surge.
//   * Wildfire cooling patch     — a dim, low ember decal left behind (no full flame).
//
// Warm red/orange/yellow palette + moving fractal-noise flames, kept deliberately distinct from FrostFx's still
// blue Voronoi crystals. Per-instance, time-varying values (_Alpha master fade, _Emission flare) ride a
// MaterialPropertyBlock, so many simultaneous effects animate independently off ONE shared material per variant.
Shader "Wavekeep/FireFx"
{
    Properties
    {
        _Color       ("Tint",            Color) = (1.0, 0.45, 0.08, 1.0)
        _HotColor    ("Hot Tint",        Color) = (1.0, 0.88, 0.4, 1.0)
        _Alpha       ("Master Alpha",    Range(0,1)) = 1
        _Emission    ("Emission Boost",  Float) = 0
        _FillBase    ("Base Coverage",   Range(0,1)) = 0.3
        _NoiseScale  ("Flame Noise Scale", Float) = 3.5
        _FlameSpeed  ("Flame Rise Speed",  Float) = 1.4
        _FresnelPower("Rim Power",       Range(0.5, 8)) = 2.2
        _FresnelBoost("Rim Boost",       Range(0, 2)) = 0.7
        _EmberAmount ("Ember Speckle Amount", Range(0,1)) = 0.4
        _SoftEdge    ("Flame Soft Edge", Range(0,1)) = 0.5
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

        Blend SrcAlpha One   // additive glow — fire emits light
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "FireFx"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _HotColor;
                float  _Alpha;
                float  _Emission;
                float  _FillBase;
                float  _NoiseScale;
                float  _FlameSpeed;
                float  _FresnelPower;
                float  _FresnelBoost;
                float  _EmberAmount;
                float  _SoftEdge;
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

            float fbm(float3 p)
            {
                float n = valueNoise(p) * 0.65;
                n += valueNoise(p * 2.03 + 7.3) * 0.35;
                return n;
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

                // Flames rise: scroll the noise field down in world-Y so the pattern licks upward.
                float t = _Time.y;
                float3 fp = IN.positionWS * _NoiseScale + float3(0.0, -t * _FlameSpeed, 0.0);
                float flame = fbm(fp);

                float coverage = saturate(_FillBase + flame * (1.0 - _SoftEdge) + rim * _FresnelBoost);

                // Hot core where the flame noise + rim peak; ember red elsewhere.
                float heat = saturate(flame * 0.7 + rim * 0.5);
                float3 color = lerp(_Color.rgb, _HotColor.rgb, saturate((heat - 0.45) * 2.2));

                // Bright flickering ember speckles (optional per variant).
                if (_EmberAmount > 0.001)
                {
                    float ember = valueNoise(IN.positionWS * (_NoiseScale * 4.0) + float3(0, -t * 4.5, t * 0.6));
                    float emberMask = saturate((ember - 0.8) * 6.0) * _EmberAmount;
                    color += _HotColor.rgb * emberMask;
                    coverage = saturate(coverage + emberMask);
                }

                color += _Emission.xxx * _HotColor.rgb;
                float alpha = saturate(coverage * _Alpha + _Emission * 0.4);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
