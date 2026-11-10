Shader "Mazura/ParticleUnlit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 5
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ParticleUnlit"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = _Color * i.color;
                // additive blend ignores alpha, so fade the colour instead
                half add = step(_SrcBlend, 1.5h);
                c.rgb *= lerp(1.0h, c.a, add);
                return c;
            }
            ENDHLSL
        }
    }
}
