// Task 53 — Firewall particle layers (base flames + rising embers).
//
// A hand-written URP additive shader (same authoring style as FireFx / HeatHaze) used by the Particle System
// layers that build the Pyromancer's wall of fire. It renders each particle as a soft, additive puff whose
// colour and alpha come from the Particle System's per-particle COLOR stream (so Color-over-Lifetime and the
// layer's start colour drive the look). A soft round mask from the quad UVs keeps puffs blobby rather than
// hard-edged, so a dense line of them reads as a continuous sheet of flame instead of separate sprites.
//
// Additive (Blend SrcAlpha One), no depth write — correct for layered fire glow. Purely visual.
Shader "Wavekeep/FireParticle"
{
    Properties
    {
        _Softness ("Edge Softness", Range(0.5, 8)) = 3.0
        _Boost    ("Intensity",     Float)         = 1.0
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

        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "FireParticle"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Softness;
                float _Boost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Soft round falloff from the quad centre — a blobby puff, not a hard sprite.
                float2 c = IN.uv - 0.5;
                float d = saturate(1.0 - dot(c, c) * 4.0);
                float mask = pow(d, _Softness);

                float alpha = mask * IN.color.a;
                return half4(IN.color.rgb * _Boost, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
