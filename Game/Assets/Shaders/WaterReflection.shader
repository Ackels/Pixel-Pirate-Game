// Reflexo na água (visão lateral): desenha, abaixo da linha d'água, a imagem espelhada do que está acima dela
// (céu, navio, ilha), ondulando e sumindo com a profundidade. A imagem vem de uma câmera de reflexo (WaterReflection.cs).
Shader "Pixel/WaterReflection"
{
    Properties
    {
        _MainTex ("Imagem da câmera de reflexo", 2D) = "black" {}
        _Tint ("Cor da água", Color) = (0.55, 0.75, 0.95, 1)
        _Strength ("Força", Range(0, 1)) = 0.45
        _Fade ("Profundidade até sumir (blocos)", Float) = 6
        _Waterline ("Linha d'água (y do mundo)", Float) = 0
        _Cam ("Câmera: centro x, y, meia largura, meia altura", Vector) = (0, 0, 15, 8)
        _FlipY ("Inverter a imagem", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tint;
                float _Strength;
                float _Fade;
                float _Waterline;
                float4 _Cam;
                float _FlipY;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 world : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(w);
                o.world = w.xy;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float depth = _Waterline - i.world.y;
                if (depth < 0) discard;
                // Ondas: o reflexo balança mais quanto mais fundo
                float t = _Time.y;
                float wave = sin(i.world.y * 7.0 + t * 2.2) * 0.05 + sin(i.world.y * 15.0 - t * 3.3) * 0.025;
                float mx = i.world.x + wave * (0.4 + depth * 0.35);
                float my = _Waterline + depth * 1.0;
                float2 uv = float2((mx - _Cam.x) / (2.0 * _Cam.z) + 0.5, (my - _Cam.y) / (2.0 * _Cam.w) + 0.5);
                if (_FlipY > 0.5) uv.y = 1.0 - uv.y;
                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) discard;
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                c.rgb = lerp(c.rgb, c.rgb * _Tint.rgb, 0.55);
                // Faixas claras de brilho na superfície
                float shine = saturate(sin(i.world.y * 22.0 + t * 1.7 + i.world.x * 0.6) * 4.0 - 3.2) * 0.25 * saturate(1 - depth / 2.0);
                float a = _Strength * saturate(1.0 - depth / _Fade);
                return half4(c.rgb + shine, saturate(a + shine));
            }
            ENDHLSL
        }
    }
}
