// Stilize gökyüzü: tepeden ufka yumuşak renk geçişi, ufkun altı zemin rengi, güneş yönünde parıltı ve disk.
// Ufuk rengi sis rengiyle aynı tutulur, böylece uzak arazi gökyüzüne dikişsiz karışır.
Shader "Hidden/SledSurfers/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.45, 0.65, 1, 1)
        _Horizon ("Horizon", Color) = (0.8, 0.9, 0.98, 1)
        _Ground ("Ground", Color) = (0.5, 0.6, 0.45, 1)
        _SunColor ("Sun", Color) = (1, 0.95, 0.8, 1)
        _SunDir ("Sun Direction", Vector) = (0, 0.6, 0.8, 0)
        _Glow ("Glow", Float) = 0.35
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

            half4 _Top, _Horizon, _Ground, _SunColor;
            float4 _SunDir;
            half _Glow;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half up = saturate(d.y);
                half3 sky = lerp(_Horizon.rgb, _Top.rgb, pow(up, 0.55));
                half3 col = d.y >= 0 ? sky : lerp(_Horizon.rgb, _Ground.rgb, saturate(-d.y * 6));
                float s = saturate(dot(d, normalize(_SunDir.xyz)));
                col += _SunColor.rgb * (_Glow * pow(s, 6) + 0.6 * pow(s, 90) + smoothstep(0.9993, 0.9996, s));
                return half4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
