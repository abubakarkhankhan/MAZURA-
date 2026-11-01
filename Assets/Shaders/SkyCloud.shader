Shader "Mazura/SkyCloud"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-90" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "SkyCloud"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half shade : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                // Soft fake lighting: top of the cloud is bright, the underside is a bit darker.
                float3 n = normalize(TransformObjectToWorldNormal(i.normalOS));
                o.shade = (half)lerp(0.62, 1.0, saturate(n.y * 0.5 + 0.5));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // No fog on purpose, so clouds stay visible through the forest fog.
                return half4(_Color.rgb * i.shade, _Color.a);
            }
            ENDHLSL
        }
    }
}
