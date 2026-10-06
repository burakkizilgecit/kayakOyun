// Tek geçişli renk ayarı: kontrast, doygunluk, sıcaklık/renk tonu, yumuşak S eğrisi ve kenar karartma.
// Resources altında olduğu için build'e girer; GradeEffect kameraya uygular.
Shader "Hidden/SledSurfers/Grade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Contrast ("Contrast", Float) = 1.08
        _Saturation ("Saturation", Float) = 1.15
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Lift ("Lift", Float) = 0.0
        _Vignette ("Vignette", Float) = 0.25
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
            half _Contrast, _Saturation, _Lift, _Vignette;
            half4 _Tint;

            half4 frag (v2f_img i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv);
                half3 col = c.rgb * _Tint.rgb;
                half luma = dot(col, half3(0.299, 0.587, 0.114));
                col = lerp(luma.xxx, col, _Saturation);
                col = (col - 0.5) * _Contrast + 0.5 + _Lift;
                // Yumuşak S eğrisi: parlak alanlar patlamaz, gölgeler biraz derinleşir.
                col = saturate(col);
                col = col * col * (3.0 - 2.0 * col) * 0.35 + col * 0.65;
                // Kenar karartma (dikey ekranda oval)
                half2 d = (i.uv - 0.5) * half2(1.0, 0.75);
                half v = 1.0 - _Vignette * smoothstep(0.18, 0.62, dot(d, d) * 2.2);
                return half4(col * v, c.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
