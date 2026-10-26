Shader "Hushwater/Water"
{
    Properties
    {
        _Color ("Colour", Color) = (0.03, 0.07, 0.075, 1)
        _NormalTex ("Wave normals", 2D) = "bump" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.93
        _Scale ("Wave scale", Float) = 0.11
        _Speed ("Wave speed", Float) = 0.025
        _Alpha ("Opacity", Range(0, 1)) = 0.82
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        LOD 200
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:premul
        #pragma target 3.0

        sampler2D _NormalTex;
        fixed4 _Color;
        half _Glossiness, _Scale, _Speed, _Alpha;

        struct Input
        {
            float3 worldPos;
            float3 viewDir;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.worldPos.xz * _Scale;
            float3 n1 = UnpackNormal(tex2D(_NormalTex, uv + _Time.y * _Speed * float2(1.0, 0.6)));
            float3 n2 = UnpackNormal(tex2D(_NormalTex, uv * 1.7 - _Time.y * _Speed * float2(0.4, 1.0)));
            o.Normal = normalize(float3((n1.xy + n2.xy) * 0.25, 1.0));
            o.Albedo = _Color.rgb;
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
            float fresnel = pow(1.0 - saturate(dot(normalize(IN.viewDir), o.Normal)), 4.0);
            o.Alpha = lerp(_Alpha, 1.0, fresnel);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
