Shader "Huhu/InkOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _InkColor ("Pencil ink", Color) = (0.30,0.35,0.63,1)
        _InkWidth ("Outline radius in source pixels", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _InkColor;
                float _InkWidth;
            CBUFFER_END
            struct Input {float3 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct Output {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            Output Vert(Input v){Output o;o.positionCS=TransformObjectToHClip(v.positionOS);o.uv=v.uv;o.color=v.color;return o;}
            half4 Frag(Output i):SV_Target
            {
                half4 body=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                float2 step=_MainTex_TexelSize.xy*_InkWidth;
                half edge=body.a;
                // A circular, antialiased silhouette follows each original animation frame.
                [unroll] for(int n=0;n<12;n++){
                    float angle=n*0.5235987756;
                    edge=max(edge,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(cos(angle),sin(angle))*step).a);
                }
                half a=body.a+edge*(1-body.a);
                half3 rgb=(body.rgb*body.a+_InkColor.rgb*edge*(1-body.a))/max(a,0.0001);
                return half4(rgb*i.color.rgb,a*i.color.a);
            }
            ENDHLSL
        }
    }
}
