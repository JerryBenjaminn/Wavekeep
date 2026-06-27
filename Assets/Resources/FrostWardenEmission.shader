// Task 55 — Frost Warden permanent pulsing frost emission.
//
// A hand-written URP shader (ShaderLab + HLSL, NOT Shader Graph, per the project convention) that adds an
// always-on, breathing blue/cyan EMISSION glow across the entire Frost Warden model. It is NOT a replacement
// for the character's base material — it is an ADDITIVE overlay rendered as a second material element on each
// of the model's renderers, so the developer's existing base material/tint is left completely untouched and is
// not shared/bled onto other Synty characters (the base PolygonFantasyCharacters material is shared).
//
// The pulse is driven entirely by the built-in shader time (_Time.y) — no C# animates a property per frame, so
// the effect is self-contained, has zero gameplay coupling, and is always active while the hero is on screen
// (Task 55 §2.1). It is deliberately separate from the Task 044 enemy Frost STATUS overlay (temporary debuff);
// the two systems must not be merged.
//
// Coverage is uniform across the whole body (a constant body-emission term) plus a soft fresnel rim aura, both
// pulsing together (§2.2). An optional alpha-cutout match (_BaseMap/_Cutoff) keeps the glow off the base
// material's cut-out holes; with the default white _BaseMap nothing is clipped, so it is safe if left unwired.
Shader "Wavekeep/FrostWardenEmission"
{
    Properties
    {
        [HDR] _FrostEmissionColor   ("Frost Emission Color", Color) = (0.25, 0.6, 1.0, 1.0)
        _FrostPulseMinIntensity     ("Pulse Min Intensity", Float) = 0.15
        _FrostPulseMaxIntensity     ("Pulse Max Intensity", Float) = 0.65
        _FrostPulseSpeed            ("Pulse Speed", Float) = 1.5

        _BodyEmission ("Body Base Emission (uniform coverage)", Range(0,1)) = 0.6
        _RimBoost     ("Rim Glow Boost", Range(0,3)) = 0.8
        _RimPower     ("Rim Power", Range(0.5, 8)) = 2.0

        // Optional: match the base material's alpha cutout so the glow doesn't bleed onto cut-out holes.
        // Default "white" (alpha 1) clips nothing, so this is safe to leave unassigned.
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

        // Additive glow layered on top of the opaque base body.
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Back

        Pass
        {
            Name "FrostWardenEmission"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _FrostEmissionColor;
                float  _FrostPulseMinIntensity;
                float  _FrostPulseMaxIntensity;
                float  _FrostPulseSpeed;
                float  _BodyEmission;
                float  _RimBoost;
                float  _RimPower;
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
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Keep the glow off the base material's alpha cut-outs (no-op with the default white map).
                half baseAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a;
                clip(baseAlpha - _Cutoff);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float  rim = pow(saturate(1.0 - saturate(dot(N, V))), _RimPower);

                // Smooth "breathing" pulse in [0,1] from built-in time — self-contained, no script.
                float wave = 0.5 + 0.5 * sin(_Time.y * _FrostPulseSpeed);
                float intensity = lerp(_FrostPulseMinIntensity, _FrostPulseMaxIntensity, wave);

                // Uniform whole-body emission + a rim aura, both scaled by the same pulse.
                float coverage = _BodyEmission + _RimBoost * rim;
                float3 emission = _FrostEmissionColor.rgb * intensity * coverage;

                return half4(emission, 1.0); // additive (Blend One One) — alpha is ignored
            }
            ENDHLSL
        }
    }

    Fallback Off
}
