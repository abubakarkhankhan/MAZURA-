Shader "Hidden/TmpCubeToEquirect"
{
    Properties { _Cube ("Cube", Cube) = "grey" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            samplerCUBE _Cube;
            float4 frag (v2f_img i) : SV_Target
            {
                float phi = (i.uv.x - 0.5) * 6.2831853;
                float theta = (i.uv.y - 0.5) * 3.1415926;
                float3 d = float3(cos(theta) * sin(phi), sin(theta), cos(theta) * cos(phi));
                float3 c = texCUBElod(_Cube, float4(d, 2)).rgb;
                return float4(c, 1);
            }
            ENDCG
        }
    }
}
