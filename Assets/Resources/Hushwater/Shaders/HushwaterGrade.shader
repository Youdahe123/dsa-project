Shader "Hidden/Hushwater/Grade"
{
    Properties
    {
        _MainTex ("", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Exposure, _Vignette, _Grain, _Damage, _Breath, _Flash, _Seed, _Bloom;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float3 aces(float3 x)
            {
                return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 d = uv - 0.5;
                float r2 = dot(d, d);

                // Slight lens fringing toward the edges, stronger when hurt.
                float ca = r2 * (0.012 + _Damage * 0.03);
                float3 c;
                c.r = tex2D(_MainTex, uv - d * ca).r;
                c.g = tex2D(_MainTex, uv).g;
                c.b = tex2D(_MainTex, uv + d * ca).b;

                // Cheap glow so lantern flames and the flash bloom.
                float3 glow = 0;
                float2 px = _MainTex_TexelSize.xy * 9.0;
                [unroll] for (int k = 0; k < 8; k++)
                {
                    float a = k * 0.785398;
                    float3 s = tex2D(_MainTex, uv + float2(cos(a), sin(a)) * px).rgb;
                    float3 s2 = tex2D(_MainTex, uv + float2(cos(a), sin(a)) * px * 2.5).rgb;
                    glow += max(s - 0.65, 0) + max(s2 - 0.65, 0) * 0.6;
                }
                c += glow * (_Bloom / 8.0);

                c *= _Exposure;

                // Drowned palette: cool, teal shadows; warmth only where the lantern is.
                float lum = dot(c, float3(0.2126, 0.7152, 0.0722));
                float3 shadowTint = float3(0.86, 1.0, 1.04);
                float3 graded = lerp(lum.xxx, c, 0.78) * lerp(shadowTint, float3(1.04, 1.0, 0.94), saturate(lum * 3.0));
                c = aces(graded * 1.15);

                // Holding breath narrows the world.
                float vignette = smoothstep(0.18, 0.75, sqrt(r2) * (1.0 + _Breath * 0.35));
                c *= 1.0 - vignette * (_Vignette + _Breath * 0.35);
                c = lerp(c, float3(0.35, 0.03, 0.02), vignette * _Damage * 0.85);

                c += _Flash;
                float grain = hash(uv * _ScreenParams.xy + _Seed) - 0.5;
                c += grain * _Grain * (1.2 - lum);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
