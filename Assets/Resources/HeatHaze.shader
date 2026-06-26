// Task 52 — Minigun heat-shimmer haze (cosmetic only).
//
// A hand-written URP transparent shader (ShaderLab + HLSL, not Shader Graph) for the subtle rising heat-haze
// that builds up around the Minigun's muzzle during sustained fire. It is PURELY visual — it communicates "this
// weapon is working hard" and has NO tie to gameplay timing or damage (no overheat mechanic; Task 52 scope).
//
// A soft, camera-facing quad with scrolling fractal-noise alpha that wavers upward (like air shimmer over hot
// metal), masked to a soft blob by the quad UVs. Per-instance _Level (0..1, the build-up while firing) rides a
// MaterialPropertyBlock from KineticVfxPresenter, so its intensity ramps with the burst and fades when fire stops.
// Warm-gray tint — kinetic/metallic, distinct from the elemental palettes of the other heroes.
Shader "Wavekeep/HeatHaze"
{
    Properties
    {
        _Color      ("Tint",            Color) = (0.86, 0.85, 0.82, 1.0)
        _Level      ("Build-up Level",  Range(0,1)) = 0
        _NoiseScale ("Noise Scale",     Float) = 3.0
        _Speed      ("Rise Speed",      Float) = 1.8
        _MaxAlpha   ("Max Alpha",       Range(0,1)) = 0.28
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
            Name "HeatHaze"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Level;
                float  _NoiseScale;
                float  _Speed;
                float  _MaxAlpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash12(i + float2(0, 0));
                float b = hash12(i + float2(1, 0));
                float c = hash12(i + float2(0, 1));
                float d = hash12(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float n = valueNoise(p) * 0.6;
                n += valueNoise(p * 2.1 + 5.2) * 0.4;
                return n;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float level = saturate(_Level);
                if (level <= 0.001) return half4(0, 0, 0, 0);

                // Soft radial blob mask from the quad centre.
                float2 c = IN.uv - 0.5;
                float mask = saturate(1.0 - dot(c, c) * 4.0);

                // Noise wavering upward (scroll the field down in UV-Y over time).
                float t = _Time.y;
                float n = fbm(IN.uv * _NoiseScale + float2(0.0, -t * _Speed));
                float haze = saturate(n * 1.3 - 0.25);

                float alpha = mask * haze * level * _MaxAlpha;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
