Shader "Mazura/SkyBlend"
{
    // Smoothly cross-fades between two cubemaps (morning -> day -> evening -> night -> midnight).
    // The whole sky can be turned with a full rotation matrix (_SkyRot), so the sun painted in the
    // sky can follow the Directional Light and the clouds turn together with it.
    Properties
    {
        _TexA ("Sky A", Cube) = "grey" {}
        _TexB ("Sky B", Cube) = "grey" {}
        _Blend ("Blend A to B", Range(0,1)) = 0
        _TintA ("Tint A", Color) = (0.5,0.5,0.5,0.5)
        _TintB ("Tint B", Color) = (0.5,0.5,0.5,0.5)
        _ExposureA ("Exposure A", Range(0,8)) = 1
        _ExposureB ("Exposure B", Range(0,8)) = 1
        _Rotation ("Rotation (yaw only, old)", Range(0,360)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            samplerCUBE _TexA;
            samplerCUBE _TexB;
            half4 _TexA_HDR;
            half4 _TexB_HDR;
            half4 _TintA;
            half4 _TintB;
            half _ExposureA;
            half _ExposureB;
            half _Blend;
            float _Rotation;
            float4x4 _SkyRot;   // set from SkyDecor; identity = no extra rotation
            float _UseSkyRot;   // 0 = old yaw-only behaviour, 1 = use _SkyRot

            struct appdata_t { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 RotateAroundYInDegrees(float3 vertex, float degrees)
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(mul(m, vertex.xz), vertex.y).xzy;
            }

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 rotated = _UseSkyRot > 0.5 ? mul((float3x3)_SkyRot, v.vertex.xyz)
                                                  : RotateAroundYInDegrees(v.vertex.xyz, _Rotation);
                o.vertex = UnityObjectToClipPos(rotated);
                o.texcoord = v.vertex.xyz;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 a = texCUBE(_TexA, i.texcoord);
                half4 b = texCUBE(_TexB, i.texcoord);
                half3 ca = DecodeHDR(a, _TexA_HDR) * _TintA.rgb * unity_ColorSpaceDouble.rgb * _ExposureA;
                half3 cb = DecodeHDR(b, _TexB_HDR) * _TintB.rgb * unity_ColorSpaceDouble.rgb * _ExposureB;
                return half4(lerp(ca, cb, _Blend), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
