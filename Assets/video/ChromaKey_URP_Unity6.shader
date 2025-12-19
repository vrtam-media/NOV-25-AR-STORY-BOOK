Shader "URP/Unlit/ChromaKey_Unity6"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _KeyColor("KeyColor", Color) = (0,1,0,1)

        _Cutoff("Cutoff", Range(0, 1)) = 0.25
        _Feather("Feather", Range(0.0001, 1)) = 0.20

        _Despill("Despill", Range(0, 1)) = 0.8
        _Tint ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "ChromaKey"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _KeyColor;
                float4 _Tint;
                float _Cutoff;
                float _Feather;
                float _Despill;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float rgb2cb(float3 c) { return (0.5 + -0.168736*c.r - 0.331264*c.g + 0.5*c.b); }
            float rgb2cr(float3 c) { return (0.5 +  0.5*c.r - 0.418688*c.g - 0.081312*c.b); }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                float keyCb = rgb2cb(_KeyColor.rgb);
                float keyCr = rgb2cr(_KeyColor.rgb);
                float pixCb = rgb2cb(col.rgb);
                float pixCr = rgb2cr(col.rgb);

                float d = distance(float2(pixCb, pixCr), float2(keyCb, keyCr));

                // alpha: 0 = removed (green), 1 = kept (character)
                float a = smoothstep(_Cutoff, _Cutoff + _Feather, d);

                // Despill (reduce green edge)
                float spill = (1.0 - a) * _Despill;
                float rb = max(col.r, col.b);
                col.g = lerp(col.g, rb, spill);

                col.rgb *= _Tint.rgb;
                return float4(col.rgb, a * _Tint.a);
            }
            ENDHLSL
        }
    }
}
